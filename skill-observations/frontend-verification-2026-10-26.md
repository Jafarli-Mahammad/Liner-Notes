# Frontend verification observations — 2026-10-26

Supplement to the existing observation log; unrelated log edits were preserved.

- Separate artifact directories for concurrent controlled and live Playwright
  invocations. A passing test can otherwise fail during trace cleanup when
  another invocation clears the shared directory. Final isolated runs passed.
- Scroll to the top after each viewport change, wait for fonts/layout and then
  capture a sticky-header page. Full-page screenshots taken while scrolled can
  place the header midway through the image and misrepresent the rendered layout.
- A browser's screenshot CLI can capture before asynchronous React route/data
  work completes. Native Waterfox BiDi waiting for the actual heading produced
  valid real-route screenshots where the immediate CLI capture was blank.
- Keep contract fixtures exclusively in tests and label their screenshots.
  Separate real provider/API journeys establish integration; controlled images
  establish repeatable comparison against approved artwork and content geometry.
- A documented partial deletion result is a valid API outcome to report, not
  success to conceal or a frontend failure to repair by changing the backend.

No reusable skill package or global memory files were modified.
