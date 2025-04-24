// File: Pulsar.Compiler/Config/Templates/Runtime/Services/LoggingService.cs

using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Extensions.Logging;

namespace Beacon.Runtime.Services
{
    /// <summary>
    /// Centralized logging service for Beacon runtime
    /// </summary>
    public static class LoggingService
    {
        private static readonly object _lock = new();
        private static Serilog.Core.Logger? _rootLogger;
        private static ILoggerFactory? _loggerFactory;

        /// <summary>
        /// Initializes the logging system with standard configuration
        /// </summary>
        /// <param name="applicationName">Name of the application for log identification</param>
        /// <param name="logLevel">Minimum log level</param>
        /// <param name="structuredLogging">Whether to enable structured JSON logging</param>
        /// <returns>Microsoft.Extensions.Logging.ILoggerFactory for creating loggers</returns>
        public static ILoggerFactory Initialize(
            string applicationName, 
            LogEventLevel logLevel = LogEventLevel.Information,
            bool structuredLogging = false)
        {
            lock (_lock)
            {
                if (_loggerFactory == null)
                {
                    EnsureLogDirectories();

                    var loggerConfig = new LoggerConfiguration()
                        .MinimumLevel.Is(logLevel)
                        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                        .MinimumLevel.Override("System", LogEventLevel.Warning)
                        .Enrich.FromLogContext()
                        .Enrich.WithThreadId()
                        .Enrich.WithProperty("Application", applicationName)
                        .WriteTo.Console(
                            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{ThreadId}] {Message:lj}{NewLine}{Exception}"
                        )
                        .WriteTo.File(
                            "logs/beacon-.log",
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

                    // Add structured JSON logging if requested
                    if (structuredLogging)
                    {
                        loggerConfig = loggerConfig.WriteTo.File(
                            new CompactJsonFormatter(),
                            "logs/structured/beacon-.json",
                            rollingInterval: RollingInterval.Day
                        );
                    }

                    _rootLogger = loggerConfig.CreateLogger();
                    _loggerFactory = new LoggerFactory().AddSerilog(_rootLogger);
                }

                return _loggerFactory;
            }
        }

        /// <summary>
        /// Gets a Microsoft.Extensions.Logging.ILogger for the specified category
        /// </summary>
        /// <param name="categoryName">Category name for the logger</param>
        /// <returns>Microsoft.Extensions.Logging.ILogger</returns>
        public static Microsoft.Extensions.Logging.ILogger GetLogger(string categoryName)
        {
            EnsureLoggerFactory();
            return _loggerFactory.CreateLogger(categoryName);
        }

        /// <summary>
        /// Gets a Microsoft.Extensions.Logging.ILogger for the specified type
        /// </summary>
        /// <typeparam name="T">Type to create logger for</typeparam>
        /// <returns>Microsoft.Extensions.Logging.ILogger</returns>
        public static Microsoft.Extensions.Logging.ILogger GetLogger<T>()
        {
            EnsureLoggerFactory();
            return _loggerFactory.CreateLogger<T>();
        }

        /// <summary>
        /// Gets a Serilog.ILogger for the specified component
        /// </summary>
        /// <param name="componentName">Component name for logger identification</param>
        /// <returns>Serilog.ILogger</returns>
        public static Serilog.ILogger GetSerilogLogger(string componentName)
        {
            return new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .Enrich.WithProperty("Component", componentName)
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{ThreadId}] [{Component}] {Message:lj}{NewLine}{Exception}"
                )
                .WriteTo.File(
                    $"logs/components/{componentName}-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7
                )
                .CreateLogger();
        }

        /// <summary>
        /// Creates a special logger for Redis operations
        /// </summary>
        /// <returns>Serilog.ILogger configured for Redis</returns>
        public static Serilog.ILogger GetRedisLogger()
        {
            return new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .WriteTo.Console(
                    restrictedToMinimumLevel: LogEventLevel.Information,
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{ThreadId}] Redis: {Message:lj}{NewLine}{Exception}"
                )
                .WriteTo.File(
                    new CompactJsonFormatter(),
                    "logs/redis/redis-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    restrictedToMinimumLevel: LogEventLevel.Debug
                )
                .WriteTo.File(
                    new CompactJsonFormatter(),
                    "logs/redis/metrics/metrics-.json",
                    restrictedToMinimumLevel: LogEventLevel.Information
                )
                .WriteTo.Logger(lc =>
                    lc.Filter.ByIncludingOnly(e => e.Level >= LogEventLevel.Error)
                        .WriteTo.File(
                            "logs/redis/errors/error-.log",
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: 30
                        )
                )
                .CreateLogger();
        }

        /// <summary>
        /// Ensures all required log directories exist
        /// </summary>
        private static void EnsureLogDirectories()
        {
            var directories = new[]
            {
                "logs",
                "logs/errors",
                "logs/structured",
                "logs/components",
                "logs/redis",
                "logs/redis/metrics",
                "logs/redis/errors"
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
        /// Ensures the logger factory has been initialized
        /// </summary>
        private static void EnsureLoggerFactory()
        {
            if (_loggerFactory == null)
            {
                Initialize("Beacon"); // Initialize with default values
            }
        }

        /// <summary>
        /// Closes and flushes all loggers
        /// </summary>
        public static void CloseAndFlush()
        {
            lock (_lock)
            {
                _rootLogger?.Dispose();
                _rootLogger = null;
                _loggerFactory?.Dispose();
                _loggerFactory = null;
            }
        }
    }
}