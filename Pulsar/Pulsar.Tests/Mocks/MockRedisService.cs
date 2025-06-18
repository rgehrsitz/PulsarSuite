// File: Pulsar.Tests/Mocks/MockRedisService.cs
// Version: 1.1.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
<<<<<<< HEAD
using Beacon.Runtime.Interfaces;
=======
using Microsoft.Extensions.Logging;
>>>>>>> 8b7c346332a26f768b0c87b04a580da20b2811ca

namespace Pulsar.Tests.Mocks
{
    public class SingleNodeConfig
    {
        public required string[] Endpoints { get; set; }
        public int PoolSize { get; set; }
        public int RetryCount { get; set; }
        public int RetryBaseDelayMs { get; set; }
        public int ConnectTimeout { get; set; }
        public int SyncTimeout { get; set; }
        public int KeepAlive { get; set; }
    }

    public class ClusterConfig
    {
        public required string[] Endpoints { get; set; }
        public int PoolSize { get; set; }
        public int RetryCount { get; set; }
        public int RetryBaseDelayMs { get; set; }
        public int ConnectTimeout { get; set; }
        public int SyncTimeout { get; set; }
        public int KeepAlive { get; set; }
    }

    public class HighAvailabilityConfig
    {
        public required string[] Endpoints { get; set; }
        public int PoolSize { get; set; }
        public bool ReplicaOnly { get; set; }
        public int RetryCount { get; set; }
        public int RetryBaseDelayMs { get; set; }
    }

    /// <summary>
    /// Configuration for Redis retry policy
    /// </summary>
    public class RetryPolicy
    {
        /// <summary>
        /// Gets the maximum number of retry attempts
        /// </summary>
        public int MaxRetryCount { get; }

        /// <summary>
        /// Gets the base delay in milliseconds between retry attempts
        /// </summary>
        public int BaseDelayMilliseconds { get; }

        /// <summary>
        /// Creates a new retry policy configuration
        /// </summary>
        public RetryPolicy(int maxRetryCount, int baseDelayMilliseconds)
        {
            MaxRetryCount = maxRetryCount;
            BaseDelayMilliseconds = baseDelayMilliseconds;
        }
    }

    public class RedisConfiguration
    {
        public SingleNodeConfig? SingleNode { get; set; }
        public ClusterConfig? Cluster { get; set; }
        public HighAvailabilityConfig? HighAvailability { get; set; }
    }

    /// <summary>
    /// Interface for Redis service operations that provides consistent access patterns
    /// throughout the application. Implementations of this interface handle Redis operations
    /// and data transfer.
    /// </summary>
    public interface IRedisService : IDisposable
    {
        /// <summary>
        /// Gets all input values from Redis
        /// </summary>
        /// <returns>Dictionary of input values</returns>
        Task<Dictionary<string, object>> GetAllInputsAsync();

        /// <summary>
        /// Gets input values (alias for GetAllInputsAsync)
        /// </summary>
        Task<Dictionary<string, object>> GetInputsAsync();

        /// <summary>
        /// Gets output values from Redis
        /// </summary>
        Task<Dictionary<string, object>> GetOutputsAsync();

        /// <summary>
        /// Gets state values from Redis
        /// </summary>
        Task<Dictionary<string, object>> GetStateAsync();

        /// <summary>
        /// Sets output values in Redis
        /// </summary>
        /// <param name="outputs">Dictionary of output values</param>
        /// <returns>Task representing the asynchronous operation</returns>
        Task SetOutputsAsync(Dictionary<string, object> outputs);

        /// <summary>
        /// Gets specific sensor values from Redis
        /// </summary>
        /// <param name="sensorKeys">List of sensor keys to retrieve</param>
        /// <returns>Dictionary of sensor values with timestamps</returns>
        Task<Dictionary<string, (double Value, DateTime Timestamp)>> GetSensorValuesAsync(
            IEnumerable<string> sensorKeys
        );

