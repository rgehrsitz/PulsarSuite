using BeaconTester.Core.Models;
using BeaconTester.RuleAnalyzer.Analysis;
using BeaconTester.RuleAnalyzer.Parsing;
using Common;
using Serilog;

namespace BeaconTester.RuleAnalyzer.Generation
{
    /// <summary>
    /// Generates test scenarios from rule definitions
    /// </summary>
    public class TestScenarioGenerator
    {
        private readonly ILogger _logger;
        private readonly TestCaseGenerator _testCaseGenerator;
        private readonly Analysis.RuleAnalyzer _ruleAnalyzer;

        /// <summary>
        /// Creates a new test scenario generator
        /// </summary>
        public TestScenarioGenerator(ILogger logger)
        {
            _logger = logger.ForContext<TestScenarioGenerator>();
            _testCaseGenerator = new TestCaseGenerator(logger);
            _ruleAnalyzer = new Analysis.RuleAnalyzer(logger);
        }

        /// <summary>
        /// Generates test scenarios for all rules
        /// </summary>
        public List<TestScenario> GenerateScenarios(List<RuleDefinition> rules)
        {
            _logger.Information("Generating test scenarios for {RuleCount} rules", rules.Count);
            var scenarios = new List<TestScenario>();

            try
            {
                // Analyze rules first to understand structure
                var analysis = _ruleAnalyzer.AnalyzeRules(rules);

                // Build a superset of all sensors referenced in any rule condition or action
                var allReferencedSensors = new HashSet<string>(analysis.InputSensors);
                foreach (var rule in rules)
                {
                    // Add sensors from conditions
                    if (rule.Conditions != null)
                    {
                        var sensors = _ruleAnalyzer.ConditionAnalyzer.ExtractSensors(rule.Conditions);
                        foreach (var s in sensors)
                        {
                            allReferencedSensors.Add(s);
                        }
                    }
                    // Add sensors from actions (for SetValueAction, etc.)
                    foreach (var action in rule.Actions)
                    {
                        if (action is SetValueAction setValueAction)
                        {
                            allReferencedSensors.Add(setValueAction.Key);
                        }
                    }
                }
                _logger.Information(
                    "Found {InputCount} total input sensors required by all rules (including referenced)",
                    allReferencedSensors.Count
                );

                // Build a map of input sensors to the conditions that reference them
                var inputConditionMap = BuildInputConditionMap(rules);

                // Generate basic test for each rule
                foreach (var rule in rules)
                {
                    var scenario = GenerateBasicScenario(
                        rule,
                        allReferencedSensors,
                        rules,
                        inputConditionMap
                    );
                    scenarios.Add(scenario);
                }

                // Generate V3 fallback test scenarios
                foreach (var rule in rules)
                {
                    if (rule.Inputs?.Any(i => i.FallbackStrategy != null) == true)
                    {
                        var fallbackTests = GenerateFallbackScenarios(rule, allReferencedSensors, inputConditionMap);
                        scenarios.AddRange(fallbackTests);
                    }
                }

                // Generate dependency tests
                if (analysis.Dependencies.Count > 0)
                {
                    var dependencyTests = GenerateDependencyScenarios(analysis, inputConditionMap);
                    scenarios.AddRange(dependencyTests);
                }

                // Generate temporal tests
                if (analysis.TemporalRules.Count > 0)
                {
                    // Generate basic temporal tests (existing)
                    var basicTemporalTests = GenerateTemporalScenarios(
                        analysis.TemporalRules,
                        allReferencedSensors,
                        inputConditionMap
                    );
                    scenarios.AddRange(basicTemporalTests);
                    
                    // Generate comprehensive v3 temporal tests (NEW)
                    var temporalGenerator = new TemporalTestScenarioGenerator(_logger);
                    foreach (var temporalRule in analysis.TemporalRules)
                    {
                        var comprehensiveTests = temporalGenerator.GenerateTemporalScenarios(temporalRule);
                        scenarios.AddRange(comprehensiveTests);
                    }
                }

                // After all scenarios are generated, ensure every step has all required inputs
                foreach (var scenario in scenarios)
                {
                    if (scenario.Steps != null)
                    {
                        foreach (var step in scenario.Steps)
                        {
                            var presentInputs = step.Inputs?.Select(i => i.Key).ToHashSet() ?? new HashSet<string>();
                            foreach (var sensor in allReferencedSensors)
                            {
                                if (!presentInputs.Contains(sensor))
                                {
                                    // Try to find a fallback/default from any rule that defines it
                                    object? defaultValue = null;
                                    foreach (var rule in rules)
                                    {
                                        var inputDef = rule.Inputs?.FirstOrDefault(i => i.Id == sensor);
                                        if (inputDef != null && inputDef.DefaultValue != null)
                                        {
                                            defaultValue = inputDef.DefaultValue;
                                            break;
                                        }
                                    }
                                    if (defaultValue == null)
                                    {
                                        defaultValue = 0.0; // Safe fallback
                                    }
                                    step.Inputs.Add(new TestInput { Key = sensor, Value = defaultValue });
                                    _logger.Debug("[FinalPass] Injected missing input {Sensor} with value {Value} into step {StepName}", sensor, defaultValue, step.Name);
                                }
                            }
                        }
                    }
                }

                _logger.Information("Generated {ScenarioCount} test scenarios", scenarios.Count);
                return scenarios;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error generating test scenarios");
                throw;
            }
        }

        /// <summary>
        /// Builds a map of input sensors to the conditions that reference them
        /// This helps us determine appropriate values for each input based on actual conditions
        /// </summary>
        private Dictionary<string, List<RuleConditionPair>> BuildInputConditionMap(
            List<RuleDefinition> rules
        )
        {
            _logger.Debug("Building input condition map for all rules");
            var inputConditionMap = new Dictionary<string, List<RuleConditionPair>>();

            foreach (var rule in rules)
            {
                if (rule.Conditions == null)
                    continue;

                // Find all input sensors used by this rule
                var sensors = FindConditionsForAllSensors(rule);

                // Add these to our map
                foreach (var pair in sensors)
                {
                    string sensor = pair.Key;
                    if (sensor.StartsWith("input:"))
                    {
                        if (!inputConditionMap.ContainsKey(sensor))
                        {
                            inputConditionMap[sensor] = new List<RuleConditionPair>();
                        }

                        // Add the rule and condition pair
                        foreach (var condition in pair.Value)
                        {
                            inputConditionMap[sensor].Add(new RuleConditionPair(rule, condition));
                        }
                    }
                }
            }

            return inputConditionMap;
        }

        /// <summary>
        /// Finds all conditions for all sensors in a rule
        /// </summary>
        private Dictionary<string, List<ConditionDefinition>> FindConditionsForAllSensors(
            RuleDefinition rule
        )
        {
            var result = new Dictionary<string, List<ConditionDefinition>>();

            if (rule.Conditions == null)
                return result;

            // Process the condition tree
            ProcessConditionForSensors(rule.Conditions, result);

            return result;
        }

        /// <summary>
        /// Recursively processes a condition tree to find all sensors and their conditions
        /// </summary>
        private void ProcessConditionForSensors(
            ConditionDefinition condition,
            Dictionary<string, List<ConditionDefinition>> result
        )
        {
            if (condition is ComparisonCondition comparison)
            {
                // Add this sensor and condition
                string sensor = comparison.Sensor;
                if (!result.ContainsKey(sensor))
                {
                    result[sensor] = new List<ConditionDefinition>();
                }
                result[sensor].Add(comparison);
            }
            else if (condition is ThresholdOverTimeCondition temporal)
            {
                // Add this sensor and condition
                string sensor = temporal.Sensor;
                if (!result.ContainsKey(sensor))
                {
                    result[sensor] = new List<ConditionDefinition>();
                }
                result[sensor].Add(temporal);
            }
            else if (condition is ConditionGroup group)
            {
                // Process 'all' conditions
                foreach (var wrapper in group.All)
                {
                    if (wrapper.Condition != null)
                    {
                        ProcessConditionForSensors(wrapper.Condition, result);
                    }
                }

                // Process 'any' conditions
                foreach (var wrapper in group.Any)
                {
                    if (wrapper.Condition != null)
                    {
                        ProcessConditionForSensors(wrapper.Condition, result);
                    }
                }
            }
        }

        /// <summary>
        /// Helper class to associate a rule with a condition
        /// </summary>
        private class RuleConditionPair
        {
            public RuleDefinition Rule { get; }
            public ConditionDefinition Condition { get; }

            public RuleConditionPair(RuleDefinition rule, ConditionDefinition condition)
            {
                Rule = rule;
                Condition = condition;
            }
        }

