# Railway startup incident — runtime JWT configuration

Status: root cause identified; Railway remediation not applied or verified.

The supplied log logs.1791859204471.log repeatedly reports, between 2026-10-13 02:38:50 and 02:39:00 UTC:

`System.InvalidOperationException: A runtime JWT signing key of at least 32 bytes is required.`

The exception comes from RuntimeSecurity.Validate at Program startup, before Kestrel binding or PostgreSQL initialization. The log proves the effective Jwt:Key configuration is missing/blank or has fewer than 32 UTF-8 bytes. It does not distinguish these conditions or establish anything about database connectivity. This is consistent with intentionally removing tracked signing credentials; the application requires a runtime binding.

## Configuration remediation for the service owner

1. In the affected Railway service and environment, inspect variable names and references without sharing their values. Bind the approved private JWT signing secret as `Jwt__Key` (two underscores), which .NET maps to Jwt:Key. A value under JWT_SECRET, JWT_KEY or Jwt_Key alone does not satisfy this code. If a secret already exists under another name, correct the binding rather than copying a value into source control.
2. The effective secret must be a nonblank unpredictable signing key of at least 32 UTF-8 bytes. Do not restore the previously exposed tracked key, which is explicitly rejected. If no approved key exists, its owner must generate/provision one privately through the secret store; no key has been generated, disclosed or changed by this investigation.
3. Existing issuer/audience defaults in appsettings.json are Alpha.API and Alpha.Frontend. Any runtime overrides must agree with token issuance and validation. Runtime DATABASE_URL is also required, but the current exception occurs before that check; no database access is needed to diagnose this crash.
4. After the authorized owner applies the configuration, restart/redeploy the affected service under their operational authority. Verify the signing-key exception stops and the service reaches listening state. Check the configured health route; HTTP 200 alone does not prove database/provider acceptance. If another exception appears, inspect sanitized fresh logs rather than weakening startup guards.

No code change to remove or bypass validation is appropriate. No random-on-start signing fallback should be added: replicas/restarts would invalidate or disagree about tokens. Do not paste secrets into chat, logs, appsettings, PRs or issue descriptions. Existing tokens may need reauthentication if the owner intentionally changes the signing key.

## Verification performed

Exact main eebeb85d23a9068cd799b763a2f37a405e0d09b3 passed the disposable local gate: 175 tests, Release builds with zero warnings/errors, clean dependency audits, nine-migration model/schema parity and synthetic startup HTTP 200. The unit suite includes missing/weak signing-key rejection. These observations verify the runtime configuration contract locally, not recovery of the Railway service.

No Railway tool/connection is available in this session. Earlier instructions prohibit production access, deployment and credential changes; none were performed. Actual Railway recovery remains pending owner configuration and fresh runtime evidence.
