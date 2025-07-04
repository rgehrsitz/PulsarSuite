# Pulsar v3 Comprehensive Examples

This directory contains comprehensive examples demonstrating all the new v3 features of PulsarSuite, including temporal conditions, three-valued logic, advanced emit controls, and comprehensive validation.

## Files Overview

### 1. **comprehensive_v3_example.yaml**
Complete rule set demonstrating all v3 features:
- **Temporal Conditions**: `threshold_over_time` with WindowTracker behavior
- **Three-Valued Logic**: Proper handling of `EvalResult.True`, `EvalResult.False`, `EvalResult.Indeterminate`
- **Advanced Emit Controls**: `on_change`, `on_enter`, `always` emission patterns
- **Fallback Strategies**: `use_last_known`, `use_default`, `propagate_unavailable`
- **Complex Dependencies**: Rules that depend on outputs from other rules
- **Buffer Actions**: Ring buffer data collection for trend analysis

### 2. **comprehensive_v3_sensor_catalog.json**
Complete sensor catalog demonstrating:
- **Physical Sensors**: Temperature, Pressure, FlowRate with validation ranges
- **Virtual Outputs**: Generated by rules with proper metadata
- **Buffer Sensors**: Data collection points for historical analysis
- **Retention Policies**: `retain_last` settings for temporal conditions
- **Export Controls**: UI visibility and dashboard integration

### 3. **comprehensive_v3_interface_outputs.json**
UI metadata catalog demonstrating:
- **Widget Types**: boolean, gauge, timeseries, custom widgets
- **Grouping**: Logical organization for dashboard layout
- **Display Names**: User-friendly labels for technical outputs
- **Units and Precision**: Proper formatting for numeric displays
- **Visibility Controls**: Default visibility and export settings

## Running the Examples

### Basic Validation
```bash
# Validate rules against sensor catalog
Pulsar.Compiler validate --rules=comprehensive_v3_example.yaml \
  --catalog=config/comprehensive_v3_sensor_catalog.json
```

### Generate Complete Beacon Solution
```bash
# Generate Beacon with all v3 features
Pulsar.Compiler beacon --rules=comprehensive_v3_example.yaml \
  --catalog=config/comprehensive_v3_sensor_catalog.json \
  --output=./output \
  --validation-level=strict \
  --lint \
  --generate-metadata \
  --verbose
```

### Advanced Validation with Linting
```bash
# Strict validation with comprehensive linting
Pulsar.Compiler validate --rules=comprehensive_v3_example.yaml \
  --catalog=config/comprehensive_v3_sensor_catalog.json \
  --validation-level=strict \
  --lint \
  --fail-on-warnings \
  --lint-level=info
```

## Features Demonstrated

### 🕒 **Temporal Conditions**
```yaml
- type: threshold_over_time
  sensor: Temperature
  operator: ">"
  threshold: 75
  duration: 10s
```
- **WindowTracker**: Precise duration tracking with state management
- **Boundary Testing**: Exact timing validation
- **Interruption Handling**: Window reset behavior

### 🔀 **Three-Valued Logic**
```yaml
inputs:
  - id: Pressure
    required: false
    fallback:
      strategy: use_last_known
      max_age: 30s
```
- **EvalResult.True**: Condition definitely satisfied
- **EvalResult.False**: Condition definitely not satisfied  
- **EvalResult.Indeterminate**: Insufficient data or sensor unavailable

### 📡 **Advanced Emit Controls**
```yaml
actions:
  - set:
      key: critical_alert
      value_expression: true
      emit: on_enter  # Only when entering alert state
  - log:
      log: "Alert triggered"
      emit: on_change  # Only when value changes
```
- **on_enter**: Execute only when rule becomes active
- **on_change**: Execute only when output value changes
- **always**: Execute every evaluation cycle (default)

### 🔄 **Fallback Strategies**
```yaml
fallback:
  strategy: use_last_known  # or use_default, propagate_unavailable, skip_rule
  max_age: 30s
  default_value: 0
```
- **use_last_known**: Use cached value within time limit
- **use_default**: Use specified default value
- **propagate_unavailable**: Return Indeterminate
- **skip_rule**: Skip entire rule evaluation

### 📊 **Buffer Actions**
```yaml
- buffer:
    key: temp_history
    value_expression: Temperature
    max_items: 100
    emit: always
```
- **Ring Buffer**: Fixed-size circular buffer for trend data
- **Automatic Management**: Oldest values removed when capacity reached
- **Integration**: Available to other rules as sensor inputs

## Testing Scenarios

### **Temporal Window Testing**
1. **Window Establishment**: Value above threshold for partial duration → False
2. **Window Completion**: Value above threshold for full duration → True  
3. **Window Interruption**: Value drops below threshold → Window resets
4. **Boundary Conditions**: Exact duration timing validation

### **Sensor Unavailability**
1. **Missing Data**: Sensor unavailable → Indeterminate based on fallback
2. **Stale Data**: Data older than `max_age` → Indeterminate
3. **Recovery**: Sensor restored → Normal evaluation resumes

### **Complex Dependencies**
1. **Multi-Rule Chain**: Rule A → Rule B → Rule C dependencies
2. **Circular Prevention**: Dependency cycle detection and prevention
3. **Conditional Logic**: Rules that depend on combinations of other outputs

## Generated Outputs

When running the complete example, the following files are generated:

### **Beacon Application**
- `output/Beacon/Beacon.Runtime/` - Complete AOT-ready application
- `output/Beacon/Beacon.Tests/` - Comprehensive unit tests
- `output/Beacon/Beacon.sln` - Solution file for development

### **Metadata Files**
- `interface_outputs.json` - UI component metadata
- `sensor_catalog.json` - Complete sensor definitions  
- `data_dictionary.json` - Unified data reference

### **BeaconTester Scenarios**
- Temporal window establishment and interruption tests
- Sensor unavailability and recovery tests
- Three-valued logic validation tests
- Emit control behavior verification

## Integration with External Systems

### **Dashboard Integration**
Use the generated metadata files to automatically create:
- **Grafana Dashboards**: From sensor catalog and interface outputs
- **Custom UIs**: Using widget type and grouping information
- **Alarm Systems**: Based on criticality tags and export flags

### **API Integration** 
- **Redis Keys**: All outputs available via Redis pub/sub
- **Prometheus Metrics**: Automatic metric generation for exported sensors
- **REST APIs**: Generated OpenAPI specs from sensor catalog

### **Monitoring Integration**
- **Health Checks**: System status and emergency shutdown monitoring
- **Alerting**: Critical alert integration with external systems
- **Trend Analysis**: Historical data via buffer sensors

## Best Practices Demonstrated

1. **Sensor Naming**: Consistent naming conventions across physical and virtual sensors
2. **Validation Ranges**: Appropriate min/max values for safety
3. **Retention Policies**: Sufficient `retain_last` for temporal conditions
4. **Export Controls**: Proper visibility settings for different audiences
5. **Error Handling**: Graceful degradation with fallback strategies
6. **Performance**: Efficient emit controls to minimize unnecessary processing

This comprehensive example serves as both a reference implementation and a template for building production-ready Pulsar v3 systems.