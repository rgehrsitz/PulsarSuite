Here is **`interface_outputs.schema.json`** — a compact JSON-Schema (Draft 2020-12) for **UI/telemetry metadata** describing each virtual output or buffer produced by Pulsar rules.
The compiler doesn’t *need* this file to run, but Beacon and downstream dashboards can use it to render friendly labels, choose widgets, and group related values.

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "Pulsar UI / Telemetry Output Catalog",
  "type": "object",
  "additionalProperties": false,

  "properties": {
    "version": {
      "type": "integer",
      "const": 1
    },

    "outputs": {
      "type": "array",
      "minItems": 1,
      "uniqueItems": true,
      "items": { "$ref": "#/$defs/output" }
    }
  },

  "required": [ "version", "outputs" ],

  "$defs": {
    /* ─────────────────────────
       Shared primitives
    ───────────────────────── */
    "identifier": {
      "type": "string",
      "pattern": "^[A-Za-z_][A-Za-z0-9_]*$"
    },

    "widget_enum": {
      "type": "string",
      "enum": [ "gauge", "boolean", "timeseries", "table", "custom" ]
    },

    /* ─────────────────────────
       Output object
    ───────────────────────── */
    "output": {
      "type": "object",
      "additionalProperties": false,

      "properties": {
        "id":          { "$ref": "#/$defs/identifier" },
        "display_name":{ "type": "string" },
        "description": { "type": "string" },

        /* UI hints */
        "widget":      { "$ref": "#/$defs/widget_enum" },
        "unit":        { "type": "string" },
        "decimals":    { "type": "integer", "minimum": 0, "maximum": 6 },

        /* Grouping / layout */
        "group":       { "type": "string" },
        "order":       { "type": "integer", "minimum": 0 },

        /* Visibility flags */
        "export": {
          "type": "boolean",
          "default": true,
          "description": "Expose in Beacon dashboards & Prometheus"
        },
        "default_visibility": {
          "type": "boolean",
          "default": true,
          "description": "Shown by default in UI if true"
        }
      },

      "required": [ "id" ]
    }
  }
}
```

### Authoring quick-start

```yaml
version: 1

outputs:
  - id: fan_override
    display_name: "Fan Override"
    description: "Auto-forced fan state"
    widget: boolean
    group: HVAC
    order: 10

  - id: heat_index
    display_name: "Heat Index"
    unit: "°F"
    widget: gauge
    decimals: 1
    group: Comfort
    order: 5
```

* **`widget`** guides Beacon’s default component (boolean pill, gauge, line chart, etc.).
* **`group` + `order`** let the UI build tidy dashboards.
* **`export`** can hide purely internal diagnostics from operator views while still allowing them in logs/metrics if desired.

Drop this file alongside `sensor_catalog.yaml`; Beacon can merge both catalogs to build a complete data dictionary for users.
