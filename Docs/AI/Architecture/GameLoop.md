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
| Create lobby | `LobbyControlUI.OnClickCreateLobby` → `MultiplayerManager.StartHost` → `RecreateNetworkManager` → `CreateRelay` (`StartHost()`) → `LobbyService.CreateLobbyAsync(GetShortPlayerId(), 2, options)` | — | [Confirmed] |
| Join lobby | `LobbyListUI.OnClickRefreshLobbyList` → `FindAvailableLobbies`. Then `LobbyListItemUI.OnClickJoin` → `LobbyListUI.OnJoinLobbyClicked` → `MultiplayerManager.JoinLobbyById` → `JoinRelay` (`StartClient()`, waits ≤10 s for connect) | — | [Confirmed] |
| Join by code | M11: `JoinLobbyByJoinCode` and `m_joinCodeInput` are retained as comments, excluded from compilation | — | Parked by owner decision; restoration requirements are in TASK-M11 |
| Lobby polling | `CurrentLobbyUI.RefreshLobbyLoop` runs every 2 s: `RefreshCurrentLobby`, and shows the Start button for the host when the lobby has 2 players | — | [Confirmed] |
| Start match | `CurrentLobbyUI.OnClickStartGame` → `MultiplayerManager.StartGameScene` → coroutine `StartGameSceneWhenReady`, which waits until 2 NGO clients connect (≤10 s) and re-checks the lobby | `SceneFlowManager.LoadGameSceneNetwork` → `NetworkManager.SceneManager.LoadScene("GameScene", Single)` | [Confirmed] |
| Game scene ready | `MultiplayerManager.OnLoadEventCompleted` (all peers) → server only: `Bootstrap.RecreateGameMaster` → `StartGameAfterSceneReady` | — | [Confirmed] |
| Settings | M12: `MainMenuUI.OnClickSettings` → `SceneFlowManager.LoadSettingsScene`; SettingsUI changes/persists locale; SettingsSceneButtonsUI returns to Main menu | Offline SettingsScene load; enabled after MainMenuScene | Implemented; en/ru/ja navigation and restart persistence checked |
| End of match → Result | Not implemented | — | [Unfinished] |
| Disconnect / connection loss | `DisconnectMenuUI` / `LobbyControlUI.OnClickLeaveLobby` → `MultiplayerManager.Disconnect`, or NGO `OnClientDisconnectCallback` → `HandleLostConnectionToHost` → `CleanupAfterDisconnect` | `LoadLobbyScene` (offline) | [Confirmed] |

`MultiplayerManager.Update` toggles `DisconnectMenuUI` with Tab while in lobby or game. **[Unknown]** `DisconnectMenuUI` is only placed in the disabled `SampleScene`/`MultiplayerScene`, so Tab probably does nothing in `GameScene`/`LobbyScene`. `DisconnectButtonUI` is not referenced by any scene or prefab.

### Lobby/network error containment (M10, 2026-10-20)

- Startup observes authentication and NetworkManager recreation errors independently, then still attempts event subscription and schedules the existing 15-second heartbeat. Quit, lost-host removal, host/client lobby removal, heartbeat, host start, lobby search, join-by-ID, Relay join and lobby refresh retain their specific service catches and also log unexpected exceptions.
- Failed host start shuts down a listening NetworkManager and clears the local lobby/menu status. A failed join attempts membership removal and clears local lobby state; exception paths also shut down a listening NetworkManager. The failed-join removal helper contains its own unexpected service exceptions so recovery can finish. Relay reports its failure once on the active join-by-ID path. Join-by-code and the unused DisconnectClient remain separate task/non-goal code.
- `RefreshCurrentLobby` returns `Task<bool>`: failures retain the cached lobby and return false. The startup coroutine warns before continuing the existing last-known-lobby and two-connected-client checks. `CurrentLobbyUI` continues to await the result without needing a signature change of its own.
- A thrown GameMaster recreation logs the exception and calls the existing N14 `AbortGameStart("game_master_create_failed")`. Existing timeout reasons, successful call order, result transitions, heartbeat interval and timeouts are retained. Remote cleanup during a service outage remains a best-effort request; local recovery does not establish immediate deletion/removal at UGS.

