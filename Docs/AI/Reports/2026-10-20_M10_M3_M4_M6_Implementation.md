# M10, M3, M4 and M6 implementation

## 1. Task summary

Implemented the requested specifications against `fe2d4fb` plus the existing uncommitted N20/M14/M8 work:

- M10: contain lobby/network async failures and restore local state on failed host start/join; retain cached lobby data when refresh fails; abort startup if GameMaster recreation throws.
- M3: key the lava duplicate-kill guard by pony instance, pruning destroyed entries so recycled IDs are not immune.
- M4: use server authority for GameMaster input enqueue/processing.
- M6: guard missing sessions and spawn points during HP death handling.

## 2. Files changed

- `Assets/Scripts/MultiplayerScripts/MultiplayerManager.cs` (M10).
- `Assets/Scripts/InGame/LavaKillZone.cs` (M3).
- `Assets/Scripts/InGame/GameMaster.cs` (M4).
- `Assets/Scripts/InGame/GameMaster/GameMaster.PackageManaging.cs` (M4, preserving the prior M14 RPC notification).
- `Assets/Scripts/InGame/GameMaster/GameMaster.Processing.cs` (M6).
- `Docs/AI/Architecture/Architecture.md`, `GameLoop.md`, `Dependencies.md` and this report.

No scenes, prefabs, assets, serialized field names, packages or project settings changed. Existing N20/M14/M8 changes, documentation and the user's task-folder additions/removals were retained. No commit, push, reset or discard was performed. Focused verification files are outside Assets in ignored `build/task-m10-m3-m4-m6-verification/`.

## 3. Important implementation details

### M10

- Authentication and NetworkManager recreation have independent catches during Start; event subscription and the 15-second repeating heartbeat are still attempted after either fails. Existing specific Lobby/Relay catches retain their log levels; new general catches use `Debug.LogException`.
- Failed host start shuts down a listening NetworkManager and resets lobby/menu state, preserving the action flag's existing finally reset. Host deletion failures now clear the local lobby too. Unexpected membership-removal errors no longer skip the following game cleanup/network shutdown during ordinary host/client disconnect and lost-host handling.
- Heartbeat returns while NetworkManager is absent and observes both service and unexpected ping errors. Lobby search contains unexpected errors and returns the current known list.
- Missing Relay metadata or join failures attempt to leave the joined lobby. The failed-join helper additionally catches unexpected exceptions and clears lobby/menu state in finally. This small extension is necessary because otherwise an exception from recovery itself could escape the new join catches. The active join-by-ID caller no longer repeats JoinRelay's failure log.
- `RefreshCurrentLobby` is the one specified public signature change: `Task` becomes `Task<bool>`. Its existing UI await caller still compiles. Failed refresh preserves the last known lobby, and the start coroutine logs the requested warning before continuing its existing validation.
- GameMaster recreation exceptions call `AbortGameStart("game_master_create_failed")`. Existing N14 reasons, successful-path order, result-transition code, guards, timeouts and heartbeat interval remain. Join-by-code, the unused public DisconnectClient, authentication internals and user-facing error UI remain outside scope.

### M3, M4 and M6

- Lava retains all server, GameMaster, spawned-object, session and current-character checks. Only its private guard changes to `HashSet<PlayerController> m_killedCharacters`; `RemoveWhere(c => c == null)` uses Unity destroyed-object semantics before checking/adding the instance. Its serialized debug field and logs are retained.
- Only GameMaster's two IsHost checks become IsServer, with the requested comment update. MultiplayerManager's lobby-host checks are unchanged. This removes the input gate that excluded a server without a local client; it does not establish dedicated-server support.
- HP death now logs and returns for a missing session. The spawn-point dereference is guarded after the existing despawn call, preserving decrement/despawn/free/clear order and the fallback when DespawnCharacter returns early. DespawnCharacter and ServerKillPlayerByHazard are unchanged.
- Required Inspector setup: none.

## 4. Tests actually executed and results

