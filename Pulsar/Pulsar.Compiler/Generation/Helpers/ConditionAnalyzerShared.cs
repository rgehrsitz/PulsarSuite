using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Common.Analysis
{
    /// <summary>
    /// Shared logic for analyzing rule conditions: sensor extraction, group traversal, and temporal detection.
    /// </summary>
    public static class ConditionAnalyzerShared
    {
        private static readonly Regex SensorRegex = new Regex(
            @"input:[a-zA-Z0-9_]+|output:[a-zA-Z0-9_]+|buffer:[a-zA-Z0-9_]+",
            RegexOptions.Compiled
        );

        /// <summary>
        /// Recursively extracts all sensors referenced in a condition tree.
        /// </summary>
        public static HashSet<string> ExtractSensors(object condition)
        {
            var sensors = new HashSet<string>();
            if (condition == null) return sensors;

            // Handle group conditions
            if (condition.GetType().Name == "ConditionGroup")
            {
                var group = condition;
                var allProp = group.GetType().GetProperty("All");
                var anyProp = group.GetType().GetProperty("Any");
                if (allProp != null)
                {
                    var all = allProp.GetValue(group) as System.Collections.IEnumerable;
                    if (all != null)
                    {
                        foreach (var wrapper in all)
                        {
                            var condProp = wrapper.GetType().GetProperty("Condition");
                            var child = condProp?.GetValue(wrapper);
                            if (child != null)
                                sensors.UnionWith(ExtractSensors(child));
                        }
                    }
                }
                if (anyProp != null)
                {
                    var any = anyProp.GetValue(group) as System.Collections.IEnumerable;
                    if (any != null)
                    {
                        foreach (var wrapper in any)
                        {
                            var condProp = wrapper.GetType().GetProperty("Condition");
                            var child = condProp?.GetValue(wrapper);
                            if (child != null)
                                sensors.UnionWith(ExtractSensors(child));
                        }
                    }
                }
            }
            // Handle comparison/threshold conditions
            else if (condition.GetType().Name == "ComparisonCondition" || condition.GetType().Name == "ThresholdOverTimeCondition")
            {
                var sensorProp = condition.GetType().GetProperty("Sensor");
                var sensor = sensorProp?.GetValue(condition) as string;
                if (!string.IsNullOrEmpty(sensor))
                    sensors.Add(sensor);
                // Check for value expressions
                var valueExprProp = condition.GetType().GetProperty("ValueExpression");
                var valueExpr = valueExprProp?.GetValue(condition) as string;
                if (!string.IsNullOrEmpty(valueExpr))
                    sensors.UnionWith(ExtractSensorsFromExpression(valueExpr));
            }
            // Handle expression conditions
            else if (condition.GetType().Name == "ExpressionCondition")
            {
                var exprProp = condition.GetType().GetProperty("Expression");
                var expr = exprProp?.GetValue(condition) as string;
                if (!string.IsNullOrEmpty(expr))
                    sensors.UnionWith(ExtractSensorsFromExpression(expr));
            }
            return sensors;
        }

        /// <summary>
        /// Extracts sensors from an expression string.
        /// </summary>
        public static HashSet<string> ExtractSensorsFromExpression(string expression)
        {
            var sensors = new HashSet<string>();
            if (string.IsNullOrEmpty(expression)) return sensors;
            var matches = SensorRegex.Matches(expression);
            foreach (Match match in matches)
                sensors.Add(match.Value);
            return sensors;
        }

        /// <summary>
        /// Recursively checks if a condition tree contains any temporal (threshold-over-time) condition.
        /// </summary>
        public static bool HasTemporalCondition(object condition)
        {
            if (condition == null) return false;
            if (condition.GetType().Name == "ThresholdOverTimeCondition")
                return true;
            if (condition.GetType().Name == "ConditionGroup")
            {
                var group = condition;
                var allProp = group.GetType().GetProperty("All");
                var anyProp = group.GetType().GetProperty("Any");
                if (allProp != null)
                {
                    var all = allProp.GetValue(group) as System.Collections.IEnumerable;
                    if (all != null)
                    {
                        foreach (var wrapper in all)
                        {
                            var condProp = wrapper.GetType().GetProperty("Condition");
                            var child = condProp?.GetValue(wrapper);
                            if (child != null && HasTemporalCondition(child))
                                return true;
                        }
                    }
                }
                if (anyProp != null)
                {
                    var any = anyProp.GetValue(group) as System.Collections.IEnumerable;
                    if (any != null)
                    {
                        foreach (var wrapper in any)
                        {
                            var condProp = wrapper.GetType().GetProperty("Condition");
                            var child = condProp?.GetValue(wrapper);
                            if (child != null && HasTemporalCondition(child))
                                return true;
                        }
                    }
                }
            }
            return false;
        }
    }
}