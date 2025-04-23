// File: Pulsar.Compiler/Commands/BeaconCommand.cs

using Pulsar.Compiler.Config;
using Pulsar.Compiler.Core;
using Pulsar.Compiler.Models;
using Pulsar.Compiler.Parsers;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Pulsar.Compiler.Commands
{
    public class BeaconCommand : ICommand
    {
        private readonly ILogger _logger;
        private readonly ConfigurationService _configService;

        public BeaconCommand(ILogger logger)
        {
            _logger = logger.ForContext<BeaconCommand>();
            _configService = new ConfigurationService(_logger);
        }

        public async Task<int> RunAsync(Dictionary<string, string> options)
        {
            _logger.Information("[DIAGNOSTIC] Entered beacon command handler");
            _logger.Information("Generating AOT-compatible Beacon solution...");
            
            try
            {
                var rulesPath = options.GetValueOrDefault("rules", null);
                var configPath = options.GetValueOrDefault("config", "system_config.yaml");
                var outputPath = options.GetValueOrDefault("output", ".");
                var target = options.GetValueOrDefault("target", "win-x64");
                var verbose = options.ContainsKey("verbose");

                if (string.IsNullOrEmpty(rulesPath))
                {
                    _logger.Error("Rules path not specified");
                    PrintBeaconUsage(_logger);
                    return 1;
                }

                // Parse system config
                if (!File.Exists(configPath))
                {
                    _logger.Error("System configuration file not found: {Path}", configPath);
                    return 1;
                }

                // Create output directory if it doesn't exist
                if (!Directory.Exists(outputPath))
                {
                    _logger.Information("Creating output directory: {Path}", outputPath);
                    Directory.CreateDirectory(outputPath);
                }
                else
                {
                    _logger.Information("Output directory already exists: {Path}", outputPath);
                    // Clean the output directory if it's not empty
                    var files = Directory.GetFiles(outputPath);
                    if (files.Length > 0)
                    {
                        _logger.Information("Cleaning output directory...");
                        foreach (var file in files)
                        {
                            File.Delete(file);
                        }
                    }
                }

                // Load system config using proper method
                _logger.Information("Loading system config from {Path}", configPath);
                if (verbose)
                {
                    var configContent = await File.ReadAllTextAsync(configPath);
                    _logger.Debug("Config file content:\n{Content}", configContent);
                }

                var systemConfig = await _configService.LoadSystemConfig(configPath);

                _logger.Information(
                    "System configuration loaded with {SensorCount} valid sensors: {Sensors}",
                    systemConfig.ValidSensors?.Count ?? 0,
                    string.Join(", ", systemConfig.ValidSensors ?? new List<string>())
                );

                // Ensure required sensors exist
                var requiredSensors = new List<string>
                {
                    "temperature_f",
                    "temperature_c",
                    "humidity",
                    "pressure",
                };
                _configService.EnsureRequiredSensors(systemConfig, requiredSensors);

                // Parse rules
                var parser = new DslParser();
                var rules = new List<RuleDefinition>();

                // Create compiler options with validation disabled
                var buildConfig = _configService.CreateBuildConfig(options);
                var compilerOptions = _configService.CreateCompilerOptions(buildConfig, systemConfig);
                compilerOptions.AllowInvalidSensors = true; // Bypass sensor validation for Beacon

                // Try to load rules directly first
                if (File.Exists(rulesPath))
                {
                    var content = await File.ReadAllTextAsync(rulesPath);
                    var parsedRules = parser.ParseRules(
                        content,
                        systemConfig.ValidSensors,
                        Path.GetFileName(rulesPath),
                        true
                    );
                    rules.AddRange(parsedRules);
                }
                else if (Directory.Exists(rulesPath))
                {
                    foreach (
                        var file in Directory.GetFiles(rulesPath, "*.yaml", SearchOption.AllDirectories)
                    )
                    {
                        var content = await File.ReadAllTextAsync(file);
                        var parsedRules = parser.ParseRules(
                            content,
                            systemConfig.ValidSensors,
                            Path.GetFileName(file),
                            true
                        );
                        rules.AddRange(parsedRules);
                    }
                }
                else
                {
                    _logger.Error("Rules path not found: {Path}", rulesPath);
                    return 1;
                }

                _logger.Information("Parsed {Count} rules", rules.Count);

                if (rules.Count == 0)
                {
                    // If no rules were parsed directly, try using the compilation pipeline
                    _logger.Warning(
                        "No rules were parsed directly, attempting to use compilation pipeline"
                    );
                    var pipeline = new CompilationPipeline(new AOTRuleCompiler(), new DslParser());
                    var compilationResult = pipeline.ProcessRules(rulesPath, compilerOptions);

                    if (!compilationResult.Success)
                    {
                        _logger.Error("No rules could be parsed from the specified path");
                        return 1;
                    }

                    rules = compilationResult.Rules;
                }

                // Create the Beacon-specific build config
                var beaconBuildConfig = _configService.CreateBeaconBuildConfig(options, systemConfig, rules);

                // Double-check that system config is properly set
                if (beaconBuildConfig.SystemConfig == null)
                {
                    _logger.Warning("SystemConfig is null in BeaconBuildConfig, initializing it");
                    beaconBuildConfig.SystemConfig = systemConfig;
                }

                // Ensure validSensors is populated
                if (
                    beaconBuildConfig.SystemConfig.ValidSensors == null
                    || beaconBuildConfig.SystemConfig.ValidSensors.Count == 0
                )
                {
                    _logger.Warning("ValidSensors is empty in SystemConfig, adding required sensors");
                    _configService.EnsureRequiredSensors(beaconBuildConfig.SystemConfig, requiredSensors);
                }

                var orchestrator = new BeaconBuildOrchestrator();
                var buildResult = await orchestrator.BuildBeaconAsync(beaconBuildConfig);

                if (buildResult.Success)
                {
                    _logger.Information("Beacon solution generated successfully");

                    // Use the GeneratedFiles from the BuildResult
                    if (buildResult.GeneratedFiles != null && buildResult.GeneratedFiles.Length > 0)
                    {
                        _logger.Information("Generated {Count} files:", buildResult.GeneratedFiles.Length);
                        foreach (var file in buildResult.GeneratedFiles)
                        {
                            var fileInfo = new FileInfo(file);
                            _logger.Information(
                                "  {Name} ({Size} bytes)",
                                Path.GetFileName(file),
                                fileInfo.Length
                            );
                        }
                    }
                    else
                    {
                        // Fallback to listing files in the output directory if GeneratedFiles is empty
                        var beaconRuntimeGenDir = Path.Combine(outputPath, "Beacon", "Beacon.Runtime", "Generated");
                        if (Directory.Exists(beaconRuntimeGenDir))
                        {
                            var generatedFiles = Directory.GetFiles(beaconRuntimeGenDir);
                            _logger.Information("Generated {Count} files:", generatedFiles.Length);
                            foreach (var file in generatedFiles)
                            {
                                var fileInfo = new FileInfo(file);
                                _logger.Information(
                                    "  {Name} ({Size} bytes)",
                                    Path.GetFileName(file),
                                    fileInfo.Length
                                );
                            }
                        }
                    }

                    return 0;
                }
                else
                {
                    _logger.Error("Failed to generate Beacon solution:");
                    foreach (var error in buildResult.Errors)
                    {
                        _logger.Error("  {Error}", error);
                    }
                    return 1;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error generating Beacon solution");
                return 1;
            }
        }

        private void PrintBeaconUsage(ILogger logger)
        {
            logger.Information(
                "Usage: dotnet run --project Pulsar.Compiler.csproj beacon --rules <rules-path> --config <config-path> --output <output-path> [--target <runtime-id>] [--verbose]"
            );
            logger.Information("");
            logger.Information("Options:");
            logger.Information(
                "  --rules <path>      Path to YAML rule file or directory containing rule files (required)"
            );
            logger.Information(
                "  --config <path>     Path to system configuration YAML file (default: system_config.yaml)"
            );
            logger.Information(
                "  --output <path>     Output directory for the Beacon solution (default: current directory)"
            );
            logger.Information(
                "  --target <runtime>  Target runtime identifier for AOT compilation (default: win-x64)"
            );
            logger.Information("  --verbose          Enable verbose logging");
            logger.Information("");
            logger.Information("Example:");
            logger.Information(
                "  dotnet run --project Pulsar.Compiler.csproj beacon --rules ./rules.yaml --config ./system_config.yaml --output ./output --target win-x64"
            );
        }
    }
}