#!/bin/bash

# PulsarSuite Rules and Config Consolidation Script
# This script consolidates all scattered rules and config files into a proper organized structure

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

echo -e "${BLUE}=== PulsarSuite Rules and Config Consolidation ===${NC}"
echo "Project root: $PROJECT_ROOT"

# Create the new consolidated structure
CONSOLIDATED_DIR="$PROJECT_ROOT/src"
RULES_DIR="$CONSOLIDATED_DIR/Rules"
CONFIGS_DIR="$CONSOLIDATED_DIR/Configs"
EXAMPLES_DIR="$CONSOLIDATED_DIR/Examples"

echo -e "${YELLOW}Creating consolidated directory structure...${NC}"

# Create main directories
mkdir -p "$RULES_DIR"
mkdir -p "$CONFIGS_DIR"
mkdir -p "$EXAMPLES_DIR"

# Create subdirectories for different rule types
mkdir -p "$RULES_DIR/Temperature"
mkdir -p "$RULES_DIR/Dependency"
mkdir -p "$RULES_DIR/Threshold"
mkdir -p "$RULES_DIR/Advanced"
mkdir -p "$RULES_DIR/Basic"

# Create subdirectories for different config types
mkdir -p "$CONFIGS_DIR/Temperature"
mkdir -p "$CONFIGS_DIR/Dependency"
mkdir -p "$CONFIGS_DIR/Threshold"
mkdir -p "$CONFIGS_DIR/Advanced"
mkdir -p "$CONFIGS_DIR/Basic"

echo -e "${GREEN}✓ Directory structure created${NC}"

# Function to copy file with backup
copy_file() {
    local source="$1"
    local dest="$2"
    local description="$3"

    if [[ -f "$source" ]]; then
        echo -e "${BLUE}Copying $description...${NC}"
        cp "$source" "$dest"
        echo -e "${GREEN}✓ Copied: $source → $dest${NC}"
    else
        echo -e "${YELLOW}⚠ Source not found: $source${NC}"
    fi
}

# Consolidate Basic Rules
echo -e "${YELLOW}Consolidating Basic Rules...${NC}"
copy_file "$PROJECT_ROOT/Pulsar/Examples/BasicRules/temperature_rules.yaml" \
          "$RULES_DIR/Basic/temperature_rules.yaml" \
          "Basic temperature rules"

copy_file "$PROJECT_ROOT/Pulsar/Examples/BasicRules/system_config.yaml" \
          "$CONFIGS_DIR/Basic/system_config.yaml" \
          "Basic system config"

# Consolidate Advanced Rules
echo -e "${YELLOW}Consolidating Advanced Rules...${NC}"
copy_file "$PROJECT_ROOT/Pulsar/Examples/advanced_rules.yaml" \
          "$RULES_DIR/Advanced/advanced_rules.yaml" \
          "Advanced rules"

copy_file "$PROJECT_ROOT/Pulsar/Examples/advanced_system_config.yaml" \
          "$CONFIGS_DIR/Advanced/advanced_system_config.yaml" \
          "Advanced system config"

# Consolidate Test Rules
echo -e "${YELLOW}Consolidating Test Rules...${NC}"
copy_file "$PROJECT_ROOT/Pulsar/TestRules.yaml" \
          "$RULES_DIR/Basic/test_rules.yaml" \
          "Test rules"

copy_file "$PROJECT_ROOT/Pulsar/Examples/rules.yaml" \
          "$RULES_DIR/Basic/example_rules.yaml" \
          "Example rules"

copy_file "$PROJECT_ROOT/Pulsar/Examples/system_config.yaml" \
          "$CONFIGS_DIR/Basic/example_system_config.yaml" \
          "Example system config"

# Consolidate Test Data
echo -e "${YELLOW}Consolidating Test Data...${NC}"
copy_file "$PROJECT_ROOT/Pulsar/TestData/sample-rules.yaml" \
          "$RULES_DIR/Basic/sample_rules.yaml" \
          "Sample rules"

