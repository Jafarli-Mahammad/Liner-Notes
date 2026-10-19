# Optional taste onboarding for guests and accounts

Date: 2026-10-19
Status: Updated with developer decisions on 2026-10-19. Remaining policy and technical details are proposals. Documentation only; implementation is not authorized.

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

## Developer decisions added on 2026-10-19

- A recent-recommendations page must exist for everyone, including guests, whether or not they complete onboarding.
- Visitors who skip every taste input land on that page. Visitors who provide taste inputs see their personalized music first and can open the recent-recommendations page to explore.
- This page contains music previously recommended to other users, with the association to the recipient completely stripped from its feed records and responses. It records only that the music was recommended, not to whom.
- Present the three questions together on one screen, rather than on separate screens. Each answer remains optional; users may skip individual questions or the entire screen.
- “My taste is a soup” means literally “I like everything.” Do not reinterpret it as merely liking several selected styles.
- Let the visitor choose which kind of favorites to enter. Offer Artists / bands, Albums, and Tracks; bands and solo performers share the artist entity type. No one must supply every type.
- Tags have no product selection limit. The proposed searchable catalog of genres/subgenres, moods and descriptive sounds is accepted.
- Returning users resume unfinished setup or their results, and can edit taste later without being forced through onboarding again.
- Album favorites use reduced-influence artist similarity as an explicit, persisted and disclosed fallback. Artist and track favorites use direct provider evidence without that fallback.
- This update authorizes documentation changes only. Do not begin implementation.

The previous five-favorite cap remains an unresolved proposal, not a newly approved limit. Detailed scoring effects, feed lifetime and recording-evidence support still need specification.

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
2. **Questions:** Display all three questions together on one screen. Each question is independently optional; offer Skip all questions and Back. No preselected answer is recorded as consent or preference.
3. **Tags:** Search and browse the accepted rich vocabulary; select any number; inspect/remove selected tags; optionally choose “My taste is a soup — I like everything”; Skip continues.
4. **Favorites:** Let the visitor choose Artists / bands, Albums, or Tracks, with a clear type selector and the option to mix types. Bands and solo artists share the artist search. Encourage three examples; three is guidance, not a minimum, and no particular type is required. The existing five-favorite limit is a proposal still to review. Skip continues.
5. **Review:** Show answers and selections together, with Edit actions. Explain whether there is enough evidence for personalization. Start discovery only on an explicit action.
6. **Results:** With usable taste inputs, show the visitor’s own music and stored explanations first, followed by Like, Not for me and Already know it. With every input skipped, open Recent recommendations instead. Keep a visible Recent recommendations / Explore entry available to everyone, including people with personalized results. Offer account creation after guests have seen value. Weekly email retains its existing consent and verification conditions.

Known returning profiles resume their saved results or unfinished setup. Existing accounts do not repeat onboarding automatically. Editing setup is always available. Temporary drafts entered before login/registration survive the transition, but importing into an existing account remains an explicit action.

Keyboard navigation, focus after step changes, accessible labels, visible errors and readable guest/Skip actions are acceptance criteria. A failed request must preserve current answers. A retry must not create duplicate profiles or jobs.

## Proposed questions and required effects

The developer accepted these three questions together on one screen. Exact recommendation effects still need an explicit, deterministic behavior contract before they ship; do not collect answers that have no useful effect.

| Question | Suggested answers | Intended effect |
| --- | --- | --- |
| What would you like to discover? | New artists; More like my favorites; Different styles; A mixture | A documented discovery policy adjusts candidate sourcing and/or batch selection. “Different styles” uses supported descriptive evidence; it must not become an unbounded random search. |
| How far would you like to explore? | Close to my taste; A balance; Surprise me | Configurable exploration behavior relative to the person's positive evidence, with persisted selection reasons. When there is no positive evidence, disclose that this answer cannot yet define closeness. |
| Any preferences about vocals? | Mostly vocals; Mostly instrumental; Both; No preference | A soft preference only where explicit, provenance-backed descriptive evidence is available. Unknown evidence remains unknown. Never infer the absence of vocals from a missing tag. |

