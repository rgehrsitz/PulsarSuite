// File: Pulsar.Compiler/Commands/InitCommand.cs

using Pulsar.Compiler.Config;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Pulsar.Compiler.Commands
{
    public class InitCommand : ICommand
    {
        private readonly ILogger _logger;

        public InitCommand(ILogger logger)
        {
            _logger = logger.ForContext<InitCommand>();
        }

        public async Task<int> RunAsync(Dictionary<string, string> options)
        {
            var outputPath = options.GetValueOrDefault("output", ".");

            try
            {
                // Create directory structure
                Directory.CreateDirectory(outputPath);
                Directory.CreateDirectory(Path.Combine(outputPath, "rules"));
                Directory.CreateDirectory(Path.Combine(outputPath, "config"));

                // Create example rule file
                var exampleRulePath = Path.Combine(outputPath, "rules", "example.yaml");
                await File.WriteAllTextAsync(
                    exampleRulePath,
                    @"rules:
  - name: 'TemperatureConversion'
    description: 'Converts temperature from Fahrenheit to Celsius'
    conditions:
      all:
        - condition:
            type: comparison
            sensor: 'temperature_f'
            operator: '>'
            value: -459.67  # Absolute zero check
    actions:
      - set_value:
          key: 'temperature_c'
          value_expression: '(temperature_f - 32) * 5/9'

  - name: 'HighTemperatureAlert'
    description: 'Alerts when temperature exceeds threshold for duration'
    conditions:
      all:
        - condition:
            type: threshold_over_time
            sensor: 'temperature_c'
            threshold: 30
            duration: 300  # 300ms
    actions:
      - set_value:
          key: 'high_temp_alert'
          value: 1"
                );

                // Create system config file
                var configPath = Path.Combine(outputPath, "config", "system_config.yaml");
                await File.WriteAllTextAsync(
                    configPath,
                    @"version: 1
validSensors:
  - temperature_f
  - temperature_c
  - high_temp_alert
cycleTime: 100  # ms
redis:
  endpoints: 
    - localhost:6379
  poolSize: 8
  retryCount: 3
  retryBaseDelayMs: 100
  connectTimeout: 5000
  syncTimeout: 1000
  keepAlive: 60
  password: null
  ssl: false
  allowAdmin: false
bufferCapacity: 100"
                );

                // Create build configuration file
                var buildConfigPath = Path.Combine(outputPath, "config", "build_config.yaml");
                await File.WriteAllTextAsync(
                    buildConfigPath,
                    @"maxRulesPerFile: 100
namespace: Pulsar.Runtime.Rules
generateDebugInfo: true
optimizeOutput: true
complexityThreshold: 100
groupParallelRules: true"
                );

                // Create a README file
                var readmePath = Path.Combine(outputPath, "README.md");
                await File.WriteAllTextAsync(
                    readmePath,
                    @"# Pulsar Rules Project

This is a newly initialized Pulsar rules project. The directory structure is:

- `rules/` - Contains your YAML rule definitions
- `config/` - Contains system and build configuration
  - `system_config.yaml` - System-wide configuration
  - `build_config.yaml` - Build process configuration

## Getting Started

1. Edit the rules in `rules/example.yaml` or create new rule files
2. Adjust configurations in the `config/` directory
3. Compile your rules:
   ```bash
   pulsar compile --rules ./rules --config ./config/system_config.yaml --output ./output
   ```

4. Build the runtime:
   ```bash
   cd output
   dotnet publish -c Release -r linux-x64 --self-contained true
   ```

## Rule Files

Each rule file should contain one or more rules defined in YAML format.
See `rules/example.yaml` for an example of the rule format.

## Configuration

- `system_config.yaml` defines valid sensors and system-wide settings
- `build_config.yaml` controls the build process and output format

## Additional Information

For more detailed documentation, visit:
https://github.com/yourusername/pulsar/docs"
                );

                _logger.Information("Initialized new Pulsar project at {Path}", outputPath);
                _logger.Information("Created example rule in rules/example.yaml");
                _logger.Information("Created system configuration in config/system_config.yaml");
                _logger.Information("See README.md for next steps");

                return 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initialize project");
                return 1;
            }
        }
    }
}