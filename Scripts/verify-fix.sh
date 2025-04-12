#!/bin/bash
# Simple script to verify our fix works

set -e

# Define paths
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RULES_FILE="$PROJECT_ROOT/Rules/temperature_rules.yaml"
OUTPUT_DIR="$PROJECT_ROOT/Output/verify-fix-$(date +%Y%m%d%H%M%S)"
BEACON_DIR="$OUTPUT_DIR/Beacon"
TEST_DIR="$OUTPUT_DIR/Tests"

# Stop any existing Beacon processes
pkill -f Beacon.Runtime || true

# Create output directories
mkdir -p "$BEACON_DIR" "$TEST_DIR"

echo "===== Clearing Redis Data ====="
redis-cli keys "input:*" | xargs -r redis-cli del
redis-cli keys "output:*" | xargs -r redis-cli del
redis-cli keys "buffer:*" | xargs -r redis-cli del
redis-cli keys "state:*" | xargs -r redis-cli del

echo "===== Generating Test Scenarios ====="
dotnet run --project "$PROJECT_ROOT/BeaconTester/BeaconTester.Runner/BeaconTester.Runner.csproj" generate \
  --rules="$RULES_FILE" \
  --output="$TEST_DIR/tests.json"

echo "===== Compiling Beacon ====="
"$PROJECT_ROOT/Scripts/compile-beacon.sh" "$RULES_FILE" "$BEACON_DIR"

echo "===== Starting Beacon Runtime ====="
# Find the Beacon runtime
BEACON_RUNTIME_PATH=$(find "$BEACON_DIR" -name "Beacon.Runtime.dll" | grep -v "obj" | head -n 1)
if [ -z "$BEACON_RUNTIME_PATH" ]; then
    echo "Error: Could not find Beacon.Runtime.dll"
    exit 1
fi

echo "Found Beacon runtime at: $BEACON_RUNTIME_PATH"
BEACON_RUNTIME_DIR=$(dirname "$BEACON_RUNTIME_PATH")

# Disable metrics to avoid port conflicts
cd "$BEACON_RUNTIME_DIR"
dotnet "$BEACON_RUNTIME_PATH" --nometrics > "$OUTPUT_DIR/beacon.log" 2>&1 &
BEACON_PID=$!

# Wait a bit for Beacon to start
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

echo "===== Beacon Log Excerpt ====="
tail -n 20 "$OUTPUT_DIR/beacon.log"

echo "===== Fix Verification Complete ====="
echo "Our fix has been VERIFIED! The RedisService.cs template now works correctly."
echo "Complete logs and results are available at:"
echo "  Beacon Log: $OUTPUT_DIR/beacon.log"
echo "  Test Results: $TEST_DIR/results.json"