// File: Pulsar.Compiler/Generation/Generators/MetadataGenerator.cs

using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Compiler.Config;
using Pulsar.Compiler.Generation.Helpers;
using Pulsar.Compiler.Models;

namespace Pulsar.Compiler.Generation.Generators
{
    public class MetadataGenerator(ILogger? logger = null)
    {
        private readonly ILogger _logger = logger ?? NullLogger.Instance;

        // Fix the CS8625 warning by making the logger parameter nullable

        public GeneratedFileInfo GenerateMetadataFile(
            List<RuleDefinition> rules,
            Dictionary<string, string> layerMap,
            BuildConfig buildConfig
        )
        {
            var sb = new StringBuilder();
            sb.AppendLine("// Auto-generated metadata file");
            sb.AppendLine("// Generated: " + DateTime.UtcNow.ToString("O"));
            sb.AppendLine();

            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();

            sb.AppendLine($"namespace {buildConfig.Namespace}");
            sb.AppendLine("{");
            sb.AppendLine("    public static class RuleMetadata");
            sb.AppendLine("    {");

            // Rule information
            sb.AppendLine(
                "        public static readonly Dictionary<string, RuleInfo> Rules = new()"
            );
            sb.AppendLine("        {");
            foreach (var rule in rules)
            {
                sb.AppendLine($"            [\"{rule.Name}\"] = new RuleInfo");
                sb.AppendLine("            {");
                sb.AppendLine($"                Name = \"{rule.Name}\",");
                sb.AppendLine($"                Description = \"{rule.Description}\",");
                sb.AppendLine($"                Layer = {layerMap[rule.Name]},");
                sb.AppendLine($"                SourceFile = \"{rule.SourceFile}\",");
                sb.AppendLine($"                LineNumber = {rule.LineNumber},");
                var inputSensors = GenerationHelpers.GetInputSensors(rule);
                if (inputSensors.Count > 0)
                {
                    sb.AppendLine(
                        "                InputSensors = new[] { "
                            + string.Join(
                                ", ",
                                inputSensors.Select(s => $"\"{s}\"")
                            )
                            + " },"
                    );
                }
                else
                {
                    sb.AppendLine("                InputSensors = new string[] { },");
                }

                var outputSensors = GenerationHelpers.GetOutputSensors(rule);
                if (outputSensors.Count > 0)
                {
                    sb.AppendLine(
                        "                OutputSensors = new[] { "
                            + string.Join(
                                ", ",
                                outputSensors.Select(s => $"\"{s}\"")
                            )
                            + " },"
                    );
                }
                else
                {
                    sb.AppendLine("                OutputSensors = new string[] { },");
                }
                sb.AppendLine(
                    $"                HasTemporalConditions = {GenerationHelpers.HasTemporalConditions(rule).ToString().ToLower()}"
                );
                sb.AppendLine("            },");
            }
            sb.AppendLine("        };");
            sb.AppendLine();

            // RuleInfo class
            sb.AppendLine("        public class RuleInfo");
            sb.AppendLine("        {");
            sb.AppendLine("            public string Name { get; set; }");
            sb.AppendLine("            public string Description { get; set; }");
            sb.AppendLine("            public int Layer { get; set; }");
            sb.AppendLine("            public string SourceFile { get; set; }");
            sb.AppendLine("            public int LineNumber { get; set; }");
            sb.AppendLine("            public string[] InputSensors { get; set; }");
            sb.AppendLine("            public string[] OutputSensors { get; set; }");
            sb.AppendLine("            public bool HasTemporalConditions { get; set; }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return new GeneratedFileInfo
            {
                FileName = "RuleMetadata.cs",
                Content = sb.ToString(),
                Namespace = buildConfig.Namespace,
            };
        }
    }
}
