// File: Pulsar.Compiler/DependencyAnalyzer.cs

using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Compiler.Models;

namespace Pulsar.Compiler.Core
{
    /// <summary>
    /// Analyzes rule dependencies to determine execution order and validate rules.
    /// This class uses specialized ConditionAnalyzer and CycleDetector to manage
    /// different aspects of dependency analysis.
    /// </summary>
    public class DependencyAnalyzer : IDisposable
    {
        private readonly ILogger<DependencyAnalyzer> _logger;
        private readonly int _maxDependencyDepth;
        private readonly ConditionAnalyzer _conditionAnalyzer;
        private readonly CycleDetector _cycleDetector;
        private bool _disposed;

        private readonly Dictionary<string, HashSet<string>> _temporalDependencies = new();

        /// <summary>
        /// Creates a new DependencyAnalyzer with the specified depth limit and logger.
        /// </summary>
        public DependencyAnalyzer(
            int maxDependencyDepth = 10,
            ILogger<DependencyAnalyzer>? logger = null)
        {
            _logger = logger ?? NullLogger<DependencyAnalyzer>.Instance;
            _maxDependencyDepth = maxDependencyDepth;
            _conditionAnalyzer = new ConditionAnalyzer(null); // Use null logger - will default to NullLogger inside
            _cycleDetector = new CycleDetector(maxDependencyDepth, null); // Use null logger - will default to NullLogger inside
        }

        /// <summary>
        /// Disposes resources used by the analyzer.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Validates dependencies between rules and identifies issues.
        /// </summary>
        public DependencyValidationResult ValidateDependencies(List<RuleDefinition> rules)
        {
            var result = new DependencyValidationResult { IsValid = true };

            // Build a graph of rule dependencies
            var graph = BuildGraph(rules);

            // Check for circular dependencies using specialized detector
            var cycles = _cycleDetector.FindCircularDependencies(graph);
            if (cycles.Any())
            {
                result.IsValid = false;
                result.CircularDependencies = cycles;
                foreach (var cycle in cycles)
                {
                    _logger.LogError(
                        "Circular dependency detected: {Path}",
                        string.Join(" -> ", cycle)
                    );
                }
            }

            // Check dependency depths
            var deepChains = _cycleDetector.FindDeepDependencyChains(graph);
            if (deepChains.Any())
            {
                result.DeepDependencyChains = deepChains;
                foreach (var chain in deepChains)
                {
                    _logger.LogWarning(
                        "Deep dependency chain detected: {Path}",
                        string.Join(" -> ", chain)
                    );
                }
            }

            // Calculate complexity scores
            result.RuleComplexityScores = CalculateRuleComplexity(rules, graph);

            // Process temporal dependencies
            var temporalDependencies = ProcessTemporalDependencies(rules);
            result.TemporalDependencies = temporalDependencies;

            return result;
        }

        /// <summary>
        /// Analyzes and sorts rules based on their dependencies.
        /// </summary>
        public List<RuleDefinition> AnalyzeDependencies(List<RuleDefinition> rules)
        {
            try
            {
                var graph = BuildGraph(rules);
                var sortedRules = TopologicalSort(graph, rules);
                return sortedRules;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing rule dependencies");
                throw;
            }
        }

        /// <summary>
        /// Builds a dependency graph from rule definitions.
        /// </summary>
        private Dictionary<string, HashSet<string>> BuildGraph(List<RuleDefinition> rules)
        {
            var graph = new Dictionary<string, HashSet<string>>();
            var sensorToRuleMap = new Dictionary<string, string>();

            // First, build a map of sensors to the rules that produce them
            foreach (var rule in rules)
            {
                foreach (var action in rule.Actions.OfType<SetValueAction>())
                {
                    sensorToRuleMap[action.Key] = rule.Name;
                }
            }

            // Then, build the dependency graph
            foreach (var rule in rules)
            {
                if (!graph.ContainsKey(rule.Name))
                {
                    graph[rule.Name] = new HashSet<string>();
                }

                // Check condition dependencies
                if (rule.Conditions != null)
                {
                    // Process "All" conditions
                    if (rule.Conditions.All != null)
                    {
                        foreach (var condition in rule.Conditions.All)
                        {
                            AddConditionDependencies(condition, rule.Name, sensorToRuleMap, graph);
                        }
                    }

                    // Process "Any" conditions
                    if (rule.Conditions.Any != null)
                    {
                        foreach (var condition in rule.Conditions.Any)
                        {
                            AddConditionDependencies(condition, rule.Name, sensorToRuleMap, graph);
                        }
                    }
                }
            }

            return graph;
        }

