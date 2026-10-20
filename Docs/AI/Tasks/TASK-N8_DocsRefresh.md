# TASK-N8: Bring the architecture docs and the README up to date

- **Source issue:** [Issue Report #7 → N8](../Reports/2026-10-20_IssueReport-7.md#-info)
- **Severity:** ⚪ Info (Confirmed)
- **Baseline:** `b7e7b91`
- **Order:** 20 of 20. **Last:** the other tasks change what the docs must describe.
- **Implementer:** **Claude** for `Docs/AI/Architecture/*` (documentation is Claude's job per `CLAUDE.md`). `README.md` is outside `Docs/AI`: Claude edits it only after the owner says go. Codex keeps its usual per-task doc updates.

## 1. Objective

The three architecture documents and the README describe the game as it is after this round of tasks. There are no stale "not implemented" entries, and the systems added since H1 are covered.

## 2. Current behavior (on `b7e7b91`)

- `Docs/AI/Architecture/GameLoop.md:45`: "End of match → Result | Not implemented". It has been implemented since H5 (`ad3a46f`, `d44b882`).
- `GameLoop.md:266` still lists the old L8 row about `m_isLocalPlayerLost`. That field is removed by TASK-L3.
- `RunGuarded`, `LeaveMatchToResultScene` and `SuspiciousActivityLog` have few or no mentions in the architecture docs. Nothing yet describes:
  - the hand limit (M15)
  - `SettingsScene` (M12)
  - the countdown (TASK-L1)
  - the phase-tagged confirm (TASK-N21)
  - the selection phase ID (TASK-N26)
  - the replicated input lock (TASK-N3)
  - `DebugModeGuard` (TASK-L9)
  - the font rule (TASK-N28)
- `README.md:24` says the target is "Defeat enemy player character 3 times **or destroy all enemy blocks**". The block rule isn't implemented directly: a player without blocks is eliminated on their next death (feature gap).
- `README.md:19` mentions ParrelSync. The shared `Editor.log` behaviour isn't documented anywhere outside Claude's memory.
- `test.md` at the root is deleted by TASK-L10.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Docs/AI/Architecture/GameLoop.md` | Phases with countdowns, death pause and resume, end of match, hand limit, confirm tagging, selection ID |
| `Docs/AI/Architecture/Architecture.md` | Scenes (incl. Settings), runtime managers, `RunGuarded`, the result flow, suspicious-activity logging, the debug-mode guard |
| `Docs/AI/Architecture/Dependencies.md` | VisualScripting removed; the NGO, Lobby and Relay versions; Localization tables; the font asset rule |
| `README.md` | The win rule as implemented; one line on testing with ParrelSync (separate `-logFile` for the clone) |

## 4. Required changes

1. Read the three documents in full, then the final code after tasks 1–19.
2. Fix every stale statement: the ones listed in §2 plus any found while reading. Each statement must point to a file (and a method where useful).
3. Add the missing sections, briefly. Prefer tables and short lists, in the style of the existing docs.
4. `README.md` (after the owner's go-ahead):
   - the win rule: lose all lives, or have no block to respawn on
   - the testing note
5. Keep the history notes (N14, M10 and others) that are still true. Move those that describe removed code to a short "History" list, or delete them.

## 5. Explicit non-goals

- No code changes.
- No new design documents.
- No rewrite of `AGENTS.md`, `CLAUDE.md` or `AI_CONTEXT.md`, except what TASK-N28 adds to `AGENTS.md`.

## 6. Constraints and compatibility requirements

- Separate what's confirmed in code from what's planned, using the `[Unfinished]` marker the docs already use.
- Line numbers age quickly. Prefer file and method names.

## 7. Acceptance criteria

1. No statement in the three docs contradicts the code at the commit where this task is done.
2. Every system listed in §2 is described in at least one document.
3. The README win rule matches `GameMaster.IsGameFinished` and the respawn rules.

## 8. Regression tests

- None (documentation).

## 9. Manual Unity Editor checks

- None.

## 10. Open questions and assumptions

- The README edit needs the owner's go-ahead (it's outside `Docs/AI`).
