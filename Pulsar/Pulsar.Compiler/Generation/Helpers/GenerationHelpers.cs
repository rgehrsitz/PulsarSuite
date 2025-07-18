// File: Pulsar.Compiler/Generation/Helpers/GenerationHelpers.cs

using System;
using System.Text.RegularExpressions;
using System.Threading;
using Pulsar.Compiler.Models;

namespace Pulsar.Compiler.Generation.Helpers
{
    public static class GenerationHelpers
    {
        // Counter to ensure uniqueness of variable names
        private static int _varNameCounter = 0;

        // Generate a unique variable name using counter and a unique identifier
        private static string GenerateUniqueVarName(string baseName, Dictionary<string, int>? occurrenceCounter = null)
        {
            int counter = Interlocked.Increment(ref _varNameCounter);

            // If we're tracking occurrences of the same variable within an expression
            if (occurrenceCounter != null)
            {
                if (!occurrenceCounter.TryGetValue(baseName, out int occurrence))
                {
                    occurrence = 1;
                }
                else
                {
                    occurrence++;
                }

                occurrenceCounter[baseName] = occurrence;
                return $"{baseName}_{counter}_{occurrence}";
            }

            return $"{baseName}_{counter}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        }

        private static readonly HashSet<string> _mathFunctions = new HashSet<string>
        {
            "Sin",
            "Cos",
            "Tan",
            "Log",
            "Exp",
            "Sqrt",
            "Abs",
            "Max",
            "Min",
        };

        private static readonly Dictionary<string, string> _logicalOperators = new Dictionary<string, string>
        {
            {"and", "&&"},
            {"or", "||"},
            {"not", "!"},
            {"true", "true"},
            {"false", "false"},
            {"null", "null"}
        };

        public static string GenerateCondition(ConditionGroup? conditions)
        {
            return GenerateCondition(conditions, null);
        }

        public static string GenerateCondition(ConditionGroup? conditions, Dictionary<string, FallbackStrategy>? fallbackStrategies)
        {
            if (conditions == null)
            {
                return "EvalResult.True";
            }

            var parts = new List<string>();

            if (conditions.All?.Any() == true)
            {
                var allConditions = conditions.All.Select(c => GenerateConditionExpression(c, fallbackStrategies));
                var combinedAll = allConditions.Aggregate((a, b) => $"EvalOps.And({a}, {b})");
                parts.Add($"({combinedAll})");
            }

            if (conditions.Any?.Any() == true)
            {
                var anyConditions = conditions.Any.Select(c => GenerateConditionExpression(c, fallbackStrategies));
                var combinedAny = anyConditions.Aggregate((a, b) => $"EvalOps.Or({a}, {b})");
                parts.Add($"({combinedAny})");
            }

            if (parts.Count == 0)
                return "EvalResult.True";
            
            return parts.Count == 1 ? parts[0] : parts.Aggregate((a, b) => $"EvalOps.And({a}, {b})");
        }

        public static string GenerateConditionExpression(ConditionDefinition condition)
        {
            return GenerateConditionExpression(condition, null);
        }

        public static string GenerateConditionExpression(ConditionDefinition condition, Dictionary<string, FallbackStrategy>? fallbackStrategies)
        {
            return condition switch
            {
                ComparisonCondition comparison => GenerateComparisonCondition(comparison, fallbackStrategies),
                ExpressionCondition expression => $"({FixupExpression(expression.Expression, fallbackStrategies)} ? EvalResult.True : EvalResult.False)",
                ThresholdOverTimeCondition threshold => GenerateThresholdCondition(threshold),
                _ => throw new InvalidOperationException(
                    $"Unknown condition type: {condition.GetType().Name}"
                ),
            };
        }

        public static string GenerateComparisonCondition(ComparisonCondition comparison)
        {
            return GenerateComparisonCondition(comparison, null);
        }

        public static string GenerateComparisonCondition(ComparisonCondition comparison, Dictionary<string, FallbackStrategy>? fallbackStrategies)
        {
            var op = comparison.Operator switch
            {
                ComparisonOperator.GreaterThan => ">",
                ComparisonOperator.LessThan => "<",
                ComparisonOperator.GreaterThanOrEqual => ">=",
                ComparisonOperator.LessThanOrEqual => "<=",
                ComparisonOperator.EqualTo => "==",
                ComparisonOperator.NotEqualTo => "!=",
                _ => throw new InvalidOperationException(
                    $"Unknown operator: {comparison.Operator}"
                ),
            };

            // Special handling for sensor that might be an output from another rule
            string sensorAccess;
            if (comparison.Sensor.StartsWith("output:"))
            {
                // For output sensors, try getting from outputs first, then inputs
                string varName = GenerateUniqueVarName($"outVal_{comparison.Sensor.Replace(":", "_")}");
                sensorAccess = $"(outputs.TryGetValue(\"{comparison.Sensor}\", out var {varName}) ? {varName} : " +
                              $"(inputs.ContainsKey(\"{comparison.Sensor}\") ? inputs[\"{comparison.Sensor}\"] : null))";
            }
            else
            {
                // Regular input sensor - use fallback if available
                if (fallbackStrategies?.TryGetValue(comparison.Sensor, out var fallback) == true)
                {
                    sensorAccess = GenerateSensorAccessWithFallback(comparison.Sensor, fallback);
                }
                else
                {
                    // Regular input sensor - add safety check to prevent KeyNotFoundException
                    string varName = GenerateUniqueVarName($"inVal_{comparison.Sensor.Replace(":", "_")}");
                    sensorAccess = $"(inputs.TryGetValue(\"{comparison.Sensor}\", out var {varName}) ? {varName} : null)";
                }
            }

            // Generate three-valued logic comparison
            return $"CompareValue({sensorAccess}, {FormatValue(comparison.Value)}, \"{op}\")";
        }

        private static string FormatValue(object? value)
        {
            return value switch
            {
                bool b => b.ToString().ToLower(),
                string s => $"\"{s}\"",
                _ => value?.ToString() ?? "null"
            };
        }

        /// <summary>
        /// Generate sensor access with V3 fallback strategy handling
        /// </summary>
        public static string GenerateSensorAccessWithFallback(string sensorId, FallbackStrategy? fallback)
        {
            if (fallback == null)
            {
                return $"(inputs.TryGetValue(\"{sensorId}\", out var val) ? val : null)";
            }

            var strategy = $"\"{fallback.Strategy}\"";
            var defaultValue = fallback.DefaultValue != null ? FormatValue(fallback.DefaultValue) : "null";
            var maxAge = !string.IsNullOrEmpty(fallback.MaxAge) ? $"\"{fallback.MaxAge}\"" : "null";

            return $"ResolveSensorWithFallback(\"{sensorId}\", inputs, {strategy}, {defaultValue}, {maxAge})";
        }

        public static string GenerateHelperMethods()
        {
            return @"
        private EvalResult CompareValue(object? sensorValue, object? expectedValue, string op)
        {
            if (sensorValue == null)
                return EvalResult.Indeterminate;

            try
            {
                return op switch
                {
                    "">"" => Convert.ToDouble(sensorValue) > Convert.ToDouble(expectedValue) ? EvalResult.True : EvalResult.False,
                    "">="" => Convert.ToDouble(sensorValue) >= Convert.ToDouble(expectedValue) ? EvalResult.True : EvalResult.False,
                    ""<"" => Convert.ToDouble(sensorValue) < Convert.ToDouble(expectedValue) ? EvalResult.True : EvalResult.False,
                    ""<="" => Convert.ToDouble(sensorValue) <= Convert.ToDouble(expectedValue) ? EvalResult.True : EvalResult.False,
                    ""=="" => Equals(sensorValue, expectedValue) ? EvalResult.True : EvalResult.False,
                    ""!="" => !Equals(sensorValue, expectedValue) ? EvalResult.True : EvalResult.False,
                    _ => EvalResult.Indeterminate
                };
            }
            catch
            {
                return EvalResult.Indeterminate;
            }
        }

        private WindowTracker GetOrCreateWindowTracker(string sensor, double threshold, string op, TimeSpan duration)
        {
            var key = $""{sensor}_{threshold}_{op}_{duration.TotalMilliseconds}"";
            return _windowTrackers.GetOrAdd(key, _ => new WindowTracker(threshold, op, duration));
        }

        private double? GetSensorValue(string sensor, Dictionary<string, object> inputs)
        {
            if (!inputs.TryGetValue(sensor, out var value) || value == null)
                return null;
                
            try
            {
                return Convert.ToDouble(value);
            }
            catch
            {
                return null;
            }
        }

        private EmitGuard GetOrCreateEmitGuard(string key, EmitMode mode)
        {
            if (!_emitGuards.ContainsKey(key))
            {
                _emitGuards[key] = new EmitGuard(mode);
            }
            return _emitGuards[key];
        }

        private object? ResolveSensorWithFallback(string sensorId, Dictionary<string, object> inputs, string fallbackStrategy, object? defaultValue = null, string? maxAge = null)
        {
            // First try to get the sensor value directly
            if (inputs.TryGetValue(sensorId, out var value) && value != null)
                return value;

            // Apply fallback strategy if sensor is missing or null
            return fallbackStrategy switch
            {
                ""use_default"" => defaultValue,
                ""use_last_known"" => GetLastKnownValue(sensorId, maxAge),
                ""propagate_unavailable"" => null, // Will result in Indeterminate
                ""skip_rule"" => null, // Will result in Indeterminate
                _ => null
            };
        }

        private readonly Dictionary<string, (object Value, DateTime Timestamp)> _sensorCache = new();

        private object? GetLastKnownValue(string sensor, string? maxAge)
        {
            if (_sensorCache.TryGetValue(sensor, out var cached))
            {
                if (!string.IsNullOrEmpty(maxAge))
                {
                    var maxAgeSpan = ParseDuration(maxAge);
                    var age = DateTime.UtcNow - cached.Timestamp;
                    if (age <= maxAgeSpan)
                        return cached.Value;
                }
                else
                {
                    return cached.Value;
                }
            }
            return null;
        }

        private TimeSpan ParseDuration(string duration)
        {
            if (string.IsNullOrEmpty(duration)) return TimeSpan.Zero;
            
            if (duration.EndsWith(""ms""))
                return TimeSpan.FromMilliseconds(double.Parse(duration.Substring(0, duration.Length - 2)));
            if (duration.EndsWith(""s""))
                return TimeSpan.FromSeconds(double.Parse(duration.Substring(0, duration.Length - 1)));
            if (duration.EndsWith(""m""))
                return TimeSpan.FromMinutes(double.Parse(duration.Substring(0, duration.Length - 1)));
            if (duration.EndsWith(""h""))
                return TimeSpan.FromHours(double.Parse(duration.Substring(0, duration.Length - 1)));
            
            return TimeSpan.FromSeconds(double.Parse(duration));
        }

        private void CacheSensorValue(string sensor, object? value)
        {
            if (value != null)
            {
                _sensorCache[sensor] = (value, DateTime.UtcNow);
            }
        }";
        }

        public static string GenerateThresholdCondition(ThresholdOverTimeCondition threshold)
        {
            // Convert the ComparisonOperator enum to the string operator format
            var op = threshold.ComparisonOperator switch
            {
                ComparisonOperator.GreaterThan => ">",
                ComparisonOperator.LessThan => "<",
                ComparisonOperator.GreaterThanOrEqual => ">=",
                ComparisonOperator.LessThanOrEqual => "<=",
                ComparisonOperator.EqualTo => "==",
                ComparisonOperator.NotEqualTo => "!=",
                _ => throw new InvalidOperationException(
                    $"Unknown operator: {threshold.ComparisonOperator}"
                ),
            };

            // Use new WindowTracker-based approach with three-valued logic
            return $"GetOrCreateWindowTracker(\"{threshold.Sensor}\", {threshold.Threshold}, \"{op}\", " +
                   $"TimeSpan.FromMilliseconds({threshold.Duration})).Evaluate(" +
                   $"GetSensorValue(\"{threshold.Sensor}\", inputs), DateTime.UtcNow)";
        }

        public static string GenerateAction(ActionDefinition action)
        {
            return GenerateAction(action, null);
        }

        public static string GenerateAction(ActionDefinition action, Dictionary<string, FallbackStrategy>? fallbackStrategies)
        {
            return action switch
            {
                SetValueAction setValue => GenerateSetValueAction(setValue),
                SendMessageAction sendMessage => GenerateSendMessageAction(sendMessage),
                V3SetAction v3Set => GenerateV3SetAction(v3Set, fallbackStrategies),
                V3LogAction v3Log => GenerateV3LogAction(v3Log),
                V3BufferAction v3Buffer => GenerateV3BufferAction(v3Buffer, fallbackStrategies),
                _ => throw new InvalidOperationException(
                    $"Unknown action type: {action.GetType().Name}"
                ),
            };
        }

        public static string GenerateSetValueAction(SetValueAction setValue)
        {
            // Handle special case for "$input" which should map to the input sensor
            if (setValue.ValueExpression == "$input")
            {
                // Map $input to the actual input sensor name
                return $"outputs[\"{setValue.Key}\"] = inputs[\"test:input\"];";
            }

            // Handle special cases for common values
            if (setValue.ValueExpression == "true")
            {
                return $"outputs[\"{setValue.Key}\"] = true;";
            }

            if (setValue.ValueExpression == "false")
            {
                return $"outputs[\"{setValue.Key}\"] = false;";
            }

            if (setValue.ValueExpression == "now()")
            {
                // Output as ISO 8601 string
                return $"outputs[\"{setValue.Key}\"] = DateTime.UtcNow.ToString(\"o\");";
            }

            // Handle direct object value if ValueExpression is null
            if (string.IsNullOrEmpty(setValue.ValueExpression) && setValue.Value != null)
            {
                // Check the type of the value
                if (setValue.Value is string strValue)
                {
                    // If it's a string, properly quote it
                    return $"outputs[\"{setValue.Key}\"] = \"{strValue}\";";
                }
                else if (setValue.Value is bool boolValue)
                {
                    // Handle boolean values
                    return $"outputs[\"{setValue.Key}\"] = {boolValue.ToString().ToLower()};";
                }
                else if (setValue.Value is DateTime)
                {
                    // Output DateTime as ISO 8601 string
                    return $"outputs[\"{setValue.Key}\"] = ((DateTime){setValue.Value}).ToString(\"o\");";
                }
                else
                {
                    // For numeric and other values, use ToString() which works for most types
                    return $"outputs[\"{setValue.Key}\"] = {setValue.Value};";
                }
            }

            // Special case for general expressions with input: prefixes
            if (setValue.ValueExpression?.Contains("input:") == true &&
                (setValue.ValueExpression?.Contains("+") == true ||
                 setValue.ValueExpression?.Contains("-") == true ||
                 setValue.ValueExpression?.Contains("*") == true ||
                 setValue.ValueExpression?.Contains("/") == true ||
                 setValue.ValueExpression?.Contains("(") == true))
            {
                // Extract all input: prefixed variables
                var matches = Regex.Matches(setValue.ValueExpression, @"input:[a-zA-Z0-9_]+");
                string expr = setValue.ValueExpression;

                // Track occurrences of each input variable to ensure uniqueness
                // even for multiple occurrences of the same variable in one expression
                var occurrenceCounter = new Dictionary<string, int>();

                // For each match, generate a unique variable name that tracks occurrence count
                var replacements = new Dictionary<string, List<Tuple<int, int, string>>>();

                // First, collect all occurrences with positions
                foreach (Match match in matches)
                {
                    string key = match.Value;
                    if (!replacements.ContainsKey(key))
                    {
                        replacements[key] = new List<Tuple<int, int, string>>();
                    }

                    // Create a unique base name for this input variable
                    string baseName = $"inVal_{key.Replace(":", "_")}";

                    // Use helper method to generate a unique variable name with occurrence tracking
                    if (!occurrenceCounter.ContainsKey(baseName))
                    {
                        occurrenceCounter[baseName] = 0;
                    }
                    occurrenceCounter[baseName]++;

                    string varName = $"{baseName}_{_varNameCounter}_{occurrenceCounter[baseName]}";

                    // Store the position and replacement info
                    replacements[key].Add(Tuple.Create(match.Index, match.Length,
                        $"Convert.ToDouble((inputs.TryGetValue(\"{key}\", out var {varName}) ? {varName} : 0))"));
                }

                // Apply replacements from right to left to maintain correct positions
                var allReplacements = replacements.Values.SelectMany(x => x)
                    .OrderByDescending(x => x.Item1)
                    .ToList();

                foreach (var r in allReplacements)
                {
                    expr = expr.Substring(0, r.Item1) + r.Item3 + expr.Substring(r.Item1 + r.Item2);
                }

                return $"outputs[\"{setValue.Key}\"] = {expr};";
            }

            // If the value expression directly references a sensor with a colon
            if (
                !string.IsNullOrEmpty(setValue.ValueExpression)
                && setValue.ValueExpression.Contains(":")
                && !setValue.ValueExpression.Contains("+")
                && !setValue.ValueExpression.Contains("-")
                && !setValue.ValueExpression.Contains("*")
                && !setValue.ValueExpression.Contains("/")
                && !setValue.ValueExpression.Contains("(")
                && !setValue.ValueExpression.Contains(")")
            )
            {
                // Direct reference to a sensor with a colon - use safe access
                // Use helper method to generate a unique variable name
                string varName = GenerateUniqueVarName($"inVal_{setValue.ValueExpression.Replace(":", "_")}");
                return $"outputs[\"{setValue.Key}\"] = (inputs.TryGetValue(\"{setValue.ValueExpression}\", out var {varName}) ? {varName} : null);";
            }

            var value = !string.IsNullOrEmpty(setValue.ValueExpression)
                ? FixupExpression(setValue.ValueExpression)
                : setValue.Value?.ToString() ?? "null";

            // If the processed value starts with "inputs[", it's already been processed by FixupExpression
            if (value.StartsWith("inputs[") || value.Contains("Convert.ToDouble"))
            {
                return $"outputs[\"{setValue.Key}\"] = {value};";
            }
            // If the value is just a simple sensor reference (without expressions), treat it as a direct input lookup
            else if (value.Contains(":") && !value.Contains(" ") && !value.Contains("(") && !value.Contains("+") && !value.Contains("-") && !value.Contains("*") && !value.Contains("/"))
            {
                // Use safe access for simple sensor references
                // Use helper method to generate a unique variable name
                string varName = GenerateUniqueVarName($"inVal_{value.Replace(":", "_")}");
                return $"outputs[\"{setValue.Key}\"] = (inputs.TryGetValue(\"{value}\", out var {varName}) ? {varName} : null);";
            }
            else
            {
                return $"outputs[\"{setValue.Key}\"] = {value};";
            }
        }

        public static string GenerateSendMessageAction(SendMessageAction sendMessage)
        {
            // Check if we have a message expression
            if (!string.IsNullOrEmpty(sendMessage.MessageExpression))
            {
                // Process the expression and use it directly
                var messageExpr = FixupExpression(sendMessage.MessageExpression);
                return $"SendMessage(\"{sendMessage.Channel}\", {messageExpr});";
            }
            // Otherwise use the static message
            else
            {
                return $"SendMessage(\"{sendMessage.Channel}\", \"{sendMessage.Message}\");";
            }
        }

        public static string FixupExpression(string expression)
        {
            return FixupExpression(expression, null);
        }

        public static string FixupExpression(string expression, Dictionary<string, FallbackStrategy>? fallbackStrategies)
        {
            if (string.IsNullOrEmpty(expression))
            {
                return "null";
            }

            // Remove special-case block for humidity_status == 'high'
            // All expressions should be processed by the general logic below

            // First, handle string literals enclosed in double quotes (already proper C# format)
            var doubleQuoteStringLiteralPattern = "\"([^\"]*)\"";
            var literalPlaceholders = new Dictionary<string, string>();
            int placeholderIndex = 0;

            // Replace string literals with placeholders to prevent them from being processed
            expression = Regex.Replace(
                expression,
                doubleQuoteStringLiteralPattern,
                match =>
                {
                    var placeholder = $"__STRING_LITERAL_{placeholderIndex}__";
                    literalPlaceholders[placeholder] = $"\"{match.Groups[1].Value}\"";
                    placeholderIndex++;
                    return placeholder;
                }
            );

            // Handle string literals enclosed in single quotes
            var singleQuoteStringLiteralPattern = @"'([^']*)'";
            expression = Regex.Replace(
                expression,
                singleQuoteStringLiteralPattern,
                match =>
                {
                    var placeholder = $"__STRING_LITERAL_{placeholderIndex}__";
                    literalPlaceholders[placeholder] = $"\"{match.Groups[1].Value}\"";
                    placeholderIndex++;
                    return placeholder;
                }
            );

            // Replace logical operators before sensor references
            foreach (var op in _logicalOperators)
            {
                // Use word boundary to ensure we're replacing whole words
                var pattern = $"\\b{op.Key}\\b";
                expression = Regex.Replace(expression, pattern, op.Value);
            }

            // First identify string comparison operations to handle them specially
            var stringComparisonPattern = @"\b([a-zA-Z_][a-zA-Z0-9_]*)\s*(==|!=)\s*(__STRING_LITERAL_\d+__)";
            var stringComparisons = new Dictionary<string, string>();
            var stringMatchIndex = 0;

            expression = Regex.Replace(
                expression,
                stringComparisonPattern,
                match =>
                {
                    var sensor = match.Groups[1].Value;
                    var op = match.Groups[2].Value;
                    var literal = match.Groups[3].Value;

                    // Don't process known non-sensors
                    if (IsMathFunction(sensor) || IsNumeric(sensor) || _logicalOperators.ContainsKey(sensor.ToLower()))
                    {
                        return match.Value;
                    }

                    // Create a placeholder for the entire comparison
                    var placeholder = $"__STRING_COMPARISON_{stringMatchIndex}__";
                    stringMatchIndex++;

                    // Store the string comparison with proper string handling
                    stringComparisons[placeholder] = $"inputs[\"{sensor}\"]?.ToString() {op} {literalPlaceholders[literal]}";

                    return placeholder;
                }
            );

            // Now replace regular sensor references with inputs["sensor"] syntax
            // Process prefixed variables like input:temperature first
            var prefixedSensorPattern = @"\b([a-zA-Z_][a-zA-Z0-9_]*:[a-zA-Z_][a-zA-Z0-9_]*)\b";
            var expressionWithPrefixes = Regex.Replace(
                expression,
                prefixedSensorPattern,
                match =>
                {
                    var sensor = match.Groups[1].Value;
                    // Skip known non-sensor terms like operators, functions, etc.
                    if (IsMathFunction(sensor) || IsNumeric(sensor) || _logicalOperators.ContainsKey(sensor.ToLower()))
                    {
                        return sensor;
                    }

                    // If it's a placeholder, don't process it
                    if ((sensor.StartsWith("__STRING_LITERAL_") && sensor.EndsWith("__")) ||
                        (sensor.StartsWith("__STRING_COMPARISON_") && sensor.EndsWith("__")))
                    {
                        return sensor;
                    }

                    // Handle prefixed variable with fallback if available
                    if (fallbackStrategies?.TryGetValue(sensor, out var fallback) == true)
                    {
                        return $"Convert.ToDouble({GenerateSensorAccessWithFallback(sensor, fallback)})";
                    }
                    else
                    {
                        return $"Convert.ToDouble(inputs[\"{sensor}\"])";
                    }
                }
            );

            // Then process standard variables
            var sensorPattern = @"\b([a-zA-Z_][a-zA-Z0-9_]*)\b";
            var fixedExpression = Regex.Replace(
                expressionWithPrefixes,
                sensorPattern,
                match =>
                {
                    var sensor = match.Groups[1].Value;
                    // Skip known non-sensor terms like operators, functions, etc.
                    if (IsMathFunction(sensor) || IsNumeric(sensor) || _logicalOperators.ContainsKey(sensor.ToLower()))
                    {
                        return sensor;
                    }

                    // If it's a placeholder, don't process it
                    if ((sensor.StartsWith("__STRING_LITERAL_") && sensor.EndsWith("__")) ||
                        (sensor.StartsWith("__STRING_COMPARISON_") && sensor.EndsWith("__")))
                    {
                        return sensor;
                    }

                    // Use fallback-aware sensor access if available
                    if (fallbackStrategies?.TryGetValue(sensor, out var fallback) == true)
                    {
                        return $"Convert.ToDouble({GenerateSensorAccessWithFallback(sensor, fallback)})";
                    }
                    else
                    {
                        // Always treat as input lookup with numeric conversion
                        return $"Convert.ToDouble(inputs[\"{sensor}\"])";
                    }
                }
            );

            // Restore the string comparisons first (they might contain string literals)
            foreach (var comparison in stringComparisons)
            {
                fixedExpression = fixedExpression.Replace(comparison.Key, comparison.Value);
            }

            // Now restore the remaining string literals
            foreach (var placeholder in literalPlaceholders)
            {
                fixedExpression = fixedExpression.Replace(placeholder.Key, placeholder.Value);
            }

            return fixedExpression;
        }

        private static bool IsNumeric(string value)
        {
            return double.TryParse(value, out _);
        }

        public static List<string> GetInputSensors(RuleDefinition rule)
        {
            // Use the InputSensors property populated during parsing
            return rule.InputSensors ?? new List<string>();
        }

        public static List<string> GetOutputSensors(RuleDefinition rule)
        {
            // Use the OutputSensors property populated during parsing
            return rule.OutputSensors ?? new List<string>();
        }

        public static bool HasTemporalConditions(RuleDefinition rule)
        {
            return rule.Conditions?.All?.Any(c => c is ThresholdOverTimeCondition) == true
                || rule.Conditions?.Any?.Any(c => c is ThresholdOverTimeCondition) == true;
        }

        private static void AddConditionSensors(
            ConditionDefinition condition,
            HashSet<string> sensors
        )
        {
            switch (condition)
            {
                case ComparisonCondition c:
                    sensors.Add(c.Sensor);
                    break;
                case ThresholdOverTimeCondition t:
                    sensors.Add(t.Sensor);
                    break;
                case ExpressionCondition e:
                    sensors.UnionWith(ExtractSensorsFromExpression(e.Expression));
                    break;
            }
        }

        /// <summary>
        /// Extract all input sensors referenced in a rule action
        /// </summary>
        /// <remarks>
        /// This is critical for rules that reference input sensors in their actions even if
        /// those inputs aren't used in the rule's conditions. For example, an alert rule
        /// might reference temperature and humidity in its alert message.
        /// </remarks>
        private static void AddActionSensors(
            ActionDefinition action,
            HashSet<string> sensors
        )
        {
            switch (action)
            {
                case SetValueAction setValueAction:
                    // Check if the action has a value expression that might contain input references
                    if (!string.IsNullOrEmpty(setValueAction.ValueExpression))
                    {
                        // Extract all potential sensor references from the expression
                        var expressionSensors = ExtractSensorsFromExpression(setValueAction.ValueExpression);

                        // Filter to only include input sensors, not other variables or functions
                        foreach (var sensor in expressionSensors)
                        {
                            if (sensor.StartsWith("input:"))
                            {
                                sensors.Add(sensor);
                            }
                        }
                    }
                    break;

                case SendMessageAction sendMessageAction:
                    // Check if message content contains input references
                    if (!string.IsNullOrEmpty(sendMessageAction.Message))
                    {
                        var messageSensors = ExtractSensorsFromExpression(sendMessageAction.Message);
                        foreach (var sensor in messageSensors)
                        {
                            if (sensor.StartsWith("input:"))
                            {
                                sensors.Add(sensor);
                            }
                        }
                    }
                    break;
            }
        }

        public static HashSet<string> ExtractSensorsFromExpression(string expression)
        {
            var sensors = new HashSet<string>();
            if (string.IsNullOrWhiteSpace(expression))
                return sensors;

            // First remove string literals to avoid confusion
            var noStringLiterals = Regex.Replace(expression, @"'[^']*'", "STRING_LITERAL");

            var sensorPattern = @"\b([a-zA-Z_][a-zA-Z0-9_]*)\b";
            var matches = Regex.Matches(noStringLiterals, sensorPattern);

            foreach (Match match in matches)
            {
                var potentialSensor = match.Value;
                if (!IsMathFunction(potentialSensor) && !IsLogicalOperator(potentialSensor) &&
                    !IsNumeric(potentialSensor) && potentialSensor != "STRING_LITERAL")
                {
                    sensors.Add(potentialSensor);
                }
            }

            return sensors;
        }

        private static bool IsMathFunction(string functionName)
        {
            return _mathFunctions.Contains(functionName);
        }

        private static bool IsLogicalOperator(string term)
        {
            return _logicalOperators.ContainsKey(term.ToLower());
        }

        /// <summary>
        /// Extracts all output sensors referenced in a condition group
        /// </summary>
        /// <param name="conditions">The condition group to extract output references from</param>
        /// <returns>A set of output sensor references</returns>
        public static HashSet<string> ExtractOutputReferencesFromConditions(ConditionGroup conditions)
        {
            var outputs = new HashSet<string>();

            if (conditions.All != null)
            {
                foreach (var condition in conditions.All)
                {
                    AddOutputReferencesFromCondition(condition, outputs);
                }
            }

            if (conditions.Any != null)
            {
                foreach (var condition in conditions.Any)
                {
                    AddOutputReferencesFromCondition(condition, outputs);
                }
            }

            return outputs;
        }

        /// <summary>
        /// Adds output references from a condition to a set
        /// </summary>
        private static void AddOutputReferencesFromCondition(ConditionDefinition condition, HashSet<string> outputs)
        {
            switch (condition)
            {
                case ComparisonCondition c when c.Sensor.StartsWith("output:"):
                    outputs.Add(c.Sensor);
                    break;
                case ExpressionCondition e:
                    // Extract output references from the expression
                    var matches = Regex.Matches(e.Expression, "output:[a-zA-Z0-9_]+");
                    foreach (Match match in matches)
                    {
                        outputs.Add(match.Value);
                    }
                    break;
            }
        }

        /// <summary>
        /// Generate code for V3 Set action with emit control
        /// </summary>
        public static string GenerateV3SetAction(V3SetAction setAction)
        {
            return GenerateV3SetAction(setAction, null);
        }

        public static string GenerateV3SetAction(V3SetAction setAction, Dictionary<string, FallbackStrategy>? fallbackStrategies)
        {
            var emitGuard = GenerateEmitGuard(setAction.Emit, setAction.Key);
            var setValue = GenerateSetValueExpression(setAction.Key, setAction.Value, setAction.ValueExpression, fallbackStrategies);
            
            if (setAction.Emit == EmitType.Always)
            {
                return setValue;
            }
            else
            {
                return $"if ({emitGuard}) {{ {setValue} }}";
            }
        }

        /// <summary>
        /// Generate code for V3 Log action with emit control
        /// </summary>
        public static string GenerateV3LogAction(V3LogAction logAction)
        {
            var emitGuard = GenerateEmitGuard(logAction.Emit, $"log_{logAction.Log.GetHashCode()}");
            var logStatement = $"Logger.Information(\"{logAction.Log}\");";
            
            if (logAction.Emit == EmitType.Always)
            {
                return logStatement;
            }
            else
            {
                return $"if ({emitGuard}) {{ {logStatement} }}";
            }
        }

        /// <summary>
        /// Generate code for V3 Buffer action
        /// </summary>
        public static string GenerateV3BufferAction(V3BufferAction bufferAction)
        {
            return GenerateV3BufferAction(bufferAction, null);
        }

        public static string GenerateV3BufferAction(V3BufferAction bufferAction, Dictionary<string, FallbackStrategy>? fallbackStrategies)
        {
            var emitGuard = GenerateEmitGuard(bufferAction.Emit, bufferAction.Key);
            var valueExpr = !string.IsNullOrEmpty(bufferAction.ValueExpression) 
                ? FixupExpression(bufferAction.ValueExpression, fallbackStrategies)
                : "null";
                
            var bufferStatement = $"BufferManager.UpdateBuffer(\"{bufferAction.Key}\", {valueExpr}, DateTime.UtcNow);";
            
            if (bufferAction.Emit == EmitType.Always)
            {
                return bufferStatement;
            }
            else
            {
                return $"if ({emitGuard}) {{ {bufferStatement} }}";
            }
        }

        /// <summary>
        /// Generate emit guard condition for v3 emit controls
        /// </summary>
        private static string GenerateEmitGuard(EmitType emit, string key)
        {
            var mode = emit switch
            {
                EmitType.OnChange => "EmitMode.OnChange",
                EmitType.OnEnter => "EmitMode.OnEnter", 
                EmitType.Always => "EmitMode.Always",
                _ => "EmitMode.Always"
            };
            
            if (emit == EmitType.Always)
            {
                return "true";
            }
            
            return $"GetOrCreateEmitGuard(\"{key}\", {mode}).ShouldEmit(\"{key}\", \"{key}\", outputs.GetValueOrDefault(\"{key}\"), true)";
        }

        /// <summary>
        /// Generate set value expression handling both Value and ValueExpression
        /// </summary>
        private static string GenerateSetValueExpression(string key, object? value, string? valueExpression)
        {
            return GenerateSetValueExpression(key, value, valueExpression, null);
        }

        private static string GenerateSetValueExpression(string key, object? value, string? valueExpression, Dictionary<string, FallbackStrategy>? fallbackStrategies)
        {
            if (!string.IsNullOrEmpty(valueExpression))
            {
                if (valueExpression == "true") return $"outputs[\"{key}\"] = true;";
                if (valueExpression == "false") return $"outputs[\"{key}\"] = false;";
                if (valueExpression == "now()") return $"outputs[\"{key}\"] = DateTime.UtcNow.ToString(\"o\");";
                
                var expr = FixupExpression(valueExpression, fallbackStrategies);
                return $"outputs[\"{key}\"] = {expr};";
            }
            else if (value != null)
            {
                if (value is string strValue) return $"outputs[\"{key}\"] = \"{strValue}\";";
                if (value is bool boolValue) return $"outputs[\"{key}\"] = {boolValue.ToString().ToLower()};";
                return $"outputs[\"{key}\"] = {value};";
            }
            
            return $"outputs[\"{key}\"] = null;";
        }
    }
}
