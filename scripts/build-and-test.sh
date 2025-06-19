#!/bin/bash

# PulsarSuite Build and Test Script
# This script builds the entire PulsarSuite and runs end-to-end tests

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

echo -e "${BLUE}=== PulsarSuite Build and Test ===${NC}"
echo "Project root: $PROJECT_ROOT"

# Create output directories
OUTPUT_DIR="$PROJECT_ROOT/output"
LOGS_DIR="$OUTPUT_DIR/logs"
REPORTS_DIR="$OUTPUT_DIR/reports"

mkdir -p "$OUTPUT_DIR" "$LOGS_DIR" "$REPORTS_DIR"

# Log file
LOG_FILE="$LOGS_DIR/build-$(date +%Y%m%d-%H%M%S).log"

# Function to log messages
log() {
    echo -e "$1"
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $2" >> "$LOG_FILE"
}

# Function to run command with logging
run_cmd() {
    local cmd="$1"
    local description="$2"

    log "${YELLOW}Running: $description${NC}" "$description"
    log "${BLUE}Command: $cmd${NC}" "Command: $cmd"

    if eval "$cmd" >> "$LOG_FILE" 2>&1; then
        log "${GREEN}✓ Success: $description${NC}" "Success: $description"
    else
        log "${RED}✗ Failed: $description${NC}" "Failed: $description"
        log "${RED}Check log file: $LOG_FILE${NC}" "Check log file: $LOG_FILE"
        exit 1
    fi
}

# Parse arguments for rules and config file
RULES_FILE=""
CONFIG_FILE=""

while [[ $# -gt 0 ]]; do
  case $1 in
    --rules)
      RULES_FILE="$2"
      shift 2
      ;;
    --config)
      CONFIG_FILE="$2"
      shift 2
      ;;
    *)
      echo "Unknown argument: $1"
      exit 1
      ;;
  esac
done

# Set defaults if not provided
RULES_DIR="$PROJECT_ROOT/rules"
DEFAULT_RULES_FILE="$RULES_DIR/temperature_rules.yaml"
DEFAULT_CONFIG_FILE="$PROJECT_ROOT/config/system_config.yaml"

if [ -z "$RULES_FILE" ]; then
  if [ -f "$DEFAULT_RULES_FILE" ]; then
    RULES_FILE="$DEFAULT_RULES_FILE"
  else
    # Use first .yaml file in rules dir
    RULES_FILE=$(find "$RULES_DIR" -name "*.yaml" | head -1)
  fi
fi

if [ -z "$CONFIG_FILE" ]; then
  CONFIG_FILE="$DEFAULT_CONFIG_FILE"
fi

if [ ! -f "$RULES_FILE" ]; then
  log "${RED}Error: Rules file not found: $RULES_FILE${NC}" "Error: Rules file not found: $RULES_FILE"
  exit 1
fi

if [ ! -f "$CONFIG_FILE" ]; then
  log "${RED}Error: Config file not found: $CONFIG_FILE${NC}" "Error: Config file not found: $CONFIG_FILE"
  exit 1
fi

log "${BLUE}Using rule file: $RULES_FILE${NC}" "Using rule file: $RULES_FILE"
log "${BLUE}Using config file: $CONFIG_FILE${NC}" "Using config file: $CONFIG_FILE"

# Step 1: Build Pulsar Compiler
log "${BLUE}Step 1: Building Pulsar Compiler...${NC}" "Step 1: Building Pulsar Compiler"
run_cmd "cd '$PROJECT_ROOT/Pulsar/Pulsar.Compiler' && dotnet build -c Release" "Build Pulsar Compiler"

# Step 2: Build BeaconTester
log "${BLUE}Step 2: Building BeaconTester...${NC}" "Step 2: Building BeaconTester"
run_cmd "cd '$PROJECT_ROOT/BeaconTester' && dotnet build -c Release" "Build BeaconTester"

# Step 3: Compile rules using Pulsar
log "${BLUE}Step 3: Compiling rules with Pulsar...${NC}" "Step 3: Compiling rules with Pulsar"

OUTPUT_PATH="$OUTPUT_DIR/beacon"

run_cmd "cd '$PROJECT_ROOT/Pulsar/Pulsar.Compiler' && dotnet run --project . -- beacon --rules '$RULES_FILE' --config '$CONFIG_FILE' --output '$OUTPUT_PATH' --target linux-x64" "Compile rules with Pulsar"

# Step 4: Build Beacon runtime
log "${BLUE}Step 4: Building Beacon runtime...${NC}" "Step 4: Building Beacon runtime"
run_cmd "cd '$OUTPUT_PATH/Beacon' && dotnet build -c Release" "Build Beacon runtime"

