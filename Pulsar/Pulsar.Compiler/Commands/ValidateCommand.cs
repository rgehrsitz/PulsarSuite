// File: Pulsar.Compiler/Commands/ValidateCommand.cs

using Pulsar.Compiler.Models;
using Pulsar.Compiler.Parsers;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Pulsar.Compiler.Commands
{
    public class ValidateCommand : ICommand
    {
        private readonly ILogger _logger;

        public ValidateCommand(ILogger logger) 
        {
            _logger = logger.ForContext<ValidateCommand>();
        }

        public async Task<int> RunAsync(Dictionary<string, string> options)
        {
            _logger.Information("Validating rules...");

            var systemConfig = await LoadSystemConfig(
                options.GetValueOrDefault("config", "system_config.yaml")
            );
            var parser = new DslParser();
            var validationLevel = options.GetValueOrDefault("validation-level", "normal");

            try
            {
                var rules = await ParseAndValidateRules(
                    options["rules"],
                    systemConfig.ValidSensors,
                    parser,
                    validationLevel,
                    _logger
                );

                _logger.Information("Successfully validated {RuleCount} rules", rules.Count);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Rule validation failed");
                return 1;
            }
        }

        private async Task<SystemConfig> LoadSystemConfig(string configPath)
        {
            if (!File.Exists(configPath))
            {
                // Try looking in the parent directory
                string parentPath = Path.Combine(
                    Directory.GetParent(Directory.GetCurrentDirectory())?.FullName
                        ?? throw new InvalidOperationException("Parent directory not found"),
                    configPath
                );
                if (File.Exists(parentPath))
                {
                    configPath = parentPath;
                }
                else
                {
                    throw new FileNotFoundException(
                        $"System configuration file not found: {configPath}"
                    );
                }
            }

            var yaml = await File.ReadAllTextAsync(configPath);
            var deserializer = new DeserializerBuilder().Build();
            return deserializer.Deserialize<SystemConfig>(yaml);
        }

        private async Task<List<RuleDefinition>> ParseAndValidateRules(
            string rulesPath,
            List<string> validSensors,
            DslParser parser,
            string validationLevel,
            ILogger logger
        )
        {
            var rules = new List<RuleDefinition>();
            var ruleFiles = GetRuleFiles(rulesPath);

            foreach (var file in ruleFiles)
            {
                var content = await File.ReadAllTextAsync(file);
                var fileRules = parser.ParseRules(content, validSensors, file);

                // Apply validation based on level
                switch (validationLevel.ToLower())
                {
                    case "strict":
                        ValidateRulesStrict(fileRules, logger);
                        break;
                    case "relaxed":
                        ValidateRulesRelaxed(fileRules, logger);
                        break;
                    default:
                        ValidateRulesNormal(fileRules, logger);
                        break;
                }

                rules.AddRange(fileRules);
            }

            return rules;
        }

        private List<string> GetRuleFiles(string rulesPath)
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

            throw new ArgumentException($"Rules path not found: {rulesPath}");
        }

        private void ValidateRulesStrict(List<RuleDefinition> rules, ILogger logger)
        {
            foreach (var rule in rules)
            {
                // Require description
                if (string.IsNullOrWhiteSpace(rule.Description))
                {
                    throw new ArgumentException(
                        $"Rule {rule.Name} missing description (required in strict mode)"
                    );
                }

                // Require at least one condition
                if (rule.Conditions?.All?.Count == 0 && rule.Conditions?.Any?.Count == 0)
                {
                    throw new ArgumentException($"Rule {rule.Name} must have at least one condition");
                }

                // Validate action complexity
                if (rule.Actions.Count > 5)
                {
                    throw new ArgumentException(
                        $"Rule {rule.Name} has too many actions (max 5 in strict mode)"
                    );
                }
            }
        }

        private void ValidateRulesNormal(List<RuleDefinition> rules, ILogger logger)
        {
            foreach (var rule in rules)
            {
                // Warning for missing description
                if (string.IsNullOrWhiteSpace(rule.Description))
                {
                    logger.Warning("Rule {RuleName} missing description", rule.Name);
                }

                // Warning for high action count
                if (rule.Actions.Count > 10)
                {
                    logger.Warning(
                        "Rule {RuleName} has a high number of actions ({Count})",
                        rule.Name,
                        rule.Actions.Count
                    );
                }
            }
        }

        private void ValidateRulesRelaxed(List<RuleDefinition> rules, ILogger logger)
        {
            foreach (var rule in rules)
            {
                // Minimal validation, just log warnings for potential issues
                if (string.IsNullOrWhiteSpace(rule.Description))
                {
                    logger.Information("Rule {RuleName} missing description", rule.Name);
                }

                if (rule.Actions.Count > 15)
                {
                    logger.Information(
                        "Rule {RuleName} has a very high number of actions ({Count})",
                        rule.Name,
                        rule.Actions.Count
                    );
                }
            }
        }
    }
}