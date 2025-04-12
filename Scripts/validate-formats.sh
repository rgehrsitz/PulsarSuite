#!/bin/bash
# Test script to validate format handling improvements in BeaconTester

# Get absolute paths to directories
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BEACONTESTER_DIR="$PROJECT_ROOT/BeaconTester"
OUTPUT_DIR="$PROJECT_ROOT/Output/FormatTests"

# Create output directories
mkdir -p "$OUTPUT_DIR"

echo "===== Format Validation Test ====="
echo "Testing various format variations between Beacon and BeaconTester"

# Ensure Redis is running
echo "===== Checking Environment ====="
"$PROJECT_ROOT/Scripts/setup-environment.sh"

# Create test file with different format edge cases
echo "Creating test scenarios with format variations..."
cat > "$OUTPUT_DIR/format-tests.json" << EOF
{
  "scenarios": [
    {
      "name": "Boolean Format Variations",
      "description": "Tests different format variations of boolean values",
      "preTestOutputs": {
        "output:bool1": "true",
        "output:bool2": "True",
        "output:bool3": "TRUE",
        "output:bool4": "1",
        "output:bool5": "yes",
        "output:bool6": "false",
        "output:bool7": "False",
        "output:bool8": "FALSE",
        "output:bool9": "0",
        "output:bool10": "no"
      },
      "steps": [
        {
          "name": "Test boolean string formats",
          "inputs": [
            { "key": "input:bool1", "value": "true" },
            { "key": "input:bool2", "value": "True" },
            { "key": "input:bool3", "value": "TRUE" },
            { "key": "input:bool4", "value": "1" },
            { "key": "input:bool5", "value": "yes" },
            { "key": "input:bool6", "value": "false" },
            { "key": "input:bool7", "value": "False" },
            { "key": "input:bool8", "value": "FALSE" },
            { "key": "input:bool9", "value": "0" },
            { "key": "input:bool10", "value": "no" }
          ],
          "expectations": [
            { "key": "output:bool1", "expected": true, "validator": "boolean" },
            { "key": "output:bool2", "expected": "True", "validator": "boolean" },
            { "key": "output:bool3", "expected": "TRUE", "validator": "boolean" },
            { "key": "output:bool4", "expected": true, "validator": "boolean" },
            { "key": "output:bool5", "expected": true, "validator": "boolean" },
            { "key": "output:bool6", "expected": false, "validator": "boolean" },
            { "key": "output:bool7", "expected": "False", "validator": "boolean" },
            { "key": "output:bool8", "expected": "FALSE", "validator": "boolean" },
            { "key": "output:bool9", "expected": false, "validator": "boolean" },
            { "key": "output:bool10", "expected": false, "validator": "boolean" }
          ]
        }
      ]
    },
    {
      "name": "Numeric Format Variations",
      "description": "Tests different format variations of numeric values",
      "preTestOutputs": {
        "output:num1": "123",
        "output:num2": 456,
        "output:num3": 3.14,
        "output:num4": "3,14",
        "output:num5": -42,
        "output:num6": 1000
      },
      "steps": [
        {
          "name": "Test numeric formats",
          "inputs": [
            { "key": "input:num1", "value": 123 },
            { "key": "input:num2", "value": "456" },
            { "key": "input:num3", "value": "3.14" },
            { "key": "input:num4", "value": "3,14" },
            { "key": "input:num5", "value": "-42" },
            { "key": "input:num6", "value": "1e3" }
          ],
          "expectations": [
            { "key": "output:num1", "expected": 123, "validator": "numeric", "tolerance": 0.0001 },
            { "key": "output:num2", "expected": "456", "validator": "numeric", "tolerance": 0.0001 },
            { "key": "output:num3", "expected": 3.14, "validator": "numeric", "tolerance": 0.0001 },
            { "key": "output:num4", "expected": "3,14", "validator": "numeric", "tolerance": 0.0001 },
            { "key": "output:num5", "expected": -42, "validator": "numeric", "tolerance": 0.0001 },
            { "key": "output:num6", "expected": 1000, "validator": "numeric", "tolerance": 0.0001 }
          ]
        }
      ]
    },
    {
      "name": "String Format Variations",
      "description": "Tests string comparisons with whitespace and case variations",
      "preTestOutputs": {
        "output:str1": "hello",
        "output:str2": "  world  ",
        "output:str3": "MIXED case",
        "output:str4": "",
        "output:str5": "   "
      },
      "steps": [
        {
          "name": "Test string formats",
          "inputs": [
            { "key": "input:str1", "value": "hello" },
            { "key": "input:str2", "value": "  world  " },
            { "key": "input:str3", "value": "Mixed CASE" },
            { "key": "input:str4", "value": "" },
            { "key": "input:str5", "value": "   " }
          ],
          "expectations": [
            { "key": "output:str1", "expected": "hello", "validator": "string" },
            { "key": "output:str2", "expected": "world", "validator": "string" },
            { "key": "output:str3", "expected": "mixed case", "validator": "string" },
            { "key": "output:str4", "expected": "", "validator": "string" },
            { "key": "output:str5", "expected": "", "validator": "string" }
          ]
        }
      ]
    }
  ]
}
EOF

