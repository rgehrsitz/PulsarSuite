#!/bin/bash

# PulsarSuite Build and Test Script
# This script provides a simplified workflow for building and testing Beacon applications

set -e  # Exit on any error

# Configuration
PROJECT_NAME=${1:-"TemperatureExample"}
RULES_FILE="src/Rules/${PROJECT_NAME}/rules/temperature_rules.yaml"
CONFIG_FILE="src/Rules/${PROJECT_NAME}/config/system_config.yaml"
OUTPUT_DIR="output"
BIN_DIR="${OUTPUT_DIR}/Bin/${PROJECT_NAME}"
DIST_DIR="${OUTPUT_DIR}/dist/${PROJECT_NAME}"
TESTS_DIR="${OUTPUT_DIR}/Tests/${PROJECT_NAME}"
REPORTS_DIR="${OUTPUT_DIR}/reports/${PROJECT_NAME}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Helper functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check prerequisites
check_prerequisites() {
    log_info "Checking prerequisites..."

    if ! command -v dotnet &> /dev/null; then
        log_error "dotnet CLI not found. Please install .NET SDK 8.0 or higher."
        exit 1
    fi

    if ! command -v redis-cli &> /dev/null; then
        log_warning "redis-cli not found. Redis server may not be running."
    fi

    if [ ! -f "$RULES_FILE" ]; then
        log_error "Rules file not found: $RULES_FILE"
        exit 1
    fi

    if [ ! -f "$CONFIG_FILE" ]; then
        log_error "Config file not found: $CONFIG_FILE"
        exit 1
    fi

    log_success "Prerequisites check completed"
}

# Clean environment
clean_environment() {
    log_info "Cleaning environment..."

    # Stop any running Beacon processes
    pkill -f Beacon.Runtime || true
    pkill -f BeaconTester || true

    # Clear Redis
    redis-cli FLUSHALL || true

    # Remove output directories
    rm -rf "$BIN_DIR" "$DIST_DIR" "$TESTS_DIR" "$REPORTS_DIR"

    log_success "Environment cleaned"
}

# Validate rules
validate_rules() {
    log_info "Validating rules..."
    dotnet run --project Pulsar/Pulsar.Compiler/Pulsar.Compiler.csproj validate \
        --rules="$RULES_FILE" \
        --config="$CONFIG_FILE"
    log_success "Rules validation completed"
}

# Compile rules
compile_rules() {
    log_info "Compiling rules..."
    mkdir -p "$BIN_DIR"
    dotnet run --project Pulsar/Pulsar.Compiler/Pulsar.Compiler.csproj compile \
        --rules="$RULES_FILE" \
        --config="$CONFIG_FILE" \
        --output="$BIN_DIR"
    log_success "Rules compilation completed"
}

# Generate Beacon application
generate_beacon() {
    log_info "Generating Beacon application..."
    mkdir -p "$DIST_DIR"
    dotnet run --project Pulsar/Pulsar.Compiler/Pulsar.Compiler.csproj beacon \
        --rules="$RULES_FILE" \
        --compiled-rules-dir="$BIN_DIR" \
        --output="$DIST_DIR" \
        --config="$CONFIG_FILE" \
        --target=linux-x64
    log_success "Beacon application generation completed"
}

# Build Beacon runtime
build_beacon() {
    log_info "Building Beacon runtime..."
    dotnet publish "$DIST_DIR/Beacon/Beacon.Runtime/Beacon.Runtime.csproj" \
        -c Debug \
        -r linux-x64 \
        --self-contained true \
        /p:PublishSingleFile=true
    log_success "Beacon runtime build completed"
}

# Generate test scenarios
generate_tests() {
    log_info "Generating test scenarios..."
    mkdir -p "$TESTS_DIR"
    dotnet run --project BeaconTester/BeaconTester.Runner/BeaconTester.Runner.csproj generate \
        --rules="$RULES_FILE" \
        --output="$TESTS_DIR/test_scenarios.json"
    log_success "Test scenarios generation completed"
}

