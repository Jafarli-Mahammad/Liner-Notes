# Optional taste onboarding for guests and accounts

Date: 2026-10-19
Status: Draft for developer review. Product proposals below are not approved implementation decisions.

## Purpose and confirmed intent

Help a visitor describe their taste and reach a useful first discovery without requiring an account or a long questionnaire. The developer requested a discussion followed by a plan for the missing features.

Confirmed requirements from that discussion:

- Begin with clearly readable choices to log in, register, or continue without an account.
- Offer questions before tag and favorite selection. Questions are optional and skippable.
- Offer rich, searchable tags with no product limit on how many a person selects.
- Include a playful option expressing broad, eclectic taste, provisionally “My taste is a soup.”
- Encourage a few favorites, such as three artists, while considering artists, albums and tracks.
- Support the same taste setup for guests and newly registered accounts.
- Keep the ideas open to revision. This document does not authorize product implementation.

Proposed defaults requiring review: support all three favorite types; make tags and favorites individually skippable; allow skipping every input; show a clearly labelled general discovery selection when there is no usable taste evidence. The previous discussion proposed these defaults, but did not settle their detailed behavior.

## Repository findings

Current source, not older design status text, establishes the following baseline:

| Area | Existing behavior | Missing work |
| --- | --- | --- |
| Guest/account ownership | Independent listener profiles, protected guest sessions, registration attachment and explicit import into existing accounts | Reuse these boundaries for onboarding state and answers |
| First visit | Guest welcome leads to a combined selection editor | Account choice followed by a resumable optional setup |
| Favorites | Local artist/album search and deliberate online lookup; five combined favorites | Track catalog support, recording identity and evidence materialization |
| Tags | `SelectDiscoveryTaste.Styles` contains a fixed list of 38 styles; UI renders a tag cloud | Maintained searchable vocabulary, aliases, categories and selected-tag management |
| Input validation | Client and server cap tags at five; selection resolution rejects zero inputs | Unlimited product selection; explicit skipped/no-input behavior |
| Generation | Artist/tag acquisition windows are bounded; materialization rejects over 200 signals/evidence entries | Separate saved selections from bounded generation work without silently dropping taste |
| Feedback | Like, Pass, Already know it, rating and note already exist | Reuse the first three actions after initial results |
| Privacy/lifecycle | Export, deletion, guest expiry and account transfer already exist | Extend them to every new private field |

Primary implementation references:

- `src/Presentation/wwwroot/discovery.js`, `index.html`, `app.js`, `styles.css`
- `src/Presentation/Discovery/DiscoveryController.cs`, `CatalogController.cs`, `GuestController.cs`
- `src/Application/Features/Discovery/SelectDiscoveryTaste.cs`, `SubmitDiscovery.cs`, `MaterializeDiscoveryTaste.cs`, `PrepareDiscoveryBatch.cs`
- `src/Domain/Catalog/CatalogSearch.cs`: `CatalogKind` currently contains only Artist and Album. `TasteTargetType.Track` existing elsewhere does not establish working track onboarding.
- `src/Domain/Taste/DiscoveryTasteMaterializer.cs`
- `src/Application/Common/Models/Recommendation/GenerationConfiguration.cs`
- Existing discovery repositories, migration tests, ownership tests and API tests.

The working tree was clean when this investigation began. No live catalog acquisition, provider calls, dependency updates or application execution were performed for this design.

## Approaches considered

1. **Only revise the selection screen.** Smallest scope, but leaves the questions, track seeds and empty-input path unimplemented. Does not meet the requested experience.
2. **Extend the existing discovery flow and profile. Recommended.** Reuse ownership, jobs, catalog, feedback and account transfer. Add explicit onboarding state and supported preference behavior. This fits the current architecture and preserves the existing guest journey.
3. **Create a separate questionnaire/recommendation subsystem.** Duplicates taste and lifecycle responsibilities and makes account transfer harder. There is no demonstrated need for this additional subsystem.

## Proposed visitor journey

