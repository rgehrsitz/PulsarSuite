// File: Pulsar.Compiler/Config/BeaconTemplateManager.cs
// NOTE: This implementation includes AOT compatibility fixes.

using System.Text;
using Serilog;

namespace Pulsar.Compiler.Config
{
    /// <summary>
    /// Generates and manages templates for the Beacon AOT-compatible solution structure
    /// </summary>
    public class BeaconTemplateManager
    {
        private readonly ILogger _logger;
        private readonly TemplateManager _templateManager;
        
        public BeaconTemplateManager(ILogger? logger = null)
        {
            _logger = logger ?? LoggingConfig.GetLogger().ForContext<BeaconTemplateManager>();
            _templateManager = new TemplateManager(_logger);
        }

        /// <summary>
        /// Creates the complete Beacon solution structure
        /// </summary>
        /// <param name="outputPath">Base path for the Beacon solution</param>
        /// <param name="buildConfig">Build configuration</param>
        public void CreateBeaconSolution(string outputPath, BuildConfig buildConfig)
        {
            _logger.Information("Creating Beacon solution at {Path}", outputPath);

            // Create the solution directory
            string solutionDir = outputPath;
            Directory.CreateDirectory(solutionDir);

            // Create the solution file
            GenerateBeaconSolutionFile(solutionDir, buildConfig);

            // Create the runtime project
            string runtimeDir = Path.Combine(solutionDir, "Beacon.Runtime");
            Directory.CreateDirectory(runtimeDir);

            // Create the runtime project structure
            CreateRuntimeProjectStructure(runtimeDir, buildConfig);

            // Create the test project if enabled
            bool generateTestProject = true; // Hard-coded for now, normally would use buildConfig.GenerateTestProject
            if (generateTestProject)
            {
                string testsDir = Path.Combine(solutionDir, "Beacon.Tests");
                Directory.CreateDirectory(testsDir);

                // Create the test project structure
                CreateTestProjectStructure(testsDir, buildConfig);
            }

            _logger.Information("Beacon solution structure created successfully");
        }

        /// <summary>
        /// Creates the runtime project structure
        /// </summary>
        private void CreateRuntimeProjectStructure(string runtimeDir, BuildConfig buildConfig)
        {
            _logger.Information("Creating runtime project structure at {Path}", runtimeDir);

            // Create project directories
            Directory.CreateDirectory(Path.Combine(runtimeDir, "Generated"));
            Directory.CreateDirectory(Path.Combine(runtimeDir, "Services"));
            Directory.CreateDirectory(Path.Combine(runtimeDir, "Buffers"));
            Directory.CreateDirectory(Path.Combine(runtimeDir, "Interfaces"));
            Directory.CreateDirectory(Path.Combine(runtimeDir, "Models"));
            Directory.CreateDirectory(Path.Combine(runtimeDir, "Rules"));

            LogDirectoryContents(runtimeDir, "After directory creation");

            // Generate project file
            GenerateRuntimeProjectFile(runtimeDir, buildConfig);
            LogDirectoryContents(runtimeDir, "After project file generation");

            // Copy necessary templates
            CopyRuntimeTemplateFiles(runtimeDir, buildConfig);
            LogDirectoryContents(runtimeDir, "After copying templates");

            // Generate Program.cs with AOT compatibility
            GenerateProgramCs(runtimeDir, buildConfig);
            LogDirectoryContents(runtimeDir, "After generating Program.cs");
        }