Skipped, explicitly answered No preference, and not yet visited are different states. Answers can be changed or cleared. Store stable answer codes and a questionnaire version; do not store speculative personality classifications.

The vocal question is the main feasibility dependency: this investigation has not verified adequate provider/catalog coverage for vocal information. Before implementation, inspect supported local evidence and verify any external API needed. If coverage cannot support a useful effect, replace or defer that question and explain the change rather than shipping a decorative question. Likewise, goal and exploration choices require a reviewed policy definition; arbitrary numeric multipliers are not approved by this draft.

Discovery goal, exploration, and the soup choice express different intentions. Soup explicitly means all musical styles are welcome. Preserve any selected tags and favorites as positive examples, but do not treat them as boundaries on which styles a soup listener may receive. Goal and exploration answers still convey the requested discovery experience. Soup does not imply liking every individual track. Precise precedence when other answers conflict with soup remains to specify; never silently change the meaning of a saved answer.

## Tags and broad taste

Use a versioned public vocabulary with canonical identifiers, display labels, aliases and categories. Separate genre/style, mood and descriptive sound categories so a broad genre is not presented as equivalent to a listening situation. Normalize aliases without merging genuinely different styles. Search must handle case, common spellings and diacritics consistently with the existing catalog conventions.

Search and category browsing use local data. Typeahead makes no upstream requests. Search results are paged; selected tags are independently searchable/removable. The first screen shows a manageable starter selection rather than the entire vocabulary. A no-match result must not silently create an arbitrary provider query or substitute a different tag.

Vocabulary breadth is a data deliverable: document its initial scope, source, licensing and maintenance. Do not promise an exhaustive taxonomy or treat unsupported tags as having provider coverage. Verify source terms before adding externally acquired vocabulary; do not repurpose held-out evaluation material.

“My taste is a soup” is an explicit “I like everything” choice, with plain explanatory text to match. It can coexist with selected tags and favorites, which remain positive examples rather than genre restrictions. Store this as a distinct all-styles preference, not thousands of automatically selected tags or a fake provider genre. It expresses openness to every kind of music, not a claim that every song will be liked.

Unlimited means no arbitrary selected-tag count in the product. Store selections separately from candidate acquisition windows. Do not merely raise five to 200: current signal, snapshot and request limits would then remain hidden product limits. Use paged/chunked selection operations where necessary and aggregate taste without requiring every selected tag in every recommendation snapshot. Preserve the existing bounded provider requests, queue, candidate count and storage admission controls. Return actionable transport/storage errors while retaining accepted selections.

All saved selections must remain visible and exportable. An explicit, versioned policy chooses a bounded acquisition window, rotating fairly rather than always taking alphabetically first tags. Scoring must have a documented bounded way to incorporate the wider profile. Record the window and materialization version so an explanation is auditable. Validate normalization so selecting many tags does not automatically overwhelm favorite-derived evidence. Any changes to evidence snapshots must preserve explanation provenance and historical snapshots.

## Artist, album and track favorites

Keep a common catalog-selection interaction, with clear type labels and artist credits. Artists offer broad reference points; albums identify a particular body of work; tracks provide precise examples. None is universally the most informative.

For tracks, use a canonical recording identity and distinguish versions, credited artists and available releases. Do not conflate a recording with a release-specific track position. Preserve the selected recording snapshot across catalog refresh/cleanup and account transfer.

Extend search DTOs, catalog import/publication validation, explicit lookup adapters, canonical snapshots, taste storage, preparation and materialization together. Artist and track favorites use their own direct provider evidence; do not substitute credited-artist similarity for a track favorite. If direct evidence is unavailable, record the coverage gap rather than silently changing its scope. Deduplicate equivalent artist/album/recording evidence to avoid accidental repeated weight.

Provider-specific IDs, endpoints and response behavior stay in adapters. The plan must verify official API support and usage requirements before implementing new track lookup/preparation. ListenBrainz remains the default and Last.fm remains swappable. Existing evidence and offline fixtures establish mechanics, not live coverage or recommendation quality.

