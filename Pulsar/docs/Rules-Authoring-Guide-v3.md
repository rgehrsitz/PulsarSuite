Pulsar Rules Authoring Guide (v3 + temporal)

---

## 1 Quick start
1. Create `rules.yaml` with `version: 3`.
2. Add rules (`name`, `conditions`, `actions`).
3. Validate:  
   ```bash
   pulsar-compiler rules.yaml --catalog sensor_catalog.yaml
4. Load into Beacon and inspect graph / warnings.

---

## 2 Rule anatomy

```yaml
- name: HeatIndexRule
  inputs:
    - id: Temperature
    - id: Humidity
  conditions:
    all:
      - type: comparison
        sensor: Temperature
        operator: ">"
        value: 20
  actions:
    - set: { key: heat_index,
             value_expression: 0.5 * (Temperature + 61 + ...) }
```

Minimum: `name`, `conditions`, `actions`.

---

## 3 Inputs & fallback  *(unchanged)*

| Field               | Default                 | Notes                          |
| ------------------- | ----------------------- | ------------------------------ |
| `required`          | `true`                  | Set `false` to allow fallback. |
| `fallback.strategy` | `propagate_unavailable` | See below.                     |

`strategy`: `propagate_unavailable` · `use_default` · `use_last_known` · `skip_rule`

---

## 4 Condition leaves

| `type`                    | Purpose                               | Required fields                               | Example               |
| ------------------------- | ------------------------------------- | --------------------------------------------- | --------------------- |
| `comparison`              | Instant test                          | `sensor`, `operator`, `value`                 | Temp >\ 30            |
| `expression`              | Free-form numeric / boolean           | `expression`                                  | `now() - TLast > 300` |
| **`threshold_over_time`** | Value beyond threshold for a duration | `sensor`, `operator`, `threshold`, `duration` | Temp >\ 75 for `10s`  |

*Operators:* `>`, `>=`, `<`, `<=`, `==`, `!=` (threshold leaf accepts only ordered ops).

---

## 5 Three-valued logic recap

* Comparison/temporal leaves return **True / False / Indeterminate**.
* Missing optional inputs → Indeterminate.
* `all` with any Indeterminate → Indeterminate.
* `any` with all Indeterminate → Indeterminate.
* Rule fires only if branch evaluates **True**.

---

## 6 Actions & `emit`

| Verb     | Required fields                        | `emit` (opt)                        |
| -------- | -------------------------------------- | ----------------------------------- |
| `set`    | `key`, `value_expression`              | `always` · `on_change` · `on_enter` |
| `log`    | `log` (string)                         | same                                |
| `buffer` | `key`, `value_expression`, `max_items` | same                                |

`emit` controls how often side-effects run while branch is active.

---

## 7 `else:` branch

Optional complementary branch.
Executed when primary `conditions` are **False or Indeterminate**.

---

## 8 Common patterns

| #       | Need                    | Snippet                                                                                                           |
| ------- | ----------------------- | ----------------------------------------------------------------------------------------------------------------- |
| 8-1     | Toggle flag with clear  | Use `else:` to set opposite value.                                                                                |
| 8-2     | Edge logging            | `emit: on_change` on the log action                                                                               |
| 8-3     | Stale-value fallback    | `fallback: { use_last_known, max_age: 5m }`                                                                       |
| **8-4** | **Sustained threshold** | `yaml\n- type: threshold_over_time\n  sensor: Temperature\n  operator: \">\"\n  threshold: 75\n  duration: 10s\n` |

---

## 9 Best practices  *(unchanged)*

1. Pair set/clear logic with `else:` or a second rule.
2. Use `emit` to avoid log/webhook spam.
3. Check compiler warnings (`--lint`).

---

## 10 Troubleshooting warnings  *(add)*

| Warning                        | Cause / Fix                                                                             |
| ------------------------------ | --------------------------------------------------------------------------------------- |
| `duration exceeds retain_last` | Sensor cache too short for temporal leaf. Increase `retain_last` or shorten `duration`. |

---

## 11 Full example with temporal logic

```yaml
version: 3

rules:
  - name: SustainedHighTemp
    inputs:
      - id: Temperature
    conditions:
      all:
        - type: threshold_over_time
          sensor: Temperature
          operator: ">"
          threshold: 75
          duration: 10s
    actions:
      - set: { key: sustained_high, value_expression: true }
      - log: { log: "🔥 Temp >75°C for 10 s", emit: on_enter }
    else:
      actions:
        - set: { key: sustained_high, value_expression: false }

  - name: HighTempSpike
    conditions:
      all:
        - type: comparison
          sensor: Temperature
          operator: ">"
          value: 90
    actions:
      - set: { key: spike, value_expression: true, emit: on_change }

  - name: HeatStressAlert
    conditions:
      all:
        - type: comparison
          sensor: sustained_high
          operator: "=="
          value: true
        - type: comparison
          sensor: spike
          operator: "=="
          value: true
    actions:
      - set: { key: stress_alert, value_expression: true }
      - log: { log: "⚠️ Heat stress alert!", emit: on_enter }
    else:
      actions:
        - set: { key: stress_alert, value_expression: false }
```

---

For a one-page refresher see **Cheat Sheet**. For schema details consult `pulsar_rules_v3.schema.json`.