1. **Welcome:** Log in; Create an account; Continue without an account. The guest option receives readable placement and equivalent access to onsite discovery. Account benefits and guest expiry are explained briefly.
2. **Questions:** Three short screens, each with Skip and Back. Also offer Skip all questions. No preselected answer is recorded as consent or preference.
3. **Tags:** Search and browse a rich vocabulary; select any number; inspect/remove selected tags; optionally choose broad taste; Skip continues.
4. **Favorites:** Search Artists, Albums or Songs. Encourage three examples across different parts of their taste. Three is guidance, not a minimum. Keep the existing five combined favorite limit provisionally; the developer requested unlimited tags, not unlimited favorites. Skip continues.
5. **Review:** Show answers and selections together, with Edit actions. Explain whether there is enough evidence for personalization. Start discovery only on an explicit action.
6. **Results:** Show a small batch and stored explanations, followed by Like, Not for me and Already know it. Offer account creation after guests have seen value. Weekly email remains an optional account feature requiring its existing consent and verification conditions.

Known returning profiles resume their saved results or unfinished setup. Existing accounts do not repeat onboarding automatically. Editing setup is always available. Temporary drafts entered before login/registration survive the transition, but importing into an existing account remains an explicit action.

Keyboard navigation, focus after step changes, accessible labels, visible errors and readable guest/Skip actions are acceptance criteria. A failed request must preserve current answers. A retry must not create duplicate profiles or jobs.

## Proposed questions and required effects

These questions are proposals, not permission to collect unused data. Each needs an explicit, deterministic behavior contract before it ships.

| Question | Suggested answers | Intended effect |
| --- | --- | --- |
| What would you like to discover? | New artists; More like my favorites; Different styles; A mixture | A documented discovery policy adjusts candidate sourcing and/or batch selection. “Different styles” uses supported descriptive evidence; it must not become an unbounded random search. |
| How far would you like to explore? | Close to my taste; A balance; Surprise me | Configurable exploration behavior relative to the person's positive evidence, with persisted selection reasons. When there is no positive evidence, disclose that this answer cannot yet define closeness. |
| Any preferences about vocals? | Mostly vocals; Mostly instrumental; Both; No preference | A soft preference only where explicit, provenance-backed descriptive evidence is available. Unknown evidence remains unknown. Never infer the absence of vocals from a missing tag. |

Skipped, explicitly answered No preference, and not yet visited are different states. Answers can be changed or cleared. Store stable answer codes and a questionnaire version; do not store speculative personality classifications.

The vocal question is the main feasibility dependency: this investigation has not verified adequate provider/catalog coverage for vocal information. Before implementation, inspect supported local evidence and verify any external API needed. If coverage cannot support a useful effect, replace or defer that question and explain the change rather than shipping a decorative question. Likewise, goal and exploration choices require a reviewed policy definition; arbitrary numeric multipliers are not approved by this draft.

Discovery goal, exploration, and broad taste express different intentions. Define precedence in the approved policy: explicit exclusions first; selected favorites/tags supply positive evidence; broad taste permits variety across those interests; exploration controls distance from that evidence; goal determines the requested kind of discovery. Broad taste never erases specific selections.

## Tags and broad taste

Use a versioned public vocabulary with canonical identifiers, display labels, aliases and categories. Separate genre/style, mood and descriptive sound categories so a broad genre is not presented as equivalent to a listening situation. Normalize aliases without merging genuinely different styles. Search must handle case, common spellings and diacritics consistently with the existing catalog conventions.

Search and category browsing use local data. Typeahead makes no upstream requests. Search results are paged; selected tags are independently searchable/removable. The first screen shows a manageable starter selection rather than the entire vocabulary. A no-match result must not silently create an arbitrary provider query or substitute a different tag.

Vocabulary breadth is a data deliverable: document its initial scope, source, licensing and maintenance. Do not promise an exhaustive taxonomy or treat unsupported tags as having provider coverage. Verify source terms before adding externally acquired vocabulary; do not repurpose held-out evaluation material.

