// File: Pulsar.Compiler/Config/Templates/Runtime/Services/RedisService.cs

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Beacon.Runtime.Interfaces;
using Beacon.Runtime.Models;
using Polly;
using Polly.Retry;
using Serilog;
using StackExchange.Redis;

namespace Beacon.Runtime.Services
{
    public class RedisService : IRedisService, IDisposable
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, DateTime> _lastErrorTime = new();
        private readonly TimeSpan _errorThrottleWindow = TimeSpan.FromSeconds(60);
        private readonly ConnectionMultiplexer[] _connectionPool;
        private readonly Random _random = new();
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly ConfigurationOptions _redisOptions;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);
        private readonly RedisMetrics _redisMetrics;
        private readonly MetricsService? _metrics;
        private readonly RedisHealthCheck? _healthCheck;
        private bool _disposed;

        // Redis key prefixes - standardized domain-prefix naming convention
        private const string INPUT_PREFIX = "input:";
        private const string OUTPUT_PREFIX = "output:";
        private const string STATE_PREFIX = "state:";
        private const string BUFFER_PREFIX = "buffer:";

        public bool IsHealthy => _healthCheck?.IsHealthy ?? true;

        public RedisService(RedisConfiguration config, ILogger logger, MetricsService? metrics = null)
        {
            _logger = logger.ForContext<RedisService>();
            _metrics = metrics;

            // Default connection pool setup
            var poolSize = config.PoolSize > 0 ? config.PoolSize : 5;
            _connectionPool = new ConnectionMultiplexer[poolSize];

            _redisOptions = new ConfigurationOptions
            {
                AbortOnConnectFail = false,
                ConnectTimeout = config.ConnectTimeout,
                SyncTimeout = config.SyncTimeout,
                Password = config.Password,
                Ssl = config.Ssl,
                AllowAdmin = config.AllowAdmin,
            };

            // Add all endpoints
            foreach (var endpoint in config.Endpoints)
            {
                _redisOptions.EndPoints.Add(endpoint);
            }

            // Configure retry policy
            _retryPolicy = Policy
                .Handle<RedisConnectionException>()
                .Or<RedisTimeoutException>()
                .Or<RedisServerException>()
                .WaitAndRetryAsync(
                    config.RetryCount,
                    retryAttempt => TimeSpan.FromMilliseconds(config.RetryBaseDelayMs * Math.Pow(2, retryAttempt - 1)),
                    (ex, timeSpan, retryCount, _) => {
                        if (!ShouldThrottleError(ex.ToString()))
                        {
                            _logger.Warning(ex, "Redis operation failed, retrying ({RetryCount}/{MaxRetries}) in {DelayMs}ms",
                                retryCount, config.RetryCount, timeSpan.TotalMilliseconds);
                        }
                    }
                );

            // Create the metrics
            _redisMetrics = new RedisMetrics(_metrics);

            // Create the health check - passing this service instance to the health check
            _healthCheck = new RedisHealthCheck(this, _logger);

            // Initialize the connection pool
            try
            {
                for (int i = 0; i < _connectionPool.Length; i++)
                {
                    _connectionPool[i] = ConnectionMultiplexer.Connect(_redisOptions);
                }
                _logger.Information("Redis connection pool initialized with {PoolSize} connections", _connectionPool.Length);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initialize Redis connection pool");
            }
        }

        /// <summary>
        /// Gets all input values from Redis
        /// </summary>
        /// <returns>Dictionary of input values</returns>
        public async Task<Dictionary<string, object>> GetAllInputsAsync()
        {
            return await ProcessRedisKeysAsync(INPUT_PREFIX);
        }

        public async Task<Dictionary<string, object>> GetInputsAsync()
        {
            return await GetAllInputsAsync();
        }

        public async Task<Dictionary<string, object>> GetOutputsAsync()
        {
            return await ProcessRedisKeysAsync(OUTPUT_PREFIX);
        }

        public async Task<Dictionary<string, object>> GetStateAsync()
        {
            return await ProcessRedisKeysAsync(STATE_PREFIX);
        }

        /// <summary>
        /// Gets specific sensor values from Redis
        /// </summary>
        /// <param name="sensorKeys">List of sensor keys to retrieve</param>
        /// <returns>Dictionary of sensor values with timestamps</returns>
        public async Task<Dictionary<string, (double Value, DateTime Timestamp)>> GetSensorValuesAsync(
            IEnumerable<string> sensorKeys)
        {
            var result = new Dictionary<string, (double Value, DateTime Timestamp)>();
            
            try
            {
                _metrics?.RedisOperationStarted("GetSensorValues");
                var connection = GetConnection();
                var db = connection.GetDatabase();

                foreach (var sensorKey in sensorKeys)
                {
                    try
                    {
                        // Apply prefix if not already present
                        var redisKey = sensorKey.StartsWith(INPUT_PREFIX) ? sensorKey : $"{INPUT_PREFIX}{sensorKey}";
                        var value = await db.StringGetAsync(redisKey);
                        
                        if (value.HasValue && double.TryParse(value.ToString(), out var doubleValue))
                        {
                            result[sensorKey] = (doubleValue, DateTime.UtcNow);
                            _logger.Debug("Retrieved sensor value {Key} = {Value}", sensorKey, doubleValue);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "Failed to get sensor value for {SensorKey}", sensorKey);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting sensor values");
            }
            finally
            {
                _metrics?.RedisOperationCompleted("GetSensorValues");
            }
            
            return result;
        }

        public async Task SetOutputsAsync(Dictionary<string, object> outputs)
        {
            if (outputs == null || outputs.Count == 0)
                return;

            try
            {
                _metrics?.RedisOperationStarted("SetOutputs");
                var connection = GetConnection();
                var db = connection.GetDatabase();

                foreach (var kvp in outputs)
                {
                    if (kvp.Value == null) continue;
                    
                    var redisKey = kvp.Key.StartsWith(OUTPUT_PREFIX) ? kvp.Key : $"{OUTPUT_PREFIX}{kvp.Key}";
                    try
                    {
                        await db.StringSetAsync(redisKey, kvp.Value.ToString());
                        _logger.Debug("Set output key {Key} = {Value}", redisKey, kvp.Value);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to set output key {Key}", redisKey);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error setting outputs");
            }
            finally
            {
                _metrics?.RedisOperationCompleted("SetOutputs");
            }
        }

        /// <summary>
        /// Sets output values in Redis
        /// </summary>
        /// <param name="outputs">Dictionary of output values</param>
        /// <returns>Task representing the asynchronous operation</returns>
        public async Task SetOutputValuesAsync(Dictionary<string, double> outputs)
        {
            if (outputs == null || outputs.Count == 0)
                return;

            var convertedOutputs = new Dictionary<string, object>();
            foreach (var kvp in outputs)
            {
                convertedOutputs[kvp.Key] = kvp.Value;
            }

            await SetOutputsAsync(convertedOutputs);
        }

        public async Task SetStateAsync(Dictionary<string, object> state)
        {
            if (state == null || state.Count == 0)
                return;

            try
            {
                _metrics?.RedisOperationStarted("SetState");
                var connection = GetConnection();
                var db = connection.GetDatabase();

                foreach (var kvp in state)
                {
                    if (kvp.Value == null) continue;
                    
                    var redisKey = kvp.Key.StartsWith(STATE_PREFIX) ? kvp.Key : $"{STATE_PREFIX}{kvp.Key}";
                    try
                    {
                        await db.StringSetAsync(redisKey, kvp.Value.ToString());
                        _logger.Debug("Set state key {Key} = {Value}", redisKey, kvp.Value);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to set state key {Key}", redisKey);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error setting state");
            }
            finally
            {
                _metrics?.RedisOperationCompleted("SetState");
            }
        }

        /// <summary>
        /// Gets the values for a sensor over time
        /// </summary>
        /// <param name="sensor">The sensor key</param>
        /// <param name="count">Number of historical values to retrieve</param>
        /// <returns>Array of historical values</returns>
        public async Task<(double Value, DateTime Timestamp)[]> GetValues(string sensor, int count)
        {
            try
            {
                _metrics?.RedisOperationStarted("GetValues");
                
                // For this simplified implementation, we'll just return the current value
                // In a real implementation, this would use a time-series database or Redis sorted sets
                var sensorValues = await GetSensorValuesAsync(new[] { sensor });
                
                if (sensorValues.TryGetValue(sensor, out var value))
                {
                    return new[] { value };
                }
                
                return Array.Empty<(double, DateTime)>();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get historical values for {Sensor}", sensor);
                return Array.Empty<(double, DateTime)>();
            }
            finally
            {
                _metrics?.RedisOperationCompleted("GetValues");
            }
        }

        /// <summary>
        /// Publishes a message to a Redis channel
        /// </summary>
        /// <param name="channel">The channel to publish to</param>
        /// <param name="message">The message to publish</param>
        /// <returns>The number of clients that received the message</returns>
        public async Task<long> PublishAsync(string channel, string message)
        {
            try
            {
                _metrics?.RedisOperationStarted("Publish");
                var connection = GetConnection();
                var subscriber = connection.GetSubscriber();
                return await subscriber.PublishAsync(channel, message);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to publish message to channel {Channel}", channel);
                return 0;
            }
            finally
            {
                _metrics?.RedisOperationCompleted("Publish");
            }
        }

        public async Task<bool> HashSetAsync(string key, string field, string value)
        {
            try
            {
                var connection = GetConnection();
                var db = connection.GetDatabase();
                return await db.HashSetAsync(key, field, value);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to set hash field {Key}.{Field}", key, field);
                return false;
            }
        }

        public async Task<string?> HashGetAsync(string key, string field)
        {
            try
            {
                var connection = GetConnection();
                var db = connection.GetDatabase();
                var result = await db.HashGetAsync(key, field);
                return result.HasValue ? result.ToString() : null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get hash field {Key}.{Field}", key, field);
                return null;
            }
        }

        public async Task<bool> DeleteKeyAsync(string key)
        {
            try
            {
                var connection = GetConnection();
                var db = connection.GetDatabase();
                return await db.KeyDeleteAsync(key);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete key {Key}", key);
                return false;
            }
        }

        public async Task<Dictionary<string, string>?> HashGetAllAsync(string key)
        {
            try
            {
                var connection = GetConnection();
                var db = connection.GetDatabase();
                var entries = await db.HashGetAllAsync(key);
                return entries.ToDictionary(he => he.Name.ToString(), he => he.Value.ToString());
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get all hash fields for {Key}", key);
                return null;
            }
        }

        // Helper method to get a connection from the pool
        private ConnectionMultiplexer GetConnection()
        {
            var index = _random.Next(0, _connectionPool.Length);
            var connection = _connectionPool[index];

            if (connection == null || !connection.IsConnected)
            {
                try
                {
                    _connectionLock.Wait();
                    try
                    {
                        if (connection == null || !connection.IsConnected)
                        {
                            connection = ConnectionMultiplexer.Connect(_redisOptions);
                            _connectionPool[index] = connection;
                        }
                    }
                    finally
                    {
                        _connectionLock.Release();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to reconnect to Redis");
                }
            }

            return connection;
        }

        // Process Redis keys with a specific prefix
        private async Task<Dictionary<string, object>> ProcessRedisKeysAsync(string prefix)
        {
            _metrics?.RedisOperationStarted("ProcessRedisKeys");
            var result = new Dictionary<string, object>();
            
            try
            {
                var connection = GetConnection();
                var db = connection.GetDatabase();

                // Simple implementation that just gets keys by pattern
                var keys = await db.ExecuteAsync("KEYS", $"{prefix}*");
                var keyArray = (RedisKey[])keys;

                foreach (var key in keyArray)
                {
                    var keyStr = key.ToString();
                    try
                    {
                        var keyType = db.KeyType(keyStr);
                        if (keyType == RedisType.String)
                        {
                            var value = await db.StringGetAsync(keyStr);
                            if (!value.IsNull)
                            {
                                // Add the value to results with proper type conversion
                                if (bool.TryParse(value.ToString(), out var boolValue))
                                {
                                    result[keyStr] = boolValue;
                                }
                                else if (double.TryParse(value.ToString(), out var doubleValue))
                                {
                                    result[keyStr] = doubleValue;
                                }
                                else
                                {
                                    result[keyStr] = value.ToString();
                                }

                                // For input keys only, also add unprefixed version
                                if (prefix == INPUT_PREFIX)
                                {
                                    var sensorName = keyStr.Substring(prefix.Length);
                                    result[sensorName] = result[keyStr];
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "Failed to process Redis key {Key}", keyStr);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error processing Redis keys with prefix {Prefix}", prefix);
            }
            finally
            {
                _metrics?.RedisOperationCompleted("ProcessRedisKeys");
            }

            return result;
        }

        // Check if we should throttle error logging
        private bool ShouldThrottleError(string errorKey)
        {
            var now = DateTime.UtcNow;
            if (_lastErrorTime.TryGetValue(errorKey, out var lastError))
            {
                if (now - lastError < _errorThrottleWindow)
                {
                    return true;
                }
            }

            _lastErrorTime[errorKey] = now;
            return false;
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
                    _connectionLock.Dispose();
                    foreach (var connection in _connectionPool)
                    {
                        connection?.Dispose();
                    }
                }

                _disposed = true;
            }
        }
    }
}
