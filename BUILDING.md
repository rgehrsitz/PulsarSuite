# Build System Documentation

This document describes the build system for PulsarSuite, which has been simplified to use a consolidated structure.

## Project Structure

The current structure is clean and intuitive:

```
PulsarSuite/
├── rules/                    # All rule definitions
│   ├── temperature_rules.yaml
│   ├── advanced_rules.yaml
│   └── ...
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

## Build Workflow

### Automated Build (Recommended)

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

### Manual Build Steps

If you prefer manual control:

```bash
# 1. Build Pulsar Compiler
cd Pulsar/Pulsar.Compiler && dotnet build -c Release

# 2. Build BeaconTester
cd ../../BeaconTester && dotnet build -c Release

# 3. Compile rules
cd ../Pulsar/Pulsar.Compiler
dotnet run -- beacon --rules ../../rules/temperature_rules.yaml --config ../../config/system_config.yaml --output ../../output/beacon

# 4. Build Beacon runtime
cd ../../output/beacon && dotnet build -c Release
```

## Configuration

### System Configuration

The system uses a single configuration file: `config/system_config.yaml`

```yaml
version: 1
cycleTime: 100                    # Rule evaluation interval (ms)
redis:
  endpoints: [localhost:6379]     # Redis connection
  poolSize: 4
  retryCount: 3
bufferCapacity: 100               # Historical data buffer size
# validSensors: []               # Optional - auto-populated from rules
```

**Key Features:**
- **Single config**: One file for all rules
- **Auto-sensor detection**: `validSensors` is automatically populated from your rules
- **No duplication**: No need for separate config files per rule set

### Rule Files

All rule files go in the `rules/` directory:

```yaml
# rules/temperature_rules.yaml
rules:
  - name: HighTemperatureAlert
    description: Alert when temperature exceeds threshold
    conditions:
      all:
        - condition:
            type: comparison
            sensor: input:temperature
            operator: '>'
            value: 30
    actions:
      - set_value:
          key: output:alert
          value: true
```

## Build Scripts

### Main Build Script (`scripts/build-and-test.sh`)

The main script provides a complete end-to-end workflow:

```bash
./scripts/build-and-test.sh
```

**Features:**
- Comprehensive logging
- Error handling
- Automatic directory creation
- Test execution
- Report generation

### Consolidation Script (`scripts/consolidate-rules.sh`)

Use this script to migrate from the old scattered structure:

```bash
./scripts/consolidate-rules.sh
```

This script:
- Moves all scattered rule files to `rules/`
- Consolidates config files to `config/`
- Creates documentation
- Provides migration guide

## Output Structure

Build outputs are organized in the `output/` directory:

```
output/
├── beacon/                    # Generated Beacon solution
├── logs/                      # Build and execution logs
└── reports/                   # Test reports and results
```

## Migration from Old Structure

If you have existing rules in scattered locations:

1. **Run consolidation script**:
   ```bash
   ./scripts/consolidate-rules.sh
   ```

2. **Update references**:
   - Old: `Pulsar/Examples/BasicRules/temperature_rules.yaml`
   - New: `rules/temperature_rules.yaml`

3. **Use single config**:
   - Old: Multiple config files per rule set
   - New: Single `config/system_config.yaml`

## Troubleshooting

### Common Issues

- **Rules not found**: Ensure rules are in `rules/` directory
- **Config not found**: Check `config/system_config.yaml` exists
- **Build failures**: Check .NET 8.0 SDK is installed
- **Redis connection**: Verify Redis server is running

### Debugging

- **Build logs**: Check `output/logs/` directory
- **Test reports**: Check `output/reports/` directory
- **Beacon logs**: Check console output during execution

## Benefits of New Structure

1. **Simplicity**: Clear, intuitive organization
2. **Maintainability**: No scattered files or duplicate configs
3. **Scalability**: Easy to add new rules without config changes
4. **Automation**: Auto-sensor detection reduces manual work
5. **Consistency**: Single workflow for all rule types

## Historical Note

The previous build system used scattered files and multiple configs per rule set. This was replaced with the current consolidated approach for better maintainability and user experience.
