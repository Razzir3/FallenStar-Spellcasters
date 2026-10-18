# RESEARCH-H6C: Can a disconnected player rejoin a running match?

- **Date:** 2026-10-18
- **Related:** [Issue Report #2 → H6](../Reports/2026-10-18_IssueReport-2.md), [TASK-H6 (chosen: forfeit)](../Tasks/TASK-H6_ForfeitOnOpponentDisconnect.md)
- **Status:** research only. This is not an implementation task, and no code was changed.
- **Baseline:** `44d858f`
- **Method:** I read the project code, the prefab YAML, and the NGO 2.11.2 and Unity Transport sources in `Library/PackageCache` (read only). Nothing was run. Statements marked ❓ need an experiment (§7).

## 1. Question

Instead of forfeiting (H6-B), can the match **pause** when a client drops and **resume** when the same player reconnects within N seconds? What would that take in this project?

## 2. Short answer

**Technically possible, but large and high-risk for the current codebase.** Netcode doesn't keep a client's identity or owned objects across a reconnect. Almost every piece of state that would have to survive is tied to the client ID or owned by the client:
- the session
- the hand list
- the character
- the blocks
- the team slot
- hidden card data

A working reconnect needs about 6–8 coordinated changes across `GameMaster`, `SessionManager`, `PlayerSession`, `MultiplayerManager`, 3 prefabs, the NetworkManager prefab and the lobby UI.

**Recommendation:**
- Ship H6-B (forfeit) first.
- Build reconnect later **on top of it**: the forfeit becomes the "reconnect window expired" branch, so none of the H6-B work is thrown away.
- Prerequisites: H1, H5, H6-B, H7, and U2/U6 resolved.

## 3. Verified facts

| # | Fact | Source |
|---|---|---|
| F1 | NGO version is **2.11.2**; Unity Transport is the default UTP | `Library/PackageCache/com.unity.netcode.gameobjects@…/package.json` |
| F2 | When a client disconnects, the server **despawns and destroys** that client's player object and **all objects it owns**, unless `DontDestroyWithOwner` is set. With the flag set, ownership is removed (it goes back to the server) instead. | NGO `Runtime/Connection/NetworkConnectionManager.cs:1357-1405` |
| F3 | `DontDestroyWithOwner: 0` on every project prefab: `PonyBase` (`:214`), `BlockBody` (`:140`), `PlayerSessionPrefab` (`:58`), `CardPrefab`, `SpawnPoint`, `GameMasterPrefab`, `SessionManager`, `PlayerUIPrefab`. **This answers U4.** | prefab YAML |
| F4 | Ownership in the project: the character uses `SpawnAsPlayerObject(clientId)` (`GameMaster.Spawn.cs:53, 150`), blocks use `SpawnWithOwnership(clientId, true)` (`:274`), the session uses `SpawnWithOwnership(clientId)` (`SessionManager.cs:231`), and cards are **server-owned** (`Spawn()`, `GameMaster.Card.cs:272, 302`) | code |
| F5 | So on a drop, **the character, all blocks and the PlayerSession are destroyed by NGO.** `SessionManager.OnClientDisconnected` also despawns the session explicitly. The session's `m_handCards`, `m_boardPlayCards`, `m_boardDiscardCards`, `m_lifeCurrent` and `TeamId` are lost with it. Cards survive, but their `m_ownerId` points to a dead session ID. | F2–F4, `SessionManager.cs:156-192` |
| F6 | A reconnecting client gets a **new client ID**. Nothing public in NGO lets you reuse one. Everything keyed by client ID would need remapping: `SessionManager.m_playerSessions`, `m_gameSlots[].m_playerId`, `SessionData.ClientId`, `PlayerSession.ClientId`. | NGO design; `SessionManager.cs:59-71, 226-237` |
| F7 | A player object can only be (re)assigned by spawning with `SpawnAsPlayerObject`. `NetworkClient.AssignPlayerObject` is `internal`. `NetworkObject.ChangeOwnership(newId)` is public and works for non-player objects (session, blocks). | NGO `NetworkClient.cs:120`, `NetworkSpawnManager.cs:125-143`, `NetworkObject.cs:2076` |
| F8 | No connection approval: `ConnectionApproval: 0`. There is currently no way to tell the server who a joining client is. | `NetworkManagerPrefab.prefab:58` |
| F9 | Identity source: UGS anonymous auth `PlayerId`. Nothing in `Assets/Scripts` calls `SingOut*`, so the ID is stable per auth profile. In the Editor the profile is `"Editor_" + hash(dataPath)` (`AuthManager.cs:134-150`), so it's stable per ParrelSync clone. | grep, `AuthManager.cs` |
| F10 | Hidden information: `NetworkCard.m_cardData` is **not** networked. It's delivered by `SendCardDataClientRpc`: targeted to the owner on draw (`GameMaster.Card.cs:162`), broadcast on board placement (`PackageManaging.cs:203, 213`). A reconnecting client would see its hand as blank cards until this is re-sent. | code |
| F11 | Transport timeouts: `m_DisconnectTimeoutMS: 30000`, `m_HeartbeatTimeoutMS: 500`, `m_MaxConnectAttempts: 60` × `m_ConnectTimeoutMS: 1000`. A hard drop (crash, cable) is noticed after about **30 s**; a graceful quit at once. | `NetworkManagerPrefab.prefab:95-98`, UTP `NetworkParams.cs` |
| F12 | Lobby and Relay: the host creates a public lobby with `MaxPlayers = 2` and the Relay `joinCode` in its public data (`MultiplayerManager.cs:460-490`). The Relay allocation is for **1 client** (`m_relayClientCount = 1`, `:39, 514`). The lobby player data does **not** carry the Relay allocation ID (it's only logged, `:518`), so the Lobby↔Relay automatic disconnect integration isn't used. | code |
| F13 | On a graceful client disconnect, the client calls `RemovePlayerAsync` itself (`HandleLostConnectionToHost` `:260`, `DisconnectClientInternal` `:381`). It then goes through `CleanupAfterDisconnect` → `LoadLobbyScene` (`:1193-1220`). There's no cached "last match" and no "Rejoin" UI. | code |
| F14 | When the server finishes loading `GameScene` (`OnLoadEventCompleted`, `:1050-1085`), it **recreates GameMaster and starts a new game**. A late joiner must not retrigger this. | code |
| F15 | `SessionManager.OnClientConnected` always calls `CreateSession(newClientId)` (`SessionManager.cs:145-152`), which would create a **third** session instead of re-binding the old one | code |
| F16 | The game loop has no pause concept. Phases poll `m_timer` against fixed durations, and `IsAnyPlayerDead()` would treat the missing character as a death and start `RespawnWait` (H6 §2). | `GameMaster.GameLoop.cs` |

## 4. What would have to change (design sketch)

| Step | Change | Touches |
|---|---|---|
| R1 | **Keep the state alive.** Set `DontDestroyWithOwner = true` on `PlayerSessionPrefab`, `PonyBase` and `BlockBody`, so F2's ownership-removal branch applies. Stop `SessionManager.OnClientDisconnected` despawning the session **while a match is running**; mark it disconnected instead (new field). | 3 prefabs (needs owner approval per `AGENTS.md`), `SessionManager` |
| R2 | **Pause the loop.** On a drop, `GameMaster` enters a new phase appended to `GamePhase` (e.g. `WaitingReconnect`) and freezes the current phase and its timer. After N seconds, take the H6-B forfeit path. | `GameMaster` (loop pause/resume is the hardest part: every phase coroutine needs an "is paused" check, or the runner from H1 must support suspension) |
| R3 | **Identify the returning player.** Enable connection approval and send `AuthenticationService.Instance.PlayerId` in the connection payload. Store each session's `PlayerId` at match start. Approve a join during a match only if the payload matches a disconnected session **and** that player is a member of the lobby. Lobby membership requires an authenticated lobby join, which limits spoofing to lobby members, and there are only 2 of those. | NetworkManager prefab, `MultiplayerManager` (approval callback), `PlayerSession`/`SessionData` (new field) |
| R4 | **Rejoin entry point.** The client caches the lobby ID and join code. The lobby UI offers "Rejoin match" (new en/ru/ja keys). Flow: `JoinLobbyById` again (after F13 the player isn't in the lobby any more) → `JoinRelay(joinCode)` → `StartClient` with the payload. | `MultiplayerManager`, lobby UI, localization |
| R5 | **Rebind on the server.** `OnClientConnected(newId)` finds the disconnected session by `PlayerId` instead of creating one (F15). It then: remaps `m_playerSessions`, `m_gameSlots` and `SessionDataList`; sets `session.ClientId.Value = newId`; calls `session.NetworkObject.ChangeOwnership(newId)` and `ChangeOwnership` on each block; and for the character, either `ChangeOwnership` (input works, because `InputServerRpc` requires ownership and C3 resolves the character through the session) or despawn and re-`SpawnAsPlayerObject(newId)` at the same position (F7). | `SessionManager`, `GameMaster.Spawn` |
| R6 | **Resync the client.** Re-send `SendCardDataClientRpc` to the new client for every card in its hand and on both boards (F10). The client side must handle `OnGainedOwnership`: `PlayerSession` registers `SessionManager.LocalPlayer` only in `OnNetworkSpawn` when `IsOwner`, so it also needs to register on gaining ownership. `PlayerUI` and `CardWorldViewManager` must bind to it. Make sure `OnLoadEventCompleted` doesn't re-run the game start for a late joiner (F14, U6). | `PlayerSession`, `PlayerUI`, `CardWorldViewManager`, `MultiplayerManager` |
| R7 | **Resume:** restore the paused phase and its remaining time, then continue | `GameMaster` |

## 5. Risks

- **Regression area is large:** the disconnect flows (H7), team slots, lava kill-tracking by network ID (M3 with `RecycleNetworkIds: 1`), the nested `SpawnPoint` NetworkObject inside `BlockBody` with `DontDestroyWithOwner` (U2), and every client-side "is local player" check.
- **Hidden-information leaks:** a mistake in R6 could send the opponent's hand data to the reconnecting client.
- **Detection delay:** with a 30 s transport timeout (F11), a hard drop leaves the opponent waiting for 30 s + N s. Lowering the timeout risks false disconnects on bad networks.
- **Relay capacity:** with an allocation for 1 client, it's unclear whether the old connection's slot is free in time for the rejoin (❓ E1).
- **Host loss can't be recovered** in any variant. NGO has no host migration.

## 6. Effort estimate

About **6–8 tasks** (R1–R7 plus a test pass), each medium-sized, plus prefab changes that need owner approval. That's roughly the size of H1 + H5 + H6-B + H7 put together. Expect several ParrelSync iterations for R5/R6.

## 7. Experiments needed before any implementation

| ID | Experiment | Answers |
|---|---|---|
| E1 | Host a lobby, connect a client, kill the client, then rejoin with the same join code at t = 0 s, 10 s, 35 s. Does Relay accept it with a 1-client allocation? | F12 capacity ❓ |
| E2 | After a client crash (no `RemovePlayerAsync`), how long does it stay in `Lobby.Players`? Can the same `PlayerId` rejoin a "full" lobby? | Lobby behaviour ❓ |
| E3 | A client joins while the host is already in `GameScene`. Which scene events fire on host and client (`Synchronize` vs `LoadEventCompleted`)? Does `OnLoadEventCompleted` run again? | F14, U6 |
| E4 | Prototype `ChangeOwnership` of a `PlayerSession` to a new client. In what order do `OnGainedOwnership` and the `NetworkList` initial sync happen on that client? | R5/R6 |
| E5 | With `DontDestroyWithOwner = 1` on `BlockBody`, does the nested `SpawnPoint` NetworkObject survive the owner's drop correctly? | U2, R1 |

## 8. Recommendation

1. Keep **H6-B** as the shipped behaviour.
2. Run E1–E3 (cheap: about an hour with ParrelSync) before deciding to invest in reconnect.
3. If reconnect is still wanted, write R1–R7 as tasks **after** H1, H5, H6-B and H7 are merged. R2 (pause) should be designed together with H1's runner.

## Sources

- Project:
  - `Assets/Scripts/MultiplayerScripts/{SessionManager,MultiplayerManager,AuthManager}.cs`
  - `Assets/Scripts/InGame/{PlayerSession,GameMaster}.cs`
  - `Assets/Scripts/InGame/GameMaster/{GameMaster.Spawn,GameMaster.Card,GameMaster.PackageManaging,GameMaster.GameLoop}.cs`
  - `Assets/Prefab/{NetworkManagerPrefab,PlayerSessionPrefab,CardPrefab,GameMasterPrefab,SessionManager}.prefab`
  - `Assets/Prefab/BoardPrefab/{PonyBase,BlockBody,SpawnPoint}.prefab`
- Packages (read only):
  - `Library/PackageCache/com.unity.netcode.gameobjects@beeaefb722f7/Runtime/{Connection/NetworkConnectionManager.cs, Connection/NetworkClient.cs, Spawning/NetworkSpawnManager.cs, Core/NetworkObject.cs}`
  - `Library/PackageCache/com.unity.transport@ed7eca02732f/Runtime/NetworkParams.cs`
