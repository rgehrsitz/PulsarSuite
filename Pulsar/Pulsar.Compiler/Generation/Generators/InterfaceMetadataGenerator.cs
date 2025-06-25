using System.Text.Json;
using Pulsar.Compiler.Models;
using Serilog;

namespace Pulsar.Compiler.Generation.Generators
{
    /// <summary>
    /// Generates interface metadata for UI components and dashboards
    /// </summary>
    public class InterfaceMetadataGenerator
    {
        private readonly ILogger _logger;

        public InterfaceMetadataGenerator(ILogger logger)
        {
            _logger = logger.ForContext<InterfaceMetadataGenerator>();
        }

        /// <summary>
        /// Generates interface outputs metadata from rules
        /// </summary>
        public string GenerateInterfaceOutputsMetadata(List<RuleDefinition> rules, string outputPath)
        {
            _logger.Information("Generating interface outputs metadata for {RuleCount} rules", rules.Count);

            var outputs = ExtractOutputsFromRules(rules);
            var metadata = new InterfaceOutputsCatalog
            {
                Version = 1,
                Outputs = outputs
            };

            var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            var metadataPath = Path.Combine(outputPath, "interface_outputs.json");
            File.WriteAllText(metadataPath, json);

            _logger.Information("Generated interface outputs metadata at: {MetadataPath}", metadataPath);
            return metadataPath;
        }

        /// <summary>
        /// Generates sensor catalog metadata for UI components
        /// </summary>
        public string GenerateSensorCatalogMetadata(string catalogPath, string outputPath)
        {
            if (!File.Exists(catalogPath))
            {
                _logger.Warning("Sensor catalog not found: {CatalogPath}", catalogPath);
                return "";
            }

            var targetPath = Path.Combine(outputPath, "sensor_catalog.json");
            File.Copy(catalogPath, targetPath, overwrite: true);

            _logger.Information("Copied sensor catalog metadata to: {TargetPath}", targetPath);
            return targetPath;
        }

