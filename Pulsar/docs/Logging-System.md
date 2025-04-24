# Pulsar Logging System

This document describes the logging system used in Pulsar and Beacon applications.

## Overview

Pulsar uses [Serilog](https://serilog.net/) for logging. Serilog provides structured logging with a rich set of sinks (outputs) and configuration options. The logging system is designed to:

- Provide consistent, structured logging across all components
- Support different log levels for different parts of the system
- Write logs to appropriate destinations (console, files, etc.)
- Include contextual information in logs
- Support test-specific logging requirements

## Key Components

### LoggingConfig Class

The central configuration for Pulsar's logging system is in `Pulsar.Compiler/LoggingConfig.cs`. This class provides methods to:

- Get a configured logger for general application use
- Get component-specific loggers
- Configure verbose logging for debugging
- Ensure log directories exist
- Close and flush loggers when the application shuts down

### LoggingExtensions Class

The `LoggingExtensions.cs` provides extension methods to:

- Convert between Serilog and Microsoft.Extensions.Logging loggers
- Safely log messages with exception handling
- Log execution time of operations

### Test Logging

Test-specific logging functionality is provided by:

- `Pulsar.Tests/TestUtilities/LoggingConfig.cs` - Configuration for test logging
- `Pulsar.Tests/TestUtilities/LoggingHelper.cs` - Helper methods for test logging
- `Pulsar.Tests/TestUtilities/LoggerAdapter.cs` - Adapter to convert between logger types

### Beacon Logging

The Beacon application uses:

- `LoggingService.cs` - Centralized logging service for Beacon runtime
- Integration with the Redis monitoring systems

## Log Directory Structure

Logs are organized in the following directory structure:

- `logs/` - Main log directory
  - `pulsar-.log` - Main Pulsar logs (daily rotation)
  - `errors/` - Error logs
  - `structured/` - JSON-formatted structured logs
  - `components/` - Component-specific logs
  - `debug/` - Verbose debug logs
  - `redis/` - Redis-specific logs
    - `metrics/` - Redis metrics logs
    - `errors/` - Redis error logs
  - `tests/` - Test logs

## Log Levels

The system uses the following log levels (in increasing severity):

1. `Verbose` - Detailed debugging information (only used with `--debug` flag)
2. `Debug` - Information useful for debugging
3. `Information` - Normal application behavior information
4. `Warning` - Abnormal or unexpected events that don't affect core functionality
5. `Error` - Errors that affect functionality but allow the application to continue
6. `Fatal` - Critical errors that prevent the application from functioning

## Usage Examples

### Basic Logging

```csharp
// Get a logger
private readonly ILogger _logger = LoggingConfig.GetLogger();

// Log at different levels
_logger.Verbose("Very detailed debug information");
_logger.Debug("Debug information");
_logger.Information("Normal application information");
_logger.Warning("Something unexpected happened");
_logger.Error("An error occurred");
_logger.Fatal("A critical error occurred");
```

### Logging with Context

```csharp
// Add context to logs
_logger.ForContext("Operation", "Compilation")
       .Information("Compiling rules from {SourceFile}", sourceFile);

// Structured logging with objects
_logger.Information("Processing rule {@Rule}", rule);
```

### Logging in Generated Code

```csharp
// Initialize logging in generated code
var loggerFactory = LoggingService.Initialize("MyBeaconApp");

// Get loggers for specific components
var componentLogger = LoggingService.GetLogger<MyComponent>();
var redisLogger = LoggingService.GetRedisLogger();
```

### Test Logging

```csharp
// In test classes
public class MyTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger _logger;

    public MyTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = LoggingConfig.GetSerilogLoggerForTests(output);
    }

    [Fact]
    public void MyTest()
    {
        _logger.Information("Running test");
        // Test code...
    }
}
```

## Command Line Arguments

Both Pulsar and Beacon applications support the following command-line arguments for logging:

- `--verbose` or `-v`: Enable verbose logging (Debug level)
- `--debug` or `-d`: Enable extremely verbose logging (Verbose level)
- `--structured-logs` or `-s`: Enable structured JSON logging

## Environment Variables

- `PULSAR_STRUCTURED_LOGGING=true`: Enable structured JSON logging for Pulsar

## Additional Resources

- [Serilog Documentation](https://github.com/serilog/serilog/wiki)
- [Serilog Sinks](https://github.com/serilog/serilog/wiki/Provided-Sinks)