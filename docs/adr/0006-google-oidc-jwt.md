# ADR-006: Google OIDC JWT on writes

- **Status:** Accepted
- **Context:** Need a real creator identity without coupling to PantryPilot, and open catalog reads.
- **Decision:** Google OIDC JWT Bearer on mutating routes; `creatorId = sub`. GETs are anonymous. Development mode uses HMAC test JWTs for Compose/local.
- **Consequences:** Standalone-testable; Apple Sign-In deferred; admin ingredient writes allowlisted by `sub`.
