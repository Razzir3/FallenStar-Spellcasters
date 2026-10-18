# Game Loop, Phases and Match State

- **Audit date:** 2026-10-18
- **Baseline:** commit `1f6d2f1` plus the uncommitted working tree
- **Method:** static code reading only. No Play Mode run.
- Tags **[Confirmed] / [Unfinished] / [Unknown] / [Risk]** are defined in [Architecture.md](Architecture.md).
- Line numbers refer to the current working-tree files.

**Primary sources:**
- `InGame/GameMaster.cs`
- `InGame/GameMaster/GameMaster.GameLoop.cs`
- `GameMaster.PackageManaging.cs`
- `GameMaster.Processing.cs`
- `GameMaster.Card.cs`
- `GameMaster.Spawn.cs`
- `InGame/PlayerSession.cs`
- `InGame/PlayerController.cs`
- `InGame/LavaKillZone.cs`
- `MultiplayerScripts/MultiplayerManager.cs`
- `MultiplayerScripts/SessionManager.cs`
- `Bootstrap.cs`
- `Manager/SceneFlowManager.cs`
- `UI/PlayerUI.cs`
- `UI/GameAnnouncerUI.cs`
- `UI/ConfirmTargetButtonUI.cs`
- `UI/ConfirmSendButton.cs`

---

## 1. Scene flow

| Step | Trigger (UI → method) | Scene call | Status |
|---|---|---|---|
| App start | `LoginScene`/`AppRoot`: `MultiplayerManager.Start` → `AuthManager.Initialize` → `Bootstrap.RecreateNetworkManager` → `SubscribeNetworkEvents` → `InvokeRepeating(HearthbeatLobby, 15, 15)` | — | [Confirmed] |
| Login | `LoginUI.OnClickLoginAnonymous` → `AuthManager.SingInAnonymous` (in the Editor, `SwitchProfile("Editor_"+hash(dataPath))` gives ParrelSync clones distinct ids) | `SceneFlowManager.LoadMainMenuScene` → `SceneManager.LoadScene` | [Confirmed] |
| Steam login | `LoginUI.OnClickLoginSteam` → `AuthManager.SignInSteam` only logs "not ready" | — | [Unfinished] |
| Menu → Lobby | `MainMenuUI.OnClickLobby` | `LoadLobbyScene` (offline) | [Confirmed] |
| Create lobby | `LobbyControlUI.OnClickCreateLobby` → `MultiplayerManager.StartHost` → `RecreateNetworkManager` → `CreateRelay` (`StartHost()`) → `LobbyService.CreateLobbyAsync("MyLobby", 2, joinCode)` | — | [Confirmed] |
| Join lobby | `LobbyListUI.OnClickRefreshLobbyList` → `FindAvailableLobbies`. Then `LobbyListItemUI.OnClickJoin` → `LobbyListUI.OnJoinLobbyClicked` → `MultiplayerManager.JoinLobbyById` → `JoinRelay` (`StartClient()`, waits ≤10 s for connect) | — | [Confirmed] |
| Join by code | `MultiplayerManager.JoinLobbyByJoinCode`. `m_joinCodeInput` is never assigned and there is no UI caller | — | [Unfinished] |
| Lobby polling | `CurrentLobbyUI.RefreshLobbyLoop` runs every 2 s: `RefreshCurrentLobby`, and shows the Start button for the host when the lobby has 2 players | — | [Confirmed] |
| Start match | `CurrentLobbyUI.OnClickStartGame` → `MultiplayerManager.StartGameScene` → coroutine `StartGameSceneWhenReady`, which waits until 2 NGO clients connect (≤10 s) and re-checks the lobby | `SceneFlowManager.LoadGameSceneNetwork` → `NetworkManager.SceneManager.LoadScene("GameScene", Single)` | [Confirmed] |
| Game scene ready | `MultiplayerManager.OnLoadEventCompleted` (all peers) → server only: `Bootstrap.RecreateGameMaster` → `StartGameAfterSceneReady` | — | [Confirmed] |
| Settings | `SceneFlowManager.LoadSettingsScene` targets `"SettingsScene"`, which does not exist. No caller found | — | [Unfinished] |
| End of match → Result | Not implemented | — | [Unfinished] |
| Disconnect / connection loss | `DisconnectMenuUI` / `LobbyControlUI.OnClickLeaveLobby` → `MultiplayerManager.Disconnect`, or NGO `OnClientDisconnectCallback` → `HandleLostConnectionToHost` → `CleanupAfterDisconnect` | `LoadLobbyScene` (offline) | [Confirmed] |

