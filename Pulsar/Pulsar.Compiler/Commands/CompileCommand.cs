// File: Pulsar.Compiler/Commands/CompileCommand.cs

using Pulsar.Compiler.Config;
using Pulsar.Compiler.Core;
using Pulsar.Compiler.Models;
using Pulsar.Compiler.Parsers;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Pulsar.Compiler.Commands
{
    public class CompileCommand : ICommand
    {
        private readonly ILogger _logger;
        private readonly ConfigurationService _configService;

        public CompileCommand(ILogger logger)
        {
            _logger = logger.ForContext<CompileCommand>();
            _configService = new ConfigurationService(_logger);
        }

        public async Task<int> RunAsync(Dictionary<string, string> options)
        {
            _logger.Information("Starting rule compilation...");

            var buildConfig = _configService.CreateBuildConfig(options);
            var systemConfig = await _configService.LoadSystemConfig(
                options.GetValueOrDefault("config", "system_config.yaml")
            );

            var compilerOptions = _configService.CreateCompilerOptions(buildConfig, systemConfig);
            var pipeline = new CompilationPipeline(new AOTRuleCompiler(), new DslParser());
            var result = pipeline.ProcessRules(options["rules"], compilerOptions);

            // Generate detailed output based on compilation result
            if (result.Success)
            {
                _logger.Information(
                    $"Successfully generated {result.GeneratedFiles.Length} files from rules"
                );

                // Log any optimizations or special handling
                if (options.GetValueOrDefault("aot") == "true")
                {
                    _logger.Information("Generated AOT-compatible code");
                }

                if (options.GetValueOrDefault("debug") == "true")
                {
                    _logger.Information("Included debug symbols and enhanced logging");
                }

                try
                {
                    var ruleManifest = new RuleManifest { GeneratedAt = DateTime.UtcNow };
                    ruleManifest.BuildMetrics.TotalRules = result.Rules?.Count ?? 0;
                    
                    foreach (var rule in result.Rules ?? new List<RuleDefinition>())
                    {
                        ruleManifest.Rules[rule.Name] = new RuleMetadata
                        {
                            SourceFile = rule.SourceFile,
                            SourceLineNumber = rule.LineNumber,
                            Description = rule.Description,
                            InputSensors = rule.InputSensors ?? new List<string>(),
                            OutputSensors = rule.OutputSensors ?? new List<string>(),
                        };
                    }
                    
                    var manifestPath = Path.Combine(buildConfig.OutputPath, "rules.manifest.json");
                    var manifestDir = Path.GetDirectoryName(manifestPath);
                    
                    if (!string.IsNullOrEmpty(manifestDir) && !Directory.Exists(manifestDir))
                    {
                        Directory.CreateDirectory(manifestDir);
                    }
                    
                    var manifestJson = JsonSerializer.Serialize(
                        ruleManifest,
                        new JsonSerializerOptions { WriteIndented = true }
                    );
                    
                    File.WriteAllText(manifestPath, manifestJson);
                    _logger.Information("Created rule manifest at {Path}", manifestPath);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to generate rules.manifest.json");
                }

                return 0;
            }
            else
            {
                foreach (var error in result.Errors)
                {
                    _logger.Error(error);
                }
                return 1;
            }
        }

        // Configuration methods moved to ConfigurationService
    }
}