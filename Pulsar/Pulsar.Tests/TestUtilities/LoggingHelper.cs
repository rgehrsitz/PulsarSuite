// File: Pulsar.Tests/TestUtilities/LoggingHelper.cs

using System;
using System.Diagnostics;
using Serilog;
using Serilog.Events;
using Xunit.Abstractions;

namespace Pulsar.Tests.TestUtilities
{
    /// <summary>
    /// Helper methods for working with logging in tests
    /// </summary>
    public static class LoggingHelper
    {
        /// <summary>
        /// Converts a Microsoft.Extensions.Logging.ILogger to Serilog.ILogger
        /// </summary>
        public static Serilog.ILogger ToSerilogLogger(
            this Microsoft.Extensions.Logging.ILogger logger
        )
        {
            // Use our adapter to convert MS Logger to Serilog
            return new LoggerAdapter(logger);
        }

        /// <summary>
        /// Helper method for tests that require Serilog.ILogger
        /// </summary>
        public static Serilog.ILogger GetSerilogLogger(ITestOutputHelper output)
        {
            return LoggingConfig.GetSerilogLoggerForTests(output);
        }
        
        /// <summary>
        /// Execute an action with logging of execution time
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="operationName">Name of the operation being performed</param>
        /// <param name="action">The action to execute</param>
        public static void LogExecutionTime(this ILogger logger, string operationName, Action action)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                logger.Debug("Starting test operation: {OperationName}", operationName);
                action();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error executing test operation: {OperationName}", operationName);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                logger.Information("Completed test operation: {OperationName} in {ElapsedMilliseconds}ms", 
                    operationName, stopwatch.ElapsedMilliseconds);
            }
        }
        
        /// <summary>
        /// Execute a function with logging of execution time
        /// </summary>
        /// <typeparam name="T">Return type of the function</typeparam>
        /// <param name="logger">The logger instance</param>
        /// <param name="operationName">Name of the operation being performed</param>
        /// <param name="func">The function to execute</param>
        /// <returns>The result of the function</returns>
        public static T LogExecutionTime<T>(this ILogger logger, string operationName, Func<T> func)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                logger.Debug("Starting test operation: {OperationName}", operationName);
                return func();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error executing test operation: {OperationName}", operationName);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                logger.Information("Completed test operation: {OperationName} in {ElapsedMilliseconds}ms", 
                    operationName, stopwatch.ElapsedMilliseconds);
            }
        }
        
        /// <summary>
        /// Safely log a message at the specified level, handling any exceptions
        /// </summary>
        public static void SafeLog(this ILogger logger, LogEventLevel level, string messageTemplate, params object[] propertyValues)
        {
            try
            {
                logger.Write(level, messageTemplate, propertyValues);
            }
            catch (Exception ex)
            {
                try
                {
                    // Try to log the failure itself
                    logger.Error(ex, "Error occurred while logging message: {MessageTemplate}", messageTemplate);
                }
                catch
                {
                    // Can't do anything if logging itself is failing
                }
            }
        }
    }
}