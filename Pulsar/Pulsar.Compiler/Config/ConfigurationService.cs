// File: Pulsar.Compiler/Config/ConfigurationService.cs

using Pulsar.Compiler.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Pulsar.Compiler.Config
{
    /// <summary>
    /// Centralized service for loading and managing configuration.
    /// </summary>
    public class ConfigurationService
    {
        private readonly ILogger _logger;

        public ConfigurationService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Loads a build configuration from command-line options.
        /// </summary>
        public BuildConfig CreateBuildConfig(Dictionary<string, string> options)
        {
            return new BuildConfig
            {
                OutputPath = options.GetValueOrDefault("output", "Generated"),
                Target = options.GetValueOrDefault("target", "win-x64"),
                ProjectName = options.GetValueOrDefault("project-name", "Generated"),
                AssemblyName = options.GetValueOrDefault("assembly-name", "Generated"),
                TargetFramework = options.GetValueOrDefault("targetframework", "net9.0"),
                RulesPath = options.GetValueOrDefault("rules", "Rules"),
                MaxRulesPerFile = int.Parse(options.GetValueOrDefault("max-rules", "100")),
                GenerateDebugInfo = options.GetValueOrDefault("debug") == "true",
                StandaloneExecutable = options.GetValueOrDefault("standalone", "true") == "true",
                Namespace = options.GetValueOrDefault("namespace", "Generated"),
                GroupParallelRules = options.GetValueOrDefault("parallel") == "true",
                OptimizeOutput = options.GetValueOrDefault("aot") == "true",
                ComplexityThreshold = int.Parse(
                    options.GetValueOrDefault("complexity-threshold", "100")
                ),
                MaxLinesPerFile = int.Parse(
                    options.GetValueOrDefault("max-lines", "1000")
                ),
                CreateSeparateDirectory = options.GetValueOrDefault("separate-dir") == "true",
                GenerateTestProject = options.GetValueOrDefault("generate-tests") == "true",
            };
        }

        /// <summary>
        /// Loads a beacon-specific build configuration.
        /// </summary>
        public BuildConfig CreateBeaconBuildConfig(Dictionary<string, string> options, SystemConfig systemConfig, List<RuleDefinition> rules)
        {
            string outputPath = options.GetValueOrDefault("output", ".");
            string target = options.GetValueOrDefault("target", "win-x64");
            string compiledRulesDir = options.GetValueOrDefault("compiled-rules-dir", "");

            _logger.Debug("Creating Beacon build config with: outputPath={OutputPath}, target={Target}",
                outputPath, target);

            return new BuildConfig
            {
                OutputPath = outputPath,
                Target = target,
                ProjectName = "Beacon.Runtime",
                AssemblyName = "Beacon.Runtime",
                TargetFramework = "net9.0",
                RulesPath = options.GetValueOrDefault("rules", "Rules"),
                RuleDefinitions = rules,
                SystemConfig = systemConfig,
                StandaloneExecutable = true,
                GenerateDebugInfo = false,
                OptimizeOutput = true,
                Namespace = "Beacon.Runtime",
                RedisConnection =
                    systemConfig.Redis != null && systemConfig.Redis.TryGetValue("endpoints", out var endpoints)
                        && endpoints is List<string> endpointList && endpointList.Count > 0
                            ? endpointList[0].ToString()
                            : "localhost:6379",
                CycleTime = systemConfig.CycleTime,
                BufferCapacity = systemConfig.BufferCapacity,
                MaxRulesPerFile = 50,
                MaxLinesPerFile = 1000,
                ComplexityThreshold = 10,
                GroupParallelRules = true,
                GenerateTestProject = true,
                CreateSeparateDirectory = true,
                SolutionName = "Beacon",
                CompiledRulesDir = compiledRulesDir,
            };
        }

        /// <summary>
        /// Loads a SystemConfig from the specified file path.
        /// </summary>
        public async Task<SystemConfig> LoadSystemConfig(string configPath)
        {
            _logger.Debug("Loading system config from {Path}", configPath);

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
                    _logger.Debug("Using config from parent directory: {Path}", parentPath);
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
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var config = deserializer.Deserialize<SystemConfig>(yaml);

            // Validate and set defaults
            if (config.ValidSensors == null || config.ValidSensors.Count == 0)
            {
                _logger.Warning("ValidSensors is null or empty in system config, auto-populating from all rule files in 'rules/' directory");
                var rulesDir = Path.Combine(AppContext.BaseDirectory, "../../../../../../rules");
                if (!Directory.Exists(rulesDir))
                {
                    rulesDir = Path.Combine(Directory.GetCurrentDirectory(), "rules");
                }
                if (Directory.Exists(rulesDir))
                {
                    var allRuleFiles = Directory.GetFiles(rulesDir, "*.yaml", SearchOption.AllDirectories);
                    var allSensors = new HashSet<string>();
                    var parser = new Pulsar.Compiler.Parsers.DslParser();
                    foreach (var ruleFile in allRuleFiles)
                    {
                        try
                        {
                            var ruleYaml = File.ReadAllText(ruleFile);
                            var rules = parser.ParseRules(ruleYaml, new List<string>(), Path.GetFileName(ruleFile), true);
                            foreach (var rule in rules)
                            {
                                if (rule.InputSensors != null)
                                    foreach (var s in rule.InputSensors)
                                        allSensors.Add(s);
                                if (rule.OutputSensors != null)
                                    foreach (var s in rule.OutputSensors)
                                        allSensors.Add(s);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Warning(ex, $"Failed to parse rule file {ruleFile} for sensor extraction");
                        }
                    }
                    config.ValidSensors = allSensors.ToList();
                    _logger.Information("Auto-populated validSensors from rules: {Sensors}", string.Join(", ", config.ValidSensors));
                }
                else
                {
                    _logger.Warning("Could not find 'rules/' directory to auto-populate validSensors");
                    config.ValidSensors = new List<string>();
                }
            }

            if (config.CycleTime <= 0)
            {
                _logger.Warning("Invalid cycle time {CycleTime}ms, using default 100ms", config.CycleTime);
                config.CycleTime = 100;
            }

            if (config.BufferCapacity <= 0)
            {
                _logger.Warning("Invalid buffer capacity {BufferCapacity}, using default 100", config.BufferCapacity);
                config.BufferCapacity = 100;
            }

            _logger.Information("Successfully loaded system config with {SensorCount} valid sensors",
                config.ValidSensors.Count);

            return config;
        }

        /// <summary>
        /// Ensures required sensors exist in the system configuration.
        /// </summary>
        public void EnsureRequiredSensors(SystemConfig config, IEnumerable<string> requiredSensors)
        {
            if (config.ValidSensors == null)
            {
                config.ValidSensors = new List<string>();
                _logger.Warning("ValidSensors was null, creating new empty list");
            }

            foreach (var sensor in requiredSensors)
            {
                if (!config.ValidSensors.Contains(sensor))
                {
                    config.ValidSensors.Add(sensor);
                    _logger.Debug("Added required sensor: {Sensor}", sensor);
                }
            }
        }

        /// <summary>
        /// Creates compiler options from build config and system config.
        /// </summary>
        public CompilerOptions CreateCompilerOptions(BuildConfig buildConfig, SystemConfig systemConfig)
        {
            return new CompilerOptions
            {
                BuildConfig = buildConfig,
                ValidSensors = systemConfig.ValidSensors.ToList(),
                AllowInvalidSensors = false,
                OutputDirectory = buildConfig.OutputPath,
                TargetFramework = buildConfig.TargetFramework,
                RuntimeIdentifier = buildConfig.Target,
            };
        }
    }
}