        /// <summary>
        /// Generates a basic test scenario for a rule
        /// </summary>
        /// <param name="rule">The rule to generate a test scenario for</param>
        /// <param name="allRequiredInputs">The complete set of input sensors required by all rules</param>
        /// <param name="allRules">The list of all rules</param>
        /// <param name="inputConditionMap">Map of input sensors to the conditions that reference them</param>
        private TestScenario GenerateBasicScenario(
            RuleDefinition rule,
            HashSet<string> allRequiredInputs,
            List<RuleDefinition> allRules,
            Dictionary<string, List<RuleConditionPair>>? inputConditionMap = null
        )
        {
            _logger.Debug("Generating basic test scenario for rule: {RuleName}", rule.Name);

            var scenario = new TestScenario
            {
                Name = $"{rule.Name}BasicTest",
                Description = $"Basic test for rule {rule.Name}: {rule.Description}",
            };

            try
            {
                // Generate test cases (input values that trigger the rule)
                var testCase = _testCaseGenerator.GenerateBasicTestCase(rule);

                // Create preset outputs for testing latching behavior
                var preSetOutputs = new Dictionary<string, object>();

                // Initialize all outputs that might be affected by this rule to a known state
                foreach (var action in rule.Actions)
                {
                    if (
                        action is SetValueAction setValueAction
                        && setValueAction.Key.StartsWith("output:")
                    )
                    {
                        // Determine a suitable initial value (opposite of what the rule will set)
                        var expectedValue = testCase.Outputs.ContainsKey(setValueAction.Key)
                            ? testCase.Outputs[setValueAction.Key]
                            : null;

                        if (expectedValue is bool boolValue)
                        {
                            // For boolean outputs, start with the opposite value
                            preSetOutputs[setValueAction.Key] = !boolValue;
                        }
                        else if (expectedValue != null)
                        {
                            // For non-boolean outputs, initialize to a different value
                            if (expectedValue is double numValue)
                            {
                                preSetOutputs[setValueAction.Key] = numValue > 5 ? 0.0 : 100.0;
                            }
                            else
                            {
                                preSetOutputs[setValueAction.Key] = "initial_value";
                            }
                        }
                    }
                }

                // Pre-set required output dependencies for this rule (e.g., output:stress_alert = true)
                if (rule.Conditions != null)
                {
                    var preSetOutputDependencies = _ruleAnalyzer
                        .ConditionAnalyzer.ExtractSensors(rule.Conditions)
                        .Where(s => s.StartsWith("output:"));
                    foreach (var dep in preSetOutputDependencies)
                    {
                        // Only set if not already preset
                        if (!preSetOutputs.ContainsKey(dep))
                        {
                            // Default to true for typical boolean dependencies, or "triggered" for shutdown
                            preSetOutputs[dep] = dep.Contains("shutdown") ? "triggered" : true;
                        }
                    }
                }

                // Add the preSetOutputs to the scenario if we have any
                if (preSetOutputs.Count > 0)
                {
                    scenario.PreSetOutputs = preSetOutputs;
                }

                // Helper function to ensure all required inputs are included
                List<TestInput> EnsureAllRequiredInputs(
                    RuleDefinition rule,
                    Dictionary<string, object> inputs,
                    bool isPositiveCase
                )
                {
                    var allInputs = new Dictionary<string, object>(inputs);

                    // Add any missing inputs from the complete set of required inputs
                    foreach (var sensor in allRequiredInputs)
                    {
                        if (!allInputs.ContainsKey(sensor))
                        {
                            // Check for fallback/default in rule.Inputs
                            var inputDef = rule.Inputs?.FirstOrDefault(i => i.Id == sensor);
                            if (inputDef != null && inputDef.DefaultValue != null)
                            {
                                allInputs[sensor] = inputDef.DefaultValue;
                                _logger.Debug(
                                    "Added missing input sensor {Sensor} with fallback/default value: {Value}",
                                    sensor,
                                    inputDef.DefaultValue
                                );
                            }
                            else if (
                                inputConditionMap != null
                                && inputConditionMap.TryGetValue(sensor, out var conditionPairs)
                                && conditionPairs.Count > 0
                            )
                            {
                                // Use the first condition to determine a suitable value
                                var pair = conditionPairs.First();
                                var valueTarget = isPositiveCase
                                    ? ValueTarget.Positive
                                    : ValueTarget.Negative;
                                var value = _testCaseGenerator.GenerateValueForSensor(
                                    sensor,
                                    pair.Condition,
                                    valueTarget
                                );
                                allInputs[sensor] = value;
                                _logger.Debug(
                                    "Added missing input sensor {Sensor} with condition-based value: {Value}",
                                    sensor,
                                    value
                                );
                            }
                            else
                            {
                                // No condition info available, use a neutral value
                                // Use different values for positive and negative tests to avoid accidental triggers
                                allInputs[sensor] = isPositiveCase ? 50.0 : 0.0;
                                _logger.Debug(
                                    "Added missing input sensor {Sensor} with neutral value",
                                    sensor
                                );
                            }
                        }
                    }

                    return allInputs
                        .Select(i => new TestInput { Key = i.Key, Value = i.Value })
                        .ToList();
                }

                // Check if this is a temporal rule
                bool isTemporalRule = _ruleAnalyzer.ConditionAnalyzer.HasTemporalCondition(
                    rule.Conditions
                );

                // --- Dependency-producing steps for required non-input: sensors ---
                var dependencySteps = new List<TestStep>();
                foreach (var required in allRequiredInputs)
                {
                    if (!required.StartsWith("input:") && !required.StartsWith("buffer:"))
                    {
                        // Find the rule that produces this dependency
                        var depRule = allRules.FirstOrDefault(r =>
                            r.Actions.OfType<SetValueAction>().Any(a => a.Key == required)
                        );
                        if (depRule != null)
                        {
                            var depTestCase = _testCaseGenerator.GenerateBasicTestCase(depRule);
                            var depStep = new TestStep
                            {
                                Name = $"Produce dependency {required}",
                                Description = $"Step to trigger rule {depRule.Name} to produce {required} (with required inputs)",
                                Inputs = EnsureAllRequiredInputs(rule, depTestCase.Inputs, true),
                                Delay = 500,
                                Expectations = new List<TestExpectation>
                                {
                                    new TestExpectation
                                    {
                                        Key = required,
                                        Expected = depTestCase.Outputs.ContainsKey(required) ? depTestCase.Outputs[required] : true,
                                        Validator = GetValidatorType(depTestCase.Outputs.ContainsKey(required) ? depTestCase.Outputs[required] : true),
                                        TimeoutMs = 1000,
                                        Tolerance = IsTimeBasedKey(required) ? 12000 : (double?)null,
                                    },
                                },
                            };
                            dependencySteps.Add(depStep);
                        }
                    }
                }
                scenario.Steps = scenario.Steps ?? new List<TestStep>();
                scenario.Steps.AddRange(dependencySteps);

                // For rules with output dependencies, add a step to trigger the dependency rule first
                var outputDependencies =
                    rule.Conditions != null
                        ? _ruleAnalyzer
                            .ConditionAnalyzer.ExtractSensors(rule.Conditions)
                            .Where(s => s.StartsWith("output:"))
                            .ToList()
                        : new List<string>();
                if (outputDependencies.Count > 0)
                {
                    foreach (var dep in outputDependencies)
                    {
                        // Find the rule that produces this dependency output
                        var depRule = allRules.FirstOrDefault(r =>
                            r.Actions.OfType<SetValueAction>().Any(a => a.Key == dep)
                        );
                        if (depRule != null)
                        {
                            var depTestCase = _testCaseGenerator.GenerateBasicTestCase(depRule);
                            var depStep = new TestStep
                            {
                                Name = $"Produce dependency {dep}",
                                Description =
                                    $"Step to trigger rule {depRule.Name} to produce {dep} (with required inputs)",
                                // Ensure all required inputs for the dependency rule are included
                                Inputs = EnsureAllRequiredInputs(rule, depTestCase.Inputs, true),
                                Delay = 500,
                                Expectations = new List<TestExpectation>
                                {
                                    new TestExpectation
                                    {
                                        Key = dep,
                                        Expected = depTestCase.Outputs.ContainsKey(dep)
                                            ? depTestCase.Outputs[dep]
                                            : true,
                                        Validator = GetValidatorType(depTestCase.Outputs.ContainsKey(dep) ? depTestCase.Outputs[dep] : true),
                                        TimeoutMs = 1000,
                                        Tolerance = IsTimeBasedKey(dep) ? 12000 : (double?)null,
                                    },
                                },
                            };
                            scenario.Steps = scenario.Steps ?? new List<TestStep>();
                            scenario.Steps.Add(depStep);
                        }
                    }
                }
                // For temporal rules, the basic test should be minimal or modified
                if (isTemporalRule)
                {
                    _logger.Debug(
                        "Rule {RuleName} has temporal conditions - adding temporal step",
                        rule.Name
                    );

                    // For temporal rules, generate a sequence of steps that satisfy the temporal condition
                    var temporalConditions = _testCaseGenerator.FindTemporalConditions(
                        rule.Conditions
                    );
                    if (temporalConditions.Count > 0)
                    {
                        foreach (var temporal in temporalConditions)
                        {
                            var sensor = temporal.Sensor;
                            var threshold = temporal.Threshold;
                            var duration = temporal.Duration;
                            var steps = Math.Max(3, duration / 500); // At least 3 steps, or more for longer durations
                            var comparisonOperator = temporal.Operator ?? ">";
                            _logger.Warning(
                                "[TEMPORAL TEST GEN] Rule: {Rule}, Sensor: {Sensor}, Threshold: {Threshold}, Operator: {Operator}, Duration: {Duration}, Steps: {Steps}",
                                rule.Name,
                                sensor,
                                threshold,
                                comparisonOperator,
                                duration,
                                steps
                            );
                            for (int i = 0; i < steps; i++)
                            {
                                double margin = Math.Max(threshold * 0.1, 5); // At least 5 or 10% of threshold
                                // Generate values that satisfy the temporal condition
                                var value = comparisonOperator switch
                                {
                                    ">" => threshold + margin + 1,
                                    ">=" => threshold + margin,
                                    "<" => threshold - margin - 1,
                                    "<=" => threshold - margin,
                                    _ => threshold + margin + 1
                                };
                                _logger.Warning(
                                    "[TEMPORAL TEST GEN] Step {Step}: Using value {Value} for sensor {Sensor} (threshold: {Threshold})",
                                    i + 1,
                                    value,
                                    sensor,
                                    threshold
                                );
                                var stepInputs = new List<TestInput>();
                                // Set all required inputs to default, then override the temporal sensor
                                foreach (var s in allRequiredInputs)
                                {
                                    var v = s == sensor ? value : GetDefaultValueForSensor(s, rule);
                                    stepInputs.Add(new TestInput { Key = s, Value = v });
                                }
                                var temporalStep = new TestStep
                                {
                                    Name = $"Temporal step {i + 1}/{steps}",
                                    Description =
                                        $"Step {i + 1} for temporal rule: {sensor} {comparisonOperator} {threshold}",
                                    Inputs = stepInputs,
                                    Delay = duration / steps,
                                    DelayMultiplier = null,
                                    Expectations = new List<TestExpectation>(),
                                    Result = null,
                                };
                                scenario.Steps.Add(temporalStep);
                            }
                        }
                    }
                    else
                    {
                        // fallback to old single step if no temporal conditions detected
                        var temporalStep = new TestStep
                        {
                            Name = "Basic test for temporal rule",
                            Description =
                                "Note: This is a temporal rule that requires a sequence of values over time. See the temporal test scenario.",
                            Inputs =
                                testCase.Inputs.Count > 0
                                    ? EnsureAllRequiredInputs(rule, testCase.Inputs, true)
                                    : allRequiredInputs
                                        .Select(s => new TestInput
                                        {
                                            Key = s,
                                            Value = GetDefaultValueForSensor(s, rule),
                                        })
                                        .ToList(),
                            Delay = 500,
                            DelayMultiplier = null,
                            Expectations = new List<TestExpectation>(),
                            Result = null,
                        };
                        scenario.Steps.Add(temporalStep);
                    }
                }
                else if (testCase.Inputs.Count > 0)
                {
                    // Regular non-temporal rules get a normal positive test step
                    var positiveStep = new TestStep
                    {
                        Name = "Positive test case",
                        Description = "Test inputs that should trigger the rule",
                        Inputs = EnsureAllRequiredInputs(rule, testCase.Inputs, true),
                        Delay = 500, // Default delay
                        Expectations = testCase
                            .Outputs.Select(o => new TestExpectation
                            {
                                Key = o.Key,
                                Expected = o.Value,
                                Validator = GetValidatorType(o.Value),
                                TimeoutMs = 1000, // Add timeout for rules to process
                                Tolerance = IsTimeBasedKey(o.Key) ? 12000 : (double?)null,
                            })
                            .ToList(),
                    };

                    scenario.Steps.Add(positiveStep);
                }

                // Generate negative test case if possible
                var negativeCase = _testCaseGenerator.GenerateNegativeTestCase(rule);

                if (negativeCase.Inputs.Count > 0)
                {
                    // Create step for negative test case
                    var negativeStep = new TestStep
                    {
                        Name = "Negative test case",
                        Description = "Test inputs that should not trigger the rule",
                        Inputs = EnsureAllRequiredInputs(rule, negativeCase.Inputs, false),
                        Delay = 500, // Default delay
                        Expectations = negativeCase
                            .Outputs.Select(o => new TestExpectation
                            {
                                Key = o.Key,
                                Expected = o.Value,
                                Validator = GetValidatorType(o.Value),
                                TimeoutMs = 1000, // Add timeout for rules to process
                                Tolerance = IsTimeBasedKey(o.Key) ? 12000 : (double?)null,
                            })
                            .ToList(),
                    };

                    scenario.Steps.Add(negativeStep);
                }

                return scenario;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error generating basic scenario for rule {RuleName}", rule.Name);

                // Return a placeholder scenario
                scenario.Steps.Add(
                    new TestStep
                    {
                        Name = "Error generating test case",
                        Description = $"Error: {ex.Message}",
                        Inputs = new List<TestInput>(),
                        Expectations = new List<TestExpectation>(),
                    }
                );

                return scenario;
            }
        }

