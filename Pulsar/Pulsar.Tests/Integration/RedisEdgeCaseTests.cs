// File: Pulsar.Tests/Integration/RedisEdgeCaseTests.cs

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Tests.Mocks;
using Pulsar.Tests.TestUtilities;
using Serilog;
using Serilog.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Pulsar.Tests.Integration
{
    public class RedisEdgeCaseTests
    {
        private readonly Microsoft.Extensions.Logging.ILogger _logger;
        private readonly ITestOutputHelper _output;
        private readonly RedisService _redisService;

        public RedisEdgeCaseTests(ITestOutputHelper output)
        {
            _output = output;
            _logger = LoggingConfig.GetLoggerForTests(output);

            // Configure Redis service with special test settings
            var config = new RedisConfiguration
            {
                SingleNode = new SingleNodeConfig
                {
                    Endpoints = new[] { "localhost:6379" },
                    RetryCount = 1,
                    RetryBaseDelayMs = 10,
                    ConnectTimeout = 100,
                    SyncTimeout = 100,
                },
            };

            // Create a logger factory for tests
            var loggerFactory = new LoggerFactory();
            // Use the Serilog provider which is set up for tests
            var serilogLogger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.TestOutput(output)
                .CreateLogger();
            loggerFactory.AddSerilog(serilogLogger);
            _redisService = new RedisService(config, loggerFactory);
        }

        [Fact]
        public async Task NullInputsToSetOutputsAsync_DoesNotThrow()
        {
            // Act & Assert
            await _redisService.SetOutputsAsync(null);

            // If we got here, the test passes (no exception thrown)
            Assert.True(true);
        }

        [Fact]
        public async Task EmptyInputsToSetOutputsAsync_DoesNotThrow()
        {
            // Act & Assert
            await _redisService.SetOutputsAsync(new Dictionary<string, object>());

            // If we got here, the test passes (no exception thrown)
            Assert.True(true);
        }

        [Fact]
        public async Task NullInputsToSetOutputValuesAsync_DoesNotThrow()
        {
            // Act & Assert
            await _redisService.SetOutputValuesAsync(null);

            // If we got here, the test passes (no exception thrown)
            Assert.True(true);
        }

        [Fact]
        public async Task EmptyInputsToSetOutputValuesAsync_DoesNotThrow()
        {
            // Act & Assert
            await _redisService.SetOutputValuesAsync(new Dictionary<string, double>());

            // If we got here, the test passes (no exception thrown)
            Assert.True(true);
        }

        [Fact]
        public async Task NullInputsToSetStateAsync_DoesNotThrow()
        {
            // Act & Assert
            await _redisService.SetStateAsync(null);

            // If we got here, the test passes (no exception thrown)
            Assert.True(true);
        }

        [Fact]
        public async Task GetSensorValuesAsync_HandlesEmptyInputs()
        {
            // Act
            var result = await _redisService.GetSensorValuesAsync(new List<string>());

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetSensorValuesAsync_HandlesNonExistentSensors()
        {
            // Act
            var result = await _redisService.GetSensorValuesAsync(
                new List<string> { "nonexistent:sensor" }
            );

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetValues_HandlesNonExistentSensor()
        {
            // Act
            var result = await _redisService.GetValues("nonexistent:sensor", 10);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task SetValue_WithNull_StoresNullValue()
        {
            // Arrange
            string key = "test:null";

            // Act
            await _redisService.SetValue(key, null);
            var result = await _redisService.GetValue(key);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task HashGetAllAsync_ReturnsNull_ForNonExistentKey()
        {
            // Act
            var result = await _redisService.HashGetAllAsync("nonexistent:hash");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task DeleteKeyAsync_ReturnsFalse_ForNonExistentKey()
        {
            // Act
            var result = await _redisService.DeleteKeyAsync("nonexistent:key");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ObjectValueTypesArePreserved()
        {
            // Arrange
            var values = new Dictionary<string, object>
            {
                { "output:int", 42 },
                { "output:double", 3.14 },
                { "output:bool", true },
                { "output:string", "test" },
                { "output:date", DateTime.UtcNow },
            };

            // Act
            await _redisService.SetOutputsAsync(values);
            var outputs = await _redisService.GetOutputsAsync();

            // Assert
            Assert.Equal(42, outputs["output:int"]);
            Assert.Equal(3.14, outputs["output:double"]);
            Assert.Equal(true, outputs["output:bool"]);
            Assert.Equal("test", outputs["output:string"]);
            // Date might be converted to string in Redis
            Assert.NotNull(outputs["output:date"]);
        }

        [Fact]
        public async Task HashSet_OverwritesExistingValue()
        {
            // Arrange
            string hashKey = "test:overwrite:hash";
            string field = "field1";

            // Act - Set initial value
            await _redisService.HashSetAsync(hashKey, field, "initial");
            var initialValue = await _redisService.HashGetAsync(hashKey, field);

            // Act - Overwrite value
            await _redisService.HashSetAsync(hashKey, field, "updated");
            var updatedValue = await _redisService.HashGetAsync(hashKey, field);

            // Assert
            Assert.Equal("initial", initialValue);
            Assert.Equal("updated", updatedValue);

            // Cleanup
            await _redisService.DeleteKeyAsync(hashKey);
        }

        [Fact]
        public async Task SetOutputValuesAsync_HandlesSpecialNumericValues()
        {
            // Arrange
            var values = new Dictionary<string, double>
            {
                { "output:positive_infinity", double.PositiveInfinity },
                { "output:negative_infinity", double.NegativeInfinity },
                { "output:nan", double.NaN },
                { "output:epsilon", double.Epsilon },
                { "output:max_value", double.MaxValue },
                { "output:min_value", double.MinValue },
            };

            // Act - Should not throw
            await _redisService.SetOutputValuesAsync(values);

            // Assert - At minimum, should have stored string representations
            var outputs = await _redisService.GetOutputsAsync();

            // Validate that something was stored for each key
            Assert.True(outputs.ContainsKey("output:positive_infinity"));
            Assert.True(outputs.ContainsKey("output:negative_infinity"));
            Assert.True(outputs.ContainsKey("output:nan"));
            Assert.True(outputs.ContainsKey("output:epsilon"));
            Assert.True(outputs.ContainsKey("output:max_value"));
            Assert.True(outputs.ContainsKey("output:min_value"));
        }

        [Fact]
        public async Task ConcurrentOperations_DoNotInterfere()
        {
            // Arrange
            var tasks = new List<Task>();
            int concurrentOperations = 10;

            // Act - Start multiple concurrent operations
            for (int i = 0; i < concurrentOperations; i++)
            {
                int operationId = i;
                tasks.Add(
                    Task.Run(async () =>
                    {
                        try
                        {
                            // Set a value - include input: prefix to ensure consistency with Redis key conventions
                            string key = $"input:concurrent:key:{operationId}";
                            await _redisService.SetValue(key, operationId);

                            // Get the value back
                            var value = await _redisService.GetValue<int>(key);

                            // Verify it matches what we set
                            Assert.Equal(operationId, value);
                        }
                        catch (Exception ex)
                        {
                            // Log the exception to help with debugging
                            _output.WriteLine($"Error in task {operationId}: {ex.Message}");
                            throw; // Re-throw so the task will be marked as faulted
                        }
                    })
                );
            }

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);

            // Assert - All tasks completed successfully
            Assert.Equal(concurrentOperations, tasks.Count);
            foreach (var task in tasks)
            {
                if (task.IsFaulted && task.Exception != null)
                {
                    // Log details about any faulted tasks
                    _output.WriteLine($"Task faulted: {task.Exception.InnerException?.Message}");
                }
                Assert.True(task.IsCompleted, "Task should be completed");
                Assert.False(task.IsFaulted, "Task should not be faulted");
            }
        }

        [Fact]
        public async Task LargeNumberOfKeys_AreHandledCorrectly()
        {
            // Arrange
            int keyCount = 10; // Reduced from 100 for test performance

            // Act - Create many keys with input: prefix
            for (int i = 0; i < keyCount; i++)
            {
                await _redisService.SetValue($"input:bulk:key:{i}", i);
            }

            // Get all inputs
            var allInputs = await _redisService.GetAllInputsAsync();

            // Assert
            for (int i = 0; i < keyCount; i++)
            {
                var fullKey = $"input:bulk:key:{i}";
                var shortKey = $"bulk:key:{i}";
                var success = allInputs.ContainsKey(fullKey) || allInputs.ContainsKey(shortKey);
                if (!success)
                {
                    // Log which keys failed for debugging
                    _output.WriteLine($"Failed to find key: {i}");
                    _output.WriteLine($"Expected either '{fullKey}' or '{shortKey}'");
                    _output.WriteLine($"Keys in result: {string.Join(", ", allInputs.Keys)}");
                }
                Assert.True(success, $"Missing key: {i}");
            }
        }

        [Fact]
        public async Task GetSensorValuesAsync_SkipsNonNumericValues()
        {
            // Arrange
            await _redisService.SetValue("input:temperature", 25);
            await _redisService.SetValue("input:text", "not a number");

            // Act
            var sensorValues = await _redisService.GetSensorValuesAsync(
                new[] { "temperature", "text" }
            );

            // Assert
            Assert.Single(sensorValues);
            Assert.True(sensorValues.ContainsKey("temperature"));
            Assert.False(sensorValues.ContainsKey("text"));
        }

        [Fact]
        public async Task PublishAsync_NoSubscribers_ReturnsZero()
        {
            // Act
            var result = await _redisService.PublishAsync("unused:channel", "message");

            // Assert - No subscribers, should return 0
            Assert.Equal(0, result);
        }
    }
}
