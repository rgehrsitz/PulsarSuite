#\!/bin/bash
set -e

# Test end-to-end workflow with temperature rules - concise version
# This script demonstrates the complete pipeline from rule compilation to test execution

# Configuration
RULES_FILE="$(pwd)/Rules/temperature_rules.yaml"
SYSTEM_CONFIG="$(pwd)/Pulsar/system_config.yaml"
OUTPUT_DIR="$(pwd)/Output/Beacon"
TEST_SCENARIOS="$(pwd)/TestOutput/temperature_test_scenarios.json"
TEST_RESULTS="$(pwd)/TestOutput/temperature_results.json"
REPORT_OUTPUT="$(pwd)/TestOutput/temperature_report.html"

echo "===== Pulsar/Beacon and BeaconTester End-to-End Test ====="
echo "Using rules file: $RULES_FILE"

echo ""
echo "Step 1: Build Pulsar Compiler"
cd Pulsar
dotnet build > /dev/null
echo "Pulsar compiled successfully"
cd ..

echo ""
echo "Step 2: Compile Beacon from temperature rules"
dotnet run --project Pulsar/Pulsar.Compiler beacon --config="$SYSTEM_CONFIG" --rules="$RULES_FILE" --output="$OUTPUT_DIR" > /dev/null
echo "Beacon compiled successfully from rules"

echo ""
echo "Step 3: Build the compiled Beacon application"
cd "$OUTPUT_DIR/Beacon"
dotnet build > /dev/null
echo "Beacon application built successfully"
cd -

echo ""
echo "Step 4: Generate test scenarios"
dotnet run --project BeaconTester/BeaconTester.Runner -- generate --rules="$RULES_FILE" --output="$TEST_SCENARIOS" > /dev/null
echo "Generated test scenarios at: $TEST_SCENARIOS"

echo ""
echo "Step 5: Check Redis"
if redis-cli ping > /dev/null 2>&1; then
  echo "Redis is already running"
else
  echo "Redis needs to be started"
  exit 1
fi

echo ""
echo "Step 6: Run Beacon in the background"
cd "$OUTPUT_DIR/Beacon/Beacon.Runtime"
dotnet run > /tmp/beacon.log 2>&1 &
BEACON_PID=$\!
echo "Beacon started with PID: $BEACON_PID"
sleep 3 # Give Beacon time to start
cd -

echo ""
echo "Step 7: Run tests against Beacon"
echo "Running tests... (this may take a moment)"
dotnet run --project BeaconTester/BeaconTester.Runner -- run --scenarios="$TEST_SCENARIOS" --output="$TEST_RESULTS" > /tmp/tests.log 2>&1
echo "Tests completed, results saved to: $TEST_RESULTS"

echo ""
echo "Step 8: Check test results"
cat "$TEST_RESULTS" | grep -E "(success|failure)" | head -10
echo "..."

echo ""
echo "Step 9: Clean up"
echo "Stopping Beacon (PID: $BEACON_PID)"
kill $BEACON_PID || true

echo ""
echo "===== Test Complete ====="
echo "Test scenarios: $TEST_SCENARIOS"
echo "Test results: $TEST_RESULTS"
