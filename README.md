# PulsarSuite - Rule-Based Data Processing Platform

PulsarSuite is a comprehensive rule-based data processing platform featuring advanced temporal monitoring, three-valued logic, and automated code generation. The system consists of three main components working together to provide rule compilation, runtime execution, and automated testing.

## Key Features (v3)

- **Temporal Rules**: `threshold_over_time` conditions for sustained monitoring
- **Three-Valued Logic**: Handles uncertain data with True/False/Indeterminate evaluation
- **Advanced Emit Controls**: `on_change`, `on_enter`, `always` emission patterns
- **Comprehensive Validation**: JSON schema validation and compiler linting
- **Automated Testing**: Complete test scenario generation and execution

## Components

- **Pulsar**: Rules compiler that generates complete C# applications with AOT support
- **Beacon**: Generated runtime applications that execute rules in real-time with Redis integration
- **BeaconTester**: Automated testing framework with scenario generation and validation

## Project Structure

```bash
PulsarSuite/
├── rules/               # Rule definitions (YAML files)
├── config/              # System configuration
├── output/              # Generated applications and reports
├── Pulsar/              # Pulsar compiler source
├── BeaconTester/        # BeaconTester source
└── scripts/             # Build automation
```

## Documentation

- **[Rules Authoring Guide v3](Pulsar/docs/Rules-Authoring-Guide-v3.md)** - Complete guide for writing rules
- **[Rules Cheat Sheet v3](Pulsar/docs/Rules-Cheat-Sheet-v3.md)** - Quick reference for rule syntax
- **[Compiler Linting Rules](Pulsar/docs/Compiler-Linting-Rules.md)** - Validation and best practices
- **[Runtime Evaluation Semantics](Pulsar/docs/Runtime-Evaluation-Semantics.md)** - How rules are evaluated
- **[End-to-End Guide](Pulsar/docs/End-to-End-Guide.md)** - Complete workflow documentation

## Quick Start

1. **Write Rules**: Create YAML rule files in `/rules/` directory
   ```yaml
   version: 3
   rules:
     - name: HighTempAlert
       conditions:
         all:
           - type: threshold_over_time
             sensor: Temperature
             operator: ">"
             threshold: 75
             duration: 10s
       actions:
         - set: { key: sustained_high, value_expression: true }
   ```

2. **Run Build Script**: Execute the automated workflow
   ```bash
   ./scripts/build-and-test.sh [rules_file] [config_file]
   ```

3. **Generated Output**: Complete Beacon application appears in `/output/beacon/`

## Prerequisites

- .NET SDK 8.0+ (install from <https://dotnet.microsoft.com/download>)
- Redis server (required by Beacon runtime)

## Full Workflow

Comprehensive build, test, and deployment steps are in the [End-to-End Guide](Pulsar/docs/End-to-End-Guide.md).

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

he current, validated workflow for building, testing, and running an example project.The following series of `dotnet` commands represents t

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
./output/dist/ThresholdOverTimeExample/Beacon/Beacon.Runtime/bin/Debug/net9.0/linux-x64/Beacon.Runtime

# 7. Generate & Run BeaconTester Scenarios
dotnet run --project BeaconTester/BeaconTester.Runner/BeaconTester.Runner.csproj generate --rules=src/Rules/ThresholdOverTimeExample/rules/threshold_over_time_rules.yaml --output=output/dist/ThresholdOverTimeExample/test_scenarios.json

dotnet run --project BeaconTester/BeaconTester.Runner/BeaconTester.Runner.csproj run --scenarios=output/dist/ThresholdOverTimeExample/test_scenarios.json --output=output/dist/ThresholdOverTimeExample/test_results.json
```

---

<!-- ## Project Structure (unchanged) -->
<!-- Commenting out this section as it might be redundant / confusing -->
<!--
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
-->

---

## Notes

- The `dotnet` CLI commands detailed in the example workflow are the recommended way to build and test.
- All outputs are placed in the `output/` directory.
- For custom projects, substitute your own `[ProjectName]` and file paths.

## Related Projects

- [Pulsar](https://github.com/example/pulsar) - Rule compiler and code generator
- [BeaconTester](https://github.com/example/beacontester) - Automated testing framework
