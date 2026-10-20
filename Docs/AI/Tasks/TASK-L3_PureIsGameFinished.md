# TASK-L3: Make `IsGameFinished()` side-effect-free and allocation-free

- **Source issue:** [Issue Report #7 → L3](../Reports/2026-10-20_IssueReport-7.md#-low-23)
- **Severity:** 🟢 Low (Confirmed)
- **Baseline:** `b7e7b91`. Line numbers below are from this commit.
- **Order:** 6 of 20. After TASK-N6 (same file). Keep N6's change.
- **Implementer:** Codex

## 1. Objective

`IsGameFinished()`, which the match loop calls several times per frame, only answers the question. It writes no state and allocates nothing. The dead "lost player" fields go.

## 2. Current behavior

`InGame/GameMaster/GameMaster.GameLoop.cs`:
- `IsGameFinished()` (`:793-828`):
  - builds a new `List<PlayerSession>` each call (`:796`)
  - counts players who have a character or lives left
  - then calls `FindLostPlayer()` (`:826`)
- `FindLostPlayer()` (`:614-630`), for any player with `m_lifeCurrent <= 0`:
  - sets `m_lostClientId` and `m_isLocalPlayerLost`
  - sets `m_externalDeathDetected = true`
  - reads `SessionManager.Instance.LocalPlayer` **without** a null check on `Instance`, which is a latent NRE
- `m_lostClientId` and `m_isLocalPlayerLost` (`GameMaster.cs:124`, `:126`) are **never read**.
- The `m_externalDeathDetected = true` written there has no effect:
  - every reader is `if (m_externalDeathDetected || IsAnyPlayerDead())` → `HandleDeathPausingCurrentPhase()`
  - that method clears the flag and returns at once unless `IsAnyPlayerDead()` (`:583-586`)
  - a player at 0 lives also has no character, so `IsAnyPlayerDead()` is already true for them
  - and `IsGameFinished()` is checked first in every loop anyway
- The real external-death signal is `GameMaster.Processing.cs:404` (`m_externalDeathDetected = true` on a character death). It stays.
- `FindDeadPlayer()` (`:861-880`) also allocates a list per call. It's called from `HandleDeathPausingCurrentPhase` and `WaitForRespawnIfNeeded`, less often.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/GameMaster/GameMaster.GameLoop.cs` | Rewrite `IsGameFinished` and `FindDeadPlayer` without lists; delete `FindLostPlayer` |
| `Assets/Scripts/InGame/GameMaster.cs` | Delete `m_isLocalPlayerLost` (`:124`) and `m_lostClientId` (`:126`) |

## 4. Required changes

1. **`IsGameFinished()`:** one `foreach` over `m_players`. Skip a null entry and `TeamId.Value == -1`. Count a player when `GetCharacter() != null || m_lifeCurrent.Value > 0`. Return `count <= 1`. No list, no `FindLostPlayer()` call.
2. Delete `FindLostPlayer()` and its XML comment.
3. Delete the fields `m_isLocalPlayerLost` and `m_lostClientId`, and any reset of them (grep: none besides the declarations).
4. **`FindDeadPlayer()`:** the same single loop, returning the first player with `TeamId != -1 && GetCharacter() == null && m_lifeCurrent.Value > 0`. Same order as today (`m_players` order).
5. **`IsAnyPlayerDead()`** (`:777-788`): add a null check for entries (`p == null → continue`), the same as the other two. Behaviour is otherwise unchanged.

## 5. Explicit non-goals

- No change to the win rule or to `GameLoop`'s end-of-match winner selection (`:114-127`).
- No change to `Processing.cs:404`.
- `GetCharacter()` is TASK-N7. Don't change it here.

## 6. Constraints and compatibility requirements

- Results must be identical for every state: same count rule, same skip of `TeamId == -1`.
- Server-only; no network changes.

## 7. Acceptance criteria

1. `IsGameFinished()` and `FindDeadPlayer()` contain no `new` and no LINQ.
2. `grep -rn "FindLostPlayer\|m_lostClientId\|m_isLocalPlayerLost" Assets/Scripts` finds nothing.
3. Match outcomes are unchanged:
   - a last-life death ends the match at once
   - a death with lives left gives the respawn pause
   - a death with no respawn blocks ends the match (N18_M1 / C2 behaviour)

## 8. Regression tests

- A harness test with session doubles. `IsGameFinished` returns the same values as the old implementation for:
  - both players alive
  - one dead with lives left
  - one at 0 lives
  - one with `TeamId -1`
  - a null entry
- Roslyn compile. Exit 0.

## 9. Manual Unity Editor checks

1. Two players, a lava death with lives left: the respawn pause, then resume. Same as A2–A5.
2. A last-life death: straight to the result screen on both sides. Same as A6.
3. The Profiler (optional): no per-frame GC allocation from `IsGameFinished` during a phase loop.

## 10. Open questions and assumptions

- None.
