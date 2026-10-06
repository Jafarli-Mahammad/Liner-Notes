# Liner Notes

Liner Notes is an open-source project for weekly music discovery with inspectable score explanations and user-owned taste data. It is under renewal; weekly email delivery and a complete recommendation pipeline are **not verified**.

Candidates and similarity evidence come from upstream providers. Liner Notes applies deterministic scoring and feedback re-ranking; it does not claim a new algorithm or ownership of Last.fm's similarity data. A Last.fm client exists. ListenBrainz and username/history imports are deferred.

The approved V1 direction is popularity-neutral, with three to five picks, stored explanations, track-level exclusion for known/disliked tracks, and sibling tracks remaining eligible. Existing scoring and demo values do not yet implement that entire contract. Synthetic examples demonstrate mechanics, not musical fit.

The current export and deletion flows are incomplete. Neither complete data portability nor legal privacy compliance is claimed. A unique weekly digest record does not guarantee exactly-once email delivery.

See [PROGRESS](PROGRESS.md) for authorization/status, [renewal contracts](docs/renewal/contracts.md) for pending decisions, and [verification evidence](docs/renewal/phase-1-evidence.md) for tested behavior.

## Structure

- `src/Domain`: pure entities, values and scorer.
- `src/Application`: CQRS orchestration and interfaces.
- `src/DataAccess`: EF Core persistence, Identity stores and migrations.
- `src/Infrastructure`: provider clients, caching and future email integration.
- `src/Presentation`: ASP.NET Core host and browser UI.
- `src/Worker`: second composition root; batch delivery remains future work.
- `tests/`: Domain, Application, DataAccess, Infrastructure and Presentation checks.

.NET 10 is the target framework; EF and several other package references remain 9.x pending approval of remediation. The architecture specifications describe intended behavior, not implementation certification.

## Local verification

```bash
dotnet restore 'Liner Notes.sln'
dotnet build 'Liner Notes.sln' --no-restore
dotnet test 'Liner Notes.sln' --no-restore --filter 'FullyQualifiedName!~FounderSeedCoverageSpikeTests'
python3 scripts/checks/renewal_docs.py
```

The excluded legacy spike can read a locally supplied key and overwrite a report. Running it against Last.fm is not authorized by the offline test command. It remains unchanged pending test-contract review. No Last.fm key is needed for normal offline tests.

The eight-genre synthetic benchmark checks scorer mechanics. It does not establish catalog coverage or recommendation quality. No hosting or email provider has been selected or provisioned by the renewal.