        /// <summary>
        /// Adds dependencies for a condition to the graph.
        /// </summary>
        private void AddConditionDependencies(
            ConditionDefinition condition,
            string ruleName,
            Dictionary<string, string> sensorToRuleMap,
            Dictionary<string, HashSet<string>> graph
        )
        {
            switch (condition)
            {
                case ComparisonCondition comparison:
                    if (sensorToRuleMap.TryGetValue(comparison.Sensor, out var producerRule))
                    {
                        graph[ruleName].Add(producerRule);
                        _logger.LogDebug(
                            "Rule {RuleName} depends on rule {DependencyName} via sensor {Sensor}",
                            ruleName,
                            producerRule,
                            comparison.Sensor
                        );
                    }
                    break;

                case ExpressionCondition expression:
                    var sensors = _conditionAnalyzer.ExtractSensorsFromExpression(expression.Expression);
                    foreach (var sensor in sensors)
                    {
                        if (sensorToRuleMap.TryGetValue(sensor, out var producer))
                        {
                            graph[ruleName].Add(producer);
                            _logger.LogDebug(
                                "Rule {RuleName} depends on rule {DependencyName} via sensor {Sensor} in expression",
                                ruleName,
                                producer,
                                sensor
                            );
                        }
                    }
                    break;

                case ThresholdOverTimeCondition threshold:
                    if (sensorToRuleMap.TryGetValue(threshold.Sensor, out var thresholdProducer))
                    {
                        graph[ruleName].Add(thresholdProducer);
                        _logger.LogDebug(
                            "Rule {RuleName} depends on rule {DependencyName} via temporal sensor {Sensor}",
                            ruleName,
                            thresholdProducer,
                            threshold.Sensor
                        );
                    }
                    break;
            }
        }

        /// <summary>
        /// Calculates complexity scores for each rule.
        /// </summary>
        private Dictionary<string, int> CalculateRuleComplexity(
            List<RuleDefinition> rules,
            Dictionary<string, HashSet<string>> graph
        )
        {
            var scores = new Dictionary<string, int>();

            foreach (var rule in rules)
            {
                var score = 0;

                // Base complexity
                score += rule.Conditions?.All?.Count ?? 0;
                score += rule.Conditions?.Any?.Count ?? 0;
                score += rule.Actions.Count;

                // Add dependency depth to complexity
                if (graph.ContainsKey(rule.Name))
                {
                    score += _cycleDetector.CalculateDependencyDepth(rule.Name, graph);
                }

                scores[rule.Name] = score;
            }

            return scores;
        }

        /// <summary>
        /// Processes temporal dependencies in rules.
        /// </summary>
        private Dictionary<string, HashSet<string>> ProcessTemporalDependencies(List<RuleDefinition> rules)
        {
            var temporalDependencies = new Dictionary<string, HashSet<string>>();

            foreach (var rule in rules)
            {
                // Process "All" conditions for temporal dependencies
                if (rule.Conditions?.All != null)
                {
                    foreach (
                        var condition in rule.Conditions.All.OfType<ThresholdOverTimeCondition>()
                    )
                    {
                        if (!temporalDependencies.ContainsKey(rule.Name))
                        {
                            temporalDependencies[rule.Name] = new HashSet<string>();
                        }
                        temporalDependencies[rule.Name].Add(condition.Sensor);

                        _logger.LogDebug(
                            "Added temporal dependency: Rule {Rule} depends on sensor {Sensor}",
                            rule.Name,
                            condition.Sensor
                        );
                    }
                }

                // Process "Any" conditions for temporal dependencies
                if (rule.Conditions?.Any != null)
                {
                    foreach (
                        var condition in rule.Conditions.Any.OfType<ThresholdOverTimeCondition>()
                    )
                    {
                        if (!temporalDependencies.ContainsKey(rule.Name))
                        {
                            temporalDependencies[rule.Name] = new HashSet<string>();
                        }
                        temporalDependencies[rule.Name].Add(condition.Sensor);

                        _logger.LogDebug(
                            "Added temporal dependency: Rule {Rule} depends on sensor {Sensor}",
                            rule.Name,
                            condition.Sensor
                        );
                    }
                }
            }

            return temporalDependencies;
        }

        /// <summary>
        /// Gets a dependency map with layers for each rule.
        /// </summary>
        public Dictionary<string, string> GetDependencyMap(List<RuleDefinition> rules)
        {
            var graph = BuildGraph(rules);
            var layerMap = _cycleDetector.AssignLayerDFS(graph, rules.Select(r => r.Name));

            // Convert int values to strings for AOT compatibility
            return layerMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
        }

        /// <summary>
        /// Performs topological sort on rules based on their dependencies.
        /// </summary>
        private List<RuleDefinition> TopologicalSort(
            Dictionary<string, HashSet<string>> graph,
            List<RuleDefinition> rules
        )
        {
            var visited = new HashSet<string>();
            var visiting = new HashSet<string>();
            var sorted = new List<string>();

            foreach (var rule in rules)
            {
                if (!visited.Contains(rule.Name))
                {
                    TopologicalSortVisit(rule.Name, graph, visited, visiting, sorted);
                }
            }

            sorted.Reverse();
            return sorted.Select(name => rules.First(r => r.Name == name)).ToList();
        }

        private void TopologicalSortVisit(
            string ruleName,
            Dictionary<string, HashSet<string>> graph,
            HashSet<string> visited,
            HashSet<string> visiting,
            List<string> sorted
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

            if (graph.ContainsKey(ruleName))
            {
                foreach (var dependency in graph[ruleName])
                {
                    TopologicalSortVisit(dependency, graph, visited, visiting, sorted);
                }
            }

            visiting.Remove(ruleName);
            visited.Add(ruleName);
            sorted.Add(ruleName);
        }
    }
}
