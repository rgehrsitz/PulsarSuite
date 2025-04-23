// File: Pulsar.Compiler/Exceptions/RuleCompilationException.cs
// Version: 1.1.0 - Enhanced exception handling

using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pulsar.Compiler.Exceptions
{
    /// <summary>
    /// Exception thrown when rule compilation fails
    /// </summary>
    public class RuleCompilationException : ValidationException
    {
        /// <summary>
        /// Gets the name of the rule
        /// </summary>
        public string RuleName { get; } = string.Empty;

        /// <summary>
        /// Gets the source file of the rule
        /// </summary>
        public string? RuleSource { get; }

        /// <summary>
        /// Gets the line number where the error occurred
        /// </summary>
        public int? LineNumber { get; }

        /// <summary>
        /// Gets the source code snippet
        /// </summary>
        public string? SourceCode { get; }

        private static readonly ILogger _logger = LoggingConfig.GetLogger();

        /// <summary>
        /// Creates a new rule compilation exception
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="ruleName">The name of the rule</param>
        /// <param name="ruleSource">The source file of the rule</param>
        /// <param name="lineNumber">The line number where the error occurred</param>
        /// <param name="errorType">The type of error</param>
        /// <param name="context">Additional context</param>
        public RuleCompilationException(
            string message,
            string ruleName,
            string? ruleSource = null,
            int? lineNumber = null,
            string? errorType = "CompilationError",
            Dictionary<string, object>? context = null
        )
            : base(message, errorType ?? "CompilationError", MergeContext(ruleName, ruleSource, lineNumber, context))
        {
            RuleName = ruleName;
            RuleSource = ruleSource;
            LineNumber = lineNumber;
        }

        /// <summary>
        /// Creates a new rule compilation exception with an inner exception
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="ruleName">The name of the rule</param>
        /// <param name="innerException">The inner exception</param>
        /// <param name="ruleSource">The source file of the rule</param>
        /// <param name="lineNumber">The line number where the error occurred</param>
        /// <param name="errorType">The type of error</param>
        /// <param name="context">Additional context</param>
        public RuleCompilationException(
            string message,
            string ruleName,
            Exception innerException,
            string? ruleSource = null,
            int? lineNumber = null,
            string? errorType = "CompilationError",
            Dictionary<string, object>? context = null
        )
            : base(message, innerException, errorType ?? "CompilationError", MergeContext(ruleName, ruleSource, lineNumber, context))
        {
            RuleName = ruleName;
            RuleSource = ruleSource;
            LineNumber = lineNumber;
        }

        /// <summary>
        /// Creates a new rule compilation exception with context
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="context">Additional context</param>
        public RuleCompilationException(string message, IDictionary<string, object>? context = null)
            : base(message, "CompilationError", ConvertToDictionary(context))
        {
            if (context != null)
            {
                if (context.TryGetValue("SourceCode", out var sourceCode))
                {
                    SourceCode = sourceCode?.ToString();
                }

                if (context.TryGetValue("RuleName", out var ruleName) && ruleName is string ruleNameStr)
                {
                    RuleName = ruleNameStr;
                }

                if (context.TryGetValue("RuleSource", out var ruleSource) && ruleSource is string ruleSourceStr)
                {
                    RuleSource = ruleSourceStr;
                }

                if (context.TryGetValue("LineNumber", out var lineNumber) && lineNumber is int lineNumberInt)
                {
                    LineNumber = lineNumberInt;
                }
            }
        }

        /// <summary>
        /// Merges the context with the rule information
        /// </summary>
        private static Dictionary<string, object> MergeContext(
            string ruleName, 
            string? ruleSource, 
            int? lineNumber, 
            Dictionary<string, object>? context)
        {
            var result = context != null 
                ? new Dictionary<string, object>(context) 
                : new Dictionary<string, object>();

            result["RuleName"] = ruleName;
            
            if (ruleSource != null)
                result["RuleSource"] = ruleSource;
                
            if (lineNumber.HasValue)
                result["LineNumber"] = lineNumber.Value;

            return result;
        }

        /// <summary>
        /// Converts an IDictionary to a Dictionary
        /// </summary>
        private static Dictionary<string, object> ConvertToDictionary(IDictionary<string, object>? source)
        {
            if (source == null)
                return new Dictionary<string, object>();
                
            return source.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>
        /// Returns a string representation of the exception
        /// </summary>
        public override string ToString()
        {
            var location = LineNumber.HasValue ? $" at line {LineNumber}" : "";
            var source = !string.IsNullOrEmpty(RuleSource) ? $" in {RuleSource}" : "";
            return $"{ErrorType} in rule '{RuleName}'{source}{location}: {Message}";
        }
    }
}
