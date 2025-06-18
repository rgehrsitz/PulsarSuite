using BeaconTester.RuleAnalyzer.Analysis;
using BeaconTester.RuleAnalyzer.Parsing;
using Serilog;
using Common;

namespace BeaconTester.RuleAnalyzer.Generation
{
    /// <summary>
    /// Target for value generation
    /// </summary>
    public enum ValueTarget
    {
        /// <summary>
        /// Generate values that satisfy conditions
        /// </summary>
        Positive,

        /// <summary>
        /// Generate values that don't satisfy conditions
        /// </summary>
        Negative,
    }

    /// <summary>
    /// Generates test values for conditions
    /// </summary>
    public class ValueGenerator
    {
        private readonly ILogger _logger;
        private readonly Random _random = new Random();

        /// <summary>
        /// Creates a new value generator
        /// </summary>
        public ValueGenerator(ILogger logger)
        {
            _logger = logger.ForContext<ValueGenerator>();
        }

        /// <summary>
        /// Generates a value for a sensor based on rule conditions
        /// </summary>
        public object GenerateValueForSensor(RuleDefinition rule, string sensor, ValueTarget target)
        {
            // Use shared helper, passing a delegate to find conditions for the sensor
            return ValueGenerationHelper.GenerateValueForSensor(
                rule,
                sensor,
                (r, s) => FindConditionsForSensor(((RuleDefinition)r).Conditions, s).Cast<object>().ToList(),
                (Common.ValueTarget)(int)target
            );
        }

        /// <summary>
        /// Generates a value for a temporal condition
        /// </summary>
        public object GenerateValueForTemporalCondition(
            ThresholdOverTimeCondition condition,
            int step,
            int totalSteps,
            ValueTarget target
        )
        {
            return ValueGenerationHelper.GenerateValueForTemporalCondition(
                condition,
                step,
                totalSteps,
                (Common.ValueTarget)(int)target
            );
        }

        /// <summary>
        /// Finds all conditions that reference a specific sensor
        /// </summary>
        private List<ConditionDefinition> FindConditionsForSensor(
            ConditionDefinition condition,
            string sensor
        )
        {
            var matchingConditions = new List<ConditionDefinition>();

            if (condition is ComparisonCondition comparison)
            {
                if (comparison.Sensor == sensor)
                {
                    matchingConditions.Add(comparison);
                }
            }
            else if (condition is ThresholdOverTimeCondition temporal)
            {
                if (temporal.Sensor == sensor)
                {
                    matchingConditions.Add(temporal);
                }
            }
            else if (condition is ConditionGroup group)
            {
                // Process 'all' conditions
                foreach (var wrapper in group.All)
                {
                    if (wrapper.Condition != null)
                    {
                        matchingConditions.AddRange(FindConditionsForSensor(wrapper.Condition, sensor));
                    }
                }
                // Process 'any' conditions
                foreach (var wrapper in group.Any)
                {
                    if (wrapper.Condition != null)
                    {
                        matchingConditions.AddRange(FindConditionsForSensor(wrapper.Condition, sensor));
                    }
                }
            }
            return matchingConditions;
        }

        /// <summary>
        /// Generates a value for a specific condition
        /// </summary>
        public object GenerateValueForCondition(ConditionDefinition condition, ValueTarget target)
        {
            return ValueGenerationHelper.GenerateValueForCondition(
                condition,
                (Common.ValueTarget)(int)target
            );
        }

        /// <summary>
        /// Generates a value that satisfies multiple conditions
        /// </summary>
        private object GenerateValueForMultipleConditions(
            List<ComparisonCondition> conditions,
            ValueTarget target
        )
        {
            return ValueGenerationHelper.GenerateValueForMultipleConditions(
                conditions.Cast<object>().ToList(),
                (Common.ValueTarget)(int)target
            );
        }

        /// <summary>
        /// Generates a default value based on sensor name
        /// </summary>
        private object GenerateDefaultValue(string sensor)
        {
            return ValueGenerationHelper.GenerateDefaultValue(sensor);
        }
    }
}
