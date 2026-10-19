# Issue Report #6: FallenStar – Spellcasters (re-audit after H2, H3, N14, N18_M1, N19)

- **Date:** 2026-10-19
- **Baseline:** `HEAD` = `fe2d4fb` ("N14, N18_M1. N19"). The working tree was clean at the start of the audit.
- **Previous reports:** [#1](2026-10-18_IssueReport.md) (`1f6d2f1`), [#2](2026-10-18_IssueReport-2.md) (`44d858f`), [#3](2026-10-18_IssueReport-3.md) (`ad3a46f`), [#4](2026-10-18_IssueReport-4.md) (`f08e854`), [#5](2026-10-19_IssueReport-5.md) (`058fa63`)
- **Method:**
  1. Read the agent instructions: `CLAUDE.md`, `AGENTS.md`, `AI_CONTEXT.md`, and the existing `Docs/AI` documents.
  2. Read the full code diff `058fa63..fe2d4fb` (commits `e7d1b03` H3/H2 and `fe2d4fb` N14/N18_M1/N19). Compared it step by step with each task spec and Codex's implementation report.
  3. Re-read the code around the changes:
     - all phase coroutines and the death, respawn and finish checks
     - `EndMatch`, `AbortMatch`, `StartNewGame`, `HandlePlayerLeft`
     - card graveyard and despawn paths (`NetworkCard`, `CardWorldViewManager`)
     - selection, target and send code in `PlayerSession` and `PlayerUI`
     - `GameAnnouncerUI`, `PonyColorScript`
  4. Re-checked every open issue from reports #1–#5 at its **current** line.
  5. Checked these assets:
     - `PlayerUIPrefab` wiring for `LeaveMatchUI` (component fields and the three `onClick` targets)
     - `ResultScene` (`m_reasonYouLeft`)
     - the 5 new `UI_table` keys in en/ru/ja
     - `PonyBase.prefab`
     - prefab `m_debugMode` flags
  6. Read the Editor logs: `%LOCALAPPDATA%\Unity\Editor\Editor.log` (10:40) and `Editor-prev.log` (10:17). Both come from the ParrelSync clone, which shares `Assets` with the main project. Also read Codex's verification output in the git-ignored `build/task-*-verification/` folders.
- **I didn't run a build or Play Mode.** No code, scenes, prefabs or settings were changed. The only changes are this report and removing completed task files (§6).

**This report is the full issue list.** §3 lists every open issue, and §4 lists every closed one, so the earlier reports are only needed for history. IDs are unchanged. New findings continue the numbering from report #5, starting at N26.

### Severity levels and verdicts (unchanged since report #1)

| Level | Meaning |
|---|---|
| 🔴 **Critical** | Breaks the build or the core loop, or lets a client cheat or control the match |
| 🟠 **High** | Freezes or corrupts a match under reachable conditions, or causes silent data loss |
| 🟡 **Medium** | Wrong behavior, a broken flow, or a latent crash with low probability |
| 🟢 **Low** | Maintainability, performance, cosmetics, dead code |
| ⚪ **Info** | Naming, typos, documentation, design notes |

- **Confirmed:** the defect is visible directly in the code path, assets or logs.
- **Plausible:** the code strongly suggests the defect, but it needs an Editor or Play Mode check.

## Summary

| | |
|---|---|
| Tasks completed since #5 | **H3** ✅, **H2** ✅ (also closes **M9**), **N14** ✅, **N18_M1** ✅ (closes **N18** and **M1**), **N19** ✅. All five match their specs. No blocking defects were found. |
| Runtime evidence | **N19:** the clone log shows two full matches that ended with the host leaving through the new button, both reaching `ResultScene` with the right winner and reason. **U13** is answered: a second match started and finished after returning to the main menu, in the same Play session. |
| Compile state | 0 `error CS`. Only the known warnings CS0114 (N1) and CS0414 (N4) (`Editor.log:270-272`) |
| Player build | ❓ Not re-run since 2026-10-18 (U1). `build/task-h3-verification/H3Verification.exe` is a Codex test harness, not a game build. |
| 🔴 Critical / 🟠 High open | **0 / 0** |
| 🟡 Medium open | 13. **N20** moved up from Low: the N18 pause makes it reachable in normal play (§2). |
| 🟢 Low open | 22, including 2 new: **N26** (an unconfirmed selection carries over to the next round) and **N27** (the pony mane color is never applied) |
| ⚪ Info open | 3 plus the naming group |
| Task files in `Docs/AI/Tasks/` | All five old specs were removed as done. Afterwards, 13 new specs were written, one for each open 🟡 Medium issue (§6). |

**Overall:** every Critical and High issue from reports #1–#5 is now fixed or closed. These now work:
- match start (including aborting back to the lobby)
- the game loop with deaths in any phase
- the deck refill
- leaving a match
- the result screen

What remains:
- Medium robustness items, mostly on the client side and in the lobby and network layer
- one owner question (N26)
- the manual Play Mode checks for this round, which have only been done for host surrender (§5)

---

## 1. Review of implemented tasks

| Task | Commit | What changed | Matches spec? | Verdict | Notes |
|---|---|---|---|---|---|
| **H3** | `e7d1b03` | `BlockController.m_blockHPMax` (server-only, `:21`), set in `Initialize` (`:61`), getter `GetMaxHP()` (`:98`). The block heal branch (`Processing.cs:216-227`) uses the block maximum in the threshold and in the overflow formula. It no longer reads `_targetCharacter`. | Yes | ✅ **Fixed** (static) | Codex: 139 harness assertions, e.g. 50→80, 80→105, 100→115. No current card heals a block, so normal play doesn't reach this branch. |
| **H2** | `e7d1b03` | Config `m_refillDeckMaxRarity = Rare` (`GameMaster.cs:84`). `CreateDeck(maxRarity = Unique)` skips null cards and cards above the limit (`Card.cs:260`). New `RefillDeck()` (`:190-217`): despawns or destroys every graveyard card and removes it from `m_spawnedCards`, then clears the graveyard, then creates the deck. `DrawCard` (`:149-186`): null-session guard, strips nulls at the front, refills at most once, never throws, uses `[0]`. | Yes, including the "graveyard first" order and the once-per-call rule | ✅ **Fixed** (static). **M9 closed.** | Checked: `NetworkCard.OnNetworkDespawn` only unsubscribes and destroys its views. It doesn't touch `GameMaster` lists, so the `foreach` over `m_graveyard` is safe. Cards played earlier in the round are tracked by ID lists, not the graveyard, so a refill in the middle of a round (Clarity) can't break `ClearPlayedCards`. A deck refill hasn't happened in any log yet. |
| **N14** | `fe2d4fb` | `MultiplayerManager.AbortGameStart` (`:1178`) is called when a client's scene load times out, when `GameMaster` is missing, on both 10 s timeouts, and when a player leaves during the waits or just before spawning. `StartNewGame` calls `AbortMatch("match_start_failed")` on both early returns (`GameMaster.cs:543`, `:571`). | Yes. Every server early exit now ends in an abort. H6 is unchanged. | ✅ **Fixed** (static) | `AbortMatch` needs `IsServer`, and `GameMaster.Instance` is only set in `OnNetworkSpawn`, so the helper never reaches a non-spawned instance. A disconnect while `RecreateGameMaster` is running is caught by the second wait or the check after it. Not run at runtime. |
| **N18_M1** | `fe2d4fb` | New `HandleDeathPausingCurrentPhase` (`GameLoop.cs:593-622`). It checks the respawn conditions first: no own block means elimination now. If a respawn is possible, it saves the phase, timer and active player, runs the unchanged `WaitForRespawnIfNeeded`, then restores them. It's called every frame in `RoundBreak` (`:403-408`), `CardSelection` (`:531-536`), before each draw and inside the now interruptible draw delay (`:560-583`), and at both places in `PlayerAction` (`:179-184`, `:320-325`), where it uses `break` so the board cleanup still runs. `GameLoop` checks for a finished match after each phase (`:106-110`). On the client, `PlayerUI.CreateCardView` restores the play and discard highlights in `CardSelection` (`PlayerUI.cs:278-285`). | Yes, including the `break` detail, "resume, don't restart" (no second mana growth) and the highlight restore | ✅ **Fixed** (static). **N18 and M1 closed.** | **Side effects** (no change to the verdict):<br>• The restored highlights make an older client quirk visible → **N26**.<br>• The pause rebuilds the hand UI, which clears the selected card, so **N20** is now reachable.<br>• A selection confirm sent just as the pause starts is lost (**N21**, extended).<br>• No runtime evidence: the only respawn in the logs (`Editor-prev.log:5446`) ran the **old** code (`GameLoop.cs:621`). |
| **N19** | `fe2d4fb` | `MatchEndReason.Surrendered`. `GameMaster.HandlePlayerSurrender` (`GameMaster.Player.cs:6-44`), `PlayerSession.RequestLeaveMatch` → `LeaveMatchServerRpc` with a sender check (`:139-168`). New `UI/LeaveMatchUI.cs` (confirmation dialog, direct disconnect in phase `None`, 5 s fallback). `ResultMenuUI` `Surrendered` case (`:48-51`). `PlayerUIPrefab`: button and dialog. `ResultScene`: `m_reasonYouLeft`. 5 keys in en/ru/ja. | Yes | ✅ **Fixed**, host side seen at runtime | **Wiring checked:** all three `onClick` handlers point to the `LeaveMatchUI` component, and both component fields are set (`PlayerUIPrefab.prefab:537, 670, 2859, 2284-2285`).<br>**Runtime (clone `Editor.log`):**<br>• Match 1: the clone was the client and won ("You won. Reason: Surrendered", `:2502`).<br>• Match 2: the clone was the host and left during `CardSelection` (`:3052` "match ended. Winner: 1. Reason: Surrendered", `:3082`), then "You lost. Reason: Surrendered" (`:4295`) → main menu (`:4308`).<br>• No exceptions.<br>**Not seen yet:** a client leaving, the 5 s fallback, and the dialog blocking clicks on cards and the world behind it. |

---

## 2. New findings and updates

| ID | Level | Issue | Location | Evidence | Verdict |
|---|---|---|---|---|---|
| **N26** | 🟢 Low | **An unconfirmed card selection carries over to the next round.** `ClearLocalCardSelection` is called only on confirm, on a MANA2 rejection and on `ClearSession`. If the selection timer runs out before the player confirms, `m_selectedCardsToPlay` / `m_selectedCardsToDiscard` keep last round's cards.<br>• **Before N18:** the next round's hand showed no highlights, but the next confirm still sent those cards, and their cost counted in the client mana gate.<br>• **Now:** the N18 highlight restore shows them as selected at the start of the next selection phase. The player can see them and deselect them, so the hidden desync is gone, but the carry-over itself is still there. | `InGame/PlayerSession.cs:199`, `:224`, `:723` (only callers of `ClearLocalCardSelection`); `UI/PlayerUI.cs:278-285` | Code | Confirmed. **Owner decision:** keep the selection ("remembered selection"), or clear it when a **new** `CardSelection` phase starts (not when resuming from `RespawnWait`). The client can tell these apart from the previous phase value in `OnValueChanged`. |
| **N27** | 🟢 Low | **The pony mane color is never applied.** `PonyBase.prefab` leaves `PonyColorScript.maneObject` unassigned, so `maneRenderer` stays null. Every character spawn logs the warning "Invalid mane material index." and skips the mane. | `Assets/Prefab/BoardPrefab/PonyBase.prefab:368` (`maneObject: {fileID: 0}`); `InGame/PonyColorScript.cs:39-42`, `:70-88` | Asset, and 8 warnings in each of the two latest clone logs | Confirmed. Assign the mane renderer in the prefab (Inspector), or remove the mane branch if the model has no separate mane mesh. |

**Updates to existing items:**
- **N20 moved up 🟢 → 🟡.** There's now a second, natural trigger for defect (a):
  1. The active player selects a board card and confirms its target, which builds the package with the card ID (`PlayerSession.cs:790-818`).
  2. Before they press Send, someone dies, for example the opponent who is still falling after an earlier Fireball. The phase becomes `RespawnWait`.
  3. `PlayerUI.UpdateCards` clears the hand, and `ValidateSelectedCard` calls `ClearSelectedCard` (`PlayerUI.cs:437-445`), which sets `PlayerSession.m_selectedCard = null` but keeps the package.
  4. After the resume, Send → `CheckPackage` → `m_selectedCard.m_cardData` throws a `NullReferenceException` (`PlayerSession.cs:1038`). Nothing is sent until the player selects the card and confirms the target again.

  Defect (b), the inverted target rule, is unchanged.
- **N21 extended:** the same phase race now also happens when a pause starts. A selection confirm that reaches the server after it entered `RespawnWait` goes to `HandlePlayerRespawnPoint` and is rejected there ("NetworkObjectId is 0"). The client has already cleared its local selection (`PlayerSession.cs:199`), so after the resume the player has to pick the cards again.
- **M10 seen again:** the same `NullReferenceException` inside `LobbyService.GetLobbyAsync` during the start-game refresh (`Editor-prev.log:1024`; `MultiplayerManager.cs:856` → logged at `:1156`). The start continued with stale lobby data, as in #5.
- **N16 narrowed:** a surrender keeps the leaving session in `m_players`, so its summary **is** logged. Only the disconnect path still loses it.
- **U11 not seen** in the two latest clone logs.
- **L10 extended:** `Assets/_Recovery/0.unity` (+ `.meta`) is a Unity crash-recovery scene, committed in `75fab38` and tracked since. It contains objects with `m_debugMode: 1`.
- **L3 / L6:** since N18, `IsGameFinished()` (which allocates a list and calls `FindLostPlayer`) and `IsAnyPlayerDead()` run every frame in **every** phase, not only in `PlayerAction`. The cost is small, but it adds to L3/L6.

---

## 3. All open issues

### 🔴 Critical and 🟠 High

None open.

### 🟡 Medium (13)

| ID | Issue | Current location | Consequence | Verdict | Direction |
|---|---|---|---|---|---|
| **N20** | `CheckPackage`: (a) NRE when `m_selectedCard` was cleared but the package still holds a card ID (deselecting the card after confirming its target, or **the N18 pause**, §2); (b) inverted target rule: `All*`, `RandomSelfBlock` and `RandomOpponentBlock` cards (target 0) can never be sent, while an untargeted `OpponentBlock`/`AnyPlayer` package passes the client check | `InGame/PlayerSession.cs:1034-1055`; triggers `UI/PlayerUI.cs:314-320`, `:437-445` | (a) Send throws and does nothing until the player selects the card and confirms the target again. (b) Latent: no current card uses those targets. | Confirmed | Reset the package whenever `m_selectedCard` is cleared, and null-check `m_selectedCard` / `m_cardData`. Rewrite the rule as "target required ⇔ type needs a selected target". |
| M3 | Lava ignores a respawned character whose network ID was reused (`m_killedCharacterIds` is never cleared; `RecycleNetworkIds: 1`) | `InGame/LavaKillZone.cs:13`, `:71`, `:86` | A respawned pony can stand in lava | Plausible | Clear the ID on despawn or respawn, or key it by `NetworkObject` reference |
| M4 | `IsHost` used where `IsServer` is meant | `InGame/GameMaster.cs:143`; `GameMaster.PackageManaging.cs:50` | All movement is dropped on a dedicated server (not used today) | Confirmed | Use `IsServer` |
| M6 | `UpdatePlayerHP` assumes the session has a spawn point | `GameMaster.Processing.cs:344` | NRE → H1 abort if the spawn point is missing | Confirmed (low probability) | Null-check `GetSpawnPoint()` |
| M8 | Pure-client NRE: `m_currentSelectedBlock.GetSession()` (the block's owner is set only on the server) | `InGame/PlayerSession.cs:862` | Targeting an `AnyPlayer` card through a block fails on a non-host client | Plausible (needs a non-host client) | Resolve the owner from `TeamId` / `m_teamId` instead |
| M10 | Async lobby and network errors aren't handled. **Seen at runtime** (§2). | `MultiplayerManager.cs:531` (`HearthbeatLobby`, `async void`), `:565` (`StartHost`), `:849-862` (`RefreshCurrentLobby` catches only `LobbyServiceException`), `:863` (`JoinLobbyByJoinCode`); `OnLoadEventCompleted` (`:1188`, `async void`, awaits `RecreateGameMaster` with no `catch`) | Unobserved exceptions. The match start continues with stale lobby data. A host can run with no lobby. | Confirmed | Catch `Exception` at each async entry point and add a status or abort path |
| M11 | Join by code is unusable: `m_joinCodeInput` is never assigned, and no UI calls it | `MultiplayerManager.cs:28`, `:863-906` | The feature is dead | Confirmed | **Owner (2026-10-19):** comment the code out for now |
| M12 | No in-game language selection: `SettingsUI` is in no scene, and `"SettingsScene"` doesn't exist or isn't in the build | `SceneFlowManager.LoadSettingsScene` | Players can't switch between en/ru/ja | Confirmed | **Owner (2026-10-19):** a separate `SettingsScene` with a locale selector |
| M13 | Hard-coded user-visible strings | `MultiplayerManager.cs:594`, `:808`, `:1363` (`"Host"`/`"Client"` + `"_"` + ID), `:613` (`"MyLobby"`); `UI/LobbyListUI.cs:209` (`"Unknown Lobby"`); `UI/LobbyPlayerItemUI.cs:50` (`"Unknown Player"`); `UI/LobbyListItemUI.cs:45` (`"x / y"`) | Breaks the en/ru/ja rule | Confirmed | Localized keys or format strings |
| M14 | Client code reads `m_cardData` before `SendCardDataClientRpc` has arrived | `InGame/PlayerSession.cs:1038`; `InGame/NetworkCard.cs:145-149` (`CheckActiveTaergetSelection`); `InGame/CardViewUI.cs:85`, `:91` | NRE on an early click | Plausible | Null guards |
| M15 | Unbounded hand growth (design question) | `GameMaster.GameLoop.cs:552-589` (`DrawCardPhase`, 2 cards per player per round) | Hands grow forever. H2 now keeps the deck alive, so long matches are possible. | Confirmed (design) | **Owner (2026-10-19):** limit of 10 cards for now. Over the limit, the oldest cards are discarded. |
| M16 | `SessionManager.Instance` is never cleared (no `OnDestroy`) | `MultiplayerScripts/SessionManager.cs:56` | A stale reference after leaving `GameScene`. Unity's null semantics hide most of the effect. | Plausible | Clear it in `OnDestroy` / `OnNetworkDespawn` |
| N1 | `PlayerUI.OnDestroy` hides `NetworkBehaviour.OnDestroy` (CS0114), so NGO's base cleanup isn't called | `UI/PlayerUI.cs:74` | A compiler warning on every compile. Base teardown is skipped. | Confirmed | `public override void OnDestroy()` + `base.OnDestroy()`. NGO's base is `public virtual`, so `protected` wouldn't compile (CS0507). |

### 🟢 Low (22)

| ID | Issue | Current location | Verdict |
|---|---|---|---|
| **N26** | An unconfirmed selection carries over to the next round (new, §2) | `PlayerSession.cs:199`, `:224`, `:723`; `PlayerUI.cs:278-285` | Confirmed |
| **N27** | The pony mane color is never applied; a warning on every spawn (new, §2) | `PonyBase.prefab:368`; `PonyColorScript.cs:70-88` | Confirmed |
| N2 | NetworkVariables are written before the session spawns | `SessionManager.cs:235` → `PlayerSession.Initialize` | Confirmed |
| N3 | The client-side `CanPlayerMove` / `CanPlayerSelectCardTargets` / `CanPlayerSelectCards` read server-only flags (`m_isResolvingCards`, `m_isWaitingRespawn`) | `GameMaster.GameLoop.cs:15-39` | Confirmed |
| N4 | Dead `ValidateCardOwner`; unused `m_cardResolveDelay` (CS0414) | `GameMaster.PackageManaging.cs:487`; `GameMaster.cs:74` | Confirmed |
| N5 | Missing `$` in a log string (`"Client disconnected: {_package.ClientId}"`) | `GameMaster.Processing.cs:40` | Confirmed |
| N6 | Card packages already queued still resolve after a match-deciding activity. The inner loop keeps going after the `break` at `:320`. | `GameMaster.GameLoop.cs:185-333` | Confirmed |
| N7 | `GetCharacter` assumes the object has a `PlayerController` | `PlayerSession.cs:641-651` | Confirmed (latent) |
| N12 | `RunGuarded` would spin on a yielded `CustomYieldInstruction`. Still latent: the loop now yields only `null` and nested phases. | `GameMaster.GameLoop.cs:83` | Confirmed (latent) |
| N16 | When a player disconnects, their suspicious-activity summary isn't logged. They're removed from `m_players` before `EndMatch`. The surrender path is fine. | `GameMaster.Player.cs:60` | Confirmed |
| N21 | A late selection confirm is read in the server's **current** phase. At the end of `CardSelection` it becomes "end turn" (the whole turn is lost if that player goes first). When a pause starts, it's rejected and the selection is lost (§2). | `GameMaster.PackageManaging.cs:204-249`; `PlayerSession.cs:170-200` | Plausible (latency window) |
| N22 | Character HP is set one frame after spawn (`DelayedInit`). Until then HP is 1/1, so activities resolved in the same frame after a mid-card respawn use 1 HP. | `PlayerController.cs:17-18`, `:63`, `:92-101`, `:127` | Confirmed (latent: every card has only activity 0) |
| N23 | `Keyboard.current` used without a null check | `MultiplayerManager.cs:142`; `PlayerController.cs:167-185` | Confirmed (low probability on PC) |
| L1 | `m_timer` is written every frame (network traffic every tick) and shown as **elapsed** time, not remaining | the phase loops in `GameMaster.GameLoop.cs`; `GameAnnouncerUI.cs:56` | Confirmed |
| L2 | `CreateDeck` `Random.Range(0, 2)` never reaches `case 2` | `GameMaster.Card.cs:224` | Confirmed |
| L3 | `IsGameFinished()` has a side effect (`FindLostPlayer` sets `m_externalDeathDetected`) and allocates. Since N18 it runs every frame in every phase. | `GameMaster.GameLoop.cs:805-840` | Confirmed |
| L4 | Blocks have two team NetworkVariables (`m_teamId`, `TeamId`), read by different code | `InGame/BlockController.cs` | Confirmed |
| L6 | Per-frame polling and allocations:<br>• `GameAnnouncerUI` creates 3 `LocalizedString` objects per frame (`:153-160`)<br>• `CardWorldViewManager` re-registers every frame after a player leaves<br>• `PlayerUI`, `LobbyControlUI`, `DisconnectMenuUI`, `SpawnPanelUI`<br>• `LeaveMatchUI.Update` copies the canvas size every frame (`:27-29`) instead of using stretch anchors | listed files | Confirmed |
| L7 | Card texts don't refresh when the locale changes (one-shot `GetLocalizedString` in `Setup`) | `CardViewUI.Setup`, `CardViewWorld.Setup` | Confirmed |
| L8 | `UI_table` key `"btn_lobbyRefresh\t"` has a trailing tab | `Assets/Localization/UI_table Shared Data.asset:175` | Confirmed |
| L9 | `m_debugMode: 1` on `GameMasterPrefab`, `PlayerSessionPrefab`, `SessionManager.prefab`, `CardWorldManagerPrefab`, `MultiplayerManager.prefab` and `ResultScene` (`ResultSceneController`). The debug HP key (Space) is active in every Editor and development build, and the logs are noisy. The timing and rule fields aren't serialized in `GameMasterPrefab`, so C# defaults apply. | the listed assets | Confirmed |
| L10 | Dead or legacy code and files: `ResolvePlayerCardsPhase` (`GameLoop.cs:419`), the commented-out `CardMessageQueueUpdate`, the `SendToClientRpc` echo, `PlayerController.ReceiveValidatedInput`, `TargetDataBackup`, `SpawnPanelUI`/`DisconnectMenuUI` (legacy scenes only), `DisconnectButtonUI`, `LocalizedTMPFormat`, `SettingsSceneButtonsUI`, the `26.06.19/` backup folder, the `Unity.VisualScripting` dependency for `ConvertTo<int>()`, and (new) `Assets/_Recovery/0.unity` | listed | Confirmed |

### ⚪ Info

- **N8: documentation drift, partly improved.** Codex updated `GameLoop.md` and `Dependencies.md` for H2, H3, N14, N18_M1 and N19. Still wrong or missing:
  - `GameLoop.md:45` still says "End of match → Result: Not implemented"
  - 0 mentions of `RunGuarded`, `LeaveMatchToResultScene` or `SuspiciousActivityLog`
  - `README.md:24` still describes a "destroy all enemy blocks" win
  - `test.md` is still at the repo root
- **N17:** after a forfeit, the host's HUD goes blank for a moment before `ResultScene` loads (`SessionManager.cs:183-186`). Cosmetic.
- **N24:** three scripts aren't UTF-8: `InGame/NetworkCard.cs`, `InGame/PlayerCardfWorldArea.cs`, `Manager/CardWorldViewManager.cs`. Their comments show as garbage in UTF-8 tools. Convert them only with owner approval. Until then, specs that touch these files must tell Codex to keep the encoding.
- **Naming and typos (report only, don't rename):** `CardControllPackage`, `PlayerCardfWorldArea.cs`, `SetOponent` / `m_currentOponentNetworkObjectId`, `CheckActiveTaergetSelection`, `SingInAnonymous` / `SingOut` / `SingOutAndClearCredentials` / `m_isSingingIn`, `HearthbeatLobby`, `MovePlayeToSpawnPoint`, `ClearupAfterDisconnect`, `CardSpecialization.DeffenceSpell`, `KnowLobbies` / `m_knowLobbies`, `m_bootsTrap`, `_teamAssing`, `m_phaseAnouncementText`, `#region Valiable`, the log text `"Packege"`, the local variable `_locakPoint`.

### Feature gaps (not defects, listed so they aren't mistaken for bugs)

- Steam login is a stub (`AuthManager.SignInSteam`).
- `GamePhase.DrawCards` and `RoundEnd` are never set, and `ResolveCards` is only transient. During draws the announcer shows "Round break", and a pause during draws resumes as `RoundBreak`.
- The `DiscardCard*` / `DrawCard*` combo actions, the `All*` / `Random*` targets and `ActivityEffect` have no effect.
- The "destroy all enemy blocks" win rule isn't implemented directly. Losing all blocks matters when that player dies: since N18 they're eliminated at once.
- `SessionRole.Spectator` exists, but nothing uses it.
- No lobby message when a match can't start (N14 Q1), and no notice of a deck refill (H2 Q1).

### ❓ Unknowns: open

| ID | Question | Status |
|---|---|---|
| U1 | Does a player build compile on `HEAD`? | ❓ Last real build: 2026-10-18 (exit 0). Not re-run since H1, H5–H10, MANA1–3, H2, H3, N14, N18_M1, N19. |
| U2 | Nested `SpawnPoint` NetworkObject inside `BlockBody` | ❓ No NGO warnings in any log |
| U5 | Unresolved network-prefab GUID `e651dbb3…` | ❓ |
| U6 | Double `GameScene` load in a clone log | ❓ Seen again (`Editor-prev.log:1057`, `:1168`) |
| U8 | Does `Shutdown()` leave no objects behind (H7 §9.2)? | ❓ Partial evidence: a second match started and finished in the same Play session with no errors (`Editor.log:2533-4308`) |
| U9 | NGO `NetworkSceneManager.Dispose` NRE on Editor quit or domain reload | ❓ Inside NGO, during teardown. Likely harmless. |
| U10 | Does the H6 forfeit work at runtime in each phase? | ❓ Still no `OpponentDisconnected` result in any log |
| U11 | `The referenced script (Unknown) on this Behaviour is missing!` after domain reloads | ❓ Not seen in the two latest logs. Close it if the main project's Console is clean. |
| U12 | Does mana replicate to a separate remote client, and does the rejection RPC reach a remote owner? | ❓ |

---

## 4. Closed issues

| ID | How it was closed | Commit / report |
|---|---|---|
| C1 | Debug HP delta only under `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD` and `m_debugMode` | `61323d5` (#2) |
| C2 | The movement target comes from `ServerRpcParams` | `cebd2fd` (#2) |
| C3 | Server-side `CanPlayerMove` check | `fb0edb9` (#2) |
| C4 | Selection confirm validates card IDs | `44d858f` (#2) |
| C5 | Editor-only `using`s removed; player build succeeded on 2026-10-18 | `15b439b` (#2, #5) |
| C6 | Not reproducible: runtime evidence shows the cast button works | #5 |
| H1 | `RunGuarded` turns exceptions into `AbortMatch` | `e776caf` (#3) |
| H2 | New deck without `UltraRare`/`Unique` after deleting the graveyard | `e7d1b03` (#6) |
| H3 | Block heal uses the block maximum | `e7d1b03` (#6) |
| H4 | Owner null check comes first in `CheckActivity` | `f08e854` (#4) |
| H5 | End-of-match flow and `ResultScene` | `ad3a46f`, `d44b882` (#4) |
| H6 | Forfeit when a player disconnects | `d44b882` (#4) |
| H7 | Single, server-guarded disconnect cleanup | `57d3467` (#3) |
| H8 | `CardData.OnValidate` no longer wipes activities | `f08e854` (#4) |
| H9 | `UltraRare` deck fill fixed | `f08e854` (#4) |
| H10 | Result screen menu | `ade4aed` (#5) |
| M1 | The pause and resume restores the timer after a respawn | `fe2d4fb` (#6) |
| M2 | Withdrawn (false positive) | #2 |
| M5 | Fixed by C3 | #2 |
| M7 | Fixed by H4 | #4 |
| M9 | `DrawCard(null)` guard (part of H2) | `e7d1b03` (#6) |
| N9 | Superseded by MANA2 ("first **valid** confirm is final") | #5 |
| N10, N11, N13 | Closed when H5 was finished | #4 |
| N14 | Host returns to the lobby when a match can't start | `fe2d4fb` (#6) |
| N15 | Number not used | #4 |
| N18 | Deaths are handled in every phase | `fe2d4fb` (#6) |
| N19 | Leave button with confirmation, counts as a defeat | `fe2d4fb` (#6) |
| N25 | Intended (owner, 2026-10-19) | #5 |
| L5 | `GameAnnouncerUI` maps every existing phase | #4 |
| U3, U4 | Answered (null `NetworkBehaviourReference` is safe; `DontDestroyWithOwner: 0`) | #3 |
| U7 | Normal end reaches `ResultScene` on both instances | #4 |
| **U13** | A second lobby and match work after result → main menu | **#6** (`Editor.log:2502-4308`) |

Tasks completed without an issue ID: MANA1–MANA3 (`ade4aed`, `5eec046`, `058fa63`) and SEC1 (`f08e854`).

---

## 5. Suggested next steps (in order)

1. **Run the manual checks for this round.** The full step-by-step checklist is [ManualChecks-Report6](../ManualChecks/2026-10-19_ManualChecks-Report6.md). The specs are in git history (§6). The key checks:
   - **N19:** a **client** leaving (only host surrenders have been seen). The dialog must block clicks on cards and the world. Test the 5 s fallback by temporarily skipping the server call, and the layout at other aspect ratios in ru/ja.
   - **N18_M1:**
     - a lava death in `RoundBreak`, between two draws, and in `CardSelection` (highlights and the accepted confirm must survive)
     - a death in the middle of an action turn (the same player resumes at the same time)
     - a last-life death, and a death with no blocks left, in each phase
   - **H2:** temporarily set `m_startHandCards = 20` to run the deck out: refill log, no abort, the spawned-object count doesn't keep growing. Test the rarity filter with Clarity temporarily set to `UltraRare`.
   - **H3:** temporarily point HealingHooves at `SelfBlock`; a full block healed by 30 goes to 115.
   - **N14:** add a temporary 5 s delay in `StartGameAfterSceneReady` and close the client during it → the host returns to the lobby.
   - **Older backlog:**
     - forfeit in each phase (U10)
     - leftover objects (U8)
     - mana with a real remote client (U12)
     - the MANA2 tamper test
     - an honest full match with 0 `[Suspicious]` lines
2. **One fresh Windows player build on `HEAD`** (U1).
3. **Next fixes (candidates for specs):**
   1. **N20:** now reachable in normal play; a small client-only fix.
   2. **N26:** needs the owner decision first: keep the selection or clear it.
   3. **M10:** already seen at runtime.
   4. **N27:** an Inspector fix.
   5. **Hygiene bundle:** N1, N4, N5, N12, N16, N23. N24 only with owner approval. L9 before any release build.
4. **Architecture docs refresh (N8).** Claude can do this directly under `Docs/AI/Architecture`.
5. **Designer decisions:** N26. M15, M11 and M12 were decided on 2026-10-19 (§6).

---

## 6. Task folder cleanup

I removed these specs from `Docs/AI/Tasks/`. All five were implemented, match their specs, and have no open blocking defects (§1). They're still in git history: `git show fe2d4fb:Docs/AI/Tasks/<file>`. Their remaining manual checks are in §5, step 1.

| Removed | Reason |
|---|---|
| `TASK-H3_BlockHealOverCap.md` | Fixed (`e7d1b03`), matches spec |
| `TASK-H2_DeckRefillWithoutRarestCards.md` | Fixed (`e7d1b03`), matches spec. Also closes M9. |
| `TASK-N14_ReturnToLobbyWhenMatchCannotStart.md` | Fixed (`fe2d4fb`), matches spec |
| `TASK-N18_M1_DeathChecksAndRespawnTimer.md` | Fixed (`fe2d4fb`), matches spec. Closes N18 and M1. |
| `TASK-N19_LeaveMatchWithConfirmation.md` | Fixed (`fe2d4fb`), matches spec. Host surrender seen at runtime. |

**`Docs/AI/Tasks/` was emptied by this cleanup.**

**New specs (written after this report, on the owner's request):** one per open 🟡 Medium issue, in the suggested order:

| # | Spec | Notes |
|---|---|---|
| 1 | [TASK-N20](../Tasks/TASK-N20_SafeSendAndTargetRule.md) | |
| 2 | [TASK-M14](../Tasks/TASK-M14_ClientCardDataNullGuards.md) | Touches `NetworkCard.cs`: keep its encoding (N24) |
| 3 | [TASK-M8](../Tasks/TASK-M8_AnyPlayerTargetViaBlockOnClient.md) | |
| 4 | [TASK-M10](../Tasks/TASK-M10_AsyncLobbyNetworkErrorHandling.md) | |
| 5 | [TASK-M11](../Tasks/TASK-M11_JoinLobbyByCode.md) | Owner (2026-10-19): comment the code out for now. After that, M11 becomes a parked feature. |
| 6 | [TASK-M13](../Tasks/TASK-M13_LocalizeLobbyStrings.md) | |
| 7 | [TASK-M3](../Tasks/TASK-M3_LavaKillsRespawnedCharacter.md) | |
| 8 | [TASK-M6](../Tasks/TASK-M6_UpdatePlayerHpNullChecks.md) | |
| 9 | [TASK-M4](../Tasks/TASK-M4_ServerChecksInsteadOfHost.md) | |
| 10 | [TASK-M16](../Tasks/TASK-M16_ClearSessionManagerInstance.md) | |
| 11 | [TASK-N1](../Tasks/TASK-N1_PlayerUIOnDestroyOverride.md) | |
| 12 | [TASK-M12](../Tasks/TASK-M12_LanguageSettingsScene.md) | Owner (2026-10-19): a separate `SettingsScene` |
| 13 | [TASK-M15](../Tasks/TASK-M15_HandSizeLimit.md) | Owner (2026-10-19): limit 10 for now; over the limit, discard the oldest |

N26 still needs the owner's decision before a spec is written.

The Codex implementation reports (`Docs/AI/Reports/2026-10-19_*_Implementation.md`) and the research documents (`Docs/AI/Research/`) are kept as reference material.

## Sources

- **Diff:** `git diff 058fa63 fe2d4fb`
- **Code read at `fe2d4fb`:**
  - `GameMaster.cs`, `GameMaster.GameLoop.cs`, `GameMaster.Card.cs`, `GameMaster.Processing.cs`, `GameMaster.Player.cs`, `GameMaster.PackageManaging.cs`
  - `PlayerSession.cs`, `PlayerUI.cs`, `LeaveMatchUI.cs`, `ResultMenuUI.cs`, `GameAnnouncerUI.cs`, `BlockController.cs`, `NetworkCard.cs`, `CardWorldViewManager.cs`, `PonyColorScript.cs`
  - `MultiplayerManager.cs` (start, abort and lobby paths)
- **Assets:**
  - `PlayerUIPrefab.prefab` (`LeaveMatchUI` fields and `onClick` targets)
  - `ResultScene.unity`
  - `UI_table` (shared, en, ru, ja)
  - `PonyBase.prefab`
  - `m_debugMode` flags across prefabs and scenes
- **Logs:** `%LOCALAPPDATA%\Unity\Editor\Editor.log` (clone, 10:40) and `Editor-prev.log` (clone, 10:17)
- **Codex output:** `build/task-{h3,h2,n14,n18-m1,n19}-verification/results.txt`. All passed: 139 assertions and 13, 26, 24 and 30 scenarios. These harnesses use Unity/NGO doubles; they aren't Play Mode or player-build results.
