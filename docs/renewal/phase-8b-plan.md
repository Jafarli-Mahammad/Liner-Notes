# Phase 8b executed implementation plan

The developer approved the concrete design, selected 8a then 8b with separate and
combined tests, and explicitly directed immediate implementation. No additional
approval checkpoint was inserted. Local developer confirmation remains pending.

- [x] Add pure UTC slot, validation, ISO-week and seven-day advancement policy.
- [x] Add immutable bounded Synthetic-only schedule options with explicit allowlist.
- [x] Add Application scheduling orchestration over existing generation/capture.
- [x] Add DataAccess stable eligibility reads, conditional initialization/advance and
  dedicated non-pooled scheduler lease; retain existing schema/export.
- [x] Integrate the finite hosted Worker, fresh per-tick scopes, cancellation and safe
  result output while retaining one-shot actions.
- [x] Add actual PostgreSQL contention, preference/eligibility, storage and receipt
  checks; prove competing processes and two publication/commit restart windows.
- [x] Add a disposable-account setup helper and runnable launcher procedure.
- [x] Run separate 8b and combined checks; inspect one fresh final review.
- [x] Record final full-solution results and focused remaining local commits.

Policy/configuration commit: `82c3329`; verified orchestration/QA commit: `0c0f7d7`.
Final results: separate 8a 70, separate 8b 58, combined 121, whole solution 468,
zero failures/skips. See [evidence and rulings](phase-8b-evidence.md).
