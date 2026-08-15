# ADR-007: Soft-delete and retention purge

- **Status:** Accepted
- **Context:** Consumers need a delete signal; GDPR-oriented retention without Postgres row TTL.
- **Decision:** Soft-delete sets `deleted_at` and emits `lifeatlas.recipe.deleted`. Background job hard-deletes after configurable days (default 90). Hard purge emits **no** event.
- **Consequences:** Lists/get hide soft-deleted rows; purge interval is app-owned; starters cannot be deleted.
