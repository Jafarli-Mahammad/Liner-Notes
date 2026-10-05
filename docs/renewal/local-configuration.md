# Local configuration and rotation

Set `ConnectionStrings__DefaultConnection` and `Jwt__SecretKey` in the process environment or
Presentation user secrets. `.env.example` names the variables; .NET does not load `.env` automatically.
Use a randomly generated signing secret of at least 32 bytes. The hosts fail closed without database
configuration; Presentation already rejects absent/short signing keys. Never put populated values
in source, command transcripts, test reports or credential-bearing URLs. Leave Last.fm credentials
unset for offline work. No upstream acquisition is authorized here.

The prior source had a default database password. Treat any credential ever committed or reused
from examples as exposed. The operator must rotate it in its owning system, update local/runtime
secrets and verify the old value no longer works. Rotate any exposed signing key (invalidates access
JWTs) and refresh credentials; rotate Last.fm/email keys with their providers if exposure is found.
This commit removes current defaults; it does not remove Git history or rotate external credentials.
History rewriting, production invalidation and external key rotation need separate approval.

`.gitignore` protects local configuration, credential files and recordings from accidental new
tracking. It cannot remove already tracked secrets or build outputs; PR 1K handles generated files.
