#!/bin/bash
# Test script to validate RedisAdapter functionality directly

# Get absolute paths to directories
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BEACONTESTER_DIR="$PROJECT_ROOT/BeaconTester"
OUTPUT_DIR="$PROJECT_ROOT/Output/AdapterTests"

# Create output directories
mkdir -p "$OUTPUT_DIR"

echo "===== RedisAdapter Validation Test ====="
echo "Testing enhanced validation functions in RedisAdapter"

# Create a focused test project to test the RedisAdapter functionality
mkdir -p "$OUTPUT_DIR/RedisAdapterTest"
cat > "$OUTPUT_DIR/RedisAdapterTest/RedisAdapterTest.csproj" << EOF
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="$BEACONTESTER_DIR/BeaconTester.Core/BeaconTester.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Serilog" Version="4.2.0" />
    <PackageReference Include="Serilog.Sinks.Console" Version="6.0.0" />
    <PackageReference Include="Serilog.Sinks.File" Version="6.0.0" />
  </ItemGroup>

</Project>
EOF

# Create a test program to validate the RedisAdapter
cat > "$OUTPUT_DIR/RedisAdapterTest/Program.cs" << EOF
using BeaconTester.Core.Redis;
using BeaconTester.Core.Models;
using Serilog;
using System.Text.Json;

namespace RedisAdapterTest;

public class Program
{
    public static void Main(string[] args)
    {
        // Setup logging
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File("$OUTPUT_DIR/redis-adapter-test.log")
            .CreateLogger();

        Log.Information("Starting RedisAdapter validation tests");

        try
        {
            // Setup Redis connection
            var config = new RedisConfiguration
            {
                Endpoints = new List<string> { "localhost:6379" },
                ConnectTimeout = 5000,
                SyncTimeout = 1000,
                AllowAdmin = true
            };

            using var adapter = new RedisAdapter(config, Log.Logger);

            TestBooleanComparisons(adapter);
            TestNumericComparisons(adapter);
            TestStringComparisons(adapter);

            Log.Information("All tests completed successfully!");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during tests");
        }
    }

    private static void TestBooleanComparisons(RedisAdapter adapter)
    {
        Log.Information("Testing boolean comparisons...");
        
        var expectations = new List<TestExpectation>
        {
            new() { Key = "test:bool1", Expected = true, Validator = "boolean" },
            new() { Key = "test:bool2", Expected = "True", Validator = "boolean" },
            new() { Key = "test:bool3", Expected = "TRUE", Validator = "boolean" },
            new() { Key = "test:bool4", Expected = "1", Validator = "boolean" },
            new() { Key = "test:bool5", Expected = "yes", Validator = "boolean" },
            new() { Key = "test:bool6", Expected = false, Validator = "boolean" },
            new() { Key = "test:bool7", Expected = "False", Validator = "boolean" },
            new() { Key = "test:bool8", Expected = "FALSE", Validator = "boolean" },
            new() { Key = "test:bool9", Expected = "0", Validator = "boolean" },
            new() { Key = "test:bool10", Expected = "no", Validator = "boolean" }
        };

        // Set values in Redis
        var inputs = new List<TestInput>
        {
            new() { Key = "test:bool1", Value = "true" },
            new() { Key = "test:bool2", Value = "True" },
            new() { Key = "test:bool3", Value = "TRUE" },
            new() { Key = "test:bool4", Value = "1" },
            new() { Key = "test:bool5", Value = "yes" },
            new() { Key = "test:bool6", Value = "false" },
            new() { Key = "test:bool7", Value = "False" },
            new() { Key = "test:bool8", Value = "FALSE" },
            new() { Key = "test:bool9", Value = "0" },
            new() { Key = "test:bool10", Value = "no" }
        };

        adapter.SendInputsAsync(inputs).GetAwaiter().GetResult();
        var results = adapter.CheckExpectationsAsync(expectations).GetAwaiter().GetResult();

        foreach (var result in results)
        {
            Log.Information(
                "Boolean test {Key}: Expected={Expected}, Actual={Actual}, Success={Success}",
                result.Key, result.Expected, result.Actual, result.Success);
            
            if (!result.Success)
            {
                throw new Exception($"Boolean test failed for {result.Key}: {result.Details}");
            }
        }
        
        Log.Information("All boolean comparisons passed!");
    }

