namespace Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public enum ValueTarget
    {
        Positive,
        Negative
    }

    public static class ValueGenerationHelper
    {
        private static readonly Random _random = new Random();

        public static object GenerateValueForSensor(
            object rule,
            string sensor,
            Func<object, string, List<object>> findConditionsForSensor,
            ValueTarget target
        )
        {
            var conditions = findConditionsForSensor(rule, sensor);
            if (conditions.Count == 0)
            {
                return GenerateDefaultValue(sensor);
            }
            if (conditions.Count == 1)
            {
                return GenerateValueForCondition(conditions[0], target);
            }
            var numericConditions = conditions.Where(c => c.GetType().Name == "ComparisonCondition").ToList();
            if (numericConditions.Count > 0)
            {
                return GenerateValueForMultipleConditions(numericConditions, target);
            }
            return GenerateValueForCondition(conditions[0], target);
        }

        public static object GenerateValueForTemporalCondition(
            object condition,
            int step,
            int totalSteps,
            ValueTarget target
        )
        {
            var thresholdProp = condition.GetType().GetProperty("Threshold");
            var opProp = condition.GetType().GetProperty("Operator");
            double threshold = thresholdProp != null ? (double)thresholdProp.GetValue(condition) : 0;
            string comparisonOperator = opProp?.GetValue(condition)?.ToString()?.ToLowerInvariant() ?? ">";
            if (comparisonOperator == "greater_than") comparisonOperator = ">";
            else if (comparisonOperator == "less_than") comparisonOperator = "<";
            else if (comparisonOperator == "greater_than_or_equal_to") comparisonOperator = ">=";
            else if (comparisonOperator == "less_than_or_equal_to") comparisonOperator = "<=";
            else if (comparisonOperator == "equal_to") comparisonOperator = "==";
            else if (comparisonOperator == "not_equal_to") comparisonOperator = "!=";

            if (target == ValueTarget.Positive)
            {
                switch (comparisonOperator)
                {
                    case ">": return threshold + 10 + (step * 2);
                    case ">=": return threshold + (step * 2);
                    case "<": return threshold - 10 - (step * 2);
                    case "<=": return threshold - (step * 2);
                    case "==": return threshold;
                    case "!=": return threshold + 10 + (step * 2);
                    default: return threshold + 10 + (step * 2);
                }
            }
            else
            {
                bool shouldBeAbove = (step % 2 == 0);
                switch (comparisonOperator)
                {
                    case ">": return shouldBeAbove ? threshold + 5 : threshold - 5;
                    case ">=": return shouldBeAbove ? threshold + 3 : threshold - 3;
                    case "<": return shouldBeAbove ? threshold + 5 : threshold - 5;
                    case "<=": return shouldBeAbove ? threshold + 3 : threshold - 3;
                    case "==": return shouldBeAbove ? threshold + 5 : threshold - 5;
                    case "!=": return shouldBeAbove ? threshold + 5 : threshold;
                    default: return shouldBeAbove ? threshold + 5 : threshold - 5;
                }
            }
        }

        public static object GenerateValueForCondition(object condition, ValueTarget target)
        {
            if (condition.GetType().Name == "ComparisonCondition")
            {
                var valueProp = condition.GetType().GetProperty("Value");
                var opProp = condition.GetType().GetProperty("Operator");
                double threshold = 0;
                if (valueProp != null)
                {
                    var val = valueProp.GetValue(condition);
                    if (val is double d) threshold = d;
                    else if (val is int i) threshold = i;
                    else if (val != null && double.TryParse(val.ToString(), out var parsed)) threshold = parsed;
                }
                string comparisonOperator = opProp?.GetValue(condition)?.ToString()?.ToLowerInvariant() ?? ">";
                return GenerateNumericValue(threshold, comparisonOperator, target);
            }
            if (condition.GetType().Name == "ThresholdOverTimeCondition")
            {
                return GenerateValueForTemporalCondition(condition, 0, 1, target);
            }
            return GenerateDefaultValue("unknown");
        }

        public static object GenerateValueForMultipleConditions(List<object> conditions, ValueTarget target)
        {
            if (conditions.Count > 0)
                return GenerateValueForCondition(conditions[0], target);
            return GenerateDefaultValue("unknown");
        }

        public static object GenerateDefaultValue(string sensor)
        {
            if (sensor.ToLower().Contains("temp")) return 25.0;
            if (sensor.ToLower().Contains("humidity")) return 50.0;
            if (sensor.ToLower().Contains("pressure")) return 101.3;
            if (sensor.ToLower().Contains("bool") || sensor.ToLower().Contains("flag")) return true;
            return _random.NextDouble() * 100;
        }

        public static double GenerateNumericValue(double threshold, string comparisonOperator, ValueTarget target)
        {
            if (target == ValueTarget.Positive)
            {
                switch (comparisonOperator)
                {
                    case ">": case "gt": return threshold + 1;
                    case ">=": case "gte": return threshold;
                    case "<": case "lt": return threshold - 1;
                    case "<=": case "lte": return threshold;
                    case "==": case "eq": return threshold;
                    case "!=": case "ne": case "neq": return threshold + 1;
                    default: return threshold + 1;
                }
            }
            else
            {
                switch (comparisonOperator)
                {
                    case ">": case "gt": return threshold - 1;
                    case ">=": case "gte": return threshold - 1;
                    case "<": case "lt": return threshold + 1;
                    case "<=": case "lte": return threshold + 1;
                    case "==": case "eq": return threshold + 1;
                    case "!=": case "ne": case "neq": return threshold;
                    default: return threshold - 10;
                }
            }
        }
    }
}