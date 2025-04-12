#!/bin/bash
# Compile rules into Beacon application

# Usage information
function show_usage {
    echo "Usage: ./compile-beacon.sh <rule-file> [output-dir] [--config=<config-file>]"
    echo ""
    echo "Arguments:"
    echo "  rule-file         Path to the YAML rules file (default: Rules/sample-rules.yaml)"
    echo "  output-dir        Directory for compiled output (default: Output/Beacon)"
    echo "  --config=<file>   Path to system config file (default: Config/system_config.yaml)"
    echo ""
    echo "Example:"
    echo "  ./compile-beacon.sh Rules/temperature_rules.yaml Output/MyBeacon --config=Config/custom_config.yaml"
}

# Check for help flag
if [[ "$1" == "--help" || "$1" == "-h" ]]; then
    show_usage
    exit 0
fi

# Get absolute paths to directories
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PULSAR_DIR="$PROJECT_ROOT/Pulsar"

# Initialize with defaults
RULES_FILE=""
OUTPUT_DIR=""
CONFIG_FILE="$PROJECT_ROOT/Config/system_config.yaml"

# Parse arguments
for arg in "$@"; do
  if [[ $arg == --config=* ]]; then
    CONFIG_FILE="${arg#*=}"
  elif [[ $arg == --* ]]; then
    echo "Unknown option: $arg"
    show_usage
    exit 1
  elif [ -z "$RULES_FILE" ]; then
    RULES_FILE="$arg"
  elif [ -z "$OUTPUT_DIR" ]; then
    OUTPUT_DIR="$arg"
  else
    echo "Too many positional arguments"
    show_usage
    exit 1
  fi
done

# Apply defaults if not specified
RULES_FILE=${RULES_FILE:-"$PROJECT_ROOT/Rules/sample-rules.yaml"}
OUTPUT_DIR=${OUTPUT_DIR:-"$PROJECT_ROOT/Output/Beacon"}

# Validate paths
if [ ! -f "$RULES_FILE" ]; then
    echo "Error: Rules file not found at $RULES_FILE"
    exit 1
fi

if [ ! -f "$CONFIG_FILE" ]; then
    echo "Error: Config file not found at $CONFIG_FILE"
    exit 1
fi

# Ensure output directory exists
mkdir -p "$OUTPUT_DIR"

# Find Pulsar.Compiler binary
COMPILER_DLL=$(find "$PULSAR_DIR" -name "Pulsar.Compiler.dll" | grep -v "obj" | head -n 1)
if [ -z "$COMPILER_DLL" ]; then
    echo "Pulsar.Compiler.dll not found. Building Pulsar first..."
    dotnet build "$PULSAR_DIR/Pulsar.sln"
    COMPILER_DLL=$(find "$PULSAR_DIR" -name "Pulsar.Compiler.dll" | grep -v "obj" | head -n 1)
    
    if [ -z "$COMPILER_DLL" ]; then
        echo "Error: Could not find or build Pulsar.Compiler.dll"
        exit 1
    fi
fi

echo "Found compiler at: $COMPILER_DLL"

# Run Pulsar compiler
echo "Compiling $RULES_FILE to $OUTPUT_DIR..."
dotnet "$COMPILER_DLL" beacon \
  --rules="$RULES_FILE" \
  --config="$CONFIG_FILE" \
  --output="$OUTPUT_DIR" \
  --target=linux-x64 \
  --verbose

if [ $? -ne 0 ]; then
    echo "Error: Failed to compile rules with Pulsar"
    exit 1
fi

# Build the generated Beacon solution
echo "Building Beacon solution..."
cd "$OUTPUT_DIR/Beacon"
dotnet build

if [ $? -ne 0 ]; then
    echo "Error: Failed to build Beacon solution"
    exit 1
fi

echo "Beacon application compiled and built successfully at $OUTPUT_DIR"
echo "To run the Beacon application:"
echo "  cd $OUTPUT_DIR/Beacon/Beacon.Runtime/bin/Debug/net9.0"
echo "  dotnet Beacon.Runtime.dll"