using BeaconTester.Core.Models;
using BeaconTester.RuleAnalyzer.Parsing;
using Serilog;

namespace BeaconTester.RuleAnalyzer.Generation
{
    /// <summary>
    /// Generates comprehensive temporal test scenarios for threshold_over_time conditions
    /// and WindowTracker behavior validation
    /// </summary>
    public class TemporalTestScenarioGenerator
    {
        private readonly ILogger _logger;

        public TemporalTestScenarioGenerator(ILogger logger)
        {
            _logger = logger.ForContext<TemporalTestScenarioGenerator>();
        }

        /// <summary>
        /// Generates comprehensive temporal test scenarios for a rule
        /// </summary>
        public List<TestScenario> GenerateTemporalScenarios(RuleDefinition rule)
        {
            _logger.Information("Generating temporal test scenarios for rule: {RuleName}", rule.Name);
            
            var scenarios = new List<TestScenario>();

            // Find temporal conditions in the rule
            var temporalConditions = FindTemporalConditions(rule);
            if (!temporalConditions.Any())
            {
                _logger.Debug("No temporal conditions found in rule {RuleName}", rule.Name);
                return scenarios;
            }

            foreach (var condition in temporalConditions)
            {
                // 1. Window establishment scenarios
                scenarios.AddRange(GenerateWindowEstablishmentScenarios(rule, condition));
                
                // 2. Window interruption scenarios  
                scenarios.AddRange(GenerateWindowInterruptionScenarios(rule, condition));
                
                // 3. Duration boundary scenarios
                scenarios.AddRange(GenerateDurationBoundaryScenarios(rule, condition));
                
                // 4. Sensor unavailability scenarios
                scenarios.AddRange(GenerateSensorUnavailabilityScenarios(rule, condition));
            }

            _logger.Information("Generated {ScenarioCount} temporal test scenarios for rule {RuleName}", 
                scenarios.Count, rule.Name);

            return scenarios;
        }

        private List<TemporalCondition> FindTemporalConditions(RuleDefinition rule)
        {
            var conditions = new List<TemporalCondition>();

            if (rule.Conditions?.All != null)
            {
                foreach (var wrapper in rule.Conditions.All)
                {
                    var condition = wrapper.Condition;
                    if (condition.Type == "threshold_over_time" && condition is ThresholdOverTimeCondition toc)
                    {
                        conditions.Add(ParseTemporalCondition(toc));
                    }
                }
            }

            if (rule.Conditions?.Any != null)
            {
                foreach (var wrapper in rule.Conditions.Any)
                {
                    var condition = wrapper.Condition;
                    if (condition.Type == "threshold_over_time" && condition is ThresholdOverTimeCondition toc)
                    {
                        conditions.Add(ParseTemporalCondition(toc));
                    }
                }
            }

            return conditions;
        }

        private TemporalCondition ParseTemporalCondition(ThresholdOverTimeCondition condition)
        {
            return new TemporalCondition
            {
                Sensor = condition.Sensor,
                Operator = condition.Operator ?? ">",
                Threshold = condition.Threshold,
                Duration = TimeSpan.FromMilliseconds(condition.Duration)
            };
        }

        private TimeSpan ParseDuration(string duration)
        {
            if (string.IsNullOrEmpty(duration))
                return TimeSpan.FromSeconds(10);

            var number = new string(duration.TakeWhile(char.IsDigit).ToArray());
            var unit = duration.Substring(number.Length);

            if (!int.TryParse(number, out var value))
                return TimeSpan.FromSeconds(10);

            return unit.ToLower() switch
            {
                "ms" => TimeSpan.FromMilliseconds(value),
                "s" => TimeSpan.FromSeconds(value),
                "m" => TimeSpan.FromMinutes(value),
                "h" => TimeSpan.FromHours(value),
                _ => TimeSpan.FromSeconds(value)
            };
        }