`MultiplayerManager.Update` toggles `DisconnectMenuUI` with Tab while in lobby or game. **[Unknown]** `DisconnectMenuUI` is only placed in the disabled `SampleScene`/`MultiplayerScene`, so Tab probably does nothing in `GameScene`/`LobbyScene`. `DisconnectButtonUI` is not referenced by any scene or prefab.

---

## 2. Match bootstrap (server)

`MultiplayerManager.StartGameAfterSceneReady` does the following:

1. It waits ≤10 s for `SessionManager.Instance`. `SessionManager` is in-scene placed in `GameScene`. Its `OnNetworkSpawn` calls `CreateSessionsForAlreadyConnectedClients`, which calls `CreateSession(clientId)` for each connected client:
   - Instantiate `PlayerSessionPrefab`.
   - `GetFreeSlot` assigns slot index → `TeamId`, from 0 to 3, or −1 when full.
   - `Initialize`: 3 lives.
   - `SpawnWithOwnership(clientId)`.
   - Add to `m_playerSessions` and `SessionDataList`.

   Clients rebuild `m_playerSessions` from the NetworkList `SessionDataList`.
2. It waits ≤10 s until `m_playerSessions.Count >= 2`.
3. It calls `GameMaster.SpawnPlayers()` (`GameMaster.Spawn.cs:210`). For each session with `SessionRole.Player`:
   - `CreateBlock`: 25 blocks around the team's scene `SpawnPoint` (5×5, 3 m pitch), each `SpawnWithOwnership(client)`. Then `BlockController.Initialize`, then `PlayerSession.AddBlock`, which re-initializes the child `SpawnPoint` team.
   - `SpawnCharacter`: random free scene `SpawnPoint` for the team, then `SpawnAsPlayerObject(client)`, `SessionObjectId`, `CharacterObjectId`, `SetSpawnPoint`, and finally `MovePlayeToSpawnPoint`, which moves the character 3 units up.
   - Finally it runs `BindPlayerCardWorldAreas` and sets `MultiplayerStatus.InGameStatus`.
4. It calls `GameMaster.StartNewGame()` (`GameMaster.cs:364`):
   - Stop the previous coroutine.
   - `DespawnAllCards`.
   - Reset all state and NetworkVariables.
   - Collect players from `SessionManager.m_playerSessions`. If the count is not exactly 2, warn and **return without starting**.
   - `SetOponent` on both players.
   - `CreateDeck`.
   - `StartCoroutine(GameLoop())`.

`GameMaster.Awake` runs on instantiation, after `GameScene` is loaded. It calls `FindSpawnPoints`, `HideSpawnPoints` and `FindPlayerCardWorldAreas`, and only finds scene objects that exist at that moment. That means the two scene spawn points, team 0 and team 1, but not the block spawn points. **[Confirmed]**

`SpawnPanelUI.OnClickSpawnPlayers` also calls `SpawnPlayers()`, but that panel exists only in disabled legacy scenes. **[Confirmed]**

---

## 3. Phase state machine (server coroutine)

Enum `GamePhase` (`GameMaster.cs`): `None, RoundBreak, DrawCards, CardSelection, PlayerAction, ResolveCards, RespawnWait, RoundEnd`.

```
GameLoop()  (GameLoop.cs:44)
 while !IsGameFinished():
   RoundBreakPhase      phase=RoundBreak; m_timer 0→m_roundBreakTime (5 s)
   DrawCardPhase        (phase NOT changed – stays RoundBreak)
                        m_startHandCards(2) × players: DrawCard, wait m_cardDrawDelay (0.8 s)
   CardSelectionPhase   phase=CardSelection; new PlayerTurnSelection per player;
                        wait until all confirmed or m_cardSelectionTime (60 s);
                        BuildTurnOrderByCardCost (descending total cost of play+discard)
   PlayerTurnPhase      for each player in m_turnOrder:
                          m_activePlayer = player
                          PlayerActionPhase(player)        (≤ m_playerActionTime 60 s)
                          if IsGameFinished → stop
                        m_activePlayer = null
 Debug.LogError("Game finished. You won/lost")   ← end of match (server log only)
```

### Mana at match start and RoundBreak (MANA1, 2026-10-19)

