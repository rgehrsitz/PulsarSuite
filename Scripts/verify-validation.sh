#!/bin/bash
# Script to verify the validation improvements by running the full end-to-end test

# Get absolute paths to directories
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RULES_FILE="$PROJECT_ROOT/Rules/format_test_rules.yaml"
OUTPUT_DIR="$PROJECT_ROOT/Output/ValidationTest"
CONFIG_FILE="$PROJECT_ROOT/Config/format_test_config.yaml"

# Create output directories
mkdir -p "$OUTPUT_DIR"

echo "===== BeaconTester Validation Verification ====="
echo "Running complete validation of BeaconTester format handling"

# Ensure Redis is running and environment is set up
echo "===== Setting up Environment ====="
"$PROJECT_ROOT/Scripts/setup-environment.sh"

# Check if Pulsar is available
if [ ! -d "$PROJECT_ROOT/Pulsar" ]; then
    echo "Error: Pulsar directory not found at $PROJECT_ROOT/Pulsar"
    exit 1
fi

# Check if config file exists, or create a default one
if [ ! -f "$CONFIG_FILE" ]; then
    echo "Creating default system configuration..."
    mkdir -p "$(dirname "$CONFIG_FILE")"
    cat > "$CONFIG_FILE" << EOF
System:
  Name: BeaconTester
  RunMode: Development

Redis:
  Endpoints:
    - localhost:6379
  ConnectTimeout: 5000
  SyncTimeout: 1000
  AllowAdmin: true
EOF
fi

# Step 1: Compile the rules into a Beacon application
echo "===== Compiling Format Test Rules into Beacon ====="
"$PROJECT_ROOT/Scripts/compile-beacon.sh" "$RULES_FILE" "$OUTPUT_DIR/Beacon" --config="$CONFIG_FILE"

# Display config being used
echo "Using config file: $CONFIG_FILE"

if [ $? -ne 0 ]; then
    echo "Error: Failed to compile rules. Aborting test."
    exit 1
fi

# Step 2: Run the full end-to-end test
echo "===== Running End-to-End Test ====="
"$PROJECT_ROOT/Scripts/run-end-to-end.sh" "$RULES_FILE" "$CONFIG_FILE"

END_TO_END_RESULT=$?

# Step 3: Run the specific format validation test
echo "===== Running Format Validation Test ====="
"$PROJECT_ROOT/Scripts/validate-formats.sh"

FORMAT_TEST_RESULT=$?

# Print summary
echo ""
echo "===== Validation Results ====="
echo "End-to-End Test: $([ $END_TO_END_RESULT -eq 0 ] && echo 'PASSED' || echo 'FAILED')"
echo "Format Test: $([ $FORMAT_TEST_RESULT -eq 0 ] && echo 'PASSED' || echo 'FAILED')"

if [ $END_TO_END_RESULT -eq 0 ] && [ $FORMAT_TEST_RESULT -eq 0 ]; then
    echo ""
    echo "🎉 VALIDATION SUCCESSFUL! 🎉"
    echo "BeaconTester validation improvements have been verified"
    echo ""
    echo "The changes made to the RedisAdapter have successfully improved the format"
    echo "handling between BeaconTester and Beacon. The BeaconTester can now handle:"
    echo ""
    echo "1. Case variations in boolean values (True/true, False/false)"
    echo "2. Alternative boolean representations (1/0, yes/no)"
    echo "3. Numeric format variations (regional formats, scientific notation)"
    echo "4. String whitespace and case sensitivity handling"
    exit 0
else
    echo ""
    echo "❌ VALIDATION FAILED ❌"
    echo "One or more tests failed. Review the test results for details."
    exit 1
fi