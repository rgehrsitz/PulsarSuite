// File: Pulsar.Compiler/Models/RuleManifest.cs

using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace Pulsar.Compiler.Models
{
    public class RuleManifest
    {
        private static readonly ILogger _logger = LoggingConfig.GetLogger();

        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion { get; set; } = "1.0";

        [JsonPropertyName("generatedAt")]
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("files")]
        public List<GeneratedFileInfo> Files { get; set; } = new();

        [JsonPropertyName("rules")]
        public Dictionary<string, RuleMetadata> Rules { get; set; } = new();

        // New properties for enhanced tracking
        [JsonPropertyName("buildMetrics")]
        public BuildMetrics BuildMetrics { get; set; } = new BuildMetrics();

        [JsonPropertyName("dependencyAnalysis")]
        public DependencyAnalysis DependencyAnalysis { get; set; } = new DependencyAnalysis();

        public void SaveToFile(string path)
        {
            try
            {
                _logger.Debug("Saving rule manifest to {Path}", path);
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                };

                var json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(path, json);
                _logger.Information("Successfully saved manifest with {Count} rules", Rules.Count);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save rule manifest to {Path}", path);
                throw;
            }
        }

        public static RuleManifest LoadFromFile(string path)
        {
            try
            {
                _logger.Debug("Loading rule manifest from {Path}", path);
                var json = File.ReadAllText(path);
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                };

                var manifest =
                    JsonSerializer.Deserialize<RuleManifest>(json, options)
                    ?? throw new InvalidOperationException("Failed to deserialize manifest");
                _logger.Information(
                    "Successfully loaded manifest with {Count} rules",
                    manifest.Rules.Count
                );
                return manifest;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load rule manifest from {Path}", path);
                throw;
            }
        }
    }

    public class BuildMetrics
    {
        [JsonPropertyName("totalRules")]
        public int TotalRules { get; set; }

        [JsonPropertyName("ruleComplexities")]
        public Dictionary<string, int> RuleComplexities { get; set; } = new();

        [JsonPropertyName("temporalRuleCount")]
        public int TemporalRuleCount { get; set; }

        [JsonPropertyName("averageRuleComplexity")]
        public double AverageRuleComplexity { get; set; }
    }

    public class DependencyAnalysis
    {
        [JsonPropertyName("ruleDependencies")]
        public Dictionary<string, List<string>> RuleDependencies { get; set; } = new();

        [JsonPropertyName("sensorDependencies")]
        public Dictionary<string, List<string>> SensorDependencies { get; set; } = new();

        [JsonPropertyName("maxDependencyDepth")]
        public int MaxDependencyDepth { get; set; }
    }

    public class GeneratedFileInfo
    {
        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("filePath")]
        public string FilePath { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("ruleLayerRange")]
        public RuleLayerRange LayerRange { get; set; } = new();

        [JsonPropertyName("containedRules")]
        public List<string> ContainedRules { get; set; } = new();

        [JsonPropertyName("hash")]
        public string Hash { get; set; } = string.Empty;

        [JsonPropertyName("namespace")]
        public string Namespace { get; set; } = "Pulsar.Generated";

        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0.0";

        [JsonPropertyName("ruleSourceMap")]
        public Dictionary<string, GeneratedSourceInfo> RuleSourceMap { get; set; } = new();
    }

    public class RuleLayerRange
    {
        [JsonPropertyName("start")]
        public int Start { get; set; }

        [JsonPropertyName("end")]
        public int End { get; set; }
    }

    public class RuleMetadata
    {
        [JsonPropertyName("sourceFile")]
        public string SourceFile { get; set; } = string.Empty;

        [JsonPropertyName("sourceLineNumber")]
        public int SourceLineNumber { get; set; }

        [JsonPropertyName("dependencies")]
        public List<string> Dependencies { get; set; } = new();

        [JsonPropertyName("layer")]
        public int Layer { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("inputSensors")]
        public List<string> InputSensors { get; set; } = new();

        [JsonPropertyName("outputSensors")]
        public List<string> OutputSensors { get; set; } = new();

        [JsonPropertyName("usesTemporalConditions")]
        public bool UsesTemporalConditions { get; set; }

        [JsonPropertyName("estimatedComplexity")]
        public int EstimatedComplexity { get; set; }
    }

    public class GeneratedSourceInfo
    {
        [JsonPropertyName("sourceFile")]
        public string SourceFile { get; set; } = string.Empty;

        [JsonPropertyName("lineNumber")]
        public int LineNumber { get; set; }

        [JsonPropertyName("generatedFile")]
        public string GeneratedFile { get; set; } = string.Empty;

        [JsonPropertyName("generatedLineStart")]
        public int GeneratedLineStart { get; set; }

        [JsonPropertyName("generatedLineEnd")]
        public int GeneratedLineEnd { get; set; }
    }

    public class ManifestGenerator
    {
        private readonly List<RuleDefinition> _rules;
        private readonly List<GeneratedFileInfo> _generatedFiles;
        private readonly Dictionary<string, RuleDefinition> _rulesByName;

        public ManifestGenerator(List<RuleDefinition> rules, List<GeneratedFileInfo> generatedFiles)
        {
            _rules = rules;
            _generatedFiles = generatedFiles;
            _rulesByName = rules.ToDictionary(r => r.Name);
        }

        public RuleManifest GenerateManifest()
        {
            var manifest = new RuleManifest();
            var ruleLayerMap = BuildRuleLayerMap();

            // Process each generated file
            foreach (var file in _generatedFiles)
            {
                var fileInfo = new GeneratedFileInfo
                {
                    FileName = file.FileName,
                    FilePath = file.FilePath,
                    Content = file.Content,
                    Hash = ComputeHash(file.Content),
                    ContainedRules = ExtractContainedRules(file.Content),
                };

                // Determine layer range for this file
                if (fileInfo.ContainedRules.Any())
                {
                    var layers = fileInfo
                        .ContainedRules.Select(r => ruleLayerMap[r])
                        .OrderBy(l => l)
                        .ToList();

                    fileInfo.LayerRange = new RuleLayerRange
                    {
                        Start = layers.First(),
                        End = layers.Last(),
                    };
                }

                manifest.Files.Add(fileInfo);
            }

            // Process each rule's metadata
            foreach (var rule in _rules)
            {
                var metadata = new RuleMetadata
                {
                    SourceFile = rule.SourceFile ?? "unknown",
                    SourceLineNumber = rule.LineNumber,
                    Layer = ruleLayerMap[rule.Name],
                    Description = rule.Description,
                    Dependencies = GetRuleDependencies(rule),
                    InputSensors = GetRuleInputs(rule),
                    OutputSensors = GetRuleOutputs(rule),
                    UsesTemporalConditions = HasTemporalConditions(rule),
                };

                manifest.Rules[rule.Name] = metadata;
            }

            return manifest;
        }

        private Dictionary<string, int> BuildRuleLayerMap()
        {
            // This would use the same layer calculation logic as the CodeGenerator
            // For now, we'll use a simple implementation
            var layerMap = new Dictionary<string, int>();
            var dependencyGraph = BuildDependencyGraph();
            var visited = new HashSet<string>();
            var visiting = new HashSet<string>();

            foreach (var rule in _rules)
            {
                if (!visited.Contains(rule.Name))
                {
                    CalculateLayer(rule.Name, dependencyGraph, layerMap, visited, visiting);
                }
            }

            return layerMap;
        }

        private Dictionary<string, HashSet<string>> BuildDependencyGraph()
        {
            var graph = new Dictionary<string, HashSet<string>>();
            foreach (var rule in _rules)
            {
                graph[rule.Name] = new HashSet<string>();
                var dependencies = GetRuleDependencies(rule);
                foreach (var dep in dependencies)
                {
                    if (_rulesByName.ContainsKey(dep))
                    {
                        graph[rule.Name].Add(dep);
                    }
                }
            }
            return graph;
        }

        private void CalculateLayer(
            string ruleName,
            Dictionary<string, HashSet<string>> graph,
            Dictionary<string, int> layerMap,
            HashSet<string> visited,
            HashSet<string> visiting
        )
        {
            if (visiting.Contains(ruleName))
                throw new InvalidOperationException($"Cycle detected involving rule '{ruleName}'");

            if (visited.Contains(ruleName))
                return;

            visiting.Add(ruleName);

            int maxDependencyLayer = -1;
            foreach (var dep in graph[ruleName])
            {
                if (!layerMap.ContainsKey(dep))
                {
                    CalculateLayer(dep, graph, layerMap, visited, visiting);
                }
                maxDependencyLayer = Math.Max(maxDependencyLayer, layerMap[dep]);
            }

            layerMap[ruleName] = maxDependencyLayer + 1;
            visiting.Remove(ruleName);
            visited.Add(ruleName);
        }

        private string ComputeHash(string content)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        private List<string> ExtractContainedRules(string content)
        {
            // Basic implementation - could be enhanced with regex
            var rules = new List<string>();
            var lines = content.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains("// Rule: "))
                {
                    var ruleName = line.Split("// Rule: ")[1].Trim();
                    rules.Add(ruleName);
                }
            }
            return rules.Distinct().ToList();
        }

        private List<string> GetRuleDependencies(RuleDefinition rule)
        {
            var dependencies = new HashSet<string>();

            if (rule.Conditions?.All != null)
            {
                foreach (var cond in rule.Conditions.All)
                {
                    AddConditionDependencies(cond, dependencies);
                }
            }

            if (rule.Conditions?.Any != null)
            {
                foreach (var cond in rule.Conditions.Any)
                {
                    AddConditionDependencies(cond, dependencies);
                }
            }

            return dependencies.ToList();
        }

        private void AddConditionDependencies(
            ConditionDefinition condition,
            HashSet<string> dependencies
        )
        {
            if (condition is ComparisonCondition comp)
            {
                foreach (var rule in _rules)
                {
                    if (rule.Actions.OfType<SetValueAction>().Any(a => a.Key == comp.Sensor))
                    {
                        dependencies.Add(rule.Name);
                    }
                }
            }
            else if (condition is ExpressionCondition expr)
            {
                // This is a simplified check - could be enhanced with proper expression parsing
                foreach (var rule in _rules)
                {
                    foreach (var action in rule.Actions.OfType<SetValueAction>())
                    {
                        if (expr.Expression.Contains(action.Key))
                        {
                            dependencies.Add(rule.Name);
                        }
                    }
                }
            }
        }

        private List<string> GetRuleInputs(RuleDefinition rule)
        {
            var inputs = new HashSet<string>();

            void AddConditionInputs(ConditionDefinition condition)
            {
                if (condition is ComparisonCondition comp)
                {
                    inputs.Add(comp.Sensor);
                }
                else if (condition is ThresholdOverTimeCondition temporal)
                {
                    inputs.Add(temporal.Sensor);
                }
            }

            if (rule.Conditions?.All != null)
            {
                foreach (var cond in rule.Conditions.All)
                {
                    AddConditionInputs(cond);
                }
            }

            if (rule.Conditions?.Any != null)
            {
                foreach (var cond in rule.Conditions.Any)
                {
                    AddConditionInputs(cond);
                }
            }

            return inputs.ToList();
        }

        private List<string> GetRuleOutputs(RuleDefinition rule)
        {
            return rule.Actions.OfType<SetValueAction>().Select(a => a.Key).ToList();
        }

        private bool HasTemporalConditions(RuleDefinition rule)
        {
            bool CheckCondition(ConditionDefinition condition)
            {
                return condition is ThresholdOverTimeCondition;
            }

            if (rule.Conditions?.All != null && rule.Conditions.All.Any(CheckCondition))
                return true;

            if (rule.Conditions?.Any != null && rule.Conditions.Any.Any(CheckCondition))
                return true;

            return false;
        }
    }
}
