# Issue Report #9: FallenStar – Spellcasters (re-audit after UI1–UI4)

- **Date:** 2026-10-21
- **Baseline:**
  - `HEAD` = `391e079`. There are no new commits since report #8.
  - **plus the uncommitted working tree:**
    - the tasks from report #8 (L1, L2/N31, N30, N17, L6, L7, L9, L10, N28, N8)
    - **UI1–UI4**, implemented by Codex today
    - Claude's documents: the UI research and the UI1–UI4 specs
- **Previous reports:** [#1](2026-10-18_IssueReport.md), [#2](2026-10-18_IssueReport-2.md), [#3](2026-10-18_IssueReport-3.md), [#4](2026-10-18_IssueReport-4.md), [#5](2026-10-19_IssueReport-5.md), [#6](2026-10-19_IssueReport-6.md), [#7](2026-10-20_IssueReport-7.md), [#8](2026-10-20_IssueReport-8.md), and the research [RESEARCH-UIScaling_Visibility](../Research/RESEARCH-UIScaling_Visibility.md), which opened N37–N46 and U14.
- **Method:**
  1. Read the four specs (`Docs/AI/Tasks/TASK-UI1…UI4`) and Codex's [implementation report](2026-10-21_UI1_UI2_UI3_UI4_Implementation.md).
  2. Diffed the 13 changed files against Codex's pre-task snapshot (`build/ui1-ui4-verification/baseline/`). This separates the UI changes from the earlier uncommitted work in the same files.
  3. Checked the YAML directly:
     - the HUD tree: the root, the 12 rows of UI3 §4.2 and every scale
     - the GameScene PrefabInstance overrides and the network hashes
     - the six CanvasScalers and the Login background fitter
     - the four localization values
     - the font asset (flag, tables, hash)
  4. Read Codex's evidence:
     - result files `edit-results.txt`, `play-results.txt`, `asset-results.txt` and `build-results.txt`
     - the screenshots
  5. Read the logs:
     - today's `Editor.log`, written by the ParrelSync clone between 13:57 and 14:45
     - `Player.log` and `Player-prev.log`, unchanged since 2026-10-20
     - the build's saved screen settings in the registry
  6. Re-located every open issue from report #8 at its current line.
- **I didn't run a build or Play Mode.** No code, scenes, prefabs or settings were changed. The only changes are:
  - this report
  - a status line in the research document
  - the new spec [TASK-UI5](../Tasks/TASK-UI5_ResolutionSetting.md), which the owner asked for

**This report is the full issue list.** §3 lists every open issue, §5 every closed one. IDs are unchanged. New findings would continue at **N47**; this audit found none.

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
| Tasks completed since #8 | **All 4** ✅: UI1, UI2, UI3, UI4, all in the working tree. Each matches its spec, decisions D0–D7 and the owner's remark R1. One acceptance criterion in my UI2 spec was wrong; Codex flagged it, and it needs no change (§1). **No blocking defect.** |
| Compile state | Codex: Unity compile with no C# errors or warnings. After the temporary helper was removed, the main Editor recompiled successfully. |
| Player build | Codex built a release Windows player: **succeeded, 0 errors, 2 warnings**. Both warnings come from the build environment (Addressables profile, Unity Services link), not from project code. |
| Runtime evidence | **Codex's harnesses:** 51 announcer cases, 198 hand cases, 27 preview placements and 166 asset assertions, all passed. **The clone Editor today:** one full two-player match on the new code, through to `ResultScene`. It logged no game-script exception and no truncation warning. The owner's **visual** checks are still to do. |
| 🔴 Critical / 🟠 High / 🟡 Medium open | **0 / 0 / 0** |
| 🟢 Low open | **5**, unchanged: N32, N33, N34, N35, and N27 (parked) |
| ⚪ Info open | N36, N29 (parked), the naming list |
| Unknowns open | **3**: U2, U9, U14 |
| Closed in this report | **N37–N46** (10 items, §5) |
| New task | **TASK-UI5:** resolution and display-mode selection in Settings (owner request, §6) |
| Task files | UI1–UI4 are done, but I **kept** them because they aren't in git yet (§7). UI5 is added. |

**Overall:** the GUI-scaling and visibility work is in. Every finding from the UI research is fixed in assets and code. The fixes were measured in Unity by Codex, and a real two-player match ran on them without errors. The register is back to report #8's Low and Info hygiene items, plus U14. What remains is visual: the owner's look at the HUD, the hand and the menus at a few resolutions (§4 D). Then the working tree can be committed.

---

## 1. Review of implemented tasks (working tree, not committed)

| Task | What changed | Matches spec? | Verdict | Notes |
|---|---|---|---|---|
| **UI1** | `PhaseAnouncer`: scale (1, 1, 1), 1560×150, margins 0, centred. GameScene `GameAnnouncerUI` 24/48, with matching TMP overrides. en/ru prefixes "Current turn: " / "Respawning: " / "Сейчас ходит: " / "Возрождается: ". One warning per distinct truncated text (`UI/GameAnnouncerUI.cs:20`, `:201-202`). | Yes | ✅ **Fixed:** N37, N38, the announcer part of N41 | All 24 line-2 variants and 27 phase-only lines fit at **font size 48** (AC2 asked for ≥ 30). `Announcer-en/ru/ja.png` show both lines in full, with round glyphs. The L6 change detection is untouched. The ru values are stored as plain UTF-8 instead of Unity's `\u` escapes. That's valid YAML and renders correctly; Unity will rewrite them as escapes the next time it saves the table, a harmless diff. |
| **UI2** | MainMenu, Settings, Lobby and Result: Scale With Screen Size, 1920×1080, Expand. Login and Game: Expand. Login `BackGround`: stretch anchors plus an `AspectRatioFitter` (Envelope Parent, 1.7769). | Yes | ✅ **Fixed:** N39, N42 | **My spec error, caught by Codex:** see the paragraph below this table. Codex's check: in all six scenes, every RectTransform (inactive panels included) stays inside the canvas at all 9 resolutions. 0 missing scripts, build list unchanged. |
| **UI3** | HUD root stretched in the prefab and in the scene overrides. The 12 rows of §4.2 (I re-checked each value in the YAML). `Panel` and `ConfirmTurnBtn` scale 1: no non-uniform scale is left under the HUD. `LeaveMatchUI` workaround removed. `PlayerUI`: `m_handCardWidth` (`:24`), the cached layout group (`:80-85`), `UpdateHandSpacing` (`:318-338`) at the end of `UpdateCards` and in `ClearLocalUI` (`:653`), and `SetActive(false)` before `Destroy` in both loops (`:541-566`). | Yes | ✅ **Fixed:** N40, N41, N45, N46 (N46 verified in a harness) | The measured preferred width of a card root is **0**, as assumed. 198 Play Mode cases (0–10 cards × 2 widths × 9 resolutions): the hand is centred ±1 unit, stays inside ±900 units, clears all three buttons, and old views are inactive in the rebuild frame. The 10-card screenshot shows about 50 units between the hand and the buttons. The network hashes (`3051432279`, `151063585`) are kept. A pre-existing scene override turns autosize on for the Confirm-turn label, so in GameScene it autosizes in its new 255×93 rect, as the spec expected. |
| **UI4** | `Start` picks the HUD's root canvas, then a root screen-space canvas, else logs one warning. `ShowPreview` returns early without a canvas, rebuilds the preview's layout, and clamps the preview in canvas units (`InGame/CardPreviewController.cs:16-42`, `:57`, `:72-82`). `ConfirmBtn` label autosize minimum 14. | Yes | ✅ **Fixed:** N43, N44 | 27 placements at 3 resolutions; the HUD canvas is chosen even when a world-space canvas exists. The Confirm label fits in every locale: ru at size **15.65** (99.8 of 100 units), en at 26.15, ja at 50. **Residual (not a defect):** the ru label is about 60 % of the en size, roughly 16 px on 2560×1600. If it's hard to read in play, the next step is a wider button, which R1 allows. |
| Docs | Codex updated `Architecture.md`, `Dependencies.md` and `GameLoop.md` for UI1–UI4 | — | ✅ | Checked against the code; accurate. |

**The UI2 spec error.** UI2's acceptance criterion 4 said that an element's share of the screen height matches 1920×1080 "on 16:10 and wider". That's wrong for 16:10:
- With a 16:9 reference, Expand is limited by the width on a 16:10 screen.
- On 2560×1600 the scale is ×1.333, so the canvas is 1920×1200.
- Front-end GUI therefore takes **90 %** of the height share it has at 16:9.
- It does match on 16:9 and wider screens.

This is the price of "never cut" (D1), and the "+33 % on the owner's screen" figure was already calculated this way. **No change needed.**

**Evidence notes:**
- **Font asset:** byte-identical to Codex's pre-task snapshot: `m_ClearDynamicDataOnBuild: 1`, glyph and character tables empty. The N28 state is preserved.
- **Screenshots:** `Game-en.png` and `Game-ru.png` actually show the **Japanese** text, because that capture didn't switch the locale. Use `Announcer-*.png` for en and ru. The per-locale fit results in `edit-results.txt` aren't affected.
- **Clone match today:** the clone Editor log (started 13:57, after Codex's report at 13:50) contains one match on the new code.
  - The `PlayerUI` log lines are `:395` and `:431`, which are the post-UI3 line numbers.
  - Events seen: hand clicks (left and right), Confirm, Send, then the opponent leaving. The clone was the host and won by `OpponentDisconnected`, then `ResultScene` loaded (`Editor.log:3893-4528`).
  - No exception came from game code, and there was no "Announcer text truncated" warning.
  - The only game warnings are expected ones: N27's mane index, "No card selected for control package." (Confirm with nothing selected), and "Send ignored: no card target was confirmed." (the N20 rule).
  - Two Editors share this log, so a missing line proves nothing; only the lines above count.

---

## 2. New findings and updates

**No new defects.** The UI1–UI4 code was read line by line against its specs, and the asset values were checked in the YAML.

**Updates to existing items:**
- **N37–N46:** closed by UI1–UI4 (§5). Their visual confirmation is in §4 D.
- **U14** (the board on a 4:3 screen) is still open. UI5 makes 4:3 easy to reach (a 1024×768 window), so its manual checks include U14.
- **N33** gains one item: GameScene keeps a stale PrefabInstance override `m_timeText` on `PlayerUI`, a field that no longer exists. It's harmless; the Inspector's "remove unused overrides" deletes it. New location: `CardPreviewController.TogglePreview` is now at `:44`.
- **N34** new locations: `PlayerUI.cs:395`, `:412`, `:431`, `:448` (UI3 moved them). The two ungated "Tried ShowPrew" logs are at `CardPreviewController.cs:58` and `:63`. Today's clone log shows the cost: one match end printed **84** "DespawnBlock" lines.
- **N35** new location: `UI/PlayerUI.cs:173-180`.
- **Font asset (N28 context):** the clone's Play Mode logged `Importer(NativeFormatImporter) generated inconsistent result for asset … MPLUSRounded1c-Medium SDF.asset` (`Editor.log:1419`).
  - TMP adds glyphs to the dynamic font during Play Mode, and the main project and the clone share the file.
  - It's an Editor-only warning and harmless. The file on disk is still in the cleared state.
  - It's one more reason to follow the AGENTS.md rule: restore the font before committing.
- **Windowed play is safe:** `NetworkManagerPrefab` has `RunInBackground: 1`, so NGO sets `Application.runInBackground = true`. An unfocused window keeps the match running, even though Player Settings has `runInBackground: 0`. This was checked for UI5.
- **Feature gap "no other settings":** display settings are now specified in TASK-UI5. Audio, graphics quality and controls are still missing.

---

## 3. All open issues

### 🔴 Critical / 🟠 High / 🟡 Medium

None open.

### 🟢 Low (5)

| ID | Issue | Current location | Verdict |
|---|---|---|---|
| **N32** | `ProcessActivity` reads the card owner's character without a null check, and never uses it. It can't throw today (`CheckActivity` checks the same owner first). If it ever did, `RunGuarded` would abort the match. | `GameMaster.Processing.cs:139` | Confirmed (latent) |
| **N33** | Dead code and unused prefabs left after L10:<br>• `GameMaster.m_gameLoopCoroutine`, `m_phasesTimerCoroutine`, `m_cardViewWorldPrefab`<br>• `PlayerController.m_isDead` (a NetworkVariable that is never written), `GetCurrentSpeed()`<br>• `PlayerSession.ClearHand()`, `ClearBlocks()`, `RemoveCard()`<br>• `NetworkCard.SetFaceUpServerRpc`, `SetRotatedServerRpc`, `DespawnCardServerRpc`<br>• `CardPreviewController.TogglePreview`, `MultiplayerManager.KnowLobbies`, `SceneFlowManager.LoadLoginScene`, `GameSettingsPrefs.HasLanguageCode`<br>• `BootsTrap.prefab` and `MultiplayerManager.prefab` (the second has `m_debugMode: 1`)<br>• **new:** the stale `m_timeText` override on the GameScene HUD instance<br>**Owner's call:** `AuthManager.SingOut()` and `SingOutAndClearCredentials()` have no caller, and the second one's branch looks inverted. | `GameMaster.cs:56`, `:104-105`; `PlayerController.cs:21`, `:118`; `PlayerSession.cs:374`, `:583`, `:748`; `NetworkCard.cs:73-86`; `CardPreviewController.cs:44`; `MultiplayerManager.cs:65`; `SceneFlowManager.cs:45`; `GameSettingsPrefs.cs:20`; `AuthManager.cs:116-133`; `Assets/Prefab/BootsTrap.prefab`, `Assets/Prefab/MultiplayerManager.prefab`; `GameScene.unity` (PrefabInstance `282301674`, target `6674156206700678496`) | Confirmed. The `SingOutAndClearCredentials` branch order is Plausible. |
| **N34** | About 30 log lines aren't behind any debug flag, so they print in release builds. Examples:<br>• "Process called", "Rpc called", "RepairCalled", "DespawnBlock"<br>• "Card clicked" / "Card deselected"<br>• the "Applied … material index" lines<br>• "DrawPhase started/ended"<br>• 9 lines per lobby search<br>• "SetLobbyCode" / "Lobby code"<br>• "Tried ShowPrew" ×2<br>Also:<br>• `PlayerUI.OnCardClicked` / `OnCardAlterClicked` read `cardView.name` before their null check (latent).<br>• One debug message is missing `$`. | `GameMaster.Processing.cs:115`, `:138`; `PlayerSession.cs:229`; `NetworkCard.cs:164`; `BlockController.cs:38`; `CardViewUI.cs:97`, `:103`; `PlayerUI.cs:395`, `:412`, `:431`, `:448`; `PonyColorScript.cs:63`, `:83`; `GameMaster.GameLoop.cs:485`, `:520`; `MultiplayerManager.cs:765-834`, `:1541-1542`; `CardPreviewController.cs:58`, `:63`; `GameMaster.PackageManaging.cs:258` | Confirmed (also seen in today's clone log) |
| **N35** | `PlayerUI` formats HP and lives every frame: two Smart-format strings, even when nothing changed | `UI/PlayerUI.cs:173-180` | Confirmed |
| N27 | The pony mane color is never applied (`maneObject` isn't assigned), and every spawn logs "Invalid mane material index." **Parked by the owner** until the mane model exists. | `Assets/Prefab/BoardPrefab/PonyBase.prefab:368`; `InGame/PonyColorScript.cs:70-88` | Confirmed (seen again today) |

### ⚪ Info

- **N36:** the countdown and the server loop use different clocks. After a server hitch, or during drawing, clients can briefly see "00". Plausible and cosmetic. (`GameMaster.cs:124-141`; the phase loops in `GameMaster.GameLoop.cs`)
- **N29:** the lobby shows the player ID twice. **Parked by the owner.** (`UI/LobbyPlayerItemUI.cs:53-65`)
- **Naming and typos** (report only, don't rename):
  - `CardControllPackage`, `PlayerCardfWorldArea.cs`, `SetOponent` / `m_currentOponentNetworkObjectId`, `CheckActiveTaergetSelection`
  - `SingInAnonymous` / `SingOut` / `SingOutAndClearCredentials` / `m_isSingingIn`, `HearthbeatLobby`, `MovePlayeToSpawnPoint`, `ClearupAfterDisconnect`
  - `CardSpecialization.DeffenceSpell`, `KnowLobbies` / `m_knowLobbies`, `m_bootsTrap`, `_teamAssing`, `m_phaseAnouncementText`, `#region Valiable`
  - the GameObject name `PhaseAnouncer`
  - the log texts `"Packege"`, `"Handle unseccess"`, `"Tried ShowPrew"`; the local variable `_locakPoint`
  - `ConfirmSendButtonUI1` (in `ConfirmSendButton.cs`); `m_startHandCards`, which is really the per-round draw count

### Feature gaps (not defects, listed so they aren't mistaken for bugs)

- **Display settings** (resolution, windowed/fullscreen): specified in **TASK-UI5**, not implemented yet. Until then the size comes from Unity's saved screen settings or the command line. Audio, graphics quality and controls have no settings.
- **Join by lobby code is parked** (M11, owner 2026-10-19). `m_lobbyCode` / `m_lobbyCodePanel` stay serialized for it.
- Steam login is a stub (`AuthManager.SignInSteam`), although `LoginScene` has a button wired to it.
- `GamePhase.DrawCards` and `RoundEnd` are never set, and `ResolveCards` is only transient. During draws the announcer shows "Round break".
- The `DiscardCard` / combo actions, the `All*` / `Random*` targets and `ActivityEffect` have no effect.
- Destroying all enemy blocks doesn't win by itself. A player with no blocks is eliminated on their next death.
- `SessionRole.Spectator` exists, but nothing assigns it.
- No player-facing messages for lobby or network failures, an ignored Send, an ignored late confirm, or a deck refill.
- The confirmed target of an action card doesn't survive a respawn pause (by design). Card selections do survive it.
- `SessionManager` has 4 slots, but the scene supports only teams 0 and 1.
- With 10 cards, the hand overlaps by about 22 units. The rightmost card is drawn on top and receives clicks in the overlap (UI3, intended).

### ❓ Unknowns: open

| ID | Question | Status |
|---|---|---|
| U2 | Nested `SpawnPoint` NetworkObject inside `BlockBody` | ❓ No NGO warning in any log so far, today's included |
| U9 | NGO `NetworkSceneManager.Dispose` NRE on Play Mode stop, Editor quit or domain reload | ❓ Not seen in today's log |
| **U14** | On a 4:3 screen the orthographic camera shows about 17 % less board width than on 16:10. Are the board edges or player areas cut? (from the research) | ❓ Check at 1024×768 (§4 D, and UI5's manual checks) |

---

## 4. Pending manual checks

Both instances must run the **same** build. Since report #8, L1, N3, N26 and N21 changed the network contract, and UI1–UI4 changed the HUD.

**A. Left from earlier checklists**
- **D.5:** start the player build, pick 日本語, quit, start again. The language must still be Japanese. `Player.log` is unchanged since 2026-10-20, so this hasn't been run yet.

**B. Tasks 1–10 (committed in `efb88c1`, `391e079`)**
- **N2:** start a match as host and as client. The "NetworkVariable is written to, but doesn't know its NetworkBehaviour yet" warning doesn't appear.
- **L4:** in Play Mode, select a block in the Hierarchy. No `ArgumentException … Team Id` when the Editor gains focus.
- **N22:** the client sees 100/100 HP on its character from the first frame.
- **N6:** with two cards to play, kill the opponent with the first one. The second card doesn't resolve, and the result screen appears.
- **N26:** select cards but don't confirm. The next round starts with no selection. A respawn pause inside one CardSelection keeps the selection.
- **N21:** confirm exactly as CardSelection ends. Nothing happens: no turn ends, and no `[Suspicious]` line.
- **N3** (regression only): movement, card selection and targeting still work in their own phases, on the host and on the client.
- **L8:** the lobby list's Refresh label is localized in en/ru/ja.

**C. Tasks 11–19 (working tree)**
- **L1:**
  - The countdown matches on both instances within ±1 s in every phase.
  - After a respawn, the interrupted phase resumes with its remaining time.
  - The over-budget timer reset still needs the tamper test (C4/T13).
- **L2/N31:** over a few matches, all three deck mixes appear. With debug on, the refill log shows 84 cards.
- **N30:** 5 create/join/start/leave cycles with the Editor as host, and 5 with the player build as host.
  - No `TryCatchRequest` NRE.
  - The player count and the Start button follow joins and leaves within about 2 s.
- **N17:** when the client forfeits, the host's HUD stays until `ResultScene`. Today's clone match ended this way; the log shows the forfeit and `ResultScene`, but not the HUD.
- **L6:**
  - In the Profiler, the announcer allocates nothing in a second where nothing changes.
  - After the opponent leaves, `FindObjectsByType` doesn't run every frame.
- **L7:** during a match, switch en → ru → ja. The hand and board card texts follow. No `MissingReferenceException`.
- **L9:** in a release build, no gated debug lines. In the Editor, the flags still work.
- **L10:**
  - Movement with W/A/S/D works.
  - HealingHooves on yourself: 100 → 115 HP at full health; 50 → 80.
  - Leave, forfeit and Result work.
  - Package Manager shows no VisualScripting.
- **N28:** in a player build, Japanese and Russian render in the HUD, on the cards and on Result. Restore the font asset before committing.

**D. UI1–UI4 (working tree), new.** Details are in each spec's §9 and in Codex's report §6.
- **UI1:**
  - Play at 2560×1600 in en, ru and ja: your turn, the opponent's turn, your respawn, the opponent's respawn.
  - Line 2 is complete, with ": ", and there's no "…".
  - Round letters look round.
  - No "Announcer text truncated" warning in the Console.
- **UI2:**
  - Run the player windowed at 1366×768: `-screen-fullscreen 0 -screen-width 1366 -screen-height 768`. Go Login → Main menu → Settings → Lobby → match → Result. Nothing is cut.
  - Then fullscreen at the native 2560×1600: the menus are 33 % larger than before (expected), and the Login background fills the screen.
  - **Note:** Unity remembers the window size of the last run. After the 1366×768 test, start once with `-screen-fullscreen 1`, or use UI5's setting once it exists.
- **UI3:**
  - At 2560×1600 the HUD is in the same place as before. Only the banner corners and the Confirm-turn label look different.
  - At 2560×1440, 3440×1440 and 1024×768 the HUD follows the corners.
  - Collect 10 cards: all are visible and clickable, none covers a button, and the hand doesn't jump when it refreshes.
  - Leave → Cancel, then Leave → Confirm.
  - The client's HUD matches the host's.
- **UI4:**
  - Click world cards near each edge and corner: the preview stays fully on screen.
  - In ru, the Confirm label fits and is readable.
- **U14:** at 1024×768 (4:3), the whole board and both player areas are visible.

**Checklist:** [2026-10-21_ManualChecks-Report8-9.xlsx](../ManualChecks/2026-10-21_ManualChecks-Report8-9.xlsx) (58 rows in 28 groups; its own A–D sections). It covers groups A–D above.

---

## 5. Closed issues

| ID | How it was closed | Commit / report |
|---|---|---|
| C1 | Debug HP delta only under `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD` and `m_debugMode` | `61323d5` (#2) |
| C2 | The movement target comes from `ServerRpcParams` | `cebd2fd` (#2) |
| C3 | Server-side `CanPlayerMove` check | `fb0edb9` (#2) |
| C4 | Selection confirm validates card IDs | `44d858f` (#2) |
| C5 | Editor-only `using`s removed | `15b439b` (#2, #5) |
| C6 | Not reproducible: the cast button works | #5 |
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
| M3 | Lava guard keyed by instance | `a0df515` (#7) |
| M4 | `GameMaster` input gates use `IsServer` | `a0df515` (#7) |
| M5 | Fixed by C3 | #2 |
| M6 | `UpdatePlayerHP` null checks | `a0df515` (#7) |
| M7 | Fixed by H4 | #4 |
| M8 | `AnyPlayer` through a block resolves the owner by team on clients | `a0df515` (#7) |
| M9 | `DrawCard(null)` guard (part of H2) | `e7d1b03` (#6) |
| M10 | Async lobby and network errors caught, with state rollback | `a0df515` (#7) |
| M11 | Parked: join-by-code commented out (owner); moved to the feature gaps | `b7e7b91` (#7) |
| M12 | Language selection in `SettingsScene` | `b7e7b91` (#7) |
| M13 | Neutral lobby data; localized counts and fallbacks | `a0df515`, `b7e7b91` (#7) |
| M14 | Client card-data null guards; hand rebuild when the data arrives | `a0df515` (#7) |
| M15 | Hand limit 10, oldest uncommitted cards discarded | `b7e7b91` (#7) |
| M16 | `SessionManager.Instance` cleared in the `OnDestroy` override | `b7e7b91` (#7) |
| N1 | `PlayerUI.OnDestroy` overrides NGO's; CS0114 gone | `b7e7b91` (#7) |
| N2 | Session values written in the server's `OnNetworkSpawn` | `391e079` (#8) |
| N3 | Replicated input lock for client-side gates | `391e079` (#8) |
| N4 | Dead `ValidateCardOwner` and `m_cardResolveDelay` removed | `efb88c1` (#8) |
| N5 | Log string interpolated | `efb88c1` (#8) |
| N6 | Finish checked after each resolved card | `391e079` (#8) |
| N7 | `GetCharacter` uses `TryGetComponent`, doesn't write the ID | `391e079` (#8) |
| N8 | Architecture docs and README refreshed | working tree (#8) |
| N9 | Superseded by MANA2 | #5 |
| N10, N11, N13 | Closed when H5 was finished | #4 |
| N12 | Withdrawn: false positive | review 2026-10-20 (#8) |
| N14 | Host returns to the lobby when a match can't start | `fe2d4fb` (#6) |
| N15 | Number not used | #4 |
| N16 | The leaver's suspicious summary is logged on disconnect | `efb88c1` (#8) |
| N17 | Host HUD kept when the opponent forfeits | working tree (#8) |
| N18 | Deaths are handled in every phase | `fe2d4fb` (#6) |
| N19 | Leave button with confirmation, counts as a defeat | `fe2d4fb` (#6) |
| N20 | Safe Send; the target rule is shared with the server | `a0df515` (#7) |
| N21 | Confirm carries the intended phase; mismatches are ignored | `391e079` (#8) |
| N22 | Character HP set in the server's `OnNetworkSpawn` | `391e079` (#8) |
| N23 | `Keyboard.current` null check | `efb88c1`, working tree (#8) |
| N24 | Three scripts converted to UTF-8 | `efb88c1` (#8) |
| N25 | Intended (owner, 2026-10-19) | #5 |
| N26 | Unconfirmed selection cleared at each new selection phase | `391e079` (#8) |
| N28 | Font dynamic data cleared on build; commit rule in `AGENTS.md` | working tree (#8) |
| N30 | Lobby refresh throttled | working tree (#8) |
| N31 | Deck size documented as 84 (with L2) | working tree (#8) |
| **N37** | Announcer shows both lines in full: unscaled 1560×150 box, font 24–48 (measured 48 in all 51 cases) | working tree, UI1 (**#9**) |
| **N38** | Line-2 prefixes have a ": " separator; shorter en/ru respawn prefix | working tree, UI1 (**#9**) |
| **N39** | All six scenes Scale With Screen Size / Expand; no menu depends on pixel size, nothing cut at 9 resolutions | working tree, UI2 (**#9**) |
| **N40** | HUD anchored to the screen edges and corners; identical at 2560×1600 | working tree, UI3 (**#9**) |
| **N41** | No non-uniform UI scale left; the per-frame `LeaveMatchUI` resize removed | working tree, UI1 + UI3 (**#9**) |
| **N42** | Login background covers any aspect (Envelope Parent) | working tree, UI2 (**#9**) |
| **N43** | Card preview clamped on screen; always on the HUD canvas | working tree, UI4 (**#9**) |
| **N44** | ru "Подтвердить" fits the Confirm button (font min 14; measured 15.65). Small but readable; a wider button is the next step if needed. | working tree, UI4 (**#9**) |
| **N45** | Hand compresses to 1800 units; 10 cards no longer reach the buttons | working tree, UI3 (**#9**) |
| **N46** | Old hand views deactivated before `Destroy`: no one-frame jump (harness-verified) | working tree, UI3 (**#9**) |
| L1 | Countdown from a replicated deadline | working tree (#8) |
| L2 | All three deck mixes reachable | working tree (#8) |
| L3 | `IsGameFinished` is pure and allocation-free | `391e079` (#8) |
| L4 | Duplicate `BlockController.TeamId` removed | `391e079` (#8) |
| L5 | `GameAnnouncerUI` maps every existing phase | #4 |
| L6 | Announcer, world views, HUD clear and lobby panels change-only (HP/lives → N35) | working tree (#8) |
| L7 | Card texts refresh on a locale change | working tree (#8) |
| L8 | `btn_lobbyRefresh` key without the tab | `efb88c1` (#8) |
| L9 | Debug flags forced off in release builds | working tree (#8) |
| L10 | Dead code, legacy scenes, backups and VisualScripting removed (leftovers → N33) | working tree (#8) |
| U1 | The player build compiles and runs | review 2026-10-20, #8 |
| U3, U4 | Answered (null `NetworkBehaviourReference` is safe; `DontDestroyWithOwner: 0`) | #3 |
| U5 | GUID `e651dbb3…` is NGO's own `NetworkPrefabsList` script | review 2026-10-20 |
| U6 | Explained: each Editor logs its own `GameScene` load in the shared log | review 2026-10-20 |
| U7 | Normal end reaches `ResultScene` on both instances | #4 |
| U8 | `Shutdown()` leaves no objects behind | review 2026-10-20 |
| U10 | The forfeit works at runtime | review 2026-10-20 (seen again today) |
| U11 | Not reproduced | #7 |
| U12 | Mana replicates to a remote client, and the rejection reaches it | review 2026-10-20 |
| U13 | A second lobby and match work after result → main menu | #6 |

Tasks completed without an issue ID: MANA1–MANA3 (`ade4aed`, `5eec046`, `058fa63`), SEC1 (`f08e854`) and HYG1 (`efb88c1`).

---

## 6. Suggested next steps (in order)

1. **Run the manual checks in §4,** at least groups C and D, with two instances on the same build.
2. **Commit the working tree** once groups C and D pass.
   - Restore the font asset to its cleared state first (`AGENTS.md`).
   - The staged L10 deletions and the unstaged edits belong together.
   - Include the UI1–UI4 specs, so §7's cleanup can follow.
3. **Hand TASK-UI5 to Codex:** resolution and display mode in Settings ([spec](../Tasks/TASK-UI5_ResolutionSetting.md)). It applies five recommended decisions (S1–S5, spec §10); change any of them before handing it over if you prefer otherwise. It can start on the current working tree; it doesn't need step 2.
4. **One small hygiene spec (all Low), on the owner's go-ahead:**
   - N32: delete the unused line
   - N33: delete the dead code, the two old prefabs and the stale override. Owner's call on the sign-out methods.
   - N34: gate the logs, fix the null-check order and the `$`
   - N35: change-only HP and lives
5. **Parked by the owner:** N27 (until the mane model exists), N29 ("later").
6. **Optional:** N36, only if "00" is noticed in play. U14 becomes a task only if the 4:3 check shows a cut.

---

## 7. Task folder

**Not removed: UI1–UI4.** They are implemented, match their specs and have no open blocking defect (§1). But unlike earlier rounds, these spec files are **untracked**: they aren't in any commit yet. Deleting them now would lose them for good. **Remove them after the commit in §6.2**; then they can be read with `git show <commit>:Docs/AI/Tasks/<file>`.

| File | State |
|---|---|
| `TASK-UI1_AnnouncerFullText.md` | Done (working tree). Remove after the commit. |
| `TASK-UI2_CanvasScalingStandard.md` | Done (working tree); AC4 corrected in §1 of this report. Remove after the commit. |
| `TASK-UI3_HudAnchoringAndHand.md` | Done (working tree). Remove after the commit. |
| `TASK-UI4_PreviewClampAndConfirmLabel.md` | Done (working tree). Remove after the commit. |
| **`TASK-UI5_ResolutionSetting.md`** | **New:** ready for Codex |

The research document, Codex's implementation reports and the manual checklists stay as reference material.

## Sources

- **Diffs:**
  - `git diff HEAD`: the working tree, staged and unstaged
  - `git diff --no-index` of each UI1–UI4 file against `build/ui1-ui4-verification/baseline/`
- **Code read:**
  - `GameAnnouncerUI.cs`, `PlayerUI.cs`, `LeaveMatchUI.cs`, `CardPreviewController.cs`, `CardViewUI.cs`, `CardViewBase.cs`
  - for UI5: `SettingsUI.cs`, `SettingsSceneButtonsUI.cs`, `GameSettingsPrefs.cs`, `Bootstrap.cs`
- **Assets:**
  - `PlayerUIPrefab.prefab`: the full RectTransform tree and every scale
  - `GameScene.unity`: PrefabInstance `282301674` and the `GameAnnouncerUI` block
  - the six scenes' CanvasScalers
  - `LoginScene` `BackGround`
  - `UI_table_en/ru`
  - `MPLUSRounded1c-Medium SDF.asset`
  - `NetworkManagerPrefab.prefab` (`RunInBackground`)
  - `ProjectSettings.asset` (display settings)
- **Logs:**
  - `%LOCALAPPDATA%\Unity\Editor\Editor.log` (2026-10-21, 13:57–14:45, header `-projectPath C:/Lesson/FallenStar-Spellcasters_clone_1`): one match to `ResultScene` on the new code
  - `Player.log` / `Player-prev.log`: unchanged since 2026-10-20
  - Registry `HKCU\Software\DefaultCompany\FallenStar-Spellcasters`: Unity's saved screen settings (2560×1600, native, fullscreen mode 1)
- **Codex output:** `build/ui1-ui4-verification/`
  - `edit-results.txt`, `play-results.txt`, `asset-results.txt` (166 PASS)
  - `build-results.txt`: release player succeeded, 0 errors, 2 warnings
  - screenshots `Announcer-*.png`, `Game-*.png`, `Hand-10-PlayMode.png`, and one per scene and language
  - `final-editor-cleanup.log`

  The harnesses run real Unity UI in isolation. They aren't two-player Play Mode results.
