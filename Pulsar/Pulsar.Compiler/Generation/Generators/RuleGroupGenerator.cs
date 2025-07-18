// File: Pulsar.Compiler/Generation/Generators/RuleGroupGenerator.cs
// NOTE: This implementation includes AOT compatibility fixes.

using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Compiler.Config;
using Pulsar.Compiler.Generation.Helpers;
using Pulsar.Compiler.Models;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Pulsar.Compiler.Generation.Generators
{
    public class RuleGroupGenerator(ILogger? logger = null)
    {
        private readonly ILogger _logger = logger ?? NullLogger.Instance;

        public GeneratedFileInfo GenerateGroupImplementation(
            int groupId,
            List<RuleDefinition> rules,
            Dictionary<string, string> layerMap,
            BuildConfig buildConfig
        )
        {
            var sb = new StringBuilder();
            sb.AppendLine("// Auto-generated rule group");
            sb.AppendLine("// Generated: " + DateTime.UtcNow.ToString("O"));
            sb.AppendLine();

            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Collections.Concurrent;");
            sb.AppendLine("using System.Linq; // Required for Any() and All() extension methods");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using Serilog;");
            sb.AppendLine("using Prometheus;");
            sb.AppendLine("using StackExchange.Redis;");
            sb.AppendLine("using Beacon.Runtime.Buffers;");
            sb.AppendLine("using Beacon.Runtime.Rules;");
            sb.AppendLine("using Beacon.Runtime.Interfaces;");
            sb.AppendLine("using Beacon.Runtime.Services;");
            sb.AppendLine("using Pulsar.Runtime.Helpers;");
            sb.AppendLine("using ILogger = Serilog.ILogger;");
            sb.AppendLine();

            sb.AppendLine($"namespace {buildConfig.Namespace}");
            sb.AppendLine("{");

            // Class declaration
            sb.AppendLine($"    public class RuleGroup{groupId} : IRuleGroup");
            sb.AppendLine("    {");

            // Properties
            sb.AppendLine($"        public string Name => \"RuleGroup{groupId}\";");
            sb.AppendLine("        public IRedisService Redis { get; }");
            sb.AppendLine("        public ILogger Logger { get; }");
            sb.AppendLine("        public RingBufferManager BufferManager { get; }");
            sb.AppendLine();
            
            // v3 Infrastructure
            sb.AppendLine("        // v3 Infrastructure for temporal conditions and emit controls");
            sb.AppendLine("        private readonly ConcurrentDictionary<string, WindowTracker> _windowTrackers = new();");
            sb.AppendLine("        private readonly Dictionary<string, EmitGuard> _emitGuards = new();");
            sb.AppendLine();

            // Constructor
            sb.AppendLine($"        public RuleGroup{groupId}(");
            sb.AppendLine("            IRedisService redis,");
            sb.AppendLine("            ILogger logger,");
            sb.AppendLine("            RingBufferManager bufferManager)");
            sb.AppendLine("        {");
            sb.AppendLine("            Redis = redis;");
            sb.AppendLine($"            Logger = logger?.ForContext<RuleGroup{groupId}>();");
            sb.AppendLine("            BufferManager = bufferManager;");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Required sensors property - include both inputs and outputs that are referenced in conditions
            var allSensors = new HashSet<string>();

            // Add all input sensors and output sensors referenced in conditions
            foreach (var rule in rules)
            {
                // Get all input sensors (including those from actions with our fix)
                var inputSensors = GenerationHelpers.GetInputSensors(rule);
                foreach (var sensor in inputSensors)
                {
                    allSensors.Add(sensor);
                }

                // Also explicitly ensure output sensors referenced in conditions are added
                if (rule.Conditions != null)
                {
                    var outputReferences = GenerationHelpers.ExtractOutputReferencesFromConditions(rule.Conditions);
                    foreach (var outputRef in outputReferences)
                    {
                        allSensors.Add(outputRef);
                    }
                }
            }

            // Before generating the RequiredSensors array, add input sensors necessary for rule actions
            // Collect any input references from actions for each rule in this group
            foreach (var rule in rules)
            {
                foreach (var action in rule.Actions)
                {
                    if (action is SetValueAction setValueAction && !string.IsNullOrEmpty(setValueAction.ValueExpression))
                    {
                        // Check for input references in the value expression
                        var matches = System.Text.RegularExpressions.Regex.Matches(
                            setValueAction.ValueExpression, "input:[a-zA-Z0-9_]+");

                        foreach (System.Text.RegularExpressions.Match match in matches)
                        {
                            allSensors.Add(match.Value);
                        }
                    }
                }
            }

            if (allSensors.Count == 0)
            {
                sb.AppendLine("        public string[] RequiredSensors => new string[] {};");
            }
            else
            {
                sb.AppendLine("        public string[] RequiredSensors => new[]");
                sb.AppendLine("        {");
                foreach (var sensor in allSensors)
                {
                    sb.AppendLine($"            \"{sensor}\",");
                }
                sb.AppendLine("        };");
            }
            sb.AppendLine();

            // Rule evaluation method
            sb.AppendLine("        public async Task EvaluateRulesAsync(");
            sb.AppendLine("            Dictionary<string, object> inputs,");
            sb.AppendLine("            Dictionary<string, object> outputs)");
            sb.AppendLine("        {");

            foreach (var rule in rules)
            {
                // Add rule metadata as comments
                sb.AppendLine($"            // Rule: {rule.Name}");
                sb.AppendLine($"            // Layer: {layerMap[rule.Name]}");
                sb.AppendLine($"            // Source: {rule.SourceFile}:{rule.LineNumber}");
                sb.AppendLine();

                // Build fallback strategies dictionary for this rule
                var fallbackStrategies = new Dictionary<string, FallbackStrategy>();
                if (rule.Inputs != null)
                {
                    foreach (var input in rule.Inputs)
                    {
                        if (input.Fallback != null)
                        {
                            fallbackStrategies[input.Id] = input.Fallback;
                        }
                    }
                }

                // Generate v3 three-valued logic condition evaluation
                if (rule.Conditions != null)
                {
                    sb.AppendLine(
                        $"            var conditionResult_{rule.Name.Replace(" ", "_")} = {GenerationHelpers.GenerateCondition(rule.Conditions, fallbackStrategies)};"
                    );
                    sb.AppendLine();
                    
                    // Primary branch (True condition)
                    sb.AppendLine($"            if (conditionResult_{rule.Name.Replace(" ", "_")} == EvalResult.True)");
                    sb.AppendLine("            {");

                    // Generate primary actions
                    if (rule.Actions != null)
                    {
                        foreach (var action in rule.Actions)
                        {
                            sb.AppendLine(
                                $"                {GenerationHelpers.GenerateAction(action, fallbackStrategies)}"
                            );
                        }
                    }

                    sb.AppendLine("            }");
                    
                    // Else branch (False or Indeterminate condition)
                    if (rule.ElseActions != null && rule.ElseActions.Any())
                    {
                        sb.AppendLine($"            else if (conditionResult_{rule.Name.Replace(" ", "_")} == EvalResult.False || conditionResult_{rule.Name.Replace(" ", "_")} == EvalResult.Indeterminate)");
                        sb.AppendLine("            {");
                        
                        foreach (var action in rule.ElseActions)
                        {
                            sb.AppendLine(
                                $"                {GenerationHelpers.GenerateAction(action, fallbackStrategies)}"
                            );
                        }
                        
                        sb.AppendLine("            }");
                    }
                }
                else
                {
                    // If no conditions, always execute actions (EvalResult.True)
                    if (rule.Actions != null)
                    {
                        foreach (var action in rule.Actions)
                        {
                            sb.AppendLine(
                                $"            {GenerationHelpers.GenerateAction(action, fallbackStrategies)}"
                            );
                        }
                    }
                }

                sb.AppendLine();
            }

            sb.AppendLine("            await Task.CompletedTask;");
            sb.AppendLine("        }");

            // Add v3 helper methods
            sb.AppendLine();
            sb.AppendLine(GenerationHelpers.GenerateHelperMethods());
            sb.AppendLine();

            // Add SendMessage method for rules that publish messages
            sb.AppendLine("        private void SendMessage(string channel, string message)");
            sb.AppendLine("        {");
            sb.AppendLine("            // Implementation of sending messages to Redis channel");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                Redis.PublishAsync(channel, message);");
            sb.AppendLine(
                "                Logger.Information(\"Sent message to channel {Channel}: {Message}\", channel, message);"
            );
            sb.AppendLine("            }");
            sb.AppendLine("            catch (Exception ex)");
            sb.AppendLine("            {");
            sb.AppendLine(
                "                Logger.Error(ex, \"Failed to send message to channel {Channel}\", channel);"
            );
            sb.AppendLine("            }");
            sb.AppendLine("        }");

            // Add ResetTemporalState method for test scenario isolation
            sb.AppendLine();
            sb.AppendLine("        public void ResetTemporalState()");
            sb.AppendLine("        {");
            sb.AppendLine("            foreach (var tracker in _windowTrackers.Values)");
            sb.AppendLine("            {");
            sb.AppendLine("                tracker.Reset();");
            sb.AppendLine("            }");
            sb.AppendLine("            _emitGuards.Clear();");
            sb.AppendLine("        }");

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return new GeneratedFileInfo
            {
                FileName = $"RuleGroup{groupId}.cs",
                Content = sb.ToString(),
                Namespace = buildConfig.Namespace,
            };
        }
    }
}
