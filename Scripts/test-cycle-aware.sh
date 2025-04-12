#!/bin/bash
# Script to test the cycle-aware BeaconTester implementation with various cycle times

set -e
BLUE='\033[0;34m'
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${BLUE}========== CYCLE-AWARE TESTING SCRIPT ==========${NC}"

# Define paths
ROOT_DIR="$(pwd)"
RULES_FILE="$ROOT_DIR/Rules/temperature_rules.yaml"
VALIDATION_DIR="/tmp/cycle-aware-test"
BEACON_DIR="$VALIDATION_DIR/beacon"
TEST_DIR="$VALIDATION_DIR/tests"

# Clean environment
echo -e "${YELLOW}[1] Cleaning environment...${NC}"
pkill -f Beacon.Runtime || true
redis-cli keys "input:*" | xargs -r redis-cli del
redis-cli keys "output:*" | xargs -r redis-cli del
redis-cli keys "buffer:*" | xargs -r redis-cli del
redis-cli keys "state:*" | xargs -r redis-cli del
rm -rf "$VALIDATION_DIR"
mkdir -p "$BEACON_DIR"
mkdir -p "$TEST_DIR"
echo -e "${GREEN}✓ Environment cleaned${NC}"

# Step 1: Generate test scenarios
echo -e "${YELLOW}[2] Generating test scenarios from temperature_rules.yaml...${NC}"
dotnet run --project "$ROOT_DIR/BeaconTester/BeaconTester.Runner/BeaconTester.Runner.csproj" generate \
  --rules="$RULES_FILE" \
  --output="$TEST_DIR/test_scenarios.json"

if [ -f "$TEST_DIR/test_scenarios.json" ]; then
  echo -e "${GREEN}✓ Test scenarios generated successfully${NC}"
  TEST_COUNT=$(grep -o '"name":' "$TEST_DIR/test_scenarios.json" | wc -l)
  echo "  - Generated $TEST_COUNT test scenarios"
else
  echo -e "${RED}× Failed to generate test scenarios${NC}"
  exit 1
fi

# Step 2: Compile Beacon with Test Mode support
echo -e "${YELLOW}[3] Compiling Beacon from temperature_rules.yaml...${NC}"
dotnet run --project "$ROOT_DIR/Pulsar/Pulsar.Compiler/Pulsar.Compiler.csproj" beacon \
  --rules="$RULES_FILE" \
  --output="$BEACON_DIR" \
  --config="$ROOT_DIR/Config/system_config.yaml" \
  --verbose
  
if [ -d "$BEACON_DIR/Beacon" ]; then
  echo -e "${GREEN}✓ Beacon compiler completed successfully${NC}"
else
  echo -e "${RED}× Beacon compilation failed${NC}"
  exit 1
fi

# Step 3: Build the Beacon solution
echo -e "${YELLOW}[4] Building the Beacon solution...${NC}"
cd "$BEACON_DIR" && dotnet build "Beacon/Beacon.Runtime/Beacon.Runtime.csproj" -r linux-x64

if [ $? -eq 0 ]; then
  echo -e "${GREEN}✓ Beacon solution built successfully${NC}"
else
  echo -e "${RED}× Failed to build Beacon solution${NC}"
  exit 1
fi

# Define test parameters for different cycle times
declare -a CYCLE_TIMES=("100" "250" "500")

