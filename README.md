# PulsarSuite

A comprehensive rules engine system consisting of **Pulsar** (rules compiler), **Beacon** (runtime), and **BeaconTester** (testing framework).

## Quick Start

### Prerequisites
- .NET 8.0 SDK
- Redis server (for data persistence and testing)

### Simple Workflow

1. **Write your rules** in `rules/` directory:
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

2. **Configure your system** in `config/system_config.yaml`:
   ```yaml
   version: 1
   cycleTime: 100  # milliseconds
   redis:
     endpoints:
       - localhost:6379
   bufferCapacity: 100
   # Note: validSensors is optional - will be auto-populated from rules
   ```

3. **Build and test** everything:
   ```bash
   ./scripts/build-and-test.sh
   ```

## Project Structure

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

## Key Features

### Simplified Configuration
- **Single config file**: One `config/system_config.yaml` for all rules
- **Auto-sensor detection**: `validSensors` is automatically populated from your rules
- **No duplication**: No need for separate config files per rule set

### Modern Workflow
- **Consolidated rules**: All rules in one `rules/` directory
- **Simple build**: One script handles everything
- **Clear structure**: Intuitive directory organization

### Advanced Capabilities
- **AOT compilation**: Native performance with .NET AOT
- **Redis integration**: Persistent data storage
- **Comprehensive testing**: Automated test generation and execution
- **Temporal rules**: Support for time-based conditions

## Usage Examples

### Basic Temperature Monitoring
```bash
# Write rules in rules/temperature_rules.yaml
# Configure system in config/system_config.yaml
# Build and test
./scripts/build-and-test.sh
```

### Advanced Rules
```bash
# Use complex rules with dependencies
# Rules automatically extract all required sensors
# Single config handles all rule types
```

### Custom Rule Sets
```bash
# Add new rule files to rules/
# They'll automatically be included in builds
# No config changes needed
```

## Development

### Building Individual Components
```bash
# Build Pulsar compiler
cd Pulsar/Pulsar.Compiler && dotnet build

# Build BeaconTester
cd BeaconTester && dotnet build

# Compile specific rules
cd Pulsar/Pulsar.Compiler
dotnet run -- beacon --rules ../../rules/temperature_rules.yaml --config ../../config/system_config.yaml
```

### Testing
```bash
# Run full test suite
./scripts/build-and-test.sh

# Run specific tests
cd BeaconTester/BeaconTester.Runner
dotnet run -- run --scenarios ../../examples/Tests/DefaultProject/test_scenarios.json
```

## Configuration

### System Configuration (`config/system_config.yaml`)
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

### Rule Definition Format
```yaml
rules:
  - name: RuleName
    description: Rule description
    conditions:
      all:                        # or 'any'
        - condition:
            type: comparison      # or 'threshold_over_time', 'expression'
            sensor: input:sensor_name
            operator: '>'         # or '<', '>=', '<=', '==', '!='
            value: 30
    actions:
      - set_value:
          key: output:result
          value: true             # or value_expression: "input:a + input:b"
```

## Architecture

### Pulsar (Compiler)
- Parses YAML rule definitions
- Generates optimized C# code
- Creates AOT-compatible Beacon applications
- Auto-extracts sensor requirements

### Beacon (Runtime)
- Executes compiled rules
- Interfaces with Redis for data persistence
- Supports real-time rule evaluation
- AOT-compiled for native performance

### BeaconTester (Testing)
- Generates test scenarios from rules
- Validates rule behavior
- Provides comprehensive test reports
- Supports automated testing workflows

## Migration from Old Structure

If you have existing rules in scattered locations:

1. **Consolidate rules**:
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
- **Redis connection**: Verify Redis server is running
- **Build failures**: Check .NET 8.0 SDK is installed

### Logs and Debugging
- Build logs: `output/logs/`
- Test reports: `output/reports/`
- Beacon logs: Check console output during execution

## Contributing

1. Follow the consolidated structure
2. Add rules to `rules/` directory
3. Update single config if needed
4. Test with `./scripts/build-and-test.sh`

## License

[Add your license information here]
