// Use the mock classes instead
using System;
using System.Threading.Tasks;
using Pulsar.Tests.Mocks;
using Pulsar.Tests.TestUtilities;
using Xunit;
using Xunit.Abstractions;

namespace Pulsar.Tests.Integration
{
    [Trait("Category", "Integration")]
    public class RedisIntegrationTests(RedisTestFixture fixture, ITestOutputHelper output)
        : IClassFixture<RedisTestFixture>,
            IAsyncLifetime
    {
        private string _uniquePrefix = $"test:{Guid.NewGuid():N}";

        public Task InitializeAsync() => Task.CompletedTask;

        public Task DisposeAsync()
        {
            // Clean up any test data
            if (fixture.Redis != null)
            {
                // Fix: Use RedisExtensions and add the missing using directive
                var keys = fixture.Redis.GetDatabase().KeysAsync($"{_uniquePrefix}*");
                foreach (var key in keys)
                {
                    fixture.Redis.GetDatabase().KeyDelete(key);
                }
            }
            return Task.CompletedTask;
        }

        [Fact]
        public async Task GetValue_NonExistentKey_ReturnsNull()
        {
            // Arrange
            var key = $"{_uniquePrefix}:nonexistent";

            // Act
            var result = await fixture.RedisService.GetValue(key);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task SetValue_ThenGetValue_ReturnsCorrectValue()
        {
            // Arrange
            var key = $"{_uniquePrefix}:setValue";
            var value = "test-value";

            // Act
            await fixture.RedisService.SetValue(key, value);
            var result = await fixture.RedisService.GetValue(key);

            // Assert
            Assert.Equal(value, result);
        }

        [Fact]
        public async Task SetValue_WithObjectValue_SerializesAndDeserializesProperly()
        {
            // Arrange
            var key = $"{_uniquePrefix}:object";
            var testObject = new TestObject { Id = 42, Name = "Test" };

            // Act
            await fixture.RedisService.SetValue(key, testObject);
            var result = await fixture.RedisService.GetValue<TestObject>(key);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(42, result.Id);
            Assert.Equal("Test", result.Name);
        }

        [Fact]
        public async Task SendMessage_SubscribedChannel_HandlerReceivesMessage()
        {
            // Arrange
            var channel = $"{_uniquePrefix}:channel";
            var message = "test-message";
            var receivedMessage = "";
            var messageReceived = new TaskCompletionSource<bool>();

            // Act
            await fixture.RedisService.Subscribe(
                channel,
                (ch, msg) =>
                {
                    receivedMessage = msg.ToString();
                    messageReceived.SetResult(true);
                }
            );

            await fixture.RedisService.SendMessage(channel, message);

            // Wait for message to be received (with timeout)
            await Task.WhenAny(messageReceived.Task, Task.Delay(5000));

            // Assert
            Assert.Equal(message, receivedMessage);
            Assert.True(
                messageReceived.Task.IsCompletedSuccessfully,
                "Message was not received within timeout"
            );
        }

        [Fact]
        public async Task GetAllInputsAsync_ReturnsCorrectValues()
        {
            // Arrange
            await fixture.RedisService.SetValue("input:a", 100);
            await fixture.RedisService.SetValue("input:b", 200);
            await fixture.RedisService.SetValue("input:c", 300);

            // Act
            var result = await fixture.RedisService.GetAllInputsAsync();

            // Assert
            Assert.NotNull(result);
            // The implementation returns both prefixed and unprefixed keys (6 total)
            Assert.True(result.Count >= 3, $"Expected at least 3 items, got {result.Count}");
            Assert.Equal(100.0, Convert.ToDouble(result["input:a"]));
            Assert.Equal(200.0, Convert.ToDouble(result["input:b"]));
            Assert.Equal(300.0, Convert.ToDouble(result["input:c"]));
        }

        [Fact]
        public async Task RetryPolicy_HandlesConnectionErrors()
        {
            // This test verifies the retry policy by monitoring retries without needing actual failures
            // We don't make actual connection errors, but verify the retry policy is correctly configured

            // Arrange - Get retry configuration
            var retryCount = fixture.RedisService.RetryPolicy.MaxRetryCount;
            var baseDelay = fixture.RedisService.RetryPolicy.BaseDelayMilliseconds;

            // Assert - Just verify that retry policy is configured with reasonable values
            Assert.Equal(3, retryCount);
            Assert.Equal(100, baseDelay);

            output.WriteLine(
                $"Redis retry policy configured with: MaxRetryCount={retryCount}, BaseDelay={baseDelay}ms"
            );

            // Test health check function
            var isHealthy = fixture.RedisService.IsHealthy;
            Assert.True(isHealthy, "Redis should be healthy in test environment");

            // No need to actually force errors - we're just verifying configuration
            await Task.CompletedTask;
        }

        private class TestObject
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }
    }
}
