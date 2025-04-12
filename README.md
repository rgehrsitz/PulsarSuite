# Pulsar/Beacon Testing Environment

This repository contains a streamlined workflow for developing, testing, and running Pulsar/Beacon rule-based applications.

## Directory Structure

- `/Rules/` - YAML rule definition files
- `/Config/` - Configuration files for Pulsar/Beacon
- `/Scripts/` - Helper scripts for automation
- `/Output/` - Generated content (excluded from version control)
- `/Pulsar/` - Pulsar compiler and templates
- `/BeaconTester/` - Automated testing framework

## Getting Started

### Prerequisites

- .NET SDK 9.0 or higher
- Redis server (will be started automatically with Docker if not running)
- Docker (optional, for Redis if not installed locally)

### Quick Start

1. Clone this repository
2. Place your rule files in the `/Rules/` directory
3. Run the end-to-end test script:

```bash
./Scripts/run-end-to-end.sh Rules/your-rules.yaml
```

This will:
- Start Redis if needed
- Compile the rules into a Beacon application
- Run the Beacon runtime
- Execute tests using BeaconTester
- Generate a test report

## Available Scripts

### setup-environment.sh

Ensures Redis is running and clears existing test data.

```bash
./Scripts/setup-environment.sh
```

### compile-beacon.sh

Compiles rule files into a Beacon application.

```bash
./Scripts/compile-beacon.sh <rule-file> [output-dir]
```

### run-tests.sh

Runs tests against a running Beacon instance.

```bash
./Scripts/run-tests.sh <rule-file> [test-output-dir]
```

### run-end-to-end.sh

Runs the complete end-to-end workflow.

```bash
./Scripts/run-end-to-end.sh <rule-file>
```

## Development Notes

- All generated content goes to `/Output/` and is excluded from version control
- Each run creates a timestamped directory for easy reference
- Test reports are available in HTML and JSON formats

## Related Projects

- [Pulsar](https://github.com/example/pulsar) - Rule compiler and code generator
- [BeaconTester](https://github.com/example/beacontester) - Automated testing framework