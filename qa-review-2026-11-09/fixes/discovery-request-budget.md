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
