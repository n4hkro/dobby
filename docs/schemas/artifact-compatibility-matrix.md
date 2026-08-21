# Artifact Compatibility Matrix

This matrix freezes the transition policy before successor runtime behavior is
enabled. Validation never implies authorization or installation.

| Artifact | Legacy input | Successor input | Inventory/read | New import/install after activation gate | Migration decision |
|---|---|---|---|---|---|
| Plugin manifest | v1 | v2 | v1 may be inventoried as legacy | v2 only | Reverify and repackage as v2; never rewrite a signature in place |
| Plugin license | v2 | v3 | Valid installed v2 LKG may be read during the bounded migration window | v3 only | Preserve only an existing non-escalating LKG; no v2 status may become a new grant |
| Owner recovery | v1 hardware-threshold request/response | v2 device-key-bound request/response | v1 may be displayed/exported for support diagnosis only | v2 only | Reject v1 authorization; issue a new v2 request after device-key provisioning |
| Branch setup | none; `.dobbylicense` is not setup authority | setup authorization v1 | Schema validation/design review only | no Standalone runtime handler | Deferred to Branch Shared by design |

Unknown schema versions, unknown purposes, malformed values and cross-protocol
artifacts are rejected. There is no silent fallback between rows.
