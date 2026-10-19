# TASK-M10: Handle async lobby and network errors in `MultiplayerManager`

- **Source issue:** [Issue Report #6 → M10](../Reports/2026-10-19_IssueReport-6.md#-medium-13) (first reported in #1; seen at runtime in #5 and #6)
- **Severity:** 🟡 Medium (Confirmed)
- **Baseline:** `fe2d4fb`. Line numbers below are from this commit.
- **Order:** 4 of 13. Do it before M11 and M13, which change the same file.
- **Implementer:** Codex

## 1. Objective

No exception from Unity Gaming Services (Lobby, Relay, Authentication) or NGO in an async lobby or network path goes unobserved. After every failure the player is left in a consistent state:
- not a half-member of a lobby
- no host running without a lobby
- no lobby scene loaded while the `NetworkManager` is still listening

Each failure is logged once.

## 2. Current behavior

All in `Assets/Scripts/MultiplayerScripts/MultiplayerManager.cs`:

| Entry point | Line | Problem |
|---|---|---|
| `Start` (`async void`) | `:79-105` | `AuthManager.Initialize` and `RecreateNetworkManager` aren't wrapped. If one throws, `SubscribeNetworkEvents` and the heartbeat `InvokeRepeating` never run. |
| `OnApplicationQuit` (`async void`) | `:117-137` | Catches only `LobbyServiceException` |
| `HandleLostConnectionToHost` (`async void`) | `:255-298` | `RemovePlayerAsync` catches only `LobbyServiceException`. Any other exception skips `ClearupAfterDisconnect` and `Shutdown()`. The `finally` still loads the lobby scene, so the client is in `LobbyScene` while the `NetworkManager` keeps listening. |
| `ShutdownHostInternal` / `DisconnectClientInternal` | `:466-497`, `:498-530` | Catch only `LobbyServiceException`. Another exception skips `ClearupAfterDisconnect` and `Shutdown()`. `Disconnect` (`:299-345`) catches it and calls `CleanupAfterDisconnect`, so the lobby scene loads while still listening. |
| `HearthbeatLobby` (`async void`, every 15 s) | `:531-546` | No null check on `NetworkManager.Singleton`, which is null while it's being recreated. No `try/catch` around `SendHeartbeatPingAsync`. |
| `StartHost` | `:565-637` | Catches only `LobbyServiceException`. `CreateRelay` can throw `RelayServiceException` or `InvalidOperationException` ("Failed to start host", `:664`). If `CreateLobbyAsync` fails after `StartHost()` succeeded, **the host keeps running with no lobby** (`m_currentLobby == null`, still listening). `LobbyControlUI.OnClickCreateLobby` is `async void`, so the exception is unobserved. |
| `FindAvailableLobbies` | `:671-783` | Catches only `LobbyServiceException`. Others reach `LobbyListUI.OnClickRefreshLobbyList` (`async void`). |
| `JoinLobbyById` | `:785-847` | Catches only `LobbyServiceException`. A missing `joinCode` (`:813-817`) returns while the player is still a lobby member. An unexpected exception after `JoinLobbyByIdAsync` leaves the player in the lobby with no connection. |
| `JoinRelay` | `:908-955` | Catches only `RelayServiceException`. A transport or NGO exception escapes, and the `NetworkManager` may still be listening. |
| `RefreshCurrentLobby` | `:849-862` | Catches only `LobbyServiceException`. **Seen at runtime:** a `NullReferenceException` inside `LobbyService.GetLobbyAsync` during the start-game refresh (`Editor-prev.log:1024`, logged by `RefreshCurrentLobbyCoroutine` at `:1156`). The start then continued with cached lobby data. |
| `OnLoadEventCompleted` (`async void`) | `:1188-1242` | `await m_bootsTrap.RecreateGameMaster()` (`:1219`) has no `catch`. If it throws, the start sequence stops silently, and N14's abort never runs. |

`JoinLobbyByJoinCode` (`:863-906`) has no `catch` at all, but it's dead code. **M11** handles it, so leave it alone here.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/MultiplayerScripts/MultiplayerManager.cs` | `catch` blocks and cleanup on each path in §2 |

Read-only:
- `Bootstrap.cs` (`RecreateNetworkManager`, `RecreateGameMaster`)
- `UI/LobbyControlUI.cs`, `UI/LobbyListUI.cs`, `UI/CurrentLobbyUI.cs` (callers)
- `GameMaster.AbortMatch`, `MultiplayerManager.AbortGameStart` (`:1178-1186`, N14)

## 4. Required changes

General rule: keep the existing specific `catch (LobbyServiceException)` / `catch (RelayServiceException)` blocks and their log level, and **add** a `catch (System.Exception e)` that calls `Debug.LogException(e)`. Cleanup that must always run goes after the `try/catch` or in a `finally`.

1. **`Start`:** wrap `AuthManager.Instance.Initialize()` and `m_bootsTrap.RecreateNetworkManager()` in their own `try/catch (Exception)`. `SubscribeNetworkEvents()` and `InvokeRepeating(...)` must run even if one of them failed.
2. **`OnApplicationQuit`:** add `catch (System.Exception e)` → `Debug.LogException(e)`.
3. **`HandleLostConnectionToHost`:** add `catch (System.Exception e)` next to the `RemovePlayerAsync` `catch`, so `ClearupAfterDisconnect` and `Shutdown()` always run. Keep the `finally` → `CleanupAfterDisconnect`.
4. **`ShutdownHostInternal` / `DisconnectClientInternal`:** add `catch (System.Exception e)` → `Debug.LogException(e)`, so the `ClearupAfterDisconnect` + `Shutdown()` that follow always run. In `ShutdownHostInternal`, also set `m_currentLobby = null` after a failed delete. A host that leaves gives up the lobby either way, and the service removes it when heartbeats stop.
5. **`HearthbeatLobby`:**
   - return if `NetworkManager.Singleton == null`
   - wrap `SendHeartbeatPingAsync` in `try`, with `catch (LobbyServiceException e)` → `Debug.LogWarning(e.Message)` and `catch (System.Exception e)` → `Debug.LogException(e)`
   - don't change the 15 s interval
6. **`StartHost`:** add `catch (System.Exception e)`. In both `catch` blocks, call a new private helper `ResetAfterFailedHostStart()`:
   - if `NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening` → `NetworkManager.Singleton.Shutdown()`
   - set `m_currentLobby = null` and `m_currentMultiplayerStatus = MultiplayerStatus.InMenuStatus`

   The `!IsHost` early return (`:584-588`) needs no shutdown, because nothing is listening. Keep the `finally` that resets `m_isRelayAction`.
7. **`FindAvailableLobbies`:** add `catch (System.Exception e)` → `Debug.LogException(e)`, and still return `m_knowLobbies`, which may be empty.
8. **`JoinLobbyById`:**
   - if `joinCode` is missing (`:813-817`), call `await LeaveCurrentLobbyAfterFailedJoin()` before `return`
   - add `catch (System.Exception e)` → `Debug.LogException(e)`
   - in both `catch` blocks: if `m_currentLobby != null`, `await LeaveCurrentLobbyAfterFailedJoin()`; if the `NetworkManager` is listening, call `Shutdown()`
9. **`JoinRelay`:** add `catch (System.Exception e)` → `Debug.LogException(e)`. Shut down the `NetworkManager` if it's listening, then return `false`.
10. **`RefreshCurrentLobby`:** change the return type to `Task<bool>`.
    - return `true` after a successful `GetLobbyAsync`
    - `false` when there's no lobby, and in both `catch` blocks
    - add `catch (System.Exception e)` → `Debug.LogException(e)`

    Existing callers keep working: `CurrentLobbyUI.RefreshOnceAsync` uses `await`, and `RefreshCurrentLobbyCoroutine` stores it as a `Task`.
11. **`RefreshCurrentLobbyCoroutine`:** if the task result is `false`, log `Debug.LogWarning("MultiplayerManager: lobby refresh failed, using the last known lobby data.")`. The start flow then continues as it does today: `CanStartTwoPlayerGame()` checks the last known data, and the NGO check (`IsNetworkReadyForTwoPlayerGame`) has already confirmed 2 connected clients.
12. **`OnLoadEventCompleted`:** wrap `await m_bootsTrap.RecreateGameMaster();` in `try/catch (System.Exception e)` → `Debug.LogException(e)`, then `AbortGameStart("game_master_create_failed")` and `return`.

## 5. Explicit non-goals

- A player-facing error message in the lobby or main menu. There's no message UI yet (same as N14 Q1).
- Retries or backoff.
- `JoinLobbyByJoinCode` (M11), lobby names and hard-coded strings (M13).
- `AuthManager`'s internal error handling.
- The unused `DisconnectClient()` (`:1036`, L10).
- Changing timeouts, the heartbeat interval, or the N14 abort reasons that already exist.

## 6. Constraints and compatibility requirements

- `RefreshCurrentLobby` changes from `Task` to `Task<bool>`; existing callers compile unchanged. No other public signature changes.
- `ResetAfterFailedHostStart` is private.
- Keep the `m_isRelayAction` / `m_onDisconnecting` guards and their `finally` resets.
- Don't change the order of the successful paths.
- No asset changes.

## 7. Acceptance criteria

- [ ] Every path in §2 (except `JoinLobbyByJoinCode`) has a `catch (System.Exception)`, or rethrows nothing to an `async void` caller.
- [ ] A failed lobby creation leaves the `NetworkManager` not listening, `m_currentLobby == null` and the search panel shown.
- [ ] A failed join never leaves the player as a lobby member or with a listening `NetworkManager`.
- [ ] A failed host or client disconnect still shuts down the `NetworkManager` before the lobby scene loads.
- [ ] A failure in `RecreateGameMaster` makes the host return to the lobby with "match start aborted. Reason: game_master_create_failed".
- [ ] Normal flows are unchanged: create, refresh, join, start, play, leave.
- [ ] Compiles with no new warnings.

## 8. Regression tests

No automated tests. **Review checklist:**
- every `async void` method in `MultiplayerManager` catches `System.Exception`
- every `catch` in the join and host paths restores a consistent state (lobby left or deleted, `NetworkManager` shut down)
- no successful path behaves differently

## 9. Manual Unity Editor checks

ParrelSync host + client.
1. **Regression:** create a lobby, the client refreshes and joins, start, play one round, leave. Same as before.
2. **Offline create:** turn off the PC's network, then click Create lobby. Expected:
   - one logged error and no "unobserved exception" line
   - the search panel is still shown
   - the `NetworkManager` in the Hierarchy isn't listening (no host is running)

   Turn the network back on and create a lobby: it works.
3. **Offline refresh:** with the network off, refresh the lobby list. You get an error log and the "not found" status, and nothing unhandled.
4. **Heartbeat:** host a lobby, turn the network off for about 20 s, then turn it back on. Only heartbeat warnings appear, no exceptions.
5. **Join a deleted lobby:** the client refreshes the list, then the host leaves the lobby, then the client clicks Join on the old entry. Expected: an error log, the client stays in the search panel, and it isn't connected.
6. **Temporary code edit (don't commit):** add `throw new System.Exception("test");` as the first line of `Bootstrap.RecreateGameMaster`. Start a match. Expected:
   - the host logs the exception and "match start aborted. Reason: game_master_create_failed"
   - the host returns to `LobbyScene`, and the client follows

   Remove the edit.

## 10. Open questions and assumptions

- **Q1:** Should the player see a localized message for these failures? Not in this task. It needs a small message UI (shared with N14 Q1).
- **A1:** The root cause of the `NullReferenceException` inside `LobbyService.GetLobbyAsync` is inside the SDK. This task only contains it.