- `StartNewGame` initializes both spawned sessions after the two-player check: capacity = `Clamp(m_baseMana, 0, m_maxMana)` and current = capacity. Defaults are base 2, growth step 2, cap 20.
- At the start of `RoundBreakPhase`, after assigning `GamePhase.RoundBreak`, the server calls `ApplyRoundManaGrowth` for each player. Round one only refills; later rounds increase capacity by the step up to the cap, then refill current to capacity. The local first-round flag resets on each new `GameLoop`.
- `ApplyRoundManaGrowth` is the single extension point for changing the round-growth/refill model. `AddMana(player, delta)` is a server-only helper for later spending/effects, clamped to `[0, capacity]`.
- `PlayerSession.m_manaCapacity` and `m_manaCurrent` are server-written NetworkVariables readable by everyone. Mana is not written in `PlayerSession.Initialize`; turn order is unchanged. Selection spending is described below; mana display belongs to MANA3.
- These paths were checked statically; host/client replication and round progression still require Play Mode verification.

### Mana validation at selection confirm (MANA2)

- The client refuses additional play cards whose combined cost exceeds current mana, and refuses cards whose data has not arrived. Deselection and free discards keep their existing behavior.
- The server first filters card IDs using the existing ownership, hand and duplicate checks, still recording invalid IDs. It then checks only the accepted play-card cost. A valid first confirm spends that cost through `AddMana` and marks the selection confirmed before the caller writes the board. Later confirms are ignored.
- An over-budget confirm leaves mana and board unchanged, clears both server selection lists, remains unconfirmed and records `ManaOverBudget`. The owner-targeted `SelectionRejectedClientRpc` clears local selection/target state and UI highlights. The localized message is deferred to MANA3.
- Rejection resets the shared selection timer to zero only while both per-player limits permit it: one reset per selection phase and three per match by default. Phase counters clear in `CardSelectionPhase`; match counters clear in `StartNewGame`. Further over-budget attempts still reject and record suspicion without resetting the timer.
- Selection still ends only after both valid confirms or timeout. A player without a valid confirm plays nothing and spends nothing. Turn order still includes both play and discard costs; casting is unchanged.
- Thirty focused local-host Play Mode assertions passed for gating, spending, rejection, owner-side clearing, timer limits, valid retries and timeout behavior. Remote-peer rejection delivery and full-match behavior still require verification.

### 3.1 PlayerActionPhase (`GameLoop.cs:91`) [Confirmed]
Each frame, until timeout or `m_activePlayerConfirmedAction` with an empty queue:
1. If `m_externalDeathDetected || IsAnyPlayerDead()`, it breaks out when the game is finished. Otherwise it runs `WaitForRespawnIfNeeded()`, then restores `PlayerAction` and the active player.
2. It drains `m_cardInputQueue`. For each package it:
   - re-checks the card exists, the card owner is the active player, the card is in `m_boardPlayCards`, and the data and activities are non-null
   - checks the target count ≥ number of activities, which is always 4
   - for each activity: a zero target is allowed only for `None/All*/Random*`, then `CheckActivity`
   - on success, runs `ProcessActivity` for each activity. After each one it checks for death and runs the respawn wait.
   - runs `MoveCardOnGraveyard`
3. At phase end it calls `ClearDiscardedCards(player)` and `ClearPlayedCards(player)`, which send all board cards to the graveyard.

### 3.2 Respawn (`WaitForRespawnIfNeeded`, `GameLoop.cs:488`) [Confirmed]
- A player is dead when `GetCharacter() == null` and `TeamId != -1`.
- `FindDeadPlayer` picks the first dead player that still has lives.
- It sets `phase = RespawnWait` and `m_waitingRespawnPlayer`, then waits up to `m_respawnWaitTime` (30 s) for `m_respawnConfirmed`.
- If the chosen block is still valid it uses its child `SpawnPoint`. Otherwise it picks a random valid own block.
- With no valid blocks, it sets `m_lifeCurrent = -1`, which permanently eliminates the player.
- Then `RespawnCharacter` spawns a new character, and the phase becomes `ResolveCards` until the caller restores `PlayerAction`.

