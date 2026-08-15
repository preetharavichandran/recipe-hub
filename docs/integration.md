# Integration

## HTTP (v1)

Base URL local: `http://localhost:8080`

- **Reads:** no auth
- **Writes:** `Authorization: Bearer <JWT>` where `sub` is the creator id
- **Idempotency:** `Idempotency-Key` **required** on POST/PUT/PATCH recipe writes (scoped to `(creatorId, key)`, ~24h TTL). Missing key → `400` problem+json.
- **Errors:** RFC 9457 `application/problem+json`

Stable seed ingredient / starter recipe ids are defined in `RecipeHub.Infrastructure.Persistence.Seed.SeedIds` (e.g. oats `11111111-1111-1111-1111-111111110001`).

## Events

Transactional outbox → dispatcher → publisher selected by **`PUBLISH_MODE`**:

| Mode | Behavior |
|------|----------|
| `console` | Log CloudEvent JSON (default local) |
| `kafka` | Produce to Kafka/Redpanda topic |
| `sns` | Publish to SNS topic ARN |
| `both` | Kafka then SNS (either failure retries via outbox) |

| Type | When |
|------|------|
| `lifeatlas.recipe.created` | Recipe created (including **platform starters** on seed) |
| `lifeatlas.recipe.updated` | Recipe updated (full snapshot) |
| `lifeatlas.recipe.deleted` | Soft-delete |

### CloudEvent shape

- `specversion`: `1.0`
- `source`: `urn:lifeatlas:recipe-hub`
- `type`: one of the event types above
- `eventVersion`: `1.0` (extension attribute)
- `data`: payload from `RecipeHub.Contracts.Events`

**No `householdId`.** PantryPilot associates recipe ids to households locally. Optional `author` supports MemoryAtlas filtering.

### Kafka

| Setting | Default |
|---------|---------|
| Bootstrap | Compose: `redpanda:9092` / host: `localhost:19092` |
| Topic | `lifeatlas.recipes` |
| Key | `data.recipeId` (falls back to CloudEvent `id`) |
| Header | `ce_type` = event type |

```bash
PUBLISH_MODE=kafka docker compose --profile kafka up --build
```

### SNS

| Setting | Env / config |
|---------|----------------|
| Topic ARN | `SNS_TOPIC_ARN` / `Publishing:Sns:TopicArn` (**required** for sns/both) |
| Region | `AWS_REGION` / `Publishing:Sns:Region` (default `eu-west-1`) |
| LocalStack | `SNS_SERVICE_URL=http://localhost:4566` + test keys |

Message body = full CloudEvent JSON; attribute `eventType` = type string.

### Failed publishes (DB DLQ)

After `Publishing:MaxPublishAttempts` (default **5**), outbox rows stay in `integration_outbox` with `Status=Failed` and `LastError`. No separate broker DLQ in v1.

## PantryPilot consumer handoff

1. **Prefer events** over polling once subscribed.
2. Subscribe to Kafka topic `lifeatlas.recipes` and/or the SNS topic.
3. Handle `lifeatlas.recipe.created|updated|deleted` idempotently (upsert / soft-remove projection by `recipeId`).
4. Treat `eventVersion` / `specversion` as contract; ignore unknown fields.
5. **Do not expect `householdId`** — store household↔recipe links in PantryPilot.
6. Starters arrive as normal `created` events (author `RecipeHub`, `creatorId` null in payload).
7. Until the consumer is wired, HTTP list/get of recipes + seed ids remain valid for parallel work.

## Config cheat sheet

```bash
PUBLISH_MODE=console|kafka|sns|both
Publishing__DispatcherIntervalSeconds=2
Publishing__MaxPublishAttempts=5
Publishing__Kafka__BootstrapServers=localhost:19092
Publishing__Kafka__Topic=lifeatlas.recipes
SNS_TOPIC_ARN=arn:aws:sns:...
SNS_SERVICE_URL=                # optional LocalStack
```
