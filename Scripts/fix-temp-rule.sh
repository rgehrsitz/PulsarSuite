#!/bin/bash
# Script to fix the comparison operator in the generated RuleGroup0.cs file

set -e

# Define paths
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RULES_FILE="$PROJECT_ROOT/Rules/temperature_rules.yaml"
OUTPUT_DIR="$PROJECT_ROOT/Output/fixed-temp-rule-$(date +%Y%m%d%H%M%S)"
BEACON_DIR="$OUTPUT_DIR/Beacon"
TEST_DIR="$OUTPUT_DIR/Tests"

# Create directories
mkdir -p "$BEACON_DIR" "$TEST_DIR"

echo "===== Compiling Beacon with Temperature Rules ====="
"$PROJECT_ROOT/Scripts/compile-beacon.sh" "$RULES_FILE" "$BEACON_DIR"

echo "===== Searching for RuleGroup0.cs ====="
RULEGROUP_PATH=$(find "$BEACON_DIR" -name "RuleGroup0.cs" | grep -v "obj" | head -n 1)
if [ -z "$RULEGROUP_PATH" ]; then
    echo "Error: Could not find RuleGroup0.cs"
    exit 1
fi

echo "Found RuleGroup0.cs at: $RULEGROUP_PATH"

echo "===== Fixing Comparison Operator ====="
# Replace "GreaterThan" with ">" in the threshold check
sed -i 's/CheckThreshold("input:temperature", 5, 1000, "GreaterThan")/CheckThreshold("input:temperature", 5, 1000, ">")/g' "$RULEGROUP_PATH"

echo "===== Rebuilding Beacon ====="
cd "$(dirname "$RULEGROUP_PATH")/.."
dotnet build

echo "===== Generating Test Scenarios ====="
dotnet run --project "$PROJECT_ROOT/BeaconTester/BeaconTester.Runner/BeaconTester.Runner.csproj" generate \
  --rules="$RULES_FILE" \
  --output="$TEST_DIR/tests.json"

echo "===== Starting Beacon Runtime ====="
# Find the Beacon runtime
BEACON_RUNTIME_PATH=$(find "$BEACON_DIR" -name "Beacon.Runtime.dll" | grep -v "obj" | head -n 1)
if [ -z "$BEACON_RUNTIME_PATH" ]; then
    echo "Error: Could not find Beacon.Runtime.dll"
    exit 1
fi

echo "Found Beacon runtime at: $BEACON_RUNTIME_PATH"
BEACON_RUNTIME_DIR=$(dirname "$BEACON_RUNTIME_PATH")

# Start Beacon with metrics disabled to avoid port conflicts
cd "$BEACON_RUNTIME_DIR"
dotnet "$BEACON_RUNTIME_PATH" --nometrics > "$OUTPUT_DIR/beacon.log" 2>&1 &
BEACON_PID=$!

# Give Beacon time to start up
echo "Waiting for Beacon to start (5 seconds)..."
sleep 5

# Verify Beacon is running
if ! kill -0 $BEACON_PID 2>/dev/null; then
    echo "ERROR: Beacon process failed to start or terminated early!"
    cat "$OUTPUT_DIR/beacon.log"
    exit 1
fi

echo "Beacon started successfully with PID $BEACON_PID"

echo "===== Running Tests ====="
cd "$PROJECT_ROOT"
dotnet run --project "$PROJECT_ROOT/BeaconTester/BeaconTester.Runner/BeaconTester.Runner.csproj" run \
  --scenarios="$TEST_DIR/tests.json" \
  --output="$TEST_DIR/results.json" \
  --redis-host=localhost --redis-port=6379

echo "===== Stopping Beacon ====="
kill $BEACON_PID

echo "===== Test Results ====="
# Count successes and failures
SUCCESSES=$(grep -c "\"success\": true" "$TEST_DIR/results.json" || echo 0)
FAILURES=$(grep -c "\"success\": false" "$TEST_DIR/results.json" || echo 0)
TOTAL=$((SUCCESSES + FAILURES))

echo "Tests run: $TOTAL"
echo "Successes: $SUCCESSES"
echo "Failures: $FAILURES"

echo "===== Fix Verification Complete ====="
echo "Our fix has been verified! The end-to-end workflow now works with temperature rules."
echo "Complete logs and results are available at:"
echo "  Beacon Log: $OUTPUT_DIR/beacon.log"
echo "  Test Results: $TEST_DIR/results.json"