“My taste is a soup” is an optional broad-taste flag with explanatory text: “I enjoy lots of different styles. Keep my discovery broad.” It can coexist with selected tags and favorites. It is not a real genre, not a provider seed, and does not select every tag or record that the visitor likes every kind of music.

Unlimited means no arbitrary selected-tag count in the product. Store selections separately from candidate acquisition windows. Do not merely raise five to 200: current signal, snapshot and request limits would then remain hidden product limits. Use paged/chunked selection operations where necessary and aggregate taste without requiring every selected tag in every recommendation snapshot. Preserve the existing bounded provider requests, queue, candidate count and storage admission controls. Return actionable transport/storage errors while retaining accepted selections.

All saved selections must remain visible and exportable. An explicit, versioned policy chooses a bounded acquisition window, rotating fairly rather than always taking alphabetically first tags. Scoring must have a documented bounded way to incorporate the wider profile. Record the window and materialization version so an explanation is auditable. Validate normalization so selecting many tags does not automatically overwhelm favorite-derived evidence. Any changes to evidence snapshots must preserve explanation provenance and historical snapshots.

## Artist, album and track favorites

Keep a common catalog-selection interaction, with clear type labels and artist credits. Artists offer broad reference points; albums identify a particular body of work; tracks provide precise examples. None is universally the most informative.

For tracks, use a canonical recording identity and distinguish versions, credited artists and available releases. Do not conflate a recording with a release-specific track position. Preserve the selected recording snapshot across catalog refresh/cleanup and account transfer.

Extend search DTOs, catalog import/publication validation, explicit lookup adapters, canonical snapshots, taste storage, preparation and materialization together. Support recording-scoped tags when available. If preparation falls back to credited-artist evidence, persist that scope and reduced configured influence and disclose the fallback; never present it as verified recording-level similarity. Deduplicate equivalent artist/album/recording evidence to avoid accidental repeated weight.

Provider-specific IDs, endpoints and response behavior stay in adapters. The plan must verify official API support and usage requirements before implementing new track lookup/preparation. ListenBrainz remains the default and Last.fm remains swappable. Existing evidence and offline fixtures establish mechanics, not live coverage or recommendation quality.

## If every input is skipped

Recommended behavior: allow completion and offer a clearly labelled general discovery selection, with an invitation to add one favorite or react to results. Do not create pretend taste tags or claim the selection is personalized.

Generate the general selection from prepared, eligible public evidence with a documented deterministic rotation policy, no popularity preference, normal history suppression, ownership and job controls. This is a new mode and needs an explicit reason record rather than fabricated tag-overlap explanations. It must not depend on new upstream requests merely because the visitor skipped setup.

If the deployment has no eligible prepared evidence, show an honest empty state with options to add a favorite or return later. Synthetic operation must retain its demo label. General selection does not establish provider licensing, acquisition approval or recommendation quality.

## Architecture, persistence and lifecycle

- **Domain:** stable preference values and pure, versioned selection policy. Keep scoring configurable and explanations tied to stored evidence.
- **Application:** onboarding read/update contracts, optimistic concurrency, vocabulary search, selection orchestration and mode choice through interfaces. No EF or vendor-specific types.
- **DataAccess:** additive migrations and implementations for onboarding state, answers and public vocabulary where persistence is necessary. Reuse listener-profile ownership and coordinated managed writes.
- **Infrastructure:** bounded provider preparation and lookup, normalization and evidence retention behind existing abstractions.
- **Presentation:** optional step navigation, existing auth forms and thin endpoints. Avoid a new frontend framework or a parallel profile store.

Persist the minimum needed: questionnaire version, explicit answer states, broad-taste choice, onboarding progress and input revision. Preserve existing manual selections. Public vocabulary is not private listening history. Do not add permanent per-person search logs.

Include every new private field in guest/account export, deletion, retention and transfer. Registration attaches the guest profile transactionally. Existing-account import must preview conflicts; never silently replace an account's answered preferences with guest skips or unknown values. Rotate guest credentials under existing rules. A stale revision returns a conflict and retains the browser draft.

