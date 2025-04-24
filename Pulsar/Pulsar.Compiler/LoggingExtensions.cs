// File: Pulsar.Compiler/LoggingExtensions.cs

using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using System;

namespace Pulsar.Compiler
{
    /// <summary>
    /// Extension methods for working with logging in Pulsar
    /// </summary>
    public static class LoggingExtensions
    {
        /// <summary>
        /// Converts a Serilog logger to Microsoft.Extensions.Logging.ILogger
        /// </summary>
        /// <param name="serilogLogger">The Serilog logger to convert</param>
        /// <returns>Microsoft.Extensions.Logging.ILogger</returns>
        public static Microsoft.Extensions.Logging.ILogger ToMicrosoftLogger(this ILogger serilogLogger)
        {
            return new SerilogLoggerFactory(serilogLogger).CreateLogger("PulsarLogger");
        }
        
        /// <summary>
        /// Converts a Serilog logger to Microsoft.Extensions.Logging.ILogger with a specific category name
        /// </summary>
        /// <param name="serilogLogger">The Serilog logger to convert</param>
        /// <param name="categoryName">The category name for the logger</param>
        /// <returns>Microsoft.Extensions.Logging.ILogger</returns>
        public static Microsoft.Extensions.Logging.ILogger ToMicrosoftLogger(this ILogger serilogLogger, string categoryName)
        {
            return new SerilogLoggerFactory(serilogLogger).CreateLogger(categoryName);
        }
        
        /// <summary>
        /// Safely logs a message with structured properties, catching any exceptions from the logging system
        /// </summary>
        /// <param name="logger">The logger to use</param>
        /// <param name="level">The log level</param>
        /// <param name="messageTemplate">The message template</param>
        /// <param name="propertyValues">Property values for the template</param>
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
                    // Try to log the failure itself, but don't throw if this fails too
                    logger.Error(ex, "Error occurred while logging message: {MessageTemplate}", messageTemplate);
                }
                catch
                {
                    // Can't do anything if logging itself is failing
                }
            }
        }
        
        /// <summary>
        /// Execute an action with logging of execution time
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="operationName">Name of the operation being performed</param>
        /// <param name="action">The action to execute</param>
        public static void LogExecutionTime(this ILogger logger, string operationName, Action action)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                logger.Debug("Starting operation: {OperationName}", operationName);
                action();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error executing operation: {OperationName}", operationName);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                logger.Debug("Completed operation: {OperationName} in {ElapsedMilliseconds}ms", 
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
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                logger.Debug("Starting operation: {OperationName}", operationName);
                return func();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error executing operation: {OperationName}", operationName);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                logger.Debug("Completed operation: {OperationName} in {ElapsedMilliseconds}ms", 
                    operationName, stopwatch.ElapsedMilliseconds);
            }
        }
    }
}