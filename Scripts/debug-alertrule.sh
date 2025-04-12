#!/bin/bash
# Debug script for AlertRule dependency testing - tracks Redis values over time

set -e
BLUE='\033[0;34m'
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${BLUE}========== ALERTRULE DEPENDENCY DEBUG ===========${NC}"

# Define paths
ROOT_DIR="$(pwd)"
BEACON_DIR="$ROOT_DIR/Output/Beacon"
TEST_DIR="$ROOT_DIR/TestOutput/alertrule-debug"

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
export BEACON_CYCLE_TIME=500  # Longer cycle time for more detailed debugging
export STEP_DELAY_MULTIPLIER=1
export TIMEOUT_MULTIPLIER=2

# Start Beacon from proper output directory
echo -e "${YELLOW}[2] Starting Beacon with cycle time ${BEACON_CYCLE_TIME}ms...${NC}"
cd "$BEACON_DIR/Beacon/Beacon.Runtime/bin/Debug/net9.0/linux-x64" && \
export BEACON_LOG_LEVEL=Debug && \
dotnet Beacon.Runtime.dll --nometrics --testmode --test-cycle-time $BEACON_CYCLE_TIME --verbose > "$TEST_DIR/beacon.log" 2>&1 &
BEACON_PID=$!

# Wait for Beacon to start
sleep 5

if ! kill -0 $BEACON_PID 2>/dev/null; then
  echo -e "${RED}× Beacon process failed to start or terminated early${NC}"
  cat "$TEST_DIR/beacon.log"
  exit 1
fi

echo -e "${GREEN}✓ Beacon is running with PID $BEACON_PID${NC}"

# Function to monitor redis values
monitor_keys() {
  while true; do
    timestamp=$(date +"%H:%M:%S.%3N")
    temperature=$(redis-cli get "input:temperature")
    humidity=$(redis-cli get "input:humidity")
    high_temp=$(redis-cli get "output:high_temperature")
    humidity_normal=$(redis-cli get "output:humidity_normal")
    alert_status=$(redis-cli get "output:alert_status")
    alert_message=$(redis-cli get "output:alert_message")
    
    echo "[$timestamp] VALUES:" >> "$TEST_DIR/redis_monitor.log"
    echo "  input:temperature = $temperature" >> "$TEST_DIR/redis_monitor.log"
    echo "  input:humidity = $humidity" >> "$TEST_DIR/redis_monitor.log"
    echo "  output:high_temperature = $high_temp" >> "$TEST_DIR/redis_monitor.log"
    echo "  output:humidity_normal = $humidity_normal" >> "$TEST_DIR/redis_monitor.log"
    echo "  output:alert_status = $alert_status" >> "$TEST_DIR/redis_monitor.log"
    echo "  output:alert_message = $alert_message" >> "$TEST_DIR/redis_monitor.log"
    echo "-------------------" >> "$TEST_DIR/redis_monitor.log"
    
    sleep 0.5
  done
}

# Start monitoring in background
echo -e "${YELLOW}[3] Starting Redis monitoring...${NC}"
monitor_keys &
MONITOR_PID=$!
echo -e "${GREEN}✓ Monitoring started with PID $MONITOR_PID${NC}"

# Run through test cases systematically
echo -e "${YELLOW}[4] Testing dependency rule execution...${NC}"

# PHASE 1: Test individual rules first
echo -e "${BLUE}Phase 1: Testing individual rules${NC}"

# Test HighTemperatureRule (temperature > 30 => high_temperature = true)
echo -e "Testing HighTemperatureRule..."
redis-cli del "output:high_temperature"
redis-cli set "input:temperature" "35"
sleep 2
high_temp=$(redis-cli get "output:high_temperature")
echo -e "  Temperature 35 => high_temperature = $high_temp"

# Test HumidityRule (humidity < 30 | humidity > 70 => humidity_normal = false)
echo -e "Testing HumidityRule..."
redis-cli del "output:humidity_normal"
redis-cli set "input:humidity" "75"
sleep 2
humidity_normal=$(redis-cli get "output:humidity_normal")
echo -e "  Humidity 75 => humidity_normal = $humidity_normal"

# PHASE 2: Test AlertRule with dependencies manually controlled
echo -e "${BLUE}Phase 2: Testing AlertRule with manually set dependencies${NC}"

# Explicitly set the dependencies
echo -e "Setting dependencies by hand..."
redis-cli set "output:high_temperature" "true"
redis-cli set "output:humidity_normal" "false"
sleep 1

# Set input values
echo -e "Setting input values..."
redis-cli set "input:temperature" "35"
redis-cli set "input:humidity" "75"
sleep 3

# Check results
alert_status=$(redis-cli get "output:alert_status")
alert_message=$(redis-cli get "output:alert_message")
echo -e "AlertRule result with manual dependencies:"
echo -e "  alert_status = $alert_status"
echo -e "  alert_message = $alert_message"

# PHASE 3: Test AlertRule with BeaconTester sequence that matches test case
echo -e "${BLUE}Phase 3: Testing using BeaconTester approach${NC}"

