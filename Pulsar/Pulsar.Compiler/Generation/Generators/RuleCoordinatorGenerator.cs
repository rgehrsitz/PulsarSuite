// File: Pulsar.Compiler/Generation/Generators/RuleCoordinatorGenerator.cs

using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Compiler.Config;
using Pulsar.Compiler.Models;

namespace Pulsar.Compiler.Generation.Generators
{
    public class RuleCoordinatorGenerator(ILogger? logger = null)
    {
        private readonly ILogger _logger = logger ?? NullLogger.Instance;

        // Fix CS8625 by marking the logger parameter as nullable

        public GeneratedFileInfo GenerateRuleCoordinator(
            List<List<RuleDefinition>> ruleGroups,
            Dictionary<string, string> layerMap,
            BuildConfig buildConfig
        )
        {
            var sb = new StringBuilder();
            sb.AppendLine("// Auto-generated rule coordinator");
            sb.AppendLine("// Generated: " + DateTime.UtcNow.ToString("O"));
            sb.AppendLine();

            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using Serilog;");
            sb.AppendLine("using Prometheus;");
            sb.AppendLine("using Beacon.Runtime.Buffers;");
            sb.AppendLine("using Beacon.Runtime.Services;");
            sb.AppendLine("using Beacon.Runtime.Rules;");
            sb.AppendLine("using Beacon.Runtime.Interfaces;");
            sb.AppendLine();

            sb.AppendLine($"namespace {buildConfig.Namespace}");
            sb.AppendLine("{");
            sb.AppendLine("    public class RuleCoordinator : IRuleCoordinator");
            sb.AppendLine("    {");
            sb.AppendLine("        private readonly IRedisService _redis;");
            sb.AppendLine("        private readonly ILogger _logger;");
            sb.AppendLine("        private readonly RingBufferManager _bufferManager;");
            sb.AppendLine("        private readonly List<IRuleGroup> _ruleGroups;");
            sb.AppendLine("        private readonly MetricsService? _metrics;");
            sb.AppendLine();

            // RuleCount property implementation
            sb.AppendLine("        public int RuleCount => _ruleGroups.Count;");
            sb.AppendLine();

            // RequiredSensors property implementation
            sb.AppendLine(
                "        public string[] RequiredSensors => _ruleGroups.SelectMany(g => g.RequiredSensors).Distinct().ToArray();"
            );
            sb.AppendLine();

            // Add Prometheus metrics
            sb.AppendLine("        private static readonly Counter RuleEvaluationsTotal = Metrics");
            sb.AppendLine(
                "            .CreateCounter(\"pulsar_rule_evaluations_total\", \"Total number of rule evaluations\");"
            );
            sb.AppendLine();
            sb.AppendLine(
                "        private static readonly Histogram RuleEvaluationDuration = Metrics"
            );
            sb.AppendLine(
                "            .CreateHistogram(\"pulsar_rule_evaluation_duration_seconds\", \"Duration of rule evaluations\");"
            );
            sb.AppendLine();

            // Constructor
            sb.AppendLine(
                "        public RuleCoordinator(IRedisService redis, ILogger logger, RingBufferManager bufferManager, MetricsService? metrics = null)"
            );
            sb.AppendLine("        {");
            sb.AppendLine(
                "            _redis = redis ?? throw new ArgumentNullException(nameof(redis));"
            );
            sb.AppendLine(
                "            _logger = logger?.ForContext<RuleCoordinator>() ?? throw new ArgumentNullException(nameof(logger));"
            );
            sb.AppendLine(
                "            _bufferManager = bufferManager ?? throw new ArgumentNullException(nameof(bufferManager));"
            );
            sb.AppendLine(
                "            _metrics = metrics;"
            );
            sb.AppendLine("            _ruleGroups = new List<IRuleGroup>();");
            sb.AppendLine();

            // Initialize rule groups
            for (int i = 0; i < ruleGroups.Count; i++)
            {
                sb.AppendLine(
                    $"            _ruleGroups.Add(new RuleGroup{i}(_redis, _logger, _bufferManager));"
                );
            }

            sb.AppendLine("        }");
            sb.AppendLine();

            // ExecuteRulesAsync method (implementation of IRuleCoordinator)
            sb.AppendLine(
                "        public async Task<Dictionary<string, object>> ExecuteRulesAsync(Dictionary<string, object> inputs)"
            );
            sb.AppendLine("        {");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                using var timer = RuleEvaluationDuration.NewTimer();");
            sb.AppendLine("                var outputs = new Dictionary<string, object>();");
            sb.AppendLine();

            // Update buffers
            sb.AppendLine("                // Update buffers with current inputs");
            sb.AppendLine("                UpdateBuffers(inputs);");
            sb.AppendLine();

            // Fetch all initial inputs and outputs from Redis for cycle-aware testing
            sb.AppendLine("                // First, get all inputs and previous outputs from Redis to ensure we have all dependencies");
            sb.AppendLine("                var allRedisValues = await _redis.GetAllInputsAsync();");
            sb.AppendLine("                _logger.Debug(\"Loaded {Count} initial values from Redis\", allRedisValues.Count);");
            sb.AppendLine("                ");
            sb.AppendLine("                // Add any Redis values not already in our inputs dictionary");
            sb.AppendLine("                foreach (var kvp in allRedisValues)");
            sb.AppendLine("                {");
            sb.AppendLine("                    if (!inputs.ContainsKey(kvp.Key))");
            sb.AppendLine("                    {");
            sb.AppendLine("                        inputs[kvp.Key] = kvp.Value;");
            sb.AppendLine("                        if (kvp.Key.StartsWith(\"output:\"))");
            sb.AppendLine("                        {");
            sb.AppendLine("                            _logger.Debug(\"Added dependency from Redis: {Key} = {Value}\", kvp.Key, kvp.Value);");
            sb.AppendLine("                        }");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine();

            // Evaluate each rule group in sequence
            for (int i = 0; i < ruleGroups.Count; i++)
            {
                sb.AppendLine($"                _logger.Debug(\"Evaluating rule group {i}\");");
                sb.AppendLine($"                using (var ruleTimer = _metrics?.MeasureRuleExecutionTime(\"RuleGroup{i}\"))");
                sb.AppendLine($"                {{");
                
                // For rule groups after the first one, merge outputs from previous groups into inputs
                if (i > 0)
                {
                    sb.AppendLine("                    // Merge outputs from previous rule groups into inputs for this rule group");
                    sb.AppendLine("                    foreach (var output in outputs)");
                    sb.AppendLine("                    {");
                    sb.AppendLine("                        inputs[output.Key] = output.Value;");
                    sb.AppendLine("                        _logger.Debug(\"Added output to inputs: {Key} = {Value}\", output.Key, output.Value);");
                    sb.AppendLine("                    }");
                    sb.AppendLine();
                }
                
                sb.AppendLine($"                    await _ruleGroups[{i}].EvaluateRulesAsync(inputs, outputs);");
                sb.AppendLine($"                    _metrics?.RecordRuleExecution(\"RuleGroup{i}\", true);");
                sb.AppendLine($"                }}");
                sb.AppendLine($"                RuleEvaluationsTotal.Inc();");
            }

            sb.AppendLine();
            sb.AppendLine("                return outputs;");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (Exception ex)");
            sb.AppendLine("            {");
            sb.AppendLine("                _logger.Error(ex, \"Error evaluating rules\");");
            sb.AppendLine("                throw;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Helper method for buffer updates
            sb.AppendLine("        private void UpdateBuffers(Dictionary<string, object> inputs)");
            sb.AppendLine("        {");
            sb.AppendLine("            var now = DateTime.UtcNow;");
            sb.AppendLine("            foreach (var kvp in inputs)");
            sb.AppendLine("            {");
            sb.AppendLine("                string sensor = kvp.Key;");
            sb.AppendLine("                object value = kvp.Value;");
            sb.AppendLine();
            sb.AppendLine("                // Only handle numeric values for the buffer");
            sb.AppendLine("                if (value is double doubleValue)");
            sb.AppendLine("                {");
            sb.AppendLine(
                "                    _bufferManager.UpdateBuffer(sensor, doubleValue, now);"
            );
            sb.AppendLine("                }");
            sb.AppendLine(
                "                else if (double.TryParse(value.ToString(), out doubleValue))"
            );
            sb.AppendLine("                {");
            sb.AppendLine(
                "                    _bufferManager.UpdateBuffer(sensor, doubleValue, now);"
            );
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            // EvaluateAllRulesAsync method - convenience method that gets inputs from Redis
            sb.AppendLine("        public async Task EvaluateAllRulesAsync()");
            sb.AppendLine("        {");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                var inputs = await _redis.GetAllInputsAsync();");
            sb.AppendLine("                var outputs = await ExecuteRulesAsync(inputs);");
            sb.AppendLine();
            sb.AppendLine("                // Send all outputs to Redis");
            sb.AppendLine("                if (outputs.Count > 0)");
            sb.AppendLine("                {");
            sb.AppendLine("                    await _redis.SetOutputsAsync(outputs);");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (Exception ex)");
            sb.AppendLine("            {");
            sb.AppendLine(
                "                _logger.Error(ex, \"Error evaluating rules from Redis\");"
            );
            sb.AppendLine("                throw;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");

            // Add ResetTemporalState method
            sb.AppendLine();
            sb.AppendLine("        public void ResetTemporalState()");
            sb.AppendLine("        {");
            sb.AppendLine("            _logger.Information(\"Resetting temporal state for all rule groups\");");
            sb.AppendLine("            foreach (var ruleGroup in _ruleGroups)");
            sb.AppendLine("            {");
            sb.AppendLine("                ruleGroup.ResetTemporalState();");
            sb.AppendLine("            }");
            sb.AppendLine("        }");

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return new GeneratedFileInfo
            {
                FileName = "RuleCoordinator.cs",
                Content = sb.ToString(),
                Namespace = buildConfig.Namespace,
            };
        }
    }
}
