#!/bin/bash
# Generate test scenarios from a Pulsar/Beacon rules file

# Usage information
function show_usage {
    echo "Usage: ./generate-tests.sh <rule-file> [output-file]"
    echo ""
    echo "Arguments:"
    echo "  rule-file     Path to the YAML rules file (default: Rules/sample-rules.yaml)"
    echo "  output-file   Path to save the generated test scenarios (default: TestOutput/<rulename>_test_scenarios.json)"
    echo ""
    echo "Example:"
    echo "  ./generate-tests.sh Rules/temperature_rules.yaml"
}

# Check for help flag
if [[ "$1" == "--help" || "$1" == "-h" ]]; then
    show_usage
    exit 0
fi

# Get absolute paths to directories
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BEACONTESTER_DIR="$PROJECT_ROOT/BeaconTester"

# Set rule file with sensible default
if [ $# -ge 1 ]; then
    RULES_FILE="$1"
else
    RULES_FILE="$PROJECT_ROOT/Rules/sample-rules.yaml"
fi

# Create output filename based on rules filename if not specified
if [ $# -ge 2 ]; then
    OUTPUT_FILE="$2"
else
    # Get the basename of the rules file without extension
    RULE_BASENAME=$(basename "$RULES_FILE" .yaml)
    OUTPUT_FILE="$PROJECT_ROOT/TestOutput/${RULE_BASENAME}_test_scenarios.json"
fi

# Validate paths
if [ ! -f "$RULES_FILE" ]; then
    echo "Error: Rules file not found at $RULES_FILE"
    exit 1
fi

# Create output directory if needed
OUTPUT_DIR=$(dirname "$OUTPUT_FILE")
mkdir -p "$OUTPUT_DIR"

echo "Generating test scenarios from $RULES_FILE..."
echo "Output will be saved to $OUTPUT_FILE"

# Build BeaconTester if needed
if [ ! -d "$BEACONTESTER_DIR/BeaconTester.Runner/bin" ]; then
    echo "Building BeaconTester..."
    dotnet build "$BEACONTESTER_DIR/BeaconTester.sln"
    
    if [ $? -ne 0 ]; then
        echo "Error: Failed to build BeaconTester"
        exit 1
    fi
fi

# Generate test scenarios
dotnet run --project "$BEACONTESTER_DIR/BeaconTester.Runner/BeaconTester.Runner.csproj" generate \
  --rules="$RULES_FILE" \
  --output="$OUTPUT_FILE"

if [ $? -eq 0 ]; then
    echo "Successfully generated test scenarios from $RULES_FILE"
    echo "Test scenarios saved to $OUTPUT_FILE"
    
    # Print some stats about the generated tests
    TEST_COUNT=$(grep -o '"name":' "$OUTPUT_FILE" | wc -l)
    echo "Generated $TEST_COUNT test scenarios"
    
    echo ""
    echo "To run these tests against a running Beacon instance:"
    echo "dotnet run --project BeaconTester/BeaconTester.Runner/BeaconTester.Runner.csproj run \\"
    echo "  --scenarios=\"$OUTPUT_FILE\" \\"
    echo "  --output=\"$OUTPUT_DIR/results.json\" \\"
    echo "  --redis-host=localhost --redis-port=6379"
else
    echo "Error: Failed to generate test scenarios"
    exit 1
fi