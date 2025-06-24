// File: Pulsar.Compiler/Commands/ValidateCommand.cs
// Version: 1.1.0 - Enhanced error handling

using Pulsar.Compiler.Config;
using Pulsar.Compiler.Exceptions;
using Pulsar.Compiler.Models;
using Pulsar.Compiler.Parsers;
using Pulsar.Compiler.Validation;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Pulsar.Compiler.Commands
{
    /// <summary>
    /// Command for validating rules
    /// </summary>
    public class ValidateCommand : ICommand
    {
        private readonly ILogger _logger;
        private readonly ConfigurationService _configService;

        /// <summary>
        /// Creates a new validate command
        /// </summary>
        /// <param name="logger">The logger to use</param>
        public ValidateCommand(ILogger logger)
        {
            _logger = logger?.ForContext<ValidateCommand>()
                ?? throw new ArgumentNullException(nameof(logger));
            _configService = new ConfigurationService(_logger);
        }

        /// <summary>
        /// Runs the validate command
        /// </summary>
        /// <param name="options">Command options</param>
        /// <returns>Exit code (0 for success, 1 for failure)</returns>
        public async Task<int> RunAsync(Dictionary<string, string> options)
        {
            return await ErrorHandling.SafeExecuteAsync(async () =>
            {
                _logger.Information("Validating rules...");

                ErrorHandling.ValidateNotNull(options, nameof(options));
                ErrorHandling.Validate(
                    options.ContainsKey("rules"),
                    "Rules path is required",
                    new Dictionary<string, object> { ["Options"] = string.Join(", ", options.Keys) }
                );

                var systemConfig = await _configService.LoadSystemConfig(
                    options.GetValueOrDefault("config", "system_config.yaml")
                );
                var parser = new DslParser();
                var validationLevel = options.GetValueOrDefault("validation-level", "normal");

                var rules = await ParseAndValidateRules(
                    options["rules"],
                    systemConfig.ValidSensors,
                    parser,
                    validationLevel
                );

                // Enhanced v3 validation: Sensor catalog validation
                if (options.ContainsKey("catalog"))
                {
                    await ValidateSensorCatalog(options["catalog"], rules);
                }

                _logger.Information("Successfully validated {RuleCount} rules", rules.Count);
                return 0;
            },
            1, // Return error code 1 on failure
            new Dictionary<string, object>
            {
                ["Command"] = "Validate",
                ["Options"] = options != null ? string.Join(", ", options.Keys) : ""
            });
        }

        /// <summary>
        /// Parses and validates rules based on the specified validation level
        /// </summary>
        /// <param name="rulesPath">Path to the rules</param>
        /// <param name="validSensors">List of valid sensors</param>
        /// <param name="parser">The parser to use</param>
        /// <param name="validationLevel">Validation level (strict, normal, relaxed)</param>
        /// <returns>List of validated rule definitions</returns>
        private async Task<List<RuleDefinition>> ParseAndValidateRules(
            string rulesPath,
            List<string> validSensors,
            DslParser parser,
            string validationLevel
        )
        {
            return await ErrorHandling.SafeExecuteAsync(async () =>
            {
                ErrorHandling.ValidateNotNullOrEmpty(rulesPath, nameof(rulesPath));
                ErrorHandling.ValidateNotNull(validSensors, nameof(validSensors));
                ErrorHandling.ValidateNotNull(parser, nameof(parser));

                var rules = new List<RuleDefinition>();
                var ruleFiles = GetRuleFiles(rulesPath);

                foreach (var file in ruleFiles)
                {
                    var content = await File.ReadAllTextAsync(file);
                    var fileRules = parser.ParseRules(content, file);

                    // Apply validation based on level
                    switch (validationLevel.ToLower())
                    {
                        case "strict":
                            ValidateRulesStrict(fileRules);
                            break;
                        case "relaxed":
                            ValidateRulesRelaxed(fileRules);
                            break;
                        default:
                            ValidateRulesNormal(fileRules);
                            break;
                    }

                    rules.AddRange(fileRules);
                }

                return rules;
            },
            new List<RuleDefinition>(), // Return empty list on failure
            new Dictionary<string, object>
            {
                ["RulesPath"] = rulesPath,
                ["ValidationLevel"] = validationLevel,
                ["ValidSensorCount"] = validSensors?.Count ?? 0
            });
        }

        /// <summary>
        /// Validates sensor catalog and cross-references with rules
        /// </summary>
        /// <param name="catalogPath">Path to the sensor catalog file</param>
        /// <param name="rules">Rules to validate against catalog</param>
        private async Task ValidateSensorCatalog(string catalogPath, List<RuleDefinition> rules)
        {
            _logger.Information("Validating sensor catalog: {CatalogPath}", catalogPath);
            
            var validator = new SensorCatalogValidator(_logger);
            
            // Validate catalog schema and business rules
            var catalogResult = await validator.ValidateAsync(catalogPath);
            if (!catalogResult.IsValid)
            {
                throw new ValidationException(
                    "Sensor catalog validation failed",
                    new Dictionary<string, object>
                    {
                        ["CatalogPath"] = catalogPath,
                        ["Errors"] = catalogResult.Errors
                    }
                );
            }

            // Load catalog and validate rule references
            var catalogContent = await File.ReadAllTextAsync(catalogPath);
            var catalog = System.Text.Json.JsonSerializer.Deserialize<SensorCatalog>(catalogContent, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            });

            if (catalog != null)
            {
                var referenceResult = validator.ValidateRuleSensorReferences(rules, catalog);
                if (!referenceResult.IsValid)
                {
                    throw new ValidationException(
                        "Rule sensor reference validation failed",
                        new Dictionary<string, object>
                        {
                            ["CatalogPath"] = catalogPath,
                            ["RuleCount"] = rules.Count,
                            ["Errors"] = referenceResult.Errors
                        }
                    );
                }
            }

            _logger.Information("Sensor catalog validation completed successfully");
        }

        /// <summary>
        /// Gets a list of rule files from a path
        /// </summary>
        /// <param name="rulesPath">The path to get rule files from</param>
        /// <returns>List of rule file paths</returns>
        private List<string> GetRuleFiles(string rulesPath)
        {
            return ErrorHandling.SafeExecute(() =>
            {
                if (File.Exists(rulesPath))
                {
                    return new List<string> { rulesPath };
                }

                if (Directory.Exists(rulesPath))
                {
                    return Directory
                        .GetFiles(rulesPath, "*.yaml", SearchOption.AllDirectories)
                        .Concat(Directory.GetFiles(rulesPath, "*.yml", SearchOption.AllDirectories))
                        .ToList();
                }

                throw new ConfigurationException(
                    $"Rules path not found: {rulesPath}",
                    new Dictionary<string, object> { ["Path"] = rulesPath }
                );
            },
            new List<string>(), // Return empty list on failure
            new Dictionary<string, object> { ["RulesPath"] = rulesPath });
        }

        /// <summary>
        /// Validates rules using strict criteria
        /// </summary>
        /// <param name="rules">The rules to validate</param>
        private void ValidateRulesStrict(List<RuleDefinition> rules)
        {
            foreach (var rule in rules)
            {
                // Require description
                ErrorHandling.Validate(
                    !string.IsNullOrWhiteSpace(rule.Description),
                    $"Rule '{rule.Name}' missing description (required in strict mode)",
                    new Dictionary<string, object> { ["RuleName"] = rule.Name }
                );

                // Require at least one condition
                ErrorHandling.Validate(
                    (rule.Conditions?.All?.Count > 0 || rule.Conditions?.Any?.Count > 0),
                    $"Rule '{rule.Name}' must have at least one condition",
                    new Dictionary<string, object> { ["RuleName"] = rule.Name }
                );

                // Validate action complexity
                ErrorHandling.Validate(
                    rule.Actions.Count <= 5,
                    $"Rule '{rule.Name}' has too many actions (max 5 in strict mode, found {rule.Actions.Count})",
                    new Dictionary<string, object>
                    {
                        ["RuleName"] = rule.Name,
                        ["ActionCount"] = rule.Actions.Count
                    }
                );
            }
        }

        /// <summary>
        /// Validates rules using normal criteria
        /// </summary>
        /// <param name="rules">The rules to validate</param>
        private void ValidateRulesNormal(List<RuleDefinition> rules)
        {
            foreach (var rule in rules)
            {
                // Warning for missing description
                if (string.IsNullOrWhiteSpace(rule.Description))
                {
                    _logger.Warning("Rule {RuleName} missing description", rule.Name);
                }

                // Warning for high action count
                if (rule.Actions.Count > 10)
                {
                    _logger.Warning(
                        "Rule {RuleName} has a high number of actions ({Count})",
                        rule.Name,
                        rule.Actions.Count
                    );
                }
            }
        }

        /// <summary>
        /// Validates rules using relaxed criteria
        /// </summary>
        /// <param name="rules">The rules to validate</param>
        private void ValidateRulesRelaxed(List<RuleDefinition> rules)
        {
            foreach (var rule in rules)
            {
                // Minimal validation, just log warnings for potential issues
                if (string.IsNullOrWhiteSpace(rule.Description))
                {
                    _logger.Information("Rule {RuleName} missing description", rule.Name);
                }

                if (rule.Actions.Count > 15)
                {
                    _logger.Information(
                        "Rule {RuleName} has a very high number of actions ({Count})",
                        rule.Name,
                        rule.Actions.Count
                    );
                }
            }
        }
    }
}