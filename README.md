# Pulsar/Beacon Development Environment

This repository provides a modern development environment for Pulsar/Beacon rule-based applications using MSBuild for streamlined builds and testing.

## Project Structure

```
PulsarSuite/
├── src/                 # Source code and input files
│   ├── Rules/          # Rule definitions
│   │   └── [ProjectName]/
│   │       ├── rules/  # YAML rule files
│   │       └── config/ # Configuration files
│   ├── Tests/         # Generated test files
│   └── Bin/           # Compiled binaries
├── build/              # Build configuration
│   └── PulsarSuite.build  # Main build file
├── output/             # Build outputs
│   ├── dist/           # Distributable Beacon apps
│   └── reports/        # Test reports
├── Pulsar/             # Pulsar compiler source
└── BeaconTester/       # BeaconTester source
```

## Getting Started

### Prerequisites

- .NET SDK 7.0 or higher (install from https://dotnet.microsoft.com/download)
- Redis server (required by Beacon runtime)
  - You can run Redis locally or via Docker

---

## Manual Build & Test Workflow (Recommended)

This project no longer requires a build script or MSBuild file. Instead, simply follow the step-by-step commands below for a full end-to-end build and test.

### 1. Validate Rules
```sh
dotnet run --project Pulsar/Pulsar.Compiler/Pulsar.Compiler.csproj validate \
    --rules=src/Rules/TemperatureExample/rules/temperature_rules.yaml \
    --config=src/Rules/TemperatureExample/config/system_config.yaml
```

### 2. Compile Rules
```sh
dotnet run --project Pulsar/Pulsar.Compiler/Pulsar.Compiler.csproj compile \
    --rules=src/Rules/TemperatureExample/rules/temperature_rules.yaml \
    --config=src/Rules/TemperatureExample/config/system_config.yaml \
    --output=output/Bin/TemperatureExample
```

### 3. Generate Beacon Solution
```sh
dotnet run --project Pulsar/Pulsar.Compiler/Pulsar.Compiler.csproj beacon \
    --rules=src/Rules/TemperatureExample/rules/temperature_rules.yaml \
    --compiled-rules-dir=output/Bin/TemperatureExample \
    --output=output/dist/TemperatureExample \
    --config=src/Rules/TemperatureExample/config/system_config.yaml \
    --target=linux-x64
```

### 4. Build Beacon Runtime
```sh
dotnet publish output/dist/TemperatureExample/Beacon/Beacon.Runtime/Beacon.Runtime.csproj \
    -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true
```

### 5. Run End-to-End Tests
```sh
dotnet test output/dist/TemperatureExample/Beacon/Beacon.Tests/Beacon.Tests.csproj
```

---

### Output Locations
- Compiled rules: `output/Bin/[ProjectName]/`
- Distributable Beacon: `output/dist/[ProjectName]/`
- Test results: `output/dist/[ProjectName]/Beacon/Beacon.Tests/TestResults/`

---

### Troubleshooting
- If you see errors about missing files or directories, ensure you have run all previous steps in order.
- If you change your rules or config, re-run the relevant steps above.
- For cross-platform builds, adjust the `--target` and `-r` arguments as needed (e.g., `win-x64`, `osx-x64`).

---

## Example: Full Workflow for TemperatureExample

```sh
# 1. Validate rules
dotnet run --project Pulsar/Pulsar.Compiler/Pulsar.Compiler.csproj validate \
    --rules=src/Rules/TemperatureExample/rules/temperature_rules.yaml

# 2. Compile rules
dotnet run --project Pulsar/Pulsar.Compiler/Pulsar.Compiler.csproj compile \
    --rules=src/Rules/TemperatureExample/rules/temperature_rules.yaml \
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
    -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true

# 5. Run end-to-end tests
dotnet test output/dist/TemperatureExample/Beacon/Beacon.Tests/Beacon.Tests.csproj
```

---

## Project Structure (unchanged)

```
PulsarSuite/
├── src/                 # Source code and input files
│   ├── Rules/          # Rule definitions
│   │   └── [ProjectName]/
│   │       ├── rules/  # YAML rule files
│   │       └── config/ # Configuration files
├── Pulsar/             # Pulsar compiler source
├── output/             # Build outputs
│   └── dist/           # Distributable Beacon apps
└── ...
```

---

## Notes
- No scripting or build system is required—just run the commands above.
- All outputs are placed in the `output/` directory.
- For custom projects, substitute your own `[ProjectName]` and file paths.

## Related Projects

- [Pulsar](https://github.com/example/pulsar) - Rule compiler and code generator
- [BeaconTester](https://github.com/example/beacontester) - Automated testing framework