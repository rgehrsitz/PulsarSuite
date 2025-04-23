// File: Pulsar.Compiler/Core/CompilationPipeline.cs
// Version: 1.1.0 - Enhanced error handling

using Pulsar.Compiler.Exceptions;
using Pulsar.Compiler.Models;
using Pulsar.Compiler.Parsers;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Pulsar.Compiler.Core
{
    /// <summary>
    /// Manages the pipeline for compiling rules from various sources
    /// </summary>
    public class CompilationPipeline
    {
        private readonly IRuleCompiler _compiler;
        private readonly DslParser _parser;
        private readonly ILogger _logger;

        /// <summary>
        /// Creates a new compilation pipeline
        /// </summary>
        /// <param name="compiler">The rule compiler to use</param>
        /// <param name="parser">The DSL parser to use</param>
        public CompilationPipeline(IRuleCompiler compiler, DslParser parser)
        {
            _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _logger = LoggingConfig.GetLogger();
        }

        /// <summary>
        /// Processes rules from a path and compiles them
        /// </summary>
        /// <param name="rulesPath">The path to the rules</param>
        /// <param name="options">Compiler options</param>
        /// <returns>The compilation result</returns>
        public CompilationResult ProcessRules(string rulesPath, CompilerOptions options)
        {
            return ErrorHandling.SafeExecute(() =>
            {
                ErrorHandling.ValidateNotNullOrEmpty(rulesPath, nameof(rulesPath));
                ErrorHandling.ValidateNotNull(options, nameof(options));

                _logger.Information("Starting rule compilation pipeline for {Path}", rulesPath);

                var rules = LoadRulesFromPaths(
                    rulesPath,
                    options.ValidSensors,
                    options.AllowInvalidSensors
                );
                _logger.Information("Loaded {Count} rules from {Path}", rules.Count, rulesPath);

                var result = _compiler.Compile(rules.ToArray(), options);
                if (result.Success)
                {
                    _logger.Information("Successfully compiled {Count} rules", rules.Count);
                    result.Rules = rules;
                }
                else
                {
                    _logger.Error(
                        "Rule compilation failed with {Count} errors",
                        result.Errors.Count
                    );
                }

                return result;
            }, 
            new CompilationResult
            {
                Success = false,
                Errors = new List<string> { "An error occurred during compilation. See logs for details." }
            },
            new Dictionary<string, object>
            {
                ["RulesPath"] = rulesPath,
                ["OptionsType"] = options?.GetType().Name ?? "null"
            });
        }

        /// <summary>
        /// Processes predefined rules and compiles them
        /// </summary>
        /// <param name="rules">The rules to compile</param>
        /// <param name="options">Compiler options</param>
        /// <returns>The compilation result</returns>
        public CompilationResult ProcessRules(List<RuleDefinition> rules, CompilerOptions options)
        {
            return ErrorHandling.SafeExecute(() =>
            {
                ErrorHandling.ValidateNotNull(rules, nameof(rules));
                ErrorHandling.ValidateNotNull(options, nameof(options));

                _logger.Information(
                    "Starting rule compilation pipeline for {Count} predefined rules",
                    rules.Count
                );

                var result = _compiler.Compile(rules.ToArray(), options);
                if (result.Success)
                {
                    _logger.Information("Successfully compiled {Count} rules", rules.Count);
                    result.Rules = rules;
                }
                else
                {
                    _logger.Error(
                        "Rule compilation failed with {Count} errors",
                        result.Errors.Count
                    );
                }

                return result;
            },
            new CompilationResult
            {
                Success = false,
                Errors = new List<string> { "An error occurred during compilation. See logs for details." }
            },
            new Dictionary<string, object>
            {
                ["RuleCount"] = rules?.Count ?? 0,
                ["OptionsType"] = options?.GetType().Name ?? "null"
            });
        }

        /// <summary>
        /// Loads rules from a file or directory path
        /// </summary>
        /// <param name="rulesPath">The path to load rules from</param>
        /// <param name="validSensors">List of valid sensors</param>
        /// <param name="allowInvalidSensors">Whether to allow invalid sensors</param>
        /// <returns>List of rule definitions</returns>
        private List<RuleDefinition> LoadRulesFromPaths(
            string rulesPath,
            List<string> validSensors,
            bool allowInvalidSensors
        )
        {
            var rules = new List<RuleDefinition>();
            
            try
            {
                // Ensure validSensors is not null
                validSensors ??= new List<string>();
                
                _logger.Information(
                    "Loading rules with {Count} valid sensors: {Sensors}",
                    validSensors.Count,
                    string.Join(", ", validSensors)
                );

                // Manually add required sensors if not already in the list
                var requiredSensors = new List<string>
                {
                    "temperature_f",
                    "temperature_c",
                    "humidity",
                    "pressure",
                };
                
                foreach (var sensor in requiredSensors.Where(s => !validSensors.Contains(s)))
                {
                    validSensors.Add(sensor);
                    _logger.Information("Added required sensor: {Sensor}", sensor);
                }

                if (Directory.Exists(rulesPath))
                {
                    _logger.Debug("Loading rules from directory: {Path}", rulesPath);
                    var files = Directory.GetFiles(
                        rulesPath,
                        "*.yaml",
                        SearchOption.AllDirectories
                    );
                    
                    foreach (var file in files)
                    {
                        try
                        {
                            var content = File.ReadAllText(file);
                            var parsedRules = _parser.ParseRules(
                                content,
                                validSensors,
                                Path.GetFileName(file),
                                allowInvalidSensors
                            );
                            
                            rules.AddRange(parsedRules);
                            
                            foreach (var rule in parsedRules)
                            {
                                _logger.Debug(
                                    "Loaded rule '{Name}' from {File} with {InputCount} inputs and {OutputCount} outputs",
                                    rule.Name,
                                    file,
                                    rule.InputSensors?.Count ?? 0,
                                    rule.OutputSensors?.Count ?? 0
                                );
                            }
                        }
                        catch (ValidationException)
                        {
                            // We want to let ValidationExceptions bubble up
                            throw;
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex, "Error parsing rule file: {Path}", file);
                        }
                    }
                }
                else if (File.Exists(rulesPath))
                {
                    _logger.Debug("Loading rules from file: {Path}", rulesPath);
                    var content = File.ReadAllText(rulesPath);
                    var parsedRules = _parser.ParseRules(
                        content,
                        validSensors,
                        Path.GetFileName(rulesPath),
                        allowInvalidSensors
                    );
                    
                    rules.AddRange(parsedRules);
                    
                    foreach (var rule in parsedRules)
                    {
                        _logger.Debug(
                            "Loaded rule '{Name}' from {File} with {InputCount} inputs and {OutputCount} outputs",
                            rule.Name,
                            rulesPath,
                            rule.InputSensors?.Count ?? 0,
                            rule.OutputSensors?.Count ?? 0
                        );
                    }
                }
                else
                {
                    throw new ConfigurationException(
                        $"Rules path not found: {rulesPath}",
                        new Dictionary<string, object> { ["Path"] = rulesPath }
                    );
                }
            }
            catch (ValidationException)
            {
                // Let ValidationExceptions bubble up
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error loading rules from {Path}", rulesPath);
                // Return empty list on error
            }
            
            return rules;
        }
    }
}