# Start Beacon (in background)
start_beacon() {
    log_info "Starting Beacon runtime..."
    mkdir -p "$REPORTS_DIR"

    # Start Beacon in background
    cd "$DIST_DIR/Beacon/Beacon.Runtime/bin/Debug/net8.0/linux-x64"
    nohup dotnet Beacon.Runtime.dll --redis-host=localhost --redis-port=6379 --verbose > "$REPORTS_DIR/beacon.log" 2>&1 &
    BEACON_PID=$!

    # Wait for Beacon to start
    sleep 5

    # Check if Beacon is running
    if ps -p $BEACON_PID > /dev/null; then
        log_success "Beacon started with PID: $BEACON_PID"
        echo $BEACON_PID > "$REPORTS_DIR/beacon.pid"
    else
        log_error "Failed to start Beacon"
        exit 1
    fi
}

# Run tests
run_tests() {
    log_info "Running tests..."
    mkdir -p "$REPORTS_DIR"

    # Set environment variables for BeaconTester
    export BEACON_CYCLE_TIME=500
    export STEP_DELAY_MULTIPLIER=2
    export TIMEOUT_MULTIPLIER=3

    dotnet run --project BeaconTester/BeaconTester.Runner/BeaconTester.Runner.csproj run \
        --scenarios="$TESTS_DIR/test_scenarios.json" \
        --output="$REPORTS_DIR/test_results.json" \
        --redis-host=localhost \
        --redis-port=6379

    log_success "Test execution completed"
}

# Stop Beacon
stop_beacon() {
    log_info "Stopping Beacon runtime..."

    if [ -f "$REPORTS_DIR/beacon.pid" ]; then
        BEACON_PID=$(cat "$REPORTS_DIR/beacon.pid")
        if ps -p $BEACON_PID > /dev/null; then
            kill $BEACON_PID
            log_success "Beacon stopped"
        fi
        rm -f "$REPORTS_DIR/beacon.pid"
    else
        pkill -f Beacon.Runtime || true
        log_warning "Beacon process not found or already stopped"
    fi
}

# Show results
show_results() {
    log_info "Test results:"
    if [ -f "$REPORTS_DIR/test_results.json" ]; then
        # Count successful and failed tests
        SUCCESS_COUNT=$(grep -c '"success": true' "$REPORTS_DIR/test_results.json" || echo "0")
        FAILED_COUNT=$(grep -c '"success": false' "$REPORTS_DIR/test_results.json" || echo "0")

        echo "  Successful tests: $SUCCESS_COUNT"
        echo "  Failed tests: $FAILED_COUNT"

        if [ "$FAILED_COUNT" -gt 0 ]; then
            log_warning "Some tests failed. Check $REPORTS_DIR/test_results.json for details."
        else
            log_success "All tests passed!"
        fi
    else
        log_error "Test results file not found"
    fi
}

# Main workflow
main() {
    echo "=========================================="
    echo "PulsarSuite Build and Test Workflow"
    echo "Project: $PROJECT_NAME"
    echo "=========================================="

    check_prerequisites
    clean_environment
    validate_rules
    compile_rules
    generate_beacon
    build_beacon
    generate_tests
    start_beacon

    # Run tests
    run_tests

    # Stop Beacon
    stop_beacon

    # Show results
    show_results

    echo "=========================================="
    log_success "Workflow completed!"
    echo "Output files:"
    echo "  - Compiled rules: $BIN_DIR"
    echo "  - Beacon application: $DIST_DIR"
    echo "  - Test scenarios: $TESTS_DIR/test_scenarios.json"
    echo "  - Test results: $REPORTS_DIR/test_results.json"
    echo "  - Beacon logs: $REPORTS_DIR/beacon.log"
    echo "=========================================="
}

# Handle command line arguments
case "${2:-}" in
    "clean")
        clean_environment
        ;;
    "validate")
        check_prerequisites
        validate_rules
        ;;
    "compile")
        check_prerequisites
        validate_rules
        compile_rules
        ;;
    "build")
        check_prerequisites
        validate_rules
        compile_rules
        generate_beacon
        build_beacon
        ;;
    "test")
        check_prerequisites
        generate_tests
        start_beacon
        run_tests
        stop_beacon
        show_results
        ;;
    "start-beacon")
        start_beacon
        echo "Beacon is running. Use 'stop-beacon' to stop it."
        ;;
    "stop-beacon")
        stop_beacon
        ;;
    *)
        main
        ;;
esac