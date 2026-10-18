# FallenStar – Spellcasters: Architecture Overview

- **Audit date:** 2026-10-18
- **Code baseline:** commit `1f6d2f1` **plus the uncommitted working tree**. A refactor that splits `GameMaster` into partial classes and moves `AuthManager`, `MultiplayerManager` and `SessionManager` into `Assets/Scripts/MultiplayerScripts/` is not committed yet. The moved `.meta` GUIDs were checked and match the committed originals.
- **Method:** static reading of every C# file under `Assets/Scripts` (52 files, ~10.7k lines), plus YAML inspection of scenes, prefabs, network prefab lists, build settings and localization tables.
- **Not executed:** Unity Editor, compilation, Play Mode, player build. Nothing in this document has been verified at runtime.

Legend used in all `Docs/AI/Architecture/*.md` files:

| Tag | Meaning |
|---|---|
| **[Confirmed]** | Read in the implementation; the call chain was traced |
| **[Unfinished]** | Code exists but is a stub, TODO, commented out or not wired |
| **[Unknown]** | Cannot be established statically; needs an Editor/Play Mode check |
| **[Risk]** | A probable defect or regression point; needs separate verification |

---

## 1. Technology stack

Source: `Packages/manifest.json`, `ProjectSettings/EditorBuildSettings.asset`, `Assets/Prefab/NetworkManagerPrefab.prefab`.

| Area | Package / setting |
|---|---|
| Engine | Unity 6000.3.13f1 (`AGENTS.md`), URP 17.3.0 |
| Networking | Netcode for GameObjects (NGO) 2.11.2, Unity Transport over Relay |
| Online services | `com.unity.services.multiplayer` 2.2.2 (Lobby + Relay), Authentication 3.6.1 |
| Localization | `com.unity.localization` 1.5.11 (tables `UI_table`, `Cards`; locales en / ru / ja) |
| Input | Input System 1.19.0 (read directly through `Keyboard.current` / `Mouse.current`; the `.inputactions` asset is not used by the scripts) |
| Other runtime deps used by scripts | `Unity.VisualScripting` (`ConvertTo<int>()` in `GameMaster.Processing.cs`), NUnit (`using NUnit.Framework` in `CardControllPackage.cs`), `UnityEditor.Localization...` (`CardPreviewController.cs`) |
| NetworkManager config | TickRate 30, `EnableSceneManagement: 1`, `ConnectionApproval: 0`, `PlayerPrefab: none`, `RecycleNetworkIds: 1` with a 120 s delay; prefab list = `Assets/Prefab/NetworkPrefabs.asset` |

Build scenes, in order: `LoginScene`, `MainMenuScene`, `LobbyScene`, `GameScene` are enabled. `SampleScene` and `MultiplayerScene` are disabled legacy scenes.

---

## 2. Folder structure and subsystems

```
Assets/Scripts/
├─ Bootstrap.cs                  NetworkManager / GameMaster (re)creation, saved language
├─ GameSettingsPrefs.cs          PlayerPrefs wrapper (language code)
├─ Manager/
│   ├─ SceneFlowManager.cs       Offline + network scene loading
│   └─ CardWorldViewManager.cs   Client-side 3D card views on the board
├─ MultiplayerScripts/           (moved here in the uncommitted working tree)
│   ├─ AuthManager.cs            UGS init + anonymous sign-in
│   ├─ MultiplayerManager.cs     Lobby + Relay + host/client start, disconnect, game start
│   └─ SessionManager.cs         NetworkBehaviour: one PlayerSession per connected client
├─ InGame/
│   ├─ GameMaster.cs + GameMaster/*.cs   (partial class; the match server)
│   │     GameLoop / Processing / PackageManaging / Card / Spawn / Player / PlayerCardWorldArea
│   ├─ PlayerSession.cs          Per-client network state + local input/target selection
│   ├─ PlayerController.cs       Character (pony): HP, movement input RPC
│   ├─ BlockController.cs        Platform block: HP, team, child SpawnPoint
│   ├─ NetworkCard.cs            Card network object (owner, location, rotation, face)
│   ├─ CardData.cs / CardDataBase.cs   ScriptableObject card definitions + lookup
│   ├─ CardViewBase/CardViewUI/CardViewWorld.cs, CardPreviewController.cs
│   ├─ PlayerCardfWorldArea.cs   (class PlayerCardWorldArea) board anchor per team
│   ├─ LavaKillZone.cs, SpawnPoint.cs, PonyColorScript.cs
│   └─ *Package.cs, PlayerTurnSelection.cs, GameSlot.cs, TargetDataBackup.cs   data types
└─ UI/                           Login, menu, lobby, in-game HUD, announcer, settings
```

