// File: Pulsar.Compiler/Program.cs

using Pulsar.Compiler.Commands;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Pulsar.Compiler
{
    public class Program
    {
        private static readonly ILogger _logger = LoggingConfig.GetLogger();

        public static async Task<int> Main(string[] args)
        {
            try
            {
                var options = ParseArguments(args);

                if (options.Count == 0 || !options.ContainsKey("command"))
                {
                    PrintUsage(_logger);
                    return 1;
                }

                var commandName = options["command"];

                try
                {
                    ValidateRequiredOptions(options);
                }
                catch (ArgumentException ex)
                {
                    _logger.Error(ex.Message);

                    // Print specific usage based on the command
                    switch (commandName)
                    {
                        case "beacon":
                            PrintBeaconUsage(_logger);
                            break;
                        default:
                            PrintUsage(_logger);
                            break;
                    }

                    return 1;
                }

                try
                {
                    var factory = new CommandFactory(_logger);
                    var command = factory.CreateCommand(commandName);
                    return await command.RunAsync(options);
                }
                catch (ArgumentException ex) 
                {
                    _logger.Error(ex.Message);
                    PrintUsage(_logger);
                    return 1;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unhandled exception occurred.");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static void PrintUsage(ILogger logger)
        {
            logger.Information("Pulsar Compiler v3.0 - Rules-based Data Processing Platform");
            logger.Information("");
            logger.Information("Usage: Pulsar.Compiler <command> [options]");
            logger.Information("");
            logger.Information("Commands:");
            logger.Information("  compile  - Compile rules into a deployable project");
            logger.Information("  validate - Validate rules and sensor catalogs");
            logger.Information("  test     - Test Pulsar rules and configuration files");
            logger.Information("  init     - Initialize a new project");
            logger.Information("  generate - Generate a buildable project");
            logger.Information("  beacon   - Generate an AOT-compatible Beacon solution");
            logger.Information("");
            logger.Information("Global Options:");
            logger.Information("  --rules=<path>      YAML rule file or directory (required for most commands)");
            logger.Information("  --config=<path>     System configuration file (default: system_config.yaml)");
            logger.Information("  --catalog=<path>    Sensor catalog JSON file for validation");
            logger.Information("  --output=<path>     Output directory (required for compile/generate/beacon)");
            logger.Information("  --target=<id>       Target runtime identifier (win-x64, linux-x64, osx-x64)");
            logger.Information("");
            logger.Information("Validation Options:");
            logger.Information("  --validation-level=<level>  Validation strictness (strict, normal, relaxed)");
            logger.Information("  --lint                      Enable compiler linting");
            logger.Information("  --fail-on-warnings          Treat warnings as errors");
            logger.Information("  --lint-level=<level>        Linting level (info, warn, error)");
            logger.Information("");
            logger.Information("Output Options:");
            logger.Information("  --generate-metadata         Generate UI metadata files");
            logger.Information("  --emit-sourcemap           Include source maps in output");
            logger.Information("  --verbose                   Enable verbose logging");
            logger.Information("  --debug                     Include debug information");
            logger.Information("");
            logger.Information("Examples:");
            logger.Information("  # Validate rules with sensor catalog");
            logger.Information("  Pulsar.Compiler validate --rules=rules.yaml --catalog=sensors.json");
            logger.Information("");
            logger.Information("  # Generate Beacon with strict validation");
            logger.Information("  Pulsar.Compiler beacon --rules=rules.yaml --catalog=sensors.json \\");
            logger.Information("    --output=./output --validation-level=strict --generate-metadata");
            logger.Information("");
            logger.Information("  # Compile with linting");
            logger.Information("  Pulsar.Compiler compile --rules=rules.yaml --lint --fail-on-warnings");
        }

        private static Dictionary<string, string> ParseArguments(string[] args)
        {
            var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (args.Length == 0)
            {
                return options;
            }

            // First argument is the command
            options["command"] = args[0];

            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i];

                // Handle different argument formats (--key=value, --key value, or --flag)
                if (arg.StartsWith("--"))
                {
                    string key = arg.Substring(2);

                    // Handle --key=value format
                    if (key.Contains("="))
                    {
                        var parts = key.Split('=', 2);
                        options[parts[0]] = parts[1];
                        continue;
                    }

                    // Handle flags without values
                    if (IsFlagOption(key))
                    {
                        options[key] = "true";
                        continue;
                    }

                    // Handle --key value format
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                    {
                        options[key] = args[++i];
                    }
                    else
                    {
                        // If no value is provided and it's not a flag, use an empty string
                        options[key] = "";
                    }
                }
                else
                {
                    // Non-option arguments (not starting with --)
                    if (i == 0)
                    {
                        options["command"] = arg;
                    }
                    else
                    {
                        // Handle positional arguments if needed
                        _logger.Warning("Ignoring unexpected positional argument: {Arg}", arg);
                    }
                }
            }

            return options;
        }

        private static bool IsFlagOption(string option)
        {
            return option switch
            {
                "aot" or "debug" or "parallel" or "emit-sourcemap" or "verbose" or "clean" or
                "lint" or "fail-on-warnings" or "generate-metadata" => true,
                _ => false,
            };
        }

        private static void ValidateRequiredOptions(Dictionary<string, string> options)
        {
            // Validate based on command context
            var command = options.GetValueOrDefault("command", "compile");

            switch (command)
            {
                case "compile":
                    if (!options.ContainsKey("rules"))
                    {
                        throw new ArgumentException("--rules argument is required for compilation");
                    }
                    ValidateCompileOptions(options);
                    break;

                case "validate":
                    if (!options.ContainsKey("rules"))
                    {
                        throw new ArgumentException("--rules argument is required for validation");
                    }
                    break;

                case "test":
                    if (!options.ContainsKey("rules"))
                    {
                        throw new ArgumentException("--rules argument is required for testing");
                    }
                    break;

                case "beacon":
                    if (!options.ContainsKey("rules"))
                    {
                        throw new ArgumentException(
                            "--rules argument is required for Beacon solution generation"
                        );
                    }
                    // Validate target runtime if specified
                    if (options.TryGetValue("target", out var target))
                    {
                        if (!IsValidTarget(target))
                        {
                            throw new ArgumentException($"Invalid target runtime: {target}");
                        }
                    }
                    break;
            }
        }

        private static void ValidateCompileOptions(Dictionary<string, string> options)
        {
            // Validate target runtime if specified
            if (options.TryGetValue("target", out var target))
            {
                if (!IsValidTarget(target))
                {
                    throw new ArgumentException($"Invalid target runtime: {target}");
                }
            }

            // Validate validation level if specified
            if (options.TryGetValue("validation-level", out var level))
            {
                if (!IsValidValidationLevel(level))
                {
                    throw new ArgumentException($"Invalid validation level: {level}");
                }
            }

            // Validate lint level if specified
            if (options.TryGetValue("lint-level", out var lintLevel))
            {
                if (!IsValidLintLevel(lintLevel))
                {
                    throw new ArgumentException($"Invalid lint level: {lintLevel}. Valid options: info, warn, error");
                }
            }

            // Validate catalog file exists if specified
            if (options.TryGetValue("catalog", out var catalogPath))
            {
                if (!File.Exists(catalogPath))
                {
                    throw new ArgumentException($"Sensor catalog file not found: {catalogPath}");
                }
            }

            // Validate numeric options
            if (options.TryGetValue("max-rules", out var maxRules))
            {
                if (!int.TryParse(maxRules, out var value) || value <= 0)
                {
                    throw new ArgumentException("max-rules must be a positive integer");
                }
            }
        }

        private static bool IsValidTarget(string target)
        {
            return new[] { "linux-x64", "win-x64", "osx-x64" }.Contains(target);
        }

        private static bool IsValidValidationLevel(string level)
        {
            return new[] { "strict", "normal", "relaxed" }.Contains(level);
        }

        private static bool IsValidLintLevel(string level)
        {
            return new[] { "info", "warn", "error" }.Contains(level);
        }

        private static void PrintBeaconUsage(ILogger logger)
        {
            logger.Information("Beacon Command - Generate AOT-compatible Beacon solution with v3 features");
            logger.Information("");
            logger.Information("Usage:");
            logger.Information("  Pulsar.Compiler beacon --rules <path> --output <path> [options]");
            logger.Information("");
            logger.Information("Required Options:");
            logger.Information("  --rules <path>      Path to YAML rule file or directory containing rule files");
            logger.Information("  --output <path>     Output directory for the Beacon solution");
            logger.Information("");
            logger.Information("Optional Configuration:");
            logger.Information("  --config <path>     Path to system configuration YAML file (default: system_config.yaml)");
            logger.Information("  --catalog <path>    Path to sensor catalog JSON file for validation");
            logger.Information("  --target <runtime>  Target runtime identifier for AOT compilation (default: win-x64)");
            logger.Information("");
            logger.Information("Validation Options:");
            logger.Information("  --validation-level <level>  Validation strictness: strict, normal, relaxed (default: normal)");
            logger.Information("  --lint                      Enable compiler linting for best practices");
            logger.Information("  --fail-on-warnings          Treat warnings as errors");
            logger.Information("  --lint-level <level>        Linting level: info, warn, error (default: warn)");
            logger.Information("");
            logger.Information("Output Options:");
            logger.Information("  --generate-metadata         Generate UI metadata files (interface_outputs.json, data_dictionary.json)");
            logger.Information("  --emit-sourcemap           Include source maps in generated code");
            logger.Information("  --verbose                   Enable verbose logging");
            logger.Information("  --debug                     Include debug information in generated code");
            logger.Information("");
            logger.Information("Examples:");
            logger.Information("  # Basic Beacon generation");
            logger.Information("  Pulsar.Compiler beacon --rules ./rules.yaml --output ./output");
            logger.Information("");
            logger.Information("  # Full v3 features with validation and metadata");
            logger.Information("  Pulsar.Compiler beacon --rules ./rules.yaml --catalog ./sensors.json \\");
            logger.Information("    --output ./output --validation-level strict --lint --generate-metadata");
            logger.Information("");
            logger.Information("  # Cross-platform AOT compilation");
            logger.Information("  Pulsar.Compiler beacon --rules ./rules.yaml --output ./output \\");
            logger.Information("    --target linux-x64 --verbose");
        }
    }
}