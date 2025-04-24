// File: Pulsar.Tests/TestUtilities/LoggingConfig.cs

using Serilog;
using Serilog.Extensions.Logging;
using System.IO;
using Xunit.Abstractions;

namespace Pulsar.Tests.TestUtilities
{
    /// <summary>
    /// Logging configuration for test scenarios
    /// </summary>
    public static class LoggingConfig
    {
        /// <summary>
        /// Gets a Serilog.ILogger for general test use
        /// </summary>
        public static Serilog.ILogger GetSerilogLogger()
        {
            EnsureTestLogDirectories();
            
            return new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();
        }

        /// <summary>
        /// Gets a Serilog.ILogger configured to write to XUnit test output
        /// </summary>
        public static Serilog.ILogger GetSerilogLoggerForTests(ITestOutputHelper output)
        {
            EnsureTestLogDirectories();
            
            return new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.TestOutput(output)
                .WriteTo.File(
                    "logs/tests/test-run-.log",
                    rollingInterval: RollingInterval.Hour,
                    retainedFileCountLimit: 24
                )
                .CreateLogger();
        }

        /// <summary>
        /// Gets a Microsoft.Extensions.Logging.ILogger for general test use
        /// </summary>
        public static Microsoft.Extensions.Logging.ILogger GetLogger()
        {
            EnsureTestLogDirectories();
            
            var serilogLogger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File(
                    "logs/tests/test-run-.log",
                    rollingInterval: RollingInterval.Hour,
                    retainedFileCountLimit: 24
                )
                .CreateLogger();

            return new SerilogLoggerFactory(serilogLogger).CreateLogger("TestLogger");
        }

        /// <summary>
        /// Gets a Microsoft.Extensions.Logging.ILogger configured to write to XUnit test output
        /// </summary>
        public static Microsoft.Extensions.Logging.ILogger GetLoggerForTests(
            ITestOutputHelper output
        )
        {
            EnsureTestLogDirectories();
            
            // Use the TestOutput sink from the Serilog.Sinks.XUnit package
            var serilogLogger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.TestOutput(output)
                .WriteTo.File(
                    "logs/tests/test-run-.log",
                    rollingInterval: RollingInterval.Hour,
                    retainedFileCountLimit: 24
                )
                .CreateLogger();

            return new SerilogLoggerFactory(serilogLogger).CreateLogger("TestLogger");
        }

        /// <summary>
        /// Converts a Microsoft.Extensions.Logging.ILogger to Serilog.ILogger
        /// </summary>
        public static Serilog.ILogger ToSerilogLogger(Microsoft.Extensions.Logging.ILogger msLogger)
        {
            return new LoggerAdapter(msLogger);
        }
        
        /// <summary>
        /// Ensures all required test log directories exist
        /// </summary>
        private static void EnsureTestLogDirectories()
        {
            var directories = new[] 
            { 
                "logs", 
                "logs/tests",
                "logs/tests/errors"
            };

            foreach (var dir in directories)
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
        }
    }
}