        // Helper method to log contents of a directory
        private void LogDirectoryContents(string dir, string label)
        {
            string logPath = Path.Combine(AppContext.BaseDirectory, "diagnostic.log");
            Console.WriteLine($"[DIAGNOSTIC] LogDirectoryContents CALLED for label: {label}, dir: {dir}");
            Console.WriteLine($"[DIAGNOSTIC] Writing diagnostic log to: {logPath}");
            try
            {
                string header = $"[DIAGNOSTIC] {label}: Listing contents of {dir}";
                _logger.Information(header);
                Console.WriteLine(header);
                File.AppendAllText(logPath, header + Environment.NewLine);
                foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    string fileMsg = $"[DIAGNOSTIC] {label}: File: {file}";
                    _logger.Information(fileMsg);
                    Console.WriteLine(fileMsg);
                    File.AppendAllText(logPath, fileMsg + Environment.NewLine);
                }
                foreach (var subdir in Directory.GetDirectories(dir, "*", SearchOption.AllDirectories))
                {
                    string dirMsg = $"[DIAGNOSTIC] {label}: Directory: {subdir}";
                    _logger.Information(dirMsg);
                    Console.WriteLine(dirMsg);
                    File.AppendAllText(logPath, dirMsg + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                string errMsg = $"[DIAGNOSTIC] {label}: Error listing contents of {dir}: {ex}";
                _logger.Error(ex, errMsg);
                Console.WriteLine(errMsg);
                File.AppendAllText(logPath, errMsg + Environment.NewLine);
            }
        }

        /// <summary>
        /// Creates the test project structure
        /// </summary>
        private void CreateTestProjectStructure(string testsDir, BuildConfig buildConfig)
        {
            _logger.Information("Creating test project structure at {Path}", testsDir);

            // Create project directories
            Directory.CreateDirectory(Path.Combine(testsDir, "Generated"));
            Directory.CreateDirectory(Path.Combine(testsDir, "Fixtures"));

            // Generate test project file
            GenerateTestProjectFile(testsDir, buildConfig);

            // Generate basic test fixtures
            GenerateTestFixtures(testsDir, buildConfig);
        }

        /// <summary>
        /// Generates the Beacon solution file
        /// </summary>
        private void GenerateBeaconSolutionFile(string solutionDir, BuildConfig buildConfig)
        {
            string solutionPath = Path.Combine(solutionDir, "Beacon.sln");

            var sb = new StringBuilder();
            sb.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
            sb.AppendLine("# Visual Studio Version 17");
            sb.AppendLine("VisualStudioVersion = 17.0.31903.59");
            sb.AppendLine("MinimumVisualStudioVersion = 10.0.40219.1");

            // Add Runtime project
            string runtimeGuid = Guid.NewGuid().ToString("B").ToUpper();
            // Use the standard C# project type GUID
            string csharpProjectTypeGuid = "{9A19103F-16F7-4668-BE54-9A1E7A4F7556}";
            sb.AppendLine(
                $"Project(\"{csharpProjectTypeGuid}\") = \"Beacon.Runtime\", \"Beacon.Runtime\\Beacon.Runtime.csproj\", \"{runtimeGuid}\""
            );
            sb.AppendLine("EndProject");

            // Add Tests project if enabled
            string testsGuid = "";
            bool generateTestProject = true; // Hard-coded for now, normally would use buildConfig.GenerateTestProject
            if (generateTestProject)
            {
                testsGuid = Guid.NewGuid().ToString("B").ToUpper();
                sb.AppendLine(
                    $"Project(\"{csharpProjectTypeGuid}\") = \"Beacon.Tests\", \"Beacon.Tests\\Beacon.Tests.csproj\", \"{testsGuid}\""
                );
                sb.AppendLine("EndProject");
            }

            // Add solution configurations
            sb.AppendLine("Global");
            sb.AppendLine("    GlobalSection(SolutionConfigurationPlatforms) = preSolution");
            sb.AppendLine("        Debug|Any CPU = Debug|Any CPU");
            sb.AppendLine("        Release|Any CPU = Release|Any CPU");
            sb.AppendLine("    EndGlobalSection");

            // Add project configurations
            sb.AppendLine("    GlobalSection(ProjectConfigurationPlatforms) = postSolution");
            sb.AppendLine($"        {runtimeGuid}.Debug|Any CPU.ActiveCfg = Debug|Any CPU");
            sb.AppendLine($"        {runtimeGuid}.Debug|Any CPU.Build.0 = Debug|Any CPU");
            sb.AppendLine($"        {runtimeGuid}.Release|Any CPU.ActiveCfg = Release|Any CPU");
            sb.AppendLine($"        {runtimeGuid}.Release|Any CPU.Build.0 = Release|Any CPU");

            if (generateTestProject)
            {
                sb.AppendLine($"        {testsGuid}.Debug|Any CPU.ActiveCfg = Debug|Any CPU");
                sb.AppendLine($"        {testsGuid}.Debug|Any CPU.Build.0 = Debug|Any CPU");
                sb.AppendLine($"        {testsGuid}.Release|Any CPU.ActiveCfg = Release|Any CPU");
                sb.AppendLine($"        {testsGuid}.Release|Any CPU.Build.0 = Release|Any CPU");
            }

            sb.AppendLine("    EndGlobalSection");
            sb.AppendLine("EndGlobal");

            File.WriteAllText(solutionPath, sb.ToString());
            _logger.Information("Generated solution file: {Path}", solutionPath);
        }

        /// <summary>
        /// Generates the runtime project file
        /// </summary>
        private void GenerateRuntimeProjectFile(string runtimeDir, BuildConfig buildConfig)
        {
            string projectPath = Path.Combine(runtimeDir, "Beacon.Runtime.csproj");

            var sb = new StringBuilder();
            sb.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
            sb.AppendLine("  <PropertyGroup>");
            sb.AppendLine($"    <TargetFramework>{buildConfig.TargetFramework}</TargetFramework>");
            sb.AppendLine("    <ImplicitUsings>enable</ImplicitUsings>");
            sb.AppendLine("    <Nullable>enable</Nullable>");
            sb.AppendLine("    <OutputType>Exe</OutputType>");

            // Add AOT compatibility properties
            sb.AppendLine("    <EnableTrimAnalyzer>true</EnableTrimAnalyzer>");
            sb.AppendLine("    <IsTrimmable>true</IsTrimmable>");
            sb.AppendLine("    <TrimmerSingleWarn>false</TrimmerSingleWarn>");

            if (buildConfig.StandaloneExecutable)
            {
                sb.AppendLine("    <PublishSingleFile>true</PublishSingleFile>");
                sb.AppendLine("    <SelfContained>true</SelfContained>");
                sb.AppendLine($"    <RuntimeIdentifier>{buildConfig.Target}</RuntimeIdentifier>");
            }

            if (buildConfig.OptimizeOutput)
            {
                sb.AppendLine("    <PublishReadyToRun>true</PublishReadyToRun>");
                sb.AppendLine("    <PublishTrimmed>true</PublishTrimmed>");
                sb.AppendLine("    <TrimMode>link</TrimMode>");
                sb.AppendLine("    <TrimmerRemoveSymbols>true</TrimmerRemoveSymbols>");
                sb.AppendLine("    <DebuggerSupport>false</DebuggerSupport>");
                sb.AppendLine(
                    "    <EnableUnsafeBinaryFormatterSerialization>false</EnableUnsafeBinaryFormatterSerialization>"
                );
                sb.AppendLine("    <EnableUnsafeUTF7Encoding>false</EnableUnsafeUTF7Encoding>");
                sb.AppendLine("    <InvariantGlobalization>true</InvariantGlobalization>");
                sb.AppendLine(
                    "    <HttpActivityPropagationSupport>false</HttpActivityPropagationSupport>"
                );
            }

            sb.AppendLine("  </PropertyGroup>");
            sb.AppendLine();

            // Add trimming configuration
            sb.AppendLine("  <ItemGroup>");
            sb.AppendLine("    <TrimmerRootDescriptor Include=\"trimming.xml\" />");
            sb.AppendLine("  </ItemGroup>");
            sb.AppendLine();

            // Add package references
            sb.AppendLine("  <ItemGroup>");
            sb.AppendLine(
                "    <PackageReference Include=\"Microsoft.Extensions.Logging\" Version=\"8.0.0\" />"
            );
            sb.AppendLine(
                "    <PackageReference Include=\"Microsoft.Extensions.Logging.Abstractions\" Version=\"8.0.0\" />"
            );
            sb.AppendLine(
                "    <PackageReference Include=\"Microsoft.Extensions.Logging.Console\" Version=\"8.0.0\" />"
            );
            sb.AppendLine("    <PackageReference Include=\"NRedisStack\" Version=\"0.13.1\" />");
            sb.AppendLine("    <PackageReference Include=\"Polly\" Version=\"8.3.0\" />");
            sb.AppendLine("    <PackageReference Include=\"prometheus-net\" Version=\"8.2.1\" />");
            sb.AppendLine("    <PackageReference Include=\"prometheus-net.AspNetCore\" Version=\"8.2.1\" />");
            sb.AppendLine("    <PackageReference Include=\"Serilog\" Version=\"4.2.0\" />");
            sb.AppendLine(
                "    <PackageReference Include=\"Serilog.Extensions.Logging\" Version=\"8.0.0\" />"
            );
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
            sb.AppendLine(
                "    <PackageReference Include=\"System.Diagnostics.DiagnosticSource\" Version=\"8.0.0\" />"
            );
            sb.AppendLine("    <PackageReference Include=\"YamlDotNet\" Version=\"16.3.0\" />");
            sb.AppendLine("  </ItemGroup>");

            sb.AppendLine("</Project>");

            File.WriteAllText(projectPath, sb.ToString());
            _logger.Information("Generated runtime project file: {Path}", projectPath);
        }

        /// <summary>
        /// Generates the test project file
        /// </summary>
        private void GenerateTestProjectFile(string testsDir, BuildConfig buildConfig)
        {
            string projectPath = Path.Combine(testsDir, "Beacon.Tests.csproj");

            var sb = new StringBuilder();
            sb.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
            sb.AppendLine("  <PropertyGroup>");
            sb.AppendLine($"    <TargetFramework>{buildConfig.TargetFramework}</TargetFramework>");
            sb.AppendLine("    <ImplicitUsings>enable</ImplicitUsings>");
            sb.AppendLine("    <Nullable>enable</Nullable>");
            sb.AppendLine("    <IsPackable>false</IsPackable>");
            sb.AppendLine("    <IsTestProject>true</IsTestProject>");
            sb.AppendLine("  </PropertyGroup>");
            sb.AppendLine();

            // Add package references
            sb.AppendLine("  <ItemGroup>");
            sb.AppendLine(
                "    <PackageReference Include=\"Microsoft.NET.Test.Sdk\" Version=\"17.10.0\" />"
            );
            sb.AppendLine("    <PackageReference Include=\"xunit\" Version=\"2.7.0\" />");
            sb.AppendLine(
                "    <PackageReference Include=\"xunit.runner.visualstudio\" Version=\"2.5.7\">"
            );
            sb.AppendLine(
                "      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>"
            );
            sb.AppendLine("      <PrivateAssets>all</PrivateAssets>");
            sb.AppendLine("    </PackageReference>");
            sb.AppendLine(
                "    <PackageReference Include=\"coverlet.collector\" Version=\"6.0.2\">"
            );
            sb.AppendLine(
                "      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>"
            );
            sb.AppendLine("      <PrivateAssets>all</PrivateAssets>");
            sb.AppendLine("    </PackageReference>");
            sb.AppendLine(
                "    <PackageReference Include=\"Testcontainers.Redis\" Version=\"3.8.0\" />"
            );
            sb.AppendLine("  </ItemGroup>");
            sb.AppendLine();

            // Add project reference to runtime project
            sb.AppendLine("  <ItemGroup>");
            sb.AppendLine(
                "    <ProjectReference Include=\"..\\Beacon.Runtime\\Beacon.Runtime.csproj\" />"
            );
            sb.AppendLine("  </ItemGroup>");

            sb.AppendLine("</Project>");

            File.WriteAllText(projectPath, sb.ToString());
            _logger.Information("Generated test project file: {Path}", projectPath);
        }

        /// <summary>
        /// Copies runtime template files from the original TemplateManager
        /// </summary>
        private void CopyRuntimeTemplateFiles(string runtimeDir, BuildConfig buildConfig)
        {
            _logger.Information("[DIAGNOSTIC] Entered CopyRuntimeTemplateFiles");
            _logger.Information("[CopyRuntimeTemplateFiles] Starting copy to {RuntimeDir}", runtimeDir);
            // Clean and recreate the project directories to ensure a fresh state
            CleanAndRecreateDirectory(Path.Combine(runtimeDir, "Generated"));
            CleanAndRecreateDirectory(Path.Combine(runtimeDir, "Services"));
            CleanAndRecreateDirectory(Path.Combine(runtimeDir, "Buffers"));
            CleanAndRecreateDirectory(Path.Combine(runtimeDir, "Interfaces"));
            CleanAndRecreateDirectory(Path.Combine(runtimeDir, "Models"));
            CleanAndRecreateDirectory(Path.Combine(runtimeDir, "Rules"));

            _logger.Information("[CopyRuntimeTemplateFiles] Copying template files to runtime directory: {RuntimeDir}", runtimeDir);

            // Copy interface templates first - IMPORTANT for resolving reference issues
            CopyInterfaceFiles(runtimeDir);

            // Copy model templates - Critical for proper serialization and AOT compatibility
            CopyModelFiles(runtimeDir);

            // Copy service files, but ensure no duplicates
            CopyServiceFiles(runtimeDir);

            // Copy buffer files
            CopyBufferFiles(runtimeDir);

            // Copy rule templates
            CopyTemplateFile(
                "Runtime/Rules/RuleBase.cs",
                Path.Combine(runtimeDir, "Rules", "RuleBase.cs")
            );

            // Copy RuntimeOrchestrator and other core templates
            CopyTemplateFile(
                "Runtime/RuntimeOrchestrator.cs",
                Path.Combine(runtimeDir, "RuntimeOrchestrator.cs")
            );
            CopyTemplateFile(
                "Runtime/TemplateRuleCoordinator.cs",
                Path.Combine(runtimeDir, "RuleCoordinator.cs")
            );

            // Copy AOT compatibility file
            CopyTemplateFile("trimming.xml", Path.Combine(runtimeDir, "trimming.xml"));

            // --- NEW: Copy compiled rule files into Generated directory ---
            var generatedDir = Path.Combine(runtimeDir, "Generated");
            try
            {
                _logger.Information("========== [DIAGNOSTIC] BEFORE COPY ==========");
                if (!string.IsNullOrWhiteSpace(buildConfig.CompiledRulesDir) && Directory.Exists(buildConfig.CompiledRulesDir))
                {
                    var preFiles = Directory.GetFiles(buildConfig.CompiledRulesDir, "*.cs");
                    foreach (var f in preFiles)
                        _logger.Information($"[DIAGNOSTIC] SourceDir contains: {f}");
                    var preGenFiles = Directory.Exists(generatedDir) ? Directory.GetFiles(generatedDir, "*.cs") : new string[0];
                    foreach (var f in preGenFiles)
                        _logger.Information($"[DIAGNOSTIC] GeneratedDir before copy contains: {f}");

                    // Copy ALL .cs files from CompiledRulesDir into Generated, not just RuleGroup*.cs and RuleCoordinator.cs
                    var compiledRuleFiles = Directory.GetFiles(buildConfig.CompiledRulesDir, "*.cs").ToList();
                    if (compiledRuleFiles.Count == 0)
                    {
                        _logger.Warning($"[DIAGNOSTIC] No .cs files found in CompiledRulesDir: {buildConfig.CompiledRulesDir}");
                    }
                    foreach (var file in compiledRuleFiles)
                    {
                        var destFile = Path.Combine(generatedDir, Path.GetFileName(file));
                        try
                        {
                            File.Copy(file, destFile, true);
                            _logger.Information($"[DIAGNOSTIC] Copied compiled rule file: {file} -> {destFile}");
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex, $"[DIAGNOSTIC] Error copying file: {file} -> {destFile}");
                        }
                    }
                    // List all files in Generated after copy
                    var allGenFiles = Directory.Exists(generatedDir) ? Directory.GetFiles(generatedDir, "*.cs") : new string[0];
                    _logger.Information($"[DIAGNOSTIC] GeneratedDir now contains {allGenFiles.Length} files:");
                    foreach (var f in allGenFiles)
                        _logger.Information($"[DIAGNOSTIC]   {f}");
                    var postGenFiles = Directory.Exists(generatedDir) ? Directory.GetFiles(generatedDir, "*.cs") : new string[0];
                    _logger.Information("========== [DIAGNOSTIC] AFTER COPY ==========");
                    foreach (var f in postGenFiles)
                        _logger.Information($"[DIAGNOSTIC] GeneratedDir after copy contains: {f}");
                }
                else
                {
                    _logger.Warning($"[DIAGNOSTIC] CompiledRulesDir not set or does not exist: {buildConfig.CompiledRulesDir}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[DIAGNOSTIC] Exception during compiled rule copy diagnostics");
            }
            // --- END NEW ---

            _logger.Information("Finished copying template files to runtime directory");
        }

        /// <summary>
        /// Copy interface files
        /// </summary>
        private void CopyInterfaceFiles(string runtimeDir)
        {
            var interfacesDir = Path.Combine(runtimeDir, "Interfaces");
            CopyTemplateFile(
                "Interfaces/ICompiledRules.cs",
                Path.Combine(interfacesDir, "ICompiledRules.cs")
            );
            CopyTemplateFile(
                "Interfaces/IRuleCoordinator.cs",
                Path.Combine(interfacesDir, "IRuleCoordinator.cs")
            );
            CopyTemplateFile(
                "Interfaces/IRuleGroup.cs",
                Path.Combine(interfacesDir, "IRuleGroup.cs")
            );
            CopyTemplateFile(
                "Interfaces/IRedisService.cs",
                Path.Combine(interfacesDir, "IRedisService.cs")
            );
        }

        /// <summary>
        /// Copy model files
        /// </summary>
        private void CopyModelFiles(string runtimeDir)
        {
            var modelsDir = Path.Combine(runtimeDir, "Models");
            _logger.Information("Copying model templates to {Path}", modelsDir);
            CopyTemplateFile(
                "Runtime/Models/RedisConfiguration.cs",
                Path.Combine(modelsDir, "RedisConfiguration.cs")
            );
            CopyTemplateFile(
                "Runtime/Models/RuntimeConfig.cs",
                Path.Combine(modelsDir, "RuntimeConfig.cs")
            );

            // Fix model file references to ensure they're using the Models namespace
            UpdateModelFiles(modelsDir);
        }

        /// <summary>
        /// Update model files to fix namespace references
        /// </summary>
        private void UpdateModelFiles(string modelsDir)
        {
            var redisConfigPath = Path.Combine(modelsDir, "RedisConfiguration.cs");
            if (File.Exists(redisConfigPath))
            {
                var content = File.ReadAllText(redisConfigPath);

                // Replace service reference with model reference to avoid circular dependency
                content = content.Replace(
                    "using Beacon.Runtime.Services;",
                    "using Beacon.Runtime.Services; // Models should be independent of Services"
                );

                File.WriteAllText(redisConfigPath, content);
                _logger.Information("Updated namespace references in RedisConfiguration.cs");
            }
        }

        /// <summary>
        /// Copy service files, ensuring no duplicates
        /// </summary>
        private void CopyServiceFiles(string runtimeDir)
        {
            _logger.Information("[CopyServiceFiles] Copying service files to {RuntimeDir}", runtimeDir);
            var servicesDir = Path.Combine(runtimeDir, "Services");

            // Copy individual service files to avoid duplicates
            _logger.Information("[CopyServiceFiles] Preparing to copy RedisService.cs from template to {Dest}", Path.Combine(servicesDir, "RedisService.cs"));
            
            // Special handling for RedisService.cs to ensure it's properly copied
            string redisServiceTemplate = "Runtime/Services/RedisService.cs";
            string redisServiceDest = Path.Combine(servicesDir, "RedisService.cs");
            
            // This prevents the file from getting corrupted during the copy process
            try {
                string templatePath = GetTemplateFilePath(redisServiceTemplate);
                if (File.Exists(templatePath)) {
                    string content = File.ReadAllText(templatePath);
                    Directory.CreateDirectory(servicesDir);
                    File.WriteAllText(redisServiceDest, content);
                    _logger.Information("[CopyServiceFiles] Successfully created RedisService.cs using direct text copy approach");
                } else {
                    _logger.Error("[CopyServiceFiles] Could not find RedisService.cs template at {TemplatePath}", templatePath);
                }
            } catch (Exception ex) {
                _logger.Error(ex, "[CopyServiceFiles] Error during direct copy of RedisService.cs");
                // Fall back to regular file copy as a last resort
                CopyTemplateFile(redisServiceTemplate, redisServiceDest);
            }
            
            // Copy other service files normally
            CopyTemplateFile(
                "Runtime/Services/RedisMetrics.cs",
                Path.Combine(servicesDir, "RedisMetrics.cs")
            );
            CopyTemplateFile(
                "Runtime/Services/RedisHealthCheck.cs",
                Path.Combine(servicesDir, "RedisHealthCheck.cs")
            );
            CopyTemplateFile(
                "Runtime/Services/RedisLoggingConfiguration.cs",
                Path.Combine(servicesDir, "RedisLoggingConfiguration.cs")
            );
            CopyTemplateFile(
                "Runtime/Services/MetricsService.cs",
                Path.Combine(servicesDir, "MetricsService.cs")
            );
            CopyTemplateFile(
                "Runtime/Services/LoggingService.cs",
                Path.Combine(servicesDir, "LoggingService.cs")
            );

            // Fix service file references to use Models namespace
            UpdateServiceFiles(servicesDir);
        }

        /// <summary>
        /// Update service files to fix namespace references
        /// </summary>
        private void UpdateServiceFiles(string servicesDir)
        {
            _logger.Debug("[UpdateServiceFiles] Updating service files in directory: {ServicesDir}", servicesDir);
            foreach (var file in Directory.GetFiles(servicesDir))
            {
                if (file != null)
                {
                    var content = File.ReadAllText(file);
                    var fileName = Path.GetFileName(file);
                    bool modified = false;
                    _logger.Debug("[UpdateServiceFiles] Checking file: {File}", fileName);

                    // Add Models namespace reference if missing
                    if (!content.Contains("using Beacon.Runtime.Models;"))
                    {
                        content = content.Replace(
                            "using Beacon.Runtime.Interfaces;",
                            "using Beacon.Runtime.Interfaces;\r\nusing Beacon.Runtime.Models;"
                        );
                        modified = true;
                        _logger.Debug("[UpdateServiceFiles] Added Models namespace to {File}", fileName);
                    }

                    // Fix RedisConfiguration references
                    if (
                        fileName == "RedisLoggingConfiguration.cs"
                        && content.Contains("RedisConfiguration config")
                    )
                    {
                        // This is a special case - RedisLoggingConfiguration tries to use RedisConfiguration without Models namespace
                        if (!content.Contains("using Beacon.Runtime.Models;"))
                        {
                            // Add Models namespace if not already added
                            content = content.Replace(
                                "using Serilog.Formatting.Compact;",
                                "using Serilog.Formatting.Compact;\r\nusing Beacon.Runtime.Models;"
                            );
                            modified = true;
                            _logger.Debug("[UpdateServiceFiles] Added Models namespace (special case) to {File}", fileName);
                        }
                    }

                    if (modified)
                    {
                        File.WriteAllText(file, content);
                        var destInfo = new FileInfo(file);
                        _logger.Information("[UpdateServiceFiles] Updated namespace references in {File} ({FileLen} bytes)", fileName, destInfo.Length);
                    }
                    else
                    {
                        _logger.Debug("[UpdateServiceFiles] No update needed for {File}", fileName);
                    }
                }
            }
        }

        /// <summary>
        /// Copy buffer files
        /// </summary>
        private void CopyBufferFiles(string runtimeDir)
        {
            var buffersDir = Path.Combine(runtimeDir, "Buffers");
            var generatedDir = Path.Combine(runtimeDir, "Generated");

            // Ensure the Generated directory exists
            if (!Directory.Exists(generatedDir))
            {
                Directory.CreateDirectory(generatedDir);
                _logger.Information("Created Generated directory at {Path}", generatedDir);
            }

            // Check if RingBufferManager is already in Generated directory (from code generation)
            bool hasRingBufferManager = false;
            if (Directory.Exists(generatedDir)) // Double-check to be safe
            {
                foreach (var file in Directory.GetFiles(generatedDir))
                {
                    // If we find RingBufferManager in any generated file, don't copy the template version
                    var content = File.ReadAllText(file);
                    if (content.Contains("class RingBufferManager"))
                    {
                        hasRingBufferManager = true;
                        _logger.Information(
                            "RingBufferManager already exists in generated files, skipping template copy"
                        );
                        break;
                    }
                }
            }

            // Copy buffer files but skip RingBufferManager if CircularBuffer.cs already exists with a RingBufferManager
            var circularBufferContent = GetTemplateContent("Runtime/Buffers/CircularBuffer.cs");
            bool circularBufferHasRingBufferManager = circularBufferContent.Contains(
                "public class RingBufferManager"
            );

            // Copy CircularBuffer.cs first
            CopyTemplateFile(
                "Runtime/Buffers/CircularBuffer.cs",
                Path.Combine(buffersDir, "CircularBuffer.cs")
            );
            CopyTemplateFile(
                "Runtime/Buffers/IDateTimeProvider.cs",
                Path.Combine(buffersDir, "IDateTimeProvider.cs")
            );
            CopyTemplateFile(
                "Runtime/Buffers/SystemDateTimeProvider.cs",
                Path.Combine(buffersDir, "SystemDateTimeProvider.cs")
            );

            // Only copy RingBufferManager.cs if CircularBuffer.cs doesn't already contain it
            if (!circularBufferHasRingBufferManager && !hasRingBufferManager)
            {
                _logger.Information("Copying separate RingBufferManager implementation");
                CopyTemplateFile(
                    "Runtime/Buffers/RingBufferManager.cs",
                    Path.Combine(buffersDir, "RingBufferManager.cs")
                );
            }
            else
            {
                _logger.Information(
                    "Skipping RingBufferManager.cs as CircularBuffer.cs already contains it"
                );
            }
        }

        /// <summary>
        /// Helper method to clean and recreate a directory
        /// </summary>
        private void CleanAndRecreateDirectory(string directory)
        {
            if (Directory.Exists(directory))
            {
                try
                {
                    Directory.Delete(directory, true);
                    _logger.Debug("Deleted existing directory: {Path}", directory);
                }
                catch (Exception ex)
                {
                    _logger.Warning(
                        "Could not delete directory {Path}: {Error}",
                        directory,
                        ex.Message
                    );
                }
            }

            Directory.CreateDirectory(directory);
            _logger.Debug("Created directory: {Path}", directory);
        }

        /// <summary>
        /// Generates the Program.cs file with AOT compatibility
        /// </summary>
        private void GenerateProgramCs(string runtimeDir, BuildConfig buildConfig)
        {
            // First delete any existing Program.cs file to avoid duplicate class definitions
            string programPath = Path.Combine(runtimeDir, "Program.cs");
            if (File.Exists(programPath))
            {
                try
                {
                    File.Delete(programPath);
                    _logger.Information("Deleted existing Program.cs file");
                }
                catch (Exception ex)
                {
                    _logger.Warning("Could not delete existing Program.cs: {Error}", ex.Message);
                }
            }

            // Also check for and delete any Program.cs file in Generated directory
            string generatedProgramPath = Path.Combine(runtimeDir, "Generated", "Program.cs");
            if (File.Exists(generatedProgramPath))
            {
                try
                {
                    File.Delete(generatedProgramPath);
                    _logger.Information(
                        "Deleted existing Program.cs file from Generated directory"
                    );
                }
                catch (Exception ex)
                {
                    _logger.Warning(
                        "Could not delete existing Program.cs in Generated directory: {Error}",
                        ex.Message
                    );
                }
            }

            var sb = new StringBuilder();

            // Add file header
            sb.AppendLine("// Auto-generated Program.cs");
            sb.AppendLine("// Generated: " + DateTime.UtcNow.ToString("O"));
            sb.AppendLine(
                "// This file contains the main entry point for the Beacon Runtime Engine"
            );
            sb.AppendLine();

            // Add standard using statements first
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.IO;");
            sb.AppendLine("using System.Diagnostics.CodeAnalysis;");
            sb.AppendLine("using System.Runtime.CompilerServices;");
            sb.AppendLine("using System.Threading;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using Microsoft.Extensions.Logging;");
            sb.AppendLine("using Serilog;");
            sb.AppendLine("using StackExchange.Redis;"); // Add Redis namespace explicitly
            sb.AppendLine("using Prometheus;"); // Add Prometheus namespace explicitly
            sb.AppendLine($"using {buildConfig.Namespace}.Buffers;");
            sb.AppendLine($"using {buildConfig.Namespace}.Services;");
            sb.AppendLine($"using {buildConfig.Namespace}.Interfaces;");
            sb.AppendLine($"using {buildConfig.Namespace}.Models;");
            sb.AppendLine($"using {buildConfig.Namespace}.Generated;");
            sb.AppendLine("using ILogger = Serilog.ILogger;");
            sb.AppendLine();

            // Add AOT compatibility attributes
            sb.AppendLine(Generation.CodeGenHelpers.GenerateAOTAttributes(buildConfig.Namespace));

            // Add namespace and class declaration
            sb.AppendLine($"namespace {buildConfig.Namespace}");
            sb.AppendLine("{");
            sb.AppendLine("    public class Program");
            sb.AppendLine("    {");

            // Add main method with proper name (not MainEntry)
            sb.AppendLine("        public static async Task Main(string[] args)");
            sb.AppendLine("        {");
            sb.AppendLine("            // Configure Serilog");
            sb.AppendLine("            var logger = ConfigureLogging();");
            sb.AppendLine("            logger.Information(\"Starting Beacon Runtime Engine\");");
            sb.AppendLine();
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                // Load configuration");
            sb.AppendLine("                var config = RuntimeConfig.LoadFromEnvironment();");
            sb.AppendLine(
                "                logger.Information(\"Loaded configuration with {SensorCount} sensors\", config.ValidSensors.Count);"
            );
            sb.AppendLine();
            sb.AppendLine("                // Initialize Redis service");
            sb.AppendLine("                var redisConfig = config.Redis;");
            sb.AppendLine();
            sb.AppendLine("                // Create buffer manager for temporal rules");
            sb.AppendLine(
                $"                var bufferManager = new RingBufferManager(config.BufferCapacity);"
            );
            sb.AppendLine();
            sb.AppendLine("                // Initialize metrics service");
            sb.AppendLine(
                "                var metricsService = new MetricsService(logger, Environment.MachineName);"
            );
            sb.AppendLine(
                "                metricsService.StartMetricsServer(9090);"
            );
            sb.AppendLine(
                "                logger.Information(\"Prometheus metrics available at http://localhost:9090/metrics\");"
            );
            sb.AppendLine();
            sb.AppendLine("                // Initialize runtime orchestrator");
            sb.AppendLine(
                "                using var redisService = new RedisService(redisConfig, logger, metricsService);"
            );
            sb.AppendLine(
                "                var coordinator = new RuleCoordinator(redisService, logger, bufferManager, metricsService);"
            );
            sb.AppendLine(
                "                var orchestrator = new RuntimeOrchestrator(redisService, logger, coordinator, metricsService);"
            );
            sb.AppendLine();
            sb.AppendLine("                // Run the main cycle loop");
            sb.AppendLine(
                "                await orchestrator.StartAsync(config.CycleTime);"
            );
            sb.AppendLine();
            sb.AppendLine("                // Wait for Ctrl+C");
            sb.AppendLine("                var cancelSource = new CancellationTokenSource();");
            sb.AppendLine("                Console.CancelKeyPress += (s, e) =>");
            sb.AppendLine("                {");
            sb.AppendLine("                    logger.Information(\"Shutdown requested\");");
            sb.AppendLine("                    cancelSource.Cancel();");
            sb.AppendLine("                    e.Cancel = true;");
            sb.AppendLine("                };");
            sb.AppendLine();
            sb.AppendLine("                // Wait until cancellation is requested");
            sb.AppendLine("                try");
            sb.AppendLine("                {");
            sb.AppendLine("                    await Task.Delay(Timeout.Infinite, cancelSource.Token);");
            sb.AppendLine("                }");
            sb.AppendLine("                catch (OperationCanceledException)");
            sb.AppendLine("                {");
            sb.AppendLine("                    // Cancellation was requested");
            sb.AppendLine("                }");
            sb.AppendLine();
            sb.AppendLine("                // Stop the orchestrator");
            sb.AppendLine("                await orchestrator.StopAsync();");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (Exception ex)");
            sb.AppendLine("            {");
            sb.AppendLine(
                "                logger.Error(ex, \"Fatal error in Beacon Runtime Engine\");"
            );
            sb.AppendLine("                Environment.ExitCode = 1;");
            sb.AppendLine("            }");
            sb.AppendLine("            finally");
            sb.AppendLine("            {");
            sb.AppendLine("                Log.CloseAndFlush();");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Add helper methods
            sb.AppendLine("        private static ILogger ConfigureLogging()");
            sb.AppendLine("        {");
            sb.AppendLine("            var logConfig = new LoggerConfiguration()");
            sb.AppendLine("                .MinimumLevel.Information()");
            sb.AppendLine("                .Enrich.WithThreadId()");
            sb.AppendLine("                .WriteTo.Console()");
            sb.AppendLine("                .WriteTo.File(");
            sb.AppendLine("                    Path.Combine(\"logs\", \"beacon-.log\"),");
            sb.AppendLine("                    rollingInterval: RollingInterval.Day,");
            sb.AppendLine("                    retainedFileCountLimit: 7");
            sb.AppendLine("                );");
            sb.AppendLine();
            sb.AppendLine("            var logger = logConfig.CreateLogger();");
            sb.AppendLine("            Log.Logger = logger;");
            sb.AppendLine("            return logger;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            
            // Write the Program.cs file to disk
            File.WriteAllText(programPath, sb.ToString());
            _logger.Information("Generated Program.cs file: {Path}", programPath);
        }

        /// <summary>
        /// Generates the test fixture classes
        /// </summary>
        private void GenerateTestFixtures(string testsDir, BuildConfig buildConfig)
        {
            string fixturesDir = Path.Combine(testsDir, "Fixtures");
            string testFixturePath = Path.Combine(fixturesDir, "RuntimeTestFixture.cs");

            var sb = new StringBuilder();
            sb.AppendLine("// Auto-generated test fixture");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using Microsoft.Extensions.Logging;");
            sb.AppendLine("using Xunit;");
            sb.AppendLine("using Testcontainers.Redis;");
            sb.AppendLine($"using {buildConfig.Namespace}.Services;");
            sb.AppendLine();

            sb.AppendLine("namespace Beacon.Tests.Fixtures");
            sb.AppendLine("{");
            sb.AppendLine("    public class RuntimeTestFixture : IAsyncLifetime");
            sb.AppendLine("    {");
            sb.AppendLine("        private RedisContainer _redisContainer;");
            sb.AppendLine();

            sb.AppendLine("        public string RedisConnectionString { get; private set; }");
            sb.AppendLine();

            sb.AppendLine("        public async Task InitializeAsync()");
            sb.AppendLine("        {");
            sb.AppendLine("            _redisContainer = new RedisBuilder()");
            sb.AppendLine("                .WithImage(\"redis:latest\")");
            sb.AppendLine("                .WithPortBinding(6379, true)");
            sb.AppendLine("                .Build();");
            sb.AppendLine();

            sb.AppendLine("            await _redisContainer.StartAsync();");
            sb.AppendLine(
                "            RedisConnectionString = _redisContainer.GetConnectionString();"
            );
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        public async Task DisposeAsync()");
            sb.AppendLine("        {");
            sb.AppendLine("            if (_redisContainer != null)");
            sb.AppendLine("            {");
            sb.AppendLine("                await _redisContainer.DisposeAsync();"); // This was previously just _redisContainer.DisposeAsync() without await
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            // Create fixtures directory if it doesn't exist
            Directory.CreateDirectory(fixturesDir);

            // Write the test fixture
            File.WriteAllText(testFixturePath, sb.ToString());
            _logger.Information("Generated test fixture: {Path}", testFixturePath);

            // Generate a basic test class
            string basicTestPath = Path.Combine(testsDir, "BasicRuntimeTests.cs");

            sb = new StringBuilder();
            sb.AppendLine("// Auto-generated test class");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using Xunit;");
            sb.AppendLine("using Beacon.Tests.Fixtures;");
            sb.AppendLine($"using {buildConfig.Namespace}.Services;");
            sb.AppendLine();

            sb.AppendLine("namespace Beacon.Tests");
            sb.AppendLine("{");
            sb.AppendLine("    public class BasicRuntimeTests : IClassFixture<RuntimeTestFixture>");
            sb.AppendLine("    {");
            sb.AppendLine("        private readonly RuntimeTestFixture _fixture;");
            sb.AppendLine();

            sb.AppendLine("        public BasicRuntimeTests(RuntimeTestFixture fixture)");
            sb.AppendLine("        {");
            sb.AppendLine("            _fixture = fixture;");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        [Fact]");
            sb.AppendLine("        public void RedisConnection_IsAvailable()");
            sb.AppendLine("        {");
            sb.AppendLine("            Assert.NotNull(_fixture.RedisConnectionString);");
            sb.AppendLine("            Assert.NotEmpty(_fixture.RedisConnectionString);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(basicTestPath, sb.ToString());
            _logger.Information("Generated basic test class: {Path}", basicTestPath);
        }

        /// <summary>
        /// Copy a template file from the source templates to a destination path
        /// </summary>
        private void CopyTemplateFile(string templatePath, string destinationPath)
        {
            _logger.Information("[CopyTemplateFile] Attempting to copy from template '{TemplatePath}' to destination '{DestinationPath}'", templatePath, destinationPath);
            try
            {
                _logger.Debug("[CopyTemplateFile] Requested copy from template '{TemplatePath}' to destination '{DestinationPath}'", templatePath, destinationPath);
                string? directoryPath = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
                else
                {
                    _logger.Warning("[CopyTemplateFile] Cannot create directory - Path is null or empty for {Destination}", destinationPath);
                }

                // Find the actual template file path
                string templateFilePath = null;
                try
                {
                    templateFilePath = GetTemplateFilePath(templatePath);
                    _logger.Debug("[CopyTemplateFile] Resolved template file path: {TemplateFilePath}", templateFilePath);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "[CopyTemplateFile] Failed to resolve template file path for {TemplatePath}", templatePath);
                }
                if (!string.IsNullOrEmpty(templateFilePath) && File.Exists(templateFilePath))
                {
                    File.Copy(templateFilePath, destinationPath, true);
                    var srcInfo = new FileInfo(templateFilePath);
                    var destInfo = new FileInfo(destinationPath);
                    _logger.Information(
                        "[CopyTemplateFile] Copied template (binary): {Source} ({SrcLen} bytes) to {Destination} ({DestLen} bytes)",
                        templateFilePath, srcInfo.Length, destinationPath, destInfo.Length
                    );

                    // Byte-for-byte comparison after copy
                    bool filesMatch = FilesAreIdentical(templateFilePath, destinationPath);
                    if (!filesMatch)
                    {
                        _logger.Warning("[CopyTemplateFile] WARNING: Source and destination files differ after copy! {Source} -> {Destination}", templateFilePath, destinationPath);
                    }
                    else
                    {
                        _logger.Information("[CopyTemplateFile] Source and destination files are identical after copy.");
                    }

                    // Direct diagnostic output for RedisService.cs
                    if (Path.GetFileName(templateFilePath).Contains("RedisService"))
                    {
                        try
                        {
                            string diagnosticDir = Path.Combine(Path.GetTempPath(), "PulsarDiagnostic");
                            Directory.CreateDirectory(diagnosticDir);

                            // Save copy of template file
                            string templateCopyPath = Path.Combine(diagnosticDir, "RedisService_template.cs");
                            File.Copy(templateFilePath, templateCopyPath, true);

                            // Save copy of destination file
                            string destCopyPath = Path.Combine(diagnosticDir, "RedisService_output.cs");
                            File.Copy(destinationPath, destCopyPath, true);

                            // Output diagnostic info
                            string diagFile = Path.Combine(diagnosticDir, "copy_diagnostics.txt");
                            using (var writer = new StreamWriter(diagFile, true))
                            {
                                writer.WriteLine($"=== COPY DIAGNOSTIC: {DateTime.Now} ===");
                                writer.WriteLine($"Source: {templateFilePath}, Length: {srcInfo.Length} bytes");
                                writer.WriteLine($"Destination: {destinationPath}, Length: {destInfo.Length} bytes");
                                writer.WriteLine($"Files identical: {filesMatch}");
                                writer.WriteLine("Template file sample (first 500 chars):");
                                writer.WriteLine(File.ReadAllText(templateFilePath).Substring(0, Math.Min(500, (int)srcInfo.Length)));
                                writer.WriteLine("\nDestination file sample (first 500 chars):");
                                writer.WriteLine(File.ReadAllText(destinationPath).Substring(0, Math.Min(500, (int)destInfo.Length)));
                                writer.WriteLine("=== END DIAGNOSTIC ===");
                                writer.WriteLine();
                            }

                            Console.WriteLine($"DIAGNOSTIC: Files saved to {diagnosticDir}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to create diagnostic files: {ex.Message}");
                        }
                    }
                }
                else
                {
                    _logger.Error("[CopyTemplateFile] Template file not found: {TemplatePath}", templatePath);
                    throw new FileNotFoundException($"Template file not found: {templatePath}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[CopyTemplateFile] Exception copying template {TemplatePath} to {DestinationPath}", templatePath, destinationPath);
            }
        }

        // Helper: compare two files byte-for-byte
        private bool FilesAreIdentical(string filePath1, string filePath2)
        {
            const int bufferSize = 1024 * 8;
            using (var fs1 = new FileStream(filePath1, FileMode.Open, FileAccess.Read))
            using (var fs2 = new FileStream(filePath2, FileMode.Open, FileAccess.Read))
            {
                if (fs1.Length != fs2.Length)
                    return false;
                var buffer1 = new byte[bufferSize];
                var buffer2 = new byte[bufferSize];
                int read1, read2;
                do
                {
                    read1 = fs1.Read(buffer1, 0, bufferSize);
                    read2 = fs2.Read(buffer2, 0, bufferSize);
                    if (read1 != read2)
                        return false;
                    for (int i = 0; i < read1; i++)
                    {
                        if (buffer1[i] != buffer2[i])
                            return false;
                    }
                } while (read1 > 0);
                return true;
            }
        }

        // Helper to get the full template file path (mirrors logic in GetTemplateContent)
        private string GetTemplateFilePath(string templateFileName)
        {
            var possiblePaths = new[]
            {
                Path.Combine("Pulsar.Compiler", "Config", "Templates", templateFileName),
                Path.Combine(
                    Path.GetDirectoryName(typeof(TemplateManager).Assembly.Location) ?? "",
                    "Config",
                    "Templates",
                    templateFileName
                ),
            };
            foreach (var path in possiblePaths)
            {
                var normalizedPath = Path.GetFullPath(path);
                if (File.Exists(normalizedPath))
                {
                    return normalizedPath;
                }
            }
            throw new FileNotFoundException($"Template file not found: {templateFileName}");
        }

        /// <summary>
        /// Gets the content of a template file
        /// </summary>
        private string GetTemplateContent(string templateFileName)
        {
            // Try multiple possible locations for the template files
            var possiblePaths = new[]
            {
                // Direct path from working directory
                Path.Combine("Pulsar.Compiler", "Config", "Templates", templateFileName),
                // Path relative to assembly location
                Path.Combine(
                    Path.GetDirectoryName(typeof(TemplateManager).Assembly.Location) ?? "",
                    "Config",
                    "Templates",
                    templateFileName
                ),
                // Path from assembly base directory
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Config",
                    "Templates",
                    templateFileName
                ),
                // Path relative to project root (go up from bin directory)
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "..",
                    "..",
                    "..",
                    "Pulsar.Compiler",
                    "Config",
                    "Templates",
                    templateFileName
                ),
                // Absolute path based on solution directory structure
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "..",
                    "..",
                    "..",
                    "..",
                    "Pulsar.Compiler",
                    "Config",
                    "Templates",
                    templateFileName
                ),
            };

            foreach (var path in possiblePaths)
            {
                var normalizedPath = Path.GetFullPath(path);
                if (File.Exists(normalizedPath))
                {
                    _logger.Debug("Found template at: {Path}", normalizedPath);
                    return File.ReadAllText(normalizedPath);
                }
            }

            _logger.Error(
                "Template file not found: {TemplateFile}. Searched in: {Paths}",
                templateFileName,
                string.Join(", ", possiblePaths)
            );

            throw new FileNotFoundException(
                $"Template file not found: {templateFileName}. Searched in: {string.Join(", ", possiblePaths)}"
            );
        }
    }
}
