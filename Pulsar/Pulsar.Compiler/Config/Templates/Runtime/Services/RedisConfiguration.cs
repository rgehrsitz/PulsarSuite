// File: Pulsar.Compiler/Config/Templates/Runtime/Services/RedisConfiguration.cs
// Version: 2.0.0

using System;
using Beacon.Runtime.Models;

// This file is kept for backward compatibility only
// It redirects to the consolidated RedisConfiguration in the Models namespace
namespace Beacon.Runtime.Services
{
    // Aliases the primary configuration class in Models namespace to maintain compatibility
    public class RedisConfiguration : Beacon.Runtime.Models.RedisConfiguration
    {
        // Note: This class is maintained for backward compatibility only
        // All functionality is now provided by Beacon.Runtime.Models.RedisConfiguration
        // This class will be removed in a future release
    }
}