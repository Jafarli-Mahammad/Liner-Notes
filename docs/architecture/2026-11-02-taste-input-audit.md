# Frontend taste inputs and Residue: trace audit

Date: 2026-11-02. Reviewed source: `bb78dd5`, branch `Prism`.

## Verdict and evidence boundary

The dedicated taste controls are wired through persistence into discovery, but they are **not all numerical weights**, and they do not necessarily all influence a particular batch. Residue currently combines candidate acquisition, a tag/novelty score, exclusions, and an ordered preference policy. Feedback notes are stored but never interpreted as taste.

This is a source trace plus local targeted tests. It does not establish what a running instance has configured, that a particular user's saved inputs reached a live-provider batch, or recommendation quality. No runtime configuration, private user records, provider calls, browser journey, or PostgreSQL integration tests were inspected/executed for this audit. No recommendation behavior was changed.

## Actual path

1. `src/Presentation/ClientApp/src/features/taste/Taste.tsx` holds favorites, tags and the `Answers` object. Save sends these through `Personal.saveTaste` in `src/Presentation/ClientApp/src/app/Personal.tsx:75` to `PUT /api/discovery/onboarding`. Unsaved edits do not affect generation.
2. `src/Presentation/Discovery/TasteOnboardingController.cs` resolves the listener and invokes `SaveTasteOnboarding`. `SelectDiscoveryTaste` resolves canonical catalog selections and validates vocabulary tags. The browser sends identifiers, not arbitrary scoring weights.
3. `src/DataAccess/Persistence/Repositories/DiscoveryTasteStore.cs:10` saves manual selections as `TasteSignals` and answers as `ListenerProfile.OnboardingJson`, under the coordinated transaction and profile revision. Changing taste also cancels outstanding generation jobs. `DiscoveryReader` returns saved selections and answers to the UI.
4. `DiscoveryBatchStore.ReadInputsAsync` reads saved signals, profile answers and recent feedback. `EffectiveTasteInputs.FromProfile` creates positive inputs, familiarity and exclusions. `MaterializeDiscoveryTaste` obtains descriptive evidence and constructs the aggregate taste vector and acquisition seeds.
5. `src/Application/Features/Discovery/PrepareDiscoveryBatch.cs` retrieves candidate pools, merges identities, applies exclusions, calls `BaselineAScorer`, then calls `DiscoverySelectionPolicy.Rank` with the saved answers.
6. Picks persist a `ScoreBreakdown` and snapshot. `ScoreBreakdown.GenerateExplanation` formats the stored numbers and preference reasons. The React discovery view displays this explanation.

The active shared discovery scorer is `BaselineAScorer`, not the older `RecommendationScorer`. Inspecting the older scorer alone would give the wrong account of popularity and feedback penalties. The weekly handler uses shared preparation when `DiscoveryRuntime.RunJobs` is enabled; its alternate legacy path does not apply the onboarding policy. This audit does not verify a running host's choice of path.

## Input coverage

