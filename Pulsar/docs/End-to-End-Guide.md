# End-to-End Guide: From YAML Rules to Running Beacon

## Table of Contents

- [Creating Custom YAML Rules](#creating-custom-yaml-rules)
- [Creating System Configuration](#creating-system-configuration)
- [Compiling with Pulsar](#compiling-with-pulsar)
- [CLI Reference](#cli-reference)
- [AOT Implementation](#aot-implementation)
- [Testing Guide](#testing-guide)
- [Building the Solution](#building-the-solution)
- [Creating a Standalone Executable (Optional)](#creating-a-standalone-executable-optional)

## Creating Custom YAML Rules

First, create a YAML file containing your rule definitions in the `rules/` directory. Rules should follow this structure:

```yaml
rules:
  - name: "SimpleRule"
    description: "Simple temperature threshold rule"
    conditions:
      all:
        - condition:
            type: comparison
            sensor: input:temperature
            operator: greater_than
            value: 30
    actions:
      - set_value:
          key: output:temp_alert
          value: 1
```

Save this as `rules/my-rules.yaml`. For more examples, refer to [Rules-Engine.md](Rules-Engine.md) or examine the files in the `rules/` directory.

## Creating System Configuration

Create a system configuration file (`config/system_config.yaml`) that includes:

```yaml
version: 1
# validSensors is optional - will be auto-populated from your rules
cycleTime: 100  # milliseconds between cycles
redis:
  endpoints:
    - localhost:6379  # Replace with your Redis instance
  poolSize: 4
  retryCount: 3
  retryBaseDelayMs: 100
  connectTimeout: 5000
  syncTimeout: 1000
  keepAlive: 60
  password: null  # Set your Redis password if needed
  ssl: false
  allowAdmin: false
  healthCheck:
    enabled: true
    intervalSeconds: 30
    failureThreshold: 5
    timeoutMs: 2000
  metrics:
    enabled: true
    instanceName: default
    samplingIntervalSeconds: 60
bufferCapacity: 100  # For temporal rules
logLevel: Information  # Optional logging level
logFile: logs/pulsar.log  # Optional log file path
```

**Note**: The `validSensors` list is now optional. The system will automatically scan all rule files in the `rules/` directory and extract all unique sensors for validation and build.

## Compiling with Pulsar

### Automated Workflow (Recommended)

Use the main build script for the complete workflow:

```bash
./scripts/build-and-test.sh
```

This script:
1. Builds Pulsar Compiler
2. Builds BeaconTester
3. Compiles rules using Pulsar
4. Builds Beacon runtime
5. Runs BeaconTester (if test scenarios exist)
6. Generates test reports

### Manual Commands

You can manually validate, compile, and generate the Beacon solution using dotnet CLI:

```bash
# 1. Validate rules
dotnet run --project Pulsar/Pulsar.Compiler/Pulsar.Compiler.csproj validate --rules=rules/my-rules.yaml --config=config/system_config.yaml

# 2. Compile rules
dotnet run --project Pulsar/Pulsar.Compiler/Pulsar.Compiler.csproj compile --rules=rules/my-rules.yaml --config=config/system_config.yaml --output=output/Bin

# 3. Generate Beacon solution
dotnet run --project Pulsar/Pulsar.Compiler/Pulsar.Compiler.csproj beacon --rules=rules/my-rules.yaml --output=MyBeacon --config=config/system_config.yaml --target=linux-x64
```

Or, if you have a published standalone version of Pulsar:

```bash
Pulsar.Compiler beacon --rules=rules/my-rules.yaml --config=config/system_config.yaml --output=MyBeacon
```

### Project Structure

The new consolidated structure is:

```
PulsarSuite/
├── rules/                    # All rule definitions
│   ├── temperature_rules.yaml
│   ├── advanced_rules.yaml
│   └── my-rules.yaml
├── config/                   # System configuration
│   └── system_config.yaml    # Single config for all rules
├── scripts/                  # Build and utility scripts
│   ├── build-and-test.sh     # Main build script
│   └── consolidate-rules.sh  # Rules consolidation script
├── Pulsar/                   # Rules compiler
├── BeaconTester/             # Testing framework
├── output/                   # Build outputs
└── examples/                 # Example test scenarios
```

### Required Template Files

Pulsar requires specific template files to be present in order to generate code correctly. These templates are searched for in the following locations:

1. `Pulsar.Compiler/Config/Templates/` (direct path from working directory)
2. Relative to the compiler assembly location
3. Relative to the current directory

When using Pulsar from source, these files are automatically available. For compiled or published versions, ensure the templates are copied alongside the executable.

### Template Directory Structure

The required template structure is:

```bash
Pulsar.Compiler/Config/Templates/
├── Interfaces/
│   ├── ICompiledRules.cs
│   ├── IRedisService.cs
│   ├── IRuleCoordinator.cs
│   └── IRuleGroup.cs
├── Program.cs
├── Project/
│   ├── Generated.sln
│   ├── Runtime.csproj
│   └── trimming.xml
├── Runtime/
│   ├── Buffers/
│   │   ├── CircularBuffer.cs
│   │   ├── IDateTimeProvider.cs
│   │   ├── RingBufferManager.cs
│   │   └── SystemDateTimeProvider.cs
│   ├── Models/
│   │   ├── RedisConfiguration.cs
│   │   └── RuntimeConfig.cs
│   ├── Rules/
│   │   └── RuleBase.cs
│   ├── RuntimeOrchestrator.cs
│   └── Services/
│       ├── RedisConfiguration.cs
│       ├── RedisHealthCheck.cs
│       ├── RedisLoggingConfiguration.cs
│       ├── RedisMetrics.cs
│       ├── RedisMonitoring.cs
│       └── RedisService.cs
└── RuntimeConfig.cs
```

### Compiling Your Rules

Run the Pulsar compiler to generate a Beacon solution:

```bash
# 1. Validate rules
dotnet run --project Pulsar.Compiler -- validate --rules=rules/my-rules.yaml --config=config/system_config.yaml

# 2. Compile rules
dotnet run --project Pulsar.Compiler -- compile --rules=rules/my-rules.yaml --config=config/system_config.yaml --output=output/Bin

# 3. Generate Beacon solution
dotnet run --project Pulsar.Compiler -- beacon --rules=rules/my-rules.yaml --output=MyBeacon --config=config/system_config.yaml --target=linux-x64
```

If using a standalone published version of Pulsar:

```bash
Pulsar.Compiler beacon --rules=rules/my-rules.yaml --config=config/system_config.yaml --output=MyBeacon
```

This will create a directory structure in `MyBeacon/` containing:

- `Beacon/` - The main solution directory
  - `Beacon.sln` - The solution file
  - `Beacon.Runtime/` - The runtime project
  - `Beacon.Tests/` - Test project

### Version Control Considerations

When working with Pulsar and Beacon in a version-controlled environment, follow these best practices:

1. **Include in Version Control**:
   - Rule definition files (YAML) in `rules/`
   - System configuration files (YAML) in `config/`
   - Custom scripts for running the compiler
   - Documentation

2. **Exclude from Version Control**:
   - Generated output directories (e.g., `MyBeacon/`)
   - Build artifacts (bin, obj folders)
   - Log files

The standard `.gitignore` for Pulsar already excludes these generated directories. If you need to share generated code with others, consider documenting the exact command used to generate it rather than committing the generated files themselves.

## Building the Solution

Build the generated Beacon solution:

```bash
cd MyBeacon/Beacon
dotnet build
```

Or, for an optimized release build:

```bash
dotnet build -c Release
```

## Creating a Standalone Executable (Optional)

To create a standalone executable optimized for your platform:

```bash
# For Windows
dotnet publish -c Release -r win-x64 --self-contained true

# For Linux
dotnet publish -c Release -r linux-x64 --self-contained true

# For macOS
dotnet publish -c Release -r osx-x64 --self-contained true
```

The executable will be generated in:
`Beacon.Runtime/bin/Release/net9.0/<runtime>/publish/`

## Running the Beacon Application

### Basic Execution

To run the compiled Beacon application:

```bash
# For debug build
dotnet run --project Beacon.Runtime

# For published standalone executable
cd Beacon.Runtime/bin/Release/net9.0/<runtime>/publish/
./Beacon.Runtime  # Linux/macOS
Beacon.Runtime.exe  # Windows
```

### Command-Line Arguments

The Beacon application supports these command-line arguments:

```bash
--config=<path>     Path to a custom runtime configuration JSON file
--redis=<endpoint>  Override the Redis endpoint (format: host:port)
--verbose           Enable verbose logging
--metrics           Enable metrics collection
--interval=<ms>     Override the rule evaluation interval in milliseconds
```

Example:

```bash
./Beacon.Runtime --redis=myredis.example.com:6379 --verbose --interval=200
```

### Runtime Configuration File (Optional)

For advanced settings, create a runtime configuration JSON file:

```json
{
  "redis": {
    "endpoints": ["redis.example.com:6379"],
    "poolSize": 8,
    "retryCount": 3,
    "retryBaseDelayMs": 100,
    "connectTimeout": 5000,
    "syncTimeout": 1000,
    "keepAlive": 60,
    "password": "your-password-here",
    "ssl": true,
    "allowAdmin": false,
    "healthCheck": {
      "enabled": true,
      "intervalSeconds": 30,
      "failureThreshold": 5,
      "timeoutMs": 2000
    },
    "metrics": {
      "enabled": true,
      "instanceName": "production",
      "samplingIntervalSeconds": 60
    }
  },
  "cycleTime": 100,
  "bufferCapacity": 100,
  "logLevel": "Information",
  "logFile": "beacon-logs.txt"
}
```

Run with this configuration:

```bash
./Beacon.Runtime --config=runtime-config.json
```

## Testing Your Rules

### Adding Test Data to Redis

You can use redis-cli to add test data:

```bash
redis-cli SET temperature 25
redis-cli SET humidity 60
```

### Verifying Rule Execution

Check if rules execute correctly by examining the outputs:

```bash
redis-cli GET temp_alert
```

### Monitoring Execution

Enable verbose logging to monitor rule execution:

```bash
./Beacon.Runtime --verbose
```

## Deployment Considerations

### Docker Container Deployment

#### For Beacon Runtime

For containerized deployment of a compiled Beacon application:

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY ./Beacon.Runtime/bin/Release/net9.0/linux-x64/publish/ .
ENTRYPOINT ["./Beacon.Runtime", "--redis=redis-host:6379"]
```

Build and run:

```bash
docker build -t beacon-app .
docker run -d --name beacon beacon-app
```

#### For Pulsar Compiler with Templates

For containerized deployment of the Pulsar compiler with all required templates:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

# Copy csproj files and restore dependencies
COPY *.sln .
COPY Pulsar.Compiler/*.csproj ./Pulsar.Compiler/
COPY Pulsar.Runtime/*.csproj ./Pulsar.Runtime/
RUN dotnet restore

# Copy source code
COPY Pulsar.Compiler/. ./Pulsar.Compiler/
COPY Pulsar.Runtime/. ./Pulsar.Runtime/

# Publish
RUN dotnet publish -c Release -o /app Pulsar.Compiler/Pulsar.Compiler.csproj

FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY --from=build /app ./
# IMPORTANT: Copy templates to the expected location
COPY --from=build /source/Pulsar.Compiler/Config/Templates ./Pulsar.Compiler/Config/Templates/

# Add sample files
COPY rules/sample_rules.yaml ./examples/
COPY config/system_config.yaml ./examples/

ENTRYPOINT ["./Pulsar.Compiler"]
CMD ["--help"]
```

With this Docker image, you can compile rules like this:

```bash
docker run -v $(pwd):/data pulsar-compiler beacon --rules=/data/my-rules.yaml --config=/data/system_config.yaml --output=/data/output
```

### Environment Variables

The runtime also supports environment variables for configuration:

- `BEACON_REDIS_ENDPOINT`: Redis endpoint
- `BEACON_CYCLE_TIME`: Cycle time in milliseconds
- `BEACON_LOG_LEVEL`: Logging level

## Troubleshooting

### Common Issues

1. **Redis Connection Problems**:
   - Verify Redis is running: `redis-cli ping`
   - Check connection settings in config file
   - Ensure network connectivity between Beacon and Redis

2. **Rule Execution Issues**:
   - Enable verbose logging to see rule evaluation
   - Verify sensors exist in Redis with correct format
   - Check for typos in sensor names

3. **Compilation Errors**:
   - Validate YAML syntax
   - Ensure all referenced sensors are in `validSensors`
   - Check for circular dependencies in rules

### Logs

Examine the application logs for detailed error information:

```bash
./Beacon.Runtime --verbose > beacon.log 2>&1
```

## Advanced Topics

### Creating a Self-Contained Pulsar Distribution

To create a self-contained distribution of Pulsar that includes all necessary templates:

```bash
# 1. Build Pulsar.Compiler as a self-contained application
dotnet publish -c Release -r <runtime> --self-contained true Pulsar.Compiler/Pulsar.Compiler.csproj

# 2. Create a distribution folder
mkdir -p PulsarDist

# 3. Copy the compiler and essential files
cp -r Pulsar.Compiler/bin/Release/net9.0/<runtime>/publish/* PulsarDist/
cp -r Pulsar.Compiler/Config/Templates PulsarDist/Pulsar.Compiler/Config/

# Create examples directory
mkdir -p PulsarDist/examples/

# 4. Include example files for reference
cp Examples/BasicRules/temperature_rules.yaml PulsarDist/examples/
cp Examples/BasicRules/system_config.yaml PulsarDist/examples/
cp Examples/BasicRules/reference_config.yaml PulsarDist/examples/
cp Examples/BasicRules/test_run.sh PulsarDist/examples/
cp Examples/README.md PulsarDist/examples/
```

You can now zip this folder and deploy it to any compatible system. The templates will be accessible from the expected relative paths.

### High Availability Setup

For production environments, consider:

- Using Redis Cluster or Redis Sentinel
- Deploying multiple Beacon instances with load balancing
- Setting up monitoring and alerting

See [Redis-Integration.md](Redis-Integration.md) for detailed Redis configuration.

### Performance Tuning

For optimal performance:

- Adjust Redis poolSize based on your workload
- Optimize cycleTime for your specific use case
- Consider adding more instances for high-throughput applications

### Custom Extensions

Consult the testing guide for adding custom functionality to the Beacon runtime.

### Template Customization

Advanced users can customize the templates to:

- Add custom monitoring integration
- Modify the Redis service implementation
- Extend the CircularBuffer implementation
- Add custom metrics collection

To customize templates:

1. Copy the original templates from `Pulsar.Compiler/Config/Templates/`
2. Modify as needed
3. Place them in the same relative path structure
4. Run Pulsar with the `--template-path=<your-templates-dir>` option

## CLI Reference

### Basic Usage
```bash
dotnet run --project Pulsar.Compiler -- <command> [options]
```

### Commands

- **beacon**: Generate Beacon solution
  ```bash
  dotnet run --project Pulsar.Compiler -- beacon --rules=<rules> --config=<config> --output=<dir> [--target=<runtime>] [--verbose]
  ```
- **compile**: Compile rules into project
- **validate**: Validate rules only
- **test**: Run rule tests
- **init**: Initialize new project
- **generate**: Generate project with defaults

### Common Options

- `--rules <path>`: Path to rule YAML (required)
- `--config <path>`: System config YAML
- `--output <path>`: Output directory
- `--target <runtime>`: Runtime ID (win-x64, linux-x64)
- `--verbose`: Verbose logging

### Examples
```bash
# Beacon
dotnet run --project Pulsar.Compiler -- beacon --rules=my-rules.yaml --config=system_config.yaml --output=MyBeacon

# Test only
dotnet run --project Pulsar.Compiler -- test --rules=my-rules.yaml --config=system_config.yaml
```

## AOT Implementation

Pulsar now outputs fully AOT-compatible Beacon projects via template-based code generation.

### Overview

- Transitioned from Pulsar.Runtime to templates in `Pulsar.Compiler/Config/Templates`
- Generates standalone C# solution with runtime, tests, and trimming support

### Goals

- AOT compatibility & trimming
- Standalone execution
- Enhanced debugging traceability

### Key Components

1. **CodeGenerator** with fixed generators for AOT
2. **BeaconTemplateManager**: scaffolds solution & projects
3. **BeaconBuildOrchestrator**: generates and builds via dotnet CLI
4. **Enhanced Redis Integration**
5. **Temporal Buffer** supporting object values

### Status

- **Completed**: Template migration, AOT compatibility, Redis, testing
- **In Progress**: Documentation updates (now done), performance tuning

## Testing Guide

This section consolidates testing instructions from the Testing Guide.

### Test Categories

- Rule parsing & validation
- AOT compilation tests
- Runtime execution (Beacon + Redis)
- Performance & memory usage
- Temporal rule behavior tests

### End-to-End Tests via MSBuild

```bash
dotnet msbuild build/PulsarSuite.core.build /t:RunEndToEnd -p:ProjectName=MyProject
```

### Running Tests

```bash
dotnet test --filter "Category=Integration"
```

### Testing with Redis

Uses TestContainers to spin up Redis; requires Docker.
