# Claude Code project guidance

Use the repository diagnostics scripts documented in [`diagnostics/README.md`](diagnostics/README.md). Targeted tests are the default for ordinary changes. Use coverage for coverage questions, CPU profiling for attribution, memory tools for allocation or retention investigations, and full benchmarks only for performance comparisons. Keep before/after runs comparable. Never attach without a user-provided PID, kill production processes, collect sensitive dumps without authorization, or silently alter application behavior for profiling.
