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
            logger.Information("Usage: Pulsar.Compiler <command> [options]");
            logger.Information("Commands:");
            logger.Information("  compile  - Compile rules into a deployable project");
            logger.Information("  validate - Validate rules without generating code");
            logger.Information("  test     - Test Pulsar rules and configuration files");
            logger.Information("  init     - Initialize a new project");
            logger.Information("  generate - Generate a buildable project");
            logger.Information("  beacon   - Generate an AOT-compatible Beacon solution");
            logger.Information("Options:");
            logger.Information("  --output=<path>  Output directory (required for compile/generate)");
            logger.Information("  --config=<path>  System configuration file (optional)");
            logger.Information(
                "  --target=<id>    Target runtime identifier for AOT (e.g., win-x64, linux-x64)"
            );
            logger.Information("  --verbose        Enable verbose logging");
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
                "aot" or "debug" or "parallel" or "emit-sourcemap" or "verbose" or "clean" => true,
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

        private static void PrintBeaconUsage(ILogger logger)
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