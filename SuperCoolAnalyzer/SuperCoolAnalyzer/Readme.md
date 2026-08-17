# SuperCoolAnalyzer

Project-specific Roslyn analyzers for SuperCoolWebServer.

## SCWS0001: Identity type exposed in an MVC response

Reports a warning when a public ASP.NET Core MVC controller action returns an `IdentityUser` type or subclass. The rule covers unsafe declared response contracts and identity values nested in response payloads, including anonymous objects, DTO members, collections, and straightforward local-variable flows.

Map identity models to purpose-built DTOs before returning them. Sending an identity model directly can expose security-sensitive fields.

Methods marked `[NonAction]` and methods outside MVC controllers are ignored. Minimal API handlers are intentionally outside this rule's scope.
