// File: Pulsar.Compiler/Exceptions/ValidationException.cs
// Version: 1.1.0 - Enhanced exception handling

using Serilog;
using System;
using System.Collections.Generic;

namespace Pulsar.Compiler.Exceptions
{
    /// <summary>
    /// Base exception thrown when validation fails for rules, configurations, or other inputs
    /// </summary>
    public class ValidationException : Exception
    {
        /// <summary>
        /// Gets the validation context information
        /// </summary>
        public Dictionary<string, object> Context { get; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets the error type - defaults to "ValidationError"
        /// </summary>
        public string ErrorType { get; protected set; } = "ValidationError";

        /// <summary>
        /// Gets the logger instance for this exception
        /// </summary>
        private static readonly ILogger _logger = LoggingConfig.GetLogger();

        /// <summary>
        /// Creates a new validation exception with a message
        /// </summary>
        /// <param name="message">The error message</param>
        public ValidationException(string message)
            : base(message) 
        {
            LogError();
        }

        /// <summary>
        /// Creates a new validation exception with a message and inner exception
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="innerException">The inner exception</param>
        public ValidationException(string message, Exception innerException)
            : base(message, innerException) 
        {
            LogError();
        }

        /// <summary>
        /// Creates a new validation exception with a message and context
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="errorType">The error type</param>
        /// <param name="context">Additional context for the error</param>
        public ValidationException(string message, string errorType, Dictionary<string, object>? context = null)
            : base(message)
        {
            ErrorType = errorType ?? "ValidationError";
            
            if (context != null)
            {
                foreach (var kvp in context)
                {
                    Context[kvp.Key] = kvp.Value;
                }
            }

            LogError();
        }

        /// <summary>
        /// Creates a new validation exception with a message, inner exception, and context
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="innerException">The inner exception</param>
        /// <param name="errorType">The error type</param>
        /// <param name="context">Additional context for the error</param>
        public ValidationException(string message, Exception innerException, string errorType, Dictionary<string, object>? context = null)
            : base(message, innerException)
        {
            ErrorType = errorType ?? "ValidationError";
            
            if (context != null)
            {
                foreach (var kvp in context)
                {
                    Context[kvp.Key] = kvp.Value;
                }
            }

            LogError();
        }

        /// <summary>
        /// Logs the error using the standardized format
        /// </summary>
        private void LogError()
        {
            var errorContext = new Dictionary<string, object>(Context)
            {
                ["ErrorType"] = ErrorType
            };

            if (InnerException != null)
                errorContext["InnerError"] = InnerException.Message;

            _logger.Error(
                "Validation error: {ErrorMessage} {@Context}",
                Message,
                errorContext
            );
        }

        /// <summary>
        /// Returns a string representation of the exception
        /// </summary>
        public override string ToString()
        {
            var contextStr = Context.Count > 0 
                ? $" Context: {string.Join(", ", Context.Select(kvp => $"{kvp.Key}={kvp.Value}"))}" 
                : "";
            
            return $"{ErrorType}: {Message}{contextStr}";
        }
    }
    
    /// <summary>
    /// Exception thrown when configuration validation fails
    /// </summary>
    public class ConfigurationException : ValidationException
    {
        /// <summary>
        /// Creates a new configuration exception
        /// </summary>
        /// <param name="message">The error message</param>
        public ConfigurationException(string message)
            : base(message) 
        {
            ErrorType = "ConfigurationError";
        }

        /// <summary>
        /// Creates a new configuration exception with an inner exception
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="innerException">The inner exception</param>
        public ConfigurationException(string message, Exception innerException)
            : base(message, innerException) 
        {
            ErrorType = "ConfigurationError";
        }

        /// <summary>
        /// Creates a new configuration exception with context
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="context">Additional context</param>
        public ConfigurationException(string message, Dictionary<string, object>? context = null)
            : base(message, "ConfigurationError", context) 
        {
        }
    }
}
