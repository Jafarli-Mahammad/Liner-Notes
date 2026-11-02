# Local configuration and credential rotation

Development settings do not contain a database password or JWT signing key. Presentation and
Worker require `ConnectionStrings:DefaultConnection`; Presentation also requires `Jwt:SecretKey`.
Both projects use the same `UserSecretsId`, and the .NET hosts load User Secrets in Development.

Configure private local values with Secret Manager, or provide the equivalent environment
variables (`ConnectionStrings__DefaultConnection` and, for Presentation, `Jwt__SecretKey`):

```sh
dotnet user-secrets set --project src/Presentation/Presentation.csproj \
  "ConnectionStrings:DefaultConnection" "<your private local PostgreSQL connection string>"
dotnet user-secrets set --project src/Presentation/Presentation.csproj \
  "Jwt:SecretKey" "<a locally generated secret of at least 32 bytes>"
```

Replace the placeholders locally; never commit the values or paste them into chat, logs, or
command transcripts. `.env.example` only names variables; .NET does not load `.env` automatically.
Local helper scripts may require `LINER_MANUAL_QA_CONNECTION_STRING` for an explicitly selected
QA database. Keep that value private and use a database password unique to the local database.

For non-development environments, set database and signing credentials through the deployment's
secret store or process environment. Presentation rejects missing or undersized JWT signing keys.
Use a randomly generated signing secret of at least 32 bytes. When TLS terminates at a reverse
proxy, set `ForwardedHeaders__KnownProxies__0` to that proxy's trusted IP address. Forwarded scheme
and client IP headers are accepted only from configured proxies; use a stable proxy address or
update this setting when it changes.

Leave Last.fm credentials unset for offline work. No upstream acquisition is authorized here.

The previous development settings included a shared database password and a committed signing
key. Treat the old database password as exposed: if a local database still uses it, change that
database account's password and update the private connection string. Rotate any external
credentials only if they were real and used; this documentation does not change a running
database, rotate provider credentials, or rewrite Git history.

`.gitignore` protects local configuration, credential files, and recordings from accidental
tracking. PR 1K removes tracked generated files from the Git index.
