# ADR-005: Full snapshot update events

- **Status:** Accepted
- **Context:** Consumers need current recipe state after updates.
- **Decision:** `lifeatlas.recipe.updated` carries a full live snapshot (same shape as created), not a JSON patch.
- **Consequences:** Larger payloads; simpler consumer upserts; no patch application bugs.
