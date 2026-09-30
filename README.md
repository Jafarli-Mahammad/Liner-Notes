# Liner Notes 🎵

> **Your taste, explained and owned by you — not a black box optimizing for what's already famous.**

Liner Notes is an open-source web application that emails subscribers a personalized weekly music discovery list. Every pick shows an inspectable, stored record of **why** it was chosen.

---

## 📌 What Liner Notes Does

- **Weekly Music Discovery:** Sends a weekly curated digest of tracks tailored to each subscriber's taste.
- **Explainable Recommendations:** Every recommendation persists a complete mathematical score breakdown (shared tags, similarity signal, penalties, novelty). The "why this pick" explanation is reconstructed directly from stored numbers — never generated text.
- **Anti-Popularity Bias:** Applies an explicit popularity penalty to prevent globally famous megastars from dominating discovery lists.
- **Data Minimization & Provenance:** Stores taste signals as small, atomic records with explicit provenance (seed, like, dislike, import) so users can inspect, export, or delete all data held about them verbatim.
- **Idempotent Delivery:** Enforces that a subscriber receives at most one digest per ISO calendar week.

---

## ⚠️ What We Do NOT Claim

- **No Proprietary Recommendation Engine / No ML:** Liner Notes does **not** run its own recommendation model or machine learning algorithm. We pull candidate tracks and listening metadata from **Last.fm** and **ListenBrainz**.
- **No Claim Over Upstream Data:** We did not build the underlying similarity data. We built the deterministic scoring, filtering, re-ranking, and email delivery layer on top of established open music APIs.

---

## 🏛️ Onion Architecture

Dependencies strictly point inward:

```
src/
├── Domain/           # Pure entities, value objects, and the deterministic scoring engine. Zero external dependencies.
├── Application/      # CQRS commands/queries (MediatR), orchestration logic, and interface definitions.
├── DataAccess/       # EF Core 10 DbContext, Entity Configurations, and Migrations.
├── Infrastructure/   # Last.fm/ListenBrainz HTTP clients, email delivery, in-memory caching.
├── Web/              # Thin ASP.NET Core presentation host (endpoints, auth, DI composition root).
└── Worker/           # Dedicated background worker running the weekly batch pipeline (composition root 2).
tests/
└── Domain.Tests/     # Mandatory unit tests for the pure scorer, benchmark fixture, and architectural boundary tests.
```

---

## 🧮 Pure Recommendation Scorer

The scoring engine is **pure**: no I/O, no randomness, no hidden state. Given identical inputs, it produces identical floating-point scores and rankings every run.

$$ \text{FinalScore} = (W_{\text{tag}} \times S_{\text{tag}}) - (W_{\text{pop}} \times P_{\text{pop}}) + (W_{\text{nov}} \times A_{\text{nov}}) - (W_{\text{fb}} \times P_{\text{fb}}) $$

1. **Tag Overlap Similarity ($S_{\text{tag}}$):** Cosine similarity between the candidate's tag vector and the user's taste vector over weighted, normalized tags.
2. **Popularity Penalty ($P_{\text{pop}}$):** Anti-popularity mechanic penalizing candidates with high global listener counts.
3. **Novelty Adjustment ($A_{\text{nov}}$):** Rewards artists or tracks unfamiliar to the user.
4. **Feedback Adjustment ($P_{\text{fb}}$):** Drastically penalizes items previously rejected or disliked by the user.

All scoring weights ($W$) are runtime configuration parameters (`ScoringParameters`), never hard-coded.

---

## 🛠️ Building & Testing

Target framework: **.NET 10.0**

```bash
# Restore packages
dotnet restore "Liner Notes.sln" --verbosity quiet

# Build solution
dotnet build "Liner Notes.sln" '/clp:NoSummary;ErrorsOnly'

# Run unit tests
dotnet test --logger "console;verbosity=quiet"
```

---

## 🧪 Benchmark Fixture

`tests/Domain.Tests/Fixtures/BenchmarkSeedFixtures.cs` provides a fixed 8-genre benchmark set (Metal, Hip-Hop, Electronic, Jazz, Pop, Indie, Classical, Regional / Anatolian Rock) to sanity-check scoring weights and ensure deterministic rankings across releases.
