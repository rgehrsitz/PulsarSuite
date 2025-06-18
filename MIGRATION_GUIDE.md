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