- Compiled all runtime sources with Unity 6000.3.13f1's bundled Roslyn compiler and the current Assembly-CSharp response-file references, defines and analyzers, redirecting output outside Unity's assembly cache. **Exit 0**, no errors or new warnings. Only the existing CS0114 (`PlayerUI.OnDestroy`) and CS0414 (`GameMaster.m_cardResolveDelay`) warnings remain. Evidence: `compile.rsp`, `compile-results.txt`, `compile-exit.txt` in the ignored verification folder.
- Refreshed the modified scripts in the open Unity Editor. Its log confirms Netcode IL postprocessing, `Tundra build success`, and a successful assembly reload with the same two warnings and no `error CS`. Selected evidence: `editor-compile-evidence.txt`. The Editor's already-running ResultScene is not counted as a gameplay acceptance run for these tasks.
- Ran `verify.ps1`: **51 isolated scenarios passed**, using production method bodies with UGS/Unity/NGO doubles. To await async-void entry bodies in the harness only, their return types were changed to Task; the production signatures remain unchanged. Subscription/scheduling/service/network effects are simulated, not real SDK operations.
- M10 coverage: independent startup failures; specific and unexpected quit/delete/remove/heartbeat/create/search/join/refresh failures; missing NetworkManager; host rollback after allocation, code, transport, NGO start and lobby errors; retry after failure; missing Relay key; failure inside failed-join cleanup; Relay join/start/connect exceptions and timeout; normal create/join/search/refresh/start paths; cached refresh warning; GameMaster recreation abort; existing client/scene/timeout guards; network shutdown before simulated lobby scene loading; action/disconnect guard reset and Relay failure logging once.
- M3/M6 coverage: duplicate lava triggers cost one life; a second instance with the same ID dies; destroyed guard entries are pruned; lava authority/spawn/session/current-character guards; normal HP death, absent session, absent spawn, positive HP, and the spawn-point fallback after early despawn return. The harness includes the production DespawnCharacter and hazard methods; Unity destroyed-object comparison is simulated by the double.
- M4 coverage: a simulated host and a server with IsHost false both accept/drain input; a pure client does neither; missing/unlistening NetworkManager still blocks Update. This is not a dedicated-server player test.
- Final diff review and `git diff --check` passed. Static checks confirm no IsHost remains under InGame, MultiplayerManager's lobby-host checks are retained, and its JoinLobbyByJoinCode, unused DisconnectClient, LeaveMatchToResultScene and CleanupAfterDisconnect method bodies are identical to the saved pre-task source. No asset changes are present; prior N20/M14/M8 work is retained.

## 5. Remaining risks / checks not executed

- No real offline UGS/Lobby/Relay fault run, two-player gameplay, physics/lava/recycled-ID test or player build was run for these tasks. Source doubles do not establish real callback timing, remote membership cleanup, NGO shutdown timing or Unity destroyed-object lifetime behavior.
- During an outage, attempted remote lobby deletion/player removal may fail; local state is still released. The task adds no retries/backoff, and remote service cleanup can rely on normal expiry when host heartbeats stop.
- Existing cleanup/result/respawn logic and unrelated tasks are not redesigned. Lobby refresh deliberately continues with cached data after failure. Existing known compile warnings remain.

## 6. Manual Unity Editor verification

1. ParrelSync host/client: create, search, join, start, play a round and leave, then create/join again. Check no new errors and that both lobby panels/statuses match their state.
2. With networking unavailable, try create and lobby search; verify contained errors, the search panel and no listening host. Restore networking and create successfully. Host a lobby through a brief outage longer than 15 seconds; check heartbeat failures remain contained. Join a stale/deleted lobby entry and check there is no retained membership/listener.
3. Temporarily throw at the start of Bootstrap.RecreateGameMaster as described in M10. Verify the new abort reason, network shutdown and host/client return to LobbyScene, then remove only that temporary edit.
4. On both peers' own action turns, move one tile at a time; outside-turn movement must remain rejected. Check host mode is unchanged; a dedicated-server build is optional and outside these tasks.
5. Kill ponies through lava, repeated Iceshard damage and the Editor debug HP key. Each normal death must cost one life, despawn once and allow a respawn, including reuse of the same block/spawn point.
6. Temporarily lower NetworkIdRecycleDelay to one second in the NetworkManager prefab via the Editor, wait before respawning and confirm the old/new IDs match. Drop the new pony into lava and verify another single death. Repeat if IDs differ, then restore only the temporary prefab edit. Review the Console and run a player build.
