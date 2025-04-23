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
        private readonly ConfigurationService _configService;

        public GenerateCommand(ILogger logger)
        {
            _logger = logger.ForContext<GenerateCommand>();
            _configService = new ConfigurationService(_logger);
        }

        public async Task<int> RunAsync(Dictionary<string, string> options)
        {
            _logger.Information("Generating buildable project...");

            try
            {
                var buildConfig = _configService.CreateBuildConfig(options);
                var systemConfig = await _configService.LoadSystemConfig(
                    options.GetValueOrDefault("config", "system_config.yaml")
                );
                _logger.Information(
                    "System configuration loaded. Valid sensors: {ValidSensors}",
                    string.Join(", ", systemConfig.ValidSensors)
                );

                // Use the new CompilationPipeline instead of BuildTimeOrchestrator.
                var compilerOptions = _configService.CreateCompilerOptions(buildConfig, systemConfig);
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
                var templateManager = new TemplateManager(_logger);
                templateManager.GenerateProjectFiles(buildConfig.OutputPath, buildConfig);

                return 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to generate buildable project");
                return 1;
            }
        }

        // Configuration methods moved to ConfigurationService
    }
}