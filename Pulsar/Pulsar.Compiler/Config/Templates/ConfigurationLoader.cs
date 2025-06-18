// File: Pulsar.Compiler/Config/Templates/ConfigurationLoader.cs
// Version: 1.0.0

using System;
using System.IO;
using System.Text.Json;
using Beacon.Compiler;
using Serilog;
using Beacon.Runtime.Models;

namespace Beacon.Runtime.Rules
{
    internal static class ConfigurationLoader
    {
        private static readonly ILogger _logger = LoggingConfig.GetLogger();

        internal static RuntimeConfig LoadConfiguration(
            string[] args,
            bool requireSensors = true,
            string? configPath = null
        )
        {
            try
            {
                _logger.Debug(
                    "Loading configuration from {Path}",
                    configPath ?? "default location"
                );
                var config = new RuntimeConfig();

                if (configPath != null && File.Exists(configPath))
                {
                    _logger.Debug("Reading configuration file");
                    var jsonContent = File.ReadAllText(configPath);
                    config =
                        JsonSerializer.Deserialize<RuntimeConfig>(jsonContent)
                        ?? new RuntimeConfig();
                    _logger.Information("Configuration loaded from {Path}", configPath);
                }
                else
                {
                    _logger.Warning(
                        "No configuration file found at {Path}, using defaults",
                        configPath ?? "default location"
                    );
                }

                ValidateConfiguration(config, requireSensors);
                return config;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error loading configuration");
                throw;
            }
        }

        private static void ValidateConfiguration(RuntimeConfig config, bool requireSensors)
        {
            _logger.Debug("Validating configuration");

            if (
                requireSensors
                && (config.ValidSensors == null || config.ValidSensors.Count == 0)
            )
            {
                _logger.Error("Valid sensors not configured");
                throw new InvalidOperationException(
                    "Valid sensors must be configured when requireSensors is true"
                );
            }

            if (config.CycleTime < 10)
            {
                _logger.Warning(
                    "Cycle time is very low ({CycleTime}ms), this may impact performance",
                    config.CycleTime
                );
            }

            _logger.Debug("Configuration validation completed");
        }
    }
}
