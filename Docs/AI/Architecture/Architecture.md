# FallenStar – Spellcasters: Architecture Overview

- **Updated:** 2026-10-20 (TASK-N8 refresh). First audit: 2026-10-18.
- **Code baseline:** commit `391e079` **plus the uncommitted working tree** with tasks L1, L2/N31, N30, N17, L6, L7, L9, L10 and N28 ([implementation report](../Reports/2026-10-20_L1_L2_N31_N30_N17_L6_L7_L9_L10_N28_Implementation.md)). Claude has not reviewed that working tree yet.
- **Method:** static reading of every C# file under `Assets/Scripts` (53 files, about 12.1k lines), plus YAML checks of scenes, prefabs, build settings and localization tables.
- **Runtime evidence** (not re-run for this update):
  - The owner's Windows player build of `b7e7b91` ran two matches, the Settings scene and a saved language ([review](../Reviews/2026-10-20_ManualChecks-Report6-7_Review.md)).
  - Codex's release build of the current working tree succeeded with 0 errors and 0 warnings, and its menus rendered en/ru/ja (implementation report §4).

Legend used in all `Docs/AI/Architecture/*.md` files:

| Tag | Meaning |
|---|---|
| **[Confirmed]** | Read in the implementation; the call chain was traced |
| **[Unfinished]** | Code exists but is a stub, TODO, commented out or not wired |
| **[Unknown]** | Cannot be established statically; needs an Editor/Play Mode check |
| **[Risk]** | A probable defect or regression point; needs separate verification |

Companion documents: [GameLoop.md](GameLoop.md) (phases, match flow, RPCs) and [Dependencies.md](Dependencies.md) (who calls whom, packages, regression points).

---

## 1. Technology stack

