// File: Pulsar.Compiler/Config/Templates/Runtime/TemplateRuleGroup.cs
// Version: 1.0.0

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Beacon.Runtime.Buffers;
using Beacon.Runtime.Interfaces;
using Beacon.Runtime.Services;
using Microsoft.Extensions.Logging;
using Beacon.Runtime;

namespace Beacon.Runtime.Rules
{
    public abstract class TemplateRuleGroup : IRuleGroup
    {
        protected readonly IRedisService _redis;
        protected readonly ILogger _logger;
        protected readonly RingBufferManager _bufferManager;

        protected TemplateRuleGroup(
            IRedisService redis,
            ILogger logger,
            RingBufferManager bufferManager
        )
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _bufferManager =
                bufferManager ?? throw new ArgumentNullException(nameof(bufferManager));
        }

        public abstract string[] RequiredSensors { get; }

        public async Task EvaluateRulesAsync(
            Dictionary<string, object> inputs,
            Dictionary<string, object> outputs
        )
        {
            try
            {
                _logger.LogDebug("Evaluating rule group");

                // Update buffers with current values
                var currentValues = new Dictionary<string, double>();
                foreach (var (sensor, value) in inputs)
                {
                    if (value is double doubleValue)
                    {
                        currentValues[sensor] = doubleValue;
                    }
                    else if (double.TryParse(value.ToString(), out doubleValue))
                    {
                        currentValues[sensor] = doubleValue;
                    }
                }
                _bufferManager.UpdateBuffers(currentValues);

                // Evaluate rules
                await EvaluateRulesInternalAsync(inputs, outputs);
                _logger.LogDebug("Rule group evaluation complete");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating rule group");
                throw;
            }
        }

        protected abstract Task EvaluateRulesInternalAsync(
            Dictionary<string, object> inputs,
            Dictionary<string, object> outputs
        );

        protected bool CheckThreshold(
            string sensor,
            double threshold,
            TimeSpan duration,
            string comparisonOperator
        )
        {
            // Gather values from the buffer for the given sensor and duration
            var values = _bufferManager.GetValues(sensor, duration).Select(v => v.Value);
            return ThresholdHelper.CheckThreshold(values, threshold, comparisonOperator);
        }
    }
}
