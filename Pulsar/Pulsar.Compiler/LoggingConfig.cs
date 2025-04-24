// File: Pulsar.Compiler/LoggingConfig.cs

using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;
using System;
using System.IO;

namespace Pulsar.Compiler
{
    /// <summary>
    /// Centralized logging configuration for the Pulsar system.
    /// </summary>
    public static class LoggingConfig
    {
        private static Logger? _logger;
        private static readonly object _lock = new();
        
        /// <summary>
        /// Gets or creates the main application logger.
        /// </summary>
        /// <param name="logLevel">Optional log level, defaults to Debug</param>
        /// <returns>A configured Serilog Logger</returns>
        public static Logger GetLogger(LogEventLevel logLevel = LogEventLevel.Debug)
        {
            if (_logger == null)
            {
                lock (_lock)
                {
                    if (_logger == null)
                    {
                        EnsureLogDirectories();
                        
                        var config = new LoggerConfiguration()
                            .MinimumLevel.Is(logLevel)
                            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                            .MinimumLevel.Override("System", LogEventLevel.Information)
                            .Enrich.FromLogContext()
                            .Enrich.WithThreadId()
                            .WriteTo.Console(
                                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{ThreadId}] {Message:lj}{NewLine}{Exception}"
                            )
                            .WriteTo.File(
                                "logs/pulsar-.log", 
                                rollingInterval: RollingInterval.Day,
                                retainedFileCountLimit: 7,
                                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] [{ThreadId}] {Message:lj}{NewLine}{Exception}"
                            )
                            .WriteTo.Logger(lc => lc
                                .Filter.ByIncludingOnly(e => e.Level >= LogEventLevel.Error)
                                .WriteTo.File(
                                    "logs/errors/error-.log", 
                                    rollingInterval: RollingInterval.Day,
                                    retainedFileCountLimit: 30,
                                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] [{ThreadId}] {Message:lj}{NewLine}{Exception}"
                                )
                            );
                        
                        // Add JSON structured logging for machine processing if needed
                        if (Environment.GetEnvironmentVariable("PULSAR_STRUCTURED_LOGGING") == "true")
                        {
                            config = config.WriteTo.File(
                                new CompactJsonFormatter(),
                                "logs/structured/pulsar-.json",
                                rollingInterval: RollingInterval.Day
                            );
                        }

                        _logger = config.CreateLogger();
                    }
                }
            }
            return _logger;
        }

        /// <summary>
        /// Creates a new logger with component-specific configuration
        /// </summary>
        /// <param name="componentName">The name of the component</param>
        /// <param name="logLevel">Optional log level, defaults to Debug</param>
        /// <returns>A configured Serilog Logger</returns>
        public static ILogger GetComponentLogger(string componentName, LogEventLevel logLevel = LogEventLevel.Debug)
        {
            EnsureLogDirectories();
            
            return new LoggerConfiguration()
                .MinimumLevel.Is(logLevel)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .MinimumLevel.Override("System", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .Enrich.WithProperty("Component", componentName)
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{ThreadId}] [{Component}] {Message:lj}{NewLine}{Exception}"
                )
                .WriteTo.File(
                    $"logs/components/{componentName}-.log", 
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] [{ThreadId}] {Message:lj}{NewLine}{Exception}"
                )
                .CreateLogger();
        }
        
        /// <summary>
        /// Configure verbose debug logging for development scenarios
        /// </summary>
        /// <returns>A configured Serilog Logger with verbose settings</returns>
        public static ILogger GetVerboseLogger()
        {
            EnsureLogDirectories();
            
            return new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{ThreadId}] {Message:lj}{NewLine}{Exception}"
                )
                .WriteTo.File(
                    "logs/debug/verbose-.log", 
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 3,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] [{ThreadId}] {SourceContext} {Message:lj}{NewLine}{Exception}"
                )
                .CreateLogger();
        }

        /// <summary>
        /// Ensure all required log directories exist
        /// </summary>
        private static void EnsureLogDirectories()
        {
            var directories = new[] 
            { 
                "logs", 
                "logs/errors", 
                "logs/structured", 
                "logs/components", 
                "logs/debug"
            };

            foreach (var dir in directories)
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
        }

        /// <summary>
        /// Closes and flushes the logger.
        /// </summary>
        public static void CloseAndFlush()
        {
            _logger?.Dispose();
            _logger = null;
        }
    }
}