# Frontend verification observations — 2026-11-01

**Status:** OPEN
**Type:** open-source
**Skills:** emil-design-eng; task-observer

**Issue:** Empty-feed integration tests masked an assertion that rejected legitimate
artwork HEAD requests. Lazy-image checks also queried images before offscreen
frames had entered the viewport.

**Suggested improvement:** Exercise populated and empty real-response paths.
Validate request purpose, method and origin together. Scroll stable artwork
containers before checking lazily created image elements, and separately test
pending, definitive-unavailable, invalid-byte and stalled-download states.

**Principle:** Verify asynchronous interfaces through their state transitions and
network contracts, including states a convenient dataset does not reach.