Other notable folders:
- `Assets/Card/` holds `CardDataBase.asset` and 5 `CardData` assets: Fireball, Iceshard, Clarity, HealingHooves, plus a placeholder `CardData.asset`.
- `Assets/Localization/` holds the tables and locales.
- `Assets/Prefab/` holds the network and UI prefabs.
- `26.06.19/`, at the repository root outside `Assets`, is a stale backup copy of the scripts. Unity does not compile it.
- `build/` is an old build output dated Jun 3.
- `Assets/_Recovery` exists and its contents were not inspected.

---

## 3. Runtime object model

### 3.1 Persistent application root [Confirmed]
Sources: `LoginScene.unity`, `AuthManager.Awake`, `SceneFlowManager.Awake`, `MultiplayerManager.Start`.

`LoginScene` contains a GameObject named **`AppRoot`** that carries `Bootstrap`, `AuthManager`, `SceneFlowManager` and `MultiplayerManager`. `AuthManager` and `SceneFlowManager` call `DontDestroyOnLoad(gameObject)` in `Awake`, so the whole `AppRoot` survives every scene change. Duplicates destroy themselves; each singleton checks `Instance != this`. `MultiplayerManager.m_bootsTrap` is wired to the `Bootstrap` on the same object.

### 3.2 Network objects
Sources: `NetworkPrefabs.asset`, `GameScene.unity`, `Bootstrap.cs`, `SessionManager.CreateSession`, `GameMaster.Spawn.cs`, `GameMaster.Card.cs`.

| Object | How it is created | Owner | Notes |
|---|---|---|---|
| `NetworkManager` | `Bootstrap.RecreateNetworkManager()` destroys it and instantiates it again before each host/join | — | DDOL |
| `SessionManager` | **in-scene placed** in `GameScene` (prefab instance) | server | Exists only in the game scene |
| `PlayerSession` | `SessionManager.CreateSession` → `SpawnWithOwnership(clientId)` | the client | One per connected client |
| `GameMaster` | `Bootstrap.RecreateGameMaster()` from `MultiplayerManager.OnLoadEventCompleted` (server only) | server | Runtime-spawned, not in scene |
| Character (`PonyBase`: `PlayerController` + `PonyColorScript`) | `GameMaster.SpawnCharacter` / `RespawnCharacter` → `SpawnAsPlayerObject(clientId)` | the client | Re-created on each respawn |
| Block (`BlockBody`: `BlockController` + child `SpawnPoint`) | `GameMaster.CreateBlock`: 5×5 per player → `SpawnWithOwnership(clientId, true)` | the client | |
| Card (`CardPrefab`: `NetworkCard`) | `GameMaster.FillCardSection` → `Spawn()` | server | The whole deck is spawned at match start |
| `PlayerUI` (`PlayerUIPrefab`) | in-scene placed in `GameScene` | server | A `NetworkBehaviour` used purely as local UI |
| `CardWorldViewManager` | in-scene (prefab) in `GameScene` | — | `MonoBehaviour`, client-side |

### 3.3 Singletons [Confirmed]
Static `Instance` fields: `GameMaster`, `SessionManager`, `MultiplayerManager`, `AuthManager`, `SceneFlowManager`, `CardWorldViewManager`, `PlayerUI`, `LobbyControlUI`, `DisconnectMenuUI`, `SpawnPanelUI`. Access is usually polled (in `Update`) rather than event-driven. Several UI classes fall back to `FindObjectsByType` every frame while a reference is null (`DisconnectMenuUI`, `SpawnPanelUI`, `LobbyControlUI`).

---

## 4. Scenes and transitions