M11/M13 (2026-10-20): join-by-code is parked unchanged inside comments. Active host/list-join calls retain protocol role values Host/Client through constants, while shared player/lobby names use the first six player-ID characters (numeric fallback when empty). Viewers localize role labels, counts and unknown-name fallbacks using UI_table. Locale subscriptions are balanced; existing list/player prefabs rely on field initializers without asset edits. Real prefab checks passed in en/ru/ja using local sample Lobby/Player data; live service create/join regression remains manual.

---

## 2. Match bootstrap (server)

`MultiplayerManager.StartGameAfterSceneReady` does the following:

N14 (2026-10-19): the server rejects a GameScene load completion with any timed-out clients before recreating GameMaster. Missing/unlinked GameMaster checks call `AbortGameStart("game_master_missing")`. `AbortGameStart` logs the reason and delegates to `GameMaster.AbortMatch`, or directly to `Disconnect` when no GameMaster exists. This reuses host lobby deletion, network shutdown, cleanup and the offline return to `LobbyScene`; no match result is generated before play starts.

1. It waits ≤10 s for `SessionManager.Instance`. `SessionManager` is in-scene placed in `GameScene`. Its `OnNetworkSpawn` calls `CreateSessionsForAlreadyConnectedClients`, which calls `CreateSession(clientId)` for each connected client:
   - Instantiate `PlayerSessionPrefab`.
   - `GetFreeSlot` assigns slot index → `TeamId`, from 0 to 3, or −1 when full.
   - `Initialize`: 3 lives.
   - `SpawnWithOwnership(clientId)`.
   - Add to `m_playerSessions` and `SessionDataList`.

   Clients rebuild `m_playerSessions` from the NetworkList `SessionDataList`.
2. It waits ≤10 s until `m_playerSessions.Count >= 2`. Both waits abort through `AbortGameStart` on timeout (`session_manager_missing` / `not_enough_sessions`), or immediately when NetworkManager is missing, stops listening or has fewer than two connected clients (`player_left_before_start`). Disappearance of SessionManager during the second wait also aborts safely. The connection check is repeated before spawning, including when neither wait was needed.
3. It calls `GameMaster.SpawnPlayers()` (`GameMaster.Spawn.cs:210`). For each session with `SessionRole.Player`:
   - `CreateBlock`: 25 blocks around the team's scene `SpawnPoint` (5×5, 3 m pitch), each `SpawnWithOwnership(client)`. Then `BlockController.Initialize`, then `PlayerSession.AddBlock`, which re-initializes the child `SpawnPoint` team.
   - `SpawnCharacter`: random free scene `SpawnPoint` for the team, then `SpawnAsPlayerObject(client)`, `SessionObjectId`, `CharacterObjectId`, `SetSpawnPoint`, and finally `MovePlayeToSpawnPoint`, which moves the character 3 units up.
   - Finally it runs `BindPlayerCardWorldAreas` and sets `MultiplayerStatus.InGameStatus`.
4. It calls `GameMaster.StartNewGame()` (`GameMaster.cs:364`):
   - Stop the previous coroutine.
   - `DespawnAllCards`.
   - Reset all state and NetworkVariables.
   - Collect players from `SessionManager.m_playerSessions`. A missing SessionManager or a count other than exactly 2 logs and calls `AbortMatch("match_start_failed")`, then returns without starting.
   - `SetOponent` on both players.
   - `CreateDeck`.
   - `StartCoroutine(GameLoop())`.

`GameMaster.Awake` runs on instantiation, after `GameScene` is loaded. It calls `FindSpawnPoints`, `HideSpawnPoints` and `FindPlayerCardWorldAreas`, and only finds scene objects that exist at that moment. That means the two scene spawn points, team 0 and team 1, but not the block spawn points. **[Confirmed]**

`SpawnPanelUI.OnClickSpawnPlayers` also calls `SpawnPlayers()`, but that panel exists only in disabled legacy scenes. **[Confirmed]**

N14 leaves the H6 `HandlePlayerLeft` and all existing disconnect/result cleanup methods unchanged. A disconnect after the match loop starts still uses the established forfeit path. Isolated source checks passed for startup failures and normal startup; real host/client timing, lobby deletion and subsequent-match behavior still require Play Mode verification.

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

### Deck exhaustion and refill (H2, 2026-10-19)

