# Trade-offs

Locked v1 decisions (see also [PLAN.md](../PLAN.md) and `docs/adr/`):

| Decision | Choice | Why |
|----------|--------|-----|
| Separate RecipeHub vs inside PantryPilot | Separate .NET service | Clear EDA producer; CV lane |
| Ingredient identity | Canonical UUID catalog | Deterministic matching |
| Household tenancy | None in RecipeHub | Household is PantryPilot’s |
| Visibility | Shared catalog | Knowledge to share |
| Mutation | Creator-only; variants = new recipe | Prevents wiki vandalism |
| Starters | Immutable; GET+POST to variant | Fewer endpoints |
| Auth | Google OIDC (Dev HMAC locally) | Real `sub` as creator |
| Delete | Soft + 90d purge | Consumer-friendly + retention |
| Update events | Full snapshot | Simpler consumers |
| Catalog writes | Large seed + admin POST | Protect canonical ids |
| Publish path | Outbox → console / Kafka / SNS | Crash-safe; portfolio brokers |
| `PUBLISH_MODE=both` | Kafka then SNS | Dual delivery; accept duplicate risk across brokers |
| Failed publishes | Mark `Failed` in outbox (DB DLQ) | Observable without Week-1 broker DLQ |
| Idempotency-Key | Required on recipe create/update | Safe client retries |
| Seed starters | Emit `recipe.created` | One consumer path for all recipes |