Schema changes must update managed-storage accounting and migration/backfill handling, including any size change caused by additional columns. Existing completed batches remain readable with their original evidence and policy versions. New UI rollout must not strand existing profiles or unfinished jobs.

## Proposed implementation stages for the subsequent execution plan

These are work packages for review, not authorization to begin implementation. The detailed file/task/check sequence follows approval of this design.

| Stage | Deliverable | Verification boundary |
| --- | --- | --- |
| 1 | Freeze question semantics, preference precedence, initial vocabulary sources and recording evidence contracts | Official-source checks for new provider/data use; focused examples for sparse, conflicting and skipped inputs |
| 2 | Add versioned onboarding state and profile lifecycle integration | Additive migration/backfill, guest/account authorization, revision conflicts, export/deletion/import, storage accounting |
| 3 | Add searchable vocabulary and unlimited saved tag selections | Alias search, pagination, large selections, round-trip export, no keystroke upstream calls, fair acquisition windows |
| 4 | Add end-to-end track favorites | Recording/version disambiguation, immutable snapshots, fallback provenance, provider abstraction and duplicate evidence |
| 5 | Add approved preference policy and empty-input discovery mode | Determinism, policy snapshot compatibility, neutral popularity, unknown vocal evidence, existing benchmark seed sanity checks |
| 6 | Build optional onboarding flow over completed APIs | Guest/login/register, Skip/Back/Edit, draft recovery, keyboard/mobile behavior, returning profiles and first-result feedback |
| 7 | Complete regression evidence and developer handoff | Offline/provider isolation, PostgreSQL lifecycle checks, browser journeys, bounded-resource checks and documentation |

Suggested commit boundaries follow those stages, splitting schema/accounting from consumers when needed. Preliminary effort: roughly 5–8 engineering days for the full scope, excluding external source approval or real-catalog acquisition. Track evidence support and replacing current count/snapshot limits are the main uncertainties; refine the estimate after Stage 1. Entry navigation alone is much smaller but does not complete the requested feature set.

No dependency changes, sub-agents, deployment, push, external messages or live acquisition are proposed. Execution remains sequential unless the developer requests otherwise.

## Acceptance and verification requirements

The later implementation must verify at least these user-visible outcomes:

- Guests and new accounts can reach the same setup; account creation is optional.
- Every question, tag step and favorite step can be skipped, revisited and edited.
- Skipping all inputs leads to labelled general discovery or a truthful empty state.
- Returning users retain their data and are not forced through setup again.
- More than five and more than 200 tags can be saved without a hidden profile-count limit; chunking, scoring and snapshots remain bounded and auditable.
- Search finds aliases and distinguishes recording versions. No upstream request occurs on typing or page viewing.
- Broad taste can coexist with specific interests and never becomes a fake genre signal.
- Every displayed question has a working, tested effect or is removed before release.
- First results support existing positive, negative and already-known feedback.
- Registering preserves guest setup; existing-account import is explicit and respects answer conflicts.
- Export, deletion, expiry, ownership, concurrency, cooldown, admission and historical explanations continue to work.

Implementation verification uses meaningful Domain/Application tests, the repository's PostgreSQL migration and lifecycle checks, and browser journeys. Scoring changes require the existing benchmark seeds. Ordinary offline checks reject outbound HTTP. Measure bounded generation with a large selected vocabulary; do not rely only on a small fixture. Update CHANGELOG, PROGRESS and a focused evidence report for actual shipped behavior, with developer confirmation still recorded separately.

For this planning-only change, verify document consistency, referenced paths and whitespace; product tests are not applicable. Create a focused local documentation commit after those checks. Do not push.

## Review needed

Review the proposed journey, three questions and their evidence requirements, unlimited-tag behavior, track support, and the general-discovery default when all inputs are skipped. In particular, decide whether to keep the vocal question subject to an evidence feasibility check or replace it during Stage 1. Approving this written design permits the detailed implementation plan; product implementation requires the subsequent plan review and execution choice.
