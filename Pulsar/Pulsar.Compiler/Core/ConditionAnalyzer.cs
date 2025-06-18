// File: Pulsar.Compiler/Core/ConditionAnalyzer.cs

using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Compiler.Models;
using Common;

namespace Pulsar.Compiler.Core
{
    /// <summary>
    /// Specialized analyzer for rule conditions
    /// </summary>
    public class ConditionAnalyzer
    {
        private readonly ILogger<ConditionAnalyzer> _logger;
        private static readonly HashSet<string> _mathFunctions = new()
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

        public ConditionAnalyzer(ILogger<ConditionAnalyzer>? logger = null)
        {
            _logger = logger ?? NullLogger<ConditionAnalyzer>.Instance;
        }

        /// <summary>
        /// Extract sensors used in a condition definition
        /// </summary>
        public HashSet<string> GetConditionDependencies(
            ConditionDefinition condition,
            Dictionary<string, RuleDefinition> rules)
        {
            // Use shared logic for sensor extraction
            return ConditionAnalyzerShared.ExtractSensors(condition);
        }

        /// <summary>
        /// Extract dependencies from a rule action
        /// </summary>
        public HashSet<string> GetActionDependencies(
            ActionDefinition action,
            Dictionary<string, RuleDefinition> rules)
        {
            var dependencies = new HashSet<string>();

            switch (action)
            {
                case SetValueAction set:
                    if (!string.IsNullOrEmpty(set.ValueExpression))
                    {
                        dependencies.UnionWith(ExtractSensorsFromExpression(set.ValueExpression));
                    }
                    break;
            }

            return dependencies;
        }

        /// <summary>
        /// Extracts sensor names from an expression string
        /// </summary>
        public HashSet<string> ExtractSensorsFromExpression(string expression)
        {
            var sensors = new HashSet<string>();
            var sensorPattern = @"\b([a-zA-Z_][a-zA-Z0-9_]*)\b";
            var matches = Regex.Matches(expression, sensorPattern);

            foreach (Match match in matches)
            {
                var potentialSensor = match.Value;
                if (!_mathFunctions.Contains(potentialSensor))
                {
                    sensors.Add(potentialSensor);
                    _logger.LogDebug("Extracted sensor {Sensor} from expression", potentialSensor);
                }
            }

            return sensors;
        }

        /// <summary>
        /// Determines if a condition uses temporal values
        /// </summary>
        public bool IsTemporalCondition(ConditionDefinition condition)
        {
            // Use shared logic for temporal detection
            return ConditionAnalyzerShared.HasTemporalCondition(condition);
        }

        /// <summary>
        /// Analyzes and returns the complexity of a condition
        /// </summary>
        public int GetConditionComplexity(ConditionDefinition condition)
        {
            int complexity = 1; // Base complexity

            switch (condition)
            {
                case ExpressionCondition expression:
                    // Expression conditions have higher complexity
                    complexity += ExtractSensorsFromExpression(expression.Expression).Count;
                    break;

                case ThresholdOverTimeCondition threshold:
                    // Temporal conditions have higher complexity
                    complexity += 2;
                    break;
            }

            return complexity;
        }

        /// <summary>
        /// Analyzes and returns the complexity of an action
        /// </summary>
        public int GetActionComplexity(ActionDefinition action)
        {
            int complexity = 1; // Base complexity

            switch (action)
            {
                case SetValueAction setValue:
                    if (!string.IsNullOrEmpty(setValue.ValueExpression))
                    {
                        // Expression-based actions have higher complexity
                        complexity += ExtractSensorsFromExpression(setValue.ValueExpression).Count;
                    }
                    break;
            }

            return complexity;
        }
    }
}