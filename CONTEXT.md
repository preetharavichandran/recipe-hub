# RecipeHub

Canonical ingredient catalog and recipe source of truth for Life Atlas. Thin writer and event publisher — not a meal planner, pantry, or household product.

## Language

**Ingredient**:
A catalogued food item with a stable **UUID**, display name, aliases, and a coarse default unit. The shared identity other services use for matching. In v1 the catalog grows via seed and admin writes only.
_Avoid_: Product, SKU, pantry item, free-text ingredient

**Recipe**:
A named set of ingredient lines (catalog ids + quantities + units), optional meal slots and steps, in the **shared catalog**. Identified by **UUID**. Globally readable; create allowed for authenticated callers; update/delete only by the creator (starters are immutable).
_Avoid_: Meal, dish plan, household recipe, personal recipe, private recipe

**Author**:
Optional display attribution on a recipe (a name people can search/filter on). Used for discovery and later MemoryAtlas/memory-lane filtering of recipe events — not the permission key for update/delete.
_Avoid_: Owner (as synonym for author), household

**Creator**:
The authenticated subject (`jwt.sub` from Google OIDC in v1) that created a recipe and alone may update or delete it. Someone else who wants a variant creates a **new** recipe (copy + edit), not an in-place edit.
_Avoid_: Author (when meaning permissions), owner household, editor

**RecipeIngredient**:
One line on a recipe: a catalog ingredient id plus quantity, unit, and optional notes.
_Avoid_: Ingredient (alone), pantry line

**MealSlot**:
A coarse time-of-day hint on a recipe: breakfast, lunch, or dinner.
_Avoid_: Meal type, course

**Starter pack**:
Platform-seeded recipes in the shared catalog. **Immutable** after seed — no update or delete via the normal API. Variants are new recipes created by the client (GET existing + POST new) — no dedicated copy endpoint in v1.
_Avoid_: Household copy-on-first-use, editable platform template, mutable seed, POST /recipes/{id}/copy (v1)

**Soft delete**:
A recipe marked deleted (`deleted_at` set) is hidden from default lists and treated as gone for normal reads, but retained for a retention period before hard purge. Publishes `lifeatlas.recipe.deleted` on soft-delete; retention hard-purge emits no event. Only the creator may soft-delete (starters cannot be deleted).
_Avoid_: Immediate hard delete as the v1 user-facing behavior

## Out of this context

**Household** — PantryPilot’s tenant (pantry, plan, shopping list). RecipeHub does not model or isolate by household. Which recipes a household uses is PantryPilot’s association.
_Avoid in RecipeHub_: household_id, X-Household-Id, multi-household tenancy