copy_file "$PROJECT_ROOT/Pulsar/TestData/system_config.yaml" \
          "$CONFIGS_DIR/Basic/sample_system_config.yaml" \
          "Sample system config"

# Create consolidated examples
echo -e "${YELLOW}Creating consolidated examples...${NC}"

# Temperature Example
cat > "$EXAMPLES_DIR/temperature_example.md" << 'EOF'
# Temperature Monitoring Example

This example demonstrates basic temperature monitoring with alerts.

## Files
- Rules: `../Rules/Basic/temperature_rules.yaml`
- Config: `../Configs/Basic/system_config.yaml`

## Usage
```bash
# Compile the rules
pulsar compile --rules ../Rules/Basic/temperature_rules.yaml --config ../Configs/Basic/system_config.yaml --output ./output

# Run Beacon
cd output
dotnet run
```

## Rule Description
The temperature rules monitor temperature values and trigger alerts when thresholds are exceeded.
EOF

# Advanced Example
cat > "$EXAMPLES_DIR/advanced_example.md" << 'EOF'
# Advanced Rules Example

This example demonstrates complex rule patterns and advanced configurations.

## Files
- Rules: `../Rules/Advanced/advanced_rules.yaml`
- Config: `../Configs/Advanced/advanced_system_config.yaml`

## Usage
```bash
# Compile the rules
pulsar compile --rules ../Rules/Advanced/advanced_rules.yaml --config ../Configs/Advanced/advanced_system_config.yaml --output ./output

# Run Beacon
cd output
dotnet run
```

## Rule Description
Advanced rules demonstrate complex condition combinations, temporal logic, and sophisticated action patterns.
EOF

# Create a master index file
echo -e "${YELLOW}Creating master index...${NC}"
cat > "$CONSOLIDATED_DIR/README.md" << 'EOF'
# PulsarSuite Consolidated Rules and Configs

This directory contains all rules and configuration files organized by type and complexity.

## Directory Structure

```
src/
├── Rules/           # Rule definitions organized by type
│   ├── Basic/      # Simple, single-purpose rules
│   ├── Advanced/   # Complex rules with multiple conditions
│   ├── Dependency/ # Rules with dependencies between sensors
│   ├── Threshold/  # Rules with temporal thresholds
│   └── Temperature/ # Temperature-specific rules
├── Configs/         # System configurations
│   ├── Basic/      # Simple configurations
│   ├── Advanced/   # Complex configurations
│   └── ...         # Other config types
└── Examples/        # Usage examples and documentation
```

## Quick Start

### Basic Temperature Monitoring
```bash
pulsar compile \
  --rules Rules/Basic/temperature_rules.yaml \
  --config Configs/Basic/system_config.yaml \
  --output ./output
```

### Advanced Rules
```bash
pulsar compile \
  --rules Rules/Advanced/advanced_rules.yaml \
  --config Configs/Advanced/advanced_system_config.yaml \
  --output ./output
```

## File Descriptions

### Basic Rules
- `temperature_rules.yaml` - Simple temperature threshold monitoring
- `test_rules.yaml` - Basic test rules for validation
- `example_rules.yaml` - General example rules
- `sample_rules.yaml` - Sample rules for testing

### Advanced Rules
- `advanced_rules.yaml` - Complex rule patterns and conditions

### Configurations
- `system_config.yaml` - Standard system configuration
- `advanced_system_config.yaml` - Advanced system configuration
- `example_system_config.yaml` - Example configuration
- `sample_system_config.yaml` - Sample configuration for testing

## Migration Notes

This consolidated structure replaces the previous scattered files:
- `Pulsar/Examples/BasicRules/` → `src/Rules/Basic/`
- `Pulsar/Examples/` → `src/Rules/Advanced/` and `src/Configs/`
- `Pulsar/TestData/` → `src/Rules/Basic/` and `src/Configs/Basic/`
- `Pulsar/TestRules.yaml` → `src/Rules/Basic/test_rules.yaml`