- `DrawCardPhase` and DrawCard activities share `DrawCard`. It rejects a null session before modifying cards and removes null entries at the front of the deck.
- An empty deck calls server-only `RefillDeck` once per draw. The server first despawns spawned graveyard cards with destruction enabled, destroys unspawned graveyard objects, removes those cards from `m_spawnedCards` and clears the graveyard. It then calls `CreateDeck(m_refillDeckMaxRarity)` (default `Rare`). Cards in hands and on boards are retained.
- `CreateDeck` skips null database entries and rarities above its optional limit; the default `Unique` preserves the initial deck. Section limits, copy batches, spawn path and shuffle remain unchanged.
- A still-empty deck logs a warning and returns without drawing. A successful draw keeps the existing hand assignment, ownership and owner-targeted identity RPC. No new phase or yield is introduced.
- Isolated source checks passed for refill ordering, rarity ceilings, repeated cleanup, owner-targeted draws and empty pools. Actual network spawn/despawn replication and UI behavior still require host/client Play Mode checks.

### Mana at match start and RoundBreak (MANA1, 2026-10-19)

- `StartNewGame` initializes both spawned sessions after the two-player check: capacity = `Clamp(m_baseMana, 0, m_maxMana)` and current = capacity. Defaults are base 2, growth step 2, cap 20.
- At the start of `RoundBreakPhase`, after assigning `GamePhase.RoundBreak`, the server calls `ApplyRoundManaGrowth` for each player. Round one only refills; later rounds increase capacity by the step up to the cap, then refill current to capacity. The local first-round flag resets on each new `GameLoop`.
- `ApplyRoundManaGrowth` is the single extension point for changing the round-growth/refill model. `AddMana(player, delta)` is a server-only helper for later spending/effects, clamped to `[0, capacity]`.
- `PlayerSession.m_manaCapacity` and `m_manaCurrent` are server-written NetworkVariables readable by everyone. Mana is not written in `PlayerSession.Initialize`; turn order is unchanged. Selection spending and mana display are described below.
- These paths were checked statically; host/client replication and round progression still require Play Mode verification.

### Hand limit after drawing (M15, 2026-10-20)

- A successful DrawCard, from either round draws or Clarity, invokes server-only DiscardOldestOverHandLimit after removing the drawn card from the deck. The additive `m_maxHandCards` defaults to 10; zero/negative disables the rule. Failed draws do not discard anything.
- Hand-list order is draw age. The helper copies uncommitted IDs, excluding both board-play and board-discard lists, and removes the oldest excess through the existing MoveCardOnGraveyard path. Stale IDs are removed with the existing list helper. Committed cards remain protected and the newly drawn card remains newest.
- One owner-targeted HandLimitDiscardClientRpc per event shows `hand_limit_discard`. Clients prune null/no-longer-held cards from local selections on hand rebuild and immediately before ordinary confirmation; valid carried-over selections and committed cards remain. This prevents discarded carry-over IDs from becoming false InvalidCardSelection reports without resolving N26 itself.
- PlayerUI's shared message helper remembers the active LocalizedString, so both mana and hand-limit messages refresh on locale changes while retaining the current deadline. Expiry and ClearLocalUI clear that reference. The HUD prefab adds only the hand-limit localization reference.

### Mana validation at selection confirm (MANA2)

- The client refuses additional play cards whose combined cost exceeds current mana, and refuses cards whose data has not arrived. Deselection and free discards keep their existing behavior.
- The server first filters card IDs using the existing ownership, hand and duplicate checks, still recording invalid IDs. It then checks only the accepted play-card cost. A valid first confirm spends that cost through `AddMana` and marks the selection confirmed before the caller writes the board. Later confirms are ignored.
- An over-budget confirm leaves mana and board unchanged, clears both server selection lists, remains unconfirmed and records `ManaOverBudget`. The owner-targeted `SelectionRejectedClientRpc` clears local selection/target state and UI highlights, and triggers the MANA3 localized message.
- Rejection resets the shared selection timer to zero only while both per-player limits permit it: one reset per selection phase and three per match by default. Phase counters clear in `CardSelectionPhase`; match counters clear in `StartNewGame`. Further over-budget attempts still reject and record suspicion without resetting the timer.
- Selection still ends only after both valid confirms or timeout. A player without a valid confirm plays nothing and spends nothing. Turn order still includes both play and discard costs; casting is unchanged.
- Thirty focused local-host Play Mode assertions passed for gating, spending, rejection, owner-side clearing, timer limits, valid retries and timeout behavior. Remote-peer rejection delivery and full-match behavior still require verification.

