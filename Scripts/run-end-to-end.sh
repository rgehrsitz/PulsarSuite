#!/bin/bash
# Run complete end-to-end test with Pulsar/Beacon and BeaconTester

# Usage information
function show_usage {
    echo "Usage: ./run-end-to-end.sh <rule-file> [config-file]"
    echo ""
    echo "Arguments:"
    echo "  rule-file    Path to the YAML rules file (default: Rules/sample-rules.yaml)"
    echo "  config-file  Path to system config file (default: Config/system_config.yaml)"
    echo ""
    echo "Example:"
    echo "  ./run-end-to-end.sh Rules/temperature_rules.yaml Config/custom_config.yaml"
}

# Check for help flag
if [[ "$1" == "--help" || "$1" == "-h" ]]; then
    show_usage
    exit 0
fi

# Get absolute paths to directories
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RULES_FILE=${1:-"$PROJECT_ROOT/Rules/sample-rules.yaml"}
CONFIG_FILE=${2:-"$PROJECT_ROOT/Config/system_config.yaml"}
BASENAME=$(basename "$RULES_FILE" .yaml)
TIMESTAMP=$(date +%Y%m%d%H%M%S)
OUTPUT_DIR="$PROJECT_ROOT/Output/$BASENAME-$TIMESTAMP"

# Validate paths
if [ ! -f "$RULES_FILE" ]; then
    echo "Error: Rules file not found at $RULES_FILE"
    exit 1
fi

# Create output directories
BEACON_DIR="$OUTPUT_DIR/Beacon"
TEST_DIR="$OUTPUT_DIR/Tests"
mkdir -p "$BEACON_DIR" "$TEST_DIR"

echo "===== Starting End-to-End Test ====="
echo "Rule File: $RULES_FILE"
echo "Output Directory: $OUTPUT_DIR"

# Ensure Redis is running
echo "===== Checking Environment ====="
"$PROJECT_ROOT/Scripts/setup-environment.sh"

# Compile Beacon
echo ""
echo "===== Compiling Beacon ====="
"$PROJECT_ROOT/Scripts/compile-beacon.sh" "$RULES_FILE" "$BEACON_DIR" --config="$CONFIG_FILE"

if [ $? -ne 0 ]; then
    echo "Error: Beacon compilation failed. Aborting test."
    exit 1
fi

# Start Beacon in background
echo ""
echo "===== Starting Beacon Runtime ====="
# Look specifically for the Linux version of the DLL
BEACON_RUNTIME_PATH=$(find "$BEACON_DIR" -path "*linux-x64*" -name "Beacon.Runtime.dll" | grep -v "obj" | head -n 1)

# If not found, look for any version as a fallback
if [ -z "$BEACON_RUNTIME_PATH" ]; then
    BEACON_RUNTIME_PATH=$(find "$BEACON_DIR" -name "Beacon.Runtime.dll" | grep -v "obj" | head -n 1)
fi

if [ -z "$BEACON_RUNTIME_PATH" ]; then
    echo "Error: Could not find Beacon.Runtime.dll"
    exit 1
fi

echo "Found Beacon runtime at: $BEACON_RUNTIME_PATH"
BEACON_RUNTIME_DIR=$(dirname "$BEACON_RUNTIME_PATH")
cd "$BEACON_RUNTIME_DIR"

# Create minimal config for Beacon
cat > appsettings.json << EOF
{
  "Redis": {
    "Endpoints": [ "localhost:6379" ],
    "PoolSize": 4,
    "RetryCount": 3,
    "RetryBaseDelayMs": 100,
    "ConnectTimeout": 5000,
    "SyncTimeout": 1000,
    "KeepAlive": 60,
    "Password": null
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "BufferCapacity": 100,
  "CycleTimeMs": 100
}
EOF

# Start Beacon with a unique metrics port and save log to output directory
# Generate a random port number between 10000 and 19999
METRICS_PORT=$((10000 + RANDOM % 10000))
dotnet "$BEACON_RUNTIME_PATH" --verbose --metrics-port=$METRICS_PORT > "$OUTPUT_DIR/beacon.log" 2>&1 &
BEACON_PID=$!

# Check if Beacon started successfully
sleep 3
if ! kill -0 $BEACON_PID 2>/dev/null; then
    echo "Error: Beacon process failed to start or terminated early"
    cat "$OUTPUT_DIR/beacon.log"
    exit 1
fi

echo "Beacon started with PID $BEACON_PID"
echo "Waiting for Beacon to initialize..."
sleep 5

# Run tests
echo ""
echo "===== Running Tests ====="
cd "$PROJECT_ROOT"  # Return to project root before running tests

# Ensure test directory exists
mkdir -p "$TEST_DIR"
"$PROJECT_ROOT/Scripts/run-tests.sh" "$RULES_FILE" "$TEST_DIR"

TEST_RESULT=$?

# Shut down Beacon
echo ""
echo "===== Shutting Down Beacon ====="
kill $BEACON_PID 2>/dev/null || true
sleep 2

# Copy logs from outputs
find "$PROJECT_ROOT" -name "*.log" -exec cp {} "$OUTPUT_DIR/" \;

# Print summary
echo ""
echo "===== Test Summary ====="
if [ $TEST_RESULT -eq 0 ]; then
    echo "End-to-end test PASSED"
else
    echo "End-to-end test FAILED with exit code $TEST_RESULT"
fi

echo ""
echo "Output files:"
echo "  $OUTPUT_DIR/beacon.log - Beacon runtime log"
echo "  $TEST_DIR/results.json - Test results"
echo "  $TEST_DIR/report.html - Test report"

# Return test result
exit $TEST_RESULT