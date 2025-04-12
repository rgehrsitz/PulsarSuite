#!/bin/bash
# Test script to verify the alert rule dependency fix using the properly generated Beacon application

set -e
BLUE='\033[0;34m'
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${BLUE}========== ALERT RULE DEPENDENCY TEST ==========${NC}"

# Define paths
ROOT_DIR="$(pwd)"
BEACON_DIR="$ROOT_DIR/Output/Beacon"
TEST_DIR="$ROOT_DIR/TestOutput/alertrule-test"

# Clean environment
echo -e "${YELLOW}[1] Cleaning environment...${NC}"
pkill -f Beacon.Runtime || true
redis-cli keys "input:*" | xargs -r redis-cli del
redis-cli keys "output:*" | xargs -r redis-cli del
redis-cli keys "buffer:*" | xargs -r redis-cli del
redis-cli keys "state:*" | xargs -r redis-cli del
mkdir -p "$TEST_DIR"
echo -e "${GREEN}✓ Environment cleaned${NC}"

# Set environment variables to help with testing
export BEACON_CYCLE_TIME=100
export STEP_DELAY_MULTIPLIER=1
export TIMEOUT_MULTIPLIER=2

# Start Beacon from proper output directory
echo -e "${YELLOW}[2] Starting Beacon with cycle time ${BEACON_CYCLE_TIME}ms...${NC}"
cd "$BEACON_DIR/Beacon/Beacon.Runtime/bin/Debug/net9.0/linux-x64" && export BEACON_LOG_LEVEL=Debug && dotnet Beacon.Runtime.dll --nometrics --testmode --test-cycle-time $BEACON_CYCLE_TIME --verbose > "$TEST_DIR/beacon.log" 2>&1 &
BEACON_PID=$!

# Wait for Beacon to start
sleep 3

if ! kill -0 $BEACON_PID 2>/dev/null; then
  echo -e "${RED}× Beacon process failed to start or terminated early${NC}"
  cat "$TEST_DIR/beacon.log"
  exit 1
fi

echo -e "${GREEN}✓ Beacon is running with PID $BEACON_PID${NC}"

# Give Beacon more time to establish stable Redis connections
echo -e "  - Waiting for Beacon to stabilize Redis connections..."
sleep 5

# Test AlertRule dependencies manually
echo -e "${YELLOW}[3] Testing AlertRule with dependencies...${NC}"

# Set dependencies to trigger the rule
echo -e "  - Setting rule dependencies..."
redis-cli set output:high_temperature true
redis-cli set output:humidity_normal false

# Set input values needed by the rule action
echo -e "  - Setting input values..."
redis-cli set input:temperature 31  # >30 to trigger HighTemperatureRule
redis-cli set input:humidity 69   # <70 to avoid triggering HumidityRule (we want humidity_normal = false)

# Debug Redis state
echo -e "  - Debugging Redis state..."
echo -e "    * Redis keys:"
redis-cli keys "*" | sort
echo -e "    * output:high_temperature = $(redis-cli get output:high_temperature)"
echo -e "    * output:humidity_normal = $(redis-cli get output:humidity_normal)"
echo -e "    * input:temperature = $(redis-cli get input:temperature)"
echo -e "    * input:humidity = $(redis-cli get input:humidity)"

# Wait longer for rule processing
echo -e "  - Waiting for rule processing (10 seconds)..."
sleep 10

# Check rule output
echo -e "  - Checking rule outputs..."
ALERT_STATUS=$(redis-cli get output:alert_status)
ALERT_MESSAGE=$(redis-cli get output:alert_message)

expected_status="\"critical\""
expected_message="\"High temperature alert: 31 with abnormal humidity: 69\""

# Print actual vs expected
echo -e "  - Alert Status:\n    Expected: $expected_status\n    Actual:   $ALERT_STATUS"
echo -e "  - Alert Message:\n    Expected: $expected_message\n    Actual:   $ALERT_MESSAGE"

# Validate results
if [ "$ALERT_STATUS" = "$expected_status" ] && [ "$ALERT_MESSAGE" = "$expected_message" ]; then
  echo -e "${GREEN}✓ AlertRule dependency test PASSED!${NC}"
  echo -e "${GREEN}✓ The fix successfully addressed the issue with input dependencies in rule actions.${NC}"
  TEST_RESULT=0
else
  echo -e "${RED}× AlertRule dependency test FAILED!${NC}"
  echo -e "${RED}× The AlertRule did not produce the expected outputs.${NC}"
  TEST_RESULT=1
fi

# Clean up
echo -e "${YELLOW}[4] Cleaning up...${NC}"
kill $BEACON_PID || true
sleep 1
redis-cli keys "*" | xargs -r redis-cli del
echo -e "${GREEN}✓ Cleanup complete${NC}"

# Exit with test result
exit $TEST_RESULT
