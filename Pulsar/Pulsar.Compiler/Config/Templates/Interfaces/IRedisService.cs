// File: Pulsar.Compiler/Config/Templates/Interfaces/IRedisService.cs
// Version: 1.1.0

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Beacon.Runtime.Interfaces
{
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
    }
}
