# Build System Documentation

## Overview

This directory contains the build system files for PulsarSuite. The build system has been simplified and consolidated to provide a cleaner, more maintainable approach.

## Current Build System

### Primary Build File: `PulsarSuite.build`

This is the main, simplified build file that provides all necessary targets:

- `Clean` - Clean environment and output directories
- `ValidateRules` - Validate YAML rules
- `CompileRules` - Compile rules to C# code
- `GenerateBeacon` - Generate Beacon application
- `BuildBeacon` - Build Beacon runtime
- `GenerateTests` - Generate test scenarios
- `RunTests` - Run tests against Beacon
- `RunEndToEnd` - Complete end-to-end workflow
- `Build` - Default build target

### Usage

```bash
# Full end-to-end workflow
dotnet msbuild build/PulsarSuite.build /p:ProjectName=TemperatureExample

# Individual targets
dotnet msbuild build/PulsarSuite.build /t:ValidateRules /p:ProjectName=TemperatureExample
dotnet msbuild build/PulsarSuite.build /t:BuildBeacon /p:ProjectName=TemperatureExample
dotnet msbuild build/PulsarSuite.build /t:RunEndToEnd /p:ProjectName=TemperatureExample
```

## Legacy Build Files

The following files are kept for reference but are no longer the primary build system:

- `PulsarSuite.core.build` - Original core build file
- `PulsarSuite.clean.build` - Clean build variant
- `e2e.build` - End-to-end build variant
- `final.build` - Final build variant
- `full.e2e.build` - Full end-to-end build
- `minimal.build` - Minimal build variant
- `minimal.fixed.build` - Fixed minimal build
- `minimal.working.build` - Working minimal build
- `simple.build` - Simple build variant
- `PulsarSuite.simple.build` - Simple build variant

## Recommended Approach

For new development, we recommend using the **automated shell script** approach:

```bash
# Use the simplified build script
./scripts/build-and-test.sh TemperatureExample
```

This approach is:
- Easier to understand and maintain
- More reliable across different environments
- Provides better error handling and logging
- Supports individual steps and debugging

## Migration Notes

If you were using the old MSBuild system:

1. **Replace MSBuild commands** with the shell script approach
2. **Update project references** to use the new simplified paths
3. **Use the new output structure** in `output/` directory
4. **Reference the main README.md** for current usage instructions

## Troubleshooting

- **Build failures**: Check that .NET 8.0+ is installed
- **Template issues**: The new system has improved template handling
- **Redis issues**: Ensure Redis is running on localhost:6379
- **Permission issues**: Make sure scripts are executable

For detailed troubleshooting, see the main README.md file.