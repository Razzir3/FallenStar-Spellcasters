# Issue Report #13: FallenStar – Spellcasters (review of the three tasks, a full revision, your questions, a QA checklist, folder cleanup)

- **Date:** 2026-10-23
- **Baseline:** `HEAD` = `6d89a09` ("N50_N52") **plus the uncommitted working tree:**
  - Codex's implementation of all three specs from report #12 (28 code and asset files, 3 architecture docs)
  - Codex's report `2026-10-22_N32-N35_N54-N58_Implementation.md`
  - report #12 and the three specs (all untracked)
- **Previous reports:** [#1](2026-10-18_IssueReport.md), [#2](2026-10-18_IssueReport-2.md), [#3](2026-10-18_IssueReport-3.md), [#4](2026-10-18_IssueReport-4.md), [#5](2026-10-19_IssueReport-5.md), [#6](2026-10-19_IssueReport-6.md), [#7](2026-10-20_IssueReport-7.md), [#8](2026-10-20_IssueReport-8.md), [#9](2026-10-21_IssueReport-9.md), [#10](2026-10-21_IssueReport-10.md), [#11](2026-10-22_IssueReport-11.md), [#12](2026-10-22_IssueReport-12.md)
- **Your request:** "Analyze all project. Ask me about pending problems. If required, make me another manual check for QA research. Also clear the folders of old manual check reports, tasks and already implemented features. Questions you can ask here or in a new type of Excel poll, as you want."
- **Method:**
  1. **Codex's work:** read its report, then the full diff (33 files) line by line against each spec's §4 and §7.
  2. **Codex's evidence:** `build/task-folder-verification/` (Play Mode results, native clicks, build result, release smoke, clean-up), `build/n55-verification/`, `build/hyg2-verification/`.
  3. **A project sweep** (§3):
     - every remaining `Debug.Log` and its gate
     - scene and prefab references to every deleted member and prefab GUID
     - all localization keys in en / ru / ja, and every reference to them
     - the network prefab list, Build Settings, `TODO` markers
     - every disconnect and quit path
  4. **Your filled 2026-10-22 checklist again,** every sheet and cell. That found the note I missed (§2).
- **I didn't run Unity, a build or Play Mode.** I changed no code, scenes, prefabs or settings. My changes:
  - this report
  - [the QA checklist](../ManualChecks/2026-10-23_ManualChecks-Report13.xlsx) and [the poll](../Polls/2026-10-23_Poll-Report13.xlsx) (a new folder)
  - the headers and two stale lines of the three `Architecture/*.md` files
  - the folder cleanup (§8)

**This report is the full issue list.** §4 lists every open issue and §6 every closed one. New IDs: **N59, N60**. The next new finding would be **N61**.

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
| **Codex's three tasks** | **The review passes, no defects.** All nine issues are done as specified: N54, N57, N56, N55, N58, N32, N33, N34, N35 (§1).<br>• **Codex's baseline harness reproduced N54 exactly as report #12 predicted:** the right-hand (team 1) card texts caught the UI ray at 4 of 4 points, the left-hand ones at 0. After the fix, 0 on both sides.<br>• **Its release build succeeded** with 0 errors.<br>• **No harness can play a real client,** so your checklist is the real test. They close when it passes and you commit. |
| **Missed in report #12** | **Your own finding,** written under "Finded" at the bottom of the *Known issues* sheet: *"When host close his application, client don't get a result scene."* I read only the Checklist notes. Sorry. |
| **New** | • **N59** 🟡, Confirmed: when the host quits mid-match, the client is dropped into the Lobby with no result and no message (§2). The client can't tell "the host quit" from "my connection dropped", so the fix needs your choice (poll **P1**).<br>• **N60** ⚪: cosmetic leftovers: an orphan comment, a doubled debug check and 10 commented-out code blocks. |
| **The rest of the project** | **Nothing else new.** No ungated release log is left. No scene or prefab references a deleted member. All 81 localization keys exist in en, ru and ja. Every network prefab is registered (§3). |
| 🔴 / 🟠 / 🟡 open | **0 / 0 / 1:** N59 |
| Done, waiting for your checklist | **9:** N54, N57 (🟡), N55, N56, N58, N32, N33, N34, N35 (🟢) |
| Other open | 🟢 N27 (parked). ⚪ N60, N36, N29 (parked), the naming list. ❓ U2, U9 |
| **QA checklist** | [2026-10-23_ManualChecks-Report13.xlsx](../ManualChecks/2026-10-23_ManualChecks-Report13.xlsx): **40 rows** in sections A–I. It covers the nine issues, research rows that confirm N59 before it's fixed, and a release-build log check (§5). |
| **Questions** | **[The poll](../Polls/2026-10-23_Poll-Report13.xlsx): 13 questions** with my recommendations, plus a sheet to rank the feature gaps (§7). |
| **Cleanup** | **24 committed files removed:** 4 old checklists, 1 old review, 18 Codex implementation reports for accepted work, and the finished mana research. All are in git history (§8). The untracked files stay until you commit. |

**Overall:** the code is in its best state so far. After your checklist and one commit, the only defect left is N59. It's a design choice more than a coding problem.

---

## 1. Review of Codex's three tasks

### 1.1 Issue by issue

| ID | The spec asked for | The diff does | Verdict |
|---|---|---|---|
| **N54** | Prefab only: remove the card canvas's `GraphicRaycaster`, turn off Raycast Target on its three texts | Exactly that, in `CardWorldViewPrefab.prefab` (component and its list entry removed; `m_RaycastTarget: 0` ×3). Canvas, scaler, texts, collider and transforms unchanged. | ✅ |
| **N57** | `ConfirmTurn` must not bind `m_playerSession` | It uses a local `_session` (`PlayerUI.cs:740-755`), so `Update` still binds and subscribes later | ✅ |
| **N56** | Null-safe lookup in both buttons, one warning per press | `ConfirmTargetButtonUI.cs:7-17`, `ConfirmSendButton.cs:6-16` | ✅ |
| **N55** | `SpawnPlayers` returns `bool` and checks spawn points first; `CreateBlock` returns; `try`/`catch` and `AbortGameStart` | `GameMaster.Spawn.cs:210-247` checks every player's team point before spawning anything. `:259-263` returns. `MultiplayerManager.cs:1462-1476` aborts with `spawn_failed` or `match_start_exception`. A `yield break` inside `try`/`catch` is legal C#, and the build compiled. | ✅ |
| **N58** | `MaxTargets = 4`; the reader turns a bad count into an empty list; the handler rejects null, empty or >4 | `CardControllPackage.cs:7`, `:32-36`; `GameMaster.PackageManaging.cs:121-127`, after the active-player and sender checks. `CreateEmpty` now fills `MaxTargets` slots instead of the literal `{0,0,0,0}`. Same result, tied to the constant. | ✅ |
| **N32** | Delete the unused owner lookup | Removed from `ProcessActivity` (`GameMaster.Processing.cs:132-135`) | ✅ |
| **N33** | The listed dead members, 2 prefabs, the stale scene override; AuthManager untouched (W3) | All gone. The four `GameScene.unity` lines removed, nothing else in the scene. Both prefab GUIDs have no references left (I re-checked). `m_knowLobbies`, `SessionRole.Spectator` and AuthManager are kept. | ✅ (see N60) |
| **N34** | Gate or delete the numbered log lines; fix three `$` | **25 gated, 5 deleted.** Codex noticed that my prose said 31 while the table numbered 30. **The table was right; "31" was my miscount.** My sweep finds no other ungated line (§3). | ✅ |
| **N35** | HP and lives written only on change; caches reset on locale, enable and clean-up | `PlayerUI.cs:177-205` with `m_healthTextDirty` / `m_isDeadTextShown`. The reset lives in `ResetManaTextCache` (`:245-250`), as the spec suggested. The dead branch still clears the selection every frame. | ✅ |

**Docs:** Codex updated `Architecture.md` (§3.4, A6 removed, RP3, RP15, RP17, the new RP18), `Dependencies.md` and `GameLoop.md`. It kept my "Target a played card" row correction from report #12.

### 1.2 Codex's evidence (what it ran; I didn't re-run it)

| Check | Result |
|---|---|
| N54 baseline, old prefab | Team 1: UI hits at all 4 points (centre and three texts). Team 0: 0. **This is report #12's §2 diagnosis, now measured.** |
| N54 fixed prefab | 0 UI hits on both sides, all physics hits kept. The HUD hand and Confirm button still block the board. |
| Real mouse in the Editor | `PlayerSession.Update` opened a right-hand card from its text and a left-hand card, `overUI=False`. Hand and Confirm gave `overUI=True`. |
| N56 / N57 / N35 / N58 / N55 harnesses | All passed (`play-results.txt`), `captured-errors.txt` empty |
| Release build | Succeeded, 0 errors, 2 warnings (Unity Services not linked; no cloud token) |
| Release smoke | Startup to LoginScene only. Lobby search and a full match weren't run. |
| Clean-up | Temporary Editor scripts and `Assets/Editor/` removed. The font and `DefaultNetworkPrefabs.asset` match `HEAD`. |

### 1.3 What only your checklist can prove

- A real client: both table sides in both windows, the HUD cast flow, an early Confirm turn.
- The lobby search and the release `Player.log` over a whole match.
- The disconnect paths (no code changed there, but N59 is new).

### 1.4 Notes (not defects)

- **Both players must run the same code.** N33 removed a NetworkVariable (`PlayerController.m_isDead`) and three RPCs. The ParrelSync clone shares `Assets`, so two Editors always match. But a player build made before today can't play against today's Editor: make a new build for section G.
- **Orphan YAML is harmless.** These values stay in the files until Unity saves them again:
  - `m_loginSceneName` in `LoginScene.unity:1502`
  - `PonyBase`'s old `m_isDead` data
  - `m_cardViewWorldPrefab` on the GameMaster prefab
- **Q1 is closed as moot.** Codex's baseline measured the team 1 block through the shared Main Camera, which both windows use. Your A5.1 wording doesn't matter any more. Section A of the new checklist tests both sides in both windows.
- **The font: correcting my report #12.** It told you to run `git restore` on the font before the commit. That command only goes back to `HEAD`, and `HEAD` (`6d89a09`) already contains the Editor-changed font. Today the font matches `HEAD`, so there's nothing to restore. Whether to roll it back to the clean `72a724c` version is poll **P3**.

---

## 2. The finding I missed: N59

**Your note** (2026-10-22 checklist, *Known issues* sheet, under "Finded"):
> When host close his application, client don't get a result scene. But when client close app - host get result scene as winner.

**Why the two directions differ:**

| Who quits | What happens | Code |
|---|---|---|
| **The client** | The host's server sees the disconnect. `SessionManager` tells `GameMaster`, which forfeits (H6): `EndMatch(host, OpponentDisconnected)`. `MatchEndedClientRpc` sets the result, then `LeaveMatchToResultScene` shows **"Victory – opponent left"**. | `SessionManager.cs:167-172`; `GameMaster.Player.cs:89`; `GameMaster.cs:439`, `:474-484` |
| **The host** | The server is gone, so nobody sends a result. On the client, NGO raises `OnClientDisconnectCallback`. Then `OnClientDisconnected` → `HandleLostConnectionToHost` → `CleanupAfterDisconnect` → **`LoadLobbyScene()`**. `MatchResultData` is never set. The client lands in the Lobby screen with no result and no message. | `MultiplayerManager.cs:218-257`, `:260-307`, `:1514-1543` |

- **Also triggered by:**
  - the host stopping Play Mode, or crashing
  - the host's network dropping
  - **the client's own network dropping:** the client sees exactly the same callback
- **The host can't help it on quit.** `OnApplicationQuit` (`:138`) is `async void` and only tries to delete the lobby. Unity doesn't wait for it, and nothing sends a result.
- **No log evidence:** the lines on this path are debug-gated, and `AppRoot`'s `MultiplayerManager` has debug off. So the verdict rests on your observation plus the code path.

| ID | Level | Issue | Location | Verdict |
|---|---|---|---|---|
| **N59** | 🟡 Medium | **When the host quits mid-match, the client is sent to the Lobby with no result screen and no message.** The opposite direction works.<br>**Design question:** the client can't tell "the host quit" from "my own connection dropped". "Victory" would be wrong in the second case, and both players would then see a win. → poll **P1**; the spec follows your answer. | `MultiplayerManager.cs:218-257`, `:260-307`, `:1514-1543` | Confirmed (your observation + code path) |

**Section F of the checklist** first confirms today's behavior, so the fix has a measured "before". It covers a Leave match control, the host stopping Play Mode, the reverse direction, and the build window closed with X.

---

## 3. Full revision: what else I checked

| Area | What I did | Result |
|---|---|---|
| Release logs (N34) | Every `Debug.Log` in `Assets/Scripts` against its gate; every class with `m_debugMode` against `DebugModeGuard.Allow` | **Clean.** The only ungated lines are `ResultSceneController.cs:23` (behind its early return) and `AuthManager.cs:148` (Editor only), both left alone on purpose. All 15 classes with a flag pass it through the guard. |
| Deleted members (N33) | Searched every `.unity`, `.prefab` and `.asset` for a UnityEvent that names a deleted method, and for the two deleted prefab GUIDs | **No reference.** Every UnityEvent binding (buttons and localization events) still names a live method. |
| Localization | Compared the shared keys with the en / ru / ja tables; resolved every scene, prefab and code reference, including name-only ones | **`UI_table` 73 keys and `Cards` 8 keys, complete in all three languages.** Every reference resolves. |
| Network prefabs | Every prefab with a `NetworkObject` against `DefaultNetworkPrefabs.asset` | All 8 registered, none missing |
| Build Settings | Scene list | 6 scenes, `LoginScene` first. Nothing loads it by name now, which is fine. |
| Running in the background | Player Settings has `runInBackground: 0` | **Not a finding:** `NetworkManagerPrefab` has `RunInBackground: 1`, which wins (report #9). An unfocused build keeps the match running. |
| N55 abort path | `AbortGameStart` → `GameMaster.AbortMatch` → `MultiplayerManager.Disconnect` | Both players end up in the Lobby. The host goes through `ShutdownHostInternal`, the client through its disconnect callback. |
| `TODO` markers | 14 left | Owner notes, or parked M11. `m_lobbyCode`, `m_lobbyCodePanel` and the log-only `SetLobbyCode()` belong to join-by-code (M11, parked) and stay with it. |
| Quit and disconnect paths | Host and client, quit and Leave match | **N59** (§2). Leave match (N19) and the client's quit work. |
| Cosmetics | The deletion sites | **N60** (below) |

| ID | Level | Issue | Location | Verdict |
|---|---|---|---|---|
| **N60** | ⚪ Info | **Cosmetic leftovers:**<br>• An orphan `//ToDo` above `m_debugMode` in `PlayerController`, left when N33 removed the commented field under it.<br>• `SetLobbyCode` checks `m_debugMode` twice in a row.<br>• **10 commented-out code blocks,** e.g. `InitUnityServices`, `GetCharacterByID`, and four in `PlayerCardfWorldArea`. M11's block stays with M11.<br>• The deletions left empty lines behind. Your files already use double empty lines elsewhere, so that part matches the style.<br>No behavior impact. → poll **P4** | `PlayerController.cs:37`; `MultiplayerManager.cs:1554-1560`<br>blocks: `MultiplayerManager.cs:582-598`, `GameMaster.Player.cs:120-139`, `PlayerCardfWorldArea.cs:47`, `:77`, `:124`, `:131`, `CardControllPackage.cs:11`, `:21`, `NetworkCard.cs:178`, `CardWorldViewManager.cs:764` | Confirmed |

---

## 4. All open issues

### 🔴 Critical / 🟠 High

None open.

### 🟡 Medium (1)

| ID | Issue | Current location | Verdict | Next |
|---|---|---|---|---|
| **N59** | Host quits mid-match → the client lands in the Lobby with no result and no message (§2) | `MultiplayerManager.cs:218-257`, `:260-307`, `:1514-1543` | Confirmed | Poll P1, then a spec |

### Done, waiting for your checklist (9)

Review passed (§1). Each closes when its checklist rows pass and you commit.

| ID | Level | Issue | Fixed in | Checklist rows |
|---|---|---|---|---|
| **N54** | 🟡 | The client's (right-hand) table cards couldn't be clicked open | `CardWorldViewPrefab.prefab` | A1–A3, B1.4 |
| **N57** | 🟡 | An early Confirm turn left the hand row empty all match | `PlayerUI.cs:740-755` | C1, G1.3 |
| **N56** | 🟢 | Confirm and Send could throw without a session | `ConfirmTargetButtonUI.cs`, `ConfirmSendButton.cs` | B2, H1 |
| **N55** | 🟢 | A missing spawn point stalled the match start | `GameMaster.Spawn.cs`, `MultiplayerManager.cs:1462-1476` | E1, I1 (optional) |
| **N58** | 🟢 | The package reader trusted a client-sent count | `CardControllPackage.cs`, `GameMaster.PackageManaging.cs:121-127` | B1, H1 |
| **N32** | 🟢 | An unused, unchecked owner lookup | `GameMaster.Processing.cs` | B1 |
| **N33** | 🟢 | Dead code, two unused prefabs, a stale scene override | 9 scripts, `GameScene.unity`, 2 prefabs deleted | E1, G1 |
| **N34** | 🟢 | Debug lines in release builds | 14 scripts | G1.4 |
| **N35** | 🟢 | HP and lives reformatted every frame | `PlayerUI.cs:177-205` | D1 |

### 🟢 Low (1)

| ID | Issue | Current location | Verdict | Next |
|---|---|---|---|---|
| N27 | The pony mane colour is never applied (`maneObject` unassigned); every spawn warns "Invalid mane material index." **Parked by you** until the mane model exists. | `PonyBase.prefab:368`; `PonyColorScript.cs:39-41`, `:85` | Confirmed | Poll P8 |

### ⚪ Info

- **N60** (new): cosmetic leftovers (§3). Poll P4.
- **N36:** the countdown and the server loop use different clocks, so clients can briefly see "00". Plausible and cosmetic. (`GameMaster.cs:124-141`; the phase loops in `GameMaster.GameLoop.cs`) Poll P10.
- **N29:** the lobby shows the player ID twice. **Parked by you.** (`UI/LobbyPlayerItemUI.cs:53-65`) Poll P9.
- **Naming and typos** (report only, don't rename):
  - `CardControllPackage`, `PlayerCardfWorldArea.cs`, `SetOponent` / `m_currentOponentNetworkObjectId`, `CheckActiveTaergetSelection`
  - `SingInAnonymous` / `SingOut` / `SingOutAndClearCredentials` / `m_isSingingIn`, `HearthbeatLobby`, `MovePlayeToSpawnPoint`, `ClearupAfterDisconnect`
  - `CardSpecialization.DeffenceSpell`, `m_knowLobbies` (the `KnowLobbies` property is gone, N33), `m_bootsTrap`, `_teamAssing`, `m_phaseAnouncementText`, `#region Valiable`
  - the GameObject name `PhaseAnouncer`
  - the log texts `"Packege"`, `"Handle unseccess"`, `"Dissconecting client"`, `"Joinde selected lobby"`; the local `_locakPoint`
  - `ConfirmSendButtonUI1` (in `ConfirmSendButton.cs`); `m_startHandCards`, which is really the per-round draw count
  - the Confirm binding in `PlayerUIPrefab.prefab:1585` names the type `ConfirmButtonUI`. It still resolves.

### Feature gaps and ideas (not defects, listed so they aren't mistaken for bugs)

The poll's *Priorities* sheet asks you to rank these.
- **Settings:** language and display exist. Audio, graphics quality, controls and a UI-scale setting don't.
- **Join by lobby code is parked** (M11, you, 2026-10-19). Its unused `m_lobbyCode` / `m_lobbyCodePanel` fields and the log-only `SetLobbyCode()` wait for it.
- **Steam login is a stub** (`AuthManager.SignInSteam`), although `LoginScene` has a button for it.
- **Phases:** `GamePhase.DrawCards` and `RoundEnd` are never set, and `ResolveCards` is only transient. During draws the announcer shows "Round break".
- **No effect yet:** the `DiscardCard` / combo actions, the `All*` / `Random*` targets and `ActivityEffect`.
- Destroying all enemy blocks doesn't win by itself. A player with no blocks is eliminated on their next death.
- **Spectators:** `SessionRole.Spectator` exists, but nothing assigns it. `SessionManager` has 4 slots, but the scene supports only teams 0 and 1.
- **No player-facing messages for:**
  - lobby or network failures
  - an ignored Send
  - an ignored Confirm turn during the opponent's action
  - a deck refill
- **Rejoining a running match** (research `RESEARCH-H6C_ReconnectMidMatch.md`; H6 chose the forfeit instead).
- The confirmed target of an action card doesn't survive a respawn pause (by design).
- With 10 cards, the hand overlaps (UI3, intended).
- The five menu scenes don't have the low profile yet (UI7, planned). Poll P12.
- **Idea (your A8.2):** a short show/hide animation for the big card.

### ❓ Unknowns: open

| ID | Question | Status |
|---|---|---|
| U2 | The nested `SpawnPoint` NetworkObject inside `BlockBody` | ❓ No NGO warning in any log so far. Checklist H1.3 watches for it. |
| U9 | NGO `NetworkSceneManager.Dispose` NRE on Play Mode stop, Editor quit or domain reload | ❓ Not seen in the 2026-10-22 logs. H1.3. |

---

## 5. The QA checklist

**File:** [Docs/AI/ManualChecks/2026-10-23_ManualChecks-Report13.xlsx](../ManualChecks/2026-10-23_ManualChecks-Report13.xlsx). Same layout as before: *Read me*, *Checklist*, *Summary*, *Known issues*. I dropped the Clean-up sheet, since you skip it. Its two steps are now in *Read me → Before you commit*.

| Section | What | Covers | Setup |
|---|---|---|---|
| **A** | Table-card clicks: **both sides in both windows** (centre, name text, picture) | N54 | Two Editors |
| **B** | Casting through the HUD row; HUD clicks don't reach the table; Confirm / Send edge cases | N54, N56, N58, N32 | Two Editors |
| **C** | Confirm turn pressed before the match starts; the hand row works all match | N57 | Two Editors |
| **D** | HP and lives: damage, death, respawn, language switch, elimination | N35 | Two Editors |
| **E** | Lobby search, a full match, a second match | N33, N34, N55 | Two Editors |
| **F** | **QA research:** quit and leave in both directions | N59 (before the fix) | Two Editors, one build row |
| **G** | A new release build: a match against an Editor; `Player.log` free of debug lines | N34, N33, N57 | Build + Editor |
| **H** | Both Editor logs: no exceptions, no unexpected guard warnings; U2 / U9 watch | all | — |
| **I** | Optional, **only after you commit:** a missing spawn point aborts cleanly | N55 | One scene edit, then `git restore` |

**Order:** A → E in one or two matches, F next (it ends matches), G with the build, H at the end. Run I only after the commit: `git restore` on `GameScene.unity` would also undo N33's scene change before that.

---

## 6. Closed issues

**Closed in this report:**

| ID | How it was closed | Commit / report |
|---|---|---|
| **Q1** | Moot: Codex's baseline measured the team 1 block through the shared Main Camera, so the answer no longer matters (§1.4). Section A tests both sides in both windows. | #13 |

**Waiting for your checklist** (§4): N54, N57, N55, N56, N58, N32, N33, N34, N35.

**Closed earlier:**

| ID | How it was closed | Commit / report |
|---|---|---|
| **N50** | The preview resolves the HUD canvas and dock when it opens, so it works on a client. Your A1, A4, A6 and C8.3 confirm it, including in a release player. | `6d89a09` (#12) |
| **N52** | Hover validation follows the HUD row (your A3.1, A3.2) | `6d89a09` (#12) |
| **N47** | The docked, readable big card, on hover (both players) and on click. Clicking the client's table cards is now **N54**. | `6d89a09`, UI6 (#12) |
| **N48** | HUD labels at least 30 units; readable at the low sizes (your B2.1–B2.5, B4.1) | `6d89a09`, UI6 (#12) |
| **N49** | Windowed fits the work area; the automatic pick and 1366×768 work in your build (C2.1–C2.3) | `6d89a09`, UI6 (#12) |
| **N51** | Accepted (W1 = a, your B6.1 "Fine") | #12 |
| **N53** | Accepted (W2 = a, your B4.1) | #12 |
| N34 (part) | The two "Tried ShowPrew" logs are removed (your C9.1) | `6d89a09` (#12) |
| **U15** | Answered: the docked big card never got in the way (A8.1) | #12 |
| **U16** | Answered: the hover view on hand cards is fine (A8.2) | #12 |
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
| M11 | Parked: join-by-code commented out (you); moved to the feature gaps | `b7e7b91` (#7) |
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
| N8 | Architecture docs and README refreshed | `05cb219` (#8) |
| N9 | Superseded by MANA2 | #5 |
| N10, N11, N13 | Closed when H5 was finished | #4 |
| N12 | Withdrawn: false positive | review 2026-10-20 (#8) |
| N14 | Host returns to the lobby when a match can't start | `fe2d4fb` (#6) |
| N15 | Number not used | #4 |
| N16 | The leaver's suspicious summary is logged on disconnect | `efb88c1` (#8) |
| N17 | Host HUD kept when the opponent forfeits | `05cb219` (#8) |
| N18 | Deaths are handled in every phase | `fe2d4fb` (#6) |
| N19 | Leave button with confirmation, counts as a defeat | `fe2d4fb` (#6) |
| N20 | Safe Send; the target rule is shared with the server | `a0df515` (#7) |
| N21 | Confirm carries the intended phase; mismatches are ignored | `391e079` (#8) |
| N22 | Character HP set in the server's `OnNetworkSpawn` | `391e079` (#8) |
| N23 | `Keyboard.current` null check | `efb88c1`, `05cb219` (#8) |
| N24 | Three scripts converted to UTF-8 | `efb88c1` (#8) |
| N25 | Intended (you, 2026-10-19) | #5 |
| N26 | Unconfirmed selection cleared at each new selection phase | `391e079` (#8) |
| N28 | Font dynamic data cleared on build; commit rule in `AGENTS.md` | `05cb219` (#8) |
| N30 | Lobby refresh throttled | `05cb219` (#8) |
| N31 | Deck size documented as 84 (with L2) | `05cb219` (#8) |
| N37 | Announcer shows both lines in full: unscaled 1560×150 box, font 30–48 | `05cb219`, UI1, UI6 (#9) |
| N38 | Line-2 prefixes have a ": " separator; shorter en/ru respawn prefix | `05cb219`, UI1 (#9) |
| N39 | All six scenes Scale With Screen Size / Expand; nothing cut at 9 resolutions | `05cb219`, UI2 (#9) |
| N40 | HUD anchored to the screen edges and corners | `05cb219`, UI3 (#9) |
| N41 | No non-uniform UI scale left; the per-frame `LeaveMatchUI` resize removed | `05cb219`, UI1 + UI3 (#9) |
| N42 | Login background covers any aspect (Envelope Parent) | `05cb219`, UI2 (#9) |
| N43 | Card preview clamped on screen; always on the HUD canvas (then N47, N50) | `05cb219`, UI4 (#9) |
| N44 | ru "Подтвердить" fits the Confirm button (then N48) | `05cb219`, UI4 (#9) |
| N45 | Hand compresses to 1800 units; 10 cards no longer reach the buttons | `05cb219`, UI3 (#9) |
| N46 | Old hand views deactivated before `Destroy`: no one-frame jump | `05cb219`, UI3 (#9) |
| L1 | Countdown from a replicated deadline | `05cb219` (#8) |
| L2 | All three deck mixes reachable | `05cb219` (#8) |
| L3 | `IsGameFinished` is pure and allocation-free | `391e079` (#8) |
| L4 | Duplicate `BlockController.TeamId` removed | `391e079` (#8) |
| L5 | `GameAnnouncerUI` maps every existing phase | #4 |
| L6 | Announcer, world views, HUD clear and lobby panels change-only (HP/lives → N35) | `05cb219` (#8) |
| L7 | Card texts refresh on a locale change | `05cb219` (#8) |
| L8 | `btn_lobbyRefresh` key without the tab | `efb88c1` (#8) |
| L9 | Debug flags forced off in release builds | `05cb219` (#8) |
| L10 | Dead code, legacy scenes, backups and VisualScripting removed (leftovers → N33) | `05cb219` (#8) |
| U1 | The player build compiles and runs | review 2026-10-20, #8 |
| U3, U4 | Answered (null `NetworkBehaviourReference` is safe; `DontDestroyWithOwner: 0`) | #3 |
| U5 | GUID `e651dbb3…` is NGO's own `NetworkPrefabsList` script | review 2026-10-20 |
| U6 | Explained: each Editor logs its own `GameScene` load in the shared log | review 2026-10-20 |
| U7 | Normal end reaches `ResultScene` on both instances | #4 |
| U8 | `Shutdown()` leaves no objects behind | review 2026-10-20 |
| U10 | The forfeit works at runtime | review 2026-10-20 |
| U11 | Not reproduced | #7 |
| U12 | Mana replicates to a remote client, and the rejection reaches it | review 2026-10-20 |
| U13 | A second lobby and match work after result → main menu | #6 |
| U14 | At 1024×768 (4:3) the whole board and both player areas are visible (your B7.1) | #10 |

**Tasks completed without an issue ID:**
- MANA1–MANA3 (`ade4aed`, `5eec046`, `058fa63`)
- SEC1 (`f08e854`)
- HYG1 (`efb88c1`)
- UI5, display settings (`72a724c`, reviewed in #10; your build checks on 2026-10-22, section C)
- UI6, readability profiles (`6d89a09`, reviewed in #11; your checks on 2026-10-22, section B)

---

## 7. Questions: the poll

**File:** [Docs/AI/Polls/2026-10-23_Poll-Report13.xlsx](../Polls/2026-10-23_Poll-Report13.xlsx). This is the new poll format you asked for:
- **one row per question,** with the background, the options in their own columns and my recommendation
- **an answer drop-down** and a comment cell
- **a *Priorities* sheet** to rank the feature gaps
- **a counter** showing how many questions are still open

| # | Question | My recommendation |
|---|---|---|
| **P1** | **N59:** what should the client see when the host quits mid-match? | **b:** a Result screen with no winner, "Connection to the host was lost" (new text in en/ru/ja). It's honest when the client's own network drops, and it avoids two winners. |
| **P2** | **W3:** `AuthManager.SingOut()` / `SingOutAndClearCredentials()`. Keep for a future sign-out button, or delete? | **b: delete.** Git keeps them, and a sign-out feature needs its own spec. |
| **P3** | **The font:** keep the committed (Editor-changed) version, or roll back to the clean `72a724c` one? | **a: keep.** It's harmless, and it makes future Editor runs produce smaller diffs. |
| **P4** | **N60:** clean the cosmetic leftovers now, with the next task that touches those files, or never? | **b: with the next task.** |
| **P5** | **`build/`** is 3.5 GB. Delete Codex's verification folders for accepted tasks (about 2.3 GB)? This round's four stay. | **a: delete.** They're git-ignored, so there's no undo. |
| **P6** | **`build/`:** the June IL2CPP output (`Il2CppOutputProject`, `FallenStar-Spellcasters/`, `.sln`, Burst info; about 1.2 GB). Yours, still needed? | — your call |
| **P7** | **Old issue reports #1–#11:** this report holds the full register. Delete them (git keeps them)? | **a: delete.** |
| **P8** | **N27** (mane colour): still parked? | — |
| **P9** | **N29** (lobby ID shown twice): still parked? | — |
| **P10** | **N36** ("00" flash): leave it, or fix it? | **a: leave.** It's cosmetic, and a fix touches the timer code. |
| **P11** | **M11** (join by lobby code): still parked? | — |
| **P12** | **UI7** (the low profile for the menu scenes): start after this checklist? | **a: yes** |
| **P13** | **This new poll format:** a poll workbook from now on, chat, or both? | — your call |
| *Priorities* | Rank 9 feature gaps: Now / Next / Later / Never. Each row has my rough size (S / M / L). | — |

**You can also answer here in chat.** I'll copy the answers into the next report.

---

## 8. Folder cleanup

**Removed (not committed).** Each file is still in git history: `git show <commit>:<path>`.

| Folder | Files | Last commit | Why |
|---|---|---|---|
| `ManualChecks/` | `2026-10-19_ManualChecks-Report6.md` | `a0df515` | Old checklist, reviewed (#6–#8) |
| | `2026-10-20_ManualChecks-Report6-7.xlsx` | `efb88c1` | Old checklist, reviewed (#8) |
| | `2026-10-21_ManualChecks-Report8-9.xlsx` | `05cb219` | Old checklist, reviewed (#10) |
| | `2026-10-22_ManualChecks-Report10-11.xlsx` | `6d89a09` | Reviewed in #12, and its "Finded" note here (§2) |
| `Reviews/` | `2026-10-20_ManualChecks-Report6-7_Review.md` | `efb88c1` | Old checklist review |
| `Reports/` | `2026-10-19_H2_…`, `H3_…` | `e7d1b03` | Implementation reports of accepted, committed work |
| | `2026-10-19_MANA2_…` / `MANA3_…` | `5eec046` / `058fa63` | 〃 |
| | `2026-10-19_N14_…`, `N18_M1_…`, `N19_…` | `fe2d4fb` | 〃 |
| | `2026-10-19_N20_M14_M8_…`, `2026-10-20_M10_M3_M4_M6_…` | `a0df515` | 〃 |
| | `2026-10-20_L1_L2_N31_N30_N17_L6_L7_L9_L10_N28_…`, `2026-10-21_UI1_UI2_UI3_UI4_…` | `05cb219` | 〃 |
| | `2026-10-20_L4_N2_N6_L3_N7_N22_N3_N21_N26_…` | `391e079` | 〃 |
| | `2026-10-20_M11_M15_M12_M13_…`, `M16_N1_…` | `b7e7b91` | 〃 (M11 is parked. Its notes are in that commit, and the code points to TASK-M11 §10 in history.) |
| | `2026-10-20_N24_HYG1_…` | `efb88c1` | 〃 |
| | `2026-10-21_UI5_…` | `72a724c` | 〃 |
| | `2026-10-22_N50_N52_…`, `UI6_…` | `6d89a09` | 〃 |
| `Research/` | `RESEARCH-ManaSystem.md` | `d44b882` | MANA1–MANA3 are implemented |
| `Tasks/` | `TASK-UI6_…`, `TASK-N50_N52_…` | `6d89a09` | Removed in report #12 (still unstaged) |

**Kept:**

| File | Why |
|---|---|
| `Tasks/TASK-N54_N56_N57_…`, `TASK-N55_N58_N32_…`, `TASK-N33_N34_N35_…` | Implemented, but **untracked.** Deleting them now would lose them for good. **Remove after your commit.** |
| `Reports/2026-10-22_N32-N35_N54-N58_Implementation.md` | Untracked, and the checklist isn't done. Remove after the commit. |
| `Reports/…IssueReport*.md` #1–#12 | Poll **P7**. #12 is untracked. |
| `Research/RESEARCH-H6C_ReconnectMidMatch.md` | Rejoining isn't implemented; it bears on N59 |
| `Research/RESEARCH-UIScaling_Visibility.md` | UI7 builds on it |
| `Architecture/*.md` | Living docs. Their headers now name this baseline. Their links to deleted reports are replaced. |

**Not touched:** `build/` (poll P5 / P6).

**Also updated:**
- **`Architecture.md`:** header; the stale `m_loginSceneName` line; the L1–N28 unknown
- **`GameLoop.md`, `Dependencies.md`:** header; the L1–N28 unknown

---

## 9. Next steps

1. **The poll** (any time) and **the checklist** (two Editors, then a new build).
2. **If the checklist passes, commit** the three tasks, the docs and this cleanup.
   - **Before the commit, with both Editors closed:**
     - set the language back to English
     - run `git restore "Assets/Fonts/MPLUSRounded1c-Medium SDF.asset"`. Ru/ja text in the Editor changes the font again, and `restore` returns it to `HEAD`.
     - If you choose P3 = b, instead run `git checkout 72a724c -- "Assets/Fonts/MPLUSRounded1c-Medium SDF.asset"`.
   - **After the commit:** I remove the three specs and Codex's report (then committed), and close N54–N58 and N32–N35.
3. **After your P1 answer,** I write the N59 spec. N60 goes with it if P4 = b.
4. **After P12:** UI7.
5. Optional checklist row I1 after the commit.

## Sources

- **Codex:** `Docs/AI/Reports/2026-10-22_N32-N35_N54-N58_Implementation.md`
  - `build/task-folder-verification/` (`play-results.txt`, `captured-errors.txt`, `native-clicks.txt`, `build-results.txt`, `release-smoke.txt`, `cleanup.txt`)
  - `build/n55-verification/`, `build/hyg2-verification/`
- **The diff:** `git diff` against `6d89a09`: 28 code and asset files, 3 docs
- **Your 2026-10-22 checklist:** `git show 6d89a09:Docs/AI/ManualChecks/2026-10-22_ManualChecks-Report10-11.xlsx`, *Known issues* rows 38–39
- **Logs:**
  - `C:\Lesson\clone-editor.log` and `Player-prev.log` (2026-10-22): no host-quit lines, because they're debug-gated
  - `Player-prev.log:310`, "Remote client disconnected. Host continue": the client-quit direction
- **Scenes and assets:**
  - `GameScene.unity` spawn points: `SpawnPoint` (team 0) and `SpawnPoint (1)` (team 1)
  - `NetworkManagerPrefab.prefab:95-98` (transport timeouts)
  - `ProjectSettings.asset:86`, `EditorBuildSettings.asset`, `DefaultNetworkPrefabs.asset`, `Assets/Localization/*`
