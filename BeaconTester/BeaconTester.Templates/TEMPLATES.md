# BeaconTester Template System Guide

## Purpose
Templates enhance BeaconTester's auto-generated tests by:
- Providing test configuration hints
- Specifying performance characteristics
- Defining edge case scenarios

## Basic Template Format
```json
{
  "templateType": "standard|performance|stability",
  "description": "Test purpose description",
  "parameters": {
    "duration": "short|normal|extended|continuous",
    "intensity": "low|normal|high|max"
  }
}
```

## Example Templates
See the included template files:
- `basic_validation.json`
- `stress_test.json`
- `long_running.json`

## Integration
BeaconTester automatically:
1. Discovers available templates
2. Matches templates to rule patterns
3. Merges template parameters with generated tests
