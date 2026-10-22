# Game Loop, Phases and Match State

- **Updated:** 2026-10-20 (TASK-N8 refresh). First audit: 2026-10-18.
- **Code baseline:** commit `391e079` **plus the uncommitted working tree** with tasks L1, L2/N31, N30, N17, L6, L7, L9, L10 and N28 ([implementation report](../Reports/2026-10-20_L1_L2_N31_N30_N17_L6_L7_L9_L10_N28_Implementation.md)). Claude has not reviewed that working tree yet.
- **Method:** static code reading. Runtime evidence is named where it exists. Nothing here was run for this update.
- Tags **[Confirmed] / [Unfinished] / [Unknown] / [Risk]** are defined in [Architecture.md](Architecture.md).
- File and method names are given instead of line numbers.

**Primary sources:**
- `InGame/GameMaster.cs` and `InGame/GameMaster/GameMaster.*.cs` (GameLoop, PackageManaging, Processing, Card, Spawn, Player, PlayerCardWorldArea)
- `InGame/PlayerSession.cs`, `InGame/PlayerController.cs`, `InGame/BlockController.cs`, `InGame/NetworkCard.cs`, `InGame/LavaKillZone.cs`, `InGame/MatchResultData.cs`
- `MultiplayerScripts/MultiplayerManager.cs`, `MultiplayerScripts/SessionManager.cs`, `Bootstrap.cs`, `Manager/SceneFlowManager.cs`
- `UI/PlayerUI.cs`, `UI/GameAnnouncerUI.cs`, `UI/LeaveMatchUI.cs`, `UI/ResultMenuUI.cs`, `UI/CurrentLobbyUI.cs`

---

## 1. Scene flow

| Step | Trigger (UI → method) | Scene call | Status |
|---|---|---|---|
| App start | `LoginScene` / `AppRoot`: `MultiplayerManager.Start` → `AuthManager.Initialize` → `Bootstrap.RecreateNetworkManager` → `SubscribeNetworkEvents` → `InvokeRepeating(HearthbeatLobby, 15, 15)`. `Bootstrap.Start` applies the saved language. | — | [Confirmed] |
| Login | `LoginUI` → `AuthManager.SingInAnonymous`. In the Editor, `ApplyEditorProfile` switches to the profile `"Editor_" + hash(dataPath)`, so ParrelSync clones get their own player IDs. | `SceneFlowManager.LoadMainMenuScene` | [Confirmed] |
| Steam login | `AuthManager.SignInSteam` only logs "not ready" | — | [Unfinished] |
| Main menu | `MainMenuUI.OnClickLobby` / `OnClickSettings` / `OnClickExit` | `LoadLobbyScene` / `LoadSettingsScene` (offline) | [Confirmed] |
| Settings | `SettingsUI.OnLanguageChanged` sets the locale and writes `GameSettingsPrefs.LanguageCode`. `DisplaySettingsUI` applies local display choices and follows actual screen changes; Unity retains the screen state. `SettingsSceneButtonsUI` goes back to the main menu. | `SettingsScene` (offline) | [Confirmed]. In a build, restoring the saved language after a restart was first shown by the owner's D2.1 run on a second machine (2026-10-21, report #10). Display settings added by UI5 (2026-10-21). |
| Create lobby | `LobbyControlUI.OnClickCreateLobby` → `MultiplayerManager.StartHost`: `RecreateNetworkManager` → `CreateRelay` (`StartHost()`) → `LobbyService.CreateLobbyAsync(GetShortPlayerId(), 2, options)` | — | [Confirmed] |
| Join lobby | `LobbyListUI.OnClickRefreshLobbyList` → `FindAvailableLobbies`. Then `LobbyListItemUI` → `LobbyListUI.OnJoinLobbyClicked` → `MultiplayerManager.JoinLobbyById` → `JoinRelay` (`StartClient()`, waits up to 10 s to connect) | — | [Confirmed] |
| Join by code | `JoinLobbyByJoinCode` and `m_joinCodeInput` are commented out. `m_lobbyCode` / `m_lobbyCodePanel` stay serialized for it. | — | Parked by the owner (M11) |
| Lobby polling | `CurrentLobbyUI.RefreshLobbyLoop` calls `RefreshCurrentLobby` every 2 s. It shows the Start button to the host when the lobby has 2 players. | — | [Confirmed]. Throttling: see below. |
| Start match | `CurrentLobbyUI.OnClickStartGame` → `MultiplayerManager.StartGameScene` → coroutine `StartGameSceneWhenReady`. It waits up to 10 s for 2 connected NGO clients, refreshes the lobby once, then checks for 2 lobby players. | `SceneFlowManager.LoadGameSceneNetwork` → `NetworkManager.SceneManager.LoadScene("GameScene", Single)` | [Confirmed] |
| Game scene ready | `MultiplayerManager.OnLoadEventCompleted` (all peers). Server only: abort if any client timed out, then `Bootstrap.RecreateGameMaster` → `StartGameAfterSceneReady` (§2) | — | [Confirmed] |
| End of match → Result | `GameMaster.EndMatch` → `MatchEndedClientRpc` → `MatchResultData.Set` → `MultiplayerManager.LeaveMatchToResultScene` (§3.8) | `LoadResultScene` (offline) | [Confirmed]. Seen in the owner's runs. |
| Leave match | `LeaveMatchUI` confirm → `PlayerSession.RequestLeaveMatch` → `LeaveMatchServerRpc` → `GameMaster.HandlePlayerSurrender` → `EndMatch(…, Surrendered)` | via Result | [Confirmed] |
| Result → menu | `ResultMenuUI.OnClickMainMenu` clears `MatchResultData` | `LoadMainMenuScene` | [Confirmed] |
| Leave lobby / connection loss | `LobbyControlUI.OnClickLeaveLobby` → `MultiplayerManager.Disconnect`, or NGO `OnClientDisconnectCallback` → `HandleLostConnectionToHost` → `CleanupAfterDisconnect` | `LoadLobbyScene` (offline) | [Confirmed] |

