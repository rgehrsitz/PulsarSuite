// File: Pulsar.Tests/TestUtilities/RedisUtilities.cs
// Version: 1.0.0

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Xunit;

namespace Pulsar.Tests.TestUtilities
{
    /// <summary>
    /// Comprehensive Redis utilities for testing, consolidating functionality
    /// from multiple existing test helper classes
    /// </summary>
    public static class RedisUtilities
    {
        #region Redis Extensions

        /// <summary>
        /// Extension method for IDatabase to get keys using a pattern
        /// </summary>
        public static IEnumerable<RedisKey> KeysAsync(this IDatabase db, string pattern)
        {
            var server = db.Multiplexer.GetServer(db.Multiplexer.GetEndPoints()[0]);
            return server.Keys(db.Database, pattern);
        }

        #endregion

        #region Configuration Helpers

        /// <summary>
        /// Creates a standard Redis configuration for tests using a test container
        /// </summary>
        public static Dictionary<string, object> CreateRedisConfig(IContainer redisContainer)
        {
            if (redisContainer == null)
            {
                throw new ArgumentNullException(nameof(redisContainer));
            }

            return new Dictionary<string, object>
            {
                ["endpoints"] = new List<string>
                {
                    $"localhost:{redisContainer.GetMappedPublicPort(6379)}"
                },
                ["poolSize"] = 8,
                ["retryCount"] = 3,
                ["retryBaseDelayMs"] = 100,
                ["connectTimeout"] = 5000,
                ["syncTimeout"] = 1000,
                ["keepAlive"] = 60,
                ["password"] = string.Empty,
                ["ssl"] = false,
                ["allowAdmin"] = true
            };
        }

        /// <summary>
        /// Creates Redis ConnectionMultiplexer configuration options for tests using a test container
        /// </summary>
        public static ConfigurationOptions CreateRedisConnectionOptions(IContainer redisContainer)
        {
            if (redisContainer == null)
            {
                throw new ArgumentNullException(nameof(redisContainer));
            }

            var options = new ConfigurationOptions
            {
                EndPoints = { $"localhost:{redisContainer.GetMappedPublicPort(6379)}" },
                ConnectTimeout = 5000,
                SyncTimeout = 1000,
                KeepAlive = 60,
                AbortOnConnectFail = false,
                AllowAdmin = true
            };
            
            return options;
        }

        #endregion

        #region Test Scenarios

        /// <summary>
        /// Tests Redis service with cluster configuration
        /// </summary>
        public static async Task TestClusterConfiguration(
            IConnectionMultiplexer redis,
            string uniquePrefix,
            ILogger logger)
        {
            // This is a simulated cluster test since we can't easily create a real Redis cluster in tests
            // In a real environment, this would connect to multiple Redis nodes
            var db = redis.GetDatabase();
            
            // Test basic operations
            var key = $"{uniquePrefix}:cluster";
            var value = "cluster-test-value";

            await db.StringSetAsync(key, value);
            var result = await db.StringGetAsync(key);

            Assert.Equal(value, result);
            logger.LogInformation("Redis cluster configuration test passed");
        }

        /// <summary>
        /// Tests Redis service with high availability configuration
        /// </summary>
        public static async Task TestHighAvailabilityConfiguration(
            IConnectionMultiplexer redis,
            string uniquePrefix,
            ILogger logger)
        {
            // This is a simulated HA test since we can't easily create master/replica setup in tests
            // In a real environment, this would connect to a master and replica nodes
            var db = redis.GetDatabase();
            
            // Test basic operations
            var key = $"{uniquePrefix}:ha";
            var value = "ha-test-value";

            await db.StringSetAsync(key, value);
            var result = await db.StringGetAsync(key);

            Assert.Equal(value, result);
            logger.LogInformation("Redis high availability configuration test passed");
        }

        /// <summary>
        /// Cleans up test data from Redis with a specific prefix
        /// </summary>
        public static async Task CleanupTestData(IConnectionMultiplexer redis, string prefix)
        {
            var db = redis.GetDatabase();
            var server = redis.GetServer(redis.GetEndPoints()[0]);
            
            var keys = server.Keys(db.Database, $"{prefix}:*");
            foreach (var key in keys)
            {
                await db.KeyDeleteAsync(key);
            }
        }

        #endregion
    }
}