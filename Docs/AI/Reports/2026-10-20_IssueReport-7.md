# Issue Report #7: FallenStar – Spellcasters (re-audit after the 13 Medium tasks)

- **Date:** 2026-10-20
- **Baseline:** `HEAD` = `b7e7b91` ("current medium task completed"). The working tree was clean at the start of the audit.
- **Previous reports:** [#1](2026-10-18_IssueReport.md), [#2](2026-10-18_IssueReport-2.md), [#3](2026-10-18_IssueReport-3.md), [#4](2026-10-18_IssueReport-4.md), [#5](2026-10-19_IssueReport-5.md), [#6](2026-10-19_IssueReport-6.md) (`fe2d4fb`)
- **Method:**
  1. Read the agent instructions: `CLAUDE.md`, `AGENTS.md`, `AI_CONTEXT.md`, the `Docs/AI` documents.
  2. Read the full code and asset diff `fe2d4fb..b7e7b91`:
     - `a0df515`: N20, M14, M8, M10, M3, M4, M6
     - `b7e7b91`: M11, M15, M12, M13, M16, N1
  3. Compared each change with its spec in `Docs/AI/Tasks/` and with Codex's four implementation reports (`2026-10-19_N20_M14_M8_…`, `2026-10-20_M10_M3_M4_M6_…`, `2026-10-20_M11_M15_M12_M13_…`, `2026-10-20_M16_N1_…`).
  4. Re-read the surrounding code:
     - send, target and selection paths
     - draw, graveyard and refill
     - lobby join, host and refresh paths
     - lifecycle overrides
  5. Checked these assets:
     - the 6 new `UI_table` keys in en/ru/ja (same IDs in every table)
     - `PlayerUIPrefab` (one new field)
     - `MainMenuScene` (Settings button and `onClick`)
     - the new `SettingsScene` (scripts, dropdown reference, Back `onClick`, labels, EventSystem, no DDOL managers)
     - `EditorBuildSettings`
     - the font asset
  6. Re-checked every open issue from report #6 at its **current** line.
  7. Read the Editor logs and Codex's verification output in the git-ignored `build/task-*-verification/` folders.
- **I didn't run a build or Play Mode.** No code, scenes, prefabs or settings were changed. The only changes are this report and the task-folder cleanup (§6).

**This report is the full issue list.** §3 lists every open issue and §4 every closed one, so the earlier reports are only needed for history. IDs are unchanged. New findings continue from report #6, starting at N28.

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
| Tasks completed since #6 | All 13: **N20, M14, M8, M10, M3, M4, M6, M11, M15, M12, M13, M16, N1** ✅. Each matches its spec, including the owner decisions of 2026-10-19 (M11 commented out, M12 separate scene, M15 limit 10 with oldest-first discard). No blocking defects. |
| Compile state | Codex: Roslyn compile of the runtime sources with the project's response file, exit 0, for all four task groups. The Unity Editor compile in the main project shows only **CS0414** (N4). **CS0114 is gone** (N1). |
| Runtime evidence | Only Codex's Editor session: Settings navigation and a localization check of the real lobby and HUD prefabs in en/ru/ja. **No two-player match has run since report #6**, so none of the gameplay fixes has been seen in a real match yet. |
| Player build | ❓ Not re-run since 2026-10-18 (U1). This now also matters because the build scene list changed (M12). |
| 🔴 Critical / 🟠 High / 🟡 Medium open | **0 / 0 / 0** |
| 🟢 Low open | 23, including 1 new: **N28** (the dynamic font atlas is committed and changes with almost every UI task) |
| ⚪ Info open | 4 plus the naming group, including 1 new: **N29** (the lobby shows the player ID twice) |
| Task files in `Docs/AI/Tasks/` | All 13 specs removed as done. **The folder is empty** (§6). |

**Overall:** no Critical, High or Medium issue is open for the first time since report #1. What remains:
- the manual two-player checks for the last two rounds (report #6 and this one)
- one fresh player build
- Low and Info hygiene
- one owner question (N26)

---

## 1. Review of implemented tasks

| Task | Commit | What changed | Matches spec? | Verdict | Notes |
|---|---|---|---|---|---|
| **N20** | `a0df515` | New `CardTargetRules.RequiresSelectedTarget` (`CardData.cs`). The server check (`GameLoop.cs:265-275`) uses it, with identical behavior. `PlayerSession.ClearSelectedCard` resets a non-empty package. `CheckPackage` rewritten in the specified order. `TryHandlePackage` checks for a null `GameMaster`. | Yes. The helper's 8 "no target" types equal the old server switch. | ✅ **Fixed** (static) | Codex harness: 35 scenarios, all 14 target types. When Send is ignored because a required target is missing, the last check returns `false` without a log line. The player gets no feedback; a UI message was a non-goal. |
| **M14** | `a0df515` | Guards in `TryConfirmTarget`, `AutoSelectTarget` (bounded loop), `CheckActiveTaergetSelection`, `CardViewUI.OnPointerClick`. `RepairCardData` keeps the old data on a failed lookup (`TryGetCard`). New `PlayerUI.OnCardDataReceived`, called from `SendCardDataClientRpc`. | Yes | ✅ **Fixed** (static) | **`NetworkCard.cs` encoding kept:** the diff touches only the two methods, and the legacy comment block isn't in the diff. The rebuild works in either message order: if the data comes first, the card isn't in the list yet; if the list comes first, the data triggers the rebuild. |
| **M8** | `a0df515` | Private `GetBlockOwnerSession` (by `m_teamId`). The `AnyPlayer` branch handles a null character session and an unknown block owner. | Yes. No client-side `BlockController.GetSession()` is left. | ✅ **Fixed** (static) | Still latent: no card uses `AnyPlayer`. |
| **M10** | `a0df515` | General `catch (Exception)` in `Start` (two separate tries), `OnApplicationQuit`, `HandleLostConnectionToHost`, `ShutdownHostInternal`, `DisconnectClientInternal`, `HearthbeatLobby` (plus a null check), `StartHost` (plus `ResetAfterFailedHostStart`), `FindAvailableLobbies`, `JoinLobbyById` (leave and shut down on failure), `JoinRelay` (shut down on failure), `RefreshCurrentLobby` (now `Task<bool>`), `OnLoadEventCompleted` (abort reason `game_master_create_failed`). | Yes | ✅ **Fixed** (static) | Two small additions beyond the spec, both reasonable:<br>• `LeaveCurrentLobbyAfterFailedJoin` also catches `Exception` and resets the status in `finally`.<br>• The duplicate "Failed to join Relay" log was removed.<br>Codex harness: 51 scenarios with SDK doubles. No real offline test was run. |
| **M3** | `a0df515` | `LavaKillZone` guards by instance (`HashSet<PlayerController>`) and drops destroyed entries first. | Yes | ✅ **Fixed** (static) | The lambda captures nothing, so it doesn't allocate on each call. The recycled-ID check (temporary recycle delay of 1 s) hasn't been run. |
| **M4** | `a0df515` | `GameMaster.Update` and `HandlePlayerControlPackage` use `IsServer` | Yes. No `IsHost` is left under `InGame/`. | ✅ **Fixed** | |
| **M6** | `a0df515` | `UpdatePlayerHP`: null-session check and null spawn-point check | Yes | ✅ **Fixed** | |
| **M11** | `b7e7b91` | `m_joinCodeInput` and `JoinLobbyByJoinCode()` commented out with `//todo … (M11)` notes (`MultiplayerManager.cs:28-29`, `:948-995`). The body is unchanged. | Yes (owner: comment out) | ✅ **Closed as parked.** Moved to the feature gaps. | `m_lobbyCode` / `m_lobbyCodePanel` (`:38-39`) stay unused (L10). |
| **M15** | `b7e7b91` | `m_maxHandCards = 10` (`GameMaster.cs:81-82`). `DiscardOldestOverHandLimit` runs after a **successful** draw (`GameMaster.Card.cs:184-232`): it discards the oldest **uncommitted** cards through `MoveCardOnGraveyard` and sends an owner-only `HandLimitDiscardClientRpc`. `PruneLocalCardSelection` runs in `PlayerUI.UpdateCards` and before `ConfirmTurn`. `PlayerUI.ShowMessage` / `m_activeMessage` keep the visible message correct on a locale change. | Yes (owner: limit 10, discard the oldest) | ✅ **Fixed** (static) | **Checked:**<br>• graveyard cards aren't drawn in the world, so a discarded hand card isn't shown to the opponent<br>• during Clarity, the Clarity card and the other cards waiting to be played are committed, so they're never discarded<br>• the pruning stops a carried-over N26 selection from creating a false `[Suspicious] InvalidCardSelection`<br>Codex harness: 16 scenarios. |
| **M12** | `b7e7b91` | New `SettingsScene` with `SettingsUI` (dropdown wired), `SettingsSceneButtonsUI` (Back), localized title, label and Back, `InputSystemUIInputModule`, no DDOL managers. `MainMenuScene` Settings button → `OnClickSettings`. `EditorBuildSettings`: one enabled entry after `MainMenuScene`. `SettingsUI`: missing-dropdown guard, native language names. | Yes (owner: separate scene) | ✅ **Fixed**, seen in the Editor | Codex ran real Play Mode navigation from `LoginScene`: Settings and Back clicks, language switch in all 3 languages, and the choice survived a Play Mode restart. Not checked at other resolutions or in a player build. No code loads scenes by build index, so the shifted scene indices are safe. |
| **M13** | both | Role constants `m_lobbyRoleHost` / `m_lobbyRoleClient`. `GetShortPlayerId()` is the player name and the lobby name. `LobbyListItemUI` count uses `lobby_players_count`; "unknown" fallbacks use the new keys in `LobbyListUI`, `LobbyListItemUI` and `LobbyPlayerItemUI`. | Yes | ✅ **Fixed**, prefab localization seen in the Editor | The new `LocalizedString` fields have initializers, so the lobby prefabs needed no edits. Cosmetic follow-up → **N29**. |
| **M16** | `b7e7b91` | `SessionManager`: `public override void OnDestroy()` (clears `Instance` only if it's this one, then calls the base). The list unsubscribe moved before the null-`NetworkManager` early return. | Yes | ✅ **Fixed** (static) | |
| **N1** | `b7e7b91` | `PlayerUI`: `public override void OnDestroy()` + `base.OnDestroy()` | Yes | ✅ **Fixed.** CS0114 is gone from the Editor compile. | |

**Assets checked:**
- **Localization:** the 6 keys (`btn_settings`, `settings_title`, `label_language`, `lobby_name_unknown`, `lobby_player_unknown`, `hand_limit_discard`) have the same IDs in the shared table and in en/ru/ja, and the texts match the specs.
- **`PlayerUIPrefab`:** only `m_handLimitDiscardText` was added, referenced by key name like `m_manaNotEnoughText`.
- **Font:** `MPLUSRounded1c-Medium SDF.asset` grew by about 320 lines of dynamic glyph data → **N28**.

---

## 2. New findings and updates

| ID | Level | Issue | Location | Evidence | Verdict |
|---|---|---|---|---|---|
| **N28** | 🟢 Low | **The dynamic font atlas is committed and changes with almost every UI task.** The TMP font asset uses dynamic population (`m_AtlasPopulationMode: 1`) and keeps its runtime data (`m_ClearDynamicDataOnBuild: 0`). Every new glyph rendered in the Editor (ru/ja text, new labels) rewrites the asset, so unrelated commits carry large font diffs that are hard to review. | `Assets/Fonts/MPLUSRounded1c-Medium SDF.asset:170`, `:5544` | Changed in 6 commits, most recently `b7e7b91` (+320/−34), `fe2d4fb`, `058fa63`, `ad3a46f` | Confirmed. Options: (a) a static atlas generated from the en/ru/ja character sets, with a dynamic fallback asset that isn't committed or is cleared on build; (b) enable "Clear Dynamic Data On Build" and leave Editor-only atlas changes out of commits. Owner choice. |
| **N29** | ⚪ Info | **The lobby shows the player ID twice.** Since M13, `LobbyPlayerItemUI` shows the short ID as the name (`m_playerNameText`) and the full `player.Id` under it (`m_playerIdText`). Not wrong, but redundant. | `UI/LobbyPlayerItemUI.cs:53-63` | Code | Confirmed (cosmetic). Possible follow-up from M13 Q1: a localized display such as "Player a1b2c3" / "Lobby a1b2c3", or hide the full-ID text. |

**Updates to existing items:**
- **N26 partly mitigated by M15:** a carried-over unconfirmed selection now loses any card that has left the hand, so it can't flag an honest player. The carry-over of cards still in the hand remains. The owner decision is still open.
- **L7 not reachable in normal play:** since M12 the language can only be changed in the main menu, before any card exists. It only shows with the Editor's Localization Scene Controls during a match.
- **L10:** `SettingsSceneButtonsUI` is now used by `SettingsScene`, so it's removed from the dead-code list. Added: the unused `MultiplayerManager.DisconnectClient()` (`:1139`) and the unused `m_lobbyCode` / `m_lobbyCodePanel` fields (`:38-39`, part of the parked join-by-code feature).
- **N24:** still 3 non-UTF-8 files. M14 edited `NetworkCard.cs` and kept its encoding, so the warning in specs works.
- **U9 seen again:** `NetworkSceneManager.Dispose` → `NetworkManager.OnDestroy` NRE inside NGO when Codex stopped Play Mode (`Editor.log:1292`, `:1306`). Still only during teardown.
- **U11 closed:** 0 occurrences of "The referenced script (Unknown)…" in the main project's latest Editor session, across about 50 domain reloads.
- **Log note:** the current `%LOCALAPPDATA%\Unity\Editor\Editor.log` is mixed. Its header belongs to another project (`C:\Lesson\TIME-HOLLOW`, Unity 6000.6.2f1), and later lines come from FallenStar's main project and its clone. Line numbers above are from this file; any gameplay evidence in it should be read with that in mind.

---

## 3. All open issues

### 🔴 Critical / 🟠 High / 🟡 Medium

None open.

### 🟢 Low (23)

| ID | Issue | Current location | Verdict |
|---|---|---|---|
| **N28** | Dynamic font atlas data is committed and changes with UI work (new, §2) | `Assets/Fonts/MPLUSRounded1c-Medium SDF.asset:170`, `:5544` | Confirmed |
| N26 | An unconfirmed card selection carries over to the next round. Since M15, cards that left the hand are dropped from it. **Owner decision:** keep it ("remembered selection"), or clear it when a **new** `CardSelection` starts but keep it when resuming from `RespawnWait`. | `InGame/PlayerSession.cs:201`, `:227`, `:741` (only callers of `ClearLocalCardSelection`); `UI/PlayerUI.cs:312-319` | Confirmed |
| N27 | The pony mane color is never applied: `maneObject` isn't assigned, and every spawn logs "Invalid mane material index." | `Assets/Prefab/BoardPrefab/PonyBase.prefab:368`; `InGame/PonyColorScript.cs:70-88` | Confirmed |
| N2 | NetworkVariables are written before the session spawns | `MultiplayerScripts/SessionManager.cs:245` → `PlayerSession.Initialize` | Confirmed |
| N3 | Client-side `CanPlayerMove` / `CanPlayerSelectCardTargets` / `CanPlayerSelectCards` read server-only flags (`m_isResolvingCards`, `m_isWaitingRespawn`) | `InGame/GameMaster/GameMaster.GameLoop.cs:15-39` | Confirmed |
| N4 | Dead `ValidateCardOwner`; unused `m_cardResolveDelay`, which is now the **only** compiler warning (CS0414) | `GameMaster.PackageManaging.cs:490`; `GameMaster.cs:74` | Confirmed |
| N5 | Missing `$` in a log string (`"Client disconnected: {_package.ClientId}"`) | `GameMaster.Processing.cs:40` | Confirmed |
| N6 | Card packages already queued still resolve after a match-deciding activity. The `break` only leaves the activity loop. | `GameMaster.GameLoop.cs:185-321` (`break` at `:308`) | Confirmed |
| N7 | `GetCharacter` assumes the object has a `PlayerController` | `InGame/PlayerSession.cs:659-688` | Confirmed (latent) |
| N12 | `RunGuarded` would spin on a yielded `CustomYieldInstruction`. Latent: the loop yields only `null` and nested phases. | `GameMaster.GameLoop.cs:41-83` | Confirmed (latent) |
| N16 | When a player disconnects, their suspicious-activity summary isn't logged: they're removed from `m_players` before `EndMatch`. The surrender path is fine. | `GameMaster.Player.cs:60` (removed), `:82` (`EndMatch`) | Confirmed |
| N21 | A late selection confirm is read in the server's **current** phase. At the end of `CardSelection` it becomes "end turn"; when a pause starts, it's rejected and the selection is lost. | `GameMaster.PackageManaging.cs:180-249` (switch at `:207`); `PlayerSession.cs:170-202` | Plausible (latency window) |
| N22 | Character HP is set one frame after spawn (`DelayedInit`); until then HP is 1/1 | `InGame/PlayerController.cs:17-18`, `:63`, `:92-101`, `:127` | Confirmed (latent) |
| N23 | `Keyboard.current` used without a null check | `MultiplayerManager.cs:164`; `PlayerController.cs:167-185` | Confirmed (low probability on PC) |
| L1 | `m_timer` is written every frame (network traffic every tick) and shown as **elapsed** time, not remaining | phase loops in `GameMaster.GameLoop.cs`; `UI/GameAnnouncerUI.cs:56` | Confirmed |
| L2 | `CreateDeck` `Random.Range(0, 2)` never reaches `case 2` | `GameMaster.Card.cs:271` | Confirmed |
| L3 | `IsGameFinished()` has a side effect (`FindLostPlayer` sets `m_externalDeathDetected`) and allocates. It runs every frame in every phase. | `GameMaster.GameLoop.cs:793-830` (calls `FindLostPlayer` at `:826`; defined at `:614`) | Confirmed |
| L4 | Blocks have two team NetworkVariables (`m_teamId`, `TeamId`), read by different code | `InGame/BlockController.cs:12`, `:23` | Confirmed |
| L6 | Per-frame polling and allocations:<br>• `GameAnnouncerUI` creates `LocalizedString` objects every frame (`:155`)<br>• `CardWorldViewManager` re-registers every frame after a player leaves<br>• `PlayerUI`, `LobbyControlUI`, `DisconnectMenuUI`, `SpawnPanelUI` poll<br>• `LeaveMatchUI.Update` copies the canvas size (`:25-29`) | listed files | Confirmed |
| L7 | Card texts don't refresh on a locale change. **Not reachable in normal play since M12.** | `InGame/CardViewUI.cs:30-40`; `InGame/CardViewWorld.cs:58` | Confirmed (latent) |
| L8 | `UI_table` key `"btn_lobbyRefresh\t"` has a trailing tab | `Assets/Localization/UI_table Shared Data.asset:175` | Confirmed |
| L9 | `m_debugMode: 1` on `GameMasterPrefab`, `PlayerSessionPrefab`, `SessionManager.prefab`, `CardWorldManagerPrefab`, `MultiplayerManager.prefab`, `ResultScene` (and the legacy `SampleScene`). The debug HP key (Space) is active in every Editor and development build. The `GameMasterPrefab` rule fields, including the new `m_maxHandCards`, aren't serialized, so C# defaults apply. | listed assets | Confirmed |
| L10 | Dead or legacy code and files:<br>• `ResolvePlayerCardsPhase` (`GameLoop.cs:407`) and the commented-out `CardMessageQueueUpdate`<br>• the `SendToClientRpc` echo and `PlayerController.ReceiveValidatedInput` (`:289`)<br>• `TargetDataBackup`, `SpawnPanelUI` / `DisconnectMenuUI` (legacy scenes only), `DisconnectButtonUI`, `LocalizedTMPFormat`<br>• **new:** `MultiplayerManager.DisconnectClient()` (`:1139`) and the unused `m_lobbyCode` / `m_lobbyCodePanel` (`:38-39`)<br>• the repo-root `26.06.19/` backup folder, `Assets/_Recovery/0.unity`<br>• the `Unity.VisualScripting` dependency for `ConvertTo<int>()` | listed | Confirmed |

### ⚪ Info

- **N8: documentation drift.** Codex keeps `GameLoop.md`, `Dependencies.md` and `Architecture.md` in step with each task. Still wrong or missing:
  - `GameLoop.md:45` still says "End of match → Result: Not implemented"
  - 0 mentions of `RunGuarded`, `LeaveMatchToResultScene` or `SuspiciousActivityLog` in the architecture docs
  - `README.md:24` still describes a "destroy all enemy blocks" win
  - `test.md` is still at the repo root
- **N17:** after a forfeit, the host's HUD goes blank for a moment before `ResultScene` loads. Cosmetic.
- **N24:** three scripts aren't UTF-8: `InGame/NetworkCard.cs`, `InGame/PlayerCardfWorldArea.cs`, `Manager/CardWorldViewManager.cs`. Convert them only with owner approval. Until then, specs that touch them must tell Codex to keep the encoding (it worked in M14).
- **N29:** the lobby shows the player ID twice (new, §2).
- **Naming and typos (report only, don't rename):** `CardControllPackage`, `PlayerCardfWorldArea.cs`, `SetOponent` / `m_currentOponentNetworkObjectId`, `CheckActiveTaergetSelection`, `SingInAnonymous` / `SingOut` / `SingOutAndClearCredentials` / `m_isSingingIn`, `HearthbeatLobby`, `MovePlayeToSpawnPoint`, `ClearupAfterDisconnect`, `CardSpecialization.DeffenceSpell`, `KnowLobbies` / `m_knowLobbies`, `m_bootsTrap`, `_teamAssing`, `m_phaseAnouncementText`, `#region Valiable`, the log text `"Packege"`, the local variable `_locakPoint`, `ConfirmSendButtonUI1` (in `ConfirmSendButton.cs`), and `m_startHandCards`, which is really the per-round draw count.

### Feature gaps (not defects, listed so they aren't mistaken for bugs)

- **Join by lobby code is parked** (M11, owner 2026-10-19): commented out at `MultiplayerManager.cs:28-29` and `:948-995`. §10 of the old M11 spec (git history) lists what to fix before restoring it.
- The language can be changed only in the main menu (M12 non-goal). There are no other settings (audio, graphics, controls).
- Steam login is a stub (`AuthManager.SignInSteam`).
- `GamePhase.DrawCards` and `RoundEnd` are never set, and `ResolveCards` is only transient. During draws the announcer shows "Round break".
- The `DiscardCard*` / `DrawCard*` combo actions, the `All*` / `Random*` targets and `ActivityEffect` have no effect.
- The "destroy all enemy blocks" win rule isn't implemented directly. A player with no blocks is eliminated on their next death.
- `SessionRole.Spectator` exists but nothing uses it.
- No player-facing messages for lobby or network failures (M10 Q1, N14 Q1), for an ignored Send (N20), or for a deck refill (H2 Q1).
- The card selection and confirmed target don't survive a respawn pause (N20 Q1, by design).

### ❓ Unknowns: open

| ID | Question | Status |
|---|---|---|
| U1 | Does a player build compile and run on `HEAD`? | ❓ Last real build: 2026-10-18. Not re-run since H1 and all later tasks, including the new `SettingsScene` in the build list. |
| U2 | Nested `SpawnPoint` NetworkObject inside `BlockBody` | ❓ No NGO warnings in any log |
| U5 | Unresolved network-prefab GUID `e651dbb3…` | ❓ |
| U6 | Double `GameScene` load in a clone log | ❓ Not seen in the latest log (no match was run) |
| U8 | Does `Shutdown()` leave no objects behind (H7 §9.2)? | ❓ Partial evidence in #6 |
| U9 | NGO `NetworkSceneManager.Dispose` NRE on Play Mode stop, Editor quit or domain reload | ❓ Seen again (`Editor.log:1292`). Inside NGO, during teardown; likely harmless. |
| U10 | Does the H6 forfeit work at runtime in each phase? | ❓ Still no `OpponentDisconnected` result in any log |
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
| **M3** | Lava guard keyed by instance; a recycled ID no longer protects a respawned pony | `a0df515` (**#7**) |
| **M4** | `GameMaster` input gates use `IsServer` | `a0df515` (**#7**) |
| M5 | Fixed by C3 | #2 |
| **M6** | `UpdatePlayerHP` null checks | `a0df515` (**#7**) |
| M7 | Fixed by H4 | #4 |
| **M8** | `AnyPlayer` through a block resolves the owner by team on clients | `a0df515` (**#7**) |
| M9 | `DrawCard(null)` guard (part of H2) | `e7d1b03` (#6) |
| **M10** | Async lobby and network errors caught, with state rollback | `a0df515` (**#7**) |
| **M11** | Parked: join-by-code commented out (owner decision); moved to the feature gaps | `b7e7b91` (**#7**) |
| **M12** | Language selection in the new `SettingsScene` | `b7e7b91` (**#7**) |
| **M13** | Neutral lobby data; localized counts and fallbacks | `a0df515`, `b7e7b91` (**#7**) |
| **M14** | Client card-data null guards; hand rebuild when the data arrives | `a0df515` (**#7**) |
| **M15** | Hand limit 10, oldest uncommitted cards discarded (owner decision) | `b7e7b91` (**#7**) |
| **M16** | `SessionManager.Instance` cleared in the `OnDestroy` override | `b7e7b91` (**#7**) |
| **N1** | `PlayerUI.OnDestroy` overrides NGO's; CS0114 gone | `b7e7b91` (**#7**) |
| N9 | Superseded by MANA2 ("first **valid** confirm is final") | #5 |
| N10, N11, N13 | Closed when H5 was finished | #4 |
| N14 | Host returns to the lobby when a match can't start | `fe2d4fb` (#6) |
| N15 | Number not used | #4 |
| N18 | Deaths are handled in every phase | `fe2d4fb` (#6) |
| N19 | Leave button with confirmation, counts as a defeat | `fe2d4fb` (#6) |
| **N20** | Safe Send; the client target rule is shared with the server | `a0df515` (**#7**) |
| N25 | Intended (owner, 2026-10-19) | #5 |
| L5 | `GameAnnouncerUI` maps every existing phase | #4 |
| U3, U4 | Answered (null `NetworkBehaviourReference` is safe; `DontDestroyWithOwner: 0`) | #3 |
| U7 | Normal end reaches `ResultScene` on both instances | #4 |
| **U11** | Not reproduced: 0 missing-script warnings across about 50 domain reloads in the main project | **#7** |
| U13 | A second lobby and match work after result → main menu | #6 |

Tasks completed without an issue ID: MANA1–MANA3 (`ade4aed`, `5eec046`, `058fa63`) and SEC1 (`f08e854`).

---

## 5. Suggested next steps (in order)

1. **Run the manual two-player checks.** None of the gameplay fixes from #6 or #7 has been seen in a real match.
   - **Report #6 round:** [ManualChecks-Report6](../ManualChecks/2026-10-19_ManualChecks-Report6.md) is still valid.
     - **Note:** its "A5 — Card Selection" and "E — known issues" entries for N20 are now fixed behavior.
     - **Note:** with the hand limit at 10, long matches discard old cards, which is expected.
   - **This round, key checks** (from each spec's §9; the specs are in git history, §6):
     - **N20:** deselect → Send, and a respawn pause → Send. A warning and no exception.
     - **M15:** temporarily set `m_maxHandCards = 3` in `GameMasterPrefab`. The leftmost card is discarded, an owner-only message appears, cards waiting to be played survive Clarity, and a carried-over selection doesn't produce `[Suspicious]`.
     - **M12:** Settings in all 3 languages, the choice survives a restart, other resolutions.
     - **M13:** host and client with different languages. The lobby shows neutral IDs, and counts use each viewer's language.
     - **M10:** create a lobby, refresh and join while offline (no listening host is left behind). Temporarily throw in `RecreateGameMaster` → host returns to the lobby with `game_master_create_failed`.
     - **M3:** temporarily set the NGO `NetworkIdRecycleDelay` to 1 s, then a lava death after a respawn with a reused ID.
     - **M8:** temporarily set Iceshard to `AnyPlayer`, then target through a block on the client.
     - **M16/N1:** two matches in one Play session with no cleanup errors.
2. **One fresh Windows player build on `HEAD`** (U1). Include the Settings scene and Japanese glyphs in the check.
3. **Owner decisions:** N26 (keep or clear an unconfirmed selection), N28 (font atlas handling), N24 (convert the 3 files to UTF-8?).
4. **Candidates for the next specs (all Low):**
   1. **Hygiene bundle:** N4 (removes the last warning), N5, N12, N16, N23, L2, L8.
   2. **N27:** an Inspector fix (assign the mane renderer), or remove the mane branch.
   3. **L9:** before any release build, set `m_debugMode: 0` on the listed prefabs and `ResultScene`.
   4. **N6:** stop resolving queued packages after a match-deciding activity.
   5. **L10 cleanup:** delete the dead code and files, including `Assets/_Recovery/` and `26.06.19/`. Needs owner approval for file deletions.
5. **Architecture docs refresh (N8).** Claude can do this directly under `Docs/AI/Architecture`, plus `README.md`.

---

## 6. Task folder cleanup

I removed these specs from `Docs/AI/Tasks/`. All 13 were implemented, match their specs and have no open blocking defects (§1). They're still in git history (`git show b7e7b91:Docs/AI/Tasks/<file>`). Their manual checks are summarized in §5, step 1.

| Removed | Reason |
|---|---|
| `TASK-N20_SafeSendAndTargetRule.md` | Fixed (`a0df515`) |
| `TASK-M14_ClientCardDataNullGuards.md` | Fixed (`a0df515`), encoding kept |
| `TASK-M8_AnyPlayerTargetViaBlockOnClient.md` | Fixed (`a0df515`) |
| `TASK-M10_AsyncLobbyNetworkErrorHandling.md` | Fixed (`a0df515`) |
| `TASK-M3_LavaKillsRespawnedCharacter.md` | Fixed (`a0df515`) |
| `TASK-M4_ServerChecksInsteadOfHost.md` | Fixed (`a0df515`) |
| `TASK-M6_UpdatePlayerHpNullChecks.md` | Fixed (`a0df515`) |
| `TASK-M11_JoinLobbyByCode.md` | Done as specified (code commented out, `b7e7b91`). Its §10 restore notes stay in git history. |
| `TASK-M15_HandSizeLimit.md` | Fixed (`b7e7b91`) |
| `TASK-M12_LanguageSettingsScene.md` | Fixed (`b7e7b91`) |
| `TASK-M13_LocalizeLobbyStrings.md` | Fixed (`a0df515`, `b7e7b91`) |
| `TASK-M16_ClearSessionManagerInstance.md` | Fixed (`b7e7b91`) |
| `TASK-N1_PlayerUIOnDestroyOverride.md` | Fixed (`b7e7b91`) |

**`Docs/AI/Tasks/` is now empty.** No new specs were written in this round. §5 lists the candidates, which need the owner's go-ahead.

The Codex implementation reports (`Docs/AI/Reports/2026-10-*_Implementation.md`), the research documents (`Docs/AI/Research/`) and the report #6 manual checklist are kept as reference material.

## Sources

- **Diff:** `git diff fe2d4fb b7e7b91` (code, scenes, prefab, localization, build settings, font)
- **Code read at `b7e7b91`:**
  - `PlayerSession.cs`, `PlayerUI.cs`, `CardData.cs`, `CardViewUI.cs`, `NetworkCard.cs`, `LavaKillZone.cs`
  - `GameMaster.cs`, `GameMaster.Card.cs`, `GameMaster.GameLoop.cs`, `GameMaster.Processing.cs`, `GameMaster.PackageManaging.cs`, `GameMaster.Player.cs`
  - `MultiplayerManager.cs`, `SessionManager.cs`
  - `LobbyListUI.cs`, `LobbyListItemUI.cs`, `LobbyPlayerItemUI.cs`, `MainMenuUI.cs`, `SettingsUI.cs`
  - `CardWorldViewManager.cs` (graveyard display)
- **Assets:** `SettingsScene.unity` (+ `.meta`), `MainMenuScene.unity`, `PlayerUIPrefab.prefab`, `UI_table` (shared, en, ru, ja), `EditorBuildSettings.asset`, `MPLUSRounded1c-Medium SDF.asset`, `PonyBase.prefab`, `m_debugMode` flags across prefabs and scenes
- **Logs:**
  - `%LOCALAPPDATA%\Unity\Editor\Editor.log` (2026-10-20 03:59). Mixed: another project's header plus FallenStar's main project and clone. It contains Codex's Editor session.
  - `Editor-prev.log` (2026-10-19 10:17, already used in #6)
- **Codex output:**
  - `build/task-n20-m14-m8-verification/results.txt`: 35 passed
  - `build/task-m10-m3-m4-m6-verification/results.txt`: 51 passed
  - `build/task-m11-m15-m12-m13-verification/results.txt`: 16 passed, plus the Play Mode localization check and Settings navigation
  - `build/task-m16-n1-verification/`: compile only
  - every `compile-exit.txt` = 0

  The harnesses use Unity, NGO and SDK doubles. They aren't two-player Play Mode or player-build results.