### Lobby errors and refresh throttling

- **M10:** every Lobby/Relay call has its own service catch and also logs unexpected exceptions. A failed host start shuts the NetworkManager down and clears the lobby state (`ResetAfterFailedHostStart`). A failed join removes the player from the lobby (`LeaveCurrentLobbyAfterFailedJoin`) and shuts down. Remote cleanup during a service outage is best effort.
- **N30:** `RefreshCurrentLobby` returns `Task<bool>`:
  - Callers share the refresh in flight for the current lobby (`m_lobbyRefreshTask`). A refresh younger than `m_minLobbyRefreshInterval` (1.5 s, serialized) returns the cached lobby.
  - A second chain (`m_lobbyRefreshInFlightTask`, `m_lastGlobalLobbyRefreshTime`) keeps `GetLobbyAsync` calls one at a time and at least 1.5 s apart, even across lobby changes (owner decision).
  - A result is kept only if the lobby ID is still the current one. `LobbyExceptionReason.RateLimited` logs a warning; other errors are logged. A failure keeps the last known lobby and returns `false`.
- `StartGameSceneWhenReady` warns on a failed refresh and continues with the last known lobby.

---

## 2. Match bootstrap (server)

`MultiplayerManager.StartGameAfterSceneReady`:

1. It waits up to 10 s for `SessionManager.Instance`. `SessionManager` is placed in `GameScene`. Its `OnNetworkSpawn` calls `CreateSessionsForAlreadyConnectedClients` → `CreateSession(clientId)` for each client:
   - Instantiate `PlayerSessionPrefab`. `GetFreeSlot` gives the slot index (0–3, −1 when full), which becomes the team.
   - `PlayerSession.Initialize(clientId, team)` only stores pending values and the 3 lives (N2).
   - `SpawnWithOwnership(clientId)`. The server's `OnNetworkSpawn` → `ApplyPendingInit` writes `ClientId`, `TeamId` and `m_lifeCurrent` before the spawn message is sent.
   - Add the session to `m_playerSessions` and to the NetworkList `SessionDataList`. Clients rebuild `m_playerSessions` from that list.
2. It waits up to 10 s until there are 2 sessions.
   - Both waits abort through `AbortGameStart`: `session_manager_missing` or `not_enough_sessions` on timeout, `player_left_before_start` as soon as the NetworkManager is gone, stops listening or has fewer than 2 clients.
   - The client count is checked again right before spawning.
