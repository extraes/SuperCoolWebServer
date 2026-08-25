# SuperCoolAnalyzer

Project-specific Roslyn analyzers for SuperCoolWebServer.

## SCWS0001: Identity type exposed in an MVC response

Reports a warning when a public ASP.NET Core MVC controller action returns an `IdentityUser` type or subclass. The rule covers unsafe declared response contracts and identity values nested in response payloads, including anonymous objects, DTO members, collections, and straightforward local-variable flows.

Map identity models to purpose-built DTOs before returning them. Sending an identity model directly can expose security-sensitive fields.

Methods marked `[NonAction]` and methods outside MVC controllers are ignored. Minimal API handlers are intentionally outside this rule's scope.

## SCWS0002: Snowflake identity copied to a new object

Reports a warning when an object or `with` initializer copies the `ISnowflake.Id` of an existing snowflake into the identity of a newly initialized snowflake. Every new snowflake object must receive a newly generated ID, even when the source and target have the same type.

The rule follows direct expressions, conversions, conditional expressions, and single-assignment local variables. It deliberately checks the semantic `ISnowflake.Id` contract, so relationship fields such as `CreatedBy`, DTO projections, and unrelated properties merely named `Id` are ignored. Mutable local-variable flows and values returned through helper methods are outside the rule's current scope.
