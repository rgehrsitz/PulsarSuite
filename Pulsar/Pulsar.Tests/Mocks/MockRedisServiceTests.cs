// File: Pulsar.Tests/Mocks/MockRedisServiceTests.cs

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Tests.Mocks;
using Xunit;

namespace Pulsar.Tests.Mocks
{
    public class MockRedisServiceTests
    {
        private readonly RedisService _redisService;
        private readonly ILoggerFactory _loggerFactory = new NullLoggerFactory();
        
        public MockRedisServiceTests()
        {
            var config = new RedisConfiguration
            {
                SingleNode = new SingleNodeConfig
                {
                    Endpoints = new[] { "localhost:6379" },
                    RetryCount = 3,
                    RetryBaseDelayMs = 100
                }
            };
            
            _redisService = new RedisService(config, _loggerFactory);
        }
        
        [Fact]
        public void RedisService_HasRetryPolicy()
        {
            // Assert
            Assert.NotNull(_redisService.RetryPolicy);
            Assert.Equal(3, _redisService.RetryPolicy.MaxRetryCount);
            Assert.Equal(100, _redisService.RetryPolicy.BaseDelayMilliseconds);
        }
        
        [Fact]
        public void RedisService_HasHealthCheckProperty()
        {
            // Assert
            Assert.True(_redisService.IsHealthy);
        }
        
        [Fact]
        public async Task GetSetValue_StoresAndRetrievesValue()
        {
            // Arrange
            string testKey = "test:key";
            string testValue = "test-value";
            
            // Act
            await _redisService.SetValue(testKey, testValue);
            var retrievedValue = await _redisService.GetValue(testKey);
            
            // Assert
            Assert.Equal(testValue, retrievedValue);
        }
        
        [Fact]
        public async Task GetValue_ReturnsNull_WhenKeyDoesNotExist()
        {
            // Act
            var retrievedValue = await _redisService.GetValue("nonexistent:key");
            
            // Assert
            Assert.Null(retrievedValue);
        }
        
        [Fact]
        public async Task GetValue_GenericOverload_PerformsTypeConversion()
        {
            // Arrange
            string testKey = "test:int";
            int testValue = 42;
            
            // Act
            await _redisService.SetValue(testKey, testValue);
            var retrievedValue = await _redisService.GetValue<int>(testKey);
            
            // Assert
            Assert.Equal(testValue, retrievedValue);
        }
        
        [Fact]
        public async Task GetAllInputsAsync_ReturnsInputsWithAndWithoutPrefix()
        {
            // Arrange
            await _redisService.SetValue("input:temp", 25);
            await _redisService.SetValue("input:humidity", 60);
            
            // Act
            var inputs = await _redisService.GetAllInputsAsync();
            
            // Assert
            Assert.NotNull(inputs);
            
            // Should contain prefixed versions
            Assert.True(inputs.ContainsKey("input:temp"));
            Assert.True(inputs.ContainsKey("input:humidity"));
            
            // Should also contain unprefixed versions
            Assert.True(inputs.ContainsKey("temp"));
            Assert.True(inputs.ContainsKey("humidity"));
            
            // Values should match
            Assert.Equal(25, inputs["input:temp"]);
            Assert.Equal(60, inputs["input:humidity"]);
        }
        
        [Fact]
        public async Task GetOutputsAsync_ReturnsOnlyOutputValues()
        {
            // Arrange
            await _redisService.SetValue("input:temp", 25);
            await _redisService.SetValue("output:alert", true);
            
            // Act
            var outputs = await _redisService.GetOutputsAsync();
            
            // Assert
            Assert.NotNull(outputs);
            Assert.True(outputs.ContainsKey("output:alert"));
            Assert.False(outputs.ContainsKey("input:temp"));
            Assert.Equal(true, outputs["output:alert"]);
        }
        
        [Fact]
        public async Task SetOutputsAsync_SetsMultipleOutputs()
        {
            // Arrange
            var outputs = new Dictionary<string, object>
            {
                { "alert", true },
                { "status", "warning" }
            };
            
            // Act
            await _redisService.SetOutputsAsync(outputs);
            var retrievedOutputs = await _redisService.GetOutputsAsync();
            
            // Assert
            Assert.NotNull(retrievedOutputs);
            Assert.True(retrievedOutputs.ContainsKey("output:alert"));
            Assert.True(retrievedOutputs.ContainsKey("output:status"));
            Assert.Equal(true, retrievedOutputs["output:alert"]);
            Assert.Equal("warning", retrievedOutputs["output:status"]);
        }
        
        [Fact]
        public async Task PublishAndSubscribe_DeliverMessagesToSubscribers()
        {
            // Arrange
            string testChannel = "test:channel";
            string testMessage = "test-message";
            var messageReceived = new TaskCompletionSource<string>();
            
            // Act
            await _redisService.Subscribe(testChannel, (channel, message) => {
                messageReceived.SetResult(message.ToString());
            });
            
            await _redisService.SendMessage(testChannel, testMessage);
            
            // Assert - Wait for message with timeout
            var receivedMessage = await messageReceived.Task.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.Equal(testMessage, receivedMessage);
        }
        
        [Fact]
        public async Task PublishAsync_ReturnsDeliveryCount()
        {
            // Arrange
            string testChannel = "test:channel";
            string testMessage = "test-message";
            
            // Add a subscriber to receive the message
            await _redisService.Subscribe(testChannel, (channel, message) => { });
            
            // Act
            var deliveryCount = await _redisService.PublishAsync(testChannel, testMessage);
            
            // Assert
            Assert.Equal(1, deliveryCount);
        }
        
        [Fact]
        public async Task HashSetAndGet_WorksCorrectly()
        {
            // Arrange
            string hashKey = "test:hash";
            string field = "field1";
            string value = "value1";
            
            // Act
            var setResult = await _redisService.HashSetAsync(hashKey, field, value);
            var retrievedValue = await _redisService.HashGetAsync(hashKey, field);
            
            // Assert
            Assert.True(setResult);
            Assert.Equal(value, retrievedValue);
        }
        
        [Fact]
        public async Task HashGetAll_ReturnsAllFields()
        {
            // Arrange
            string hashKey = "test:hash2";
            await _redisService.HashSetAsync(hashKey, "field1", "value1");
            await _redisService.HashSetAsync(hashKey, "field2", "value2");
            
            // Act
            var allFields = await _redisService.HashGetAllAsync(hashKey);
            
            // Assert
            Assert.NotNull(allFields);
            Assert.Equal(2, allFields.Count);
            Assert.Equal("value1", allFields["field1"]);
            Assert.Equal("value2", allFields["field2"]);
        }
        
        [Fact]
        public async Task DeleteKeyAsync_RemovesKeyAndChildren()
        {
            // Arrange
            string baseKey = "test:delete";
            string hashField1 = "field1";
            string hashField2 = "field2";
            
            await _redisService.HashSetAsync(baseKey, hashField1, "value1");
            await _redisService.HashSetAsync(baseKey, hashField2, "value2");
            
            // Act
            var deleteResult = await _redisService.DeleteKeyAsync(baseKey);
            var retrievedValue1 = await _redisService.HashGetAsync(baseKey, hashField1);
            var retrievedValue2 = await _redisService.HashGetAsync(baseKey, hashField2);
            
            // Assert
            Assert.True(deleteResult);
            Assert.Null(retrievedValue1);
            Assert.Null(retrievedValue2);
        }
    }
}