# System design

RecipeHub is a thin ASP.NET Core API that owns the Life Atlas **ingredient catalog** and **recipe** write model.

## Context

- **Upstream callers:** any client with a Google (or Development) JWT for writes; anonymous reads.
- **Downstream:** PantryPilot (and later MemoryAtlas) via CloudEvents: outbox → console / Kafka / SNS (`PUBLISH_MODE`).
- **Not in scope:** households, pantry, meal plans, shopping lists.

## Containers

| Component | Role |
|-----------|------|
| RecipeHub.Api | Minimal APIs, auth, OpenAPI, Problem Details |
| RecipeHub.Application | Use cases, outbox dispatch, CloudEvent mapping |
| RecipeHub.Domain | Entities and invariants (e.g. immutable starters) |
| RecipeHub.Infrastructure | EF Core / PostgreSQL, seed, purge, Kafka/SNS/console publishers |
| RecipeHub.Contracts | CloudEvent payload DTOs |
| PostgreSQL | Source of truth + `integration_outbox` |
| Redpanda (optional) | Local Kafka-compatible broker |

## Key flows (v1)

1. **Read catalog / recipes** — open GETs; soft-deleted recipes hidden.
2. **Create recipe** — JWT + `Idempotency-Key`; `creatorId = sub`; catalog ingredient ids only; outbox `created` in same TX.
3. **Update / delete** — creator only; starters immutable; update writes full-snapshot `updated`; delete soft-deletes + `deleted` event.
4. **Seed** — large ingredient catalog + immutable starters; starters also enqueue `created` events.
5. **Dispatch** — background poll publishes pending outbox rows; failures retry then `Failed` (DB DLQ).
6. **Retention** — hard-delete soft-deleted rows after configurable days (default 90); no event on hard purge.