        /// <summary>
        /// Sets output values in Redis
        /// </summary>
        /// <param name="outputs">Dictionary of output values</param>
        /// <returns>Task representing the asynchronous operation</returns>
        Task SetOutputValuesAsync(Dictionary<string, double> outputs);

        /// <summary>
        /// Sets state values in Redis
        /// </summary>
        Task SetStateAsync(Dictionary<string, object> state);

        /// <summary>
        /// Gets the values for a sensor over time
        /// </summary>
        /// <param name="sensor">The sensor key</param>
        /// <param name="count">Number of historical values to retrieve</param>
        /// <returns>Array of historical values</returns>
        Task<(double Value, DateTime Timestamp)[]> GetValues(string sensor, int count);

        /// <summary>
        /// Checks if Redis is healthy
        /// </summary>
        /// <returns>True if Redis is healthy, false otherwise</returns>
        bool IsHealthy { get; }

        /// <summary>
        /// Publishes a message to a Redis channel
        /// </summary>
        /// <param name="channel">The channel to publish to</param>
        /// <param name="message">The message to publish</param>
        /// <returns>The number of clients that received the message</returns>
        Task<long> PublishAsync(string channel, string message);

        /// <summary>
        /// Sets a hash field in Redis
        /// </summary>
        Task<bool> HashSetAsync(string key, string field, string value);

        /// <summary>
        /// Gets a hash field from Redis
        /// </summary>
        Task<string?> HashGetAsync(string key, string field);

        /// <summary>
        /// Gets all hash fields from Redis
        /// </summary>
        Task<Dictionary<string, string>?> HashGetAllAsync(string key);

        /// <summary>
        /// Deletes a key from Redis
        /// </summary>
        Task<bool> DeleteKeyAsync(string key);

        /// <summary>
        /// Legacy method - Gets a value from Redis with type conversion
        /// </summary>
        Task<T?> GetValue<T>(string key);

        /// <summary>
        /// Legacy method - Gets a value from Redis as object
        /// </summary>
        Task<object?> GetValue(string key);

        /// <summary>
        /// Legacy method - Sets a value in Redis
        /// </summary>
        Task SetValue(string key, object value);

        /// <summary>
        /// Legacy method - Sends a message to a Redis channel
        /// </summary>
        Task SendMessage(string channel, object message);

        /// <summary>
        /// Legacy method - Subscribes to a Redis channel
        /// </summary>
        Task Subscribe(string channel, Action<string, object> handler);
    }

    /// <summary>
    /// Mock implementation of IRedisService for testing
    /// </summary>
    public class RedisService : IRedisService
    {
        private readonly Dictionary<string, object> _values = new();
        private readonly Dictionary<string, List<Action<string, object>>> _subscribers = new();
        private readonly ILogger _logger;
        private bool _disposed;

        // Redis key prefixes
        private const string INPUT_PREFIX = "input:";
        private const string OUTPUT_PREFIX = "output:";
        private const string STATE_PREFIX = "state:";

        /// <summary>
        /// Gets whether the Redis service is healthy
        /// </summary>
        public bool IsHealthy => true;

        /// <summary>
        /// Gets retry policy information
        /// </summary>
        public RetryPolicy RetryPolicy { get; } = new RetryPolicy(3, 100);

        /// <summary>
        /// Creates a new instance of MockRedisService
        /// </summary>
        public RedisService(RedisConfiguration config, ILoggerFactory loggerFactory)
        {
<<<<<<< HEAD
            _logger = loggerFactory?.CreateLogger<RedisService>() ??
                      Microsoft.Extensions.Logging.Abstractions.NullLogger<RedisService>.Instance;
=======
            _logger =
                loggerFactory?.CreateLogger<RedisService>()
                ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<RedisService>.Instance;
>>>>>>> 8b7c346332a26f768b0c87b04a580da20b2811ca

            // In a real implementation, this would connect to Redis
        }

