# Local configuration and rotation

Development uses an explicit, well-known PostgreSQL password (`postgres`) and signing key in the
Presentation and Worker development settings. This is the developer's temporary local default,
not a deployable secret. Start an isolated local PostgreSQL database with the matching `linernotes`
database and local account, or override `ConnectionStrings__DefaultConnection` for both hosts.
Application users still set their own passwords. Production settings keep database and signing
credentials empty; the Presentation host refuses the development signing key outside Development.

For non-development environments, set `ConnectionStrings__DefaultConnection` and `Jwt__SecretKey`
in the process environment or Presentation user secrets. `.env.example` names the variables; .NET
does not load `.env` automatically. Use a randomly generated signing secret of at least 32 bytes.
Leave Last.fm credentials unset for offline work. No upstream acquisition is authorized here.

The prior source had a default database password. Treat any credential ever committed or reused
from examples as exposed outside local development. Operators must rotate it in its owning system,
update runtime secrets and verify the old value no longer works. Rotate exposed signing and refresh
credentials, and any exposed Last.fm/email keys with their providers. This documentation does not
rotate external credentials or rewrite Git history.

`.gitignore` protects local configuration, credential files and recordings from accidental tracking.
PR 1K removes tracked generated files from the Git index.
