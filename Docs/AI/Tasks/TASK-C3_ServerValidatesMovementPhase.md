# TASK-C3: The server must check turn and phase before applying movement

- **Source issue:** [Issue Report C3](../Reports/2026-10-18_IssueReport.md#-critical) (also partly covers M5)
- **Severity:** 🔴 Critical
- **Recommended order:** C5 → C6 → C1 → C2 → **C3** → C4. **Depends on C2**: the `ClientId` must already be the trusted sender ID.
- **Implementer:** Codex

## 1. Objective

The server applies a movement package only if the sending player is allowed to move at the moment of processing, as decided by the existing `GameMaster.CanPlayerMove(PlayerSession)`.

## 2. Current behavior

- `GameMaster.CanPlayerMove` (`GameMaster.GameLoop.cs:14`) returns true only when **all** of these hold:
  - phase is `PlayerAction`
  - the sender is `m_activePlayer`
  - `!m_isResolvingCards`
  - `!m_isWaitingRespawn`
- It's called **only on the client** (`PlayerController.SendMovementInput`, `PlayerController.cs:203`).
- Server `InputQueueUpdate` (`GameMaster.Processing.cs:7-63`) moves the character of any connected client in any phase: card selection, the opponent's turn, a respawn wait, or a round break.
- Also, `InputQueueUpdate` line 47 dereferences `_client.PlayerObject` without a null check. `PlayerObject` is null while the character is dead (it's despawned in `UpdatePlayerHP` → `DespawnCharacter`), so an in-flight package throws a `NullReferenceException` inside `Update`.

## 3. Relevant files and dependencies

| File | Member | Role |
|---|---|---|
| `Assets/Scripts/InGame/GameMaster/GameMaster.Processing.cs` | `InputQueueUpdate` | The place to change |
| `Assets/Scripts/InGame/GameMaster/GameMaster.GameLoop.cs` | `CanPlayerMove` | Existing rule (reuse, don't change) |
| `Assets/Scripts/MultiplayerScripts/SessionManager.cs` | `GetPlayerSession(ulong clientId)` | Session lookup by client ID |
| `Assets/Scripts/InGame/PlayerSession.cs` | `GetCharacter()` | Current character |
| `Assets/Scripts/InGame/PlayerController.cs` | `GetSession()` | Back-reference |

Execution context: `InputQueueUpdate` runs from `GameMaster.Update` on the host only (`if (!IsHost) return;`).

## 4. Required changes

In `InputQueueUpdate`, for each dequeued package, after the existing `ConnectedClients.ContainsKey` check:

1. Resolve `PlayerSession _session = SessionManager.Instance != null ? SessionManager.Instance.GetPlayerSession(_package.ClientId) : null;`
2. If `_session == null`, `continue` (with a debug log under `m_debugMode`).
3. If `!CanPlayerMove(_session)`, `continue`. Log a warning only under `m_debugMode`, because a normal client never sends these.
4. Resolve the controller from the session, not from `PlayerObject`: `PlayerController _playerController = _session.GetCharacter();`. If null, `continue`. This removes the `PlayerObject` null dereference (M5).
5. Optional consistency guard: if `_playerController.OwnerClientId != _package.ClientId`, `continue`.
6. Keep the existing calls `MoveInputProcessing(...)`, `HPDeltaProcessing(...)` (as guarded by C1) and `SendToClientRpc(...)` unchanged in order and arguments.

## 5. Explicit non-goals

- Changing `CanPlayerMove` or the client-side check (keep it for UX).
- Movement bounds, the "can't step off your own blocks" rule, or collision rules. They don't exist today, and adding them would change gameplay.
- `IsHost` vs `IsServer` (M4).
- Rate-limiting input.

## 6. Constraints and compatibility

- No renames (`AGENTS.md`).
- Don't change the `CanPlayerMove` semantics. `CanPlayerSelectCardTargets` has the same body; don't merge them.
- Legitimate behavior must stay identical for an honest client.
- Respect the server-only context. Don't add client-side calls.

## 7. Acceptance criteria

- [ ] A package is applied only when `CanPlayerMove(senderSession)` is true at processing time.
- [ ] Packages that arrive during `RoundBreak`, `CardSelection`, `RespawnWait`, while resolving cards, or on the opponent's turn are dropped without exceptions.
- [ ] No `NullReferenceException` when a package arrives while the sender's character is despawned.
- [ ] The active player moves exactly as before.
- [ ] Compiles without new warnings.

## 8. Regression tests

There's no automated test infrastructure in the project. Code review checklist:
- the session lookup goes through `SessionManager.GetPlayerSession`
- `CanPlayerMove` is called on the server
- the controller comes from `GetCharacter()`
- no change to `MoveInputProcessing`

## 9. Manual Unity Editor checks

1. ParrelSync, host and client. Start a match.
2. Honest play: in each player's `PlayerAction`, WASD moves the active pony. Otherwise nothing moves.
3. **Bypass check (temporary, don't commit):** on the client, comment out the `CanPlayerMove` condition in `SendMovementInput`. Press W during:
   - `RoundBreak`
   - `CardSelection`
   - the host's turn
   - the client's own `RespawnWait`

   Expected: the client's pony doesn't move, and there are no exceptions in the host Console. Revert.
4. Kill the active player (lava or Iceshard) and press W rapidly at the moment of death. Expected: no NRE in the host Console.

## 10. Open questions and assumptions

- **A1:** The check is evaluated at **processing time** (the next host `Update`), not at receive time. A press sent in the last frame of a turn may be dropped. That's acceptable.
- **A2:** `SessionManager.m_playerSessions` is keyed by client ID on the server (`SessionManager.CreateSession`). Verified in code.