### Mana display and rejection feedback (MANA3)

- `PlayerUI` displays local and opponent mana as current / capacity. It resolves the opponent through the local session's `m_currentOponentNetworkObjectId` and `GameMaster.GetSessionByID`; an unavailable opponent clears that label.
- Each mana label caches its last two integer values, formatting only when they change. Locale changes invalidate the caches so labels refresh even when mana is unchanged. Polling runs independently of character existence, including respawn waits.
- `OnSelectionRejected` calls `ShowManaNotEnough`. The message uses `UI_table.mana_not_enough` and expires after two unscaled seconds by default; repeated rejections restart the deadline. An active message also refreshes on locale change.
- `ClearLocalUI` empties all three texts, invalidates the caches and cancels the message deadline. Locale subscriptions follow `OnEnable`/`OnDisable`.
- `PlayerUIPrefab` contains three added TMP texts using the existing MPLUSRounded1c font, with serialized field and localization references. `label_mana_capacity`, `label_mana_opponent` and `mana_not_enough` are present in English, Russian and Japanese. Optional local click feedback was not added.

### 3.1 PlayerActionPhase (`GameLoop.cs:91`) [Confirmed]
Each frame, until timeout or `m_activePlayerConfirmedAction` with an empty queue:
1. Check finish, then death, then finish again after `HandleDeathPausingCurrentPhase`. A respawn restores the saved phase, elapsed timer and active player (N18/M1).
2. It drains `m_cardInputQueue`. For each package it:
   - re-checks the card exists, the card owner is the active player, the card is in `m_boardPlayCards`, and the data and activities are non-null
   - checks the target count ≥ number of activities, which is always 4
   - for each activity: a zero target is allowed only for `None/All*/Random*`, then `CheckActivity`
   - on success, runs `ProcessActivity` for each activity. After each one it uses the same finish/death/finish sequence. A decisive death breaks the activity loop; end-of-action cleanup remains reachable. Queued packages after a decisive activity remain the separate N6 issue.
   - runs `MoveCardOnGraveyard`
3. At phase end it calls `ClearDiscardedCards(player)` and `ClearPlayedCards(player)`, which send all board cards to the graveyard.

### 3.2 Death pause and respawn (N18/M1, 2026-10-19) [Confirmed]

- RoundBreak, drawing, CardSelection and PlayerAction check finish before death handling, then finish again after it. The outer GameLoop also checks finish after RoundBreak, drawing and CardSelection, reaching its existing winner/EndMatch path without starting the next phase.
- `HandleDeathPausingCurrentPhase` clears the external death flag, finds a dead player with remaining lives and checks `GetRespawnPoints` before entering a pause. No lives means no wait; no valid blocks means life becomes -1 and no wait.
- A possible respawn saves the current phase, elapsed timer and active-player reference, then yields the existing `WaitForRespawnIfNeeded`. It restores all three only if the match can continue. Mana growth, draws already made, accepted selections and MANA2 reset counters are not initialized again.
- Drawing uses an interruptible, scaled per-frame delay instead of one `WaitForSeconds`, checking death/finish inside the delay as well as before each draw. The partially completed delay remains local to the suspended iterator, so respawn time does not consume it. The configured delay and the existing elapsed-timer increment per completed draw are retained. Drawing still inherits its existing phase value (normally RoundBreak); no phase enum/order change was made.
- The respawn routine below, respawn-condition lookup and character-spawn implementation are unchanged. H6 still stops the outer guarded coroutine during a pause.

Existing `WaitForRespawnIfNeeded` behavior:
- A player is dead when `GetCharacter() == null` and `TeamId != -1`.
- `FindDeadPlayer` picks the first dead player that still has lives.
- It sets `phase = RespawnWait` and `m_waitingRespawnPlayer`, then waits up to `m_respawnWaitTime` (30 s) for `m_respawnConfirmed`.
- If the chosen block is still valid it uses its child `SpawnPoint`. Otherwise it picks a random valid own block.
- With no valid blocks, it sets `m_lifeCurrent = -1`, which permanently eliminates the player.
- Then `RespawnCharacter` spawns a new character, and the phase becomes `ResolveCards` until the pause helper restores the original phase. A finished match skips restoration.