        private List<TestScenario> GenerateWindowEstablishmentScenarios(RuleDefinition rule, TemporalCondition condition)
        {
            var scenarios = new List<TestScenario>();
            
            var scenario = new TestScenario
            {
                Name = $"{rule.Name}_WindowEstablishment",
                Description = $"Tests window establishment for {condition.Sensor} {condition.Operator} {condition.Threshold} over {condition.Duration}",
                ClearOutputs = true,
                Steps = new List<TestStep>()
            };

            // Step 1: Value below threshold - should be False
            var belowValue = GetValueBelowThreshold(condition);
            scenario.Steps.Add(new TestStep
            {
                Name = "Below threshold",
                Inputs = new List<TestInput>
                {
                    new TestInput { Key = condition.Sensor, Value = belowValue }
                },
                Delay = 500,
                Expectations = new List<TestExpectation>
                {
                    new TestExpectation
                    {
                        Key = GetExpectedOutputKey(rule),
                        Expected = "False",
                        Validator = GetValidatorType(rule)
                    }
                }
            });

            // Step 2: Value above threshold but within duration - should be False
            var aboveValue = GetValueAboveThreshold(condition);
            var partialDuration = (int)(condition.Duration.TotalMilliseconds * 0.5);
            scenario.Steps.Add(new TestStep
            {
                Name = "Above threshold - partial duration",
                Inputs = new List<TestInput>
                {
                    new TestInput { Key = condition.Sensor, Value = aboveValue }
                },
                Delay = partialDuration,
                Expectations = new List<TestExpectation>
                {
                    new TestExpectation
                    {
                        Key = GetExpectedOutputKey(rule),
                        Expected = "False",
                        Validator = GetValidatorType(rule)
                    }
                }
            });

            // Step 3: Complete duration - should be True
            var remainingDuration = (int)(condition.Duration.TotalMilliseconds * 0.6); // Ensure we exceed duration
            scenario.Steps.Add(new TestStep
            {
                Name = "Above threshold - complete duration",
                Inputs = new List<TestInput>
                {
                    new TestInput { Key = condition.Sensor, Value = aboveValue }
                },
                Delay = remainingDuration,
                Expectations = new List<TestExpectation>
                {
                    new TestExpectation
                    {
                        Key = GetExpectedOutputKey(rule),
                        Expected = "True",
                        Validator = GetValidatorType(rule)
                    }
                }
            });

            scenarios.Add(scenario);
            return scenarios;
        }

        private List<TestScenario> GenerateWindowInterruptionScenarios(RuleDefinition rule, TemporalCondition condition)
        {
            var scenarios = new List<TestScenario>();
            
            var scenario = new TestScenario
            {
                Name = $"{rule.Name}_WindowInterruption",
                Description = $"Tests window interruption behavior for {condition.Sensor}",
                ClearOutputs = true,
                Steps = new List<TestStep>()
            };

            var aboveValue = GetValueAboveThreshold(condition);
            var belowValue = GetValueBelowThreshold(condition);
            var partialDuration = (int)(condition.Duration.TotalMilliseconds * 0.7);

            // Step 1: Start above threshold
            scenario.Steps.Add(new TestStep
            {
                Name = "Start above threshold",
                Inputs = new List<TestInput>
                {
                    new TestInput { Key = condition.Sensor, Value = aboveValue }
                },
                Delay = partialDuration,
                Expectations = new List<TestExpectation>
                {
                    new TestExpectation
                    {
                        Key = GetExpectedOutputKey(rule),
                        Expected = "False",
                        Validator = GetValidatorType(rule)
                    }
                }
            });

            // Step 2: Drop below threshold - window should reset
            scenario.Steps.Add(new TestStep
            {
                Name = "Drop below threshold - window resets",
                Inputs = new List<TestInput>
                {
                    new TestInput { Key = condition.Sensor, Value = belowValue }
                },
                Delay = 500,
                Expectations = new List<TestExpectation>
                {
                    new TestExpectation
                    {
                        Key = GetExpectedOutputKey(rule),
                        Expected = "False",
                        Validator = GetValidatorType(rule)
                    }
                }
            });

            // Step 3: Back above threshold - should restart duration
            scenario.Steps.Add(new TestStep
            {
                Name = "Back above threshold - restart duration",
                Inputs = new List<TestInput>
                {
                    new TestInput { Key = condition.Sensor, Value = aboveValue }
                },
                Delay = (int)(condition.Duration.TotalMilliseconds * 1.2), // Ensure full duration completes
                Expectations = new List<TestExpectation>
                {
                    new TestExpectation
                    {
                        Key = GetExpectedOutputKey(rule),
                        Expected = "True",
                        Validator = GetValidatorType(rule)
                    }
                }
            });

            scenarios.Add(scenario);
            return scenarios;
        }

        private List<TestScenario> GenerateDurationBoundaryScenarios(RuleDefinition rule, TemporalCondition condition)
        {
            var scenarios = new List<TestScenario>();
            
            var scenario = new TestScenario
            {
                Name = $"{rule.Name}_DurationBoundary",
                Description = $"Tests duration boundary conditions for {condition.Sensor}",
                ClearOutputs = true,
                Steps = new List<TestStep>()
            };

            var aboveValue = GetValueAboveThreshold(condition);
            
            // Test exact duration boundary
            var exactDuration = (int)condition.Duration.TotalMilliseconds;
            var justBefore = exactDuration - 100;
            var justAfter = exactDuration + 100;

            // Step 1: Just before duration - should be False
            scenario.Steps.Add(new TestStep
            {
                Name = "Just before duration boundary",
                Inputs = new List<TestInput>
                {
                    new TestInput { Key = condition.Sensor, Value = aboveValue }
                },
                Delay = justBefore,
                Expectations = new List<TestExpectation>
                {
                    new TestExpectation
                    {
                        Key = GetExpectedOutputKey(rule),
                        Expected = "False",
                        Validator = GetValidatorType(rule)
                    }
                }
            });

            // Step 2: Just after duration - should be True
            scenario.Steps.Add(new TestStep
            {
                Name = "Just after duration boundary",
                Inputs = new List<TestInput>
                {
                    new TestInput { Key = condition.Sensor, Value = aboveValue }
                },
                Delay = justAfter,
                Expectations = new List<TestExpectation>
                {
                    new TestExpectation
                    {
                        Key = GetExpectedOutputKey(rule),
                        Expected = "True",
                        Validator = GetValidatorType(rule)
                    }
                }
            });

            scenarios.Add(scenario);
            return scenarios;
        }