### 3.3 Death and finish conditions [Confirmed]
- **HP ≤ 0** (`UpdatePlayerHP`, `Processing.cs:319`): `m_lifeCurrent -= 1`, `DespawnCharacter`, free the spawn point, `CharacterObjectId = 0`.
- **Lava** (`LavaKillZone.TryKillPlayer`, server): `ServerKillPlayerByHazard` sets HP to 0, then runs `UpdatePlayerHP` and sets `m_externalDeathDetected`.
- **Block HP ≤ 0** (`UpdateBlockHp`): `PlayerSession.DeleteBlock`, then `Despawn`. A character standing on it is expected to fall into the lava. That is physics, **[Unknown]** at runtime.
- **Game finished** (`IsGameFinished`, `GameLoop.cs:646`): at most one player with `TeamId != -1` has a character or lives > 0.
- **[Unfinished]** README's "destroy all enemy blocks" win rule is not implemented directly. Losing all blocks only matters at the next respawn, when no spawn point exists and the life is set to −1.

### 3.4 Unused or unreached phases
- `GamePhase.DrawCards` is never assigned. **[Confirmed]**
- `GamePhase.RoundEnd` is never assigned. **[Confirmed]**
- `GamePhase.ResolveCards` is only set as a transient value at the end of `WaitForRespawnIfNeeded`, and inside `ResolvePlayerCardsPhase`, which is never called (`//ToDo`). **[Unfinished]**
- `GameAnnouncerUI` and the localization tables do have keys for all 8 phases.

### 3.5 Timer
- `m_timer` is a server-written `NetworkVariable<float>`, incremented by `Time.deltaTime` every frame in each phase and reset at the start of each phase and each respawn wait.
- Clients show `(int)m_timer` as **elapsed** time, not remaining (`GameAnnouncerUI`). **[Confirmed]**

---

## 4. Network message paths

| Action | Client side | RPC | Server handler | Validation |
|---|---|---|---|---|
| Move (WASD) | `PlayerController.Update` → `SendMovementInput`. Requires `CanPlayerMove` **on the client** | `PlayerController.InputServerRpc(PlayerControlPackage)` (owner-only) | `GameMaster.HandlePlayerControlPackage` (enqueue, `IsHost` only) → `Update` → `InputQueueUpdate` → `MoveInputProcessing` + `HPDeltaProcessing` | Direction must be one axis step. **No phase check, and the target client comes from payload `ClientId`.** [Risk] |
| Debug damage (Space) | `SendMovementInput` sets `s_hpDelta = -10` | same | `HPDeltaProcessing` applies any delta | **None.** [Risk] |
| Select cards to play/discard | `PlayerUI.OnCardClicked` (LMB) / `OnCardAlterClicked` (RMB) → `PlayerSession.SelectCardToPlay/Discard` (local lists) | — | — | Local only |
| Confirm selection | `PlayerUI.ConfirmTurn` → `PlayerSession.ConfirmTurn` | `PlayerSession.ConfirmTurnServerRpc(play[], discard[], respawnBlock)` | `HandleConfirmTurn` (`PackageManaging.cs:166`), `CardSelection` branch: `HandleConfirmSelectionTurn` (cost and turn order) **and** a raw write of the IDs into `m_boardPlayCards`/`m_boardDiscardCards` | The sender must own the session. The board write is not checked against hand, owner, duplicates or the already-confirmed flag. [Risk] |
| Target a played card | World click → `PlayerSession.SelectObject` (raycast) → `ConfirmTargetButtonUI.ConfirmSelect` → `PlayerSession.TryConfirmTarget` (fills `m_currentCardControllPackage`) | — | — | Local |
| Cast card | `ConfirmSendButtonUI1.TryConfirmSend` → `PlayerSession.TryHandlePackage` → `CheckPackage` | `PlayerSession.CardInputServerRpc(CardControllPackage)` | `HandleCardControlPackage` (enqueue) → consumed by `PlayerActionPhase` | Phase, active player, card owner, card on board. Then target checks in the loop. |
| End turn | `PlayerUI.ConfirmTurn` during `PlayerAction` | `ConfirmTurnServerRpc` | `HandlePlayerActionConfirm` sets `m_activePlayerConfirmedAction` | Must be the active player |
| Respawn block | `ConfirmTargetButtonUI` during `RespawnWait` → `TryConfirmRespawnBlock`, then `ConfirmTurn` | `ConfirmTurnServerRpc(…, blockId)` | `HandlePlayerRespawnPoint` | Must be the waiting player, own block, spawned, with a spawn point |
| Card identity | — | `GameMaster.SendCardDataClientRpc(cardId)`: on draw it goes to the owner only, and on board placement it goes to all clients | `NetworkCard.RepairCardData` → `CardDatabase.GetCard` | — |

