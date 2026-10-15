# ListenBrainz provider implementation evidence

## Provider resolution

2026-10-15: a scoped resolver binds discovery, candidate hydration and seed tags
as one provider bundle. Selection is recorded in generation configuration;
unknown identifiers fail closed. Last.fm remains the default during adapter
development. No provider UI or account linking has been added.

Verification: targeted Infrastructure provider/storage checks passed 10/10,
zero skipped. The sandbox cannot open the test-runner socket; the same command
passed with the required sandbox permission. Live calls were not involved.