3. `GameMaster.SpawnPlayers()`. For each session with `SessionRole.Player`:
   - `CreateBlock`: 25 blocks (5×5, 3 m apart) below the team's scene `SpawnPoint`. Each block is spawned with `SpawnWithOwnership(client, true)`, then `BlockController.Initialize` and `PlayerSession.AddBlock` (which sets the child `SpawnPoint` team).
   - `SpawnCharacter`: a random free scene `SpawnPoint` for the team, then `SpawnAsPlayerObject(client)`. The server's `PlayerController.OnNetworkSpawn` → `Init` sets HP 100/100 and speed 3 (N22). Then `SessionObjectId`, `PlayerController.Initialize`, `CharacterObjectId`, `SetSpawnPoint`, `SetCharacter`, and `MovePlayeToSpawnPoint` (3 units up).
   - Then `BindPlayerCardWorldAreas`, and `MultiplayerManager.m_currentMultiplayerStatus = InGameStatus`.
4. `GameMaster.StartNewGame()`:
   - Stop the old loop. `DespawnAllCards`. Reset queues, flags, `m_isInputLocked`, `m_selectionPhaseId`, the countdown and the phase.
   - Collect the sessions. If `SessionManager` is missing or there aren't exactly 2 players: `AbortMatch("match_start_failed")`.
   - Mana: capacity = `Clamp(m_baseMana, 0, m_maxMana)`, current = capacity.
   - `SetOponent` on both players, `CreateDeck()`, `StartCoroutine(RunGuarded(GameLoop()))`.

`GameMaster.Awake` (on spawn, after `GameScene` loads) runs `FindSpawnPoints`, `HideSpawnPoints` and `FindPlayerCardWorldAreas`. It finds only the objects that exist then: the two scene spawn points (team 0 and 1), not the block spawn points. **[Confirmed]**

---

## 3. Phase state machine (server coroutine)

`enum GamePhase` (`GameMaster.cs`): `None, RoundBreak, DrawCards, CardSelection, PlayerAction, ResolveCards, RespawnWait, RoundEnd, GameOver`.

```
StartNewGame → RunGuarded(GameLoop())
GameLoop()
 while !IsGameFinished():
   RoundBreakPhase      phase = RoundBreak; mana growth; wait m_roundBreakTime (5 s)
   DrawCardPhase        phase stays RoundBreak; m_startHandCards (2) × players:
                        DrawCard, then an interruptible m_cardDrawDelay (0.8 s)
   CardSelectionPhase   phase = CardSelection; m_selectionPhaseId++; new PlayerTurnSelection
                        per player; wait for both confirms or m_cardSelectionTime (60 s);
                        BuildTurnOrderByCardCost
   PlayerTurnPhase      for each player in m_turnOrder:
                          m_activePlayer = player; PlayerActionPhase (≤ m_playerActionTime 60 s)
                          stop if IsGameFinished
                        m_activePlayer = null
 winner = the only player with a character or lives > 0 (else null = draw)
 EndMatch(winner, LivesDepleted)
```

Each phase checks `IsGameFinished` first, then a death (`m_externalDeathDetected || IsAnyPlayerDead`), then finish again. The outer loop also checks finish between phases.

### 3.1 Countdown (L1)

- The loop counts time in private `m_phaseElapsed` against `m_phaseDuration`. Clients see one server-written NetworkVariable, `m_phaseEndServerTime` (a `double` in server time).
- `StartPhaseCountdown(duration, alreadyElapsed)` is called at the start of RoundBreak, drawing (duration = draws × delay), CardSelection, PlayerAction and each respawn wait. It's also called when a rejected selection resets the timer (§3.4), and on resume after a death pause with the time already used. Nothing writes the deadline each frame.
- `ClearPhaseCountdown` runs in `EndMatch`, `AbortMatch` and `StartNewGame`.
- `GetPhaseSecondsLeft()` = ceiling of (deadline − `NetworkManager.ServerTime.Time`), never below 0. `GameAnnouncerUI` shows it as two digits.

### 3.2 RoundBreak and mana (MANA1)

- `RoundBreakPhase` sets the phase, then `ApplyRoundManaGrowth` for each player:
  - Round one only refills.
  - Later rounds add `m_manaGrowthStep` (2) up to `m_maxMana` (20), then refill.
- `ApplyRoundManaGrowth` is the single place for the growth rule. `AddMana(player, delta)` is the server-only helper, clamped to `[0, capacity]`.
- `PlayerSession.m_manaCapacity` / `m_manaCurrent` are server-written NetworkVariables that everyone can read.

