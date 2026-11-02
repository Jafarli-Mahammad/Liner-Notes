# Taste-input trace audit

### Observation 040: Trace preference composition beyond the score formula

**Status:** OPEN
**Date:** 2026-11-02
**Skill:** ponytail; task-observer
**Type:** open-source
**Issue:** A preference can survive transport and persistence and still have no effect because an earlier ranking key always decides first. Numerical score replay does not prove that final selection can be reconstructed, and a rotating input window is different from using every saved input in each result.
**Suggested improvement:** For input-coverage reviews, map each control through submission, persistence, acquisition, materialization, scoring, selection and explanation. Check conflicting combinations and neutral values, distinguish per-result coverage from eventual window coverage, and report numerical replay separately from selection replay. Verify the active scorer and composition root before interpreting legacy implementations.
**Principle:** Input persistence, numerical influence and final-choice influence require separate evidence.
