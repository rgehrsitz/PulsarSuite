// Auto-generated rule coordinator
// Generated: 2025-04-17T20:51:00.0419815Z

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using Prometheus;
using Beacon.Runtime.Buffers;
using Beacon.Runtime.Services;
using Beacon.Runtime.Rules;
using Beacon.Runtime.Interfaces;

namespace Generated
{
    public class RuleCoordinator : IRuleCoordinator
    {
        private readonly IRedisService _redis;
        private readonly ILogger _logger;
        private readonly RingBufferManager _bufferManager;
        private readonly List<IRuleGroup> _ruleGroups;
        private readonly MetricsService? _metrics;

        public int RuleCount => _ruleGroups.Count;

        public string[] RequiredSensors => _ruleGroups.SelectMany(g => g.RequiredSensors).Distinct().ToArray();

        private static readonly Counter RuleEvaluationsTotal = Metrics
            .CreateCounter("pulsar_rule_evaluations_total", "Total number of rule evaluations");

        private static readonly Histogram RuleEvaluationDuration = Metrics
            .CreateHistogram("pulsar_rule_evaluation_duration_seconds", "Duration of rule evaluations");

        public RuleCoordinator(IRedisService redis, ILogger logger, RingBufferManager bufferManager, MetricsService? metrics = null)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger?.ForContext<RuleCoordinator>() ?? throw new ArgumentNullException(nameof(logger));
            _bufferManager = bufferManager ?? throw new ArgumentNullException(nameof(bufferManager));
            _metrics = metrics;
            _ruleGroups = new List<IRuleGroup>();

        }

        public async Task<Dictionary<string, object>> ExecuteRulesAsync(Dictionary<string, object> inputs)
        {
            try
            {
                using var timer = RuleEvaluationDuration.NewTimer();
                var outputs = new Dictionary<string, object>();

                // Update buffers with current inputs
                UpdateBuffers(inputs);

                // First, get all inputs and previous outputs from Redis to ensure we have all dependencies
                var allRedisValues = await _redis.GetAllInputsAsync();
                _logger.Debug("Loaded {Count} initial values from Redis", allRedisValues.Count);
                
                // Add any Redis values not already in our inputs dictionary
                foreach (var kvp in allRedisValues)
                {
                    if (!inputs.ContainsKey(kvp.Key))
                    {
                        inputs[kvp.Key] = kvp.Value;
                        if (kvp.Key.StartsWith("output:"))
                        {
                            _logger.Debug("Added dependency from Redis: {Key} = {Value}", kvp.Key, kvp.Value);
                        }
                    }
                }


                return outputs;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error evaluating rules");
                throw;
            }
        }

        private void UpdateBuffers(Dictionary<string, object> inputs)
        {
            var now = DateTime.UtcNow;
            foreach (var kvp in inputs)
            {
                string sensor = kvp.Key;
                object value = kvp.Value;

                // Only handle numeric values for the buffer
                if (value is double doubleValue)
                {
                    _bufferManager.UpdateBuffer(sensor, doubleValue, now);
                }
                else if (double.TryParse(value.ToString(), out doubleValue))
                {
                    _bufferManager.UpdateBuffer(sensor, doubleValue, now);
                }
            }
        }

        public async Task EvaluateAllRulesAsync()
        {
            try
            {
                var inputs = await _redis.GetAllInputsAsync();
                var outputs = await ExecuteRulesAsync(inputs);

                // Send all outputs to Redis
                if (outputs.Count > 0)
                {
                    await _redis.SetOutputsAsync(outputs);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error evaluating rules from Redis");
                throw;
            }
        }
    }
}