### 3.3 Drawing, deck refill and hand limit

- `DrawCard(session)` (`GameMaster.Card.cs`) rejects a null session and skips null cards at the front of the deck.
- **H2:** an empty deck calls `RefillDeck` once per draw. It despawns or destroys the graveyard cards, removes them from `m_spawnedCards`, then calls `CreateDeck(m_refillDeckMaxRarity)` (default `Rare`). Cards in hands and on boards stay. If the deck is still empty, the draw logs a warning and does nothing.
- A drawn card goes to `m_handCards`, gets `SetOwner`, and its identity goes **only to its owner** through `SendCardDataClientRpc`.
- **M15:** after each successful draw, `DiscardOldestOverHandLimit` keeps at most `m_maxHandCards` (10; 0 or less = no limit) cards that aren't committed to the board. The oldest extra cards go through `MoveCardOnGraveyard`. The owner gets one `HandLimitDiscardClientRpc` (message `hand_limit_discard`).
- Deck building: see [Architecture.md §6](Architecture.md#6-cards-deck-and-effects).

### 3.4 CardSelection and confirm validation

- **Client:** `PlayerSession.SelectCardToPlay` refuses a card whose data hasn't arrived, and any card that would push the play cost over current mana. Discards are free.
- **Client clean-up:** before sending, `ConfirmTurn` prunes selections that are no longer in the hand. When `m_selectionPhaseId` changes (a new selection phase), `PlayerUI.OnSelectionPhaseChanged` clears both local lists (N26). A death pause inside the same phase doesn't change the ID, so selections survive it.
- **Server** (`HandleConfirmSelectionTurn`, called by `HandleConfirmTurn`):
  - `ValidateSelectionCards` accepts only non-zero, non-duplicate IDs that are in the hand, owned by the session and have card data. Rejected IDs are logged and recorded as `InvalidCardSelection`.
  - If the accepted play cost exceeds current mana: both lists are cleared, the selection stays unconfirmed, `ManaOverBudget` is recorded, and the owner gets `SelectionRejectedClientRpc` (it clears the local selection; `PlayerUI` shows `mana_not_enough` for 2 s).
  - A rejection resets the selection timer through `ResetCardSelectionTimer`, at most `m_maxSelectionTimerResetsPerPhase` (1) per phase and `m_maxSelectionTimerResetsPerMatch` (3) per match for each player.
  - Otherwise the cost is spent with `AddMana`, the selection is confirmed, and the caller writes the accepted cards to `m_boardPlayCards` (`m_isRotated = false`) and `m_boardDiscardCards` (`m_isRotated = true`). Their identities go to all clients. Later confirms in the same phase are ignored.
- The phase ends when both have confirmed, or on timeout. A player without a valid confirm plays nothing and spends nothing.
- `BuildTurnOrderByCardCost` sorts players by `PlayerTurnSelection.TotalCost()` (play + discard cost), highest first.

### 3.5 PlayerActionPhase [Confirmed]

Each frame, until the timeout or until the active player has confirmed with an empty queue:
1. Finish, death, finish (as above).
2. Drain `m_cardInputQueue`. For each package:
   - Re-check that the card exists, belongs to the active player, is in `m_boardPlayCards`, and has data and activities. A card without data or activities goes to the graveyard.
   - Set `m_isResolvingCards`. The target list must have at least one slot per activity (always 4).
   - For each activity: a zero target is allowed only when `CardTargetRules.RequiresSelectedTarget` is false; otherwise `CheckActivity`. Any failure drops the package and clears the flag.
   - Run `ProcessActivity` for each activity, with the finish / death / finish check after each one. A deciding death stops the remaining activities.
   - `MoveCardOnGraveyard`, clear the flag, then check finish before yielding (N6). Packages left in the queue are cleared by `EndMatch`; their effects don't run.
3. At the end: `ClearDiscardedCards(player)` and `ClearPlayedCards(player)` send that player's board cards to the graveyard.

### 3.6 Death pause and respawn (N18/M1) [Confirmed]

- `HandleDeathPausingCurrentPhase` clears `m_externalDeathDetected` and finds the first dead player (`GetCharacter() == null`, `TeamId != -1`, lives > 0).
  - No blocks left (`GetRespawnPoints` empty): `m_lifeCurrent = -1`, no pause. The player is out.
  - Otherwise it saves the phase, `m_phaseElapsed`, `m_phaseDuration` and `m_activePlayer`, then runs `WaitForRespawnIfNeeded`. If the match goes on, it restores all of them and restarts the countdown with the remaining time.
- `WaitForRespawnIfNeeded`:
  - Sets `RespawnWait`, `m_isWaitingRespawn` and `m_waitingRespawnPlayer`, and starts a `m_respawnWaitTime` (30 s) countdown.
  - Waits for `HandlePlayerRespawnPoint` (the waiting player confirms one of their own spawned blocks with a spawn point).
  - Uses the chosen block's `SpawnPoint` if it's still valid, otherwise a random valid own block. None left: life = −1.
  - `RespawnCharacter` (2.5 units above the point), then the phase becomes `ResolveCards` until the pause helper restores the saved phase.
- Drawing waits in an interruptible per-frame delay, so a death during the delay is noticed on the next frame, and the pause doesn't eat the delay.
- Mana growth, draws already made, accepted selections and timer-reset counters are not repeated after a pause.

### 3.7 Death, finish and the win rule [Confirmed]

- **HP ≤ 0** (`UpdatePlayerHP`): lose one life, `DespawnCharacter`, free the spawn point, clear the character reference and `CharacterObjectId`. A character without a session logs an error and returns (M6).
- **Lava** (`LavaKillZone`, server only): `ServerKillPlayerByHazard` sets HP to 0, runs `UpdatePlayerHP` and sets `m_externalDeathDetected`. Already-killed characters are tracked by instance (a `HashSet<PlayerController>`, destroyed entries pruned), not by network ID (M3).
- **Block HP ≤ 0** (`UpdateBlockHp`): `PlayerSession.DeleteBlock`, then `Despawn`. A character standing on it should fall into the lava (physics, **[Unknown]**).
- **Finished** (`IsGameFinished`): at most one player (non-null, `TeamId != -1`) still has a character or lives > 0. One allocation-free loop with no side effects (L3).
- **How a player loses:**
  - by losing all 3 lives (cards or lava)
  - by dying with no block left to respawn on (life = −1)
  - by pressing Leave match, or by disconnecting during a match (§3.8)
- Destroying all blocks doesn't end the match by itself. It counts at the player's next death. **[Unfinished]** as a direct rule.
- If both players are out at once, `winner` is null and the result is a draw.

### 3.8 Match end, abort and the guarded runner

- **`RunGuarded`** runs the loop and its nested iterators on a stack. An exception in any of them is logged, then `AbortMatch("loop_exception")`. It yields only `null` and nested phases (the N12 "spin" report was a false positive).
- **`EndMatch(winner, reason)`** (server, once per match):
  - clears the countdown; sets `GameOver`; clears the active and respawning player and both input queues
  - in debug mode, logs each player's suspicious-activity summary
  - `MatchEndedClientRpc(winnerClientId, reason)`. Every peer stores win / lose / draw in `MatchResultData` and calls `LeaveMatchToResultScene`.
- **`LeaveMatchToResultScene`:**
  - The host waits up to `m_resultLeaveTimeout` (3 s) for the client to disconnect first.
  - The host deletes the lobby; the client removes itself from it.
  - Then `ClearupAfterDisconnect`, `Shutdown`, a wait while `ShutdownInProgress`, and `ClearSessionOnDisconnect`. It loads `ResultScene`.
  - Disconnect callbacks are ignored while this runs (`m_isLeavingToResult`).
- **Reasons** (`MatchEndReason`):
  - `LivesDepleted` (normal end)
  - `OpponentDisconnected`: `SessionManager.OnClientDisconnected` → `GameMaster.HandlePlayerLeft`. The leaver is removed from `m_players`.
  - `Surrendered`: `HandlePlayerSurrender`. The leaver stays in `m_players`.
  - Both handlers work only while the loop runs and the phase isn't `None` or `GameOver`. Each stops the loop and picks the other valid player. If there is none: `AbortMatch("player_left_no_winner")` or `AbortMatch("player_surrendered_no_winner")`.
- **N17:** `SessionManager.OnClientDisconnected` clears the HUD only when the local client itself disconnected. A host watching the opponent forfeit keeps its HUD until `ResultScene`.
- **`AbortMatch(reason)`** (server, once): clears the countdown, sets `None`, logs an error, and calls `MultiplayerManager.Disconnect`. That deletes the lobby (host), shuts down and returns to `LobbyScene`. No result is shown.
- `ResultMenuUI` picks the title and reason text:
  - `LivesDepleted`: `result_reason_youLostLives` / `opponentLostLives` / `bothLostLives`
  - `OpponentDisconnected`: `result_reason_opponentLeft`
  - `Surrendered`: `result_reason_youLeft` for the leaver, `result_reason_opponentLeft` otherwise

### 3.9 Unused or transient phases

- `DrawCards` and `RoundEnd` are never assigned. **[Confirmed]**
- `ResolveCards` is only a short transient value at the end of `WaitForRespawnIfNeeded`. **[Confirmed]**
- `GameAnnouncerUI` and `UI_table` have keys for all 9 phases (`Phase.*`).

---

## 4. Network message paths

| Action | Client side | RPC | Server handler | Validation |
|---|---|---|---|---|
| Move (W/A/S/D) | `PlayerController.Update` (owner) → `SendMovementInput`, only if `CanPlayerMove` | `InputServerRpc(PlayerControlPackage)` (owner only) | Checks the sender is the owner, overwrites `ClientId` with the sender, `HandlePlayerControlPackage` (enqueue) → `GameMaster.Update` → `InputQueueUpdate` → `MoveInputProcessing` | Client still connected; its session's `CanPlayerMove` (PlayerAction, active player, no input lock); the character's owner; one axis step of `m_stepMoveDistance` (3) |
| Debug damage (Space) | `SendMovementInput` sets `s_hpDelta = -10`. Compiled only in the Editor and development builds. | same | `HPDeltaProcessing`, only in the Editor or development builds with `m_debugMode` on | Debug only |
| Select cards | `PlayerUI.OnCardClicked` (LMB) / `OnCardAlterClicked` (RMB) → `PlayerSession.SelectCardToPlay` / `SelectCardToDiscard` | — | — | Local: phase, input lock, card data, mana |
| Confirm selection | `PlayerUI.ConfirmTurn` → `PlayerSession.ConfirmTurn` | `ConfirmTurnServerRpc(intendedPhase, play[], discard[], respawnBlock)` | `HandleConfirmTurn` → `HandleConfirmSelectionTurn` (§3.4) | Sender owns the session; the phase matches (below) |
| Target a played card | World click → `PlayerSession.SelectObject` (raycast) → `ConfirmTargetButtonUI.ConfirmSelect` → `TryConfirmTarget` (fills `m_currentCardControllPackage`) | — | — | Local; `AutoSelectTarget` fills self/opponent-only cards |
| Cast a card | `ConfirmSendButtonUI1.TryConfirmSend` → `PlayerSession.TryHandlePackage` → `CheckPackage` | `CardInputServerRpc(CardControllPackage)` | `HandleCardControlPackage` (enqueue) → `PlayerActionPhase` | PlayerAction, sender is the active player, card owner (`ForeignCardPackage` recorded), card on the board; then §3.5 |
| End turn | `PlayerUI.ConfirmTurn` during PlayerAction | `ConfirmTurnServerRpc` | `HandlePlayerActionConfirm` sets `m_activePlayerConfirmedAction` | Must be the active player |
| Respawn block | `ConfirmTargetButtonUI` during RespawnWait → `TryConfirmRespawnBlock`, then `ConfirmTurn` | `ConfirmTurnServerRpc(…, blockId)` | `HandlePlayerRespawnPoint` | The waiting player; own, spawned block with a spawn point |
| Leave match | `LeaveMatchUI.OnClickConfirmLeave` → `PlayerSession.RequestLeaveMatch` | `LeaveMatchServerRpc` (owner only) | `HandlePlayerSurrender` | Sender owns the session (`RpcSenderMismatch` recorded otherwise) |
| Card identity | — | `GameMaster.SendCardDataClientRpc(cardId)`: to the owner on a draw, to everyone on board placement | `NetworkCard.RepairCardData` → `CardDatabase.TryGetCard`; then `CardWorldViewManager.RefreshAllSessions` and `PlayerUI.OnCardDataReceived` | Unknown IDs warn and keep the old data (M14) |
| Hand-limit / rejection notices | — | `HandLimitDiscardClientRpc`, `SelectionRejectedClientRpc` (owner only) | — | — |
| Match result | — | `MatchEndedClientRpc(winnerClientId, reason)` | every peer → `LeaveMatchToResultScene` | — |

**Phase-tagged confirm (N21).** `ConfirmTurn` reads the phase once and sends it as `intendedPhase`. `IsConfirmForCurrentPhase` accepts only:
- CardSelection → CardSelection
- RespawnWait → RespawnWait
- PlayerAction or ResolveCards → PlayerAction or ResolveCards

A mismatch is logged in debug mode and ignored, with no suspicion and no mana feedback.

**Input lock (N3).** `IsInputLocked()` uses the server's own flags (`m_isResolvingCards || m_isWaitingRespawn`) on the server. Clients use the NetworkVariable `m_isInputLocked`, which `GameMaster.Update` mirrors on change. It resets on new game and cleanup. `CanPlayerMove`, `CanPlayerSelectCardTargets` and `CanPlayerSelectCards` all check it.

**Send safety (N20, M14, M8):**
- `PlayerSession.ClearSelectedCard` also clears a confirmed package.
- Send needs a confirmed card that matches the selected card, card data, enough target slots, and a target for each activity that requires one.
- Deselecting a card, or entering RespawnWait, drops the package: after a respawn, select and confirm again.
- An `AnyPlayer` target picked through a block resolves its owner by matching the replicated `BlockController.m_teamId` to the local or opponent `TeamId` (`GetBlockOwnerSession`).

---

## 5. Client state display

- **`PlayerUI.Update`** binds to `SessionManager.LocalPlayer` lazily.
  - It subscribes to the three card lists, `m_gamePhase` and `m_selectionPhaseId`, and unsubscribes in `ClearLocalUI` and `OnDestroy`. It clears the HUD once when the local player goes away (L6).
  - Each frame it writes HP and lives (or the "dead" text when lives < 0). The mana labels are formatted only when a value or the locale changes.
- **`PlayerUI.UpdateCards`** rebuilds every `CardViewUI` on each list or phase change:
  - None / RoundBreak / CardSelection show the hand.
  - PlayerAction / ResolveCards show `m_boardPlayCards`.
  - RespawnWait and GameOver show nothing.
  - In CardSelection, `CreateCardView` restores the play/discard colours from the local lists.
  - `OnCardDataReceived` rebuilds only when a local hand or board card has no view yet.
  - UI3 deactivates old views before deferred `Destroy`, then adjusts the cached HorizontalLayoutGroup spacing to keep the rendered hand centered within its 1800-unit container (card width 200, maximum authored spacing 220). The hand center stays 300 reference units above the bottom. Clearing the local UI restores the spacing.
- **`PlayerUI` messages** (`mana_not_enough`, `hand_limit_discard`) last 2 unscaled seconds and refresh on a locale change.
- **`CardWorldViewManager`** lays out `CardViewWorld` instances for every session's board.
  - It rebuilds on every board-list change and every `NetworkCard` location, rotation or face change.
  - In `Update` it drops sessions that left, were despawned or were destroyed, and registers new ones when the count changes (L6).
- **`GameAnnouncerUI.Update`** shows the localized phase, the countdown and whose turn or respawn it is (red/blue by team).
  - It keeps the last phase, seconds, target, team, local-player flag and locale, and does nothing when none changed (L6).
  - It reuses one `LocalizedString` per key.
  - UI1 gives it an unscaled 1560×150 centered box and scene font limits 24–48, preserving two lines, NoWrap and Ellipsis. en/ru turn/respawn prefixes include a separator; any remaining truncation warns once per distinct text.
- **Card views** (`CardViewUI`, `CardViewWorld`) re-read the name and description on `SelectedLocaleChanged`, through `CardViewBase` (L7).
- **`LeaveMatchUI`**:
  - UI3 stretches its rect with the HUD root; the former canvas-size polling workaround is removed.
  - Its button is disabled at GameOver.
  - Confirming during phase None, or without a local session, disconnects directly.
  - If no result arrives within `m_leaveFallbackTimeout` (5 s), it disconnects.
- **`CardPreviewController`** (UI4) retains assigned references, otherwise selects PlayerUI's root canvas or a root screen-space fallback. A missing canvas disables previews with a warning. Layout is rebuilt before the measured preview rect/pivot/scale is clamped to canvas-local bounds; an oversized axis is centered.

---

## 6. Known risks in the loop

| # | Risk | Location | Consequence |
|---|---|---|---|
| GL1 | `ProcessActivity` calls `GetSessionByID(cardOwnerId).GetCharacter()` without a null check, and never uses the result (N32) | `GameMaster.Processing.cs`, `ProcessActivity` | Latent: `CheckActivity` has just checked the owner, and a disconnect stops the loop before the session despawns. If it ever happened, `RunGuarded` would abort the match (`loop_exception`); no freeze. |
| GL2 | One RPC means "confirm selection", "end turn" or "confirm respawn". N21 drops stale confirms, but a confirm sent just before a phase change is lost. | `HandleConfirmTurn` | The player has to press again (the owner chose "ignore", 2026-10-20) |
| GL3 | `HandlePlayerActionConfirm` compares `PlayerSession` with a `NetworkBehaviourReference` through an implicit conversion | `GameMaster.PackageManaging.cs` | **[Unknown]**: equality semantics not checked at runtime |
| GL4 | Falling after a block is destroyed depends on physics and the lava trigger geometry | `UpdateBlockHp`, `LavaKillZone` | **[Unknown]**: a player could stand on nothing |
| GL5 | The 4-slot target list and `CardData.OnValidate` (always 4 activities) are coupled to the server's "slots ≥ activities" check | `CardControllPackage.CreateEmpty`, `CardData.OnValidate`, `PlayerActionPhase` | Changing the activity count changes validation |

## 7. Unknown, needs Play Mode

- Physics of falling after a block despawns, and the lava trigger (GL4).
- Timing between `SendCardDataClientRpc` and the client-side spawn of the card ("NetworkCard … was not found" warning path).
- The tasks L1–N28 in the working tree: countdown agreement between peers (±1 s), respawn resume, per-frame cost, and locale switching of card texts. These are the open manual checks in the [implementation report](../Reports/2026-10-20_L1_L2_N31_N30_N17_L6_L7_L9_L10_N28_Implementation.md#6-manual-unity-editor-verification-steps).
- `GameMasterPrefab.prefab` stores none of the Timing, Movement or GameRules fields, so the C# defaults above are the real values. It stores `m_debugMode: 1`; in release builds that flag is forced off (L9). **[Confirmed]**

---

## History

Items from earlier versions of this document that are now resolved:

- **Result flow:** "End of match → Result: not implemented" was resolved by H5 (`EndMatch`, `MatchEndedClientRpc`, `ResultScene`). Forfeit on disconnect was resolved by H6, and the Leave match button by N19.
- **Loop exceptions** used to stop `GameLoop` for good. H1 added `RunGuarded`, which aborts the match instead.
- **Deck exhaustion** threw an exception; H2 added the refill.
- **Block heal** used the character's maximum HP; H3 fixed it with `GetMaxHP`.
- **`CheckActivity`** dereferenced the card owner before its null check. It's now checked first.
- **`FillCardSection`** indexed an empty list for UltraRare cards. UltraRare and Unique cards are no longer added to the repeat list.
- **Respawn and the timer:** a respawn reset the action timer. N18/M1 now saves and restores the phase, timer and active player; L1 keeps the countdown.
- **Queued cards after the deciding hit** kept resolving. N6 checks finish after each card.
- **Lost-player bookkeeping:** `FindLostPlayer` and `m_isLocalPlayerLost` are removed (L3).
- **Hand size** was unlimited; M15 added the hand limit.
- **Lava kills** used a recycled-ID cache; M3 now tracks instances.
- **Removed by L10:** `ResolvePlayerCardsPhase`, the movement echo (`SendToClientRpc`, `ToPlayerControlPackage`), `PlayerController.ReceiveValidatedInput`, `SpawnPanelUI`, `DisconnectMenuUI` and the Tab toggle in `MultiplayerManager`.
- **Movement** had no phase check and trusted the payload's `ClientId` and HP delta:
  - C3: the server now checks `CanPlayerMove`.
  - C2: the RPC overwrites `ClientId` with the sender.
  - C1: the debug damage key exists only in Editor and development builds.
- **Card selection** trusted the client's card IDs. C4 and MANA2 now validate the IDs, ownership and mana.
