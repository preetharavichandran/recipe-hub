# ADR-003: Kafka and SNS publish adapters

- **Status:** Accepted
- **Context:** Portfolio wants Kafka locally (Redpanda) and SNS on AWS; local demos should still work with console only.
- **Decision:** One `IEventPublisher` port; `ConfiguredEventPublisher` selects by `PUBLISH_MODE=console|kafka|sns|both`. `both` = Kafka then SNS. Outbox remains the durability boundary; adapters are interchangeable.
- **Consequences:** Extra adapter code and broker config; at-least-once across brokers when `both` (SNS may succeed after Kafka fails mid-flight on retry — consumers must be idempotent). Local default stays `console`.
