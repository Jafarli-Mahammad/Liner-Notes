<p align="center">
  <img src="docs/assets/liner-notes-cover.svg" alt="A record sleeve, vinyl record and sound waves" width="100%">
</p>

<h1 align="center">Liner Notes</h1>

<p align="center"><strong>Make room for music you haven’t heard yet.</strong></p>

Liner Notes is an open-source music discovery project. The goal is to send a
short weekly list shaped by the music you like, with an explanation saved for
every pick and feedback that can shape what comes next.

**The project is in active development.** Some parts of the site work locally;
the full weekly email service and recommendation quality are not verified.

[What it does](#the-idea) · [How recommendations work](#how-recommendations-work) ·
[Current status](#current-status) · [Developer guide](README-Developer.md)

## The idea

Most of us have a handful of artists, albums or songs we return to. Liner Notes
starts there and looks for music just outside that familiar set. The intended
weekly list is small enough to listen to, and each pick keeps the evidence that
explains why it appeared.

That explanation matters. You should be able to see what parts of your taste
informed a pick, respond to it, and inspect or export the taste data the project
stores.

## How recommendations work

Candidate tracks and similarity evidence come from music services. ListenBrainz
is the configured default; Last.fm remains available behind the provider
interface. Liner Notes applies its own deterministic scoring and feedback
re-ranking to the evidence those services provide. It does not build upstream
similarity data, use a custom machine-learning model, or prefer popular or
obscure tracks by default.

Each saved recommendation includes a score breakdown. Its explanation is
formatted from that stored data, so later page views do not need to contact a
music provider to explain a pick.

## The discovery loop

This is the product's intended weekly rhythm. It describes the goal, not a
claim that scheduled email delivery is ready:

```mermaid
flowchart LR
    Taste["Your artists, records and feedback"] --> Find["Provider candidates and evidence"]
    Find --> Rank["Deterministic scoring and feedback re-ranking"]
    Rank --> Notes["Saved picks with score explanations"]
    Notes --> Listen["Listen, rate or mark familiar"]
    Listen --> Taste
```

For what works today and what remains open, see [current status](#current-status)
and [PROGRESS.md](PROGRESS.md).

## Current status

The repository includes a React browser app, account and guest discovery flows,
local catalog search, taste editing, recommendation feedback, history, and
export and deletion paths. Developers can run the site locally with live
ListenBrainz and MusicBrainz data after completing the private local setup.

The `Worker` has manual operations for reconciliation, generation and capture,
and the repository includes scheduled processing code. Public email delivery is
not configured. Per-user provider choice and listening-history import are not
available.

Local checks and browser runs show that individual flows work in this checkout.
They do not establish recommendation quality, production capacity, provider
release approval, or production readiness. The current project status and open
work are tracked in [PROGRESS.md](PROGRESS.md).

## For developers


Start with the [developer guide](README-Developer.md) for setup, architecture,
feature entry points and verification commands. The ASP.NET Core host is in
[`src/Presentation`](src/Presentation); its React app is in
[`src/Presentation/ClientApp`](src/Presentation/ClientApp/README.md). Read [AGENTS.md](AGENTS.md)
before making changes; it records the project boundaries and contribution
requirements.

### Project references

These arrows show which projects reference one another, not runtime requests:

```mermaid
flowchart TB
    Web["Presentation<br/>ASP.NET Core and browser app"] --> App["Application<br/>Handlers and interfaces"]
    Worker["Worker<br/>Batch and scheduled operations"] --> App
    Web --> Data["DataAccess<br/>EF Core and PostgreSQL"]
    Worker --> Data
    Web --> Infra["Infrastructure<br/>Music-provider adapters"]
    Worker --> Infra
    Data --> App
    Infra --> App
    App --> Domain["Domain<br/>Taste and scoring"]
    Data --> Domain
    Infra --> Domain
    classDef host fill:#153e53,color:#fff,stroke:#67d8da
    classDef adapter fill:#48274e,color:#fff,stroke:#f4a1ca
    classDef core fill:#513827,color:#fff,stroke:#ffc58f
    class Web,Worker host
    class Data,Infra adapter
    class App,Domain core
```

### Explore the source graph

[Open the interactive Nebula](https://jafarli-mahammad.github.io/Liner-Notes/?tab=graph&project=LinerNotes). It is rebuilt from `main` on each successful Pages deployment; the graph is interactive, but its data is a generated snapshot rather than a live server connection.

![Nebula graph snapshot of Liner Notes source, tests and tooling](docs/architecture/codebase-nebula.png)

This is a static Codebase Memory snapshot of commit `62a2698`, captured on
2026-10-10. It can help you find related code, but it is not a runtime diagram
and does not include later changes. The [snapshot notes](docs/architecture/README.md)
explain its scope and limits.

Useful project documents:

- [Current progress and open work](PROGRESS.md)
- [Product contracts](docs/renewal/contracts.md)
- [Local configuration](docs/renewal/local-configuration.md)
- [Changelog](CHANGELOG.md)
- [Security policy](SECURITY.md)

For bugs and ideas, use the [issue tracker](https://github.com/Jafarli-Mahammad/Liner-Notes/issues).
Please use the security policy for vulnerability reports.

Liner Notes is licensed under [AGPL-3.0](LICENSE).