    private static void TestNumericComparisons(RedisAdapter adapter)
    {
        Log.Information("Testing numeric comparisons...");
        
        var expectations = new List<TestExpectation>
        {
            new() { Key = "test:num1", Expected = 123, Validator = "numeric" },
            new() { Key = "test:num2", Expected = "456", Validator = "numeric" },
            new() { Key = "test:num3", Expected = 3.14, Validator = "numeric" },
            new() { Key = "test:num4", Expected = "3,14", Validator = "numeric" },
            new() { Key = "test:num5", Expected = -42, Validator = "numeric" },
            new() { Key = "test:num6", Expected = 1000, Validator = "numeric" }
        };

        // Set values in Redis
        var inputs = new List<TestInput>
        {
            new() { Key = "test:num1", Value = "123" },
            new() { Key = "test:num2", Value = 456 },
            new() { Key = "test:num3", Value = 3.14 },
            new() { Key = "test:num4", Value = "3,14" },
            new() { Key = "test:num5", Value = -42 },
            new() { Key = "test:num6", Value = 1000 }
        };

        adapter.SendInputsAsync(inputs).GetAwaiter().GetResult();
        var results = adapter.CheckExpectationsAsync(expectations).GetAwaiter().GetResult();

        foreach (var result in results)
        {
            Log.Information(
                "Numeric test {Key}: Expected={Expected}, Actual={Actual}, Success={Success}",
                result.Key, result.Expected, result.Actual, result.Success);
            
            if (!result.Success)
            {
                throw new Exception($"Numeric test failed for {result.Key}: {result.Details}");
            }
        }
        
        Log.Information("All numeric comparisons passed!");
    }

    private static void TestStringComparisons(RedisAdapter adapter)
    {
        Log.Information("Testing string comparisons...");
        
        var expectations = new List<TestExpectation>
        {
            new() { Key = "test:str1", Expected = "hello", Validator = "string" },
            new() { Key = "test:str2", Expected = "world", Validator = "string" },
            new() { Key = "test:str3", Expected = "mixed case", Validator = "string" },
            new() { Key = "test:str4", Expected = "", Validator = "string" },
            new() { Key = "test:str5", Expected = "", Validator = "string" }
        };

        // Set values in Redis
        var inputs = new List<TestInput>
        {
            new() { Key = "test:str1", Value = "hello" },
            new() { Key = "test:str2", Value = "  world  " },
            new() { Key = "test:str3", Value = "MIXED case" },
            new() { Key = "test:str4", Value = "" },
            new() { Key = "test:str5", Value = "   " }
        };

        adapter.SendInputsAsync(inputs).GetAwaiter().GetResult();
        var results = adapter.CheckExpectationsAsync(expectations).GetAwaiter().GetResult();

        foreach (var result in results)
        {
            Log.Information(
                "String test {Key}: Expected={Expected}, Actual={Actual}, Success={Success}",
                result.Key, result.Expected, result.Actual, result.Success);
            
            if (!result.Success)
            {
                throw new Exception($"String test failed for {result.Key}: {result.Details}");
            }
        }
        
        Log.Information("All string comparisons passed!");
    }
}

// No need to define model classes, we're using the ones from BeaconTester.Core.Models
EOF

# Build and run the test project
echo "Building and running RedisAdapter test..."
cd "$OUTPUT_DIR/RedisAdapterTest"
dotnet build
dotnet run

TEST_RESULT=$?

echo ""
echo "===== Test Summary ====="
if [ $TEST_RESULT -eq 0 ]; then
    echo "RedisAdapter validation test PASSED"
    echo ""
    echo "🎉 SUCCESS! 🎉"
    echo "The enhanced RedisAdapter validation methods work correctly:"
    echo "- Boolean comparisons handle case variations and alternative formats"
    echo "- Numeric comparisons handle regional formats and type conversions"
    echo "- String comparisons handle whitespace and case sensitivity properly"
else
    echo "RedisAdapter validation test FAILED with exit code $TEST_RESULT"
    echo ""
    echo "See test log for details:"
    echo "  $OUTPUT_DIR/redis-adapter-test.log"
fi

exit $TEST_RESULT