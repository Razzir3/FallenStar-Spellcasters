
# FallenStar-Spellcasters — AI Context

## Project
- Engine: Unity
- Language: C#
- Genre: 3D card game
- Repository: FallenStar-Spellcasters

## Development principles
- Inspect the actual implementation before making assumptions.
- Preserve existing field, method, and class names.
- Prefer minimal, focused changes.
- Do not rewrite working systems without a justified requirement.
- Keep architecture documentation synchronized with significant changes.

## Agent responsibilities
- Claude Opus: architecture analysis, task specifications,
  implementation review, corrective specifications.
- GPT/Codex: code implementation, focused tests,
  implementation reports, corrections based on review findings.
- Human developer: approve scope, review changes,
  test in Unity, and accept or reject the result.

## Workflow
1. Claude analyzes the task and relevant code.
2. Claude creates a specification under Documentation/AI/Tasks/.
3. Codex implements the specification.
4. Codex reports changes and actual test results.
5. Claude reviews the actual diff and implementation.
6. Codex fixes confirmed defects if required.
7. The developer verifies the result in Unity.
8. The developer commits the accepted changes.

## Safety
- Never discard user changes.
- Never overwrite uncommitted work.
- Do not modify Unity scenes or prefabs unnecessarily.
- Do not claim tests passed unless they were executed.
- Keep implementation scope limited to the task.

## Current architecture
See:
- Docs/AI/Architecture/Architecture.md
- Docs/AI/Architecture/GameLoop.md
- Docs/AI/Architecture/Dependencies.md
