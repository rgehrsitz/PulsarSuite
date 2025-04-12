# Build System Documentation

This document describes the MSBuild-based build system for Pulsar/Beacon and BeaconTester projects.

## Project Structure

```text
PulsarSuite/
├── src/                 # Source code and input files
│   ├── Rules/          # Rule definitions
│   │   └── [ProjectName]/
│   │       ├── rules/  # YAML rule files
│   │       └── config/ # Configuration files
│   ├── Tests/         # Generated test files
│   └── Bin/           # Compiled binaries
├── build/              # Build configuration
│   └── PulsarSuite.core.build  # Main build file
└── output/             # Build outputs
    ├── dist/           # Distributable Beacon apps
    └── reports/        # Test reports
```

## Using the Build System

The build system uses .NET's built-in MSBuild capabilities, so all commands are run using `dotnet build` with appropriate parameters.

### Basic Usage

```bash
# Full build process for a project
dotnet build /p:ProjectName=MyProject /p:RulesFile=/path/to/rules.yaml
```

### Specifying Rules Files

The build system can work with any valid rules file, regardless of its name or location:

```bash
# Specify a rules file explicitly
dotnet build /t:ValidateRules /p:RulesFile=/home/user/PulsarSuite/src/Rules/MyProject/rules/custom_rules.yaml
```

If no rules file is specified, the system will look for a file named `temperature_rules.yaml` in the project's rules directory.

### Available Targets

```bash
# Validate rules only
dotnet build /t:ValidateRules /p:ProjectName=MyProject /p:RulesFile=/path/to/rules.yaml

# Compile rules to C#
dotnet build /t:CompileRules /p:ProjectName=MyProject /p:RulesFile=/path/to/rules.yaml

# Build Beacon application
dotnet build /t:BuildBeacon /p:ProjectName=MyProject /p:RulesFile=/path/to/rules.yaml

# Full build process
dotnet build /p:ProjectName=MyProject /p:RulesFile=/path/to/rules.yaml
```

## Output Locations

All build outputs are placed in consistent locations:

- Compiled rules: `src/Bin/[ProjectName]/`
- Generated tests: `src/Tests/[ProjectName]/`
- Distributable Beacon: `output/dist/[ProjectName]/`
- Test reports: `output/reports/[ProjectName]/`

## Build Process

The build process follows these steps:

1. **ValidateRules**: Validates the rule files using the Pulsar compiler
2. **CompileRules**: Compiles the rules into C# code
3. **BuildBeacon**: Builds a distributable Beacon application

Each step depends on the previous steps, so running `BuildBeacon` will automatically run `ValidateRules` and `CompileRules` first.

## Advantages Over Shell Scripts

- **Standardization**: Consistent build process across all projects
- **Simplicity**: Clear targets for each build step
- **Flexibility**: Works with any valid rules file
- **Maintainability**: Easy to extend and customize
- **Integration**: Works well with existing tools and IDEs

## Extending the Build System

The build system is designed to be extensible. You can add custom targets, modify properties, or add new project types by editing the `build/PulsarSuite.core.build` file.
