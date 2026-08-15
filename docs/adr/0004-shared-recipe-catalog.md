# ADR-004: Shared recipe catalog

- **Status:** Accepted
- **Context:** Private journals vs shared knowledge base for Life Atlas.
- **Decision:** All recipes are globally readable; no household scoping in RecipeHub. Attribution via optional `author`; permissions via `creatorId` (JWT `sub`).
- **Consequences:** PantryPilot owns which recipes a household *uses*; RecipeHub stays a thin shared catalog.
