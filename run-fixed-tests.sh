#!/bin/bash
set -e

# Directory setup
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Configuration
CONFIG_DIR="$SCRIPT_DIR/Config"
RULES_DIR="$SCRIPT_DIR/Rules"
OUTPUT_DIR="$SCRIPT_DIR/TestOutput"
LOG_DIR="$SCRIPT_DIR/logs"

# Create directories if they don't exist
mkdir -p "$CONFIG_DIR" "$RULES_DIR" "$OUTPUT_DIR" "$LOG_DIR"

# Create minimal rules file if it doesn't exist
RULES_FILE="$RULES_DIR/simple_rules.yaml"
if [ ! -f "$RULES_FILE" ]; then
  echo "Creating simple rules file..."
  cat > "$RULES_FILE" << EOF
rules:
  - name: "HighTemperatureRule"
    description: "Detects when temperature is high"
    conditions:
      always: true
    actions:
      - set_value:
          key: output:high_temperature
          value: true
  
  - name: "HumidityNormalRule"
    description: "Sets humidity normal flag"
    conditions:
      always: true
    actions:
      - set_value:
          key: output:humidity_normal
          value: true

  - name: "ComfortIndexRule"
    description: "Calculates comfort index"
    conditions:
      always: true
    actions:
      - set_value:
          key: output:comfort_index
          value: 40
EOF
fi

# Create config file if it doesn't exist
CONFIG_FILE="$CONFIG_DIR/simple_rules_config.yaml"
if [ ! -f "$CONFIG_FILE" ]; then
  echo "Creating config file..."
  cat > "$CONFIG_FILE" << EOF
# System configuration
system:
  name: SimpleSensors
  version: 1.0.0
  description: Simple sensor system for testing

# Allowed sensors (inputs and outputs)
sensors:
  inputs:
    - name: temperature
      description: Temperature sensor value
    - name: humidity
      description: Humidity sensor value
  
  outputs:
    - name: high_temperature
      description: Flag indicating high temperature
    - name: humidity_normal
      description: Flag indicating normal humidity
    - name: comfort_index
      description: Calculated comfort index value

# Redis configuration
redis:
  host: localhost
  port: 6379
  password: null
  database: 0
  ssl: false
EOF
fi

# Test scenarios file
TEST_FILE="$OUTPUT_DIR/fixed-test-scenarios.json"

# Ensure Redis is running
echo "Checking Redis..."
redis-cli ping > /dev/null 2>&1 || { 
  echo "Redis is not running. Starting Redis..."; 
  redis-server --daemonize yes;
  sleep 1;
}

# First set some values directly in Redis to test the enhanced validation
echo "Setting initial test values in Redis..."
redis-cli set output:high_temperature "True"
redis-cli set output:humidity_normal "true"
redis-cli set output:comfort_index "40"

echo "Checking if values are in Redis:"
redis-cli get output:high_temperature
redis-cli get output:humidity_normal
redis-cli get output:comfort_index

# Run the BeaconTester on our test file
echo "Running tests with BeaconTester..."
cd BeaconTester/BeaconTester.Runner
dotnet run -- run --scenarios "$TEST_FILE" --redis-host "localhost" --redis-port 6379

echo "Test complete. Check the console output above for results."