| Frontend input | Actual effect | Limits and interpretation |
|---|---|---|
| Favorite artist | Artist candidate seed, descriptive tag evidence, familiar-artist status | Selection starts at weight 1. Identity itself is not an additional continuous affinity term. |
| Favorite album | Album tag evidence; credited artists seed candidates | Missing album tags may use explicitly recorded artist fallback at configured influence, default 0.35. Candidate similarity can also be discounted for album-fallback-only paths. |
| Favorite recording | Recording candidate seed and recording tag evidence; credits mark familiar artists | The selected recording is excluded from recommendations back to its owner. Missing recording evidence stays missing; there is no inferred audio profile. |
| Genre, sound and mood tags | Aggregate taste vector and tag-based acquisition | All positive tag signals enter the vector. More than five tag signals receive group scaling `1 / sqrt(count)`. Acquisition uses a bounded rotating tag window. A saved vocabulary tag only helps similarity where candidate evidence supports it. |
| Goal: new artists | Sorts unfamiliar artists ahead of familiar ones | Familiarity means seed/feedback familiarity, not comprehensive knowledge of the user's listening history. This is a leading ranking key, not a larger novelty weight. |
| Goal: more like favorites | Sorts by descending raw tag overlap | Overrides the preference for surprise wherever overlaps differ; may also override numerical novelty/fallback adjustments. |
| Goal: different styles | Sorts by ascending raw tag overlap | Can prioritize the least overlapping eligible candidate. There is no minimum-affinity threshold in this selection method. It does not itself fetch a new genre pool. |
| Goal: mixture | No extra ranking key | Uses the remaining preference keys and default score. It does not enforce a familiar/new or close/far quota. |
| Exploration: close | Sorts by descending raw overlap after goal | Does not change the candidate requests or score weights. |
| Exploration: surprise | Sorts by ascending raw overlap after goal | Does not enlarge the acquisition radius. Its effect depends on variation already present in retrieved pools. |
| Exploration: balance | No extra ranking key | Default numerical scoring remains; no special balancing algorithm or quota. Null/skipped is also neutral in ranking. |
| Vocals / instrumental | Later ranking key: match 1, unknown/conflicting evidence 0, opposite -1 | Only explicit recording-level tags count. Artist-level tags cannot establish a track's vocal content. Earlier overlap keys usually decide before this key is reached. This is a soft preference, not an exclusion filter. |
| Vocals: Any / both / no preference | No extra ranking key | Neutral by design. Backend also retains null and skipped states distinctly. |
| All musical styles welcome | Replaces selected-tag acquisition requests with a rotating broad genre window | Favorite and selected-tag taste evidence remains in the scoring vector. It does not mean all genres are queried in each batch or that existing taste is ignored. |
| Skip optional questions | Stores skipped goal/exploration/vocals | Neutralizes those preference keys; it does not clear favorites, tags or AllStyles. |
| Like | Adds artist taste plus up to three previously matched tags | Recent feedback window only; tag weight defaults to 0.5 times the positive-feedback weight. This reinforces supported matched tags, not every candidate tag. |
| Rating 7–10 | Positive feedback with graduated weight | Weight is `clamp((rating - 5) / 5, minimum, 1)`; default minimum 0.4. A Like with a middle rating can also produce positive input. |
| Dislike / rating 1–3 | Excludes the track aliases | No general artist, genre or acoustic dislike model. Contradictory Like/low-rating and Dislike/high-rating combinations are rejected by the Domain entity. |
| Rating 4–6 without Like | No positive or negative taste change | Stored, but not a continuous whole-scale ranking signal. |
| Already know | Track suppression and retained known marker | Does not by itself generalize familiarity to every track by that artist. Recommendation history separately suppresses already recommended track identities. |
| Feedback note/comment | Stored and returned/exported | Not parsed, embedded, converted to tags or used in scoring/ranking. “Less vocals please” in a note does not change the vocal setting. |
| Search text, navigation, artwork and link clicks | No taste input in the traced recommendation path | Catalog searching becomes taste only when a selection is saved. No implicit click/listening model was found in this path. |
| Onboarding version and step | Validation/progress state | Persisted, not musical preference weights. |

Sources: `Taste.tsx:68`, `SelectDiscoveryTaste.cs`, `TasteOnboarding.cs`, `EffectiveTasteInputs.cs:45`, `DiscoveryTasteMaterializer.cs:29`, `MaterializeDiscoveryTaste.cs:50`, `PrepareDiscoveryBatch.cs:27`, `DiscoverySelectionPolicy.cs:17`, and `FeedbackControls.tsx`. Paths without prefixes above refer to their corresponding Domain/Application/Presentation source directories.

## What the score actually means

`BaselineAScorer.ScoreCore`, `src/Domain/Scoring/BaselineAScorer.cs:101`:

```text
score = TagWeight × cosine(aggregate taste tags, candidate tags) × SimilarityInfluence
      + NoveltyWeight × (familiar artist ? 0 : 1)
```

The configuration defaults are TagWeight = 1 and NoveltyWeight = 0.2. Popularity and feedback-penalty components in this active score are zero. Negative feedback acts through exclusions; positive feedback changes taste inputs. Upstream similarity/path evidence helps acquire candidates and is stored as provenance; it is not an additional numerical similarity term in this formula.

The final order is lexicographic:

1. Unfamiliar artist first, if goal is new artists.
2. Raw overlap descending/ascending, if goal is favorites/different styles.
3. Raw overlap descending/ascending, if exploration is close/surprise.
4. Explicit recording-level vocal match.
5. Numerical score, then canonical track key.
6. Keep one pick per normalized artist name and take the configured number of picks.

Each later key only matters when every earlier key ties. This is not a weighted blend. In particular, goal=favorites plus exploration=surprise still sorts primarily by descending overlap. Equal overlap also gives equal exploration keys, so surprise cannot reverse the goal. `OptionalTastePolicyTests.GoalsAndExplorationChangeSelectionWithoutChangingStoredScores` explicitly tests that precedence.

Likewise, vocal preference cannot overcome a different overlap when an overlap preference is active. With no goal/exploration key, it precedes the numerical score and can have a substantial effect. Two candidates can change final selection order without any change to their stored numerical scores.