# Step 5: Generate test scenarios from rules
log "${BLUE}Step 5: Generating test scenarios from rules...${NC}" "Step 5: Generating test scenarios from rules"
GENERATED_TEST_FILE="$REPORTS_DIR/generated_test_scenarios.json"
run_cmd "cd '$PROJECT_ROOT/BeaconTester/BeaconTester.Runner' && dotnet run --project . -- generate --rules '$RULES_FILE' --output '$GENERATED_TEST_FILE'" "Generate test scenarios from rules"

# Step 6: Start Beacon runtime in background
log "${BLUE}Step 6: Starting Beacon runtime...${NC}" "Step 6: Starting Beacon runtime"
BEACON_PID=""

# Find the actual Beacon runtime location
BEACON_RUNTIME_PATH=""
POSSIBLE_PATHS=(
    "$OUTPUT_PATH/Beacon/Beacon.Runtime/bin/Release/net9.0/linux-x64/Beacon.Runtime.dll"
    "$OUTPUT_PATH/Beacon/Beacon.Runtime/bin/Release/net8.0/linux-x64/Beacon.Runtime.dll"
    "$OUTPUT_PATH/Beacon/Beacon.Runtime/bin/Debug/net9.0/linux-x64/Beacon.Runtime.dll"
    "$OUTPUT_PATH/Beacon/Beacon.Runtime/bin/Debug/net8.0/linux-x64/Beacon.Runtime.dll"
    "$OUTPUT_PATH/Beacon/Beacon.Runtime/bin/Release/net9.0/Beacon.Runtime.dll"
    "$OUTPUT_PATH/Beacon/Beacon.Runtime/bin/Release/net8.0/Beacon.Runtime.dll"
)

for path in "${POSSIBLE_PATHS[@]}"; do
    if [ -f "$path" ]; then
        BEACON_RUNTIME_PATH="$path"
        break
    fi
done

if [ -n "$BEACON_RUNTIME_PATH" ]; then
    log "${GREEN}Starting Beacon runtime from: $BEACON_RUNTIME_PATH${NC}" "Starting Beacon runtime"
    cd "$(dirname "$BEACON_RUNTIME_PATH")"
    dotnet "$(basename "$BEACON_RUNTIME_PATH")" &
    BEACON_PID=$!
    log "${GREEN}Beacon runtime started with PID: $BEACON_PID${NC}" "Beacon runtime started"
    # Give Beacon time to start up
    sleep 3
else
    log "${YELLOW}Warning: Beacon runtime not found in expected locations${NC}" "Warning: Beacon runtime not found"
    log "${YELLOW}Available Beacon files:${NC}" "Available Beacon files"
    find "$OUTPUT_PATH" -name "Beacon.Runtime.dll" -type f 2>/dev/null | head -10
fi

# Step 7: Running BeaconTester with generated scenarios
log "${BLUE}Step 7: Running BeaconTester with generated scenarios...${NC}" "Step 7: Running BeaconTester with generated scenarios"
run_cmd "cd '$PROJECT_ROOT/BeaconTester/BeaconTester.Runner' && dotnet run --project . -- run --scenarios '$GENERATED_TEST_FILE' --output '$REPORTS_DIR'" "Run BeaconTester"

# Step 8: Stop Beacon runtime if it was started
if [ ! -z "$BEACON_PID" ]; then
    log "${BLUE}Step 8: Stopping Beacon runtime (PID: $BEACON_PID)...${NC}" "Step 8: Stopping Beacon runtime"
    kill $BEACON_PID 2>/dev/null || true
    log "${GREEN}Beacon runtime stopped${NC}" "Beacon runtime stopped"
fi

# Step 9: Generate report
log "${BLUE}Step 9: Generating test report...${NC}" "Step 9: Generating test report"
if [ -f "$GENERATED_TEST_FILE" ]; then
    run_cmd "cd '$PROJECT_ROOT/BeaconTester/BeaconTester.Runner' && dotnet run --project . -- report --results '$REPORTS_DIR/test_results.json' --output '$REPORTS_DIR/report.html'" "Generate test report"
fi

log "${GREEN}=== Build and Test Complete ===${NC}" "Build and Test Complete"
log "${GREEN}Output directory: $OUTPUT_DIR${NC}" "Output directory: $OUTPUT_DIR"
log "${GREEN}Log file: $LOG_FILE${NC}" "Log file: $LOG_FILE"
if [ -f "$GENERATED_TEST_FILE" ]; then
    log "${GREEN}Test report: $REPORTS_DIR/report.html${NC}" "Test report: $REPORTS_DIR/report.html"
fi