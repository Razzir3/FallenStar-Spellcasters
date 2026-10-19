# TASK-N14: Return the host to the lobby when the match can't start

- **Source issue:** [Issue Report #5 → N14](../Reports/2026-10-19_IssueReport-5.md#-medium-still-open-code-unchanged), first reported in [#4](../Reports/2026-10-18_IssueReport-4.md#2-new-findings)
- **Severity:** 🟡 Medium
- **Order:** 3 of 5 (H3 → H2 → **N14** → N18_M1 → N19)
- **Implementer:** Codex

## 1. Objective

If the match can't start after `GameScene` loads, the host goes back to the lobby through the existing disconnect path instead of staying in an empty `GameScene` forever. Causes include:
- the opponent leaves or crashes during loading
- the opponent fails to load the scene
- the player sessions or `GameMaster` don't appear

## 2. Current behavior

Start sequence (server only), all in `Assets/Scripts/MultiplayerScripts/MultiplayerManager.cs`:

1. `OnLoadEventCompleted` (`:1178-1224`, `async void`) runs on every peer when the NGO load event completes. On the server, for `GameScene`, it awaits `Bootstrap.RecreateGameMaster()`, checks `GameMaster.Instance`, then starts `StartGameAfterSceneReady`. **It ignores `clientTimeOut`**, the clients that failed to load the scene.
2. `StartGameAfterSceneReady` (`:1225-1267`) waits up to 10 s for `SessionManager.Instance`, then up to 10 s for `m_playerSessions.Count >= 2`. **On either timeout it logs an error and `yield break`s. Nothing else happens.** Then it calls `GameMaster.SpawnPlayers()` and `GameMaster.StartNewGame()` in the same frame.

Then in `GameMaster.StartNewGame` (`Assets/Scripts/InGame/GameMaster.cs:491-590`):
- `SessionManager.Instance == null` → log, `return` (`:536-542`)
- `m_players.Count != 2` → warning, `return` (`:562-569`)

H6's `GameMaster.HandlePlayerLeft` (`GameMaster.Player.cs:6-49`) deliberately ignores disconnects while `m_gameCoroutine == null`, i.e. before the loop runs.

**Result:** in each failure case above, the host stays in `GameScene` with no match, no result and no button to leave (N19).

Already correct: when the **host** leaves, the client gets `OnClientDisconnected` → `HandleLostConnectionToHost` → `CleanupAfterDisconnect` → lobby.

Existing return path to reuse: `MultiplayerManager.Disconnect()` (`:299-345`). On the host it runs `ShutdownHostInternal` (delete lobby, H7 cleanup, `Shutdown`), then `CleanupAfterDisconnect` → `SceneFlowManager.LoadLobbyScene()`. `GameMaster.AbortMatch(reason)` (`GameMaster.cs:460-489`) logs, sets the phase to `None` and calls `Disconnect()`. It's idempotent.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/MultiplayerScripts/MultiplayerManager.cs` | Abort on load timeouts, on missing sessions or GameMaster, and when a player leaves during the start wait |
| `Assets/Scripts/InGame/GameMaster.cs` | `StartNewGame` calls `AbortMatch` on its two failure returns |

Related:
- NGO `LoadSceneTimeOut: 120` (`NetworkManagerPrefab.prefab:68`)
- Relay allows 1 client (`m_relayClientCount = 1`), so there are at most 2 connections

## 4. Required changes

1. **`MultiplayerManager`:** add a private helper:
   ```csharp
   private void AbortGameStart(string reason)
   ```
   - It logs `Debug.LogError("MultiplayerManager: match start aborted. Reason: " + reason)`.
   - If `GameMaster.Instance != null`, it calls `GameMaster.Instance.AbortMatch(reason)`. Otherwise it calls `Disconnect()`. Both end in `Disconnect()`; `AbortMatch` adds its guard and log.
2. **`OnLoadEventCompleted`** (server branch, `GameScene` only): before `RecreateGameMaster`, if `clientTimeOut != null && clientTimeOut.Count > 0`, call `AbortGameStart("client_scene_load_timeout")` and return.
   - Keep the existing failure checks after `RecreateGameMaster` (`m_gameMaster == null`, `GameMaster.Instance == null`), but call `AbortGameStart(...)` after their log lines instead of only returning.
3. **`StartGameAfterSceneReady`:**
   1. Both timeout branches call `AbortGameStart("session_manager_missing")` / `AbortGameStart("not_enough_sessions")` after their existing log, then `yield break`.
   2. Inside both wait loops, abort immediately if a player left while waiting:
      - `NetworkManager.Singleton == null`
      - `!NetworkManager.Singleton.IsListening`
      - `NetworkManager.Singleton.ConnectedClientsIds.Count < m_maxLobbyPlayers`

      → `AbortGameStart("player_left_before_start")`, then `yield break`.
   3. Before `SpawnPlayers()`, if `GameMaster.Instance == null`, call `AbortGameStart("game_master_missing")` and `yield break`.
4. **`GameMaster.StartNewGame`:** on the `SessionManager.Instance == null` return and the `m_players.Count != 2` return, call `AbortMatch("match_start_failed")` after the existing log, then `return`.
   - `m_isAbortingMatch` is already reset at the top of `StartNewGame`, so the abort goes through.
5. Don't change H6. `HandlePlayerLeft` keeps ignoring disconnects before the loop starts; the start sequence now handles that window.

## 5. Explicit non-goals

- A message in the lobby ("Match could not start"). There's no lobby message UI yet; see Q1.
- Retrying, rejoining or waiting longer.
- Changing timeouts (10 s waits, NGO `LoadSceneTimeOut`).
- Changes to the client side, H6 forfeit, H7 cleanup, or `LeaveMatchToResultScene`.
- The in-match leave button (N19).

## 6. Constraints and compatibility

- No renames and no public API changes. `AbortGameStart` is private.
- Server-only paths. Clients follow through the existing "host disconnected" handling.
- `Disconnect()` and `AbortMatch()` are already guarded against repeats (`m_onDisconnecting`, `m_isAbortingMatch`). Calling the helper from several branches is safe.
- `StartGameAfterSceneReady` remains a coroutine on `MultiplayerManager` (AppRoot, DDOL).

## 7. Acceptance criteria

- [ ] When the client leaves (or crashes) after the host started the scene load but before the match loop starts, the host is back in `LobbyScene` within about 1 s of the disconnect being detected. The host's lobby is deleted.
- [ ] If sessions never reach 2 within the timeout, the host returns to the lobby after the timeout.
- [ ] A client scene-load timeout makes the host return to the lobby. The client, if still connected, follows through the host-disconnect path.
- [ ] `StartNewGame` with fewer than 2 players aborts to the lobby instead of returning silently.
- [ ] A normal start is unchanged: both players spawn and the loop starts.
- [ ] A disconnect **after** the loop started still produces the H6 forfeit result, not the lobby.
- [ ] Compiles without new warnings.

## 8. Regression tests

No automated tests are required. **Review checklist:**
- every early `return` / `yield break` in the server start path now ends in `AbortGameStart` / `AbortMatch`, except the non-`GameScene` and non-server returns
- no change to `HandlePlayerLeft`, `CleanupAfterDisconnect` or `LeaveMatchToResultScene`

## 9. Manual Unity Editor checks

1. **Normal start:** ParrelSync host and client. Start the match and play one round. No change in behavior.
2. **Client leaves during loading (temporary delay, don't commit):** add a temporary `yield return new WaitForSeconds(5f);` at the top of `StartGameAfterSceneReady`. Start the match, and close the client window (or stop its Play Mode) during the 5 s. Expected:
   - host Console: `match start aborted. Reason: player_left_before_start`
   - the host is in `LobbyScene`, and the lobby list no longer shows the old lobby

   Remove the delay.
3. **Host leaves during loading:** same temporary delay; stop the host instead. The client returns to `LobbyScene` (existing behavior).
4. **After start:** without any delay, close the client in `CardSelection`. The host gets the H6 victory result ("Your opponent left the match.").
5. Repeat check 2, then create a new lobby and start a full match. Nothing stale is left over.

## 10. Open questions and assumptions

- **Q1:** Should the host see a localized message such as "The match could not start" in the lobby? This spec doesn't add one. If wanted, it needs a small lobby message UI and en/ru/ja keys, as a separate task.
- **A1:** An opponent who leaves before the loop starts gives no win or loss: no game was played, so the lobby is the right place to return to.
- **A2:** A scene-load timeout (120 s) is rare. Treating it as "can't start" avoids a match with a client that never loaded the scene.
