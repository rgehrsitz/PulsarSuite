**Cheat Sheet — Pulsar Rules v3 (with Temporal Thresholds)**
*(file: `Cheat_Sheet.md`, one page)*

---

### 1 Rule Mini-Skeleton

```yaml
- name: MyRule
  conditions:
    all:
      - type: comparison
        sensor: Temp
        operator: ">"
        value: 30
  actions:
    - set: { key: alert, value_expression: true, emit: on_change }
  else:
    actions:
      - set: { key: alert, value_expression: false }
```

---

### 2 Condition Leaves (pick one)

| `type`                    | Required fields                                            | Sample                  |
| ------------------------- | ---------------------------------------------------------- | ----------------------- |
| `comparison`              | `sensor`, `operator`, `value`                              | `Temp > 30`             |
| `expression`              | `expression` (free-form)                                   | `now() - LastRun > 300` |
| **`threshold_over_time`** | `sensor`, `operator`, `threshold`, `duration` (e.g. `10s`) | `Temp > 75 for 10 s`    |

*Operators:* `>`, `>=`, `<`, `<=`, `==`, `!=` (temporal leaf uses ordered ops only).

---

### 3 Fallback Cheats

```yaml
inputs:
  - id: Temp
    required: false
    fallback: { strategy: use_last_known, max_age: 5m }
```

Strategies: `propagate_unavailable` · `use_default` · `use_last_known` · `skip_rule`.

---

### 4 Actions & `emit`

| Verb     | Required fields                        | Optional `emit`                               |
| -------- | -------------------------------------- | --------------------------------------------- |
| `set`    | `key`, `value_expression`              | `always` (default) / `on_change` / `on_enter` |
| `log`    | `log` (string)                         | same                                          |
| `buffer` | `key`, `value_expression`, `max_items` | same                                          |

`emit` cuts spam:

* `on_change` → only when a `set.key` value changes
* `on_enter` → only first tick branch becomes active

---

### 5 Branch Logic

* Primary `actions` run when `conditions` == **true**.
* `else.actions` run when **false or indeterminate**.
* One branch per tick.

---

### 6 Temporal Pattern Quick-copypasta

```yaml
- type: threshold_over_time
  sensor: Temperature
  operator: ">"
  threshold: 75
  duration: 10s
```

---

### 7 CLI Reminders

```bash
pulsar-compiler rules.yaml --catalog sensors.yaml   # validate + lint
pulsar-compiler --lint rules.yaml                   # warnings only
```

---

### 8 Common Pitfalls

| Issue                    | Fix                                                                   |
| ------------------------ | --------------------------------------------------------------------- |
| Sticky flag              | Add `else:` or a second rule to clear.                                |
| Log spam                 | Add `emit: on_change` or `on_enter`.                                  |
| Temporal leaf never true | Ensure sensor `retain_last` ≥ `duration` *if using `use_last_known`*. |

---

*For full details see **Authoring\_Guide.md***
