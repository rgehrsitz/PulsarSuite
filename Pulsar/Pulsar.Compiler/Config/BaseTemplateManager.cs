// File: Pulsar.Compiler/Config/BaseTemplateManager.cs
// Version: 1.0.0
// Phase 3 Optimization: Shared template management functionality

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Serilog;

namespace Pulsar.Compiler.Config
{
    /// <summary>
    /// Base class for template managers providing shared functionality
    /// </summary>
    public abstract class BaseTemplateManager
    {
        protected readonly ILogger _logger;

        // Template path cache for performance optimization
        private static readonly ConcurrentDictionary<string, string> _templatePathCache = new();

        // Template versions and source paths
        protected static readonly Dictionary<string, string> TemplateVersions = new Dictionary<
            string,
            string
        >
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
            ["Project.csproj"] = "2.0.0",
        };

        protected BaseTemplateManager(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Optimized template file path resolution with caching
        /// </summary>
        protected string GetTemplateFilePath(string templateFileName)
        {
            // Check cache first
            if (_templatePathCache.TryGetValue(templateFileName, out var cachedPath))
            {
                if (File.Exists(cachedPath))
                {
                    return cachedPath;
                }
                // Remove invalid cached path
                _templatePathCache.TryRemove(templateFileName, out _);
            }

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
                    // Cache the successful path
                    _templatePathCache.TryAdd(templateFileName, normalizedPath);
                    return normalizedPath;
                }
            }

            throw new FileNotFoundException($"Template file not found: {templateFileName}");
        }

        /// <summary>
        /// Optimized template content retrieval with caching
        /// </summary>
        protected string GetTemplateContent(string templateFileName)
        {
            var templatePath = GetTemplateFilePath(templateFileName);
            return File.ReadAllText(templatePath);
        }

        /// <summary>
        /// Optimized file copying with verification but reduced logging
        /// </summary>
        protected void CopyTemplateFile(
            string templatePath,
            string destinationPath,
            bool verifyAfterCopy = false
        )
        {
            try
            {
                // Ensure destination directory exists
                var directoryPath = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                // Get source template path
                string sourceTemplatePath = GetTemplateFilePath(templatePath);

                // Perform the copy
                File.Copy(sourceTemplatePath, destinationPath, true);

                // Optional verification (only when explicitly requested)
                if (verifyAfterCopy && !FilesAreIdentical(sourceTemplatePath, destinationPath))
                {
                    _logger.Warning(
                        "Template copy verification failed: {Source} -> {Destination}",
                        sourceTemplatePath,
                        destinationPath
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.Error(
                    ex,
                    "Failed to copy template {Template} to {Destination}",
                    templatePath,
                    destinationPath
                );
                throw;
            }
        }

        /// <summary>
        /// Efficient byte-for-byte file comparison
        /// </summary>
        protected static bool FilesAreIdentical(string filePath1, string filePath2)
        {
            const int bufferSize = 1024 * 8;
            using var fs1 = new FileStream(filePath1, FileMode.Open, FileAccess.Read);
            using var fs2 = new FileStream(filePath2, FileMode.Open, FileAccess.Read);

            if (fs1.Length != fs2.Length)
                return false;

            var buffer1 = new byte[bufferSize];
            var buffer2 = new byte[bufferSize];
            int read1,
                read2;

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

        /// <summary>
        /// Efficient directory cleanup and recreation
        /// </summary>
        protected void CleanAndRecreateDirectory(string directory)
        {
            if (Directory.Exists(directory))
            {
                try
                {
                    Directory.Delete(directory, true);
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
        }

        /// <summary>
        /// Clear the template path cache (useful for testing or when templates change)
        /// </summary>
        protected static void ClearTemplateCache()
        {
            _templatePathCache.Clear();
        }
    }
}
