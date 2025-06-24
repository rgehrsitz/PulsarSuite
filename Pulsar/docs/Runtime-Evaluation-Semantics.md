**Runtime Evaluation Semantics — Pulsar v3**
*(file `Runtime_Evaluation_Semantics.md`)*

---

## 1 Cycle-level overview

| Stage                    | What happens (per 100 ms deterministic cycle)                                                 |
| ------------------------ | --------------------------------------------------------------------------------------------- |
| 1 📥 Gather              | Pull latest sensor values, apply `retain_last` windows.                                       |
| 2 🌳 Evaluate conditions | Every rule produces **EvalResult** (`True` / `False` / `Indeterminate`). No side-effects yet. |
| 3 ▶️ Resolve branches    | Select **primary** vs **else** branch for each rule.                                          |
| 4 ⚙️ Execute actions     | Respect `emit` modifiers, update virtual outputs/buffers, emit logs.                          |
| 5 📤 Commit & publish    | All `set` values become visible to next cycle; Beacon/Prometheus scrape.                      |

*Two-phase design prevents read-after-write races.*

---

## 2 EvalResult enum

```csharp
enum EvalResult { True, False, Indeterminate }
```

* Only **True** can activate a primary branch.
* **False / Indeterminate** → else-branch (if present) else *no actions*.

---

## 3 Leaf evaluation

| Leaf `type`               | Algorithm                                                                                                                       | Returns Indeterminate when                           |
| ------------------------- | ------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------- |
| **comparison**            | Read latest (or fallback) value, apply operator.                                                                                | Value unavailable or input optional+propagate.       |
| **expression**            | Parse & eval arithmetic/boolean with available vars.                                                                            | Any referenced var Indeterminate.                    |
| **threshold\_over\_time** | `WindowTracker` keeps `(startTs, inWindow)`; true after sensor continuously satisfies `operator` vs `threshold` for `duration`. | Sensor unavailable ➜ tracker paused ➜ Indeterminate. |

### 3.1 WindowTracker pseudocode

```csharp
if (Comp(sensorVal, op, threshold)) {
    if (!inWindow) { startTs = now; inWindow = true; }
    return (now - startTs >= duration) ? True : False;
} else { inWindow = false; return False; }
```

Tracker state resets the moment the comparison fails.

---

## 4 Three-valued logic propagation

| op    | T ∧ | F ∧ | I ∧ |   | T ∨ | F ∨ | I ∨ |
| ----- | --- | --- | --- | - | --- | --- | --- |
| **T** | T   | F   | I   |   | T   | T   | T   |
| **F** | F   | F   | F   |   | T   | F   | I   |
| **I** | I   | F   | I   |   | T   | I   | I   |

`not(I) = I`.

---

## 5 Branch selection

```csharp
if (Conditions(rule) == True)
    activeBranch = Primary;
else
    activeBranch = rule.Else ?? None;
```

*Else branch fires on both **False** *and* **Indeterminate**.*

---

## 6 Action execution order

* Within a branch: **list order**.
* Across rules: topological order of dependency graph (inputs first, consumers later) but same-phase commit prevents cascaded writes this cycle.

---

## 7 `emit` modifier semantics

| `emit` value         | Execute when…                                             | Additional state stored          |
| -------------------- | --------------------------------------------------------- | -------------------------------- |
| **always** (default) | every tick branch active                                  | none                             |
| **on\_enter**        | branch active **and** `BranchId` changed vs previous tick | remember last active branch hash |
| **on\_change**       | at least one `set.key` value differs from previous value  | per-key previous value cache     |

> Log/webhook throttling happens *before* emit-logic; identical consecutive messages under `on_change` are suppressed.

---

## 8 Virtual output write rules

1. All `set` operations stage values in **WriteSet**.
2. If two writes in same tick target same key **with different constants**, compiler earlier emits *A-203* error.
3. After all rules execute, WriteSet commits → `CurrentState`.
4. Reads occur from `CurrentState` of **previous** cycle. (Two-phase).

---

## 9 Buffers

*Fixed-size ring (`max_items`).*

```csharp
if (emitAllowed) buffer.Enqueue(val);
if (buffer.Count > max_items) buffer.Dequeue();
```

Buffers are virtual outputs (`source: buffer`) → visible to rules via implicit `.Latest()` accessor.

---

## 10 Time base

* `now()` returns monotonically increasing UNIX seconds from a single scheduler clock.
* Cycle jitter ≤1 ms; temporal windows compare `(now - startTs)` to literal seconds.

---

## 11 Fallback handling

* Fallback resolved **before** leaf evaluation.
* If strategy is `use_last_known` and cache age > `max_age` ➜ value = **Unavailable**.
* `skip_rule` short-circuits: EvalResult = Indeterminate, actions skipped (else evaluated).

---

## 12 Error handling

| Error                        | Runtime behaviour                                                                     |
| ---------------------------- | ------------------------------------------------------------------------------------- |
| Divide-by-zero in expression | Rule leaf → Indeterminate; compiler warns if detectable const.                        |
| Unknown variable             | Fatal – cannot reach runtime (schema validation).                                     |
| Overflow                     | Saturating semantics for ints; IEEE 754 for floats (NaN propagates to Indeterminate). |

---

## 13 Metrics emitted

| Metric                       | Type      | Labels           |
| ---------------------------- | --------- | ---------------- |
| `rule_eval_total`            | counter   | `rule`, `branch` |
| `rule_eval_duration_seconds` | histogram | `rule`           |
| `rule_indeterminate_total`   | counter   | `rule`           |
| `sensor_stale_seconds`       | gauge     | `sensor`         |
| `window_tracker_state` (0/1) | gauge     | `rule`, `sensor` |

---

## 14 Determinism & thread safety

* Single-threaded evaluation loop ⇒ deterministic ordering.
* Background I/O (sensors, exporters) marshalled via channels; never mutate rule state mid-cycle.
* `WindowTracker` instances are indexed by `(ruleId, leafIndex)` → deterministic key.

---

*End of document.*