### Album similarity: explicit artist fallback

Developer instruction recorded on 2026-10-19: ListenBrainz has no release-level similarity endpoint. This provider capability statement was supplied by the developer; it was not independently verified during this documentation-only update. Verify official provider documentation before implementation, as required by the project rules.

When preparing similarity evidence for an album favorite, resolve its credited artist and perform the similarity lookup using that artist. Do not represent this as album-to-album or album-to-track similarity. Album-scoped descriptive tags, if available, remain separate evidence and do not establish direct album similarity.

Persist the original album identity, the artist identity actually queried, the provider and evidence reference, an explicit artist-level fallback indicator/reason, and the effective configured influence. The fallback influence must be configurable, lower than the corresponding direct-evidence influence, and recorded in the recommendation's stored breakdown. Do not hard-code a new multiplier. The exact value and the treatment of albums with multiple credited artists remain implementation-plan details.

The “why this pick” explanation must disclose this fallback from the stored evidence, for example: “Based on artists similar to [artist], the artist behind your favorite album [album]. Album-level similarity was unavailable, so this signal had reduced influence.” Never describe it as verified album-level similarity. Historical explanations must retain the influence and scope used when the recommendation was generated.

Artist and track favorites use direct provider evidence for their respective entity types; no artist-level fallback is needed or authorized for either. A missing direct result is a coverage gap, not permission to relabel artist evidence as track evidence. This requirement stays behind provider abstractions and must be verified for each implemented adapter.

Verification must cover album-to-artist lookup routing, persisted source/target identities and fallback scope, reduced configured influence, explanation disclosure, separation from album descriptive tags, and the absence of fallback for artist/track inputs. Existing benchmark seeds must sanity-check any scoring influence change.

## Recent recommendations for everyone

This replaces the earlier proposed general-discovery generator as the empty-input destination. The page shows music that has actually been recommended to other users, ordered as recent additions to a separate shared feed. It is available to guests and accounts regardless of onboarding completion. Completing setup puts the person's own recommendations first; Explore opens the same shared page.

### Privacy and data boundary

Recipient identity is private. Feed entries and responses must not retain a link to the recipient or originating recommendation: no user/profile/session identifiers, recommendation/batch/job identifiers, source-row foreign keys, recipient counts, source request identifiers, personal explanations, taste tags, ratings, comments, listening history or recipient-specific URLs. Logs, caches and operational metadata for feed publication must not reintroduce those associations.

The feed stores only the public music identity needed to display/play the item and the fact that it was recommended. Resolve titles, artist credits, artwork and links through eligible shared catalog metadata. Do not copy private evidence snapshots. Use an independent feed sequence/order for recency rather than exposing the original recipient's exact recommendation timestamp. The sequence must not encode a source batch or recipient; its implementation remains to specify. The feed API must not expose who received a track or how many people received it.

The existing private recommendation records still serve their owners' history, explanations, feedback, export and deletion. They are not the feed's storage or read model. Stripping recipient associations from the shared page does not authorize removing ownership from private records. Public music identity can overlap with private results, so the design must not claim stronger resistance to inference than has been established.

### Publication and behavior

Publish only after a recommendation batch has successfully completed and its picks have been committed. Use a dedicated sanitized projection containing eligible public music identity, with no originating recipient or recommendation key. The feed read path must use this projection rather than querying across other people's private batches. Publication must be retry-safe and must not leak partial, failed, revoked or ineligible evidence. Deduplicate repeated music; repeated recommendations must not become a recipient counter or popularity ranking.

Use a bounded, paged local read path. Browsing never triggers upstream acquisition. Feed lifetime, recency ordering, publication retries and shared-record cleanup need concrete implementation rules while preserving the no-recipient-link requirement. If someone reacts to a shared track, that new feedback belongs privately to the current visitor, without adding a recipient association to the shared feed record.

