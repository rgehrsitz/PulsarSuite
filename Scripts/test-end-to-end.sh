#\!/bin/bash
set -e

# Test end-to-end workflow with temperature rules
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
dotnet build
cd ..

echo ""
echo "Step 2: Compile Beacon from temperature rules"
dotnet run --project Pulsar/Pulsar.Compiler beacon --config="$SYSTEM_CONFIG" --rules="$RULES_FILE" --output="$OUTPUT_DIR"

echo ""
echo "Step 3: Build the compiled Beacon application"
cd "$OUTPUT_DIR/Beacon"
dotnet build
cd -

echo ""
echo "Step 4: Generate test scenarios"
dotnet run --project BeaconTester/BeaconTester.Runner -- generate --rules="$RULES_FILE" --output="$TEST_SCENARIOS"

echo ""
echo "Step 5: Start Redis (if not already running)"
# This assumes Redis is installed and configured
redis-cli ping > /dev/null 2>&1 || { echo "Starting Redis..."; redis-server --daemonize yes; sleep 2; }

echo ""
echo "Step 6: Run Beacon in the background"
cd "$OUTPUT_DIR/Beacon/Beacon.Runtime"
dotnet run > /dev/null 2>&1 &
BEACON_PID=$\!
cd -
echo "Beacon started with PID: $BEACON_PID"
sleep 2 # Give Beacon time to start

echo ""
echo "Step 7: Run tests against Beacon"
dotnet run --project BeaconTester/BeaconTester.Runner -- run --scenarios="$TEST_SCENARIOS" --output="$TEST_RESULTS"

echo ""
echo "Step 8: Generate HTML report"
dotnet run --project BeaconTester/BeaconTester.Runner -- report --results="$TEST_RESULTS" --output="$REPORT_OUTPUT" --format=html

echo ""
echo "Step 9: Clean up"
echo "Stopping Beacon (PID: $BEACON_PID)"
kill $BEACON_PID

echo ""
echo "===== Test Complete ====="
echo "Test scenarios: $TEST_SCENARIOS"
echo "Test results: $TEST_RESULTS"
echo "HTML report: $REPORT_OUTPUT" 
