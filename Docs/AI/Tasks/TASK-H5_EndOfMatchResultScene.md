# TASK-H5: Simple end-of-match flow: decide the winner, close the network session, show the ResultScene

- **Source issue:** [Issue Report #2 → H5](../Reports/2026-10-18_IssueReport-2.md)
- **Severity:** 🟠 High
- **Owner decisions (2026-10-18):**
  - A new **ResultScene** shows who won and who lost.
  - For now the result is only a **Console message, when debug mode is on**. The GUI comes in [TASK-H10](TASK-H10_ResultSceneMenu.md).
  - The network session **and the lobby** are closed when leaving `GameScene` for the ResultScene. A later host disconnect must not throw a player back to the main menu or lobby.
- **Order:** H7 → H1 → **H5** → H6 / H10
- **Implementer:** Codex (scene creation in the Unity Editor)

## 1. Objective

When the match is decided:
1. The server determines the winner and tells every instance its own result.
2. Each instance stores that result locally, leaves the Unity lobby, shuts down Netcode, and loads the offline `ResultScene`.
3. In `ResultScene` the result is written to the Console (debug mode only).

Once an instance has started leaving for `ResultScene`, no disconnect callback may redirect it to the lobby.

## 2. Current behavior

- `GameMaster.GameLoop()` (`GameMaster.GameLoop.cs:44-62`) exits its `while (!IsGameFinished())` loop. It then only logs `Debug.LogError("Game finished. You lost/won.")`, based on `m_isLocalPlayerLost`. That value is host-relative and isn't networked.
- Nothing is sent to clients. Nobody leaves `GameScene`, and the session and lobby stay open.
- `GamePhase` (`GameMaster.cs:9-19`) has no game-over value.
- `GameAnnouncerUI.GetPhaseLocalizationKey` (`UI/GameAnnouncerUI.cs:~116-130`) **throws** for an unmapped phase (L5). It runs every frame on all instances.
- Disconnect handling:
  - `MultiplayerManager.OnClientDisconnected` → `HandleLostConnectionToHost` (`:247`, returns early if `m_onDisconnecting`) → `CleanupAfterDisconnect` (`:1193`) → `SceneFlowManager.LoadLobbyScene()`
  - The lobby heartbeat only pings while `InLobbyStatus` (`:403-416`), so during a match the lobby is never refreshed.
- `SceneFlowManager` (`Manager/SceneFlowManager.cs`) loads offline scenes with `SceneManager.LoadScene`. It has no result scene.
- `EditorBuildSettings` scenes: `SampleScene`, `MultiplayerScene`, `LoginScene`, `MainMenuScene`, `LobbyScene`, `GameScene`.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/GameMaster.cs` | `GamePhase.GameOver` (append), new `MatchEndReason` enum, `EndMatch`, `MatchEndedClientRpc`, reset in `StartNewGame` |
| `Assets/Scripts/InGame/GameMaster/GameMaster.GameLoop.cs` | End of `GameLoop()`: determine the winner and call `EndMatch` |
| `Assets/Scripts/InGame/MatchResultData.cs` | **New**: static holder for the local result |
| `Assets/Scripts/MultiplayerScripts/MultiplayerManager.cs` | New `LeaveMatchToResultScene()`; the disconnect callback respects it |
| `Assets/Scripts/Manager/SceneFlowManager.cs` | New `m_resultSceneName = "ResultScene"` and `LoadResultScene()` |
| `Assets/Scripts/UI/GameAnnouncerUI.cs` | Map `GameOver` to a key |
| `Assets/Scripts/InGame/ResultSceneController.cs` | **New**: Console output in ResultScene |
| `Assets/Scenes/ResultScene.unity` | **New scene**, created in the Editor |
| `ProjectSettings/EditorBuildSettings.asset` | Add ResultScene, through the Build Profiles window |
| `Assets/Localization/UI_table*` | One key, `Phase.GameOver` (en/ru/ja) |
| H1 runner / H7 cleanup | Used, not changed |

## 4. Required changes

### 4.1 Types and data

1. Append `GameOver` to the **end** of `GamePhase`, after `RoundEnd`, so existing values keep their numbers.
2. Add `public enum MatchEndReason { LivesDepleted, OpponentDisconnected }` in `GameMaster.cs`, next to `GamePhase`.
3. Add a new file `Assets/Scripts/InGame/MatchResultData.cs` containing `public static class MatchResultData`:
   - **Properties:** `HasResult`, `IsWinner`, `IsDraw` (`bool`), and `Reason` (`MatchEndReason`)
   - `public static void Set(bool isWinner, bool isDraw, MatchEndReason reason)`
   - `public static void Clear()`

   It's a plain static class, with no `MonoBehaviour` and nothing networked. It survives the scene load.

### 4.2 Server: end the match (`GameMaster`)

4. Add `public void EndMatch(PlayerSession winner, MatchEndReason reason)`. `winner == null` means a draw.
   - Return if `!IsServer`, or if the new private `bool m_isMatchEnded` is already true. Then set it to true; reset it in `StartNewGame`.
   - Set `m_gamePhase.Value = GamePhase.GameOver`, and set `m_activePlayer.Value` and `m_waitingRespawnPlayer.Value` to `default`.
   - Clear `m_inputQueue` and `m_cardInputQueue`.
   - **Don't** stop `m_gameCoroutine` here. `GameLoop` calls `EndMatch` itself and then ends normally. TASK-H6 stops the loop before it calls `EndMatch`.
   - Send `MatchEndedClientRpc(winner != null ? winner.OwnerClientId : k_NoWinnerClientId, reason)`, where `private const ulong k_NoWinnerClientId = ulong.MaxValue`.
   - Log `winner` and `reason` under `m_debugMode`.
5. Add `[ClientRpc] private void MatchEndedClientRpc(ulong winnerClientId, MatchEndReason reason)`. It runs on every instance, the host included:
   - `isDraw = winnerClientId == k_NoWinnerClientId`
   - `isWinner = !isDraw && winnerClientId == NetworkManager.Singleton.LocalClientId`
   - `MatchResultData.Set(isWinner, isDraw, reason);`
   - `MultiplayerManager.Instance?.LeaveMatchToResultScene();`. Use an explicit null check in the project style.
6. At the end of `GameLoop()`, replace the `switch (m_isLocalPlayerLost)` block (`:53-61`) with this:
   - Collect the players in `m_players` with `TeamId.Value != -1` that are still "alive or can respawn". That's the same rule as `IsGameFinished`: `GetCharacter() != null || m_lifeCurrent.Value > 0`.
   - Exactly 1 such player → that player is the `winner`. 0 → `null` (draw).
   - Call `EndMatch(winner, MatchEndReason.LivesDepleted)`.
   - Keep the fields `m_isLocalPlayerLost` and `m_lostClientId` and the method `FindLostPlayer`. Don't delete them (`AGENTS.md`).

### 4.3 All instances: leave for the ResultScene (`MultiplayerManager`)

7. Add `public async void LeaveMatchToResultScene()` and a private `bool m_isLeavingToResult`.
   1. If `m_isLeavingToResult` is true, return. Otherwise set `m_isLeavingToResult = true` **and** `m_onDisconnecting = true`. The second flag makes `HandleLostConnectionToHost` and `Disconnect` return early, so no lobby redirect can happen from now on.
   2. In `OnClientDisconnected`: if `m_isLeavingToResult`, log under debug and return before any other handling.
   3. **Host only:** wait until every remote client has disconnected, which they do themselves in their own step 7. That means `NetworkManager.Singleton.ConnectedClientsIds.Count <= 1`, with a timeout (new `[SerializeField] private float m_resultLeaveTimeout = 3f`). Poll with `await Task.Delay(100)` while `IsListening`. This way the host doesn't shut down before the clients have received the result RPC.
   4. **Lobby:**
      - Host: `DeleteLobbyAsync(m_currentLobby.Id)`.
      - Client: `RemovePlayerAsync(m_currentLobby.Id, AuthenticationService.Instance.PlayerId)`.
      - Wrap both in `try/catch (LobbyServiceException)` plus `catch (System.Exception)`, logging only. A failure must not stop the scene change.
   5. Call `GameMaster.Instance.ClearupAfterDisconnect()` if the instance exists. This is the H7 version: local state only, and it stops the loop.
   6. If the NetworkManager is listening, call `NetworkManager.Singleton.Shutdown()`.
   7. Reset local multiplayer state, the same as `CleanupAfterDisconnect` but **without** `LoadLobbyScene`:
      - `SessionManager.Instance?.ClearSessionOnDisconnect()`
      - `m_currentLobby = null`
      - `m_currentMultiplayerStatus = MultiplayerStatus.InMenuStatus`
      - `m_isStartingGameScene = false`, and stop `m_startGameSceneCoroutine`

      Use explicit null checks. A new private helper method is fine; **leave `CleanupAfterDisconnect` unchanged**.
   8. Call `SceneFlowManager.Instance.LoadResultScene()`.
   9. Finally, set `m_onDisconnecting = false` and `m_isLeavingToResult = false`. Keep everything after step 1 in a `try/finally`, so the flags are always reset and the scene is always loaded (on error, still try `LoadResultScene`).
8. In `SceneFlowManager`, add `[SerializeField] private string m_resultSceneName = "ResultScene";` and `public void LoadResultScene() { LoadOfflineScene(m_resultSceneName); }`.

### 4.4 Phase display

9. In `GameAnnouncerUI.GetPhaseLocalizationKey`, add `GamePhase.GameOver => "Phase.GameOver"`. Also check every other `switch` on `GamePhase` (`PlayerUI.cs:146`) for unhandled values.
10. Add the localization key `Phase.GameOver` to `UI_table`, **in en, ru and ja**, through the Localization Tables window:
    - en: "Game over"
    - ru: "Игра окончена"
    - ja: "ゲーム終了"

### 4.5 ResultScene (debug Console output only)

11. **In the Unity Editor:** create an empty scene with a Main Camera and save it as `Assets/Scenes/ResultScene.unity`. **Don't hand-write scene YAML.** Add it to **File → Build Profiles → Scene List** after `GameScene`.
12. Add `Assets/Scripts/InGame/ResultSceneController.cs`: a `MonoBehaviour` with `[SerializeField] private bool m_debugMode = false;`. In `Start()`, when `m_debugMode` is on:
    - If `!MatchResultData.HasResult`: `Debug.LogWarning("ResultScene: no match result available.")`
    - Else: `Debug.Log($"ResultScene: {(IsDraw ? "Draw" : IsWinner ? "You won" : "You lost")}. Reason: {Reason}")`

    It **doesn't** clear `MatchResultData`; H10's "Main menu" button does that.
13. Put one GameObject `ResultSceneController` in `ResultScene`, with `m_debugMode` enabled.

## 5. Explicit non-goals

- Any result GUI, button or result text keys (that's **TASK-H10**).
- Forfeit on disconnect (**TASK-H6**, which uses `EndMatch` with `OpponentDisconnected`).
- H1's `AbortMatch`. It still returns to the lobby, not to the result screen.
- Statistics, rematch, or returning to the same lobby.
- Lobby heartbeat behaviour during a match.

## 6. Constraints and compatibility

- No renames or deletions (`AGENTS.md`). `GamePhase.GameOver` is **appended**.
- `ResultScene` is an **offline** scene: no `NetworkObject`, and it's loaded with `SceneManager.LoadScene` after `Shutdown()`.
- `LeaveMatchToResultScene` must never end in `LobbyScene` or `MainMenuScene`, including when the host shuts down first or the lobby call fails.
- A new `.cs` file gets its `.meta` generated by Unity. Commit it. Don't hand-write GUIDs.
- New text exists in en/ru/ja.

## 7. Acceptance criteria

- [ ] When a player loses the last life, **both** instances end up in `ResultScene`. With debug mode on:
  - the winner's Console shows "You won. Reason: LivesDepleted"
  - the loser's Console shows "You lost. Reason: LivesDepleted"
- [ ] Neither instance passes through `LobbyScene` or `MainMenuScene` on the way. No exceptions.
- [ ] The Unity lobby is deleted (the host's lobby list no longer shows it, and `FindAvailableLobbies` doesn't find it).
- [ ] With the host in `ResultScene`, a client that is still in `ResultScene` is **not** moved anywhere, and the reverse.
- [ ] `GameAnnouncerUI` doesn't throw during `GameOver`. `Phase.GameOver` exists in en/ru/ja.
- [ ] After returning to the main menu (H10, or temporarily by loading `MainMenuScene` from the Inspector), a new lobby and a new match work.
- [ ] Compiles without new warnings.

## 8. Regression tests

No automated tests. Optional EditMode test: winner selection as a pure function over (`hasCharacter`, `lives`, `teamId`) tuples.

Review checklist:
- `m_onDisconnecting` is set before any await in `LeaveMatchToResultScene`
- `CleanupAfterDisconnect` is untouched
- the enum value is appended
- `EndMatch` is idempotent

## 9. Manual Unity Editor checks

1. ParrelSync host and client. Play until one pony loses all 3 lives (use lava or Iceshard; the debug Space key works in the Editor). Check:
   - both windows switch to `ResultScene`
   - the Consoles show the right win and loss lines
   - there are no errors
2. Repeat, with the **host** as the loser.
3. While both are in `ResultScene`, stop the host's Play Mode. The client stays in `ResultScene`.
4. Check the lobby list from a third instance, or after restarting: the old lobby is gone.
5. Switch the locale to ru and ja, and briefly look at the announcer during the game-over moment: the text is localized, and there's no exception.

## 10. Open questions and assumptions

- **A1:** the host's wait (step 4.3.7.3) relies on clients disconnecting themselves after the RPC. If a client never receives it, the host shuts down after `m_resultLeaveTimeout`. That client then takes the old "lost connection to host" path to the lobby. This is acceptable for now.
- **A2:** `MatchResultData` is per process. ParrelSync clones are separate processes, so each one shows its own result.
- **Q1:** Should H1's `AbortMatch` later also go to `ResultScene`, with a "match aborted" reason, instead of the lobby? Not in scope.