## Testing

Use BeaconTester to validate your rules:
```bash
# From the project root
./scripts/build-and-test.sh
```
EOF

# Create a migration guide
echo -e "${YELLOW}Creating migration guide...${NC}"
cat > "$PROJECT_ROOT/MIGRATION_GUIDE.md" << 'EOF'
# Migration Guide: Rules and Config Consolidation

## Overview

This migration consolidates all scattered rules and configuration files into a proper organized structure under `src/`.

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
src/
├── Rules/
│   ├── Basic/
│   │   ├── temperature_rules.yaml
│   │   ├── test_rules.yaml
│   │   ├── example_rules.yaml
│   │   └── sample_rules.yaml
│   └── Advanced/
│       └── advanced_rules.yaml
├── Configs/
│   ├── Basic/
│   │   ├── system_config.yaml
│   │   ├── example_system_config.yaml
│   │   └── sample_system_config.yaml
│   └── Advanced/
│       └── advanced_system_config.yaml
└── Examples/
    ├── temperature_example.md
    └── advanced_example.md
```

## Migration Steps

1. **Update Build Scripts**: Update any scripts that reference the old paths
2. **Update Documentation**: Update documentation to reference new paths
3. **Update Tests**: Update test files to use new consolidated paths
4. **Clean Up**: Remove old scattered files after confirming everything works

## Path Mapping

| Old Path | New Path |
|----------|----------|
| `Pulsar/Examples/BasicRules/temperature_rules.yaml` | `src/Rules/Basic/temperature_rules.yaml` |
| `Pulsar/Examples/BasicRules/system_config.yaml` | `src/Configs/Basic/system_config.yaml` |
| `Pulsar/Examples/advanced_rules.yaml` | `src/Rules/Advanced/advanced_rules.yaml` |
| `Pulsar/Examples/advanced_system_config.yaml` | `src/Configs/Advanced/advanced_system_config.yaml` |
| `Pulsar/TestRules.yaml` | `src/Rules/Basic/test_rules.yaml` |
| `Pulsar/Examples/rules.yaml` | `src/Rules/Basic/example_rules.yaml` |
| `Pulsar/Examples/system_config.yaml` | `src/Configs/Basic/example_system_config.yaml` |
| `Pulsar/TestData/sample-rules.yaml` | `src/Rules/Basic/sample_rules.yaml` |
| `Pulsar/TestData/system_config.yaml` | `src/Configs/Basic/sample_system_config.yaml` |

## Benefits

1. **Organization**: Clear separation of rules by type and complexity
2. **Maintainability**: Easier to find and update specific rule types
3. **Scalability**: Easy to add new rule types and configurations
4. **Documentation**: Each example has its own documentation
5. **Testing**: Consistent structure for automated testing

## Rollback

If needed, the original files are preserved in their original locations. You can restore the old structure by reverting this migration.
EOF

echo -e "${GREEN}✓ Consolidation complete!${NC}"
echo ""
echo -e "${BLUE}Summary:${NC}"
echo "  • Created consolidated structure under: $CONSOLIDATED_DIR"
echo "  • Organized rules by type: Basic, Advanced, Dependency, Threshold, Temperature"
echo "  • Organized configs by type: Basic, Advanced"
echo "  • Created examples and documentation"
echo "  • Created migration guide: $PROJECT_ROOT/MIGRATION_GUIDE.md"
echo ""
echo -e "${YELLOW}Next steps:${NC}"
echo "  1. Review the consolidated structure"
echo "  2. Update build scripts to use new paths"
echo "  3. Test the new structure with your workflow"
echo "  4. Remove old scattered files once confirmed working"
echo ""
echo -e "${BLUE}To test the new structure:${NC}"
echo "  cd $PROJECT_ROOT"
echo "  ./scripts/build-and-test.sh"