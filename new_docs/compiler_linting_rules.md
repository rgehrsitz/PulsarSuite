**Compiler Linting Rules — Pulsar v3**
*(file `Compiler_Linting_Rules.md`)*

---

## 0 Purpose

The linting pass runs **after JSON-schema validation** but **before code-generation**.
Its job is to catch logic mistakes and unsafe patterns that the schema alone cannot express.

*Severity levels*

| Level       | Build flag             | Effect                    |
| ----------- | ---------------------- | ------------------------- |
| **Error**   | `--strict` *(default)* | Fails compilation         |
| **Warning** | always                 | Reported; build continues |
| **Info**    | `--verbose`            | FYI only                  |

---

## 1 Rule-level checks

| ID                                 | Severity | Description                                                     | Trigger & Fix                   |
| ---------------------------------- | -------- | --------------------------------------------------------------- | ------------------------------- |
| **R-001  DuplicateRuleName**       | Error    | Duplicate `name` across rules.                                  | Make names unique.              |
| **R-002  UnusedInputDecl**         | Warning  | `inputs[].id` never referenced in conditions/expressions.       | Remove or use the input.        |
| **R-003  OptionalWithoutFallback** | Warning  | `required:false` but no `fallback` block. Input may block rule. | Add fallback or mark required.  |
| **R-004  ElseWithoutPrimary**      | Error    | `else:` present but primary `actions` empty.                    | Add `actions` or remove `else`. |

---

## 2 Condition checks

| ID                                 | Severity | Description                                                                                  | Trigger & Fix                                 |
| ---------------------------------- | -------- | -------------------------------------------------------------------------------------------- | --------------------------------------------- |
| **C-101  UnknownSensorReference**  | Error    | Leaf `sensor` not found in sensor catalog **or** as rule output.                             | Fix typo or add to catalog.                   |
| **C-102  TemporalDurationTooLong** | Warning  | `threshold_over_time.duration` > sensor `retain_last` *and* rule uses `use_last_known`.      | Increase `retain_last` or shorten `duration`. |
| **C-103  ConstantTrueFalse**       | Info     | Condition tree evaluates to literal `true` / `false` at compile time.                        | Remove dead logic.                            |
| **C-104  AlwaysIndeterminate**     | Warning  | Every branch includes optional input with `propagate_unavailable`; rule can never be `true`. | Provide fallback or mark required.            |

---

## 3 Action checks

| ID                            | Severity | Description                                                                                                   | Trigger & Fix                                     |
| ----------------------------- | -------- | ------------------------------------------------------------------------------------------------------------- | ------------------------------------------------- |
| **A-201  StickyStateTrue**    | Warning  | A `set.key` is only ever assigned **`true`**. No rule/branch writes `false`.                                  | Add clearing logic (else or second rule).         |
| **A-202  StickyStateFalse**   | Warning  | Key only ever set **`false`**.                                                                                | Check logic—maybe inverted?                       |
| **A-203  ConflictingWriters** | Error    | Two primary branches in different rules set same `key` **to different constant literals** in the *same tick*. | Merge logic or coordinate via else-branch.        |
| **A-204  EmitAlwaysLog**      | Warning  | `log` action with `emit:always` but message literal has no placeholders – likely spamming.                    | Use `on_change` / `on_enter` or add placeholders. |
| **A-205  BufferNoMaxItems**   | Error    | `buffer` action missing `max_items`.                                                                          | Add field (≥1).                                   |
| **A-206  ElseEmitMismatch**   | Info     | Primary branch uses `emit:on_change` but else branch does not (or vice-versa).                                | Ensure symmetric behaviour if desired.            |

---

## 4 System-level checks

| ID                                  | Severity | Description                                                                                                                  | Trigger & Fix                                      |
| ----------------------------------- | -------- | ---------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------- |
| **S-301  UnusedOutput**             | Info     | A `set.key` is never read by any rule or exported.                                                                           | Remove or export the value.                        |
| **S-302  DanglingConditionOutput**  | Error    | A comparison references virtual output that is never written anywhere.                                                       | Add writer rule or fix typo.                       |
| **S-303  CircularSetDependency**    | Error    | Rule A `set:key1` depends (via conditions) on `key2`, while Rule B sets `key2` and depends on `key1`, forming a write cycle. | Break the loop with explicit ordering or redesign. |
| **S-304  LargeRule (>6 000 chars)** | Warning  | Single rule YAML length exceeds 6 kB (maintainability).                                                                      | Split into smaller rules or import expressions.    |

---

## 5 Fixed-default severities

You can downgrade **Warnings** to **Infos** via `--lint-level warn` or promote **Infos** to **Warnings** with `--lint-level info→warn`.
Errors cannot be downgraded.

---

## 6 CLI Flags

```bash
pulsar-compiler rules.yaml \
  --catalog sensor_catalog.yaml \
  --lint                         # show warnings & infos
  --fail-on-warnings             # treat warnings as errors
  --lint-level info=warn         # elevate infos
```

---

## 7 Extending the linter

Add a new rule in `Compiler/Lint/` implementing `ILintRule`:

```csharp
public class NoEmitOnBuffer : ILintRule {
    public IEnumerable<LinterMessage> Check(RuleGraph g) {
        foreach (var act in g.Actions.OfType<BufferAction>())
            if (act.Emit == Emit.Always)
                yield return warn("A-207", act.Source, "Buffer without emit…");
    }
}
```

Register via `LintRegistry.Register<NoEmitOnBuffer>();`.

---

Keep this document in sync with compiler rule IDs so Beacon can link directly from a warning to its explanation.
