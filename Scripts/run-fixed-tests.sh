#!/bin/bash
# Run end-to-end tests with a fixed test scenarios file

# Get absolute paths to directories
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RULES_FILE="$PROJECT_ROOT/Rules/simple_rules.yaml"
CONFIG_FILE="$PROJECT_ROOT/Config/simple_rules_config.yaml"
TEST_FILE="$PROJECT_ROOT/Output/fixed_tests.json"
OUTPUT_DIR="$PROJECT_ROOT/Output/FixedTests"

# Create output directories
mkdir -p "$OUTPUT_DIR"

# Create a fixed test scenarios file
cat > "$TEST_FILE" << EOF
{
  "scenarios": [
    {
      "name": "HighTemperatureTest",
      "description": "Tests the high temperature rule",
      "steps": [
        {
          "name": "Above Threshold",
          "inputs": [
            { "key": "input:temperature", "value": 35 }
          ],
          "expectations": [
            { "key": "output:high_temperature", "expected": true, "validator": "boolean", "timeoutMs": 1000 }
          ]
        }
      ]
    },
    {
      "name": "HumidityTest",
      "description": "Tests the humidity rule",
      "steps": [
        {
          "name": "Above Threshold",
          "inputs": [
            { "key": "input:humidity", "value": 45 }
          ],
          "expectations": [
            { "key": "output:humidity_normal", "expected": true, "validator": "boolean", "timeoutMs": 1000 }
          ]
        }
      ]
    },
    {
      "name": "ComfortIndexTest",
      "description": "Tests the comfort index calculation",
      "steps": [
        {
          "name": "Calculate Index",
          "inputs": [
            { "key": "input:temperature", "value": 10 },
            { "key": "input:humidity", "value": 40 }
          ],
          "expectations": [
            { "key": "output:comfort_index", "expected": 16, "validator": "numeric", "tolerance": 0.01, "timeoutMs": 1000 }
          ]
        }
      ]
    }
  ]
}
EOF

echo "===== Fixed Tests ====="
echo "Running tests with a fixed test scenarios file"

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

# Check Redis values directly
echo ""
echo "===== Checking Redis Directly ====="
echo "Checking if values are in Redis:"
redis-cli get output:high_temperature
redis-cli get output:humidity_normal
redis-cli get output:comfort_index

# Run the tests
echo ""
echo "===== Running Tests ====="
cd "$PROJECT_ROOT"  # Return to project root before running tests

echo "Running BeaconTester with the fixed test scenarios..."
dotnet run --project "$PROJECT_ROOT/BeaconTester/BeaconTester.Runner/BeaconTester.Runner.csproj" run \
  --scenarios="$TEST_FILE" \
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
    echo "Fixed tests PASSED"
    echo ""
    echo "🎉 SUCCESS! 🎉"
    echo "The tests passed with enhanced validators."
else
    echo "Fixed tests FAILED with exit code $TEST_RESULT"
    echo ""
    echo "See test logs for details:"
    echo "  $OUTPUT_DIR/beacon.log - Beacon runtime log"
    echo "  $OUTPUT_DIR/results.json - Test results"
fi

echo ""
echo "Output files:"
echo "  $TEST_FILE - Fixed test scenarios"
echo "  $OUTPUT_DIR/results.json - Test results"
echo "  $OUTPUT_DIR/report.html - Test report"

exit $TEST_RESULT