for CYCLE_TIME in "${CYCLE_TIMES[@]}"; do
  echo -e "\n${BLUE}========== TESTING WITH CYCLE TIME: ${CYCLE_TIME}ms ==========${NC}"
  
  # Step 4: Start Beacon with the specified cycle time
  echo -e "${YELLOW}[5] Starting Beacon with ${CYCLE_TIME}ms cycle time...${NC}"
  BEACON_RUNTIME_PATH=$(find "$BEACON_DIR" -name "Beacon.Runtime.dll" | grep "linux-x64" | grep -v "obj" | head -n 1)
  BEACON_RUNTIME_DIR=$(dirname "$BEACON_RUNTIME_PATH")
  
  # Create settings file 
  cd "$BEACON_RUNTIME_DIR"
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
  "MetricsEnabled": false
}
EOF

  # Start Beacon in test mode with specified cycle time
  dotnet "$BEACON_RUNTIME_PATH" --nometrics --testmode --test-cycle-time "$CYCLE_TIME" > "$VALIDATION_DIR/beacon_${CYCLE_TIME}.log" 2>&1 &
  BEACON_PID=$!
  
  # Wait for Beacon to start
  sleep 3
  
  if ! kill -0 $BEACON_PID 2>/dev/null; then
    echo -e "${RED}× Beacon process failed to start or terminated early${NC}"
    cat "$VALIDATION_DIR/beacon_${CYCLE_TIME}.log"
    exit 1
  fi
  
  echo -e "${GREEN}✓ Beacon is running with PID $BEACON_PID${NC}"
  
  # Step 5: Run the tests with matching cycle time
  echo -e "${YELLOW}[6] Running tests with matching ${CYCLE_TIME}ms cycle time...${NC}"
  cd "$ROOT_DIR"
  
  # Set environment variables for cycle time configuration
  export BEACON_CYCLE_TIME="$CYCLE_TIME"
  export STEP_DELAY_MULTIPLIER="5"
  export TIMEOUT_MULTIPLIER="6"
  export GLOBAL_TIMEOUT_MULTIPLIER="2.0"
  
  # Run tests without the cycle time command line arguments 
  dotnet run --project "$ROOT_DIR/BeaconTester/BeaconTester.Runner/BeaconTester.Runner.csproj" run \
    --scenarios="$TEST_DIR/test_scenarios.json" \
    --output="$TEST_DIR/results_${CYCLE_TIME}.json" \
    --redis-host=localhost --redis-port=6379
  
  if [ -f "$TEST_DIR/results_${CYCLE_TIME}.json" ]; then
    echo -e "${GREEN}✓ Tests executed successfully${NC}"
    
    # Count successes and failures
    SUCCESSES=$(grep -c '"success": true' "$TEST_DIR/results_${CYCLE_TIME}.json" || echo 0)
    FAILURES=$(grep -c '"success": false' "$TEST_DIR/results_${CYCLE_TIME}.json" || echo 0)
    TOTAL=$((SUCCESSES + FAILURES))
    
    echo "  - Tests run: $TOTAL"
    echo "  - Successes: $SUCCESSES"
    echo "  - Failures: $FAILURES"
    
    # Check specific tests
    if grep -q '"name": "AlertRuleDependencyTest", "success": true' "$TEST_DIR/results_${CYCLE_TIME}.json"; then
      echo -e "  - ${GREEN}AlertRuleDependencyTest: PASSED${NC}"
    else
      echo -e "  - ${RED}AlertRuleDependencyTest: FAILED${NC}"
    fi
  else
    echo -e "${RED}× Failed to run tests or generate results${NC}"
  fi
  
  # Generate report
  echo -e "${YELLOW}[7] Generating test report...${NC}"
  dotnet run --project "$ROOT_DIR/BeaconTester/BeaconTester.Runner/BeaconTester.Runner.csproj" report \
    --results="$TEST_DIR/results_${CYCLE_TIME}.json" \
    --output="$TEST_DIR/report_${CYCLE_TIME}.html" \
    --format=html
  
  if [ -f "$TEST_DIR/report_${CYCLE_TIME}.html" ]; then
    echo -e "${GREEN}✓ Test report generated successfully${NC}"
  else
    echo -e "${RED}× Failed to generate test report${NC}"
  fi
  
  # Clean up
  echo -e "${YELLOW}[8] Cleaning up...${NC}"
  kill $BEACON_PID || true
  sleep 1
  redis-cli keys "input:*" | xargs -r redis-cli del
  redis-cli keys "output:*" | xargs -r redis-cli del
  redis-cli keys "buffer:*" | xargs -r redis-cli del
  redis-cli keys "state:*" | xargs -r redis-cli del
  echo -e "${GREEN}✓ Cleanup complete${NC}"
done

echo -e "\n${BLUE}========== TEST SUMMARY ==========${NC}"
echo "Tested with cycle times: ${CYCLE_TIMES[*]}ms"
echo "Results are available in $TEST_DIR/"
ls -la "$TEST_DIR/"

echo -e "${GREEN}All tests completed!${NC}"
