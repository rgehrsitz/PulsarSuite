Below are **ready-to-drop example rule files** illustrating every major feature in Pulsar v3.
Save each snippet under the indicated path (e.g., `examples/01_basic_threshold.yaml`).

---

### **examples/01\_basic\_threshold.yaml** — Minimal comparison rule

```yaml
version: 3

rules:
  - name: HighTempAlert
    conditions:
      all:
        - type: comparison
          sensor: Temperature
          operator: ">"
          value: 30
    actions:
      - set: { key: high_temp, value_expression: true }
    else:
      actions:
        - set: { key: high_temp, value_expression: false }
```

---

### **examples/02\_else\_toggle.yaml** — Flag set/clear with `else:`

```yaml
version: 3

rules:
  - name: DoorOpenFlag
    conditions:
      all:
        - type: comparison
          sensor: DoorSwitch
          operator: "=="
          value: true
    actions:
      - set: { key: door_open, value_expression: true }
      - log: { log: "🚪 Door opened", emit: on_change }
    else:
      actions:
        - set: { key: door_open, value_expression: false }
        - log: { log: "🔒 Door closed", emit: on_change }
```

---

### **examples/03\_fallback\_temporal.yaml** — Optional input + `use_last_known`

```yaml
version: 3

rules:
  - name: FanOverrideController
    inputs:
      - id: Temperature
        required: false
        fallback: { strategy: use_last_known, max_age: 5m }
      - id: LastFanStartTime
    conditions:
      all:
        - type: comparison
          sensor: Temperature
          operator: ">"
          value: 30
        - type: expression
          expression: "now() - LastFanStartTime >= 600"
    actions:
      - set: { key: fan_override, value_expression: true }
      - log: { log: "🔥 Fan override ENABLED", emit: on_change }
    else:
      actions:
        - set: { key: fan_override, value_expression: false }
        - log: { log: "✅ Fan override CLEARED", emit: on_change }
```

---

### **examples/04\_buffer\_log.yaml** — Ring-buffer action with throttled log

```yaml
version: 3

rules:
  - name: TempHistoryBuffer
    conditions:
      all:
        - type: comparison
          sensor: Temperature
          operator: ">"
          value: -273    # always true (cheap unconditional rule)
    actions:
      - buffer:
          key: temp_history
          value_expression: Temperature
          max_items: 100
          emit: on_change
      - log:
          log: "🌡️ Temp recorded: ${Temperature}"
          emit: on_enter        # only first time buffer receives new value
```

---

### **examples/05\_temporal\_threshold.yaml** — `threshold_over_time`

```yaml
version: 3

rules:
  - name: SustainedHighTemp
    conditions:
      all:
        - type: threshold_over_time
          sensor: Temperature
          operator: ">"
          threshold: 75
          duration: 10s
    actions:
      - set: { key: sustained_high, value_expression: true }
      - log: { log: "🔥 Temp > 75 °C for 10 s", emit: on_enter }
    else:
      actions:
        - set: { key: sustained_high, value_expression: false }
```

*All examples validate against the latest `pulsar_rules_v3.schema.json` and are immediately runnable with the compiler.*
