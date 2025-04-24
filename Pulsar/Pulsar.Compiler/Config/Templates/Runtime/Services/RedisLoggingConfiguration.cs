// File: Pulsar.Compiler/Config/Templates/Runtime/Services/RedisLoggingConfiguration.cs

using System;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Beacon.Runtime.Services
{
    /// <summary>
    /// Redis-specific logging configuration
    /// </summary>
    public static class RedisLoggingConfiguration
    {
        private const string DefaultLogPath = "logs/redis/redis-{Date}.log";

        /// <summary>
        /// Configures a Serilog logger specifically for Redis operations
        /// </summary>
        /// <param name="config">Redis configuration</param>
        /// <param name="logPath">Optional custom log path</param>
        /// <returns>Configured Serilog ILogger</returns>
        public static ILogger ConfigureRedisLogger(
            RedisConfiguration config,
            string? logPath = null
        )
        {
            // Simply use the centralized logging service
            return LoggingService.GetRedisLogger();
        }

        /// <summary>
        /// Ensures Redis log directories exist - kept for backwards compatibility,
        /// but LoggingService now handles directory creation
        /// </summary>
        public static void EnsureLogDirectories()
        {
            var directories = new[] { "logs/redis", "logs/redis/metrics", "logs/redis/errors" };

            foreach (var dir in directories)
            {
                if (!System.IO.Directory.Exists(dir))
                {
                    System.IO.Directory.CreateDirectory(dir);
                }
            }
        }
    }
}