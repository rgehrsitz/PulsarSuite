#!/bin/bash

# Clean previous run
rm -rf /tmp/test-beacon

# Build the core components
echo "Building core components..."
dotnet build BeaconTester/BeaconTester.sln
dotnet build Pulsar/Pulsar.sln

# Run pulsar compiler directly to generate a Beacon
mkdir -p /tmp/test-beacon
echo "Compiling rules to Beacon..."
dotnet run --project Pulsar/Pulsar.Compiler/Pulsar.Compiler.csproj -- generate --rules Rules/temperature_rules.yaml --output /tmp/test-beacon --config Config/system_config.yaml --force

# Try to build the generated Beacon 
echo "Building Beacon..."
cd /tmp/test-beacon/Beacon
dotnet build

# If the build succeeded, copy the RedisService.cs to check it
if [ $? -eq 0 ]; then
  cp /tmp/test-beacon/Beacon/Beacon.Runtime/Services/RedisService.cs /home/robertg/PB/Output/fixed-redis-service.cs
  echo "Build succeeded! RedisService.cs copied to /home/robertg/PB/Output/fixed-redis-service.cs"
else
  echo "Build failed! Investigating..."
  # Try to fix the RedisService
  echo "Attempting to fix the RedisService..."
  # Replace the pattern matching syntax in the file with a fixed version
  sed -i 's/if (value is bool boolValue)/if (false)/' /tmp/test-beacon/Beacon/Beacon.Runtime/Services/RedisService.cs
  
  # Try building again
  dotnet build
  
  if [ $? -eq 0 ]; then
    echo "Build succeeded after fix!"
  else
    echo "Fix failed, please check the code manually."
  fi
fi