Source: `Packages/manifest.json`, `ProjectSettings/EditorBuildSettings.asset`, `Assets/Prefab/NetworkManagerPrefab.prefab`. Versions: see [Dependencies.md §5](Dependencies.md#5-packages-and-assets).

| Area | Package / setting |
|---|---|
| Engine | Unity 6000.3.13f1, URP |
| Networking | Netcode for GameObjects (NGO) 2.11.2, Unity Transport over Relay |
| Online services | `com.unity.services.multiplayer` (Lobby + Relay), Authentication (anonymous) |
| Localization | Unity Localization; tables `UI_table` and `Cards`; locales en / ru / ja |
| Text | TextMeshPro with one dynamic font asset, `Assets/Fonts/MPLUSRounded1c-Medium SDF.asset` (§9) |
| Input | Input System, read directly through `Keyboard.current` / `Mouse.current` (the `.inputactions` asset isn't used by scripts) |
| NetworkManager | TickRate 30, `EnableSceneManagement: 1`, `ConnectionApproval: 0`, no player prefab, `RecycleNetworkIds: 1` with a 120 s delay; prefab list `Assets/Prefab/NetworkPrefabs.asset` |

Build scenes, all enabled, in this order: `LoginScene`, `MainMenuScene`, `SettingsScene`, `LobbyScene`, `GameScene`, `ResultScene`.

---

## 2. Folder structure and subsystems

```
Assets/Scripts/
├─ Bootstrap.cs                  NetworkManager / GameMaster (re)creation, saved language
├─ GameSettingsPrefs.cs          PlayerPrefs wrapper ("setting.language_code")
├─ DebugModeGuard.cs             Forces debug flags off in release builds (L9)
├─ Manager/
│   ├─ SceneFlowManager.cs       Offline scene loads + the network GameScene load
│   └─ CardWorldViewManager.cs   Client-side 3D card views on the board
├─ MultiplayerScripts/
│   ├─ AuthManager.cs            UGS init + anonymous sign-in (Editor profile per project path)
│   ├─ MultiplayerManager.cs     Lobby + Relay, host/client start, game start, result exit, disconnect
│   └─ SessionManager.cs         NetworkBehaviour: one PlayerSession per connected client
├─ InGame/
│   ├─ GameMaster.cs + GameMaster/*.cs   partial class, the match server:
│   │     GameLoop / Processing / PackageManaging / Card / Spawn / Player / PlayerCardWorldArea
│   ├─ PlayerSession.cs          Per-client network state + owner input (card/target selection)
│   ├─ PlayerController.cs       Character (pony): HP, movement RPC
│   ├─ BlockController.cs        Platform block: HP, team, child SpawnPoint
│   ├─ NetworkCard.cs            Card network object (owner, location, rotation, face)
│   ├─ CardData.cs               Card ScriptableObject, enums, CardTargetRules
│   ├─ CardDataBase.cs           Card lookup by id
│   ├─ CardViewBase/CardViewUI/CardViewWorld.cs, CardPreviewController.cs
│   ├─ PlayerCardfWorldArea.cs   (class PlayerCardWorldArea) board anchor per team
│   ├─ MatchResultData.cs        Static result handed to ResultScene
│   ├─ ResultSceneController.cs  Debug log of the result
│   ├─ SuspiciousActivityLog.cs  Server-only per-session log of rejected input
│   ├─ LavaKillZone.cs, SpawnPoint.cs, PonyColorScript.cs
│   └─ CardControllPackage.cs, PlayerControlPackage.cs, PlayerTurnSelection.cs, GameSlot.cs   data types
└─ UI/                           Login, main menu, settings, lobby, HUD, announcer, leave, result
```

Other folders:
- `Assets/Card/`: `CardDataBase.asset` lists 4 cards (Fireball, Iceshard, HealingHooves, Clarity). `Data/CardData.asset` is a placeholder that isn't in the database.
- `Assets/Localization/`: tables and locales.
- `Assets/Prefab/`: network and UI prefabs. `BootsTrap.prefab` and `MultiplayerManager.prefab` are old copies; the live ones are on `AppRoot` in `LoginScene`.
- `build/` (git-ignored): Codex's verification output (`build/task-*-verification/`).
- The owner's player builds go to `../Spellcasters.Build/`.

---

## 3. Runtime object model

### 3.1 Persistent application root [Confirmed]

`LoginScene` has a GameObject **`AppRoot`** with `Bootstrap`, `AuthManager`, `SceneFlowManager` and `MultiplayerManager`.
- `AuthManager` and `SceneFlowManager` call `DontDestroyOnLoad` in `Awake`; `MultiplayerManager` does it in `Start`. So `AppRoot` survives every scene change.
- Duplicates destroy themselves (`Instance != this`).
- `MultiplayerManager.m_bootsTrap` points to the `Bootstrap` on the same object.

### 3.2 Network objects

| Object | How it is created | Owner | Notes |
|---|---|---|---|
| `NetworkManager` | `Bootstrap.RecreateNetworkManager()` at app start, and again before each host/join | — | DDOL |
| `SessionManager` | In-scene placed in `GameScene` (prefab instance) | server | Exists only in the game scene |
| `PlayerSession` | `SessionManager.CreateSession` → `SpawnWithOwnership(clientId)` | the client | One per connected client. **On a remote client it's created while LobbyScene is still loaded:** the server spawns sessions inside its own GameScene load callback, and NGO sends that create message before the load event. Its `Awake` / `Start` therefore see no GameScene objects; resolve them later (report #11, N50) |
| `GameMaster` | `Bootstrap.RecreateGameMaster()` from `MultiplayerManager.OnLoadEventCompleted` (server only) | server | Spawned at runtime, not in the scene |
| Character (`PonyBase`: `PlayerController` + `PonyColorScript`) | `GameMaster.SpawnCharacter` / `RespawnCharacter` → `SpawnAsPlayerObject(clientId)` | the client | New object on each respawn |
| Block (`BlockBody`: `BlockController` + child `SpawnPoint`) | `GameMaster.CreateBlock`, 5×5 per player → `SpawnWithOwnership(clientId, true)` | the client | |
| Card (`CardPrefab`: `NetworkCard`) | `GameMaster.FillCardSection` → `Spawn()` | server | The whole deck is spawned at match start and on each refill |
| `PlayerUI` (`PlayerUIPrefab`) | In-scene placed in `GameScene` | server | A `NetworkBehaviour` used only as local UI |
| `CardWorldViewManager` | In-scene (prefab) in `GameScene` | — | `MonoBehaviour`, client side |

### 3.3 Singletons [Confirmed]

- Static `Instance` fields: `GameMaster`, `SessionManager`, `MultiplayerManager`, `AuthManager`, `SceneFlowManager`, `CardWorldViewManager`, `PlayerUI`, `LobbyControlUI`.
- Access is mostly polled (`Instance` checks in `Update`), not event-driven.
- Scene searches (`FindObjectsByType` / `FindFirstObjectByType`) run once or only as a fallback:
  - once: `GameMaster.Awake` (spawn points, card areas), `LobbyControlUI.Start`
  - on demand: `CardPreviewController.ShowPreview` calls `TryResolveTargets` to resolve a missing dock, HUD root canvas and preview root, with a root screen-space canvas fallback. Live assigned references are retained; Unity null checks re-resolve destroyed scene objects on the next preview request. Missing targets warn at most once per controller instance (N50). Hover validation follows the rebuilt HUD-row dictionary, including played cards, and safely hides a destroyed hovered card (N52).
  - fallback: `CardWorldViewManager` when an area isn't registered, `PlayerSession` when its preview reference is missing

### 3.4 Debug flags (L9) [Confirmed]

- 16 classes have a `m_debugMode` flag:
  - `GameMaster`, `PlayerSession`, `PlayerController`, `BlockController`, `CardViewUI`, `LavaKillZone`, `ResultSceneController`
  - `CardWorldViewManager`, `AuthManager`, `MultiplayerManager`, `SessionManager`
  - `LeaveMatchUI`, `LobbyControlUI`, `LobbyListUI`, `PlayerUI`, `ResultMenuUI`
- The first statement of each `Awake` is `m_debugMode = DebugModeGuard.Allow(m_debugMode)`. In the Editor and in development builds the Inspector value is kept; in release builds it's always `false`.
- Several prefabs store `m_debugMode: 1` (`GameMasterPrefab`, `SessionManager`, `PlayerSessionPrefab`, `CardWorldManagerPrefab`), so Editor runs log verbosely.
- Some `Debug.Log` lines aren't behind the flag and still appear in release builds, for example "Process called" and the lobby search logs (N34).

---

## 4. Scenes and transitions

Detailed flow: [GameLoop.md §1](GameLoop.md#1-scene-flow).

```
LoginScene ──(LoginUI → AuthManager.SingInAnonymous)──► MainMenuScene
MainMenuScene ──(MainMenuUI.OnClickSettings)──► SettingsScene ──(Back)──► MainMenuScene
MainMenuScene ──(MainMenuUI.OnClickLobby)──► LobbyScene
LobbyScene ──(host: CurrentLobbyUI.OnClickStartGame → MultiplayerManager.StartGameScene
             → SceneFlowManager.LoadGameSceneNetwork [NGO scene load])──► GameScene
GameScene ──(EndMatch → MatchEndedClientRpc → LeaveMatchToResultScene)──► ResultScene
ResultScene ──(ResultMenuUI.OnClickMainMenu)──► MainMenuScene
LobbyScene / GameScene ──(Leave lobby, AbortMatch or connection loss → CleanupAfterDisconnect)──► LobbyScene
```

- Offline loads use `SceneManager.LoadScene`. The game scene uses `NetworkManager.SceneManager.LoadScene(..., Single)`, so clients follow the host.
- `SceneFlowManager` keeps scene names in serialized strings (`m_loginSceneName` … `m_resultSceneName`).
- A second lobby and match after Result → main menu work (U13, report #6).

---

## 5. Server / client responsibility

Full call chains: [GameLoop.md §4](GameLoop.md#4-network-message-paths).

| Responsibility | Where | Authority |
|---|---|---|
| Match state machine, timers, turn order | `GameMaster.GameLoop.cs`, run by `RunGuarded` | Server only (started by `StartNewGame`) |
| Phase, countdown end, active/respawning player, input lock, selection phase ID | `GameMaster.m_gamePhase`, `m_phaseEndServerTime`, `m_activePlayer`, `m_waitingRespawnPlayer`, `m_isInputLocked`, `m_selectionPhaseId` (NetworkVariables). Loop time (`m_phaseElapsed`, `m_phaseDuration`) is private. | Server write |
| HP, lives, mana, block HP, team | `PlayerController.m_hpCurrent/m_hpMax`, `PlayerSession.m_lifeCurrent/m_manaCapacity/m_manaCurrent/TeamId`, `BlockController.m_blockHP/m_teamId` | Server write |
| Hand and board lists | `PlayerSession.m_handCards/m_boardPlayCards/m_boardDiscardCards` (`NetworkList<ulong>`) | Server write |
| Card identity (`CardData`) | **Not networked.** The server sets `NetworkCard.m_cardData` directly. Clients get the card id through `GameMaster.SendCardDataClientRpc` and look it up with `CardDatabase.TryGetCard`. | Server → targeted client RPC |
| Movement | Client `PlayerController.SendMovementInput` → `InputServerRpc` → server queue → `MoveInputProcessing` moves the Rigidbody 3 units | Server validates and applies |
| Card selection, targeting, confirm, cast, leave | Client-side in `PlayerSession` / `PlayerUI` → `ConfirmTurnServerRpc`, `CardInputServerRpc`, `LeaveMatchServerRpc` | Server validates (sender, phase, ownership, hand, mana, targets) |
| Hazard death | `LavaKillZone.OnTrigger*` (server check) → `GameMaster.ServerKillPlayerByHazard` | Server |
| Match result | `GameMaster.EndMatch` → `MatchEndedClientRpc` → `MatchResultData` | Server decides, each peer shows its own result |
| All visuals: hand, board views, announcer, HP/mana text, messages | `PlayerUI`, `CardWorldViewManager`, `GameAnnouncerUI`, `CardViewUI/World`, `PonyColorScript` | Local, driven by NetworkVariable/NetworkList callbacks and polling |

- The host is both server and a player. `GameMaster.Update` and `HandlePlayerControlPackage` check `IsServer` (M4), so input handling would also run without a local client.
- `MultiplayerManager` keeps its own lobby-host checks. A dedicated server is out of scope.

---

## 6. Cards, deck and effects

Card data (`Assets/Card/Data/*.asset`):

| Card | Specialization | Cost | Activity 0 | Value |
|---|---|---|---|---|
| Fireball | AttackSpell | 3 | Damage → OpponentBlock | 50 |
| Iceshard | AttackSpell | 2 | Damage → OpponentPlayer | 20 |
| HealingHooves | DeffenceSpell | 4 | Heal → SelfPlayer | 30 |
| Clarity | CardDraftSpell | 3 | DrawCard → SelfPlayer | 2 |

All four are Common. Activities 1–3 are `None`.

- **[Confirmed]** Effects are in `GameMaster.ProcessActivity`. Only `Damage`, `Heal` and `DrawCard` do something.
- **[Unfinished]** The combo action types (`DrawCard*`, `DiscardCard*`) and `DiscardCard` are empty `case`s. The target types `All*` / `Random*` pass `CheckActivity`, but `ProcessActivity` does nothing for them. `ActivityEffect` (`CardActivity.s_effect`) is never read.
- **[Confirmed]** `CardData.OnValidate` keeps exactly 4 activities (it resizes the array). `CardTargetRules.RequiresSelectedTarget` is the one rule for which targets may be empty, shared by client and server (N20).
- **[Confirmed] Deck** (`GameMaster.CreateDeck` / `FillCardSection`): one shared deck for both players.
  - `Random.Range(0, 3)` picks one of three attack/defence/draw targets: 20/30/30, 30/20/30 or 30/30/20 (L2).
  - Cards are added in whole sets: 4 for Common/Uncommon/Rare, 2 for UltraRare, 1 for Unique. One set of each card first, then random repeat sets of Common/Uncommon/Rare until the target is reached or passed.
  - A 30-card section therefore becomes 32. With today's database, every deck has **84 cards**: 20/32/32, 32/20/32 or 32/32/20 (N31).
  - The first deck allows every rarity; a refill is capped at `m_refillDeckMaxRarity` (Rare). See [GameLoop.md §3.3](GameLoop.md#33-drawing-deck-refill-and-hand-limit).
- **[Confirmed] Values:**
  - A character has 100 HP (`PlayerController.Init`), a session has 3 lives (`PlayerSession.Initialize`), a block has 100 HP (`BlockController.Initialize`).
  - Mana starts at 2 and grows by 2 per round up to 20.
  - The hand limit is 10 uncommitted cards.
- **[Confirmed] Overheal:** healing a character or a block above its maximum turns 50% of the excess into HP above the maximum. The result is rounded with `System.Convert.ToInt32`, which rounds .5 to the nearest even number. For a block, the maximum is the server-only `m_blockHPMax` from `GetMaxHP()` (H3).
- **[Confirmed] Colours:** `PonyColorScript` uses server-written material indices set from `TeamId` (`PlayerController.ApplyMaterial`). The mane colour is never applied: `maneObject` isn't assigned in `PonyBase.prefab` (N27, parked by the owner).

---

## 7. Match end, result and leaving

Details: [GameLoop.md §3.8](GameLoop.md#38-match-end-abort-and-the-guarded-runner).

| Path | Entry | Result screen |
|---|---|---|
| Normal end | `GameLoop` → `EndMatch(winner, LivesDepleted)` | Win / lose / draw |
| Leave match button | `LeaveMatchUI` → `PlayerSession.LeaveMatchServerRpc` → `GameMaster.HandlePlayerSurrender` → `EndMatch(…, Surrendered)` | The leaver loses ("you left") |
| Opponent disconnects in a match | `SessionManager.OnClientDisconnected` → `GameMaster.HandlePlayerLeft` → `EndMatch(…, OpponentDisconnected)` | The remaining player wins ("opponent left") |
| Loop exception | `RunGuarded` → `AbortMatch("loop_exception")` | None: back to `LobbyScene` |
| Start failure | `MultiplayerManager.AbortGameStart(reason)` → `AbortMatch`, or `Disconnect` when there's no GameMaster (N14) | None: back to `LobbyScene` |

- `MatchEndedClientRpc` makes every peer run `MultiplayerManager.LeaveMatchToResultScene`. It leaves or deletes the lobby, cleans up, shuts the network down and loads `ResultScene`.
- `ResultMenuUI` shows the localized title and reason from `MatchResultData`.
- `ResultSceneController` only logs the result in debug mode.

---

## 8. Suspicious-activity logging [Confirmed]

- `PlayerSession.ReportSuspicious(type, detail)` runs only on the server. It writes a `[Suspicious]` warning and records the entry in the session's `SuspiciousActivityLog`: a total count plus the last 20 entries with their phase. The log isn't replicated.
- Types (`SuspiciousActionType`):
  - `RpcSenderMismatch`: movement, confirm or leave sent for someone else's object
  - `InvalidCardSelection`: rejected card IDs at confirm
  - `ManaOverBudget`
  - `ForeignCardPackage`: casting another player's card
- In debug mode, `EndMatch`, `AbortMatch` and `HandlePlayerLeft` log each player's summary.
- Nothing kicks or blocks a player.

---

## 9. Localization, settings and fonts

Sources: `Assets/Localization/*`, `Bootstrap.ApplySavedLanguage`, `SettingsUI.cs`, `GameSettingsPrefs.cs`, the UI scripts.

- **[Confirmed]** Locales en, ru, ja. `UI_table` has 73 keys and `Cards` has 8. Every locale has a non-empty entry for every key, including `Phase.*` for all 9 phases, the result texts, mana and hand-limit messages.
- **[Confirmed] Saved language:**
  - `SettingsUI.OnLanguageChanged` (in `SettingsScene`) sets `LocalizationSettings.SelectedLocale` and saves the code in `PlayerPrefs["setting.language_code"]`.
  - `Bootstrap.Start` waits for `LocalizationSettings.InitializationOperation`, then applies the saved code.
- **Patterns in use:**
  - `LocalizedString` with `StringChanged` and Smart variables: `LobbyListUI`, `LobbyPlayerListUI`, `CurrentLobbyUI`, `LobbyListItemUI`.
  - Async `GetLocalizedStringAsync` that reacts to locale changes: `LobbyPlayerItemUI`.
  - Cached `LocalizedString` per key, re-read only when the state or locale changes: `GameAnnouncerUI` (L6).
  - Serialized `LocalizedString` fields: `PlayerUI` (HP and lives every frame, mana on change, messages on locale change) and `ResultMenuUI` (via `LocalizeStringEvent`).
  - `StringDatabase.GetLocalizedString("Cards", key)` in `CardViewUI` / `CardViewWorld.ApplyTexts`, re-run on `SelectedLocaleChanged` by `CardViewBase` (L7).
- **Names:** the lobby name and player names are the first six characters of the player ID (`GetShortPlayerId`). Role values "Host"/"Client" are protocol data, not display text (M13). The lobby shows the ID twice (N29, parked).
- **Font (N28):**
  - The TMP font asset is **dynamic**: glyphs are added when text is drawn, and its source font stays referenced.
  - "Clear Dynamic Data On Build" is on, so builds start with an empty atlas.
  - Drawing text in the Editor changes the asset. `AGENTS.md` says not to commit those changes (`git restore` the file), unless a task changes the font settings on purpose.
  - The asset keeps its font-feature table (about 67k lines), by the owner's choice.

**UI1–UI4 layout update (2026-10-21).** All six root CanvasScalers use Scale With Screen Size / Expand: 1920×1080 for front-end scenes and 2560×1600 for GameScene. Login's background uses Envelope Parent at aspect 1.7769. Expand preserves the full reference rectangle; front-end UI at 16:10 therefore occupies slightly less height than at the 16:9 reference. The HUD root and LeaveMatch stretch to the canvas; stats/buttons follow corners, while the banner, announcer and hand stay centered. The announcer uses an unscaled 1560×150 rect, centered text and scene font limits 24–48; en/ru prefixes include the separator, and truncation warns once per distinct text. The hand keeps a 300-unit bottom offset and fits within 1800 units using preferred card width and compressed spacing; old views deactivate before deferred destruction. Card previews preserve assigned references, otherwise choose the HUD/root screen-space canvas, rebuild layout and clamp in canvas-local units. The target-Confirm label's minimum size is 14. Existing network identities, UI references and gameplay authority are unchanged.

**UI5 display settings (2026-10-21).** `DisplaySettingsUI` on `SettingsCanvas` owns the Fullscreen/Windowed and resolution dropdowns. It filters and deduplicates monitor modes, keeps the actual current size visible, applies choices immediately with `Screen.SetResolution`, and refreshes without notifications when Unity reports an outside size/mode change. Fullscreen selects the native display size; Windowed keeps a strictly fitting size or chooses the largest fitting monitor mode. Mode labels follow locale changes. Unity's own screen-state persistence is used; there are no new preference keys or startup overrides. The settings panel is 600×400 with three rows; existing language and Back references and CanvasScaler settings are preserved.

**UI6 readability profiles (2026-10-22).** `UiResolutionProfile` selects Low below width 1366. `ResolutionProfileLayout` on `PlayerUIPrefab` captures authored rectangles/reference in Awake, applies the compact 1600×1000 reference and 13 rectangle overrides locally, and restores the authored 2560×1600 layout when returning to Medium/high. It executes before `PlayerUI`, which re-spaces the hand whenever its local width changes. HUD label minima are 30; Confirm, Send and Confirm turn form a wide button column. `CardViewDock` is a non-interactive, non-raycasting child before LeaveMatch. The local session's `CardPreviewController` fits a reused card view to this dock, refits on dock-size changes, and retains UI4's pointer/clamp path if no dock exists. Hand pointer enter/exit events show/hide the view without changing play/discard or board-click rules. Cleanup removes hover subscriptions and hides the preview. The card prefab and networking are unchanged. Prescribed card sizes do not meet every description-pixel target; exact measurements and inherited medium-layout rectangle intersections are documented in the UI6 implementation report.

**UI6 windowed bounds (N49).** Windowed mode uses the display work area minus 16 horizontal pixels and 64 vertical pixels; an unavailable work area falls back to display minus one pixel on both axes. Resolution-list callers supply these inclusive bounds. The automatic size first prefers a fitting monitor mode within 1% of the display aspect, then any fitting mode, then the current size. The current-size exception, fullscreen behavior, preference persistence and Settings minimum 1024×720 remain unchanged.

---

## 10. Architectural risks (current)

These are the open structural risks. Each needs its own task before anything changes. Loop-specific risks are in [GameLoop.md §6](GameLoop.md#6-known-risks-in-the-loop), and change-sensitive spots in [Dependencies.md §6](Dependencies.md#6-regression-points).

| # | Risk | Evidence |
|---|---|---|
| A1 | Card identity is outside the network model. A client sees `m_cardData == null` until the RPC arrives; nothing re-sends it later. | `NetworkCard.m_cardData`, `SendCardDataClientRpc`; guarded by N20/M14 |
| A2 | Server rules call client UI, and `GameMaster` writes `MultiplayerManager` state | `ClearupAfterDisconnect` → `PlayerUI.ClearLocalUI`, `CardWorldViewManager.ClearAllWorldViews`; `SpawnPlayers` → `m_currentMultiplayerStatus` |
| A3 | Team identity is the slot index. `SessionManager` has 4 slots, but the scene supports only teams 0 and 1. | `GetFreeSlot`, `SpawnPoint.m_teamId`, `PlayerCardWorldArea.m_sessionSearchId` |
| A4 | Nested NetworkObject: `SpawnPoint.prefab` has a `NetworkObject` and is a child of `BlockBody` | U2: no NGO warning in any log so far. **[Unknown]** |
| A5 | Disconnect cleanup runs `GameMaster.ClearupAfterDisconnect` twice per path (`HandleLostConnectionToHost` / `ShutdownHostInternal` / `DisconnectClientInternal`, then `CleanupAfterDisconnect`) | Each step is safe to repeat; all despawns are server-only |
| A6 | `NetworkCard` has three ServerRpcs that nothing calls (`SetFaceUpServerRpc`, `SetRotatedServerRpc`, `DespawnCardServerRpc`). Cards are server-owned, so client calls would be rejected by NGO's default ownership check. | `NetworkCard.cs`; part of N33 |
| A7 | Dynamic font asset changes on every Editor run | N28 rule in `AGENTS.md` |

---

## 11. Proven vs. unfinished vs. unknown

**Proven** by code reading and by the owner's two-player runs (2026-10-20):
- the persistent `AppRoot`; Login → Menu → Settings / Lobby → Game → Result → Menu
- Lobby + Relay host and join by lobby ID, with refresh throttling
- the server-spawned GameMaster, sessions, blocks, characters and deck
- the phase loop with countdown, mana, hand limit and deck refill
- death pause and respawn; Damage / Heal / DrawCard; lava death
- match end with result, Leave match, disconnect forfeit
- en/ru/ja tables complete; a working player build

**Unfinished:**
- Steam login (`AuthManager.SignInSteam` only logs a warning)
- join by code (parked, M11)
- the `DrawCards`, `ResolveCards` and `RoundEnd` phases as real phases
- combo and discard actions; `All*` / `Random*` targets; `ActivityEffect`
- `SessionRole.Spectator` (never assigned)
- the "destroy all blocks" win rule as a direct rule
- the mane colour (N27)

**Unknown, needing an Editor or Play Mode check:**
- NGO behaviour with the nested SpawnPoint NetworkObject (A4, U2)
- the NGO `NetworkSceneManager.Dispose` NRE on Play Mode stop (U9, inside NGO, during teardown)
- the open manual checks of tasks L1–N28 (implementation report §6)

---

## History

Earlier risks and gaps that are now resolved:

| Former item | Resolution |
|---|---|
| R1 Build break from runtime `UnityEditor`/`NUnit` imports | C5 removed them; player builds succeed (U1) |
| R2 Client-trusted HP delta and `ClientId` | C1, C2, C3 |
| R3 Selection confirm wrote raw client IDs | C4, MANA2 |
| R4 Exceptions stop the loop | H1 `RunGuarded` → `AbortMatch` |
| R5 No end-of-match flow or opponent-disconnect handling | H5 result flow, H6 forfeit, N19 leave button |
| R6 Shared timer overwritten by respawn waits | N18/M1, then L1 countdown |
| R7 Cleanup despawned on clients | `PlayerSession.ClearSession` no longer despawns; despawns are server-only |
| R8 Lava ID cache with recycled IDs | M3 instance set |
| R9 `ConfirmSendButtonUI1` might not load | C6 closed: the cast button works (report #5). The class/file name mismatch is on the naming list only. |
| R10 Nested NetworkObjects | Still open: A4 above |
| U5 Unresolved network-prefab GUID `e651dbb3…` | It's NGO's own `NetworkPrefabsList` script (review 2026-10-20) |
| U3 / U4 Null `NetworkBehaviourReference`, `DontDestroyWithOwner` | Null reference is safe; `DontDestroyWithOwner: 0` (report #3) |
| No language UI | M12 `SettingsScene` |
| Hard-coded lobby/player names and counts; `"btn_lobbyRefresh\t"` key | M13 localization; the key no longer has the tab |
| `LocalizedTMPFormat`, `DisconnectMenuUI`, `SpawnPanelUI`, `DisconnectButtonUI`, `TargetDataBackup`, `ToPlayerControlPackage`, legacy scenes, `26.06.19/`, `Assets/_Recovery/`, VisualScripting | Deleted by L10 |

---

## Sources

`Assets/Scripts/**/*.cs` (all files), `Assets/Scenes/*.unity`, `Assets/Prefab/*.prefab`, `Assets/Prefab/BoardPrefab/*.prefab`, `Assets/Prefab/UiPrefab/*.prefab`, `Assets/Prefab/NetworkPrefabs.asset`, `Assets/Prefab/NetworkManagerPrefab.prefab`, `Assets/Card/**/*.asset`, `Assets/Localization/*.asset`, `ProjectSettings/EditorBuildSettings.asset`, `Packages/manifest.json`, `AGENTS.md`, `README.md`, and the reports and reviews in `Docs/AI/`.
