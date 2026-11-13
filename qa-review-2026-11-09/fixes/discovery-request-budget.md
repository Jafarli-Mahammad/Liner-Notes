# Discovery request budget failure

The affected account's latest generation job failed with `upstream_request_limit`; earlier jobs included the same failure and a timeout. A live preparation run using that account's current 17 taste signals reproduced the exact exception in `ListenBrainzApiClient.SendAsync` during candidate acquisition.

The runner previously classified every `GenerationStoppedException` as permanent. This discarded the opportunity to resume from already cached public evidence when the per-attempt request budget was reached.

The runner now retries only `upstream_request_limit` through its existing retry mechanism. Each execution retains its original request cap; attempt limits, retry delays, job age, rate limiting, evidence eligibility and private-data boundaries remain enforced. Other generation stops remain terminal. No taste signals or saved picks are edited by the diagnostic preparation.

After resuming, the account exposed a second independent failure: `PrepareDiscoveryBatch` called `ToDictionary` over all effective signal IDs. Learned artists and learned matched tags intentionally share their recommendation ID. The selection-interest lookup now indexes only entity signals, matching `DiscoveryTasteMaterializer`; normalization still uses all positive weights. No scorer formula changed.

Verification:
- Original live inputs reproduced `upstream_request_limit`, followed by the duplicate-key exception after acquisition could proceed.
- With both fixes, the same account's 17 saved signals successfully prepared **five picks** against real providers. Preparation did not publish a batch or edit saved picks.
- 4 background-runner cases cover persisted budget retries, provider backoff and lane isolation.
- 14 targeted application cases pass, including a new real feedback receipt with multiple learned tags sharing the learned artist's recommendation ID.
- 5 domain lease cases pass.

Restart the Presentation launch profile to load the code and request the next discoveries. Previously terminal jobs retain their historical failure status; fresh requests use the corrected runner. Developer confirmation remains pending.


## Full persistence follow-up (2026-11-10)

The latest reported failure still ran in the locally managed host started before the previous fix. Restarted that host with the current code and submitted an account-scoped request through `SubmitDiscovery`, leaving its processing to the actual ASP.NET background runner.

This full path exposed `prepared_evidence_expired` in `DiscoveryBatchStore.CompleteAsync` after the request-budget retry. Public evidence can age out while other pools are being acquired. The save correctly rejected expired evidence, but the runner classified that recoverable condition as terminal. It now retries that specific failure within the existing attempt and total-age bounds. Neither transaction freshness check is relaxed.

The regression suite now has five passing cases; its expiry case throws from the batch completion store after successful preparation, reproducing the failed stage. Repeated live verification showed retry alone was insufficient: an 80-request cap cycled through exhausted budgets and expired evidence. The default acquisition window supports up to 40 pools plus favorite materialization; its cold request demand can exceed 80. Raised the default per-attempt budget to the already validated 200-request ceiling. Rate gates, concurrency, timeout and total job age remain enforced.

Once acquisition completed, persistence exposed `batch_evidence_invalid`: the full selection/coverage audit exceeded 32 KB. Expanded the domain and database coverage bound to 256 KB through `ExpandDiscoveryCoverageBound`. Preserved the full report and replay data; included actual UTF-8 coverage size in the coordinated write reservation. Existing per-pick snapshot bounds remain enforced.

Final live result: the account-scoped request completed on attempt **one** in the running ASP.NET host. The discovery read projection reports `Completed` and returns **five persisted picks**, all with nonempty explanations. The previous account batches remain available. The local host is running the updated code and its database migration has been applied.

Final focused checks: 5 background-runner cases; 7 domain coverage/lease cases; 1 real PostgreSQL migration test confirming retention above the old 32 KB cap and rejection above 256 KB; targeted application preparation/feedback tests. Developer UI confirmation remains pending.
