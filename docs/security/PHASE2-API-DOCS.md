# Phase 2 security remediation: production API documentation

**Evidence ID:** `ALPHA-EV-20261011-P2-API-DOCS`  
**Repository:** `Coxskull/alpha-backend`  
**Scope:** Limit Swagger/OpenAPI UI exposure in hosted environments.

## Finding

The baseline `Program.cs` registered `app.UseSwagger()` and `app.UseSwaggerUI()` unconditionally. This made API schema and interactive documentation endpoints available in every environment where the application was running. Swagger does not itself bypass endpoint authorization, but exposing the schema in production can unnecessarily disclose route and contract details.

## Change proposed

- Swagger remains enabled automatically in Development.
- In non-development environments, Swagger is enabled only when `ApiDocs:Enabled=true`.
- On standard .NET configuration providers, the environment-variable spelling is `ApiDocs__Enabled=true`.
- The change does not change controller authorization, API routes, database configuration, or deployment settings.

## Validation and limits

- A pull request is opened for review; exact-head CI is the required build evidence.
- Production configuration has not been changed and production endpoints have not been probed.
- Do not set the opt-in flag in production unless an authorized operator explicitly requires the docs.
- This is defense-in-depth, not a substitute for endpoint authorization or network access controls.