Detailed flow: [GameLoop.md](GameLoop.md#1-scene-flow). Summary:

```
LoginScene ──(LoginUI → AuthManager.SingInAnonymous)──► MainMenuScene
MainMenuScene ──(MainMenuUI.OnClickLobby)──► LobbyScene
LobbyScene ──(host: CurrentLobbyUI.OnClickStartGame → MultiplayerManager.StartGameScene
             → SceneFlowManager.LoadGameSceneNetwork [NGO scene load])──► GameScene
GameScene / LobbyScene ──(Disconnect or connection loss → CleanupAfterDisconnect)──► LobbyScene
```

- Offline loads use `SceneManager.LoadScene`. The game scene uses `NetworkManager.SceneManager.LoadScene(..., Single)`, so clients follow the host.
- **[Unfinished]** `README.md` describes a flow ending in **Result**. No Result scene exists, and no code path leaves `GameScene` at the end of a match.
- **[Unfinished]** `SceneFlowManager.LoadSettingsScene()` targets `"SettingsScene"`. That scene does not exist in `Assets/Scenes` or in the build settings. Nothing references `SettingsUI` or `SettingsSceneButtonsUI` in any scene or prefab.

---

## 5. Server / client responsibility

Full call chains: [GameLoop.md](GameLoop.md#4-network-message-paths). Summary:

| Responsibility | Where | Authority |
|---|---|---|
| Match state machine, timers, turn order | `GameMaster.GameLoop.cs` coroutine | Server only (started by `StartNewGame`, guarded by `IsServer`) |
| Phase, timer, active and respawning player | `GameMaster.m_gamePhase`, `m_timer`, `m_activePlayer`, `m_waitingRespawnPlayer` (NetworkVariables) | Server write |
| HP, lives, block HP, team | `PlayerController.m_hpCurrent/m_hpMax`, `PlayerSession.m_lifeCurrent/TeamId`, `BlockController.m_blockHP/m_teamId` | Server write |
| Hand and board lists | `PlayerSession.m_handCards/m_boardPlayCards/m_boardDiscardCards` (NetworkList&lt;ulong&gt;) | Server write |
| Card identity (`CardData`) | **Not networked.** Server sets `NetworkCard.m_cardData` directly; clients receive a string id via `GameMaster.SendCardDataClientRpc` and resolve it through `CardDatabase.GetCard` | Server → targeted client RPC |
| Movement | Client `PlayerController.SendMovementInput` → `InputServerRpc` → server queue → `MoveInputProcessing` teleports the Rigidbody by 3 units | Server applies |
| Card selection, target selection, confirm | Client-side in `PlayerSession`/`PlayerUI` → `ConfirmTurnServerRpc` / `CardInputServerRpc` | Server validates (partially, see §8) |
| Hazard death | `LavaKillZone.OnTrigger*` (server check) → `GameMaster.ServerKillPlayerByHazard` | Server |
| All visuals: hand UI, board views, announcer, HP text | `PlayerUI`, `CardWorldViewManager`, `GameAnnouncerUI`, `CardViewUI/World`, `PonyColorScript` | Local, driven by NetworkVariable/NetworkList callbacks and polling |

The host is both server and a player. Several server paths use `IsHost` instead of `IsServer` (`GameMaster.Update`, `GameMaster.HandlePlayerControlPackage`). **[Risk]** On a dedicated server, movement input would be dropped silently.

---

## 6. Cards, effects, player and state display

Card data, from `CardData.cs` and the `Assets/Card/Data/*.asset` files:

| Card | Specialization | Activity 0 | Value |
|---|---|---|---|
| Fireball | AttackSpell | Damage → OpponentBlock | 50 |
| Iceshard | AttackSpell | Damage → OpponentPlayer | 20 |
| HealingHooves | DeffenceSpell | Heal → SelfPlayer | 30 |
| Clarity | CardDraftSpell | DrawCard → SelfPlayer | 2 |
| CardData (placeholder) | — | DiscardCard → None | 0 |

- **[Confirmed]** Effects are implemented in `GameMaster.ProcessActivity`. Only `Damage`, `Heal` and `DrawCard` do anything.
- **[Unfinished]** All `DiscardCard*` / `DrawCard*` combo action types are empty `case`s. Target types `All*` / `Random*` pass validation in `CheckActivity`, but `ProcessActivity` does nothing for them. `ActivityEffect` (Fire1/Frost1) is never read.
- **[Risk]** `CardData.OnValidate` overwrites activities 1–3 with `None` on every validation, so only activity 0 can be authored.
- **[Confirmed]** Deck building (`GameMaster.CreateDeck` / `FillCardSection`) uses one shared deck for both players: 20/30/30 cards of attack, defence and draw spells, chosen randomly, with copy limits by rarity.
- **[Confirmed]** The character has 100 HP (`PlayerController.Init`). A session has 3 lives (`PlayerSession.Initialize`). A block has 100 HP (`BlockController.Initialize`).
- **[Confirmed]** Heal above max HP converts 50% of the overflow into extra HP above max (`ProcessActivity`, Heal branch).

State display (client):
- `PlayerUI` polls in `Update` for HP and life text. It rebuilds the hand UI on `NetworkList` changes and phase changes, showing the hand during selection phases and `m_boardPlayCards` during `PlayerAction`/`ResolveCards`.
- `CardWorldViewManager` subscribes to every session's board lists and lays out `CardViewWorld` instances on the `PlayerCardWorldArea` whose `m_sessionSearchId == TeamId`. `GameScene` has two areas, with ids 0 and 1.
- `GameAnnouncerUI` polls every frame and shows the localized phase, the `m_timer` value, and whose turn or respawn it is, with team colours red/blue.
- `PonyColorScript` uses server-written material indices (`NetworkBodyColor`/`NetworkManeColor`) set from `TeamId` in `PlayerController.ApplyMaterial`.

---

## 7. Localization and settings

Sources: `Assets/Localization/*`, `Bootstrap.ApplySavedLanguage`, `SettingsUI.cs`, `GameSettingsPrefs.cs`, UI scripts.

- **[Confirmed]** Locales: en, ru, ja. Table `UI_table` has 46 keys and `Cards` has 8 keys. Every locale has a non-empty entry for every key.
- **[Confirmed]** Saved language: `Bootstrap.Start` waits for `LocalizationSettings.InitializationOperation`, then applies `PlayerPrefs["setting.language_code"]`.
- **[Unfinished]** The only writer of that pref is `SettingsUI.OnLanguageChanged`, and `SettingsUI` is not placed in any scene or prefab. **The player has no in-game way to change the language.**
- Localization patterns in use:
  - `LocalizedString` with `StringChanged` events and Smart variables: `LobbyListUI`, `LobbyPlayerListUI`, `CurrentLobbyUI`.
  - Async `GetLocalizedStringAsync` that reacts to locale changes: `LobbyPlayerItemUI`.
  - Synchronous `GetLocalizedString` every frame, allocating a new `LocalizedString` per call: `GameAnnouncerUI`, and `PlayerUI` for the HP/life labels.
  - A one-shot `StringDatabase.GetLocalizedString` in `Setup`: `CardViewUI`, `CardViewWorld`. These **do not refresh on locale change**.
- **[Risk]** Hard-coded, non-localized user-visible strings: lobby name `"MyLobby"`, player names `"Host_xxxxxx"`/`"Client_xxxxxx"` (`MultiplayerManager.CreateLobbyPlayerData`), `"Unknown Lobby"` (`LobbyListUI`), `"Unknown Player"` (`LobbyPlayerItemUI`), and the lobby player count `"x / y"` (`LobbyListItemUI`).
- **[Risk]** The `UI_table` key `"btn_lobbyRefresh\t"` contains a trailing tab character. Any lookup with `"btn_lobbyRefresh"` will miss.
- **[Unfinished]** There are no localization keys for game over, win/lose or disconnect messages.
- `LocalizedTMPFormat` (a TMP auto-size/ellipsis helper) is not used by any scene or prefab. `GameAnnouncerUI` duplicates its logic.

---

## 8. Architectural risks (summary)

These are documented for verification only. None has been fixed. Details and evidence are in [GameLoop.md](GameLoop.md#6-known-risks-in-the-loop) and [Dependencies.md](Dependencies.md#5-regression-points).

| # | Risk | Evidence |
|---|---|---|
| R1 | **Build break suspected.** Runtime scripts import editor/test-only namespaces | `CardPreviewController.cs:1` `using UnityEditor.Localization.Plugins.XLIFF.V20;`, `CardControllPackage.cs:1` `using NUnit.Framework;` |
| R2 | Client-trusted HP delta and client-supplied `ClientId` in movement | `PlayerControlPackage.s_hpDelta`, `GameMaster.Processing.cs:36-53`, `PlayerController.cs` Space key |
| R3 | Card selection confirm writes raw client IDs to the board without validating owner, hand membership or duplicates | `GameMaster.PackageManaging.cs` `HandleConfirmTurn`, `CardSelection` branch |
| R4 | Unhandled exceptions stop the server `GameLoop` coroutine (empty deck, null targets, etc.) | `GameMaster.Card.cs:151`, `Processing.cs:69,209`, `Card.cs:262` |
| R5 | No end-of-match flow or opponent-disconnect handling | `GameLoop()` ends with `Debug.LogError`; `MultiplayerManager.OnClientDisconnected` "Host continue" |
| R6 | One shared `m_timer` is overwritten by nested respawn waits | `WaitForRespawnIfNeeded` resets `m_timer` mid-`PlayerActionPhase` |
| R7 | Disconnect cleanup runs `Despawn` on clients and runs up to 3× | `GameMaster.ClearupAfterDisconnect` → `PlayerSession.ClearSession` |
| R8 | `LavaKillZone` id cache combined with `RecycleNetworkIds: 1` | `LavaKillZone.m_killedCharacterIds` is never cleared |
| R9 | `ConfirmSendButton.cs` declares class `ConfirmSendButtonUI1`, which does not match the file name, yet it is referenced by `PlayerUIPrefab` | The component may fail to load ("script cannot be loaded") |
| R10 | Nested NetworkObjects: `SpawnPoint.prefab` has a `NetworkObject` and is a child of `BlockBody` (also a NetworkObject) | `GlobalObjectIdHash` present in both prefabs |

---

## 9. Proven vs. unfinished vs. unknown

**Proven by code reading:** the persistent `AppRoot`; Login → Menu → Lobby → Game transitions; Lobby+Relay host and join by lobby id; server-spawned GameMaster, sessions, blocks, characters and deck; the phase loop (RoundBreak → DrawCards → CardSelection → PlayerAction per player, with respawn waits); Damage/Heal/DrawCard effects; lava death; local hand UI; board card views; announcer; en/ru/ja tables complete for existing keys.

**Unfinished:** Result scene and game-over handling; Settings scene and language selection UI; Steam login (`AuthManager.SignInSteam` only logs a warning); join by code (`m_joinCodeInput` is never assigned); `ResolvePlayerCardsPhase` (never called; marked `//ToDo`); the `ResolveCards` and `RoundEnd` phases as real phases; combo/discard actions; All/Random targets; `ActivityEffect`; `SessionRole.Spectator`; `PlayerController.ReceiveValidatedInput` (its queue is never consumed); `TargetDataBackup`; `ToPlayerControlPackage` echo RPC (debug only).

**Unknown, needing an Editor or Play Mode check:**
- whether the project builds a standalone player (R1)
- whether the `ConfirmSendButtonUI1` component loads (R9)
- how NGO handles the nested SpawnPoint NetworkObjects (R10)
- whether the third GUID in the network prefab lists (`e651dbb3…`) resolves to a valid asset
- runtime behaviour of `NetworkBehaviourReference` holding `null` (`m_activePlayer.Value = null`, and `GameAnnouncerUI.GetTargetSession` initialising a reference to `null`)
- Inspector wiring of `GameMaster` serialized fields in `GameMasterPrefab`
- contents of `Assets/_Recovery`

---

## Sources

`Assets/Scripts/**/*.cs` (all files), `Assets/Scenes/{Login,MainMenu,Lobby,Game}Scene.unity`, `Assets/Prefab/*.prefab`, `Assets/Prefab/BoardPrefab/*.prefab`, `Assets/Prefab/UiPrefab/*.prefab`, `Assets/Prefab/NetworkPrefabs.asset`, `Assets/DefaultNetworkPrefabs.asset`, `Assets/Prefab/NetworkManagerPrefab.prefab`, `Assets/Card/Data/*.asset`, `Assets/Localization/*.asset`, `ProjectSettings/EditorBuildSettings.asset`, `Packages/manifest.json`, `AGENTS.md`, `CLAUDE.md`, `README.md`.
