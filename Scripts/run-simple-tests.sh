#!/bin/bash
# Run end-to-end tests with the simple rules

# Get absolute paths to directories
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RULES_FILE="$PROJECT_ROOT/Rules/simple_rules.yaml"
CONFIG_FILE="$PROJECT_ROOT/Config/simple_rules_config.yaml"
OUTPUT_DIR="$PROJECT_ROOT/Output/SimpleRulesTest"

# Create output directories
mkdir -p "$OUTPUT_DIR"

echo "===== Simple Rules Test ====="
echo "Running BeaconTester with automatic test generation"

# Ensure Redis is running and environment is set up
echo "===== Setting up Environment ====="
"$PROJECT_ROOT/Scripts/setup-environment.sh"

# Compile Beacon with the simple rules
echo "===== Compiling Simple Rules ====="
"$PROJECT_ROOT/Scripts/compile-beacon.sh" "$RULES_FILE" "$OUTPUT_DIR/Beacon" --config="$CONFIG_FILE"

if [ $? -ne 0 ]; then
    echo "Error: Failed to compile rules. Aborting test."
    exit 1
fi

# First, let BeaconTester automatically generate tests from rules
echo "===== Generating Tests ====="
dotnet run --project "$PROJECT_ROOT/BeaconTester/BeaconTester.Runner/BeaconTester.Runner.csproj" generate \
  --rules="$RULES_FILE" \
  --output="$OUTPUT_DIR/generated_tests.json"

if [ $? -ne 0 ]; then
    echo "Error: Failed to generate tests. Aborting."
    exit 1
fi

echo "Generated test scenarios: $OUTPUT_DIR/generated_tests.json"

# Start Beacon in background
echo ""
echo "===== Starting Beacon Runtime ====="
BEACON_RUNTIME_PATH=$(find "$OUTPUT_DIR/Beacon" -path "*linux-x64*" -name "Beacon.Runtime.dll" | grep -v "obj" | head -n 1)

if [ -z "$BEACON_RUNTIME_PATH" ]; then
    BEACON_RUNTIME_PATH=$(find "$OUTPUT_DIR/Beacon" -name "Beacon.Runtime.dll" | grep -v "obj" | head -n 1)
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

# Start Beacon and save log to output directory
dotnet "$BEACON_RUNTIME_PATH" --verbose > "$OUTPUT_DIR/beacon.log" 2>&1 &
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

# Run the automatically generated tests
echo ""
echo "===== Running Tests ====="
cd "$PROJECT_ROOT"  # Return to project root before running tests

echo "Running BeaconTester with the generated test scenarios..."
dotnet run --project "$PROJECT_ROOT/BeaconTester/BeaconTester.Runner/BeaconTester.Runner.csproj" run \
  --scenarios="$OUTPUT_DIR/generated_tests.json" \
  --output="$OUTPUT_DIR/results.json" \
  --redis-host=localhost --redis-port=6379

TEST_RESULT=$?

# Generate a report
echo "Generating test report..."
dotnet run --project "$PROJECT_ROOT/BeaconTester/BeaconTester.Runner/BeaconTester.Runner.csproj" report \
  --results="$OUTPUT_DIR/results.json" \
  --output="$OUTPUT_DIR/report.html" \
  --format=html

# Shut down Beacon
echo ""
echo "===== Shutting Down Beacon ====="
kill $BEACON_PID 2>/dev/null || true
sleep 2

# Print summary
echo ""
echo "===== Test Summary ====="
if [ $TEST_RESULT -eq 0 ]; then
    echo "Simple rules test PASSED"
    echo ""
    echo "🎉 SUCCESS! 🎉"
    echo "The automatically generated tests ran successfully."
else
    echo "Simple rules test FAILED with exit code $TEST_RESULT"
    echo ""
    echo "See test logs for details:"
    echo "  $OUTPUT_DIR/beacon.log - Beacon runtime log"
    echo "  $OUTPUT_DIR/results.json - Test results"
fi

echo ""
echo "Output files:"
echo "  $OUTPUT_DIR/generated_tests.json - Auto-generated test scenarios"
echo "  $OUTPUT_DIR/results.json - Test results"
echo "  $OUTPUT_DIR/report.html - Test report"

exit $TEST_RESULT