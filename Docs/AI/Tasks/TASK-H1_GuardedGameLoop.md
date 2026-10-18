# TASK-H1: Run the match loop inside an exception boundary

- **Source issue:** [Issue Report #2 → H1](../Reports/2026-10-18_IssueReport-2.md)
- **Severity:** 🟠 High
- **Chosen solution:** **H1-B.** A guarded runner logs the exception and ends the match.
- **Order:** after TASK-H7, because `AbortMatch` relies on the new cleanup. Independent of H4, H8 and H9.
- **Implementer:** Codex

## 1. Objective

An exception anywhere inside the server match loop must never leave the match frozen. It must be logged with its full stack trace, and the match must end in a defined way: everyone returns to the lobby.

## 2. Current behavior

- `GameMaster.StartNewGame` (`GameMaster.cs:449`) runs `m_gameCoroutine = StartCoroutine(GameLoop());`.
- `GameLoop()` (`GameMaster.GameLoop.cs:44`) yields nested enumerators: `RoundBreakPhase()`, `DrawCardPhase()`, `CardSelectionPhase()`, `PlayerTurnPhase()` → `PlayerActionPhase()` → `WaitForRespawnIfNeeded()`.
  - The only `YieldInstruction` used is `WaitForSeconds` in `DrawCardPhase` (`:458`).
  - Nothing in the chain calls `StartCoroutine`. That call exists only at `GameMaster.cs:449`.
- If any of these throws, Unity logs the exception and silently stops the coroutine. The phase NetworkVariable stays frozen, and players see a stuck game with no message.
- Known throw sites: H2, H3, H4, H9, M6, M9.

## 3. Relevant files and dependencies

| File | Member |
|---|---|
| `Assets/Scripts/InGame/GameMaster/GameMaster.GameLoop.cs` | `GameLoop` and all phase coroutines (unchanged); new runner goes here |
| `Assets/Scripts/InGame/GameMaster.cs` | `StartNewGame` (`:365-452`), `m_gameCoroutine` (`:96`), `ClearupAfterDisconnect` (`:301`, changed by H7) |
| `Assets/Scripts/MultiplayerScripts/MultiplayerManager.cs` | `Instance` (`:59`), `public async void Disconnect()` (`:291`) → `ShutdownHostInternal` (`:338`) → `ClearupAfterDisconnect` → `NetworkManager.Shutdown()`, then `CleanupAfterDisconnect` (`:1193`) → `LoadLobbyScene` |

## 4. Required changes

1. **Guarded runner.** Add `private IEnumerator RunGuarded(IEnumerator root)` in `GameMaster.GameLoop.cs`.
   - It keeps a `Stack<IEnumerator>`, starting with `root`.
   - **Each step:** call `top.MoveNext()` inside `try { } catch (System.Exception e)`.
     - C# does not allow `yield` inside a `try` that has a `catch`, so store the result and `Current` in locals, and yield **after** the `try` block.
   - **When `MoveNext()` returns false:** pop the stack. When the stack is empty, set `m_gameCoroutine = null` and `yield break`.
   - **When `Current` is an `IEnumerator`:** push it and continue without yielding, so the child's first step runs in the same frame. That matches Unity's nested-coroutine behaviour.
   - **Any other `Current`** (`null`, `WaitForSeconds`, …): `yield return` it to Unity unchanged.
   - **On exception:**
     - `Debug.LogException(e)`
     - `Debug.LogError($"GameMaster: game loop aborted by exception in phase {m_gamePhase.Value}.")`
     - `AbortMatch("loop_exception")`
     - `yield break`
2. **Start through the runner.** In `StartNewGame`, replace `StartCoroutine(GameLoop())` with `StartCoroutine(RunGuarded(GameLoop()))`. `m_gameCoroutine` keeps referring to the outer runner coroutine.
3. **`AbortMatch(string reason)`.** Add it as a new `public` server method in `GameMaster.cs`.
   - If `!IsServer`, return. If an abort is already in progress (new private `bool m_isAbortingMatch`), return.
   - Set `m_isAbortingMatch = true` and `m_gamePhase.Value = GamePhase.None`. Setting the phase blocks all player input through `CanPlayerMove`, `CanPlayerSelectCards` and `HandleCardControlPackage`.
   - Log `Debug.LogError("GameMaster: match aborted. Reason: " + reason)`.
   - Call `MultiplayerManager.Instance.Disconnect()` if `Instance != null`. On the host this shuts the session down; clients follow the existing "lost connection to host" path back to the lobby.
   - Reset `m_isAbortingMatch = false` at the start of `StartNewGame`.
4. Don't call `StopCoroutine` on the runner from inside itself. The runner leaves with `yield break`.

## 5. Explicit non-goals

- Fixing the individual throw sites: H2, H3, H4, H9, M6, M9 (separate tasks).
- Continuing the match after an error (option H1-C was rejected).
- A result or error screen, or new localization keys. Players simply return to the lobby. A message can be added once H5 exists (see §10).
- Changing any phase coroutine body.

## 6. Constraints and compatibility

- No renames (`AGENTS.md`). Only additive members, plus the one-line change in `StartNewGame`.
- Behaviour without exceptions must be **identical**, including the frame timing of each phase. In particular, the runner must not add an extra `yield return null` when it enters a child enumerator.
- Server-only. Clients never run the loop.

## 7. Acceptance criteria

- [ ] A normal match runs exactly as before: phases, timers, draw delays, turn order.
- [ ] An exception thrown anywhere in the loop chain produces:
  - [ ] one `LogException` with the original stack trace
  - [ ] one "match aborted" error log
  - [ ] the host back in the lobby, and the client back in the lobby through its disconnect path
  - [ ] no further loop activity
- [ ] A second exception or `AbortMatch` call during an abort has no effect.
- [ ] A new match can be started after an abort.
- [ ] Compiles without new warnings.

## 8. Regression tests

No automated test infrastructure exists. **Optional EditMode test:** `RunGuarded` can be exercised without NGO by feeding it a hand-made nested `IEnumerator` that throws at depth 3, and checking it stops and reports. That needs `AbortMatch` behind a virtual or delegate seam, so it is only for Codex to decide.

Review checklist:
- no `yield` inside a `try` that has a `catch`
- nested enumerators are pushed, not yielded to Unity
- `YieldInstruction`s pass through unchanged

## 9. Manual Unity Editor checks

1. ParrelSync host and client. Play a full round. Behaviour is unchanged.
2. **Temporary fault injection (don't commit):**
   - add `throw new System.Exception("test");` at the top of the `CardSelectionPhase()` loop
   - start a match
   - expected: the exception and the "aborted" error appear in the host Console, and both instances end up in the lobby
   - revert
3. Repeat with the throw inside `WaitForRespawnIfNeeded` (depth 4) to prove nested coverage.
4. After an abort, host a new lobby and start a new match from the same Editor session. It must start normally.

## 10. Open questions and assumptions

- **Q1:** once H5 exists, should `AbortMatch` show a short "match ended because of an error" message (en/ru/ja) instead of returning to the lobby silently? Not part of this task.
- **A1:** `MultiplayerManager.Disconnect()` on the host is safe to call from a coroutine. It is `async void`, so its first await (`DeleteLobbyAsync`) yields. Codex should confirm that no exception escapes from it, because it already has a catch-all (`:331`).
- **A2:** the H7 cleanup stops `m_gameCoroutine`. That is harmless here, because the runner has already exited.
