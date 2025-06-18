# Migration Guide: Rules and Config Consolidation

## Overview

This migration consolidates all scattered rules and configuration files into a clean, intuitive structure with `rules/` and `config/` directories.

## What Changed

### Before (Scattered Structure)
```
Pulsar/
├── Examples/
│   ├── BasicRules/
│   │   ├── temperature_rules.yaml
│   │   └── system_config.yaml
│   ├── advanced_rules.yaml
│   ├── advanced_system_config.yaml
│   ├── rules.yaml
│   └── system_config.yaml
├── TestData/
│   ├── sample-rules.yaml
│   └── system_config.yaml
└── TestRules.yaml
```

### After (Consolidated Structure)
```
PulsarSuite/
├── rules/                    # All rule definitions
│   ├── temperature_rules.yaml
│   ├── advanced_rules.yaml
│   ├── example_rules.yaml
│   ├── sample_rules.yaml
│   └── test_rules.yaml
├── config/                   # System configuration
│   └── system_config.yaml    # Single config for all rules
├── scripts/                  # Build and utility scripts
│   ├── build-and-test.sh     # Main build script
│   └── consolidate-rules.sh  # Rules consolidation script
└── ... (source, build, output, etc)
```

## Key Improvements

### 1. **Simplified Structure**
- **Rules**: All rule files in `rules/` directory
- **Config**: Single `config/system_config.yaml` for all rules
- **No duplication**: No need for separate config files per rule set

### 2. **Auto-Sensor Detection**
- **`validSensors` is now optional** in the config file
- **Auto-population**: System automatically scans all rule files and extracts sensors
- **No manual maintenance**: No need to manually list sensors in config

### 3. **Modern Workflow**
- **One script**: `./scripts/build-and-test.sh` handles everything
- **Intuitive paths**: Easy to find rules and config
- **Consistent**: Same structure for all projects

## Migration Steps

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

4. **Update build scripts**:
   - Use `./scripts/build-and-test.sh` for the complete workflow

## Path Mapping

| Old Path | New Path |
|----------|----------|
| `Pulsar/Examples/BasicRules/temperature_rules.yaml` | `rules/temperature_rules.yaml` |
| `Pulsar/Examples/BasicRules/system_config.yaml` | `config/system_config.yaml` |
| `Pulsar/Examples/advanced_rules.yaml` | `rules/advanced_rules.yaml` |
| `Pulsar/Examples/advanced_system_config.yaml` | `config/system_config.yaml` (consolidated) |
| `Pulsar/TestRules.yaml` | `rules/test_rules.yaml` |
| `Pulsar/Examples/rules.yaml` | `rules/example_rules.yaml` |
| `Pulsar/Examples/system_config.yaml` | `config/system_config.yaml` (consolidated) |
| `Pulsar/TestData/sample-rules.yaml` | `rules/sample_rules.yaml` |
| `Pulsar/TestData/system_config.yaml` | `config/system_config.yaml` (consolidated) |

## Configuration Changes

### Old Config (Required validSensors)
```yaml
version: 1
validSensors:
  - temperature
  - humidity
  - pressure
  - temp_alert
cycleTime: 100
redis:
  endpoints: [localhost:6379]
```

### New Config (Optional validSensors)
```yaml
version: 1
# validSensors is optional - auto-populated from rules
cycleTime: 100
redis:
  endpoints: [localhost:6379]
```

## Benefits

1. **🎯 Intuitive**: Rules in `rules/`, config in `config/`
2. **🔄 Auto-detection**: Sensors automatically extracted from rules
3. **📁 No duplication**: Single config for all rules
4. **🚀 Simple**: One script handles everything
5. **🔧 Maintainable**: Clear, organized structure

## Usage Examples

### Basic Workflow
```bash
# 1. Write rules in rules/
# 2. Configure system in config/system_config.yaml (optional validSensors)
# 3. Run everything:
./scripts/build-and-test.sh
```

### Manual Commands
```bash
# Compile specific rules
cd Pulsar/Pulsar.Compiler
dotnet run -- beacon --rules ../../rules/temperature_rules.yaml --config ../../config/system_config.yaml
```

## Rollback

If needed, the original files are preserved in their original locations. You can restore the old structure by reverting this migration.
