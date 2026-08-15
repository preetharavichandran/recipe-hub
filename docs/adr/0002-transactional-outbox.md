# ADR-002: Transactional outbox

- **Status:** Accepted
- **Context:** Direct broker publish can lose events on crash after commit.
- **Decision:** Persist CloudEvent JSON rows in `integration_outbox` in the same Postgres transaction as recipe writes; `OutboxDispatcherHostedService` publishes via `IEventPublisher`. After max attempts, rows become `Failed` (DB-side DLQ).
- **Consequences:** At-least-once delivery; consumers must be idempotent; hard-purge of soft-deleted recipes emits no new event.