        private List<TestScenario> GenerateSensorUnavailabilityScenarios(RuleDefinition rule, TemporalCondition condition)
        {
            var scenarios = new List<TestScenario>();
            
            var scenario = new TestScenario
            {
                Name = $"{rule.Name}_SensorUnavailability",
                Description = $"Tests sensor unavailability handling for {condition.Sensor}",
                ClearOutputs = true,
                Steps = new List<TestStep>()
            };

            var aboveValue = GetValueAboveThreshold(condition);
            var partialDuration = (int)(condition.Duration.TotalMilliseconds * 0.5);

            // Step 1: Start above threshold
            scenario.Steps.Add(new TestStep
            {
                Name = "Start above threshold",
                Inputs = new List<TestInput>
                {
                    new TestInput { Key = condition.Sensor, Value = aboveValue }
                },
                Delay = partialDuration,
                Expectations = new List<TestExpectation>
                {
                    new TestExpectation
                    {
                        Key = GetExpectedOutputKey(rule),
                        Expected = "False",
                        Validator = GetValidatorType(rule)
                    }
                }
            });

            // Step 2: Remove sensor data (sensor unavailable)
            scenario.Steps.Add(new TestStep
            {
                Name = "Sensor unavailable - should pause window",
                Inputs = new List<TestInput>(), // No inputs = sensor unavailable
                Delay = 1000,
                Expectations = new List<TestExpectation>
                {
                    new TestExpectation
                    {
                        Key = GetExpectedOutputKey(rule),
                        Expected = "Indeterminate", // v3 behavior: sensor unavailable = Indeterminate
                        Validator = GetValidatorType(rule)
                    }
                }
            });

            // Step 3: Restore sensor - should restart window
            scenario.Steps.Add(new TestStep
            {
                Name = "Sensor restored - restart window",
                Inputs = new List<TestInput>
                {
                    new TestInput { Key = condition.Sensor, Value = aboveValue }
                },
                Delay = (int)(condition.Duration.TotalMilliseconds * 1.2),
                Expectations = new List<TestExpectation>
                {
                    new TestExpectation
                    {
                        Key = GetExpectedOutputKey(rule),
                        Expected = "True",
                        Validator = GetValidatorType(rule)
                    }
                }
            });

            scenarios.Add(scenario);
            return scenarios;
        }

        private double GetValueAboveThreshold(TemporalCondition condition)
        {
            return condition.Operator switch
            {
                ">" => condition.Threshold + 10,
                ">=" => condition.Threshold + 5,
                "<" => condition.Threshold - 10,
                "<=" => condition.Threshold - 5,
                _ => condition.Threshold + 10
            };
        }

        private double GetValueBelowThreshold(TemporalCondition condition)
        {
            return condition.Operator switch
            {
                ">" => condition.Threshold - 10,
                ">=" => condition.Threshold - 5,
                "<" => condition.Threshold + 10,
                "<=" => condition.Threshold + 5,
                _ => condition.Threshold - 10
            };
        }

        private string GetExpectedOutputKey(RuleDefinition rule)
        {
            // Try to find the output key from the rule's actions
            var setAction = rule.Actions?.OfType<SetValueAction>().FirstOrDefault();
            if (setAction != null && !string.IsNullOrEmpty(setAction.Key))
            {
                return $"output:{setAction.Key}";
            }

            // Check for V3 actions
            var v3SetAction = rule.Actions?.OfType<V3SetAction>().FirstOrDefault();
            if (v3SetAction != null && !string.IsNullOrEmpty(v3SetAction.Key))
            {
                return $"output:{v3SetAction.Key}";
            }

            // Fallback to rule name pattern
            return $"output:{rule.Name.ToLowerInvariant().Replace(" ", "_")}";
        }

        private string GetValidatorType(RuleDefinition rule)
        {
            // For temporal rules with v3 three-valued logic, use EvalResult validator
            return "evalresult";
        }
    }

    /// <summary>
    /// Represents a temporal condition for test generation
    /// </summary>
    public class TemporalCondition
    {
        public string Sensor { get; set; } = "";
        public string Operator { get; set; } = ">";
        public double Threshold { get; set; }
        public TimeSpan Duration { get; set; }
    }
}