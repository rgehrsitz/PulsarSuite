using System;
using System.Collections.Generic;
using System.Linq;

namespace Beacon.Runtime
{
    public static class ThresholdHelper
    {
        /// <summary>
        /// Checks if all values for a sensor meet the specified threshold and comparison operator.
        /// </summary>
        public static bool CheckThreshold(IEnumerable<object> values, double threshold, string comparisonOperator)
        {
            if (values == null || !values.Any()) return false;

            switch (comparisonOperator)
            {
                case ">": return values.All(v => Convert.ToDouble(v) > threshold);
                case "<": return values.All(v => Convert.ToDouble(v) < threshold);
                case ">=": return values.All(v => Convert.ToDouble(v) >= threshold);
                case "<=": return values.All(v => Convert.ToDouble(v) <= threshold);
                case "==": return values.All(v => Convert.ToDouble(v) == threshold);
                case "!=": return values.All(v => Convert.ToDouble(v) != threshold);
                default: throw new ArgumentException($"Unsupported comparison operator: {comparisonOperator}");
            }
        }
    }
}