// File: Pulsar.Compiler/Core/CycleDetector.cs

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Pulsar.Compiler.Core
{
    /// <summary>
    /// Specialized detector for cycles in dependency graphs
    /// </summary>
    public class CycleDetector
    {
        private readonly ILogger<CycleDetector> _logger;
        private readonly int _maxDependencyDepth;

        public CycleDetector(int maxDependencyDepth = 10, ILogger<CycleDetector>? logger = null)
        {
            _logger = logger ?? NullLogger<CycleDetector>.Instance;
            _maxDependencyDepth = maxDependencyDepth;
        }

        /// <summary>
        /// Finds circular dependencies in a dependency graph
        /// </summary>
        public List<List<string>> FindCircularDependencies(Dictionary<string, HashSet<string>> graph)
        {
            var cycles = new List<List<string>>();
            var visited = new HashSet<string>();
            var path = new List<string>();

            foreach (var rule in graph.Keys)
            {
                if (!visited.Contains(rule))
                {
                    DetectCycle(rule, graph, visited, path, cycles);
                }
            }

            return cycles;
        }

        /// <summary>
        /// Finds long dependency chains that exceed the maximum depth
        /// </summary>
        public List<List<string>> FindDeepDependencyChains(Dictionary<string, HashSet<string>> graph)
        {
            var deepChains = new List<List<string>>();
            var visited = new HashSet<string>();
            var path = new List<string>();

            foreach (var rule in graph.Keys)
            {
                if (!visited.Contains(rule))
                {
                    FindLongPaths(rule, graph, visited, path, deepChains);
                }
            }

            return deepChains;
        }

        private void DetectCycle(
            string current,
            Dictionary<string, HashSet<string>> graph,
            HashSet<string> visited,
            List<string> path,
            List<List<string>> cycles
        )
        {
            _logger.LogDebug(
                "DetectCycle called: current={Current}, path={Path}",
                current,
                string.Join("->", path)
            );

            // Use a separate set for tracking the current path to detect cycles
            var currentPath = new HashSet<string>(path);

            if (currentPath.Contains(current))
            {
                var cycleStart = path.IndexOf(current);
                var cycle = path.Skip(cycleStart).Concat(new[] { current }).ToList();
                _logger.LogError("Cycle found: {Cycle}", string.Join(" -> ", cycle));
                cycles.Add(cycle);
                return;
            }

            if (visited.Contains(current))
            {
                return;
            }

            visited.Add(current);
            path.Add(current);

            if (graph.ContainsKey(current))
            {
                foreach (var dependency in graph[current])
                {
                    DetectCycle(dependency, graph, visited, path, cycles);
                }
            }

            path.RemoveAt(path.Count - 1);
        }

        private void FindLongPaths(
            string current,
            Dictionary<string, HashSet<string>> graph,
            HashSet<string> visited,
            List<string> path,
            List<List<string>> deepChains
        )
        {
            path.Add(current);

            // Check if the current path exceeds the maximum depth
            if (path.Count > _maxDependencyDepth)
            {
                _logger.LogWarning(
                    "Deep dependency chain detected: {Path}",
                    string.Join(" -> ", path)
                );
                deepChains.Add(new List<string>(path)); // Create a copy of the path
            }

            // Only continue if the current node is not already visited
            if (!visited.Contains(current))
            {
                // Mark as visited for this path
                visited.Add(current);

                // Process dependencies if they exist
                if (graph.ContainsKey(current))
                {
                    foreach (var dependency in graph[current])
                    {
                        FindLongPaths(dependency, graph, visited, path, deepChains);
                    }
                }

                // Backtrack: remove from visited to allow this node in other paths
                visited.Remove(current);
            }

            // Backtrack: remove from current path
            path.RemoveAt(path.Count - 1);
        }

        /// <summary>
        /// Calculates maximum dependency depth for a specific rule
        /// </summary>
        public int CalculateDependencyDepth(
            string ruleName,
            Dictionary<string, HashSet<string>> graph
        )
        {
            var visited = new HashSet<string>();
            var depth = 0;
            var queue = new Queue<(string Rule, int Depth)>();
            queue.Enqueue((ruleName, 0));

            while (queue.Count > 0)
            {
                var (current, currentDepth) = queue.Dequeue();

                if (!visited.Contains(current))
                {
                    visited.Add(current);
                    depth = Math.Max(depth, currentDepth);

                    if (graph.ContainsKey(current))
                    {
                        foreach (var dependency in graph[current])
                        {
                            queue.Enqueue((dependency, currentDepth + 1));
                        }
                    }
                }
            }

            return depth;
        }

        /// <summary>
        /// Assigns layers to rules based on their dependency depth
        /// </summary>
        public Dictionary<string, int> AssignLayerDFS(
            Dictionary<string, HashSet<string>> graph,
            IEnumerable<string> ruleNames
        )
        {
            var layerMap = new Dictionary<string, int>();
            var visited = new HashSet<string>();
            var visiting = new HashSet<string>();

            foreach (var ruleName in ruleNames)
            {
                if (!layerMap.ContainsKey(ruleName))
                {
                    AssignLayerDFSHelper(ruleName, graph, layerMap, visited, visiting);
                }
            }

            return layerMap;
        }

        private void AssignLayerDFSHelper(
            string ruleName,
            Dictionary<string, HashSet<string>> graph,
            Dictionary<string, int> layerMap,
            HashSet<string> visited,
            HashSet<string> visiting
        )
        {
            if (visiting.Contains(ruleName))
            {
                _logger.LogError("Cyclic dependency detected involving rule {RuleName}", ruleName);
                throw new InvalidOperationException(
                    $"Cyclic dependency detected involving rule '{ruleName}'"
                );
            }

            if (visited.Contains(ruleName))
            {
                return;
            }

            visiting.Add(ruleName);

            int maxDependencyLayer = -1;
            if (graph.ContainsKey(ruleName))
            {
                foreach (var dependency in graph[ruleName])
                {
                    if (!layerMap.ContainsKey(dependency))
                    {
                        AssignLayerDFSHelper(dependency, graph, layerMap, visited, visiting);
                    }
                    maxDependencyLayer = Math.Max(maxDependencyLayer, layerMap[dependency]);
                }
            }

            layerMap[ruleName] = maxDependencyLayer + 1;
            visiting.Remove(ruleName);
            visited.Add(ruleName);
        }
    }
}