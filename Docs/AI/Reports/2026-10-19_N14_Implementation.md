# N14 implementation: return to lobby when match startup fails

## 1. Task summary

Implemented `TASK-N14_ReturnToLobbyWhenMatchCannotStart.md`. Specified match-start failures now use the existing disconnect path instead of leaving the host in an empty GameScene. Failures before play do not award a win or loss.

## 2. Files changed

- `Assets/Scripts/MultiplayerScripts/MultiplayerManager.cs`
- `Assets/Scripts/InGame/GameMaster.cs`
- `Docs/AI/Architecture/GameLoop.md`
- `Docs/AI/Architecture/Dependencies.md`
- This implementation report.

The working tree was clean at the start (`e7d1b03`, "h3,h2 complete"). No scenes, prefabs, assets, package settings or Inspector references were modified. No commit or push was performed.

## 3. Important implementation details

- Added private `AbortGameStart(reason)`, which logs and calls existing `GameMaster.AbortMatch(reason)`, or `Disconnect()` if GameMaster does not exist.
- The server GameScene completion branch aborts for any timed-out client before `RecreateGameMaster`. Existing missing/unlinked GameMaster checks now abort after their original logs.
- Both session-readiness waits check the NetworkManager, listening state and connected-client count every iteration. They abort immediately for `player_left_before_start` and abort on their existing 10-second timeouts with the specified reasons.
- Added the same connection check just before spawning. This covers an already-ready state where neither wait runs. Made the second wait's condition null-safe and abort if SessionManager disappears while waiting, preventing its loop condition from throwing before the disconnect check. These small additions cover the startup timing gaps found during inspection.
- A missing GameMaster immediately before spawning aborts. `StartNewGame` now calls `AbortMatch("match_start_failed")` when SessionManager is absent or the collected player count is not exactly two.
- Normal startup still invokes SpawnPlayers and StartNewGame in the same frame. Server/client ownership and timeout values remain unchanged. Non-server and non-GameScene callbacks retain their existing behavior.
- `HandlePlayerLeft`, `Disconnect`, `ShutdownHostInternal`, `CleanupAfterDisconnect` and `LeaveMatchToResultScene` are unchanged. Existing abort/disconnect guards and host lobby deletion/shutdown are reused. The startup coroutine terminates after an abort; the existing H6 path remains responsible for disconnects after the game loop starts.
- Required Inspector setup: none.

## 4. Tests actually executed and results

### C# compilation

Invoked Unity 6000.3.13f1's bundled Roslyn compiler with the project's existing Editor `Assembly-CSharp.rsp` source list, references, definitions and analyzers. Outputs were redirected to git-ignored `build/task-n14-verification/`, without replacing Unity's cached assemblies.

Result: exit code 0, no errors and no new warnings. Only existing CS0114 (`PlayerUI.OnDestroy`) and CS0414 (`GameMaster.m_cardResolveDelay`) were reported. This verifies C# compilation, not Editor import, NGO IL postprocessing, a player build or Play Mode.

Evidence: `build/task-n14-verification/compile.rsp` and `compile-results.txt`.

### Focused source checks

Ran `build/task-n14-verification/verify.ps1`, which compiles the actual `AbortGameStart`, `OnLoadEventCompleted`, `StartGameAfterSceneReady`, `StartNewGame` and `AbortMatch` methods with Unity/NGO/service doubles.

**26 isolated scenarios passed**, covering:

- Normal ready startup and sessions arriving during both waits.
- The two existing 10-second session timeouts.
- Missing NetworkManager, stopped listening and a lost connection in each wait and at the ready-to-spawn stage.
- Disconnect detection on the next coroutine iteration and SessionManager disappearing during the second wait.
- Missing/unlinked GameMaster, scene-load timeout before recreation, ordinary scene completion, ignored unrelated scenes and the client callback path.
- StartNewGame with no SessionManager or one player, its existing non-server guard and repeated helper calls through the existing GameMaster abort guard.

The harness records calls to Disconnect and substitutes Unity lifecycle, NGO state, coroutine scheduling and services. It does not actually delete a lobby, unload a scene, replicate a disconnect or play a game. It is git-ignored, outside Assets, and introduces no permanent test scripts. Final Git diff and whitespace checks were reviewed, including unchanged H6/H7/result cleanup methods.

## 5. Remaining risks / checks not executed

- No Unity Play Mode, real host/client loading/disconnection, lobby-service deletion, full-match regression or standalone player build was run for N14.
- Aborts are initiated on the next wait iteration after detecting a disconnect; actual arrival in LobbyScene depends on the existing asynchronous disconnect and lobby-service path. The approximately one-second acceptance target needs runtime measurement.
- The scoped failure checks do not add exception handling or cancellation to Bootstrap's asynchronous recreation implementation. That broader behavior is unchanged.

## 6. Manual Unity Editor verification

1. Start a normal ParrelSync host/client match and play one round; verify both players spawn and the loop progresses.
2. Temporarily add the task's five-second delay at the top of StartGameAfterSceneReady. Disconnect the client during it. Verify the host logs `player_left_before_start`, returns to LobbyScene promptly and deletes the old lobby.
3. Repeat with the host leaving; verify the client returns to LobbyScene through the existing host-disconnect path.
4. Remove the delay. Disconnect the client during CardSelection after startup; verify the host receives the existing H6 victory result rather than a startup-abort return.
5. After a startup abort, create a new lobby and start another complete match; check for stale sessions/objects. Restore every temporary testing change. No persistent Inspector setup is required.
