# Contributing

Thanks for taking a look at Liner Notes. The guides below cover the project setup and the boundaries a change should follow.

## Get oriented

- Read [AGENTS.md](AGENTS.md) for project rules and [README-Developer.md](README-Developer.md) for the first local run, repository map, and request paths.
- Use [local configuration](docs/renewal/local-configuration.md) for settings and database setup. The [ListenBrainz provider runbook](docs/renewal/listenbrainz-provider-runbook.md) covers provider-specific work.
- Browse the [architecture diagrams and Nebula](docs/architecture/README.md) before changing code across layers.

## Make a focused change

- Keep scoring and recommendation policy in `Domain`; keep provider calls in `Infrastructure`; keep HTTP and UI wiring in `Presentation`.
- Preserve the provider interfaces and stored score breakdowns. If a change affects user data, explain what is stored and how users can see or export it.
- For scoring changes, update the benchmark seed set and run the required domain checks described in `AGENTS.md`.
- Update `CHANGELOG.md` for user-visible changes. Avoid unrelated formatting or cleanup in the same pull request.

## Open a pull request

Describe the problem and the smallest change that addresses it. List the checks you ran and their results; if you did not run a check, say so. Call out effects on providers, privacy, or stored data, even when the answer is “none.”

Do not commit credentials, local settings, database contents, recordings, or personal listening data. Bug reports should use synthetic or redacted examples.
