# TASK-N30: One lobby refresh at a time, and not too often

- **Source issue:** [Manual-check review → N30](../Reviews/2026-10-20_ManualChecks-Report6-7_Review.md#3-new-findings) (new)
- **Severity:** 🟢 Low. The exception is Confirmed; its cause (request rate) is Plausible.
- **Baseline:** `b7e7b91`. Line numbers below are from this commit.
- **Order:** 13 of 20. Independent.
- **Implementer:** Codex

## 1. Objective

`MultiplayerManager.RefreshCurrentLobby()` never has two `GetLobbyAsync` calls in flight, and doesn't call the Lobby service more often than once per 1.5 s per player. The start-game check still gets current data. The SDK exception seen in the build (§2) no longer appears in normal use.

## 2. Current behavior

- **Seen in the player build** (`Player.log`, second lobby `HBNRB7`, as the host pressed Start):
  ```
  NullReferenceException: Object reference not set to an instance of an object
    at Unity.Services.Lobbies.Internal.WrappedLobbyService.TryCatchRequest[…]
    at Unity.Services.Lobbies.Internal.WrappedLobbyService.GetLobbyAsync (System.String lobbyId)
    at MultiplayerManager.RefreshCurrentLobby ()
  MultiplayerManager: lobby refresh failed, using the last known lobby data.
  ```
  M10's handler caught it, and the match started with the cached data.
- **Where the NRE comes from:** `Library/PackageCache/com.unity.services.multiplayer@…/Runtime/Lobbies/SDK/WrappedLobbyService.cs:559-606` `TryCatchRequest`. Its `catch` blocks dereference `he.ActualError.Code` and `he.Response.StatusCode`. The NRE escapes unwrapped, so the HTTP call failed with an error response the SDK couldn't parse. A rejected burst (HTTP 429 Too Many Requests) fits.
- **Unity's documented limits** are per player. Query-type calls are limited to about 1 request per second ([Lobby rate limits](https://docs.unity.com/en-us/lobby/rate-limits)). Unity's own lobby sample uses a 1.5 s client cooldown.
- **Who calls `GetLobbyAsync`:** only `RefreshCurrentLobby()` (`MultiplayerManager.cs:927-947`), which has two callers:
  1. `UI/CurrentLobbyUI.cs` `RefreshLobbyLoop` / `RefreshOnceAsync` (`:121-160`): every `m_refreshInterval` s on **both** peers. The interval is 2 s in `LobbyScene.unity:1266`. The loop waits for its own call to finish, so it never overlaps itself.
  2. `MultiplayerManager.StartGameSceneWhenReady` → `RefreshCurrentLobbyCoroutine` (`:1232`, `:1248-1265`): once, when the host presses Start.
- Call 2 can start while call 1 is in flight, or within a second of it. Both results are assigned to `m_currentLobby`, and the slower one wins.
- `HearthbeatLobby` (every 15 s, `:122`) and `FindAvailableLobbies` (`:733`, on request) use other endpoints. Leave them as they are.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/MultiplayerScripts/MultiplayerManager.cs` | Share an in-flight refresh; minimum interval; clearer log for a rate limit |
| `Assets/Scripts/UI/CurrentLobbyUI.cs` | No change needed. Read it to confirm. |

## 4. Required changes

1. **`MultiplayerManager`**, new private fields:
   - `private Task<bool> m_lobbyRefreshTask;`
   - `private float m_lastLobbyRefreshTime = -100f;`
   - `[SerializeField] private float m_minLobbyRefreshInterval = 1.5f;` (with a short comment: Lobby rate limit, per player)
2. **`RefreshCurrentLobby()`** keeps its signature `public async Task<bool> RefreshCurrentLobby()`, and becomes:
   1. If `m_currentLobby == null` → `return false;` (as now)
   2. If `m_lobbyRefreshTask != null && !m_lobbyRefreshTask.IsCompleted` → `return await m_lobbyRefreshTask;` (join the call in flight)
   3. If `Time.unscaledTime - m_lastLobbyRefreshTime < m_minLobbyRefreshInterval` → `return true;` (the cached lobby is recent enough)
   4. Otherwise `m_lastLobbyRefreshTime = Time.unscaledTime; m_lobbyRefreshTask = RefreshCurrentLobbyCore(); return await m_lobbyRefreshTask;`
3. **`private async Task<bool> RefreshCurrentLobbyCore()`:** the current try/catch body, moved unchanged, with these changes:
   - Keep the lobby ID before the await (`string _lobbyId = m_currentLobby.Id;`), and assign the result only if `m_currentLobby != null && m_currentLobby.Id == _lobbyId`. A refresh that finishes after the player left or switched lobbies must not bring the old lobby back.
   - `catch (LobbyServiceException e)`: when `e.Reason == LobbyExceptionReason.RateLimited`, log `Debug.LogWarning("MultiplayerManager: lobby refresh rate-limited; using the last known lobby data.")`. Otherwise `Debug.LogError(e)` as now.
   - `catch (System.Exception e)`: keep `Debug.LogException(e)`.
4. **Reset** `m_lobbyRefreshTask = null` and `m_lastLobbyRefreshTime = -100f` wherever `m_currentLobby` is set to `null` or replaced by a create or join (`:459`, `:521`, `:658`, `:697`, `:877`, `:1134`, `:1152`, `:1510`). A new lobby then refreshes at once.
5. **The start path stays correct:** `StartGameSceneWhenReady` already waits until both NGO clients are connected. With step 2.3 it may reuse data at most 1.5 s old, which `CanStartTwoPlayerGame()` then checks as before.

## 5. Explicit non-goals

- No change to `CurrentLobbyUI`'s interval or loop.
- No retry or backoff logic.
- No change to the heartbeat, the query or the join paths.
- No on-screen error message (M10 Q1 feature gap).

## 6. Constraints and compatibility requirements

- Main-thread only: Unity's synchronization context. No locks needed.
- The `Task<bool>` contract stays: `true` = data usable, `false` = no lobby or the refresh failed. `RefreshCurrentLobbyCoroutine`'s log stays.
- The new serialized field gets its default on `MultiplayerManager.prefab`. No manual prefab edit.
- `LobbyExceptionReason.RateLimited` exists in the installed package (`LobbyExceptionReason.cs:244`, value 16429, which is the SDK range 16000 + HTTP 429).

## 7. Acceptance criteria

1. Never more than one `GetLobbyAsync` in flight per peer. Verify with a temporary counter or debug log around the SDK call.
2. Not more than one `GetLobbyAsync` per 1.5 s per peer, including when the host presses Start.
3. A refresh that completes after leaving the lobby doesn't restore `m_currentLobby`.
4. In 10 lobby → Start cycles (Editor host + build client, then the other way round), the log has no `NullReferenceException … TryCatchRequest` and no "lobby refresh failed" line.
5. The lobby screen still updates the player list and the Start button within about 2 s of a join or leave.

## 8. Regression tests

- Harness with a Lobby-service double that counts calls and completes on demand:
  - two overlapping `RefreshCurrentLobby()` calls → 1 SDK call, both return the same result
  - a call within 1.5 s of a completed one → 0 SDK calls, returns `true`
  - the lobby is cleared during an in-flight call → `m_currentLobby` stays `null`
  - a rate-limited exception → a warning, returns `false`
- Roslyn compile. Exit 0.

## 9. Manual Unity Editor checks

1. Host and client (ParrelSync): create, join, start, play a round, leave, back to the lobby, create again, join, start. Repeat 5 times. No lobby exception in either Console.
2. The same with the player build as the host (the case from the review). Check `Player.log`.
3. Join and leave while watching the host's lobby screen: the count and the Start button update within about 2 s.

## 10. Open questions and assumptions

- Plausible cause: the exact HTTP status behind the SDK's NRE wasn't logged. If the exception still appears after this change, the next step is to log `he.Response.StatusCode` in a temporary copy of the call, then report.
