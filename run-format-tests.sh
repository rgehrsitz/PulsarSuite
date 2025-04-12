#\!/bin/bash
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

RULES_FILE="$RULES_DIR/format_test_rules.yaml"
CONFIG_FILE="$CONFIG_DIR/format_test_config.yaml"

# Ensure Redis is running
echo "Checking Redis..."
redis-cli ping > /dev/null 2>&1 || { 
  echo "Redis is not running. Starting Redis..."; 
  redis-server --daemonize yes;
  sleep 1;
}

# Clear any existing Redis keys to start fresh
echo "Clearing existing Redis keys..."
redis-cli keys "output:*" | xargs -r redis-cli del

# Generate automated test scenarios
echo "Using pre-generated test scenarios..."
# Test scenarios are already created in TestOutput/format_test_scenarios.json
    {
      "name": "BooleanFormatTests",
      "description": "Tests boolean format handling with various representations",
      "clearOutputs": false,
      "timeoutMultiplier": 1.2,
      "steps": [
        {
          "name": "Check boolean values",
          "delay": 1000,
          "expectations": [
            {
              "key": "output:bool_true",
              "expected": true,
              "validator": "boolean",
              "timeoutMs": 5000
            },
            {
              "key": "output:bool_false",
              "expected": false,
              "validator": "boolean",
              "timeoutMs": 5000
            },
            {
              "key": "output:bool_numeric_true",
              "expected": true,
              "validator": "boolean",
              "timeoutMs": 5000
            },
            {
              "key": "output:bool_numeric_false",
              "expected": false,
              "validator": "boolean",
              "timeoutMs": 5000
            },
            {
              "key": "output:bool_string_true",
              "expected": true,
              "validator": "boolean",
              "timeoutMs": 5000
            },
            {
              "key": "output:bool_string_false",
              "expected": false,
              "validator": "boolean",
              "timeoutMs": 5000
            },
            {
              "key": "output:bool_yes",
              "expected": true,
              "validator": "boolean",
              "timeoutMs": 5000
            },
            {
              "key": "output:bool_no",
              "expected": false,
              "validator": "boolean",
              "timeoutMs": 5000
            }
          ]
        }
      ]
    },
    {
      "name": "NumericFormatTests",
      "description": "Tests numeric format handling with various representations",
      "clearOutputs": false,
      "timeoutMultiplier": 1.2,
      "steps": [
        {
          "name": "Check numeric values",
          "delay": 1000,
          "expectations": [
            {
              "key": "output:number_integer",
              "expected": 42,
              "validator": "numeric",
              "timeoutMs": 5000
            },
            {
              "key": "output:number_decimal_period",
              "expected": 3.14,
              "validator": "numeric",
              "timeoutMs": 5000,
              "tolerance": 0.001
            },
            {
              "key": "output:number_decimal_comma",
              "expected": 3.14,
              "validator": "numeric",
              "timeoutMs": 5000,
              "tolerance": 0.001
            },
            {
              "key": "output:number_scientific",
              "expected": 123,
              "validator": "numeric",
              "timeoutMs": 5000
            },
            {
              "key": "output:number_string",
              "expected": 100,
              "validator": "numeric",
              "timeoutMs": 5000
            },
            {
              "key": "output:negative_number",
              "expected": -42,
              "validator": "numeric",
              "timeoutMs": 5000
            }
          ]
        }
      ]
    },
    {
      "name": "StringFormatTests",
      "description": "Tests string format handling with various representations",
      "clearOutputs": false,
      "timeoutMultiplier": 1.2,
      "steps": [
        {
          "name": "Check string values",
          "delay": 1000,
          "expectations": [
            {
              "key": "output:string_plain",
              "expected": "Hello World",
              "validator": "string",
              "timeoutMs": 5000
            },
            {
              "key": "output:string_whitespace",
              "expected": "Padded String",
              "validator": "string",
              "timeoutMs": 5000
            },
            {
              "key": "output:string_mixed_case",
              "expected": "mixed case",
              "validator": "string",
              "timeoutMs": 5000
            },
            {
              "key": "output:string_special_chars",
              "expected": "Special & Characters\! $",
              "validator": "string",
              "timeoutMs": 5000
            }
          ]
        }
      ]
    }
  ]
}
EOJSON

# Set initial values directly in Redis to test validation
echo "Setting test values in Redis..."

# Boolean values
redis-cli set output:bool_true "true"
redis-cli set output:bool_false "false"
redis-cli set output:bool_numeric_true "1"
redis-cli set output:bool_numeric_false "0"
redis-cli set output:bool_string_true "True"
redis-cli set output:bool_string_false "False"
redis-cli set output:bool_yes "yes"
redis-cli set output:bool_no "no"

# Numeric values
redis-cli set output:number_integer "42"
redis-cli set output:number_decimal_period "3.14"
redis-cli set output:number_decimal_comma "3,14"
redis-cli set output:number_scientific "1.23e2"
redis-cli set output:number_string "100"
redis-cli set output:negative_number "-42"

# String values
redis-cli set output:string_plain "Hello World"
redis-cli set output:string_whitespace "  Padded String  "
redis-cli set output:string_mixed_case "MiXeD cAsE"
redis-cli set output:string_special_chars "Special & Characters\! $"

echo "Checking if values are in Redis:"
echo "Boolean values:"
redis-cli get output:bool_true
redis-cli get output:bool_false
redis-cli get output:bool_numeric_true
redis-cli get output:bool_numeric_false

echo "Numeric values:"
redis-cli get output:number_integer
redis-cli get output:number_decimal_period
redis-cli get output:number_decimal_comma
redis-cli get output:number_scientific

echo "String values:"
redis-cli get output:string_plain
redis-cli get output:string_whitespace
redis-cli get output:string_mixed_case

# Run BeaconTester
echo "Running tests with BeaconTester..."
cd BeaconTester/BeaconTester.Runner
dotnet run -- run --scenarios "$OUTPUT_DIR/format_test_scenarios.json" --redis-host "localhost" --redis-port 6379

echo "Test complete. Check the console output above for results."