### 3.3 Death and finish conditions [Confirmed]
- **HP ≤ 0** (`UpdatePlayerHP`): M6 (2026-10-20) logs and returns if the controller has no session. Otherwise the existing order is retained: decrement life, `DespawnCharacter`, free the spawn point when present, clear character and `CharacterObjectId`. The guarded spawn-point fallback remains even when despawning returns early.
- **Lava** (`LavaKillZone.TryKillPlayer`, server): `ServerKillPlayerByHazard` sets HP to 0, then runs `UpdatePlayerHP` and sets `m_externalDeathDetected`. M3 (2026-10-20) tracks killed `PlayerController` instances instead of recycled network IDs, removing Unity-destroyed entries before checking membership. A new pony instance can therefore die even with the same ID; existing authority, spawned/current-character/session checks remain.
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
- N18/M1 saves the suspended phase timer before respawning and restores it afterward, along with the phase and active player. The respawn timer remains the existing shared timer while the phase is RespawnWait.
- Clients show `(int)m_timer` as **elapsed** time, not remaining (`GameAnnouncerUI`). **[Confirmed]**

---

## 4. Network message paths

| Action | Client side | RPC | Server handler | Validation |
|---|---|---|---|---|
| Move (WASD) | `PlayerController.Update` → `SendMovementInput`. Requires `CanPlayerMove` **on the client** | `PlayerController.InputServerRpc(PlayerControlPackage)` (owner-only) | `GameMaster.HandlePlayerControlPackage` (enqueue, `IsServer` per M4) → server `Update` → `InputQueueUpdate` → `MoveInputProcessing` + `HPDeltaProcessing` | Direction must be one axis step. **No phase check, and the target client comes from payload `ClientId`.** [Risk] |
| Debug damage (Space) | `SendMovementInput` sets `s_hpDelta = -10` | same | `HPDeltaProcessing` applies any delta | **None.** [Risk] |
| Select cards to play/discard | `PlayerUI.OnCardClicked` (LMB) / `OnCardAlterClicked` (RMB) → `PlayerSession.SelectCardToPlay/Discard` (local lists) | — | — | Local only |
| Confirm selection | `PlayerUI.ConfirmTurn` → `PlayerSession.ConfirmTurn` | `PlayerSession.ConfirmTurnServerRpc(play[], discard[], respawnBlock)` | `HandleConfirmTurn` (`PackageManaging.cs:166`), `CardSelection` branch: `HandleConfirmSelectionTurn` (cost and turn order) **and** a raw write of the IDs into `m_boardPlayCards`/`m_boardDiscardCards` | The sender must own the session. The board write is not checked against hand, owner, duplicates or the already-confirmed flag. [Risk] |
| Target a played card | World click → `PlayerSession.SelectObject` (raycast) → `ConfirmTargetButtonUI.ConfirmSelect` → `PlayerSession.TryConfirmTarget` (fills `m_currentCardControllPackage`) | — | — | Local |
| Cast card | `ConfirmSendButtonUI1.TryConfirmSend` → `PlayerSession.TryHandlePackage` → `CheckPackage` | `PlayerSession.CardInputServerRpc(CardControllPackage)` | `HandleCardControlPackage` (enqueue) → consumed by `PlayerActionPhase` | Phase, active player, card owner, card on board. Then target checks in the loop. |
| End turn | `PlayerUI.ConfirmTurn` during `PlayerAction` | `ConfirmTurnServerRpc` | `HandlePlayerActionConfirm` sets `m_activePlayerConfirmedAction` | Must be the active player |
| Respawn block | `ConfirmTargetButtonUI` during `RespawnWait` → `TryConfirmRespawnBlock`, then `ConfirmTurn` | `ConfirmTurnServerRpc(…, blockId)` | `HandlePlayerRespawnPoint` | Must be the waiting player, own block, spawned, with a spawn point |
| Card identity | — | `GameMaster.SendCardDataClientRpc(cardId)`: on draw it goes to the owner only, and on board placement it goes to all clients | `NetworkCard.RepairCardData` → `CardDatabase.TryGetCard`; then world refresh and `PlayerUI.OnCardDataReceived` (M14) | Missing prerequisites or unknown IDs warn and retain the previous card data |

