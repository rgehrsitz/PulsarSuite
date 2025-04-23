// File: Pulsar.Compiler/Commands/GenerateCommand.cs

using Pulsar.Compiler.Config;
using Pulsar.Compiler.Core;
using Pulsar.Compiler.Models;
using Pulsar.Compiler.Parsers;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Pulsar.Compiler.Commands
{
    public class GenerateCommand : ICommand
    {
        private readonly ILogger _logger;

        public GenerateCommand(ILogger logger)
        {
            _logger = logger.ForContext<GenerateCommand>();
        }

        public async Task<int> RunAsync(Dictionary<string, string> options)
        {
            _logger.Information("Generating buildable project...");

            try
            {
                var buildConfig = CreateBuildConfig(options);
                var systemConfig = await LoadSystemConfig(
                    options.GetValueOrDefault("config", "system_config.yaml")
                );
                _logger.Information(
                    "System configuration loaded. Valid sensors: {ValidSensors}",
                    string.Join(", ", systemConfig.ValidSensors)
                );

                // Use the new CompilationPipeline instead of BuildTimeOrchestrator.
                var compilerOptions = new Models.CompilerOptions
                {
                    BuildConfig = buildConfig,
                    ValidSensors = systemConfig.ValidSensors.ToList(),
                };
                var pipeline = new Core.CompilationPipeline(
                    new Core.AOTRuleCompiler(),
                    new Parsers.DslParser()
                );
                var result = pipeline.ProcessRules(options["rules"], compilerOptions);
                if (!result.Success)
                {
                    foreach (var error in result.Errors)
                    {
                        _logger.Error(error);
                    }
                    return 1;
                }

                // Generate project files
                var templateManager = new TemplateManager();
                templateManager.GenerateProjectFiles(buildConfig.OutputPath, buildConfig);

                return 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to generate buildable project");
                return 1;
            }
        }

        private BuildConfig CreateBuildConfig(Dictionary<string, string> options)
        {
            return new BuildConfig
            {
                OutputPath = options.GetValueOrDefault("output", "Generated"),
                Target = options.GetValueOrDefault("target", "win-x64"),
                ProjectName = "Generated",
                TargetFramework = options.GetValueOrDefault("targetframework", "net9.0"),
                RulesPath = options.GetValueOrDefault("rules", "Rules"),
                MaxRulesPerFile = int.Parse(options.GetValueOrDefault("max-rules", "100")),
                GenerateDebugInfo = options.GetValueOrDefault("debug") == "true",
                StandaloneExecutable = true,
                Namespace = "Generated",
                GroupParallelRules = options.GetValueOrDefault("parallel") == "true",
                OptimizeOutput = options.GetValueOrDefault("aot") == "true",
                ComplexityThreshold = int.Parse(
                    options.GetValueOrDefault("complexity-threshold", "100")
                ),
            };
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
            var deserializer = new YamlDotNet.Serialization.DeserializerBuilder().Build();
            return deserializer.Deserialize<SystemConfig>(yaml);
        }
    }
}