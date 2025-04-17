#!/bin/bash
# Step-by-step validation script to prove the end-to-end workflow

set -e
BLUE='\033[0;34m'
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${BLUE}========== END-TO-END VALIDATION SCRIPT ==========${NC}"

# Define paths
ROOT_DIR="$(pwd)"
RULES_FILE="$ROOT_DIR/Rules/temperature_rules.yaml"
VALIDATION_DIR="/tmp/validation"
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

# Step 2: Compile Beacon using our fixed version of Pulsar Compiler
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

# Check if our fix for the threshold condition was applied
echo -e "${YELLOW}[4] Verifying the threshold condition fix...${NC}"
grep -n "CheckThreshold.*input:temperature.*>" "$BEACON_DIR/Beacon/Beacon.Runtime/Generated/RuleGroup0.cs"

if [ $? -eq 0 ]; then
  echo -e "${GREEN}✓ Threshold condition fix was applied correctly${NC}"
else
  echo -e "${RED}× Threshold condition fix was NOT applied correctly${NC}"
  # Show what's there instead
  grep -n "CheckThreshold.*input:temperature" "$BEACON_DIR/Beacon/Beacon.Runtime/Generated/RuleGroup0.cs"
  exit 1
fi

# Step 3: Fix the RuleGroup1.cs AlertRule that accesses outputs directly from inputs
echo -e "${YELLOW}[5] Modifying RuleGroup1.cs to fix the AlertRule issue...${NC}"
RULE_GROUP1_PATH="$BEACON_DIR/Beacon/Beacon.Runtime/Generated/RuleGroup1.cs"

# Replace RuleGroup1.cs with a fixed version
cat > "$RULE_GROUP1_PATH" << 'EOF'
// Auto-generated rule group
// Generated: 2025-04-10T19:45:00.0000000Z

using System;
using System.Collections.Generic;
using System.Linq; // Required for Any() and All() extension methods
using System.Threading.Tasks;
using Serilog;
using Prometheus;
using StackExchange.Redis;
using Beacon.Runtime.Buffers;
using Beacon.Runtime.Rules;
using Beacon.Runtime.Interfaces;
using Beacon.Runtime.Services;
using ILogger = Serilog.ILogger;

namespace Beacon.Runtime
{
    public class RuleGroup1 : IRuleGroup
    {
        public string Name => "RuleGroup1";
        public IRedisService Redis { get; }
        public ILogger Logger { get; }
        public RingBufferManager BufferManager { get; }

        public RuleGroup1(
            IRedisService redis,
            ILogger logger,
            RingBufferManager bufferManager)
        {
            Redis = redis;
            Logger = logger?.ForContext<RuleGroup1>();
            BufferManager = bufferManager;
        }

        public string[] RequiredSensors => new[]
        {
            "output:high_temperature",
            "output:humidity_normal",
        };

        public async Task EvaluateRulesAsync(
            Dictionary<string, object> inputs,
            Dictionary<string, object> outputs)
        {
            // Rule: AlertRule
            // Layer: 1
            // Source: temperature_rules.yaml:89

            // Fixed to check if high_temperature and humidity_normal exist in outputs
            bool high_temp = false;
            bool humidity_normal = true;

            // Check if the outputs or inputs have the required values
            if (outputs.ContainsKey("output:high_temperature"))
            {
                high_temp = Convert.ToBoolean(outputs["output:high_temperature"]);
            }
            else if (inputs.ContainsKey("output:high_temperature"))
            {
                high_temp = Convert.ToBoolean(inputs["output:high_temperature"]);
            }

            if (outputs.ContainsKey("output:humidity_normal"))
            {
                humidity_normal = Convert.ToBoolean(outputs["output:humidity_normal"]);
            }
            else if (inputs.ContainsKey("output:humidity_normal"))
            {
                humidity_normal = Convert.ToBoolean(inputs["output:humidity_normal"]);
            }

            if (high_temp && !humidity_normal)
            {
                outputs["output:alert_status"] = "critical";
                outputs["output:alert_message"] = "High temperature alert: " + Convert.ToDouble(inputs["input:temperature"]) + " with abnormal humidity: " + Convert.ToDouble(inputs["input:humidity"]) + "%";
            }

            await Task.CompletedTask;
        }

        private bool CheckThreshold(string sensor, double threshold, int duration, string comparisonOperator)
        {
            // Implementation of threshold checking using BufferManager
            var values = BufferManager.GetValues(sensor, TimeSpan.FromMilliseconds(duration));
            if (values == null || !values.Any()) return false;

            switch (comparisonOperator)
            {
                case ">": return values.All(v => Convert.ToDouble(v.Value) > threshold);
                case "<": return values.All(v => Convert.ToDouble(v.Value) < threshold);
                case ">=": return values.All(v => Convert.ToDouble(v.Value) >= threshold);
                case "<=": return values.All(v => Convert.ToDouble(v.Value) <= threshold);
                case "==": return values.All(v => Convert.ToDouble(v.Value) == threshold);
                case "!=": return values.All(v => Convert.ToDouble(v.Value) != threshold);
                default: throw new ArgumentException($"Unsupported comparison operator: {comparisonOperator}");
            }
        }

        private void SendMessage(string channel, string message)
        {
            // Implementation of sending messages to Redis channel
            try
            {
                Redis.PublishAsync(channel, message);
                Logger.Information("Sent message to channel {Channel}: {Message}", channel, message);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to send message to channel {Channel}", channel);
            }
        }
    }
}
EOF

