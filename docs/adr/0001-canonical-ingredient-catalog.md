# ADR-001: Canonical ingredient catalog

- **Status:** Accepted
- **Context:** PantryPilot needs deterministic ingredient matching across recipes and pantry lines.
- **Decision:** Recipes reference catalog UUIDs only; free text is display/alias, not identity. v1 grows via seed + admin POST; LLM resolve is v2.
- **Consequences:** Clients must resolve ids before write; catalog governance matters.
