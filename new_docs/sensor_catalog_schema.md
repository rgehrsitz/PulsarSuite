Here is **`sensor_catalog.schema.json`** — the authoritative JSON Schema (Draft 2020-12) for a Pulsar **sensor & virtual-output catalog**.
Use this file to validate / document every data point Pulsar can read or set.
The compiler will load this catalog to:

* validate that rule-referenced inputs exist
* verify type/unit compatibility
* confirm `use_last_known` fallback is legal (`retain_last > 0`)
* supply metadata to Beacon (units, display names, export flags)

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "Pulsar Sensor & Output Catalog (v1)",
  "type": "object",
  "additionalProperties": false,

  "properties": {
    "version": {
      "type": "integer",
      "const": 1,
      "description": "Schema version – must be 1."
    },

    "sensors": {
      "type": "array",
      "minItems": 1,
      "uniqueItems": true,
      "items": { "$ref": "#/$defs/sensor" },
      "description": "All physical sensors, virtual outputs, and internal buffers."
    }
  },

  "required": [ "version", "sensors" ],

  "$defs": {
    /* ──────────────────────────
       Common primitives
    ────────────────────────── */
    "identifier": {
      "type": "string",
      "pattern": "^[A-Za-z_][A-Za-z0-9_]*$"
    },

    "duration": {
      "type": "string",
      "pattern": "^(\\d+)(ms|s|m|h|d)$",
      "description": "Timespan such as 500ms, 30s, 5m, 2h, 1d"
    },

    "numeric_range": {
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "min": { "type": "number" },
        "max": { "type": "number" }
      },
      "required": [ "min", "max" ]
    },

    /* ──────────────────────────
       Sensor / Output definition
    ────────────────────────── */
    "sensor": {
      "type": "object",
      "additionalProperties": false,

      "properties": {
        /* Core identity */
        "id":         { "$ref": "#/$defs/identifier" },
        "type": {
          "type": "string",
          "enum": [ "float", "int", "bool", "string" ]
        },

        /* Optional metadata */
        "unit":        { "type": "string" },
        "description": { "type": "string" },
        "tags": {
          "type": "array",
          "items": { "type": "string" },
          "uniqueItems": true
        },

        /* Retention & quality flags */
        "retain_last": {
          "$ref": "#/$defs/duration",
          "description": "Cache the last value for this duration (0 disables)."
        },
        "quality": {
          "type": "string",
          "enum": [ "raw", "filtered", "derived" ],
          "default": "raw"
        },

        /* Validation constraints (numeric types only) */
        "range": { "$ref": "#/$defs/numeric_range" },

        /* Export / visibility */
        "export": {
          "type": "boolean",
          "default": false,
          "description": "Expose in Beacon UI & metrics if true."
        },

        /* Data source classification */
        "source": {
          "type": "string",
          "enum": [ "physical", "virtual", "buffer" ],
          "default": "physical"
        }
      },

      "required": [ "id", "type" ],

      /* Conditional: range only allowed on numeric types */
      "allOf": [
        {
          "if": { "properties": { "type": { "enum": [ "float", "int" ] } } },
          "then": { "else": true },
          "else": { "not": { "required": [ "range" ] } }
        }
      ]
    }
  }
}
```

### Authoring quick-start

```yaml
version: 1

sensors:
  - id: Temperature
    type: float
    unit: "°C"
    retain_last: 5m
    range: { min: -40, max: 100 }
    export: true

  - id: FanState
    type: bool
    description: "Physical fan relay feedback"
    export: true

  - id: fan_override          # virtual output set by rules
    type: bool
    source: virtual
    export: true
```

* **`retain_last`** enables `use_last_known` fallback for rules.
* **`export: true`** tells Beacon to surface the value on dashboards.
* **`tags`** and **`quality`** are free-form aids for filtering/graphing.

This schema is ready to drop into your tooling for catalog validation and auto-documentation.