If no eligible tracks have been recommended yet, show an honest empty state and invite the visitor to add taste inputs or return later. Do not invent recommendations, fall back silently to random music, or expose synthetic items as real recommendations. Synthetic operation keeps its demo label. This page does not establish live-catalog quality, acquisition permission or production readiness.

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
| 4 | Add end-to-end track favorites | Recording/version disambiguation, immutable snapshots, direct track/artist evidence, album artist-fallback provenance and influence, explanation disclosure, provider abstraction and duplicate evidence |
| 5 | Add approved preference policy and the shared recent-recommendations projection/page | Determinism, soup semantics, neutral popularity, unknown vocal evidence, benchmark seeds, successful-publication boundary, deduplication and absence of recipient links in storage/API/logs |
| 6 | Build optional onboarding with all three questions together and visitor-selected favorite types | Guest/login/register, independent question skipping, Skip/Back/Edit, draft recovery, keyboard/mobile behavior, returning profiles, first-result feedback, skipped-input routing and Explore access for everyone |
| 7 | Complete regression evidence and developer handoff | Offline/provider isolation, PostgreSQL lifecycle checks, browser journeys, bounded-resource checks and documentation |

Suggested commit boundaries follow those stages, splitting schema/accounting from consumers when needed. Preliminary effort: roughly 5–8 engineering days for the full scope, excluding external source approval or real-catalog acquisition. Track evidence support and replacing current count/snapshot limits are the main uncertainties; refine the estimate after Stage 1. Entry navigation alone is much smaller but does not complete the requested feature set.

No dependency changes, sub-agents, deployment, push, external messages or live acquisition are proposed. Execution remains sequential unless the developer requests otherwise.

## Acceptance and verification requirements

The later implementation must verify at least these user-visible outcomes:

- Guests and new accounts can reach the same setup; account creation is optional.
- Every question, tag step and favorite step can be skipped, revisited and edited.
- Skipping all inputs opens Recent recommendations or its truthful empty state. The same page remains reachable by guests and accounts who completed onboarding; their own music is shown first.
- Returning users retain their data and are not forced through setup again.
- More than five and more than 200 tags can be saved without a hidden profile-count limit; chunking, scoring and snapshots remain bounded and auditable.
- Search finds aliases and distinguishes recording versions. No upstream request occurs on typing or page viewing.
- Album favorites query artist similarity with a persisted fallback marker and reduced configured influence; “why this pick” discloses that scope. Artist and track favorites retain direct provider evidence and never silently use that fallback.
- Soup explicitly means “I like everything,” can coexist with specific favorites, and does not become a fake genre signal or genre restriction.
- All three questions appear together with independent optional answers. Visitors choose favorite types; bands and solo artists use the same artist entity.
- Shared recent-recommendation records, responses, caches and publication logs contain no recipient/source-recommendation links or personal explanations. Publication occurs only for completed recommendations and paging makes no upstream calls.
- Every displayed question has a working, tested effect or is removed before release.
- First results support existing positive, negative and already-known feedback.
- Registering preserves guest setup; existing-account import is explicit and respects answer conflicts.
- Export, deletion, expiry, ownership, concurrency, cooldown, admission and historical explanations continue to work.

Implementation verification uses meaningful Domain/Application tests, the repository's PostgreSQL migration and lifecycle checks, and browser journeys. Scoring changes require the existing benchmark seeds. Ordinary offline checks reject outbound HTTP. Measure bounded generation with a large selected vocabulary; do not rely only on a small fixture. Update CHANGELOG, PROGRESS and a focused evidence report for actual shipped behavior, with developer confirmation still recorded separately.

For this planning-only change, verify document consistency, referenced paths and whitespace; product tests are not applicable. Create a focused local documentation commit after those checks. Do not push.

## Remaining design work

The developer decisions above are recorded, not awaiting repeat approval. Specify exact question effects and conflicting-answer precedence, verify recording/vocal evidence support, decide the favorite-count policy, and finalize the anonymous feed's bounded recency, retention and retry mechanics. Reassess the earlier effort estimate after including this shared feed; it is not simply a generic empty-input selection mode.

This is a documentation-only update at the developer's request. No application implementation, provider acquisition or deployment is authorized. The detailed implementation plan and execution choice remain later steps.
