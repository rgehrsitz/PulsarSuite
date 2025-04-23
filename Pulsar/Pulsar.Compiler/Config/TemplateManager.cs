// File: Pulsar.Compiler/Config/TemplateManagerV2.cs

using Pulsar.Compiler.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Pulsar.Compiler.Config
{
    /// <summary>
    /// Template manager that centralizes template handling.
    /// </summary>
    public class TemplateManager
    {
        private readonly ILogger _logger;
        
        // Template versions and source paths
        private static readonly Dictionary<string, string> TemplateVersions = new Dictionary<string, string>
        {
            ["Program.cs"] = "1.2.0",
            ["RuntimeConfig.cs"] = "1.1.0",
            ["RuleBase.cs"] = "1.0.5",
            ["TemplateRuleCoordinator.cs"] = "1.1.1",
            ["TemplateRuleGroup.cs"] = "1.0.2",
            ["CircularBuffer.cs"] = "1.3.0",
            ["RedisService.cs"] = "1.0.0",
            ["RedisHealthCheck.cs"] = "1.0.0",
            ["RedisMetrics.cs"] = "1.0.0",
            ["ConfigurationService.cs"] = "1.0.2",
            ["RuntimeOrchestrator.cs"] = "1.0.0",
            ["ICompiledRules.cs"] = "1.0.0",
            ["IRuleCoordinator.cs"] = "1.0.0",
            ["IRuleGroup.cs"] = "1.0.1",
            ["IRedisService.cs"] = "1.0.0",
            ["Project.csproj"] = "2.0.0"
        };

        public TemplateManager(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // We don't actually set _templateBasePath here since we use a dynamic
            // lookup mechanism in GetTemplatePath to find the templates
            EnsureTemplateDirectoryExists();
        }
        
        /// <summary>
        /// Generates a solution file at the specified path.
        /// </summary>
        public void GenerateSolutionFile(string path)
        {
            var content =
                @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Generated"", ""Generated.csproj"", ""{"
                + Guid.NewGuid().ToString().ToUpper()
                + @"}""
EndProject
Global
    GlobalSection(SolutionConfigurationPlatforms) = preSolution
        Debug|Any CPU = Debug|Any CPU
        Release|Any CPU = Release|Any CPU
    EndGlobalSection
    GlobalSection(ProjectConfigurationPlatforms) = postSolution
        {"
                + Guid.NewGuid().ToString().ToUpper()
                + @"}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
        {"
                + Guid.NewGuid().ToString().ToUpper()
                + @"}.Debug|Any CPU.Build.0 = Debug|Any CPU
        {"
                + Guid.NewGuid().ToString().ToUpper()
                + @"}.Release|Any CPU.ActiveCfg = Release|Any CPU
        {"
                + Guid.NewGuid().ToString().ToUpper()
                + @"}.Release|Any CPU.Build.0 = Release|Any CPU
    EndGlobalSection
EndGlobal
";
            File.WriteAllText(path, content);
            _logger.Information("Generated solution file: {Path}", path);
        }
        
        /// <summary>
        /// Generates a project file at the specified path.
        /// </summary>
        public void GenerateProjectFile(string path, BuildConfig buildConfig)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
            sb.AppendLine("\n  <PropertyGroup>");
            sb.AppendLine($"    <OutputType>Exe</OutputType>");
            sb.AppendLine($"    <TargetFramework>{buildConfig.TargetFramework}</TargetFramework>");
            sb.AppendLine($"    <ImplicitUsings>enable</ImplicitUsings>");
            sb.AppendLine($"    <Nullable>enable</Nullable>");
            
            // Add AOT settings if optimized
            if (buildConfig.OptimizeOutput)
            {
                sb.AppendLine($"    <PublishAot>true</PublishAot>");
                sb.AppendLine($"    <IlcGenerateStackTraceData>false</IlcGenerateStackTraceData>");
                sb.AppendLine($"    <IlcDisableReflection>true</IlcDisableReflection>");
                sb.AppendLine($"    <IlcFoldIdenticalMethodBodies>true</IlcFoldIdenticalMethodBodies>");
                sb.AppendLine($"    <DebugType>none</DebugType>");
                sb.AppendLine($"    <DebugSymbols>false</DebugSymbols>");
                sb.AppendLine($"    <TrimMode>link</TrimMode>");
                sb.AppendLine($"    <InvariantGlobalization>true</InvariantGlobalization>");
                sb.AppendLine($"    <OptimizationPreference>Speed</OptimizationPreference>");
                sb.AppendLine($"    <IlcOptimizationPreference>Speed</IlcOptimizationPreference>");
                sb.AppendLine($"    <TrimmerRootAssembly Include=\"$(MSBuildProjectName)\" />");
            }
            else
            {
                sb.AppendLine($"    <PublishAot>false</PublishAot>");
                sb.AppendLine($"    <DebugType>portable</DebugType>");
                sb.AppendLine($"    <DebugSymbols>true</DebugSymbols>");
            }
            
            sb.AppendLine($"    <RootNamespace>{buildConfig.Namespace}</RootNamespace>");
            sb.AppendLine("  </PropertyGroup>");
            
            // Add package references
            sb.AppendLine("\n  <ItemGroup>");
            sb.AppendLine(
                "    <PackageReference Include=\"Microsoft.Extensions.DependencyInjection\" Version=\"9.0.0-preview.3.24163.7\" />"
            );
            sb.AppendLine(
                "    <PackageReference Include=\"Microsoft.Extensions.Logging\" Version=\"9.0.0-preview.3.24163.7\" />"
            );
            sb.AppendLine("    <PackageReference Include=\"NRedisStack\" Version=\"0.13.1\" />");
            sb.AppendLine("    <PackageReference Include=\"Polly\" Version=\"8.3.0\" />");
            sb.AppendLine("    <PackageReference Include=\"prometheus-net\" Version=\"8.2.1\" />");
            sb.AppendLine("    <PackageReference Include=\"Serilog\" Version=\"4.2.0\" />");
            sb.AppendLine(
                "    <PackageReference Include=\"Serilog.Enrichers.Thread\" Version=\"3.1.0\" />"
            );
            sb.AppendLine(
                "    <PackageReference Include=\"Serilog.Formatting.Compact\" Version=\"2.0.0\" />"
            );
            sb.AppendLine(
                "    <PackageReference Include=\"Serilog.Sinks.Console\" Version=\"5.0.1\" />"
            );
            sb.AppendLine(
                "    <PackageReference Include=\"Serilog.Sinks.File\" Version=\"5.0.0\" />"
            );
            sb.AppendLine(
                "    <PackageReference Include=\"StackExchange.Redis\" Version=\"2.8.16\" />"
            );
            sb.AppendLine("    <PackageReference Include=\"YamlDotNet\" Version=\"16.3.0\" />");
            sb.AppendLine("  </ItemGroup>");
            sb.AppendLine("</Project>");

            File.WriteAllText(path, sb.ToString());
            _logger.Information("Generated project file: {Path}", path);
        }

        /// <summary>
        /// Get a template file path based on its logical name.
        /// </summary>
        public string GetTemplatePath(string templateName)
        {
            var path = ResolveTemplatePath(templateName);
            
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Template {templateName} not found at path: {path}");
            }
            
            return path;
        }

        /// <summary>
        /// Read a template by name.
        /// </summary>
        public async Task<string> ReadTemplateAsync(string templateName)
        {
            var path = GetTemplatePath(templateName);
            return await File.ReadAllTextAsync(path);
        }

        /// <summary>
        /// Generate project files for a buildable output.
        /// </summary>
        public void GenerateProjectFiles(string outputPath, BuildConfig config)
        {
            _logger.Information("Generating project files in {Path}", outputPath);
            
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }
            
            // Special handling for solution and project files
            GenerateSolutionFile(Path.Combine(outputPath, "Generated.sln"));
            GenerateProjectFile(Path.Combine(outputPath, "Generated.csproj"), config);
            
            // Copy Program.cs template
            CopyTemplateFile("Program.cs", Path.Combine(outputPath, "Program.cs"));
            
            // Generate interface files in Interfaces directory
            Directory.CreateDirectory(Path.Combine(outputPath, "Interfaces"));
            CopyTemplateFile("Interfaces/ICompiledRules.cs", Path.Combine(outputPath, "Interfaces/ICompiledRules.cs"));
            CopyTemplateFile("Interfaces/IRuleCoordinator.cs", Path.Combine(outputPath, "Interfaces/IRuleCoordinator.cs"));
            CopyTemplateFile("Interfaces/IRuleGroup.cs", Path.Combine(outputPath, "Interfaces/IRuleGroup.cs"));
            CopyTemplateFile("Interfaces/IRedisService.cs", Path.Combine(outputPath, "Interfaces/IRedisService.cs"));
            
            // Generate Services directory
            Directory.CreateDirectory(Path.Combine(outputPath, "Services"));
            
            // Redis services
            CopyTemplateFile("Runtime/Services/RedisService.cs", Path.Combine(outputPath, "Services/RedisService.cs"));
            CopyTemplateFile("Runtime/Services/RedisHealthCheck.cs", Path.Combine(outputPath, "Services/RedisHealthCheck.cs"));
            CopyTemplateFile("Runtime/Services/RedisMetrics.cs", Path.Combine(outputPath, "Services/RedisMetrics.cs"));
            CopyTemplateFile("Runtime/Services/MetricsService.cs", Path.Combine(outputPath, "Services/MetricsService.cs"));
            
            // Configuration
            CopyTemplateFile("Runtime/Configuration/ConfigurationService.cs", Path.Combine(outputPath, "Configuration/ConfigurationService.cs"));
            
            // Buffer implementation
            Directory.CreateDirectory(Path.Combine(outputPath, "Buffers"));
            CopyTemplateFile("Runtime/Buffers/CircularBuffer.cs", Path.Combine(outputPath, "Buffers/CircularBuffer.cs"));
            CopyTemplateFile("Runtime/Buffers/IDateTimeProvider.cs", Path.Combine(outputPath, "Buffers/IDateTimeProvider.cs"));
            CopyTemplateFile("Runtime/Buffers/SystemDateTimeProvider.cs", Path.Combine(outputPath, "Buffers/SystemDateTimeProvider.cs"));
            
            // Models
            Directory.CreateDirectory(Path.Combine(outputPath, "Models"));
            CopyTemplateFile("Runtime/Models/RuntimeConfig.cs", Path.Combine(outputPath, "Models/RuntimeConfig.cs"));
            CopyTemplateFile("Runtime/Models/RedisConfiguration.cs", Path.Combine(outputPath, "Models/RedisConfiguration.cs"));
            
            // Rules base implementation
            Directory.CreateDirectory(Path.Combine(outputPath, "Rules"));
            CopyTemplateFile("Runtime/Rules/RuleBase.cs", Path.Combine(outputPath, "Rules/RuleBase.cs"));
            
            // Template rule classes
            CopyTemplateFile("Runtime/TemplateRuleCoordinator.cs", Path.Combine(outputPath, "TemplateRuleCoordinator.cs"));
            CopyTemplateFile("Runtime/TemplateRuleGroup.cs", Path.Combine(outputPath, "TemplateRuleGroup.cs"));
            CopyTemplateFile("Runtime/RuntimeOrchestrator.cs", Path.Combine(outputPath, "RuntimeOrchestrator.cs"));
        }

        /// <summary>
        /// Copy a template file to the specified path, applying any required transformations.
        /// </summary>
        private void CopyTemplateFile(string templatePath, string outputPath)
        {
            try
            {
                // Ensure the output directory exists
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Find the source template
                string sourceTemplate;
                try 
                {
                    sourceTemplate = GetTemplatePath(templatePath);
                }
                catch (FileNotFoundException)
                {
                    _logger.Warning("Template file not found: {Path}", templatePath);
                    return;
                }
                
                // Read the content
                string content = File.ReadAllText(sourceTemplate);
                
                // Add version header if not present
                if (Path.GetFileName(templatePath) is string fileName && 
                    TemplateVersions.TryGetValue(fileName, out var version))
                {
                    if (!content.Contains("// Version:"))
                    {
                        // Add version if there's already a file comment
                        if (content.StartsWith("// "))
                        {
                            var lines = content.Split('\n');
                            lines[0] = lines[0] + "\n// Version: " + version;
                            content = string.Join('\n', lines);
                        }
                        else
                        {
                            content = $"// File: {fileName}\n// Version: {version}\n\n{content}";
                        }
                    }
                }
                
                File.WriteAllText(outputPath, content);
                _logger.Debug("Copied template {Template} to {Output}", templatePath, outputPath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to copy template file {Template} to {Output}", templatePath, outputPath);
            }
        }

        /// <summary>
        /// Maps logical template names to their file paths.
        /// </summary>
        private string ResolveTemplatePath(string templateName)
        {
            // Map logical template names to file paths
            var templatePathMap = new Dictionary<string, string>
            {
                ["Program.cs"] = "Program.cs",
                ["RuntimeConfig.cs"] = "RuntimeConfig.cs",
                ["RuleCoordinator.cs"] = "Runtime/TemplateRuleCoordinator.cs",
                ["RuleGroup.cs"] = "Runtime/TemplateRuleGroup.cs",
                ["CircularBuffer.cs"] = "Runtime/Buffers/CircularBuffer.cs",
                ["RedisService.cs"] = "Runtime/Services/RedisService.cs",
                ["RedisHealthCheck.cs"] = "Runtime/Services/RedisHealthCheck.cs",
                ["RedisMetrics.cs"] = "Runtime/Services/RedisMetrics.cs",
                ["ICompiledRules.cs"] = "Interfaces/ICompiledRules.cs",
                ["IRuleCoordinator.cs"] = "Interfaces/IRuleCoordinator.cs",
                ["IRuleGroup.cs"] = "Interfaces/IRuleGroup.cs",
                ["IRedisService.cs"] = "Interfaces/IRedisService.cs",
                ["RuleBase.cs"] = "Runtime/Rules/RuleBase.cs",
                ["RuntimeOrchestrator.cs"] = "Runtime/RuntimeOrchestrator.cs",
                ["Project.csproj"] = "Project/Runtime.csproj",
                ["Solution.sln"] = "Project/Generated.sln"
            };
            
            // Resolve the template path based on the logical name
            string templateRelativePath = templateName;
            if (templatePathMap.TryGetValue(templateName, out var mappedPath))
            {
                templateRelativePath = mappedPath;
            }
            
            // Try multiple possible locations for the template files
            var possiblePaths = new[]
            {
                // Direct path from working directory
                Path.Combine("Pulsar.Compiler", "Config", "Templates", templateRelativePath),
                // Path relative to assembly location
                Path.Combine(
                    Path.GetDirectoryName(typeof(TemplateManager).Assembly.Location) ?? "",
                    "Config",
                    "Templates",
                    templateRelativePath
                ),
                // Path from assembly base directory
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Config",
                    "Templates",
                    templateRelativePath
                ),
                // Path relative to project root (go up from bin directory)
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "..", "..", "..",
                    "Pulsar.Compiler",
                    "Config",
                    "Templates",
                    templateRelativePath
                ),
                // Absolute path based on solution directory structure
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "..", "..", "..", "..",
                    "Pulsar.Compiler",
                    "Config",
                    "Templates",
                    templateRelativePath
                ),
            };

            foreach (var path in possiblePaths)
            {
                var normalizedPath = Path.GetFullPath(path);
                if (File.Exists(normalizedPath))
                {
                    _logger.Debug("Found template at: {Path}", normalizedPath);
                    return normalizedPath;
                }
            }
            
            throw new FileNotFoundException(
                $"Template file not found: {templateName} at {templateRelativePath}. " +
                $"Searched in: {string.Join(", ", possiblePaths.Select(p => Path.GetFullPath(p)))}"
            );
        }

        private void EnsureTemplateDirectoryExists()
        {
            // Verify key template files exist by trying to resolve them
            var requiredTemplates = new[]
            {
                "Program.cs",
                "Runtime/TemplateRuleCoordinator.cs",
                "Interfaces/ICompiledRules.cs"
            };
            
            var missingTemplates = new List<string>();
            
            foreach (var template in requiredTemplates)
            {
                try 
                {
                    GetTemplatePath(template);
                }
                catch (FileNotFoundException)
                {
                    missingTemplates.Add(template);
                }
            }
            
            if (missingTemplates.Count > 0)
            {
                _logger.Warning("Some required template files could not be found: {MissingTemplates}", 
                    string.Join(", ", missingTemplates));
            }
        }
        
        /// <summary>
        /// Copies all necessary template files to the output directory.
        /// </summary>
        /// <param name="outputPath">The directory to copy templates to</param>
        public void CopyTemplates(string outputPath)
        {
            _logger.Information("Copying templates to output directory: {OutputPath}", outputPath);
            
            // Generate all project files which includes copying all necessary templates
            var defaultConfig = new BuildConfig
            {
                TargetFramework = "net8.0",
                OptimizeOutput = false,
                Namespace = "Pulsar.Runtime",
                ProjectName = "Generated",
                AssemblyName = "Generated",
                OutputPath = outputPath,
                Target = "linux-x64",  // Default target platform
                RulesPath = Path.Combine(outputPath, "rules")  // Provide a valid path even if not used
            };
            
            GenerateProjectFiles(outputPath, defaultConfig);
        }
    }
}