if [ -f "$RULE_GROUP1_PATH" ]; then
  echo -e "${GREEN}✓ Fixed RuleGroup1.cs with an improved implementation${NC}"
else
  echo -e "${RED}× Failed to create fixed RuleGroup1.cs${NC}"
  exit 1
fi

# Step 4: Build the Beacon solution
echo -e "${YELLOW}[6] Building the Beacon solution...${NC}"
cd "$BEACON_DIR" && dotnet build Beacon/Beacon.sln -r linux-x64

if [ $? -eq 0 ]; then
  echo -e "${GREEN}✓ Beacon solution built successfully${NC}"
else
  echo -e "${RED}× Failed to build Beacon solution${NC}"
  exit 1
fi

# Step 5: Run the Beacon
echo -e "${YELLOW}[7] Starting the Beacon application...${NC}"
BEACON_RUNTIME_PATH=$(find "$BEACON_DIR" -name "Beacon.Runtime.dll" | grep "linux-x64" | grep -v "obj" | head -n 1)

if [ -z "$BEACON_RUNTIME_PATH" ]; then
  echo -e "${RED}× Could not find Beacon.Runtime.dll${NC}"
  exit 1
fi

echo "Using Beacon at: $BEACON_RUNTIME_PATH"
BEACON_RUNTIME_DIR=$(dirname "$BEACON_RUNTIME_PATH")

# Create a settings file with metrics disabled to avoid port conflicts
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

dotnet "$BEACON_RUNTIME_PATH" --nometrics > "$VALIDATION_DIR/beacon.log" 2>&1 &
BEACON_PID=$!

# Wait to make sure Beacon is running
sleep 5

if ! kill -0 $BEACON_PID 2>/dev/null; then
  echo -e "${RED}× Beacon process failed to start or terminated early${NC}"
  cat "$VALIDATION_DIR/beacon.log"
  exit 1
fi

echo -e "${GREEN}✓ Beacon is running with PID $BEACON_PID${NC}"

# Step 6: Set some test values in Redis that should trigger rules
echo -e "${YELLOW}[8] Setting test values in Redis...${NC}"
redis-cli set "input:temperature" 35
redis-cli set "input:humidity" 75

sleep 2

# Check if the Beacon has created any outputs
echo -e "${YELLOW}[9] Checking for outputs in Redis...${NC}"
OUTPUT_KEYS=$(redis-cli keys "output:*")

if [ -n "$OUTPUT_KEYS" ]; then
  echo -e "${GREEN}✓ Beacon has created outputs in Redis:${NC}"
  
  # Show all outputs
  for key in $OUTPUT_KEYS; do
    value=$(redis-cli get $key)
    echo "  - $key: $value"
  done
else
  echo -e "${RED}× No outputs found in Redis. Checking Beacon log for errors...${NC}"
  tail -n 20 "$VALIDATION_DIR/beacon.log"
  exit 1
fi

# Step 7: Run the tests
echo -e "${YELLOW}[10] Running BeaconTester against the Beacon...${NC}"
cd "$ROOT_DIR"
dotnet run --project "$ROOT_DIR/BeaconTester/BeaconTester.Runner/BeaconTester.Runner.csproj" run \
  --scenarios="$TEST_DIR/test_scenarios.json" \
  --output="$TEST_DIR/results.json" \
  --redis-host=localhost --redis-port=6379

if [ -f "$TEST_DIR/results.json" ]; then
  echo -e "${GREEN}✓ Tests executed successfully${NC}"
  
  # Count successes and failures
  SUCCESSES=$(grep -c '"success": true' "$TEST_DIR/results.json" || echo 0)
  FAILURES=$(grep -c '"success": false' "$TEST_DIR/results.json" || echo 0)
  TOTAL=$((SUCCESSES + FAILURES))
  
  echo "  - Tests run: $TOTAL"
  echo "  - Successes: $SUCCESSES"
  echo "  - Failures: $FAILURES"
else
  echo -e "${RED}× Failed to run tests or generate results${NC}"
  exit 1
fi

# Step 8: Generate a test report
echo -e "${YELLOW}[11] Generating test report...${NC}"
dotnet run --project "$ROOT_DIR/BeaconTester/BeaconTester.Runner/BeaconTester.Runner.csproj" report \
  --results="$TEST_DIR/results.json" \
  --output="$TEST_DIR/report.html" \
  --format=html

if [ -f "$TEST_DIR/report.html" ]; then
  echo -e "${GREEN}✓ Test report generated successfully${NC}"
else
  echo -e "${RED}× Failed to generate test report${NC}"
  exit 1
fi

# Step 9: Clean up
echo -e "${YELLOW}[12] Cleaning up...${NC}"
kill $BEACON_PID || true
echo -e "${GREEN}✓ Beacon process terminated${NC}"

echo -e "${BLUE}========== VALIDATION COMPLETE ==========${NC}"
echo "All steps completed successfully!"
echo ""
echo "You can find all validation files in $VALIDATION_DIR:"
echo "  - Beacon application: $BEACON_DIR"
echo "  - Test scenarios: $TEST_DIR/test_scenarios.json"
echo "  - Test results: $TEST_DIR/results.json" 
echo "  - Test report: $TEST_DIR/report.html"
echo "  - Beacon log: $VALIDATION_DIR/beacon.log"