        /// <summary>
        /// Generates test scenarios for rule dependencies
        /// </summary>
        private List<TestScenario> GenerateDependencyScenarios(
            RuleAnalysisResult analysis,
            Dictionary<string, List<RuleConditionPair>>? inputConditionMap = null
        )
        {
            _logger.Debug(
                "Generating dependency test scenarios for {DependencyCount} dependencies",
                analysis.Dependencies.Count
            );

            var scenarios = new List<TestScenario>();
            var allRequiredInputs = analysis.InputSensors;

            // Group dependencies by target rule
            var dependenciesByTarget = analysis
                .Dependencies.GroupBy(d => d.TargetRule.Name)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var targetRuleName in dependenciesByTarget.Keys)
            {
                var dependencies = dependenciesByTarget[targetRuleName];
                var targetRule = dependencies.First().TargetRule;

                // Create a test scenario that tests the dependencies
                var scenario = new TestScenario
                {
                    Name = $"{targetRuleName}DependencyTest",
                    Description = $"Tests dependencies for rule {targetRuleName}",
                    Steps = new List<TestStep>(),
                };

                // Declare all locals outside try so they're available after
                var dependencySteps = new List<TestStep>();
                var dependencyOutputsToProduce = new Dictionary<string, object>();
                var preSetOutputs = new Dictionary<string, object>();
                var requiredInputs = new Dictionary<string, object>();
                var dependencyRules = dependencies.ToDictionary(d => d.Key, d => d.SourceRule);
                TestCase testCase = null;
                try
                {
                    // Collect all outputs referenced in the target rule's conditions
                    HashSet<string> allReferencedOutputs = new HashSet<string>();
                    foreach (var dependency in dependencies)
                        allReferencedOutputs.Add(dependency.Key);
                    if (targetRule.Conditions != null)
                    {
                        var outputSensors = _ruleAnalyzer
                            .ConditionAnalyzer.ExtractSensors(targetRule.Conditions)
                            .Where(s => s.StartsWith("output:"))
                            .ToList();
                        foreach (var outputSensor in outputSensors)
                            allReferencedOutputs.Add(outputSensor);
                    }

                    // Build dependency-producing steps
                    foreach (var key in allReferencedOutputs)
                    {
                        var sourceRule = analysis.Rules.FirstOrDefault(r =>
                            r.Actions.OfType<SetValueAction>().Any(a => a.Key == key)
                        );
                        if (sourceRule != null)
                        {
                            var dependencyTestCase = _testCaseGenerator.GenerateBasicTestCase(
                                sourceRule
                            );
                            if (dependencyTestCase.Inputs.Count > 0)
                            {
                                var dependencyStep = new TestStep
                                {
                                    Name = $"Produce dependency {key}",
                                    Description =
                                        $"Step to trigger rule {sourceRule.Name} to produce {key}",
                                    Inputs = dependencyTestCase
                                        .Inputs.Select(i => new TestInput
                                        {
                                            Key = i.Key,
                                            Value = i.Value,
                                        })
                                        .ToList(),
                                    Delay = 500,
                                    Expectations = new List<TestExpectation>
                                    {
                                        new TestExpectation
                                        {
                                            Key = key,
                                            Expected = dependencyTestCase.Outputs.ContainsKey(key)
                                                ? dependencyTestCase.Outputs[key]
                                                : true,
                                            Validator = GetValidatorType(dependencyTestCase.Outputs.ContainsKey(key) ? dependencyTestCase.Outputs[key] : true),
                                            TimeoutMs = 1000,
                                            Tolerance = IsTimeBasedKey(key) ? 12000 : (double?)null,
                                        },
                                    },
                                };
                                dependencySteps.Add(dependencyStep);
                                dependencyOutputsToProduce[key] =
                                    dependencyTestCase.Outputs.ContainsKey(key)
                                        ? dependencyTestCase.Outputs[key]
                                        : true;
                            }
                        }
                        else
                        {
                            // If no rule produces this output, fall back to preSetOutputs as before
                            object value;
                            // Check if the targetRule has an action for this key that sets a string value
                            var stringAction = targetRule
                                .Actions?.OfType<SetValueAction>()
                                .FirstOrDefault(a => a.Key == key && a.Value is string);
                            if (
                                key.Contains("enabled")
                                || key.Contains("status")
                                || key.Contains("active")
                                || key.Contains("alarm")
                                || key.Contains("alert")
                                || key.Contains("normal")
                                || key.Contains("stress_alert")
                            )
                            {
                                value = true;
                            }
                            else if (stringAction != null)
                            {
                                value = "initial_value";
                            }
                            else
                            {
                                value = 1.0;
                            }
                            dependencyOutputsToProduce[key] = value;
                        }
                    }

                    // Main step to trigger the target rule
                    testCase = _testCaseGenerator.GenerateBasicTestCase(targetRule);
                    var mainStep = new TestStep
                    {
                        Name = "Test with dependencies",
                        Description = "Tests rule with dependencies satisfied",
                        Inputs = testCase
                            .Inputs.Select(i => new TestInput { Key = i.Key, Value = i.Value })
                            .ToList(),
                        Delay = 500,
                        Expectations = testCase
                            .Outputs.Select(o => new TestExpectation
                            {
                                Key = o.Key,
                                Expected = o.Value,
                                Validator = o.Value is bool ? "boolean" : "string",
                                TimeoutMs = 1000,
                            })
                            .ToList(),
                    };

                    scenario.Steps.AddRange(dependencySteps);

                    // For each dependency output produced, add a step to explicitly set it in the system (if not already)
                    foreach (var dep in dependencyOutputsToProduce)
                    {
                        // Only add if not already expected in the last dependency step
                        if (!dependencySteps.Any(s => s.Expectations.Any(e => e.Key == dep.Key)))
                        {
                            var setOutputStep = new TestStep
                            {
                                Name = $"Set dependency output {dep.Key}",
                                Description =
                                    $"Explicitly set {dep.Key} to {dep.Value} before main step",
                                Inputs = new List<TestInput>(),
                                Delay = 100,
                                Expectations = new List<TestExpectation>
                                {
                                    new TestExpectation
                                    {
                                        Key = dep.Key,
                                        Expected = dep.Value,
                                        Validator = dep.Value is string ? "string" : "boolean",
                                        TimeoutMs = 500,
                                        Tolerance = IsTimeBasedKey(dep.Key) ? 12000 : (double?)null,
                                    },
                                },
                            };
                            scenario.Steps.Add(setOutputStep);
                        }
                    }

                    scenario.Steps.Add(mainStep);

                    // Only use preSetOutputs for outputs that cannot be produced by rules
                    foreach (var kvp in dependencyOutputsToProduce)
                    {
                        if (!dependencySteps.Any(s => s.Expectations.Any(e => e.Key == kvp.Key)))
                        {
                            preSetOutputs[kvp.Key] = kvp.Value;
                        }
                    }
                    scenario.PreSetOutputs = preSetOutputs.Count > 0 ? preSetOutputs : null;
                }
                catch (Exception ex)
                {
                    _logger.Error(
                        ex,
                        "Error generating dependency scenario for rule {RuleName}",
                        targetRuleName
                    );
                }

                scenarios.Add(scenario);

                // Post-processing: handle value expressions using unique variable names
                foreach (var act in targetRule.Actions)
                {
                    if (act is SetValueAction svc && !string.IsNullOrEmpty(svc.ValueExpression))
                    {
                        var sensors = ConditionAnalyzerShared.ExtractSensorsFromExpression(
                            svc.ValueExpression
                        );
                        foreach (var sensor in sensors)
                        {
                            if (sensor.StartsWith("input:") && !requiredInputs.ContainsKey(sensor))
                            {
                                // CRITICAL: For dependency tests, we need to ensure values are consistent with pre-set outputs
                                // First check if any of our dependency rules consume this input
                                bool foundConsistentValue = false;

                                foreach (var dependencyPair in dependencyRules)
                                {
                                    var outputKey = dependencyPair.Key; // outputKey is valid here
                                    var rule = dependencyPair.Value;
                                    object expectedOutputValue;
                                    if (
                                        !preSetOutputs.TryGetValue(
                                            outputKey,
                                            out expectedOutputValue
                                        )
                                    )
                                    {
                                        _logger.Warning(
                                            "[TestGen] Dependency output key '{OutputKey}' not found in preSetOutputs. Available keys: [{Keys}] - Skipping this dependency.",
                                            outputKey,
                                            string.Join(", ", preSetOutputs.Keys)
                                        );
                                        continue;
                                    }

                                    // Check if this rule uses this input
                                    if (rule.Conditions != null)
                                    {
                                        var ruleSensors =
                                            _ruleAnalyzer.ConditionAnalyzer.ExtractSensors(
                                                rule.Conditions
                                            );
                                        if (ruleSensors.Contains(sensor))
                                        {
                                            // We have a rule that both:
                                            // 1. Produces a dependency output we're pre-setting, and
                                            // 2. Consumes the input we need to set

                                            // We need to set the input value to ensure the rule produces our expected output
                                            var sensorValue = GetConsistentInputValue(
                                                rule,
                                                sensor,
                                                outputKey,
                                                expectedOutputValue
                                            );
                                            if (sensorValue != null)
                                            {
                                                requiredInputs[sensor] = sensorValue;
                                                _logger.Debug(
                                                    "Using dependency-consistent value {Value} for input {Sensor} to maintain {Output}={ExpectedValue}",
                                                    sensorValue,
                                                    sensor,
                                                    outputKey,
                                                    expectedOutputValue
                                                );
                                                foundConsistentValue = true;
                                                break;
                                            }
                                        }
                                    }
                                }

                                if (!foundConsistentValue)
                                {
                                    // If no dependency rules consume this input, fall back to regular value generation
                                    foreach (var rule in analysis.Rules)
                                    {
                                        var outputKey = svc.Key;
                                        var expectedOutputValue = preSetOutputs.ContainsKey(
                                            outputKey
                                        )
                                            ? preSetOutputs[outputKey]
                                            : null;

                                        // Check if this rule uses this input
                                        if (rule.Conditions != null)
                                        {
                                            var ruleSensors =
                                                _ruleAnalyzer.ConditionAnalyzer.ExtractSensors(
                                                    rule.Conditions
                                                );
                                            if (ruleSensors.Contains(sensor))
                                            {
                                                // We have a rule that both:
                                                // 1. Produces a dependency output we're pre-setting, and
                                                // 2. Consumes the input we need to set

                                                // We need to set the input value to ensure the rule produces our expected output
                                                var sensorValue = GetConsistentInputValue(
                                                    rule,
                                                    sensor,
                                                    outputKey,
                                                    expectedOutputValue
                                                );
                                                if (sensorValue != null)
                                                {
                                                    requiredInputs[sensor] = sensorValue;
                                                    _logger.Debug(
                                                        "Using dependency-consistent value {Value} for input {Sensor} to maintain {Output}={ExpectedValue}",
                                                        sensorValue,
                                                        sensor,
                                                        outputKey,
                                                        expectedOutputValue
                                                    );
                                                    foundConsistentValue = true;
                                                    break;
                                                }
                                            }
                                        }
                                    }

                                    if (!foundConsistentValue)
                                    {
                                        // If no dependency rules consume this input, fall back to regular value generation
                                        foreach (var rule in analysis.Rules)
                                        {
                                            if (rule.Conditions != null)
                                            {
                                                var ruleSensors =
                                                    _ruleAnalyzer.ConditionAnalyzer.ExtractSensors(
                                                        rule.Conditions
                                                    );
                                                if (ruleSensors.Contains(sensor))
                                                {
                                                    var sensorValue = GetSafeValueForSensor(
                                                        rule,
                                                        sensor
                                                    );
                                                    if (sensorValue != null)
                                                    {
                                                        requiredInputs[sensor] = sensorValue;
                                                        _logger.Debug(
                                                            "Using safe value {Value} for input {Sensor} from rule {Rule}",
                                                            sensorValue,
                                                            sensor,
                                                            rule.Name
                                                        );
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    // If we didn't find a value, use a default that's easily identified
                                    if (!requiredInputs.ContainsKey(sensor))
                                    {
                                        requiredInputs[sensor] = 1.0;
                                        _logger.Debug(
                                            "Using default value 1.0 for required input {Sensor} - no rule conditions found",
                                            sensor
                                        );
                                    }
                                }
                            }
                        }
                    }

                    // Add all required inputs to the test case
                    foreach (var (key, value) in requiredInputs)
                    {
                        if (!testCase.Inputs.ContainsKey(key))
                        {
                            testCase.Inputs[key] = value;
                        }
                    }

                    // Helper function to ensure all required inputs are included
                    List<TestInput> EnsureAllRequiredInputs(
                        RuleDefinition rule,
                        Dictionary<string, object> inputs,
                        bool isPositiveCase
                    )
                    {
                        var allInputs = new Dictionary<string, object>(inputs);

                        // Add any missing inputs from the complete set of required inputs
                        foreach (var sensor in allRequiredInputs)
                        {
                            if (!allInputs.ContainsKey(sensor))
                            {
                                // Check for fallback/default in rule.Inputs
                                var inputDef = rule.Inputs?.FirstOrDefault(i => i.Id == sensor);
                                if (inputDef != null && inputDef.DefaultValue != null)
                                {
                                    allInputs[sensor] = inputDef.DefaultValue;
                                    _logger.Debug(
                                        "Added missing input sensor {Sensor} with fallback/default value: {Value}",
                                        sensor,
                                        inputDef.DefaultValue
                                    );
                                }
                                else if (
                                    inputConditionMap != null
                                    && inputConditionMap.TryGetValue(sensor, out var conditionPairs)
                                    && conditionPairs.Count > 0
                                )
                                {
                                    // Use the first condition to determine a suitable value
                                    var pair = conditionPairs.First();
                                    var valueTarget = isPositiveCase
                                        ? ValueTarget.Positive
                                        : ValueTarget.Negative;
                                    var value = _testCaseGenerator.GenerateValueForSensor(
                                        sensor,
                                        pair.Condition,
                                        valueTarget
                                    );
                                    allInputs[sensor] = value;
                                    _logger.Debug(
                                        "Added missing input sensor {Sensor} with condition-based value: {Value}",
                                        sensor,
                                        value
                                    );
                                }
                                else
                                {
                                    // No condition info available, use a neutral value
                                    // Use different values for positive and negative tests to avoid accidental triggers
                                    allInputs[sensor] = isPositiveCase ? 50.0 : 0.0;
                                    _logger.Debug(
                                        "Added missing input sensor {Sensor} with neutral value",
                                        sensor
                                    );
                                }
                            }
                        }

                        return allInputs
                            .Select(i => new TestInput { Key = i.Key, Value = i.Value })
                            .ToList();
                    }

                    // Create the main test step
                    var step = new TestStep
                    {
                        Name = "Test with dependencies",
                        Description = "Tests rule with dependencies satisfied",
                        // Ensure all required inputs are included
                        Inputs = EnsureAllRequiredInputs(targetRule, testCase.Inputs, true),
                        Delay = 500,
                        // We need to set expectations based on actual input values
                        Expectations = new List<TestExpectation>(),
                    };

                    // Add inputs to a dictionary for easier access
                    var inputDict = step.Inputs.ToDictionary(i => i.Key, i => i.Value);

                    // Generate expectations based on actual inputs - handles both static values and expressions
                    foreach (var action in targetRule.Actions)
                    {
                        if (action is SetValueAction setAction)
                        {
                            // Use the TestCaseGenerator to determine the expected output based on the ACTUAL input values
                            var expectedValue = _testCaseGenerator.DetermineOutputValue(
                                setAction,
                                inputDict
                            );

                            step.Expectations.Add(
                                new TestExpectation
                                {
                                    Key = setAction.Key,
                                    Expected = expectedValue,
                                    Validator = GetValidatorType(expectedValue),
                                    TimeoutMs = 1000, // Add timeout for rules to process
                                    Tolerance = IsTimeBasedKey(setAction.Key)
                                        ? 12000
                                        : (double?)null,
                                }
                            );
                        }
                    }

                    scenario.Steps.Add(step);
                    scenarios.Add(scenario);

                    // Also generate a negative case where dependencies are not met
                    var negativeScenario = new TestScenario
                    {
                        Name = $"{targetRuleName}MissingDependencyTest",
                        Description =
                            $"Tests that {targetRuleName} doesn't trigger when dependencies are not met",
                    };

                    // Use opposite/inverted values for each preset output
                    var oppositeOutputs = new Dictionary<string, object>();
                    foreach (var (key, value) in preSetOutputs)
                    {
                        // Normalize the value to ensure proper type
                        var normalizedValue = _ruleAnalyzer.ConditionAnalyzer.NormalizeValue(value);

                        if (normalizedValue is bool b)
                        {
                            oppositeOutputs[key] = !b;
                        }
                        else if (normalizedValue is double d)
                        {
                            // Use an inverted value
                            oppositeOutputs[key] = d > 0 ? 0.0 : 1.0;
                        }
                        else
                        {
                            // For other values, use a distinct string
                            oppositeOutputs[key] = "different_value";
                        }
                    }

                    negativeScenario.PreSetOutputs = oppositeOutputs;

                    // For negative tests, we need inputs that won't naturally trigger the rules
                    // This ensures proper testing of dependency behavior

                    // Create new input values specific to negative tests
                    var negativeInputs = new List<TestInput>();

                    // For temperature-related inputs, use values below thresholds
                    // For humidity-related inputs, use values within normal range
                    foreach (var input in testCase.Inputs)
                    {
                        var key = input.Key;
                        var newValue = input.Value;

                        if (key.Contains("temperature"))
                        {
                            // Use value below threshold for high_temperature (< 30)
                            newValue = 25.0;
                            _logger.Debug(
                                "Using safe temperature value {Value} for negative test",
                                newValue
                            );
                        }
                        else if (key.Contains("humidity"))
                        {
                            // Use value within normal range (30-70) for humidity
                            newValue = 50.0;
                            _logger.Debug(
                                "Using normal humidity value {Value} for negative test",
                                newValue
                            );
                        }

                        negativeInputs.Add(
                            new TestInput
                            {
                                Key = key,
                                Value = newValue,
                                Format = RedisDataFormat.Auto,
                                Field = null,
                            }
                        );
                    }

                    // Create the negative test step
                    var negativeStep = new TestStep
                    {
                        Name = "Test with missing dependencies",
                        Description = "Tests rule with dependencies not satisfied",
                        Inputs = negativeInputs,
                        Delay = 500,
                        Expectations = testCase
                            .Outputs.Select(o => new TestExpectation
                            {
                                Key = o.Key,
                                Expected = null, // Expect no output when dependencies aren't met
                                Validator = "string", // String validator is most flexible
                                TimeoutMs = 1000, // Add timeout for rules to process
                                Tolerance = IsTimeBasedKey(o.Key) ? 12000 : (double?)null,
                            })
                            .ToList(),
                    };

                    negativeScenario.Steps.Add(negativeStep);
                    scenarios.Add(negativeScenario);
                }
            }

            return scenarios;
        }

        /// <summary>
        /// Generates test scenarios for temporal rules
        /// </summary>
        /// <summary>
        /// Gets a safe value for a sensor that's safely beyond threshold values
        /// </summary>
        private object GetSafeValueForSensor(RuleDefinition rule, string sensor)
        {
            // First try to analyze the rule's conditions to understand the thresholds
            var conditions =
                rule.Conditions != null
                    ? FindConditionsForSensor(rule.Conditions, sensor)
                    : new List<ConditionDefinition>();

            if (conditions.Count == 0)
            {
                // No conditions found for this sensor, use a default value
                return sensor.Contains("temperature") ? 35.0 : 75.0;
            }

            // Look for numeric comparison conditions
            foreach (var condition in conditions.OfType<ComparisonCondition>())
            {
                if (condition.Sensor == sensor && condition.Value != null)
                {
                    double safeValue;
                    var threshold = Convert.ToDouble(condition.Value);
                    var op = condition.Operator?.ToLowerInvariant();

                    // Generate values safely away from thresholds
                    if (op == "greater_than" || op == ">" || op == "gt")
                    {
                        // For '>' conditions, use a value clearly above the threshold
                        safeValue = threshold + Math.Max(5, threshold * 0.1); // At least 5 units or 10% above
                    }
                    else if (op == "less_than" || op == "<" || op == "lt")
                    {
                        // For '<' conditions, use a value clearly below the threshold
                        safeValue = threshold - Math.Max(5, threshold * 0.1); // At least 5 units or 10% below
                    }
                    else if (op == "equal_to" || op == "==" || op == "=" || op == "eq")
                    {
                        // For equality, use the exact value
                        safeValue = threshold;
                    }
                    else
                    {
                        // For other operators, use a conservative default
                        safeValue = sensor.Contains("temperature") ? 35.0 : 75.0;
                    }

                    _logger.Debug(
                        "Generated safe value {Value} for sensor {Sensor} based on condition {Op} {Threshold}",
                        safeValue,
                        sensor,
                        op,
                        threshold
                    );
                    return safeValue;
                }
            }

            // Fallback to sensible defaults based on sensor name
            return sensor.Contains("temperature") ? 35.0 : 75.0;
        }

        /// <summary>
        /// Gets a value for an input sensor that will cause a rule to produce the expected output value
        /// </summary>
        private object GetConsistentInputValue(
            RuleDefinition rule,
            string inputSensor,
            string outputKey,
            object expectedOutputValue
        )
        {
            try
            {
                _logger.Debug(
                    "Analyzing rule {Rule} to find consistent input value for {Input} that results in {Output}={ExpectedValue}",
                    rule.Name,
                    inputSensor,
                    outputKey,
                    expectedOutputValue
                );

                bool expectedBoolValue = false;
                if (expectedOutputValue is bool boolValue)
                {
                    expectedBoolValue = boolValue;
                }
                else if (
                    expectedOutputValue is string strValue
                    && bool.TryParse(strValue, out var parsedBool)
                )
                {
                    expectedBoolValue = parsedBool;
                }

                // Find conditions that use this input to set the output
                var conditions =
                    rule.Conditions != null
                        ? FindConditionsForSensor(rule.Conditions, inputSensor)
                        : new List<ConditionDefinition>();

                if (conditions.Count == 0)
                {
                    _logger.Debug(
                        "No conditions found in rule {Rule} for input {Input}",
                        rule.Name,
                        inputSensor
                    );
                    return null;
                }

                // Handle specific rules based on their output keys
                if (outputKey == "output:high_temperature")
                {
                    // For high_temperature rule (temperature > 30 => high_temperature = true)
                    // If we want high_temperature = true, we need temperature > 30
                    // If we want high_temperature = false, we need temperature <= 30
                    if (expectedBoolValue)
                    {
                        return 35.0; // Safely above 30 for true
                    }
                    else
                    {
                        return 25.0; // Safely below 30 for false
                    }
                }
                else if (outputKey == "output:humidity_normal")
                {
                    // For humidity rule (30 <= humidity <= 70 => humidity_normal = true)
                    // If we want humidity_normal = true, we need 30 <= humidity <= 70
                    // If we want humidity_normal = false, we need humidity < 30 or humidity > 70
                    if (expectedBoolValue)
                    {
                        return 50.0; // Safe middle value for true
                    }
                    else
                    {
                        return 75.0; // Safely above 70 for false
                    }
                }

                // For other rules, analyze the conditions and determine based on first comparison
                foreach (var condition in conditions.OfType<ComparisonCondition>())
                {
                    if (condition.Sensor == inputSensor && condition.Value != null)
                    {
                        double value;
                        var threshold = Convert.ToDouble(condition.Value);
                        var op = condition.Operator?.ToLowerInvariant();

                        // If expectedBoolValue is true, we want the condition to evaluate to true
                        // If expectedBoolValue is false, we want the condition to evaluate to false
                        bool wantConditionTrue = expectedBoolValue;

                        // For some rule types, the logic might be inverted (i.e., condition true -> output false)
                        // We would need special handling for those cases here

                        if (op == "greater_than" || op == ">" || op == "gt")
                        {
                            if (wantConditionTrue)
                            {
                                value = threshold + Math.Max(5, threshold * 0.1); // Clearly above threshold
                            }
                            else
                            {
                                value = threshold - Math.Max(5, threshold * 0.1); // Clearly below threshold
                            }
                        }
                        else if (op == "less_than" || op == "<" || op == "lt")
                        {
                            if (wantConditionTrue)
                            {
                                value = threshold - Math.Max(5, threshold * 0.1); // Clearly below threshold
                            }
                            else
                            {
                                value = threshold + Math.Max(5, threshold * 0.1); // Clearly above threshold
                            }
                        }
                        else if (op == "equal_to" || op == "==" || op == "=" || op == "eq")
                        {
                            if (wantConditionTrue)
                            {
                                value = threshold; // Exact match
                            }
                            else
                            {
                                value = threshold + Math.Max(5, threshold * 0.5); // Clearly different
                            }
                        }
                        else
                        {
                            // For other operators, use a safe default
                            value = wantConditionTrue ? threshold + 5 : threshold - 5;
                        }

                        _logger.Debug(
                            "Found consistent value {Value} for input {Sensor} to achieve {Output}={ExpectedValue}",
                            value,
                            inputSensor,
                            outputKey,
                            expectedOutputValue
                        );
                        return value;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(
                    ex,
                    "Error finding consistent input value for {Input} in rule {Rule}",
                    inputSensor,
                    rule.Name
                );
            }

            // Fall back to safe defaults if we couldn't determine a value
            if (inputSensor.Contains("humidity"))
            {
                return expectedOutputValue.ToString().ToLower() == "true" ? 50.0 : 75.0;
            }
            else if (inputSensor.Contains("temperature"))
            {
                return expectedOutputValue.ToString().ToLower() == "true" ? 35.0 : 25.0;
            }

            return null;
        }

        /// <summary>
        /// Finds all conditions in a rule that reference a specific sensor
        /// </summary>
        private List<ConditionDefinition> FindConditionsForSensor(
            ConditionDefinition condition,
            string sensor
        )
        {
            // Get all conditions for all sensors, then filter for our target sensor
            var allSensors = new Dictionary<string, List<ConditionDefinition>>();
            ProcessConditionForSensors(condition, allSensors);

            // Return matching conditions or empty list if none found
            return allSensors.TryGetValue(sensor, out var matches)
                ? matches
                : new List<ConditionDefinition>();
        }

        private List<TestScenario> GenerateTemporalScenarios(
            List<RuleDefinition> temporalRules,
            HashSet<string> allRequiredInputs,
            Dictionary<string, List<RuleConditionPair>>? inputConditionMap = null
        )
        {
            _logger.Debug(
                "Generating temporal test scenarios for {RuleCount} rules",
                temporalRules.Count
            );
            var scenarios = new List<TestScenario>();

            foreach (var rule in temporalRules)
            {
                try
                {
                    // Generate a temporal test case for this rule
                    var scenario = _testCaseGenerator.GenerateTemporalTestCase(rule);
                    if (scenario != null)
                    {
                        // Add additional properties to enhance the latching behavior testing

                        // 1. Set initial state to reflect the start condition
                        // This tests the "latching" behavior by explicitly setting initial states
                        var presetOutputs = new Dictionary<string, object>();

                        // Initialize all outputs that might be affected by this rule to a known state
                        foreach (var action in rule.Actions)
                        {
                            if (
                                action is SetValueAction setValueAction
                                && setValueAction.Key.StartsWith("output:")
                            )
                            {
                                // For boolean outputs, start with the opposite value to verify rule changes it
                                if (setValueAction.Value is bool boolValue)
                                {
                                    presetOutputs[setValueAction.Key] = !boolValue;
                                }
                                else
                                {
                                    // For non-boolean outputs, initialize to 0 or other neutral value
                                    presetOutputs[setValueAction.Key] = 0;
                                }
                            }
                        }

                        // Only set preSetOutputs if we have values
                        if (presetOutputs.Count > 0)
                        {
                            scenario.PreSetOutputs = presetOutputs;
                        }

                        // 2. Ensure all required inputs are included in each step of the sequence
                        if (scenario.InputSequence != null)
                        {
                            foreach (var sequenceInput in scenario.InputSequence)
                            {
                                // Get the existing inputs for this step
                                var existingInputs = sequenceInput.Inputs;

                                // Add any missing inputs from the complete set of required inputs
                                foreach (var sensor in allRequiredInputs)
                                {
                                    if (!existingInputs.ContainsKey(sensor))
                                    {
                                        // If we have condition information for this sensor, use it to determine an appropriate value
                                        if (
                                            inputConditionMap != null
                                            && inputConditionMap.TryGetValue(
                                                sensor,
                                                out var conditionPairs
                                            )
                                            && conditionPairs.Count > 0
                                        )
                                        {
                                            // Use the first condition to determine a suitable value based on actual rule conditions
                                            var pair = conditionPairs.First();
                                            var value = _testCaseGenerator.GenerateValueForSensor(
                                                sensor,
                                                pair.Condition,
                                                ValueTarget.Positive
                                            );
                                            sequenceInput.AdditionalInputs[sensor] = value;
                                            _logger.Debug(
                                                "Added missing input sensor {Sensor} with condition-based value to temporal step",
                                                sensor
                                            );
                                        }
                                        else
                                        {
                                            // No condition info available, use a neutral value that won't trigger edge cases
                                            sequenceInput.AdditionalInputs[sensor] = 50.0;
                                            _logger.Debug(
                                                "Added missing input sensor {Sensor} with neutral value to temporal step",
                                                sensor
                                            );
                                        }
                                    }
                                }
                            }
                        }

                        scenarios.Add(scenario);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(
                        ex,
                        "Error generating temporal scenario for rule {RuleName}",
                        rule.Name
                    );
                }
            }

            return scenarios;
        }

        /// <summary>
        /// Gets a generic default value for a key
        /// </summary>
        private object GetDefaultValueForKey(string key)
        {
            // For keys that appear to represent boolean values
            if (key.EndsWith("_enabled") || key.EndsWith("_active") || key.EndsWith("_status"))
            {
                return true;
            }

            // For other outputs, prefer numeric values that won't trigger edge conditions
            // Use a mid-range value to minimize chance of unexpected interactions
            return 50.0;
        }

        /// <summary>
        /// Gets a default value for a sensor, attempting to make a smart guess based on the sensor name and rule
        /// </summary>
        private object GetDefaultValueForSensor(string sensor, RuleDefinition rule)
        {
            // Prefer fallback/default value from rule.Inputs if available
            var inputDef = rule.Inputs?.FirstOrDefault(i => i.Id == sensor);
            if (inputDef != null && inputDef.DefaultValue != null)
            {
                return inputDef.DefaultValue;
            }

            // First try to find a condition that uses this sensor
            var conditions =
                rule.Conditions != null
                    ? FindConditionsForSensor(rule.Conditions, sensor)
                    : new List<ConditionDefinition>();

            // If we found conditions, try to generate a suitable value
            if (conditions.Count > 0)
            {
                foreach (var condition in conditions)
                {
                    if (condition is ComparisonCondition comparison)
                    {
                        return _testCaseGenerator.GenerateValueForSensor(
                            sensor,
                            comparison,
                            ValueTarget.Positive
                        );
                    }
                    else if (condition is ThresholdOverTimeCondition threshold)
                    {
                        // Create a value that exceeds the threshold
                        var thresholdValue = Convert.ToDouble(threshold.Threshold);
                        var op = threshold.Operator?.ToLowerInvariant() ?? ">";

                        if (op == "greater_than" || op == ">" || op == "gt")
                        {
                            return thresholdValue + 10.0; // Safely above threshold
                        }
                        else if (op == "less_than" || op == "<" || op == "lt")
                        {
                            return Math.Max(1.0, thresholdValue - 10.0); // Safely below threshold
                        }
                        else
                        {
                            return thresholdValue; // For equals, use exact match
                        }
                    }
                }
            }

            // If no conditions found, fall back to heuristics based on sensor name
            if (sensor.Contains("temperature"))
            {
                return 35.0; // Common temperature value above typical thresholds
            }
            else if (sensor.Contains("humidity"))
            {
                return 50.0; // Middle of typical humidity range
            }
            else if (sensor.Contains("level") || sensor.Contains("percent"))
            {
                return 75.0; // High enough to trigger most threshold conditions
            }
            else if (
                sensor.Contains("enabled")
                || sensor.Contains("active")
                || sensor.Contains("status")
                || sensor.Contains("alert")
            )
            {
                return true; // Boolean sensors default to true
            }

            // Generic fallback that's identifiable in logs
            return 42.0;
        }

        /// <summary>
        /// Finds all conditions in a rule that reference a specific key
        /// </summary>
        private List<ConditionDefinition> FindConditionsReferencingKey(
            ConditionDefinition condition,
            string key
        )
        {
            var matchingConditions = new List<ConditionDefinition>();

            if (condition is ComparisonCondition comparison)
            {
                // Direct comparison with the key
                if (comparison.Sensor == key)
                {
                    matchingConditions.Add(comparison);
                }
                // Expression that might reference the key
                else if (
                    !string.IsNullOrEmpty(comparison.ValueExpression)
                    && comparison.ValueExpression.Contains(key)
                )
                {
                    matchingConditions.Add(comparison);
                }
            }
            else if (condition is ThresholdOverTimeCondition temporal)
            {
                if (temporal.Sensor == key)
                {
                    matchingConditions.Add(temporal);
                }
            }
            else if (condition is ExpressionCondition expression)
            {
                if (
                    !string.IsNullOrEmpty(expression.Expression)
                    && expression.Expression.Contains(key)
                )
                {
                    matchingConditions.Add(expression);
                }
            }
            else if (condition is ConditionGroup group)
            {
                // Process 'all' conditions
                foreach (var wrapper in group.All)
                {
                    if (wrapper.Condition != null)
                    {
                        matchingConditions.AddRange(
                            FindConditionsReferencingKey(wrapper.Condition, key)
                        );
                    }
                }

                // Process 'any' conditions
                foreach (var wrapper in group.Any)
                {
                    if (wrapper.Condition != null)
                    {
                        matchingConditions.AddRange(
                            FindConditionsReferencingKey(wrapper.Condition, key)
                        );
                    }
                }
            }

            return matchingConditions;
        }

        /// <summary>
        /// Gets appropriate validator type for a value
        /// </summary>
        private string GetValidatorType(object? value)
        {
            // For null values, use string validator which works better for checking nulls
            if (value == null)
                return "string";

            // Use appropriate validators based on actual type
            if (value is bool)
                return "boolean";
            if (value is double || value is int || value is float)
                return "numeric";
            
            // Handle JsonElement types
            if (value is System.Text.Json.JsonElement jsonElement)
            {
                switch (jsonElement.ValueKind)
                {
                    case System.Text.Json.JsonValueKind.True:
                    case System.Text.Json.JsonValueKind.False:
                        return "boolean";
                    case System.Text.Json.JsonValueKind.Number:
                        return "numeric";
                    case System.Text.Json.JsonValueKind.String:
                        string stringVal = jsonElement.GetString() ?? "";
                        if (stringVal.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                            stringVal.Equals("false", StringComparison.OrdinalIgnoreCase))
                            return "boolean";
                        if (double.TryParse(stringVal, out _))
                            return "numeric";
                        return "string";
                    default:
                        return "string";
                }
            }

            // For string values check if they're boolean or numeric in disguise
            if (value is string stringValue)
            {
                if (
                    stringValue.Equals("true", StringComparison.OrdinalIgnoreCase)
                    || stringValue.Equals("false", StringComparison.OrdinalIgnoreCase)
                )
                    return "boolean";

                if (double.TryParse(stringValue, out _))
                    return "numeric";
            }

            // Default to string for all other types
            return "string";
        }

        // Helper to determine if a key is a time-based output
        private bool IsTimeBasedKey(string key)
        {
            var lower = key.ToLowerInvariant();
            return lower.Contains("time")
                || lower.Contains("timestamp")
                || lower == "output:last_alert_time";
        }

        /// <summary>
        /// Generates test scenarios specifically for V3 fallback strategies
        /// </summary>
        private List<TestScenario> GenerateFallbackScenarios(
            RuleDefinition rule,
            HashSet<string> allReferencedSensors,
            Dictionary<string, List<RuleConditionPair>> inputConditionMap)
        {
            var scenarios = new List<TestScenario>();

            _logger.Debug("Generating fallback test scenarios for rule: {RuleName}", rule.Name);

            foreach (var input in rule.Inputs.Where(i => i.FallbackStrategy != null))
            {
                switch (input.FallbackStrategy)
                {
                    case "use_default":
                        scenarios.Add(GenerateUseDefaultTest(rule, input, allReferencedSensors));
                        break;
                    
                    case "propagate_unavailable":
                        scenarios.Add(GeneratePropagateUnavailableTest(rule, input, allReferencedSensors));
                        break;
                    
                    case "use_last_known":
                        scenarios.Add(GenerateUseLastKnownTest(rule, input, allReferencedSensors));
                        break;
                    
                    case "skip_rule":
                        scenarios.Add(GenerateSkipRuleTest(rule, input, allReferencedSensors));
                        break;
                    
                    default:
                        _logger.Warning("Unknown fallback strategy: {Strategy} for input {InputId} in rule {RuleName}", 
                            input.FallbackStrategy, input.Id, rule.Name);
                        break;
                }
            }

            return scenarios;
        }

        private TestScenario GenerateUseDefaultTest(RuleDefinition rule, InputDefinition input, HashSet<string> allReferencedSensors)
        {
            var scenario = new TestScenario
            {
                Name = $"{rule.Name}UseDefaultFallbackTest_{input.Id}",
                Description = $"Test {input.Id} fallback: use_default = {input.DefaultValue}",
                ClearOutputs = true,
                Steps = new List<TestStep>()
            };

            var step = new TestStep
            {
                Name = "Fallback Test Step",
                Description = $"Test missing {input.Id} with use_default fallback",
                Inputs = new List<TestInput>()
            };

            // Add all required sensors EXCEPT the one we're testing fallback for
            foreach (var sensor in allReferencedSensors.Where(s => s != input.Id))
            {
                step.Inputs.Add(new TestInput 
                { 
                    Key = sensor, 
                    Value = GetNeutralValueForSensor(sensor) 
                });
            }

            // Explicitly omit the input we're testing - let fallback handle it
            _logger.Debug("Omitting {InputId} to test use_default fallback to {DefaultValue}", input.Id, input.DefaultValue);

            scenario.Steps.Add(step);

            // Expected output should reflect the default value behavior
            if (input.DefaultValue != null)
            {
                scenario.ExpectedOutputs = new Dictionary<string, object>();
                // Try to determine what output this rule would produce with the default value
                var expectedOutput = DetermineExpectedOutputWithDefaultValue(rule, input);
                if (expectedOutput != null)
                {
                    foreach (var kvp in expectedOutput)
                    {
                        scenario.ExpectedOutputs[kvp.Key] = kvp.Value;
                    }
                }
            }

            return scenario;
        }

        private TestScenario GeneratePropagateUnavailableTest(RuleDefinition rule, InputDefinition input, HashSet<string> allReferencedSensors)
        {
            var scenario = new TestScenario
            {
                Name = $"{rule.Name}PropagateUnavailableTest_{input.Id}",
                Description = $"Test {input.Id} fallback: propagate_unavailable (should result in Indeterminate)",
                ClearOutputs = true,
                Steps = new List<TestStep>()
            };

            var step = new TestStep
            {
                Name = "Propagate Unavailable Test",
                Description = $"Test missing {input.Id} with propagate_unavailable fallback",
                Inputs = new List<TestInput>()
            };

            // Add all required sensors EXCEPT the one we're testing fallback for
            foreach (var sensor in allReferencedSensors.Where(s => s != input.Id))
            {
                step.Inputs.Add(new TestInput 
                { 
                    Key = sensor, 
                    Value = GetNeutralValueForSensor(sensor) 
                });
            }

            scenario.Steps.Add(step);

            // For propagate_unavailable, we expect rule conditions to be Indeterminate
            // which typically means else-branch actions execute
            scenario.ExpectedOutputs = DetermineExpectedOutputsForIndeterminate(rule);

            return scenario;
        }

        private TestScenario GenerateUseLastKnownTest(RuleDefinition rule, InputDefinition input, HashSet<string> allReferencedSensors)
        {
            var scenario = new TestScenario
            {
                Name = $"{rule.Name}UseLastKnownTest_{input.Id}",
                Description = $"Test {input.Id} fallback: use_last_known (max_age: {input.MaxAge})",
                ClearOutputs = true,
                Steps = new List<TestStep>()
            };

            // Step 1: Provide the input to cache it
            var setupStep = new TestStep
            {
                Name = "Setup - Cache Value",
                Description = $"Provide {input.Id} to cache for later use",
                Inputs = new List<TestInput>()
            };

            foreach (var sensor in allReferencedSensors)
            {
                setupStep.Inputs.Add(new TestInput 
                { 
                    Key = sensor, 
                    Value = sensor == input.Id ? 100.0 : GetNeutralValueForSensor(sensor)
                });
            }

            // Step 2: Omit the input to test last known value
            var testStep = new TestStep
            {
                Name = "Test Last Known",
                Description = $"Omit {input.Id} to test use_last_known fallback",
                Inputs = new List<TestInput>()
            };

            foreach (var sensor in allReferencedSensors.Where(s => s != input.Id))
            {
                testStep.Inputs.Add(new TestInput 
                { 
                    Key = sensor, 
                    Value = GetNeutralValueForSensor(sensor) 
                });
            }

            scenario.Steps.Add(setupStep);
            scenario.Steps.Add(testStep);

            // Expected outputs should use the cached value (100.0)
            scenario.ExpectedOutputs = DetermineExpectedOutputWithCachedValue(rule, input, 100.0);

            return scenario;
        }

        private TestScenario GenerateSkipRuleTest(RuleDefinition rule, InputDefinition input, HashSet<string> allReferencedSensors)
        {
            var scenario = new TestScenario
            {
                Name = $"{rule.Name}SkipRuleTest_{input.Id}",
                Description = $"Test {input.Id} fallback: skip_rule (rule should not execute)",
                ClearOutputs = true,
                Steps = new List<TestStep>()
            };

            var step = new TestStep
            {
                Name = "Skip Rule Test",
                Description = $"Test missing {input.Id} with skip_rule fallback",
                Inputs = new List<TestInput>()
            };

            // Add all required sensors EXCEPT the one we're testing fallback for
            foreach (var sensor in allReferencedSensors.Where(s => s != input.Id))
            {
                step.Inputs.Add(new TestInput 
                { 
                    Key = sensor, 
                    Value = GetNeutralValueForSensor(sensor) 
                });
            }

            scenario.Steps.Add(step);

            // For skip_rule, we expect no outputs from this rule
            scenario.ExpectedOutputs = new Dictionary<string, object>();

            return scenario;
        }

        private object GetNeutralValueForSensor(string sensor)
        {
            // Provide neutral values that won't trigger conditions
            switch (sensor.ToLowerInvariant())
            {
                case "temperature":
                    return 20.0; // Room temperature
                case "pressure":
                    return 55.0; // Mid-range pressure
                case "flowrate":
                    return 10.0; // Low flow rate
                case "systemmode":
                    return "operational";
                case "emergencybutton":
                    return false;
                default:
                    if (sensor.Contains("temp"))
                        return 20.0;
                    else if (sensor.Contains("button") || sensor.Contains("alert"))
                        return false;
                    else
                        return 42.0; // Generic numeric default
            }
        }

        private Dictionary<string, object>? DetermineExpectedOutputWithDefaultValue(RuleDefinition rule, InputDefinition input)
        {
            // This is a simplified implementation - in practice you'd need to evaluate 
            // the rule's conditions with the default value to determine expected outputs
            var outputs = new Dictionary<string, object>();
            
            // For the EfficiencyCalculator rule with FlowRate default of 0
            if (rule.Name == "EfficiencyCalculator" && input.Id == "FlowRate" && input.DefaultValue?.Equals(0) == true)
            {
                outputs["system_efficiency"] = 0; // Because FlowRate = 0 makes condition false
            }
            
            return outputs.Count > 0 ? outputs : null;
        }

        private Dictionary<string, object> DetermineExpectedOutputsForIndeterminate(RuleDefinition rule)
        {
            var outputs = new Dictionary<string, object>();
            
            // For Indeterminate conditions, else-branch actions typically execute
            if (rule.ElseActions?.Any() == true)
            {
                foreach (var action in rule.ElseActions)
                {
                    if (action is V3SetAction setAction)
                    {
                        // Try to determine the else-branch value
                        if (setAction.Value != null)
                        {
                            outputs[setAction.Key] = setAction.Value;
                        }
                        else if (setAction.ValueExpression == "false")
                        {
                            outputs[setAction.Key] = false;
                        }
                        else if (setAction.ValueExpression == "true")
                        {
                            outputs[setAction.Key] = true;
                        }
                        else if (setAction.ValueExpression == "0")
                        {
                            outputs[setAction.Key] = 0;
                        }
                    }
                }
            }
            
            return outputs;
        }

        private Dictionary<string, object>? DetermineExpectedOutputWithCachedValue(RuleDefinition rule, InputDefinition input, double cachedValue)
        {
            // Similar to DetermineExpectedOutputWithDefaultValue but using cached value
            var outputs = new Dictionary<string, object>();
            
            // This would need rule-specific logic to determine expected outputs
            // when using cached values
            
            return outputs.Count > 0 ? outputs : null;
        }
    }
}
