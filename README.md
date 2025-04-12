# Pulsar/Beacon Development Environment

This repository provides a modern development environment for Pulsar/Beacon rule-based applications using MSBuild for streamlined builds and testing.

## Project Structure

```
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

- .NET SDK 9.0 or higher
- Redis server (will be started automatically with Docker if not running)
- Docker (optional, for Redis if not installed locally)

### Quick Start

1. Clone this repository
2. Create a new project directory in `src/Rules/`:
   ```bash
   mkdir -p src/Rules/MyProject/{rules,config}
   ```
3. Place your rule files in `src/Rules/MyProject/rules/`
4. Build and test your project:
   ```bash
   msbuild build/PulsarSuite.build /t:Build /p:ProjectName=MyProject
   ```

This will:
- Validate your rules
- Compile them to C#
- Generate test cases
- Build the Beacon application
- Run tests and generate reports

## Build Commands

```bash
# Clean all build artifacts
msbuild build/PulsarSuite.build /t:Clean

# Validate rules only
msbuild build/PulsarSuite.build /t:ValidateRules

# Compile rules to C#
msbuild build/PulsarSuite.build /t:CompileRules

# Generate tests from compiled rules
msbuild build/PulsarSuite.build /t:GenerateTests

# Build Beacon application
msbuild build/PulsarSuite.build /t:BuildBeacon

# Run tests
msbuild build/PulsarSuite.build /t:RunTests

# Full build process (clean + validate + compile + test)
msbuild build/PulsarSuite.build /t:Build
```

## Output Locations

All build outputs are placed in consistent locations:

- Compiled rules: `src/Bin/[ProjectName]/`
- Generated tests: `src/Tests/[ProjectName]/`
- Distributable Beacon: `output/dist/[ProjectName]/`
- Test reports: `output/reports/`

## Development Notes

- All generated content goes to the `output/` directory and is excluded from version control
- Test reports are available in `output/reports/`
- Each project has its own isolated build directories under `src/Bin/` and `src/Tests/`

## Related Projects

- [Pulsar](https://github.com/example/pulsar) - Rule compiler and code generator
- [BeaconTester](https://github.com/example/beacontester) - Automated testing framework