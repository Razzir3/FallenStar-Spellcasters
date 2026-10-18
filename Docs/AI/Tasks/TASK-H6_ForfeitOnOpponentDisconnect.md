# TASK-H6: The remaining player wins by forfeit when the opponent disconnects mid-match

- **Source issue:** [Issue Report #2 → H6](../Reports/2026-10-18_IssueReport-2.md)
- **Severity:** 🟠 High
- **Chosen solution:** **H6-B (forfeit win).** The reconnect alternative (H6-C) is researched separately in [RESEARCH-H6C](../Research/RESEARCH-H6C_ReconnectMidMatch.md).
- **Depends on:** [TASK-H5](TASK-H5_EndOfMatchResultScene.md), for `EndMatch`, `MatchEndReason`, `GamePhase.GameOver` and the ResultScene. Also H1 (the runner in `m_gameCoroutine`) and H7 (cleanup).
- **Order:** H7 → H1 → H5 → **H6** (H10 can be done in parallel)
- **Implementer:** Codex
- **Owner decisions (2026-10-18):**
  - The departed player's pony and blocks may disappear (NGO destroys them with the owner).
  - The remaining player goes to the ResultScene.
  - The result text is shown by TASK-H10 in en/ru/ja. Its key `result_reason_opponentLeft` is defined there.

## 1. Objective

When a remote client disconnects during a running match, the server must:
- stop the match immediately
- remove the departed player from the match state
- call `EndMatch(remainingPlayer, MatchEndReason.OpponentDisconnected)`

The remaining player then goes through the normal H5 flow into the ResultScene as the winner. The match must never wait for, respawn, or touch the departed player.

## 2. Current behavior

1. NGO raises `OnClientDisconnectCallback` on the server. Two handlers run, in subscription order:
   - `MultiplayerManager.OnClientDisconnected`: logs "Remote client disconnected. Host continue" (`MultiplayerManager.cs:232`). This was seen at runtime in `Editor.log:4385`.
   - `SessionManager.OnClientDisconnected` (`SessionManager.cs:156-192`): frees the game slot, removes the `SessionData`, calls `PlayerUI.ClearLocalUI()`, despawns the session and removes it from `m_playerSessions`.
2. Before those handlers, NGO itself despawns and destroys the departed client's **player object (character)** and **every object it owns**: its blocks (`SpawnWithOwnership`, `GameMaster.Spawn.cs:274`) and its `PlayerSession`. All prefabs have `DontDestroyWithOwner: 0`. Verified in the NGO 2.11.2 source, `NetworkConnectionManager.cs:1357-1405`.
3. `GameMaster` is never told. `m_players`, `m_turnOrder` and `m_turnSelections` still hold the destroyed session.
4. The loop then sees "no character", so `IsAnyPlayerDead` (`GameLoop.cs:630`) is true. It enters `RespawnWait` for a client that no longer exists. When that times out, it calls `RespawnCharacter` → `SpawnAsPlayerObject(goneClientId)`, which is expected to throw (H1). Otherwise the lives are set to −1 and the loop ends.
5. A crashed or unplugged client is only detected after `m_DisconnectTimeoutMS: 30000` (`NetworkManagerPrefab.prefab:98`). A graceful quit is detected immediately.

## 3. Relevant files and dependencies

| File | Member | Role |
|---|---|---|
| `Assets/Scripts/MultiplayerScripts/SessionManager.cs` | `OnClientDisconnected` | Hook point. Must notify GameMaster **before** it changes its own state. |
| `Assets/Scripts/InGame/GameMaster/GameMaster.Player.cs` | new `HandlePlayerLeft` | Forfeit logic |
| `Assets/Scripts/InGame/GameMaster.cs` | `m_players`, `m_turnOrder`, `m_turnSelections`, `m_inputQueue`, `m_cardInputQueue`, `m_gameCoroutine`, `m_activePlayer`, `m_waitingRespawnPlayer` | Match state |
| `Assets/Scripts/InGame/GameMaster/GameMaster.GameLoop.cs` | `PlayerTurnPhase` (iterates `m_turnOrder` with `foreach`) | The loop must be stopped before the lists are changed |
| H5 | `EndMatch(PlayerSession, MatchEndReason)`, `GamePhase.GameOver`, `MultiplayerManager.LeaveMatchToResultScene` | Game over and scene change |
| H1 | `AbortMatch(string)` | Fallback when no winner can be found |

## 4. Required changes

1. **New server method** `public void HandlePlayerLeft(PlayerSession session)` in `GameMaster.Player.cs`:
   1. Return if `!IsServer` or `session == null`.
   2. Return if no match is running: `m_gameCoroutine == null`, or `m_gamePhase.Value` is `None` or `GameOver`. This also covers clients leaving on their own after a normal match end (H5 step 4.3.7).
   3. Return if `session` isn't in `m_players`.
   4. **Stop the loop first:** `StopCoroutine(m_gameCoroutine); m_gameCoroutine = null;`. Do this before any list changes, because `PlayerTurnPhase` uses `foreach` over `m_turnOrder`.
   5. Remove `session` from `m_players`, `m_turnOrder` and `m_turnSelections`. Set `m_isResolvingCards` and `m_isWaitingRespawn` to `false`.
   6. Winner = the remaining entry in `m_players` with `TeamId.Value != -1`. There is exactly one in a 2-player match. If none, call `AbortMatch("player_left_no_winner")` (H1) and return.
   7. Call `EndMatch(winner, MatchEndReason.OpponentDisconnected)` (H5). That handles the phase, queues, references, the RPC and the scene change.
   8. Log it under `m_debugMode`.
2. **Hook:** at the start of `SessionManager.OnClientDisconnected`, before the slot, list and despawn code:
   ```csharp
   if (IsServer && GameMaster.Instance != null &&
       m_playerSessions.TryGetValue(clientId, out PlayerSession _leaving) && _leaving != null)
   {
       GameMaster.Instance.HandlePlayerLeft(_leaving);
   }
   ```
   NGO may already have despawned the session's `NetworkObject` at this point. `HandlePlayerLeft` must only use the C# reference, for list removal by reference. It must not call anything on the session that needs it to be spawned.
3. No new localization keys. The reason text `result_reason_opponentLeft` (en/ru/ja) belongs to TASK-H10.

## 5. Explicit non-goals

- The host disconnecting. The session ends for everyone, and clients return to the lobby through the existing `HandleLostConnectionToHost` path. NGO has no host migration.
- Reconnecting (H6-C), a grace period, or a pause.
- Keeping the departed player's character or blocks visible (accepted by the owner).
- Changing `MultiplayerManager.OnClientDisconnected` beyond what H5 adds.
- Lowering `DisconnectTimeoutMS`.

## 6. Constraints and compatibility

- No renames. Only additive members, plus one guarded call in `SessionManager.OnClientDisconnected`.
- Server-only logic. `SessionManager.OnClientDisconnected` also runs on clients, so the hook stays inside `IsServer`.
- Must change nothing when no match is running (lobby), or after `GameOver`.

## 7. Acceptance criteria

- [ ] In every phase (RoundBreak, DrawCards, CardSelection, PlayerAction for either player, RespawnWait for either player, card resolution), a remote client disconnect sends the host straight to `ResultScene`. With debug mode on, it logs "You won. Reason: OpponentDisconnected".
- [ ] No `RespawnWait` for the departed player, no `SpawnAsPlayerObject` for a gone client ID, and no exceptions in the host Console.
- [ ] A disconnect in the lobby, or a client leaving after a normal `GameOver` (H5 flow), triggers no forfeit.
- [ ] The lobby is deleted by the host's `LeaveMatchToResultScene` (H5).
- [ ] Compiles without new warnings.

## 8. Regression tests

No automated tests. Review checklist:
- the coroutine is stopped before the lists are changed
- the hook runs before `SessionManager` changes its own state
- the `IsServer` guard is in place
- the phase guard includes `GameOver`
- nothing is called on the despawned session that needs it to be spawned

## 9. Manual Unity Editor checks

1. ParrelSync host and client. Close the **client** window, or press Stop in the clone, in each of these phases:
   - RoundBreak
   - CardSelection
   - host's PlayerAction
   - client's PlayerAction
   - client RespawnWait (kill the client pony in lava first)

   Each time, the host goes to `ResultScene` with the win line in the Console.
2. Hard drop (kill the clone process instead of a clean stop): the host reaches `ResultScene` after about 30 s, the transport timeout. Expected.
3. Disconnect in the lobby before the match starts: no result, no errors.
4. With H10 merged: the host shows Victory with "Your opponent left the match." in en, ru and ja.

## 10. Open questions and assumptions

- **A1:** a 2-player match only (`m_maxLobbyPlayers = 2`).
- **A2:** when the host is the one who leaves, the client takes the existing lobby path (non-goal). This is unchanged by H5. The client never reached `GameOver`, so H5's protection doesn't apply.