## Coverage and traceability findings

1. **Preference composition is the largest expectation risk.** Dedicated controls are connected, but some combinations make a control ineffective. Broad exploration remains restricted to the existing pool unless AllStyles changes acquisition. Treat this as an explicit product-policy decision; passing tests confirm the current precedence, not that it matches the intended experience.
2. **Not every saved favorite participates in every batch's tag materialization.** `MaterializeDiscoveryTaste.cs:50` windows positive entity signals to `MaximumArtistSeeds`, default 20, shared across artist/album/recording selections and positive artist feedback. Later acquisition limits can further trim requests. The window rotates by completed-generation ordinal. All positive manual tags still enter the vector, but only a bounded tag window drives acquisition. Rotation tests cover eventual window coverage; that is not simultaneous use.
3. **Evidence availability constrains influence.** A favorite with no supported descriptive tags cannot contribute those missing tags. Album fallback is explicitly weaker; missing recording evidence is recorded rather than fabricated. ListenBrainz hydration can use recording tags or artist-tag fallback (`ListenBrainzRecommendationProvider.cs:137`); only the former can affect vocal preference. Actual coverage frequency was not measured here.
4. **Long-term feedback is not all used for positive taste.** `DiscoveryBatchStore.cs:62` loads the latest `FeedbackWindow` feedback rows, default 20, maximum 40. Older likes/high ratings fall out of positive taste materialization. Comment-only and cleared-feedback updates also receive feedback timestamps and can occupy this window. Separate recommendation-identity history still suppresses repeated tracks; a feedback window is not the full history boundary.
5. **Comments are intentionally passive data.** Storing or exporting a note is different from weighting it. The frontend should make that distinction clear if users expect written instructions to be acted on.
6. **Numerical score receipts are substantially stronger than selection receipts.** Stored evidence includes seed identities, tag evidence and scope, source response references, missing reasons, fallback influences, normalized contributions, configuration/hash, familiarity, selection answers/policy/reasons and acquisition window. Numerical replay is tested. For a taste vector over 80 dimensions, the score snapshot can retain matching dimensions plus unmatched squared magnitude, preserving the numeric replay without preserving every original dimension in that field.
7. **Full causal reconstruction is incomplete.** Per-pick `Evidence.Signals` is filtered and capped at 80. `FullInputSha256`/count covers effective positive signals, not the complete request, answers or exclusion state; answers are stored separately. The receipt does not retain the complete competing candidate set, all exclusion decisions, a per-control counterfactual, or the result of applying every ranking key to every competitor. `BaselineAScorer.Replay` reconstructs the score; it does not rerun the whole batch-selection policy. Generic stored preference reasons can describe a requested preference even when an earlier key made it irrelevant.
8. **The frontend displays a summary.** `DiscoveryPick` exposes explanation text, matched tags and sources, rather than the entire score snapshot. `Discover.tsx` renders this text. A user cannot inspect the full evidence chain or distinguish all precedence effects in the current pick UI.

## Verification and remaining work

Local checks passed with zero failures/skips:

- Domain: 39 tests selected from `OptionalTastePolicyTests`, `DiscoveryTasteMaterializerTests` and `BaselineAScorerTests`. Coverage includes benchmark genre score reconstruction, bounded materialization, stored-score replay, ranking precedence, recording suppression and recording-only vocal evidence.
- Application: 38 tests selected from `PrepareDiscoveryBatchTests`, `DiscoveryTasteTests` and `DigestAndFeedbackTests`. Coverage includes shared preparation, merged provenance, suppression, canonical selections and feedback handling.

Commands used `dotnet test`, `--no-restore`, `-m:1`, disabled parallel/shared compilation for builds, and filtered by the class names above. Final captured runs used `--no-build` after those builds.

Existing PostgreSQL onboarding tests cover answer/tag round trips and export, and a browser fixture test checks exploration/vocals in the submitted payload. They were inspected but not run in this audit. A complete live trace would need a controlled saved profile revision, its returned job/batch IDs, persisted answers/signals, actual acquired pools, selected picks and explanations. Neither source inspection nor these unit tests substitutes for that evidence.

Recommended next scope, requiring a product decision before behavior changes: define how goal, exploration and vocals should combine; decide whether exploration must change acquisition; explicitly communicate feedback-note and history-window semantics; and add a bounded batch-selection receipt that records the actual ranking keys and reasons for inputs not used. Recommendation-quality evaluation remains separate from wiring correctness.