Notes:
- `ToPlayerControlPackage` / `SendToClientRpc` are an echo with an empty body (debug).
- `PlayerController.ReceiveValidatedInput` and its queue are never used. **[Unfinished]**
- **[Unknown]** `HandlePlayerActionConfirm` compares `PlayerSession != NetworkBehaviourReference` through an implicit conversion. Equality semantics need a runtime check.

---

## 5. Client state display (per frame or per event)

- `PlayerUI.Update` lazily binds to `SessionManager.LocalPlayer`, subscribes to the three NetworkLists and `m_gamePhase`, and writes the HP and life labels every frame.
- `PlayerUI.UpdateCards` destroys and rebuilds every `CardViewUI` on each list or phase change.
- `CardWorldViewManager` rebuilds the board view layout on every board-list change and every `NetworkCard` location, rotation or face change (`RefreshAllSessions`).
- `GameAnnouncerUI.Update` builds the phase, timer and active-player text every frame through new `LocalizedString` instances.

---

## 6. Known risks in the loop

These are documented, not fixed. Each needs separate verification.

| # | Risk | Location | Consequence |
|---|---|---|---|
| L1 | Any exception inside the coroutine stops `GameLoop` permanently | the coroutine as a whole | Match freezes with no recovery |
| L2 | `DrawCard` uses `m_deck.ElementAt(0)` with no empty check, and the graveyard is never recycled | `GameMaster.Card.cs:151` | L1 once the deck (~80 cards) is exhausted |
| L3 | Block heal uses `_targetCharacter.m_hpMax` (null for a block target) | `Processing.cs:209` | NRE leads to L1 when a heal targets a block. No current card does this. |
| L4 | `CheckActivity` dereferences `_ownerPlayer` before its null check | `Processing.cs:69` | NRE leads to L1 |
| L5 | `FillCardSection` writes `_nonUniqueList[i]` on a possibly empty list for `UltraRare` | `Card.cs:262` | `StartNewGame` throws once an UltraRare card exists. No current card is UltraRare. |
| L6 | `WaitForRespawnIfNeeded` resets the shared `m_timer` inside `PlayerActionPhase` | `GameLoop.cs:530` | The active player's turn timer is altered after a respawn |
| L7 | After a lethal activity, `break` only exits the activity `for` loop | `GameLoop.cs:272` | The match can idle up to 60 s before ending |
| L8 | The end of the match only logs on the server. `m_isLocalPlayerLost` is host-relative and is not networked | `GameLoop.cs:44-62`, `FindLostPlayer` | Clients never see a result. Players remain in `GameScene`. |
| L9 | An opponent disconnect is not handled by `GameMaster`. Destroyed sessions stay in `m_players` | `MultiplayerManager.OnClientDisconnected`, `SessionManager.OnClientDisconnected` | Undefined end state. See [Dependencies.md](Dependencies.md#5-regression-points). |
| L10 | `IsGameFinished()` has the side effect `FindLostPlayer()`, which sets `m_externalDeathDetected` | `GameLoop.cs:679` | Hidden coupling. The death flag is re-raised by every finish check. |
| L11 | `CardSelectionPhase` selects but does not move cards. Unselected cards stay in hand indefinitely, and there is no hand limit | `CardSelectionPhase`, `DrawCardPhase` | Unbounded hand growth (design question) |
| L12 | `LavaKillZone.m_killedCharacterIds` is never cleared, with `RecycleNetworkIds: 1` (120 s delay) | `LavaKillZone.cs` | A respawned character with a recycled id may be immune to lava |

## 7. Unknown, needs Play Mode

- Whether `NetworkBehaviourReference` assigned `null` (`m_activePlayer.Value = null`, `GameAnnouncerUI.GetTargetSession`) is accepted by NGO 2.11 or throws.
- Physics of falling after a block despawns, and the lava trigger geometry in `GameScene`.
- Timing between `SendCardDataClientRpc` and the client-side spawn of the card object (the warning path "NetworkCard … was not found").
- **Resolved during the audit:** `GameMasterPrefab.prefab` does not serialize any of the Timing, Movement or GameRules fields. Only `m_debugMode: 1` and the five prefab/database references are stored. The C# defaults documented above are therefore the effective values until the prefab is re-saved. **[Confirmed]** `m_debugMode` is enabled on the prefab, so verbose server logging is on.
