# Production next

1. Apple Sign-In (second OIDC provider)
2. Thin CRUD UI
3. Catalog governance UX beyond admin allowlist
4. LLM-assisted ingredient resolve/insert on recipe write (confirm vs auto)
5. Event versioning / schema evolution policy
6. Multi-region + **broker DLQ** (beyond DB `Failed` outbox rows) for Kafka/SNS
7. GDPR export / accelerated erasure beyond soft-delete retention
8. OpenTelemetry exporters to shared Life Atlas observability
9. Stronger `both`-mode publishing (transactional outbox per destination or inbox dedupe keys)
