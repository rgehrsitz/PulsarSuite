# Pulsar/Beacon Development Environment

This repository provides a modern development environment for Pulsar/Beacon rule-based applications using MSBuild for streamlined builds and testing.

## Project Structure

```bash
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

- .NET SDK 7.0 or higher (install from <https://dotnet.microsoft.com/download>)
- Redis server (required by Beacon runtime)
  - You can run Redis locally or via Docker

---

## Full Workflow
Comprehensive build, test, and deployment steps are now consolidated in the [End-to-End Guide](Pulsar/docs/End-to-End-Guide.md).

---

### Output Locations

- Compiled rules: `output/Bin/ThresholdOverTimeExample/`
- Distributable Beacon: `output/dist/ThresholdOverTimeExample/`
- Published Beacon runtime: `output/dist/ThresholdOverTimeExample/Beacon/Beacon.Runtime/bin/Release/net9.0/linux-x64/publish/Beacon.Runtime`
- Manifest file: `output/dist/ThresholdOverTimeExample/Beacon/Beacon.Runtime/Generated/rules.manifest.json`
- Test results (Beacon unit tests): `output/dist/ThresholdOverTimeExample/Beacon/Beacon.Tests/TestResults/`
- Test results (BeaconTester): `output/dist/ThresholdOverTimeExample/test_results.json`

---

### Troubleshooting

- If you see errors about missing files or directories, ensure you have run all previous steps in order.
- If you change your rules or config, re-run the relevant steps above.
- For cross-platform builds, adjust the `--target` and `-r` arguments as needed (e.g., `win-x64`, `osx-x64`).

---

## Example: Full Workflow for ThresholdOverTimeExample

```sh
# 1. Validate rules
dotnet run --project Pulsar/Pulsar.Compiler/Pulsar.Compiler.csproj validate \
    --rules=src/Rules/ThresholdOverTimeExample/rules/threshold_over_time_rules.yaml \
    --config=src/Rules/ThresholdOverTimeExample/config/system_config.yaml

# 2. Compile rules
dotnet run --project Pulsar/Pulsar.Compiler/Pulsar.Compiler.csproj compile \
    --rules=src/Rules/ThresholdOverTimeExample/rules/threshold_over_time_rules.yaml \
    --config=src/Rules/ThresholdOverTimeExample/config/system_config.yaml \
    --output=output/Bin/ThresholdOverTimeExample

# 3. Generate Beacon solution
dotnet run --project Pulsar/Pulsar.Compiler/Pulsar.Compiler.csproj beacon \
    --rules=src/Rules/ThresholdOverTimeExample/rules/threshold_over_time_rules.yaml \
    --compiled-rules-dir=output/Bin/ThresholdOverTimeExample \
    --output=output/dist/ThresholdOverTimeExample \
    --config=src/Rules/ThresholdOverTimeExample/config/system_config.yaml \
    --target=linux-x64

# 4. Build Beacon runtime
dotnet publish output/dist/ThresholdOverTimeExample/Beacon/Beacon.Runtime/Beacon.Runtime.csproj \
    -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true

# 5. Run Beacon unit tests
dotnet test output/dist/ThresholdOverTimeExample/Beacon/Beacon.Tests/Beacon.Tests.csproj

# 6. **Start the Beacon runtime**
# IMPORTANT: You must start the Beacon runtime before running BeaconTester scenarios. Leave this process running in a separate terminal.
# NOTE: The published runtime is located at:
# output/dist/ThresholdOverTimeExample/Beacon/Beacon.Runtime/bin/Release/net9.0/linux-x64/publish/Beacon.Runtime
./output/dist/ThresholdOverTimeExample/Beacon/Beacon.Runtime/bin/Release/net9.0/linux-x64/publish/Beacon.Runtime

# 7. Generate & Run BeaconTester Scenarios
dotnet run --project BeaconTester/BeaconTester.Runner/BeaconTester.Runner.csproj generate --rules=src/Rules/ThresholdOverTimeExample/rules/threshold_over_time_rules.yaml --output=output/dist/ThresholdOverTimeExample/test_scenarios.json

dotnet run --project BeaconTester/BeaconTester.Runner/BeaconTester.Runner.csproj run --scenarios=output/dist/ThresholdOverTimeExample/test_scenarios.json --output=output/dist/ThresholdOverTimeExample/test_results.json
```

---

## Project Structure (unchanged)

```bash
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