        /// <summary>
        /// Generates a unified data dictionary combining sensors and outputs
        /// </summary>
        public string GenerateDataDictionary(List<RuleDefinition> rules, string? catalogPath, string outputPath)
        {
            _logger.Information("Generating unified data dictionary");

            var dataDictionary = new DataDictionary
            {
                Version = 1,
                GeneratedAt = DateTime.UtcNow,
                Outputs = ExtractOutputsFromRules(rules)
            };

            // Include sensor information if catalog is available
            if (!string.IsNullOrEmpty(catalogPath) && File.Exists(catalogPath))
            {
                try
                {
                    var catalogContent = File.ReadAllText(catalogPath);
                    var catalog = JsonSerializer.Deserialize<SensorCatalog>(catalogContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        AllowTrailingCommas = true
                    });

                    if (catalog?.Sensors != null)
                    {
                        dataDictionary.Sensors = catalog.Sensors.Select(s => new SensorMetadata
                        {
                            Id = s.Id,
                            Type = s.Type,
                            Unit = s.Unit,
                            Description = s.Description,
                            Source = s.Source,
                            Export = s.Export
                        }).ToList();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to load sensor catalog: {CatalogPath}", catalogPath);
                }
            }

            var json = JsonSerializer.Serialize(dataDictionary, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            var dictionaryPath = Path.Combine(outputPath, "data_dictionary.json");
            File.WriteAllText(dictionaryPath, json);

            _logger.Information("Generated data dictionary at: {DictionaryPath}", dictionaryPath);
            return dictionaryPath;
        }

        private List<OutputMetadata> ExtractOutputsFromRules(List<RuleDefinition> rules)
        {
            var outputs = new Dictionary<string, OutputMetadata>();
            var groupCounter = 0;

            foreach (var rule in rules)
            {
                if (rule.Actions == null) continue;

                foreach (var action in rule.Actions)
                {
                    switch (action)
                    {
                        case SetValueAction setAction when !string.IsNullOrEmpty(setAction.Key):
                            var outputId = setAction.Key;
                            if (!outputs.ContainsKey(outputId))
                            {
                                outputs[outputId] = CreateOutputMetadata(outputId, rule, groupCounter++);
                            }
                            break;
                        case V3SetAction v3SetAction when !string.IsNullOrEmpty(v3SetAction.Key):
                            var v3OutputId = v3SetAction.Key;
                            if (!outputs.ContainsKey(v3OutputId))
                            {
                                outputs[v3OutputId] = CreateOutputMetadata(v3OutputId, rule, groupCounter++);
                            }
                            break;
                    }
                }

                // Also check else actions
                if (rule.ElseActions == null || rule.ElseActions.Count == 0) continue;

                foreach (var action in rule.ElseActions)
                {
                    switch (action)
                    {
                        case SetValueAction setAction when !string.IsNullOrEmpty(setAction.Key):
                            var outputId = setAction.Key;
                            if (!outputs.ContainsKey(outputId))
                            {
                                outputs[outputId] = CreateOutputMetadata(outputId, rule, groupCounter++);
                            }
                            break;
                        case V3SetAction v3SetAction when !string.IsNullOrEmpty(v3SetAction.Key):
                            var v3OutputId = v3SetAction.Key;
                            if (!outputs.ContainsKey(v3OutputId))
                            {
                                outputs[v3OutputId] = CreateOutputMetadata(v3OutputId, rule, groupCounter++);
                            }
                            break;
                    }
                }
            }

            return outputs.Values.OrderBy(o => o.Group).ThenBy(o => o.Order).ToList();
        }

        private OutputMetadata CreateOutputMetadata(string outputId, RuleDefinition rule, int order)
        {
            // Infer metadata from rule and output key
            var widget = InferWidgetType(outputId, rule);
            var displayName = GenerateDisplayName(outputId);
            var group = InferGroup(rule.Name);
            var description = $"Output from rule: {rule.Name}";

            return new OutputMetadata
            {
                Id = outputId,
                DisplayName = displayName,
                Description = description,
                Widget = widget,
                Group = group,
                Order = order,
                Export = true,
                DefaultVisibility = true
            };
        }

        private string InferWidgetType(string outputId, RuleDefinition rule)
        {
            var lowerKey = outputId.ToLowerInvariant();
            
            // Check for boolean indicators
            if (lowerKey.Contains("flag") || lowerKey.Contains("alert") || 
                lowerKey.Contains("status") || lowerKey.Contains("enabled") ||
                lowerKey.Contains("active") || lowerKey.Contains("on"))
            {
                return "boolean";
            }

            // Check for temporal indicators
            if (lowerKey.Contains("sustained") || lowerKey.Contains("threshold") ||
                rule.Name.ToLowerInvariant().Contains("temporal") || 
                rule.Name.ToLowerInvariant().Contains("over_time"))
            {
                return "timeseries";
            }

            // Check for numeric indicators
            if (lowerKey.Contains("temp") || lowerKey.Contains("pressure") ||
                lowerKey.Contains("level") || lowerKey.Contains("count") ||
                lowerKey.Contains("rate") || lowerKey.Contains("value"))
            {
                return "gauge";
            }

            // Default to gauge for numeric-like outputs
            return "gauge";
        }

        private string GenerateDisplayName(string outputId)
        {
            // Convert snake_case or camelCase to Display Case
            var result = outputId;
            
            // Insert spaces before uppercase letters (camelCase)
            result = System.Text.RegularExpressions.Regex.Replace(result, @"([a-z])([A-Z])", "$1 $2");
            
            // Replace underscores with spaces
            result = result.Replace("_", " ");
            
            // Capitalize first letter of each word
            result = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(result.ToLower());
            
            return result;
        }

        private string InferGroup(string ruleName)
        {
            var lowerRule = ruleName.ToLowerInvariant();
            
            if (lowerRule.Contains("temp") || lowerRule.Contains("heat") || lowerRule.Contains("cool"))
                return "Temperature";
            
            if (lowerRule.Contains("pressure") || lowerRule.Contains("flow"))
                return "Pressure";
                
            if (lowerRule.Contains("level") || lowerRule.Contains("tank"))
                return "Levels";
                
            if (lowerRule.Contains("alert") || lowerRule.Contains("alarm") || lowerRule.Contains("warning"))
                return "Alerts";
                
            if (lowerRule.Contains("status") || lowerRule.Contains("state"))
                return "Status";
                
            if (lowerRule.Contains("safety") || lowerRule.Contains("emergency"))
                return "Safety";
                
            return "General";
        }
    }

    /// <summary>
    /// Interface outputs catalog model
    /// </summary>
    public class InterfaceOutputsCatalog
    {
        public int Version { get; set; } = 1;
        public List<OutputMetadata> Outputs { get; set; } = new();
    }

    /// <summary>
    /// Output metadata for UI generation
    /// </summary>
    public class OutputMetadata
    {
        public string Id { get; set; } = "";
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
        public string Widget { get; set; } = "gauge";
        public string? Unit { get; set; }
        public int? Decimals { get; set; }
        public string? Group { get; set; }
        public int Order { get; set; }
        public bool Export { get; set; } = true;
        public bool DefaultVisibility { get; set; } = true;
    }

    /// <summary>
    /// Unified data dictionary model
    /// </summary>
    public class DataDictionary
    {
        public int Version { get; set; } = 1;
        public DateTime GeneratedAt { get; set; }
        public List<SensorMetadata> Sensors { get; set; } = new();
        public List<OutputMetadata> Outputs { get; set; } = new();
    }

    /// <summary>
    /// Sensor metadata for data dictionary
    /// </summary>
    public class SensorMetadata
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public string? Unit { get; set; }
        public string? Description { get; set; }
        public string Source { get; set; } = "physical";
        public bool Export { get; set; } = false;
    }

    /// <summary>
    /// Sensor catalog model for deserialization
    /// </summary>
    public class SensorCatalog
    {
        public int Version { get; set; }
        public List<SensorDefinition> Sensors { get; set; } = new();
    }

    /// <summary>
    /// Sensor definition model for deserialization
    /// </summary>
    public class SensorDefinition
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public string? Unit { get; set; }
        public string? Description { get; set; }
        public string Source { get; set; } = "physical";
        public bool Export { get; set; } = false;
    }
}