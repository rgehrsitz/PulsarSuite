# Build System Documentation (Experimental)

**Note:** The MSBuild-based system described here was an experimental approach to streamline builds. While functional to some extent, the primary and currently recommended workflow relies on the direct `dotnet` CLI commands detailed in `README.md`. This document is retained for informational purposes regarding that experimental system.

## Project Structure (for MSBuild Experiment)

The following structure was used for the MSBuild experiment:

```text
PulsarSuite/
├── src/                 # Source code and input files
│   ├── Rules/          # Rule definitions
│   │   └── [ProjectName]/
│   │       ├── rules/  # YAML rule files
│   │       └── config/ # Configuration files
│   ├── Tests/         # Generated test files
│   └── Bin/           # Compiled binaries (Note: README.md uses output/Bin)
├── build/              # Build configuration
│   └── PulsarSuite.core.build  # Main build file for the experiment
└── output/             # Build outputs (Consistent with README.md)
    ├── dist/           # Distributable Beacon apps
    └── reports/        # Test reports
```

## Using the Build System (Experimental)

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

# Build Beacon solution (includes the fix for RedisService.cs template issues)
dotnet build /t:BuildBeaconSolution /p:ProjectName=MyProject /p:RulesFile=/path/to/rules.yaml /p:Configuration=Release

# Generate tests
dotnet build /t:GenerateTests /p:ProjectName=MyProject /p:RulesFile=/path/to/rules.yaml /p:Configuration=Release

# Run tests
dotnet build /t:RunTests /p:ProjectName=MyProject /p:RulesFile=/path/to/rules.yaml /p:Configuration=Release

# Full build process
dotnet build /p:ProjectName=MyProject /p:RulesFile=/path/to/rules.yaml
```

### Known Issues and Workarounds (for MSBuild Experiment)

#### RedisService.cs Template Issue

The RedisService.cs template in Pulsar.Compiler can have issues with C# pattern matching syntax, causing compilation errors when generating a Beacon application. This is addressed directly in the MSBuild system using the following approach:

1. The BuildBeacon target in full.e2e.build creates a Beacon application using the Pulsar compiler

2. A fixed version of RedisService.cs is maintained in the build directory (`RedisService.cs.fixed`)

3. The build system automatically replaces the problematic generated file:

```xml
<!-- Replace the generated RedisService.cs with our fixed version -->
<PropertyGroup>
  <RedisServicePath>$(BeaconOutputDir)/Beacon/Beacon.Runtime/Services/RedisService.cs</RedisServicePath>
  <FixedRedisServicePath>$(MSBuildThisFileDirectory)/RedisService.cs.fixed</FixedRedisServicePath>
</PropertyGroup>

<!-- Use our rewritten RedisService.cs to avoid template processing issues -->
<Copy SourceFiles="$(FixedRedisServicePath)" DestinationFiles="$(RedisServicePath)" OverwriteReadOnlyFiles="true" />
```

This MSBuild approach ensured that no manual intervention or shell scripts were required to fix the issue during the experiment.

#### End-to-End Test Process (for MSBuild Experiment)

The complete workflow for testing with the fixed RedisService.cs template:

```bash
# Clean environment
dotnet msbuild build/full.e2e.build /t:Clean /p:Configuration=Release

# Build Beacon application (includes automatic RedisService.cs fix)
dotnet msbuild build/full.e2e.build /t:BuildBeacon /p:Configuration=Release \
   /p:RulesFile=/path/to/your/rules.yaml \
   /p:ConfigFile=/path/to/your/system_config.yaml

# Compile Beacon solution
dotnet msbuild build/full.e2e.build /t:BuildBeaconSolution /p:Configuration=Release

# Generate tests
dotnet msbuild build/full.e2e.build /t:GenerateTests /p:Configuration=Release

# Run tests
dotnet msbuild build/full.e2e.build /t:RunTests /p:Configuration=Release
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