# Clean all keys
echo -e "Cleaning all keys..."
redis-cli keys "input:*" | xargs -r redis-cli del
redis-cli keys "output:*" | xargs -r redis-cli del
redis-cli keys "buffer:*" | xargs -r redis-cli del
redis-cli keys "state:*" | xargs -r redis-cli del
sleep 1

# Test sequence that matches AlertRuleDependencyTest
echo -e "Setting pre-test outputs (as BeaconTester would)..."
redis-cli set "output:high_temperature" "true"
redis-cli set "output:humidity_normal" "false"
sleep 1

echo -e "Setting input values (as BeaconTester would)..."
redis-cli set "input:temperature" "35"
redis-cli set "input:humidity" "35"
sleep 3

# Check results
alert_status=$(redis-cli get "output:alert_status")
alert_message=$(redis-cli get "output:alert_message")
echo -e "AlertRule result with BeaconTester approach:"
echo -e "  alert_status = $alert_status"
echo -e "  alert_message = $alert_message"

# Clean up
echo -e "${YELLOW}[5] Cleaning up...${NC}"
kill $BEACON_PID || true
kill $MONITOR_PID || true
wait $BEACON_PID 2>/dev/null || true
wait $MONITOR_PID 2>/dev/null || true

# Print debug logs location
echo -e "${GREEN}✓ Debug complete!${NC}"
echo -e "Beacon logs: $TEST_DIR/beacon.log"
echo -e "Redis monitoring: $TEST_DIR/redis_monitor.log"

# Optional - show the tail of the beacon log for any relevant errors
echo -e "${BLUE}Last 30 lines of Beacon log:${NC}"
tail -n 30 "$TEST_DIR/beacon.log"
echo -e "${YELLOW}[3] Starting Redis monitoring...${NC}"
( while true; do
  timestamp=$(date +"%H:%M:%S.%3N")
  high_temp=$(redis-cli get output:high_temperature)
  humidity_normal=$(redis-cli get output:humidity_normal)
  alert_status=$(redis-cli get output:alert_status)
  alert_message=$(redis-cli get output:alert_message)
  echo "[$timestamp] high_temperature=$high_temp, humidity_normal=$humidity_normal, alert_status=$alert_status" >> "$TEST_DIR/redis_monitor.log"
  sleep 0.5
done ) &
MONITOR_PID=$!

# Test AlertRule dependencies manually
echo -e "${YELLOW}[4] Testing AlertRule with dependencies...${NC}"

echo -e "  - Setting input values first to trigger native rule evaluation..."
redis-cli set input:temperature 31  # >30 to trigger HighTemperatureRule
redis-cli set input:humidity 75   # >70 to ensure HumidityRule sets humidity_normal = false

echo -e "  - Waiting 3 seconds to let natural rule evaluation occur..."
sleep 3

# Debug Redis state
echo -e "  - Debugging Redis state after natural rule execution:"
echo -e "    * Redis keys:"
redis-cli keys "*" | sort
echo -e "    * output:high_temperature = $(redis-cli get output:high_temperature)"
echo -e "    * output:humidity_normal = $(redis-cli get output:humidity_normal)"
echo -e "    * input:temperature = $(redis-cli get input:temperature)"
echo -e "    * input:humidity = $(redis-cli get input:humidity)"
echo -e "    * output:alert_status = $(redis-cli get output:alert_status)"
echo -e "    * output:alert_message = $(redis-cli get output:alert_message)"

# Now manually set dependencies to test if manual setup works
echo -e "  - Setting rule dependencies directly..."
redis-cli set output:high_temperature true
redis-cli set output:humidity_normal false

# Wait longer for rule processing
echo -e "  - Waiting for rule processing (10 seconds)..."
sleep 10

# Check rule output
echo -e "  - Checking rule outputs..."
ALERT_STATUS=$(redis-cli get output:alert_status)
ALERT_MESSAGE=$(redis-cli get output:alert_message)

expected_status="critical"
expected_message="High temperature alert: 31 with abnormal humidity: 75"

# Print actual vs expected
echo -e "  - Final Alert Status:\n    Expected: $expected_status\n    Actual:   $ALERT_STATUS"
echo -e "  - Final Alert Message:\n    Expected: $expected_message\n    Actual:   $ALERT_MESSAGE"

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
echo -e "${YELLOW}[5] Cleaning up...${NC}"
kill $MONITOR_PID || true
kill $BEACON_PID || true
sleep 1
echo -e "${GREEN}✓ Monitoring stopped. Log file: $TEST_DIR/redis_monitor.log${NC}"
echo -e "${YELLOW}Displaying Redis monitoring log:${NC}"
cat "$TEST_DIR/redis_monitor.log"

echo -e "\n${YELLOW}Displaying Beacon process log:${NC}"
tail -n 30 "$TEST_DIR/beacon.log" | grep -i rule

redis-cli keys "*" | xargs -r redis-cli del
echo -e "${GREEN}✓ Cleanup complete${NC}"

# Exit with test result
exit $TEST_RESULT
