// File: Pulsar.Compiler/Config/Templates/Runtime/Models/RedisConfiguration.cs
// Version: 2.0.0

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using StackExchange.Redis;
using YamlDotNet.Serialization;

namespace Beacon.Runtime.Models
{
    /// <summary>
    /// Comprehensive unified Redis configuration for all Pulsar components
    /// </summary>
    public class RedisConfiguration
    {
        [JsonPropertyName("endpoints")]
        [YamlMember(Alias = "endpoints")]
        [JsonInclude]
        public List<string> Endpoints { get; set; } = new List<string> { "localhost:6379" };

        [JsonInclude]
        [JsonPropertyName("password")]
        [YamlMember(Alias = "password")]
        public string? Password { get; set; }

        [JsonInclude]
        [JsonPropertyName("poolSize")]
        [YamlMember(Alias = "poolSize")]
        public int PoolSize { get; set; } = Environment.ProcessorCount * 2;

        [JsonInclude]
        [JsonPropertyName("connectTimeout")]
        [YamlMember(Alias = "connectTimeout")]
        public int ConnectTimeout { get; set; } = 5000;

        [JsonInclude]
        [JsonPropertyName("syncTimeout")]
        [YamlMember(Alias = "syncTimeout")]
        public int SyncTimeout { get; set; } = 1000;

        [JsonInclude]
        [JsonPropertyName("keepAlive")]
        [YamlMember(Alias = "keepAlive")]
        public int KeepAlive { get; set; } = 60;

        [JsonInclude]
        [JsonPropertyName("retryCount")]
        [YamlMember(Alias = "retryCount")]
        public int RetryCount { get; set; } = 3;

        [JsonInclude]
        [JsonPropertyName("retryBaseDelayMs")]
        [YamlMember(Alias = "retryBaseDelayMs")]
        public int RetryBaseDelayMs { get; set; } = 100;

        [JsonInclude]
        [JsonPropertyName("ssl")]
        [YamlMember(Alias = "ssl")]
        public bool Ssl { get; set; } = false;

        [JsonInclude]
        [JsonPropertyName("allowAdmin")]
        [YamlMember(Alias = "allowAdmin")]
        public bool AllowAdmin { get; set; } = false;

        [JsonPropertyName("healthCheck")]
        [YamlMember(Alias = "healthCheck")]
        public RedisHealthCheckConfig HealthCheck { get; set; } = new RedisHealthCheckConfig();

        [JsonPropertyName("metrics")]
        [YamlMember(Alias = "metrics")]
        public RedisMetricsConfig Metrics { get; set; } = new RedisMetricsConfig();

        /// <summary>
        /// Converts the configuration to Redis connection options
        /// </summary>
        /// <returns>ConfigurationOptions for Redis connection</returns>
        public ConfigurationOptions ToRedisOptions()
        {
            var options = new ConfigurationOptions
            {
                Password = Password,
                ConnectTimeout = ConnectTimeout,
                SyncTimeout = SyncTimeout,
                KeepAlive = KeepAlive,
                AbortOnConnectFail = false,
                AllowAdmin = AllowAdmin,
                Ssl = Ssl,
                ReconnectRetryPolicy = new ExponentialRetry(RetryBaseDelayMs),
            };

            foreach (var endpoint in Endpoints)
            {
                var parts = endpoint.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[1], out int port))
                {
                    options.EndPoints.Add(parts[0], port);
                }
                else
                {
                    options.EndPoints.Add(endpoint);
                }
            }

            if (Endpoints.Count > 1)
            {
                options.ServiceName = "PulsarRedisCluster";
            }

            return options;
        }
    }

    /// <summary>
    /// Configuration for Redis health checks
    /// </summary>
    public class RedisHealthCheckConfig
    {
        [JsonPropertyName("enabled")]
        [YamlMember(Alias = "enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("intervalSeconds")]
        [YamlMember(Alias = "intervalSeconds")]
        public int HealthCheckIntervalSeconds { get; set; } = 30;

        [JsonPropertyName("failureThreshold")]
        [YamlMember(Alias = "failureThreshold")]
        public int FailureThreshold { get; set; } = 5;

        [JsonPropertyName("timeoutMs")]
        [YamlMember(Alias = "timeoutMs")]
        public int TimeoutMs { get; set; } = 2000;
    }

    /// <summary>
    /// Configuration for Redis metrics collection
    /// </summary>
    public class RedisMetricsConfig
    {
        [JsonPropertyName("enabled")]
        [YamlMember(Alias = "enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("instanceName")]
        [YamlMember(Alias = "instanceName")]
        public string InstanceName { get; set; } = "default";

        [JsonPropertyName("samplingIntervalSeconds")]
        [YamlMember(Alias = "samplingIntervalSeconds")]
        public int SamplingIntervalSeconds { get; set; } = 60;
    }
}