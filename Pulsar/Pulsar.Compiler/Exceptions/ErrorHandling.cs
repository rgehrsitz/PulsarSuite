// File: Pulsar.Compiler/Exceptions/ErrorHandling.cs
// Version: 1.0.0 - Centralized error handling

using Serilog;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Pulsar.Compiler.Exceptions
{
    /// <summary>
    /// Static class providing standardized error handling functions
    /// </summary>
    public static class ErrorHandling
    {
        private static readonly ILogger _logger = LoggingConfig.GetLogger();

        /// <summary>
        /// Safely executes a function with standardized error handling
        /// </summary>
        /// <typeparam name="T">The return type of the function</typeparam>
        /// <param name="operation">The operation to execute</param>
        /// <param name="errorResult">The result to return if an error occurs</param>
        /// <param name="context">Additional context for logging</param>
        /// <param name="operationName">Name of the operation (auto-populated from caller)</param>
        /// <param name="allowedExceptions">Types of exceptions to handle without considering as errors</param>
        /// <returns>The result of the operation or errorResult if an exception occurs</returns>
        public static T SafeExecute<T>(
            Func<T> operation,
            T errorResult,
            Dictionary<string, object>? context = null,
            [CallerMemberName] string operationName = "",
            params Type[] allowedExceptions
        )
        {
            try
            {
                return operation();
            }
            catch (Exception ex)
            {
                // Check if this is an allowed exception type
                foreach (var allowedType in allowedExceptions)
                {
                    if (allowedType.IsInstanceOfType(ex))
                    {
                        // If it's an allowed exception, rethrow it
                        throw;
                    }
                }

                // Create logging context
                var logContext = context != null 
                    ? new Dictionary<string, object>(context) 
                    : new Dictionary<string, object>();
                
                logContext["Operation"] = operationName;
                logContext["ExceptionType"] = ex.GetType().Name;

                _logger.Error(ex, "Error in operation {Operation}: {Message} {@Context}", 
                    operationName, ex.Message, logContext);
                
                return errorResult;
            }
        }

        /// <summary>
        /// Safely executes an async function with standardized error handling
        /// </summary>
        /// <typeparam name="T">The return type of the function</typeparam>
        /// <param name="operation">The async operation to execute</param>
        /// <param name="errorResult">The result to return if an error occurs</param>
        /// <param name="context">Additional context for logging</param>
        /// <param name="operationName">Name of the operation (auto-populated from caller)</param>
        /// <param name="allowedExceptions">Types of exceptions to handle without considering as errors</param>
        /// <returns>The result of the operation or errorResult if an exception occurs</returns>
        public static async Task<T> SafeExecuteAsync<T>(
            Func<Task<T>> operation,
            T errorResult,
            Dictionary<string, object>? context = null,
            [CallerMemberName] string operationName = "",
            params Type[] allowedExceptions
        )
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                // Check if this is an allowed exception type
                foreach (var allowedType in allowedExceptions)
                {
                    if (allowedType.IsInstanceOfType(ex))
                    {
                        // If it's an allowed exception, rethrow it
                        throw;
                    }
                }

                // Create logging context
                var logContext = context != null 
                    ? new Dictionary<string, object>(context) 
                    : new Dictionary<string, object>();
                
                logContext["Operation"] = operationName;
                logContext["ExceptionType"] = ex.GetType().Name;

                _logger.Error(ex, "Error in async operation {Operation}: {Message} {@Context}", 
                    operationName, ex.Message, logContext);
                
                return errorResult;
            }
        }

        /// <summary>
        /// Safely executes an action with standardized error handling
        /// </summary>
        /// <param name="action">The action to execute</param>
        /// <param name="context">Additional context for logging</param>
        /// <param name="operationName">Name of the operation (auto-populated from caller)</param>
        /// <param name="rethrow">Whether to rethrow the exception after logging</param>
        /// <param name="allowedExceptions">Types of exceptions to handle without considering as errors</param>
        /// <returns>True if the action completed successfully, false otherwise</returns>
        public static bool SafeExecute(
            Action action,
            Dictionary<string, object>? context = null,
            [CallerMemberName] string operationName = "",
            bool rethrow = false,
            params Type[] allowedExceptions
        )
        {
            try
            {
                action();
                return true;
            }
            catch (Exception ex)
            {
                // Check if this is an allowed exception type
                foreach (var allowedType in allowedExceptions)
                {
                    if (allowedType.IsInstanceOfType(ex))
                    {
                        // If it's an allowed exception, rethrow it
                        throw;
                    }
                }

                // Create logging context
                var logContext = context != null 
                    ? new Dictionary<string, object>(context) 
                    : new Dictionary<string, object>();
                
                logContext["Operation"] = operationName;
                logContext["ExceptionType"] = ex.GetType().Name;

                _logger.Error(ex, "Error in operation {Operation}: {Message} {@Context}", 
                    operationName, ex.Message, logContext);
                
                if (rethrow)
                {
                    throw;
                }
                
                return false;
            }
        }

        /// <summary>
        /// Safely executes an async action with standardized error handling
        /// </summary>
        /// <param name="action">The async action to execute</param>
        /// <param name="context">Additional context for logging</param>
        /// <param name="operationName">Name of the operation (auto-populated from caller)</param>
        /// <param name="rethrow">Whether to rethrow the exception after logging</param>
        /// <param name="allowedExceptions">Types of exceptions to handle without considering as errors</param>
        /// <returns>True if the action completed successfully, false otherwise</returns>
        public static async Task<bool> SafeExecuteAsync(
            Func<Task> action,
            Dictionary<string, object>? context = null,
            [CallerMemberName] string operationName = "",
            bool rethrow = false,
            params Type[] allowedExceptions
        )
        {
            try
            {
                await action();
                return true;
            }
            catch (Exception ex)
            {
                // Check if this is an allowed exception type
                foreach (var allowedType in allowedExceptions)
                {
                    if (allowedType.IsInstanceOfType(ex))
                    {
                        // If it's an allowed exception, rethrow it
                        throw;
                    }
                }

                // Create logging context
                var logContext = context != null 
                    ? new Dictionary<string, object>(context) 
                    : new Dictionary<string, object>();
                
                logContext["Operation"] = operationName;
                logContext["ExceptionType"] = ex.GetType().Name;

                _logger.Error(ex, "Error in async operation {Operation}: {Message} {@Context}", 
                    operationName, ex.Message, logContext);
                
                if (rethrow)
                {
                    throw;
                }
                
                return false;
            }
        }

        /// <summary>
        /// Validates a condition and throws an exception if it fails
        /// </summary>
        /// <param name="condition">The condition to validate</param>
        /// <param name="errorMessage">The error message if validation fails</param>
        /// <param name="context">Additional context information</param>
        /// <exception cref="ValidationException">Thrown when validation fails</exception>
        public static void Validate(
            bool condition, 
            string errorMessage,
            Dictionary<string, object>? context = null)
        {
            if (!condition)
            {
                throw new ValidationException(errorMessage, "ValidationError", context);
            }
        }

        /// <summary>
        /// Validates that an object is not null
        /// </summary>
        /// <param name="value">The value to check</param>
        /// <param name="paramName">The parameter name</param>
        /// <param name="errorMessage">Optional custom error message</param>
        /// <exception cref="ArgumentNullException">Thrown when value is null</exception>
        public static void ValidateNotNull(
            object? value, 
            string paramName, 
            string? errorMessage = null)
        {
            if (value == null)
            {
                throw new ArgumentNullException(paramName, errorMessage ?? $"Parameter '{paramName}' cannot be null");
            }
        }

        /// <summary>
        /// Validates that a string is not null or empty
        /// </summary>
        /// <param name="value">The string to check</param>
        /// <param name="paramName">The parameter name</param>
        /// <param name="errorMessage">Optional custom error message</param>
        /// <exception cref="ArgumentException">Thrown when string is null or empty</exception>
        public static void ValidateNotNullOrEmpty(
            string? value, 
            string paramName, 
            string? errorMessage = null)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException(
                    errorMessage ?? $"Parameter '{paramName}' cannot be null or empty", 
                    paramName);
            }
        }
    }
}