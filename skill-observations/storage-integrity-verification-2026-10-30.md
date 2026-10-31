# Performance verification observation — 2026-10-30

Status: OPEN
Skill: performance-reviewer

A mixed storage benchmark aborted on a legitimate baseline admission failure. Timing only successful complete runs would have hidden the failure and biased comparison. The harness now records bounded failure reasons alongside successful latency distributions, retains the failed attempt, and reruns only when a correction or unresolved cause warrants it.

Suggested skill improvement: require performance comparisons to report attempted/successful/failed operation counts and distinguish an isolated component improvement from end-to-end throughput, reliability and small-workload regressions. Include an explicit fallback workload when an optimization depends on an optional kernel capability.

No reusable skill was changed during this task.

Upgrade-path follow-up: a fresh migrated fixture did not catch the existing local database missing new migrations. Add real process checks for old schemas and uninitialized witnesses, including bounded exit status and no server binding. Operator helpers should disable core dumps and catch failures before handling private configuration. Validate backup archives by consuming their contents; listing a streamed archive can exit early and break the input pipe.