Notes:
- `ToPlayerControlPackage` / `SendToClientRpc` are an echo with an empty body (debug).
- `PlayerController.ReceiveValidatedInput` and its queue are never used. **[Unfinished]**
- **[Unknown]** `HandlePlayerActionConfirm` compares `PlayerSession != NetworkBehaviourReference` through an implicit conversion. Equality semantics need a runtime check.

### Safe card confirmation and Send (N20, M14, M8; 2026-10-19)

- `CardTargetRules.RequiresSelectedTarget` is shared by client package validation and the server's zero-target check. Its eight optional-target cases are identical to the former server switch; an optional zero target still skips `CheckActivity`. Other target validation, resolution and authority remain unchanged.
- `PlayerSession.ClearSelectedCard` clears a nonempty confirmed package too. Repeated clearing of an empty package retains its target list. Send requires a nonzero confirmed card ID matching the currently selected card, available card data/activities, enough target slots and a nonzero target for each required activity. A missing GameMaster returns safely. The existing phase permission and successful-send reset remain.
- Deselecting or entering RespawnWait drops the action card/package. After resuming, select and confirm again. Respawn confirmation continues using its separate block ID and bypasses card-data checks.
- M14 guards missing data in confirmation, automatic targeting and active-target detection. Automatic targeting writes only up to the smaller activity/target-list count. Card clicks retain both click events while logging a safe placeholder when data is unavailable.
- M8 resolves an AnyPlayer target through a block by matching replicated `m_teamId` to the local/opponent session's `TeamId`. It does not read the server-only block owner reference. A character whose session has not arrived, or a block with no known team owner, warns and returns false. Server target checks are unchanged.

---

## 5. Client state display (per frame or per event)

- `PlayerUI.Update` lazily binds to `SessionManager.LocalPlayer`, subscribes to the three NetworkLists and `m_gamePhase`, and writes the HP and life labels every frame.
- `PlayerUI.UpdateCards` destroys and rebuilds every `CardViewUI` on each list or phase change.
- M14 also calls `PlayerUI.OnCardDataReceived` after the existing card-data RPC repair/world refresh. It rebuilds only when a local hand/played-board card has no view, covering data arriving after the list delta. Existing views and unrelated/opponent cards do not trigger a rebuild. The current phase still decides which cards are displayed; N18 selection-highlight restoration and selected-card validation are retained.
- N18/M1: `CreateCardView` restores play/discard colors only in CardSelection, using `PlayerSession.IsCardSelectedToPlay` / `IsCardSelectedToDiscard`. The local lists survive the pause; they remain the source for the eventual confirm package. RespawnWait continues to hide the hand.
- `CardWorldViewManager` rebuilds the board view layout on every board-list change and every `NetworkCard` location, rotation or face change (`RefreshAllSessions`).
- `GameAnnouncerUI.Update` builds the phase, timer and active-player text every frame through new `LocalizedString` instances.

### Voluntary surrender (N19, 2026-10-19)

- `PlayerUIPrefab/LeaveMatch` provides a localized Leave match button and an initially inactive confirmation panel. Cancel only closes the panel. The new rect follows the existing root canvas dimensions because the prefab's existing root rect is only 100 by 100; existing HUD layout is retained. The full-viewport background Image has raycast targeting enabled, using the existing canvas and EventSystem.
- During a running phase, confirmation calls the local owner's `PlayerSession.RequestLeaveMatch` and its owner-only `LeaveMatchServerRpc`. The RPC additionally checks the sender against `OwnerClientId`, reporting mismatches on the sender's session. Only the server's `HandlePlayerSurrender` decides the winner.
- The surrender handler uses the H6 running-match guards, stops the guarded game coroutine, clears resolution/respawn flags and selects the other valid team. It retains the leaver in `m_players` and calls the unchanged `EndMatch` with the appended `MatchEndReason.Surrendered`. No eligible opponent uses `AbortMatch("player_surrendered_no_winner")`. H6 disconnect handling is unchanged.
- The existing H5 RPC/result transition delivers both peers to ResultScene: the leaver sees defeat and `result_reason_youLeft`; the winner uses the existing opponent-left reason. A draw uses the opponent-left reason defensively. Confirmation during phase None or without a local session uses the ordinary disconnect path. An unanswered request falls back to disconnect after five unscaled seconds; GameOver closes the dialog, disables the button and cancels that deadline.
- Five new UI_table keys are available in en/ru/ja. Editor previews verified localized dialog and defeat-result rendering, plus actual Leave/Cancel button input. Real host/client delivery, simultaneous peer races and card/world click blocking still require the N19 manual checks; isolated source checks do not establish transport behavior.