# Set up mock values in Redis to simulate Beacon's behavior
echo "Setting up test values in Redis..."

# Set both input and output values for complete testing

# Boolean variations - Input
redis-cli set input:bool1 "true"
redis-cli set input:bool2 "True"
redis-cli set input:bool3 "TRUE"
redis-cli set input:bool4 "1"
redis-cli set input:bool5 "yes"
redis-cli set input:bool6 "false"
redis-cli set input:bool7 "False"
redis-cli set input:bool8 "FALSE"
redis-cli set input:bool9 "0"
redis-cli set input:bool10 "no"

# Boolean variations - Output
redis-cli set output:bool1 "true"
redis-cli set output:bool2 "True"
redis-cli set output:bool3 "TRUE"
redis-cli set output:bool4 "1"
redis-cli set output:bool5 "yes"
redis-cli set output:bool6 "false"
redis-cli set output:bool7 "False"
redis-cli set output:bool8 "FALSE"
redis-cli set output:bool9 "0"
redis-cli set output:bool10 "no"

# Numeric variations - Input
redis-cli set input:num1 "123"
redis-cli set input:num2 456
redis-cli set input:num3 3.14
redis-cli set input:num4 3,14
redis-cli set input:num5 -42
redis-cli set input:num6 1e3

# Numeric variations - Output
redis-cli set output:num1 "123"
redis-cli set output:num2 456
redis-cli set output:num3 3.14
redis-cli set output:num4 3,14
redis-cli set output:num5 -42
redis-cli set output:num6 1000

# String variations - Input
redis-cli set input:str1 "hello"
redis-cli set input:str2 "  world  "
redis-cli set input:str3 "Mixed CASE"
redis-cli set input:str4 ""
redis-cli set input:str5 "   "

# String variations - Output
redis-cli set output:str1 "hello"
redis-cli set output:str2 "  world  "
redis-cli set output:str3 "MIXED case"
redis-cli set output:str4 ""
redis-cli set output:str5 "   "

# Run the BeaconTester against our test scenarios
echo "Running BeaconTester with format validation tests..."
dotnet run --project "$BEACONTESTER_DIR/BeaconTester.Runner/BeaconTester.Runner.csproj" run \
  --scenarios="$OUTPUT_DIR/format-tests.json" \
  --output="$OUTPUT_DIR/format-results.json" \
  --redis-host=localhost --redis-port=6379

TEST_RESULT=$?

# Generate a report
echo "Generating test report..."
dotnet run --project "$BEACONTESTER_DIR/BeaconTester.Runner/BeaconTester.Runner.csproj" report \
  --results="$OUTPUT_DIR/format-results.json" \
  --output="$OUTPUT_DIR/format-report.html" \
  --format=html

# Print summary
echo ""
echo "===== Test Summary ====="
if [ $TEST_RESULT -eq 0 ]; then
    echo "Format validation test PASSED"
else
    echo "Format validation test FAILED with exit code $TEST_RESULT"
fi

echo ""
echo "Output files:"
echo "  $OUTPUT_DIR/format-results.json - Test results"
echo "  $OUTPUT_DIR/format-report.html - Test report"

# Return test result
exit $TEST_RESULT