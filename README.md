# RecipeHub

Canonical **ingredient catalog** and **recipe** source of truth for Life Atlas (.NET 10). Thin writer API — no households, no meal planning.

See [CONTEXT.md](CONTEXT.md) for domain language and [PLAN.md](PLAN.md) for the full plan.

## Quick start

```bash
docker compose up --build
```

API: `http://localhost:8080`  
OpenAPI (Development): `http://localhost:8080/openapi/v1.json`  
Health: `GET /health`

Default publish mode is **console** (CloudEvents printed by the outbox dispatcher).

### Local without Docker (API only)

```bash
docker compose up db -d
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$DOTNET_ROOT:$PATH"
dotnet run --project src/RecipeHub.Api
```

Postgres is published on **host port 5433** (avoids clashing with a local 5432).

### Kafka (optional)

```bash
PUBLISH_MODE=kafka docker compose --profile kafka up --build
```

Redpanda listens on `localhost:19092` (host) / `redpanda:9092` (from the API container). Topic: `lifeatlas.recipes`.

### SNS

Set `PUBLISH_MODE=sns` or `both`, plus `SNS_TOPIC_ARN` (and optional `SNS_SERVICE_URL` for LocalStack). See [docs/integration.md](docs/integration.md).

### Dev JWT (writes)

Default `Authentication:Mode` is `Development` (HMAC JWT). Mint a token:

```bash
dotnet run --project tools/DevToken
# or:
./scripts/dev-token.sh user-a
```

## Demo script

```bash
# 1) Catalog + starters
curl -s http://localhost:8080/ingredients | head
curl -s 'http://localhost:8080/recipes?mealSlot=breakfast' | head

TOKEN=$(./scripts/dev-token.sh user-a)

# 2) Create with Idempotency-Key (required)
curl -s -X POST http://localhost:8080/recipes \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: demo-create-1" \
  -d '{
    "title": "My oats",
    "author": "Preetha",
    "mealSlots": ["breakfast"],
    "ingredients": [
      { "ingredientId": "11111111-1111-1111-1111-111111110001", "quantity": 60, "unit": "g" }
    ]
  }'

# 3) Watch console (or Kafka/SNS) for lifeatlas.recipe.created
#    docker compose logs -f api | grep 'RecipeHub outbox'

# 4) Update → lifeatlas.recipe.updated
curl -s -X PUT "http://localhost:8080/recipes/<recipe-id>" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: demo-update-1" \
  -d '{
    "title": "My oats (v2)",
    "author": "Preetha",
    "mealSlots": ["breakfast"],
    "ingredients": [
      { "ingredientId": "11111111-1111-1111-1111-111111110001", "quantity": 80, "unit": "g" }
    ]
  }'

# 5) PantryPilot: subscribe to topic lifeatlas.recipes (Kafka) or the SNS topic;
#    filter types lifeatlas.recipe.*; idempotent upsert by recipeId. No householdId on events.
```

Admin ingredient create uses `Authentication:AdminSubs` (default `dev-admin`).

### Google OIDC

Set:

```json
"Authentication": {
  "Mode": "Google",
  "GoogleAudience": "<your-google-oauth-client-id>",
  "AdminSubs": [ "<your-google-sub>" ]
}
```

Pass a Google **ID token** as `Authorization: Bearer …`. `sub` becomes `creatorId`.

## API surface

| Method | Path | Auth |
|--------|------|------|
| GET | `/health` | open |
| GET | `/ingredients`, `/ingredients/{id}` | open |
| POST | `/ingredients` | JWT + admin |
| GET | `/recipes`, `/recipes/{id}` | open |
| POST/PUT/PATCH | `/recipes…` | JWT + `Idempotency-Key` |
| DELETE | `/recipes/{id}` | JWT (soft-delete; creator-only) |

Soft-delete + 90-day purge. Outbox → `PUBLISH_MODE=console|kafka|sns|both`.

## Tests

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$DOTNET_ROOT:$PATH"
dotnet test
```

API integration tests skip when Postgres is unreachable (`RECIPEHUB_TEST_CONNECTION` optional).