---

## 6. Known risks in the loop

These are documented, not fixed. Each needs separate verification.

| # | Risk | Location | Consequence |
|---|---|---|---|
| L1 | Any exception inside the coroutine stops `GameLoop` permanently | the coroutine as a whole | Match freezes with no recovery |
| L2 | Resolved by H2 (2026-10-19): empty decks refill once per draw after graveyard deletion; empty filtered pools warn and return | `GameMaster.Card.cs` | Default refill excludes UltraRare/Unique; initial deck and section/copy rules are unchanged. |
| L3 | Resolved by H3 (2026-10-19): block heal uses its server-only `GetMaxHP()` and the character overflow formula/conversion | `Processing.cs`, `BlockController.cs` | No character reference or hard-coded maximum remains in the block heal branch. |
| L4 | `CheckActivity` dereferences `_ownerPlayer` before its null check | `Processing.cs:69` | NRE leads to L1 |
| L5 | `FillCardSection` writes `_nonUniqueList[i]` on a possibly empty list for `UltraRare` | `Card.cs:262` | `StartNewGame` throws once an UltraRare card exists. No current card is UltraRare. |
| L6 | Resolved by N18/M1: the pause helper saves and restores the phase timer and active player | `GameMaster.GameLoop.cs` | Respawn time no longer consumes/resets action-turn time. |
| L7 | N6 remains: after a decisive activity, queued card packages can still resolve | `PlayerActionPhase` | N18/M1 adds finish checks at frame/activity sites; draining already queued packages remains outside its scope. |
| L8 | The end of the match only logs on the server. `m_isLocalPlayerLost` is host-relative and is not networked | `GameLoop.cs:44-62`, `FindLostPlayer` | Clients never see a result. Players remain in `GameScene`. |
| L9 | An opponent disconnect is not handled by `GameMaster`. Destroyed sessions stay in `m_players` | `MultiplayerManager.OnClientDisconnected`, `SessionManager.OnClientDisconnected` | Undefined end state. See [Dependencies.md](Dependencies.md#5-regression-points). |
| L10 | `IsGameFinished()` has the side effect `FindLostPlayer()`, which sets `m_externalDeathDetected` | `GameLoop.cs:679` | Hidden coupling. The death flag is re-raised by every finish check. |
| L11 | Resolved by M15 (2026-10-20): successful draws enforce the configured uncommitted-hand limit, default 10, discarding oldest excess cards | `GameMaster.Card.cs` | Committed cards are excluded; zero/negative disables the rule. Valid unconfirmed selection carry-over (N26) remains separate. |
| L12 | Resolved by M3 (2026-10-20): lava uses an instance set and prunes Unity-destroyed entries | `LavaKillZone.cs` | Network ID reuse no longer suppresses a different pony instance; actual physics/recycled-ID Play Mode verification remains |

## 7. Unknown, needs Play Mode

- Whether `NetworkBehaviourReference` assigned `null` (`m_activePlayer.Value = null`, `GameAnnouncerUI.GetTargetSession`) is accepted by NGO 2.11 or throws.
- Physics of falling after a block despawns, and the lava trigger geometry in `GameScene`.
- Timing between `SendCardDataClientRpc` and the client-side spawn of the card object (the warning path "NetworkCard … was not found").
- **Resolved during the audit:** `GameMasterPrefab.prefab` does not serialize any of the Timing, Movement or GameRules fields. Only `m_debugMode: 1` and the five prefab/database references are stored. The C# defaults documented above are therefore the effective values until the prefab is re-saved. **[Confirmed]** `m_debugMode` is enabled on the prefab, so verbose server logging is on.