        public Task<T?> GetValue<T>(string key)
        {
            if (_values.TryGetValue(key, out var value) && value is T typedValue)
                return Task.FromResult<T?>(typedValue);

            return Task.FromResult<T?>(default);
        }

        public Task<object?> GetValue(string key)
        {
            if (_values.TryGetValue(key, out var value))
                return Task.FromResult<object?>(value);

            return Task.FromResult<object?>(null);
        }

        public Task SetValue(string key, object value)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }

        public Task SendMessage(string channel, object message)
        {
            if (_subscribers.TryGetValue(channel, out var handlers))
            {
                foreach (var handler in handlers)
                {
                    handler(channel, message);
                }
            }
            return Task.CompletedTask;
        }

        public Task Subscribe(string channel, Action<string, object> handler)
        {
            if (!_subscribers.TryGetValue(channel, out var handlers))
            {
                handlers = new List<Action<string, object>>();
                _subscribers[channel] = handlers;
            }

            handlers.Add(handler);
            return Task.CompletedTask;
        }

        public Task<Dictionary<string, object>> GetAllInputsAsync()
        {
            var result = new Dictionary<string, object>();
            foreach (var kvp in _values)
            {
                if (kvp.Key.StartsWith(INPUT_PREFIX))
                {
                    result[kvp.Key] = kvp.Value;

                    // Also add the unprefixed version
                    var sensorName = kvp.Key.Substring(INPUT_PREFIX.Length);
                    result[sensorName] = kvp.Value;
                }
            }
            return Task.FromResult(result);
        }

        public Task<Dictionary<string, object>> GetInputsAsync()
        {
            return GetAllInputsAsync();
        }

        public Task<Dictionary<string, object>> GetOutputsAsync()
        {
            var result = new Dictionary<string, object>();
            foreach (var kvp in _values)
            {
                if (kvp.Key.StartsWith(OUTPUT_PREFIX))
                {
                    result[kvp.Key] = kvp.Value;
                }
            }
            return Task.FromResult(result);
        }

        public Task<Dictionary<string, object>> GetStateAsync()
        {
            var result = new Dictionary<string, object>();
            foreach (var kvp in _values)
            {
                if (kvp.Key.StartsWith(STATE_PREFIX))
                {
                    result[kvp.Key] = kvp.Value;
                }
            }
            return Task.FromResult(result);
        }

        public Task SetOutputsAsync(Dictionary<string, object> outputs)
        {
            if (outputs == null || outputs.Count == 0)
                return Task.CompletedTask;

            foreach (var kvp in outputs)
            {
                var redisKey = kvp.Key.StartsWith(OUTPUT_PREFIX)
                    ? kvp.Key
                    : $"{OUTPUT_PREFIX}{kvp.Key}";
                _values[redisKey] = kvp.Value;
            }

            return Task.CompletedTask;
        }

        public Task<Dictionary<string, (double Value, DateTime Timestamp)>> GetSensorValuesAsync(
            IEnumerable<string> sensorKeys
        )
        {
            var result = new Dictionary<string, (double Value, DateTime Timestamp)>();

            foreach (var sensorKey in sensorKeys)
            {
<<<<<<< HEAD
                var redisKey = sensorKey.StartsWith(INPUT_PREFIX) ? sensorKey : $"{INPUT_PREFIX}{sensorKey}";
                if (_values.TryGetValue(redisKey, out var value) &&
                    (value is double doubleValue || double.TryParse(value.ToString(), out doubleValue)))
=======
                var redisKey = sensorKey.StartsWith(INPUT_PREFIX)
                    ? sensorKey
                    : $"{INPUT_PREFIX}{sensorKey}";
                if (
                    _values.TryGetValue(redisKey, out var value)
                    && (
                        value is double doubleValue
                        || double.TryParse(value.ToString(), out doubleValue)
                    )
                )
>>>>>>> 8b7c346332a26f768b0c87b04a580da20b2811ca
                {
                    result[sensorKey] = (doubleValue, DateTime.UtcNow);
                }
            }

            return Task.FromResult(result);
        }

