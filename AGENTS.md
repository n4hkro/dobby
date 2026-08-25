# Agent Instructions

## Quality First

During development, do not choose shortcuts merely to save tokens, tool calls, or elapsed time. The user's standing preference is to prioritize correctness, reliability, and complete verification over speed.

Apply this especially when implementing or reviewing product behavior:

- Investigate root causes before proposing or applying fixes.
- Prefer deterministic tests and explicit synchronization over timing guesses.
- Add or update meaningful tests for behavior changes and bug fixes.
- Run the strongest relevant no-restore verification that local assets allow.
- After development changes, refresh the portable test build so the user can try the latest state; prefer `.\tools\publish-portable\publish-portable.ps1 -NoRestore` when local restore assets allow it, and report the executable path.
- Do not mark work complete while known defects, flaky verification, or unverified assumptions remain.
- If more time or context is needed, state that clearly and continue methodically instead of narrowing scope for convenience.
- For flaky WPF/STA/Dispatcher tests, first rule out test harness state leakage before blaming production logic: Window.Close/dispatcher cleanup, pending action reset, shared/static state, test ordering, and explicit completion signals must be checked.
- Do not write behavioral fixes for flakes unless the current failure reproduces. If it does not reproduce, preserve evidence, add guardrails, and avoid speculative changes.

This instruction does not override safety, security, repository constraints, or explicit user instructions for a specific turn. It means that, within those constraints, quality and verification win over saving time or tokens.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

When the user types `/graphify`, use the installed graphify skill or instructions before doing anything else.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- Dirty graphify-out/ files are expected after hooks or incremental updates; dirty graph files are not a reason to skip graphify. Only skip graphify if the task is about stale or incorrect graph output, or the user explicitly says not to use it.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
