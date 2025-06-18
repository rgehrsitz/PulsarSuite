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

# Step 1: Build Pulsar Compiler
log "${BLUE}Step 1: Building Pulsar Compiler...${NC}" "Step 1: Building Pulsar Compiler"
run_cmd "cd '$PROJECT_ROOT/Pulsar/Pulsar.Compiler' && dotnet build -c Release" "Build Pulsar Compiler"

# Step 2: Build BeaconTester
log "${BLUE}Step 2: Building BeaconTester...${NC}" "Step 2: Building BeaconTester"
run_cmd "cd '$PROJECT_ROOT/BeaconTester' && dotnet build -c Release" "Build BeaconTester"

# Step 3: Compile rules using Pulsar
log "${BLUE}Step 3: Compiling rules with Pulsar...${NC}" "Step 3: Compiling rules with Pulsar"

# Use the new consolidated structure
RULES_DIR="$PROJECT_ROOT/rules"
CONFIG_FILE="$PROJECT_ROOT/config/system_config.yaml"
OUTPUT_PATH="$OUTPUT_DIR/beacon"

# Check if rules directory exists
if [ ! -d "$RULES_DIR" ]; then
    log "${RED}Error: Rules directory not found: $RULES_DIR${NC}" "Error: Rules directory not found: $RULES_DIR"
    log "${YELLOW}Please run the consolidation script first: ./scripts/consolidate-rules.sh${NC}" "Please run the consolidation script first"
    exit 1
fi

# Check if config file exists
if [ ! -f "$CONFIG_FILE" ]; then
    log "${RED}Error: Config file not found: $CONFIG_FILE${NC}" "Error: Config file not found: $CONFIG_FILE"
    exit 1
fi

# Use the first rule file found (or all rule files)
FIRST_RULE_FILE=$(find "$RULES_DIR" -name "*.yaml" | head -1)
if [ -z "$FIRST_RULE_FILE" ]; then
    log "${RED}Error: No rule files found in $RULES_DIR${NC}" "Error: No rule files found in $RULES_DIR"
    exit 1
fi

# Prefer temperature_rules.yaml if it exists
if [ -f "$RULES_DIR/temperature_rules.yaml" ]; then
    FIRST_RULE_FILE="$RULES_DIR/temperature_rules.yaml"
fi

log "${BLUE}Using rule file: $FIRST_RULE_FILE${NC}" "Using rule file: $FIRST_RULE_FILE"
log "${BLUE}Using config file: $CONFIG_FILE${NC}" "Using config file: $CONFIG_FILE"

run_cmd "cd '$PROJECT_ROOT/Pulsar/Pulsar.Compiler' && dotnet run --project . -- beacon --rules '$FIRST_RULE_FILE' --config '$CONFIG_FILE' --output '$OUTPUT_PATH'" "Compile rules with Pulsar"

# Step 4: Build Beacon runtime
log "${BLUE}Step 4: Building Beacon runtime...${NC}" "Step 4: Building Beacon runtime"
run_cmd "cd '$OUTPUT_PATH/Beacon' && dotnet build -c Release" "Build Beacon runtime"

# Step 5: Run BeaconTester
log "${BLUE}Step 5: Running BeaconTester...${NC}" "Step 5: Running BeaconTester"

# Find the test scenarios file
TEST_SCENARIOS="$PROJECT_ROOT/examples/Tests/DefaultProject/test_scenarios.json"
if [ ! -f "$TEST_SCENARIOS" ]; then
    log "${YELLOW}Warning: Test scenarios file not found: $TEST_SCENARIOS${NC}" "Warning: Test scenarios file not found"
    log "${YELLOW}Skipping BeaconTester execution${NC}" "Skipping BeaconTester execution"
else
    run_cmd "cd '$PROJECT_ROOT/BeaconTester/BeaconTester.Runner' && dotnet run --project . -- run --scenarios '$TEST_SCENARIOS' --output '$REPORTS_DIR'" "Run BeaconTester"
fi

# Step 6: Generate report
log "${BLUE}Step 6: Generating test report...${NC}" "Step 6: Generating test report"
if [ -f "$TEST_SCENARIOS" ]; then
    run_cmd "cd '$PROJECT_ROOT/BeaconTester/BeaconTester.Runner' && dotnet run --project . -- report --input '$REPORTS_DIR' --output '$REPORTS_DIR/report.html'" "Generate test report"
fi

log "${GREEN}=== Build and Test Complete ===${NC}" "Build and Test Complete"
log "${GREEN}Output directory: $OUTPUT_DIR${NC}" "Output directory: $OUTPUT_DIR"
log "${GREEN}Log file: $LOG_FILE${NC}" "Log file: $LOG_FILE"
if [ -f "$TEST_SCENARIOS" ]; then
    log "${GREEN}Test report: $REPORTS_DIR/report.html${NC}" "Test report: $REPORTS_DIR/report.html"
fi