        public Task SetOutputValuesAsync(Dictionary<string, double> outputs)
        {
            if (outputs == null || outputs.Count == 0)
                return Task.CompletedTask;

            var convertedOutputs = new Dictionary<string, object>();
            foreach (var kvp in outputs)
            {
                convertedOutputs[kvp.Key] = kvp.Value;
            }

            return SetOutputsAsync(convertedOutputs);
        }

        public Task SetStateAsync(Dictionary<string, object> state)
        {
            if (state == null || state.Count == 0)
                return Task.CompletedTask;

            foreach (var kvp in state)
            {
                var redisKey = kvp.Key.StartsWith(STATE_PREFIX)
                    ? kvp.Key
                    : $"{STATE_PREFIX}{kvp.Key}";
                _values[redisKey] = kvp.Value;
            }

            return Task.CompletedTask;
        }

        public Task<(double Value, DateTime Timestamp)[]> GetValues(string sensor, int count)
        {
            var result = new List<(double Value, DateTime Timestamp)>();

            var sensorKey = sensor.StartsWith(INPUT_PREFIX) ? sensor : $"{INPUT_PREFIX}{sensor}";
<<<<<<< HEAD
            if (_values.TryGetValue(sensorKey, out var value) &&
                (value is double doubleValue || double.TryParse(value.ToString(), out doubleValue)))
=======
            if (
                _values.TryGetValue(sensorKey, out var value)
                && (
                    value is double doubleValue
                    || double.TryParse(value.ToString(), out doubleValue)
                )
            )
>>>>>>> 8b7c346332a26f768b0c87b04a580da20b2811ca
            {
                result.Add((doubleValue, DateTime.UtcNow));
            }

            return Task.FromResult(result.ToArray());
        }

        public async Task<long> PublishAsync(string channel, string message)
        {
            if (!_subscribers.TryGetValue(channel, out var handlers) || handlers.Count == 0)
            {
                // If there are no subscribers, return 0 as per Redis behavior
                return 0L;
            }

            // Deliver message to subscribers
            await SendMessage(channel, message);

            // Return the number of subscribers that received the message
            return handlers.Count;
        }

        public Task<bool> HashSetAsync(string key, string field, string value)
        {
            var hashKey = $"{key}:{field}";
            _values[hashKey] = value;
            return Task.FromResult(true);
        }

        public Task<string?> HashGetAsync(string key, string field)
        {
            var hashKey = $"{key}:{field}";
            if (_values.TryGetValue(hashKey, out var value))
            {
                return Task.FromResult(value?.ToString());
            }

            return Task.FromResult<string?>(null);
        }

        public Task<Dictionary<string, string>?> HashGetAllAsync(string key)
        {
            var result = new Dictionary<string, string>();
            var prefix = $"{key}:";

            foreach (var kvp in _values)
            {
                if (kvp.Key.StartsWith(prefix))
                {
                    var field = kvp.Key.Substring(prefix.Length);
                    result[field] = kvp.Value?.ToString() ?? string.Empty;
                }
            }

            return Task.FromResult(result.Count > 0 ? result : null);
        }

        public Task<bool> DeleteKeyAsync(string key)
        {
            bool removed = _values.Remove(key);

            // Also try to remove hash keys
            var prefix = $"{key}:";
            var hashKeys = _values.Keys.Where(k => k.StartsWith(prefix)).ToList();
            foreach (var hashKey in hashKeys)
            {
                _values.Remove(hashKey);
                removed = true;
            }

            return Task.FromResult(removed);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Clean up managed resources
                    _values.Clear();
                    _subscribers.Clear();
                }

                _disposed = true;
            }
        }
    }
}
