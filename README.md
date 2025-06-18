# PulsarSuite - Rules Engine Development Environment

This repository provides a development environment for Pulsar/Beacon rule-based applications. The system consists of:

- **Pulsar**: A compiler/code generator that takes YAML rules, validates them, optimizes them, then generates code
- **Beacon**: A standalone executable that runs the compiled rules and interfaces with Redis
- **BeaconTester**: An automated testing framework that validates Beacon applications

## Project Structure

```bash
PulsarSuite/
├── src/                 # Source code and input files
│   └── Rules/          # Rule definitions
│       └── [ProjectName]/
│           ├── rules/  # YAML rule files
│           └── config/ # Configuration files
├── Pulsar/             # Pulsar compiler source
├── BeaconTester/       # BeaconTester source
├── output/             # Build outputs
│   ├── Bin/           # Compiled rules
│   ├── dist/          # Distributable Beacon apps
│   ├── Tests/         # Generated test scenarios
│   └── reports/       # Test reports
└── scripts/           # Build and test scripts
```

## Prerequisites

- .NET SDK 8.0 or higher (install from <https://dotnet.microsoft.com/download>)
- Redis server (required by Beacon runtime)
  - You can run Redis locally or via Docker

## Quick Start

### Option 1: Automated Script (Recommended)

Use the simplified build script for the complete workflow:

```bash
# Full end-to-end workflow
./scripts/build-and-test.sh TemperatureExample

# Or for a different project
./scripts/build-and-test.sh ThresholdOverTimeExample
```

### Option 2: Manual Commands

If you prefer manual control, follow these steps:

```bash
# 1. Validate rules
dotnet run --project Pulsar/Pulsar.Compiler/Pulsar.Compiler.csproj validate \
    --rules=src/Rules/TemperatureExample/rules/temperature_rules.yaml \
    --config=src/Rules/TemperatureExample/config/system_config.yaml

# 2. Compile rules
dotnet run --project Pulsar/Pulsar.Compiler/Pulsar.Compiler.csproj compile \
    --rules=src/Rules/TemperatureExample/rules/temperature_rules.yaml \
    --config=src/Rules/TemperatureExample/config/system_config.yaml \
    --output=output/Bin/TemperatureExample

# 3. Generate Beacon solution
dotnet run --project Pulsar/Pulsar.Compiler/Pulsar.Compiler.csproj beacon \
    --rules=src/Rules/TemperatureExample/rules/temperature_rules.yaml \
    --compiled-rules-dir=output/Bin/TemperatureExample \
    --output=output/dist/TemperatureExample \
    --config=src/Rules/TemperatureExample/config/system_config.yaml \
    --target=linux-x64

# 4. Build Beacon runtime
dotnet publish output/dist/TemperatureExample/Beacon/Beacon.Runtime/Beacon.Runtime.csproj \
    -c Debug -r linux-x64 --self-contained true /p:PublishSingleFile=true

# 5. Generate test scenarios
dotnet run --project BeaconTester/BeaconTester.Runner/BeaconTester.Runner.csproj generate \
    --rules=src/Rules/TemperatureExample/rules/temperature_rules.yaml \
    --output=output/Tests/TemperatureExample/test_scenarios.json

# 6. Start Beacon runtime (in a separate terminal)
cd output/dist/TemperatureExample/Beacon/Beacon.Runtime/bin/Debug/net8.0/linux-x64
dotnet Beacon.Runtime.dll --redis-host=localhost --redis-port=6379 --verbose

# 7. Run tests (in another terminal)
dotnet run --project BeaconTester/BeaconTester.Runner/BeaconTester.Runner.csproj run \
    --scenarios=output/Tests/TemperatureExample/test_scenarios.json \
    --output=output/reports/TemperatureExample/test_results.json \
    --redis-host=localhost --redis-port=6379
```

## Build Script Usage

The `scripts/build-and-test.sh` script provides several options:

```bash
# Full workflow (default)
./scripts/build-and-test.sh TemperatureExample

# Individual steps
./scripts/build-and-test.sh TemperatureExample clean      # Clean environment
./scripts/build-and-test.sh TemperatureExample validate   # Validate rules only
./scripts/build-and-test.sh TemperatureExample compile    # Compile rules only
./scripts/build-and-test.sh TemperatureExample build      # Build Beacon only
./scripts/build-and-test.sh TemperatureExample test       # Run tests only

# Beacon management
./scripts/build-and-test.sh TemperatureExample start-beacon  # Start Beacon
./scripts/build-and-test.sh TemperatureExample stop-beacon   # Stop Beacon
```

## Output Locations

- Compiled rules: `output/Bin/[ProjectName]/`
- Distributable Beacon: `output/dist/[ProjectName]/`
- Published Beacon runtime: `output/dist/[ProjectName]/Beacon/Beacon.Runtime/bin/Debug/net8.0/linux-x64/publish/Beacon.Runtime`
- Test scenarios: `output/Tests/[ProjectName]/test_scenarios.json`
- Test results: `output/reports/[ProjectName]/test_results.json`
- Beacon logs: `output/reports/[ProjectName]/beacon.log`

## Troubleshooting

- **Redis Connection Issues**: Ensure Redis is running and accessible on localhost:6379
- **Build Failures**: Check that all prerequisites are installed and .NET version is 8.0+
- **Test Failures**: Verify Beacon is running before executing tests
- **Permission Issues**: Make sure the build script is executable: `chmod +x scripts/build-and-test.sh`

## Development

### Building Individual Components

```bash
# Build Pulsar compiler
dotnet build Pulsar/Pulsar.sln

# Build BeaconTester
dotnet build BeaconTester/BeaconTester.sln

# Run unit tests
dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj
```

### Adding New Projects

1. Create a new directory under `src/Rules/[ProjectName]/`
2. Add your `rules/` and `config/` subdirectories
3. Use the build script: `./scripts/build-and-test.sh [ProjectName]`

## Architecture Overview

The system follows this workflow:

1. **YAML Rules** → Pulsar validates and compiles rules into C# code
2. **Generated Code** → Pulsar creates a complete Beacon solution with templates
3. **Beacon Runtime** → Compiled executable that runs rules and interfaces with Redis
4. **BeaconTester** → Generates test scenarios from rules and validates Beacon behavior

This architecture ensures that the final Beacon executable is completely standalone, optimized, and can be AOT compiled for maximum performance.
