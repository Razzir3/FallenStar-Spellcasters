# Issue Report #16: FallenStar – Spellcasters (your checklist and poll, Codex's mane and HUD work, a structure pass)

- **Date:** 2026-10-24
- **Baseline:** `HEAD` = `5abd155` ("F9, F14a, N62, N27"), on top of `314498a` ("N59_n61_f1_f4_f6_N36_UI7"). The working tree had no changes when I started.
- **Previous report:** #15, deleted this round (poll #15 Q2 = a: keep only the newest); git keeps it.
- **Your request:** "Check my and Codex reports. After, analyze project structure once more. Prepare new report, poll, clean old completed docs from AI folder."
- **Method:**
  1. Read every non-empty cell of every sheet of your filled `ManualChecks/2026-10-24_ManualChecks-Report15.xlsx` and `Polls/2026-10-24_Poll-Report15.xlsx`, including the stray cells and your four new *Priorities* rows.
  2. Checked your notes against the Editor logs: `%LOCALAPPDATA%\Unity\Editor\Editor-prev.log` and `Editor.log` (both turned out to be the **clone's**, §1.3) and `C:\Lesson\clone-editor.log`.
  3. Read Codex's report and **every line of the code diff** in `5abd155`, plus the prefab, material and import-setting diffs, against TASK-N27 and TASK-F9_F14a_N62.
  4. Checked Codex's evidence in `build/n27-verification/` and `build/f9-f14a-n62-verification/`, and opened four of its images myself.
  5. **Structure pass:** sizes of all 60 scripts, the three Architecture docs against the code, and a GUID scan of which asset folders the scenes, prefabs and materials use (§5).
  6. Read the code for the next two tasks: the deck and hand lists, `PlayerUI`'s mana lines, the HUD prefab rects, the lobby refresh and the mane materials.
- **I didn't run Unity, a build or Play Mode.** I changed no C# file, scene, prefab or asset.
- **What I changed:** this report, two specs, this round's checklist and poll, the headers and a few stale lines of the three `Architecture/*.md` files, and **deleted** eleven reviewed, committed docs (§10).
- **New IDs:** **N63** and **N64**. **U9 is reopened.** The next new finding would be **N65**.

**This report is the full issue list.** §8 lists every open issue and §9 every closed one.

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
| **Your checklist #15** | **Passed:** 43 rows, 35 Pass, 4 N/A, 4 optional rows not run, no Fail. **N59, N61, N36, F1, F4, F6 and UI7 close** (§1). N59 was the last open 🟡. |
| **Your two notes** | **A4.1 "lobby not found":** a real race. A lobby refresh reaches the service after your own Disconnect deleted the lobby, and the code logs it as a red error → **N63** 🟢 (§3.1).<br>**B "can't be run":** the clone's log shows all three offline attempts did run. The message only comes after the services' 10 s timeout. This round's section E repeats them with a 15 s wait (optional). |
| **Codex's work** (`5abd155`) | **Both tasks match their specs** (§2). No defect found.<br>• N27: the mane sits right (I looked at the renders); mane warnings 4 → 0; respawn and new-match colours checked.<br>• F9 / F14a / N62: fades 0.77 s / 0.62 s measured; six boxes checked in 2 sizes × 3 languages; the release build succeeded.<br>N27 has **no Codex report**: that session was interrupted, and the F9 session built on top of it. Its evidence is complete anyway. |
| **New findings** | **N63** 🟢 the red "lobby not found" (above).<br>**N64** 🟢 the **"Confirm turn" button is English in every language**: the key `btn_confirmTurn` has ru / ja texts, but nothing uses it, since July (§3.2).<br>**U9 reopened:** NGO's `NetworkSceneManager.Dispose` NRE appeared once more when the clone stopped Play Mode (§3.3). |
| **Structure pass** | 60 scripts, 13.0k lines. The architecture risks are unchanged. I fixed a few stale lines in the docs. **About 281 MB of tracked asset packs aren't used by anything** (two of them are effect packs that fit your F19). Details and no-task recommendations in §5. |
| **Your poll #15** | 6 of 6 answered, plus four new ideas (F17–F20) and a request to sort the *Priorities* sheet: done (§4). |
| **New Codex tasks** | **[TASK-F15_F16_N64](../Tasks/TASK-F15_F16_N64_HudCounters.md):** "Deck : 37" and "Opponent's hand : 5" under the opponent's mana, with dark boxes, and the localized Confirm Turn label.<br>**[TASK-N63_ManePalette](../Tasks/TASK-N63_ManePalette.md):** the quiet lobby refresh, and your palette (white, gold, pink, violet, orange).<br>Both start after checklist #16 passes. They share no files. |
| **This round's checklist** | [2026-10-24_ManualChecks-Report16.xlsx](../ManualChecks/2026-10-24_ManualChecks-Report16.xlsx): 29 rows, 20 required, for N27, F9, F14a, N62 (§7). |
| **New poll** | [2026-10-24_Poll-Report16.xlsx](../Polls/2026-10-24_Poll-Report16.xlsx): **4 questions** (U9, the unused packs, F19's assets, splitting F17). The *Priorities* sheet is now sorted by rank. |
| **Clean-up** | **Deleted** eleven committed docs (§10): last round's checklist and both polls, report #14 and #15, Codex's accepted report, three finished specs, the mane guide, the UI-scaling research. |
| 🔴 / 🟠 / 🟡 open | **0 / 0 / 0** |

---

## 1. Your checklist #15

### 1.1 Results

| Section | Rows (required) | Result | Notes |
|---|---|---|---|
| A. Lost host and leaving | 10 (8) | ✅ Pass | A5.1 (optional) N/A: "Can't reproduce. Load scene too fast". Codex's harness covered `MarkMatchCancelled`. |
| B. Messages with the network off | 3 (0) | ➖ N/A | "Whole block can't be running." The attempts did run (§1.3). Repeated as section E (optional). |
| C. Hints in a match | 6 (5) | ✅ Pass | C1.4 recorded N62's old hint, as planned. C2.1 (deck refill, optional) not run. |
| D. Draw phase, timers, blocks | 9 (5) | ✅ Pass | D4 (the T1 block test, optional) not run. F6 rests on Codex's harness, which destroyed all 25 blocks of a player and ended the match. |
| E. Compact menus | 8 (7) | ✅ Pass | Both of your UI7 exceptions look right (E4). |
| G. Release build | 5 (5) | ✅ Pass | Lost host in the build, the quiet log, no exception. |
| H. Editor logs | 2 (1) | ✅ Pass | H1.2: "none". See §3.3 for the clone's log. |

**Closed by this checklist:** N59, N61, N36, F1, F4, F6, UI7 (§9).

### 1.2 Two small things in the workbook (no effect on the result)

- **Row C1.4's Group cell** held the clone-start text from *Read me* instead of "C1". So the Summary counted C1 with 4 rows and showed 42 items and 34 Pass. The row itself says Pass; the real totals are 43 and 35.
- **The *Read me* sheet** got "Owner / date" in columns L–M of many rows, and four notes in N12, N14, N16, N53. They're copies of the Checklist's notes. I read them; nothing was lost.
- This round's *Read me* asks to type only in the yellow cells.

### 1.3 What the logs show

- **Both `%LOCALAPPDATA%` Editor logs are the clone's** (`-projectPath C:/Lesson/FallenStar-Spellcasters_clone_1`, not batch mode): `Editor-prev.log` (until 17:56) and `Editor.log` (until 18:33). So the clone ran at least once **without** `-logFile`, and its lines replaced the main Editor's. In those sessions **the clone was the host** ("Remote client disconnected. Host continue").
- **A4.1 (16:58:36):** the clone's `Editor-prev.log`, line 3839: `LobbyServiceException: lobby not found` (404) from `GetLobbyAsync` ← `RefreshCurrentLobbyCore` (`MultiplayerManager.cs:989`), logged at `:1000`, right after a LobbyScene load. That's **N63** (§3.1). The log has no timestamps, so it can't confirm or refute your one-Lobby-load count. Your Pass stands.
- **B (offline):** the attempts are all there:
  - `Editor-prev.log` 6660 and 6785: two `QueryLobbiesAsync` "Request timeout" from `FindAvailableLobbies` (B1.1)
  - 6215: a Relay "Request timeout" during `CreateAllocationAsync` (B1.2)
  - `clone-editor.log` 1271–1376: sign-in "Cannot resolve destination host" (B1.3)

  Each catch posts its message (`MultiplayerManager.cs:850-858` for the search), but only when the request gives up: **"Operation timed out after 10000 milliseconds"**. If you looked for the box during those 10 s, it wasn't there yet. Section E repeats the three rows with a 15 s wait; it's optional.
- **U9:** the clone's `Editor-prev.log` 19319 / 19333, §3.3.

---

## 2. Codex's work (`5abd155`)

### 2.1 Task by task

| Task | What the spec asked | Verdict | Notes |
|---|---|---|---|
| **N27** | Preserve Hierarchy, no rig or animation import; five mane materials; `ManeColor`; `m_maneMaterials` in `PonyColorScript`; `AssignManeColors` before the first spawn, cleared on disconnect; `ApplyMaterial` with the server's colour; `PonyMane` under the prefab root; the N60 `//ToDo` gone | ✅ Matches | The code is the spec's code, line for line. `PonyMane.fbx.meta`: `preserveHierarchy: 1`, `importAnimation: 0`, `animationType: 0`; scale and axis settings untouched. The prefab adds a `PonyMane` instance under the root at identity, slot 0 = `ManeWhite`, `maneObject` = the `Mane` GameObject, the five materials in enum order. **No Codex report:** the session was interrupted; the F9 report says so and built the release player with it. |
| **F9_F14a_N62** | Fade with `Time.unscaledDeltaTime`, one current and one fading view, reuse of the same card, `HidePreview(true)` for cleanup, `OnDisable` / `OnDestroy`; `HudTextBacking` and six backings; `IsOpponentActing` before the package check | ✅ Matches | `DestroyPreview` also detaches the view before `Destroy`, so a dock that outlives the controller never shows it: safer than the spec. `ConfirmTurn` now reads the phase through the helper; `_intended` is the same value read in the same frame, so behaviour is identical. |

### 2.2 What I checked myself

- **Code:** every changed line of `CardPreviewController`, `HudTextBacking`, `PlayerUI` (one line), `PlayerSession`, `GameMaster` (+ `.Spawn`), `ManeColor`, `PlayerController`, `PonyColorScript`.
- **Side effects:**
  - **Mane on clients:** `ApplyMaterial` uses `NetworkManeColor` on clients; `OnManeColorChanged` re-applies later values; `ApplyManeMaterial` now indexes `m_maneMaterials`. The body still uses `m_materials`. Selection white and reset go through the same paths.
  - **`m_maneColors` keys are `PlayerSession`s,** cleared in `ClearupAfterDisconnect`; a respawn reads the same entry. A second match re-shuffles in `SpawnPlayers`.
  - **The fade** only changes `CanvasGroup.alpha`; `blocksRaycasts` / `interactable` stay as the prefab has them, so a fading card doesn't take clicks.
  - **The boxes** turn `raycastTarget` off in `Awake` and on the prefab; they're placed only when the text's bounds or transform change.
- **Assets:**
  - the five materials are `TestMaterial0` with only `_BaseColor` / `_Color` changed, to the spec's values
  - the six `…Backing` objects are in `PlayerUIPrefab`, each before its text
  - no font, `ProjectSettings` or `DefaultNetworkPrefabs.asset` change in the commit
- **Images I opened:**
  - `n27-verification/side.png`: the mane follows the head and neck, the forelock between the ears
  - `n27-verification/match-pair.png`: blue pony with a black mane, red pony with a pink one
  - `f9-f14a-n62-verification/HUD-ru-1280x720.png`: all six boxes behind readable texts. **It also shows N64:** the third button reads "Confirm turn" in the Russian HUD.

### 2.3 Codex's measurements

| Measurement | Before | After |
|---|---|---|
| N27: "Invalid mane material index." for two spawned ponies | 4 | 0 (also through a respawn and a second match) |
| N27: mane height / body height | — | 0.544 (target 0.54 ± 0.05); mane inside the body bounds + 10 %, centre in the upper 40 % |
| N27: colour picks | — | 1,000 runs: always two different colours, all five appear |
| N62: Send on the opponent's turn | "Choose a target …" | "It's your opponent's turn.", package kept, nothing queued on the server |
| F9: fade in 0.7 s / out 0.6 s | — | 0.770 s (incl. a 0.52 s first-use frame) / 0.616 s; 0.2 / 1.5 s custom values within one frame |
| F9: 40 fast switches | — | at most two views under the dock, none afterwards |
| UI6 HUD sweep | 199 inherited diagnostics | the same 199 |
| Release build | — | Succeeded, 0 errors, 2 warnings (services / Addressables settings, as before) |

### 2.4 Notes (not defects)

- **The announcer's box sits on the announcer's grey panel.** That's what the spec asked for. If you'd rather have no box there, say so in checklist row C1.1.
- **At 1280×720 the HP and Life boxes touch** (screenshot). Row C1.2 lets you judge.
- **Codex's remaining risks** are real but not defects: no real two-peer run, no remote-client mane check. Checklist sections A and G cover them.

---

## 3. New findings

### 3.1 N63: a red "lobby not found" after a Disconnect

| ID | Level | Issue | Location | Verdict |
|---|---|---|---|---|
| **N63** | 🟢 Low | **A lobby refresh that finishes after the lobby was deleted logs a red `LobbyServiceException: lobby not found`.** `CurrentLobbyUI` refreshes every 2 s. `ShutdownHostInternal` awaits `DeleteLobbyAsync` while `m_currentLobby` is still set, so a refresh in flight (or started during the delete) gets 404, and `RefreshCurrentLobbyCore` logs every reason except `RateLimited` with `Debug.LogError`. The same can happen on a client when the host deletes the lobby. Nothing else goes wrong: the method returns `false` and the view closes. | `MultiplayerManager.cs:984-1007`, `:498-536`; `CurrentLobbyUI.cs:123-147` | Confirmed (code; the clone's log, §1.3) |

- **Fix:** [TASK-N63_ManePalette](../Tasks/TASK-N63_ManePalette.md) §4.1 adds a `LobbyNotFound` catch: silent while leaving, a warning if the lobby vanished while still current. A small static helper makes it testable, like `ShouldShowConnectionLost`.

### 3.2 N64: the "Confirm turn" label isn't localized

| ID | Level | Issue | Location | Verdict |
|---|---|---|---|---|
| **N64** | 🟢 Low | **The Confirm Turn button shows "Confirm turn" in en, ru and ja.** Its TMP text has no `LocalizeStringEvent`, although `UI_table` has `btn_confirmTurn` (en "Confirm Turn", ru "Подтвердить ход", ja "ターンを確認"). The other three HUD buttons are bound. It dates from commit `75fab38` (July); no report or checklist caught it. | `PlayerUIPrefab.prefab:3219-3290` (`ConfirmTurnBtn/Text (TMP)`) | Confirmed (prefab; Codex's ru screenshot) |

- **Fix:** [TASK-F15_F16_N64](../Tasks/TASK-F15_F16_N64_HudCounters.md) §4.5, the same file as the counters.

### 3.3 U9 reopened: NGO's NRE when Play Mode stops

- **Seen:** the clone's `Editor-prev.log` (lines 19319 and 19333), after a match that ended by Leave ("ResultScene: You won. Reason: Surrendered"), when Play Mode stopped: `NullReferenceException at Unity.Netcode.NetworkSceneManager.Dispose () … NetworkSceneManager.cs:600` ← `NetworkManager.ShutdownInternal` ← `OnDestroy` ← `OnApplicationQuit`, twice.
- **Inside NGO 2.11.2:** line 600 loops over `SceneEventDataStore`, which a first `Dispose` already set to `null`. No game frame is on the stack.
- **Your H1.2 "none" is consistent:** the line is in a log the checklist didn't name (the clone without `-logFile`).
- **You closed U9 in poll #14 with "reopen if seen again", so it's open again** as an unknown. The release `Player.log` had no exception (G1.5). Poll #16 Q1 asks whether to just watch it (my recommendation) or research it.

---

## 4. Your poll #15 answers

| # | Your answer | Your comment | Applied |
|---|---|---|---|
| **Q1** (mane colours) | **c** | "White, gold, pink violet and orange" | Read as five colours: white, gold, pink, violet, orange. **[TASK-N63_ManePalette](../Tasks/TASK-N63_ManePalette.md)** renames Black → Violet and Brown → Orange (same indices, same material GUIDs, so the prefab doesn't change) and recolours them. Its §10 A2 asks if you meant one "pink-violet" shade instead. |
| **Q2** (delete rule) | **a:** you'll add it | — | Not there yet (no `.claude/settings.local.json`). The deletes worked again this round anyway. Report #15's steps still apply whenever you like. |
| **Q3** (deck counter) | **a:** a HUD line | "keep b idea for future" | In TASK-F15_F16_N64. The face-down card is **F15b** (Later). |
| **Q4** (hand counter) | **a:** a line under the opponent's mana | "same as q3" | In TASK-F15_F16_N64. The card-backs row is **F16b** (Later). |
| **Q5** (next work) | **a:** F15 + F16 | "New card idea on the next week. I'll try to prepare some cards with descriptions" | Done as one task. F5 stays Next; I'll wait for your card list. |
| **Q6** (Codex asking you) | **a:** fine | "If it's not lead to securiti issues" | Recorded: Codex may ask you directly about layout and similar exceptions. Anything touching server authority, input checks or sign-in stops and goes into its report. Both new specs say so. |

**Your *Priorities* sheet:**
- **"Can you please sort futures by priority in this poll next time?"** Done: the new sheet runs Now → Next → Later → Done.
- **Your four new rows, all Later:**

  | ID | Your idea | Note |
  |---|---|---|
  | **F17** | Moving animation + rotation ("Delegate to other claude agent to create animation for current character model") | The pony has no skeleton. Poll #16 Q4 suggests a split: turn + hop in code now-ish (F17a), a rigged walk later (F17b). |
  | **F18** | Reposition the game view: focus on your side, the enemy side, or the whole table | — |
  | **F19** | Card click / selection animation, and effects for both players (explosions, magic); "Need some research, I'll give you some assets" | Two effect packs are already in the project (§5.3); poll #16 Q3 asks if those are the ones. |
  | **F20** | Camera-focus settings and play-hint / auto-zoom settings, "when general animation system will be done" | Overlaps F11 / F12 (hints). |

---

## 5. Project structure, once more

### 5.1 Code size and shape

| Area | Files | Lines | Largest |
|---|---|---|---|
| `MultiplayerScripts/` | 3 | 2,059 | `MultiplayerManager.cs` 1,601 (its own `//ToDo Need full rework` at `:48`) |
| `InGame/GameMaster*` (partial class) | 8 | 3,395 | `GameMaster.GameLoop.cs` 757, `GameMaster.cs` 664 |
| `InGame/` (other) | 22 | 3,329 | `PlayerSession.cs` 1,185 |
| `UI/` | 22 | 3,040 | `PlayerUI.cs` 764 |
| `Manager/` | 2 | 940 | `CardWorldViewManager.cs` 829 |
| root (`Bootstrap`, `GameSettingsPrefs`, `DebugModeGuard`) | 3 | 240 | — |
| **Total** | **60** (the docs said 56) | **13,003** | |

- **The architecture risks A1–A7** (`Architecture.md` §10) are unchanged by this round's code: card identity outside the network model, server code calling client UI, team = slot index, the nested SpawnPoint NetworkObject (U2), the double cleanup, the dynamic font.
- **Three files carry most of the change risk:** `MultiplayerManager` (lobby, relay, start, leave and disconnect in one class; 12 `async void` handlers in it and the login / lobby UI), `PlayerSession` (network state, owner input, selection and HUD glue) and `GameMaster.GameLoop`. **No task now.** The natural moment to split `MultiplayerManager` is your planned Server + clients move (`Architecture.md` §5).
- **Open `//todo` markers:** 11, in `GameMaster.Processing` (4), `GameMaster.Spawn`, `PlayerSession` (2), `PlayerUI`, `MultiplayerManager` (3, two of them M11's parking notes). They are notes, not defects.

### 5.2 Docs drift, fixed in this round

- `Architecture.md`: the file count (56 → 60), `ManeColor.cs` and the newer UI scripts in the folder tree, `Assets/Models/` and `Assets/Materials/Mane/`, the asset packs, ParrelSync's real path, U9's state.
- `Dependencies.md`: ParrelSync is under `Assets/ParrelSync-master/ParrelSync-master/ParrelSync/`, not `Assets/ParrelSync`.
- All three headers now name `5abd155` and this report.

### 5.3 Asset packs nobody uses

A scan of every `guid:` in the project's own scenes, prefabs, materials and assets:

| Folder | Size | Used by |
|---|---|---|
| `Fantasy Wooden GUI  Free` | 29 MB | 7 sprites in the menus and HUD buttons |
| `Stylized Lava Materials` | 66 MB | `M_Lava03` (GameScene) and its textures |
| `SkyboxHandPainted` | 30 MB | two skyboxes |
| `Background`, `Logo` | 3 MB | LoginScene, menus |
| **`500FreeSkillIcons`** | **200 MB** | **nothing** |
| **`JMO Assets/Cartoon FX Remaster`** | **41 MB** | **nothing** (65 effect prefabs: explosions, electric, …) |
| **`GabrielAguiarProductions/FreeQuickEffectsVol1`** | **40 MB** | **nothing** (explosion, heal, lightning, flames, …) |
| **`TutorialInfo`** + `Readme.asset` | 63 KB | only each other (the URP template's welcome page) |

- **Unused assets don't go into builds,** so players aren't affected. They make every clone, import and backup about 281 MB heavier.
- **The two effect packs fit F19.** Poll #16 Q2 asks what to remove (my recommendation: only the URP template leftovers); Q3 asks if they're the F19 assets you meant.
- `Assets/WSATestCertificate.pfx` is Unity's standard test certificate for UWP builds, from the template. It's harmless (⚪, not on the register).

---

## 6. The next tasks for Codex

| Task | Covers | Touches | Size |
|---|---|---|---|
| [TASK-F15_F16_N64_HudCounters](../Tasks/TASK-F15_F16_N64_HudCounters.md) | F15, F16, N64 🟢 | `GameMaster` (+ `.Card`): one NetworkVariable `m_deckCount`; `PlayerSession`: `GetUncommittedHandCount()`; `PlayerUI`; `PlayerUIPrefab` (two lines with boxes, the Confirm Turn binding); two localization keys | S–M |
| [TASK-N63_ManePalette](../Tasks/TASK-N63_ManePalette.md) | N63 🟢, poll #15 Q1 | `MultiplayerManager` (one catch + a helper), `ManeColor`, two renamed materials | S |

**Design points:**
- **F15:** the deck lives only on the server, so `GameMaster` gets `m_deckCount`, written after every deck change. One new NetworkVariable: both peers need the same build.
- **F16:** the hand lists already replicate to every peer. The count leaves out cards the opponent has already put on the table (they stay in `m_handCards` until they reach the graveyard). That spec's §10 A1 has the one-line alternative.
- **Layout:** the two lines go under the opponent's mana (y −300 / −350). In the compact layout the hint line moves from y −330 to −410, because a long Russian hint would otherwise overlap the new right-column lines (§4.4 of the spec has the numbers).
- **Palette:** renaming keeps the enum indices and the material GUIDs, so `PonyBase.prefab` and the network values don't change.

**When and in what order:**
- **Both start after checklist #16 passes** and its round is committed.
- They share no file, so the order is free. I'd do **TASK-F15_F16_N64 first**: it's the one you'll use in every match.

---

## 7. This round's checklist

[ManualChecks/2026-10-24_ManualChecks-Report16.xlsx](../ManualChecks/2026-10-24_ManualChecks-Report16.xlsx), the same layout as last time (*Read me*, *Checklist*, *Summary*, *Known issues* with the yellow "Found during the checks" table):

| Section | Rows (required) | Covers |
|---|---|---|
| A. The mane | 7 (6) | N27: prefab, two colours on both peers, respawn, new match, selection |
| B. Big-card fade | 6 (4) | F9: hover, sweep, table clicks, clicks through a fade; the Inspector fields; leaving |
| C. Dark boxes | 4 (3) | F14a: six texts, both layouts, ru / ja, empty hint |
| D. Send on the opponent's turn | 4 (3) | N62, from both windows |
| E. Offline messages | 3 (0, optional) | F1: last round's B with a 15 s wait |
| G. Release build | 3 (3) | N27, F9, F14a; a clean `Player.log` |
| H. Editor logs | 2 (1) | all; U9 |

- **Start the clone with `-logFile`** (*Read me → Preparation 1*): last round its lines replaced the main Editor's.
- **No temporary code change this round.**
- **Before you commit:** restore the font; check that `PonyBase.prefab` isn't modified (A1.1 only looks at it).

---

## 8. All open issues

### 🔴 Critical / 🟠 High / 🟡 Medium

None open.

### 🟢 Low (6)

| ID | Issue | Location | Verdict | Next |
|---|---|---|---|---|
| **N63** | A lobby refresh after the lobby's deletion logs a red "lobby not found" (§3.1) | `MultiplayerManager.cs:984-1007` | Confirmed | TASK-N63_ManePalette |
| **N64** | "Confirm turn" isn't localized (§3.2) | `PlayerUIPrefab.prefab` `ConfirmTurnBtn` | Confirmed | TASK-F15_F16_N64 |
| N27 | No mane; "Invalid mane material index." at every spawn | `PonyBase.prefab`, `PonyColorScript.cs` | Confirmed | **Implemented** (`5abd155`); closes with checklist A, G1.3 |
| N62 | Send on the opponent's turn showed "Choose a target …" | `PlayerSession.cs` `TryHandlePackage` | Confirmed | **Implemented** (`5abd155`); closes with checklist D |
| N60 (rest) | Commented-out blocks in `PlayerCardfWorldArea.cs:47`, `:77`, `:124`, `:131`, `CardControllPackage.cs:11`, `:21`, `NetworkCard.cs:178`, `CardWorldViewManager.cs:764`. (The `PlayerController` `//ToDo` is gone with N27.) | as listed | Confirmed | With the next task touching each file (P4 = b) |

### ⚪ Info

- **N29:** the lobby shows the player ID twice. **Parked by you** ("still needed for debugs"). (`UI/LobbyPlayerItemUI.cs:53-65`)
- **Naming and typos** (report only; don't rename):
  - `CardControllPackage`, `PlayerCardfWorldArea.cs`, `SetOponent` / `m_currentOponentNetworkObjectId`, `CheckActiveTaergetSelection`
  - `SingInAnonymous` / `SingOut` / `SingOutAndClearCredentials` / `m_isSingingIn`, `HearthbeatLobby`, `MovePlayeToSpawnPoint`, `ClearupAfterDisconnect`
  - `CardSpecialization.DeffenceSpell`, `m_knowLobbies`, `m_bootsTrap`, `_teamAssing`, `m_phaseAnouncementText`, `#region Valiable`, the GameObjects `PhaseAnouncer` / `PhaseAnouncerBacking`
  - log texts `"Packege"`, `"Handle unseccess"`, `"Dissconecting client"`, `"Joinde selected lobby"`, `"Need exatly 2 …"`, `"Not Enought card for section"`; the local `_locakPoint`
  - `ConfirmSendButtonUI1` (in `ConfirmSendButton.cs`); `m_startHandCards` (really the per-round draw count); the Confirm binding in `PlayerUIPrefab.prefab` names `ConfirmButtonUI` (it resolves)

### ❓ Unknowns and possible issues

| ID | Question | Status |
|---|---|---|
| U2 | The nested `SpawnPoint` NetworkObject inside `BlockBody` | ❓ No NGO warning in any log so far |
| **U9** | NGO `NetworkSceneManager.Dispose` NRE when Play Mode stops (§3.3) | ❓ **Reopened**; poll #16 Q1 |
| U17 | Sign-out code kept (P2 = a). `AuthManager.SingOutAndClearCredentials()` has swapped branches. **Fallback:** fix the order when a sign-out button is added, or delete both. | `AuthManager.cs:116-133`. Kept by you. |
| U18 | No result screen for a lost host (P1 = c). **Fallback:** a no-winner Result screen with the same text. | Your choice; revisit with the Server + clients move |

### Feature gaps (not defects)

The new poll's *Priorities* sheet has all of them, sorted:

| Rank | # | Gap | Status |
|---|---|---|---|
| Now | F9 | Big-card fade | Implemented; checklist B |
| Now | **F15** | Deck counter | TASK-F15_F16_N64 |
| Now | **F16** | Opponent's hand counter | TASK-F15_F16_N64 |
| Next | F5 | More card effects | Waiting for your card list ("next week") |
| Later | F2, F3, F7, F8, F10–F13 | Settings, Steam, spectators, rejoin, movement limit, hints, hint settings, audio | — |
| Later | F14 | Full GUI redraw (F14a, the boxes, is implemented) | — |
| Later | **F15b**, **F16b** | The face-down deck card, the card-backs row | your Q3 / Q4 notes |
| Later | **F17–F20** | Pony animation, camera views, card / spell effects, animation and camera settings | your new rows (§4) |

**Also known, by design or parked:** join by lobby code (M11, until Server + clients); `RoundEnd` unused and `ResolveCards` transient; the confirmed target doesn't survive a respawn pause; with 10 cards the hand overlaps (UI3); offline failures appear only after the services' 10 s timeout.

---

## 9. Closed issues

**Closed in this report** (checklist #15 passed, committed in `314498a`):

| ID | How it was closed |
|---|---|
| **N59** 🟡 | The client gets "Connection to the host was lost." after the host quits, crashes or leaves (A1, A2, G1.3) |
| **N61** 🟢 | A host's own Disconnect loads the Lobby once (A4) |
| **N36** ⚪ | The countdown and the phase change agree (D2) |
| **F1** | On-screen messages: lobby and login failures, hints, cancelled match, deck refill (A, C; B by code and Codex's harness) |
| **F4** | The "Drawing cards" phase (D1) |
| **F6** | Losing the last block ends the match (D3 unchanged; the block rule by Codex's harness, D4 optional and not run) |
| **UI7** | Compact menus below 1366 px, Settings down to 640×480, your two exceptions (E, G1.2) |
| N60 (part) | The `PlayerController` `//ToDo` (with N27, `5abd155`) |

**Closed earlier:**

| ID | How it was closed | Commit / report |
|---|---|---|
| U9 | Closed by you (poll #14 Q7 = a); **reopened in this report** | #15 → #16 |
| N54 | Board card texts no longer catch UI clicks | `ff8a9dd` (#14) |
| N57 | An early Confirm Turn no longer empties the hand row | `ff8a9dd` (#14) |
| N56 | Confirm and Send are null-safe | `ff8a9dd` (#14) |
| N55 | A missing spawn point or a start exception aborts cleanly | `ff8a9dd` (#14) |
| N58 | The package reader and handler bound the target count | `ff8a9dd` (#14) |
| N32 | The unused owner lookup is gone | `ff8a9dd` (#14) |
| N33 | Dead code, two prefabs and the stale scene override removed | `ff8a9dd` (#14) |
| N34 | Release logs quiet | `ff8a9dd` (#14) |
| N35 | HP and lives written only on change | `ff8a9dd` (#14) |
| Font question | P3 = a: keep the committed font version | #14 |
| Q1 (#13) | Moot: Codex measured the team-1 block through the shared camera | #13 |
| N50 | The preview resolves the HUD canvas and dock when it opens, so it works on a client | `6d89a09` (#12) |
| N52 | Hover validation follows the HUD row | `6d89a09` (#12) |
| N47 | The docked, readable big card | `6d89a09`, UI6 (#12) |
| N48 | HUD labels at least 30 units | `6d89a09`, UI6 (#12) |
| N49 | Windowed fits the work area | `6d89a09`, UI6 (#12) |
| N51, N53 | Accepted (W1 = a, W2 = a) | #12 |
| U15, U16 | Answered: the docked big card and the hand hover are fine | #12 |
| C1 | Debug HP delta only in Editor/development builds with debug on | `61323d5` (#2) |
| C2 | The movement target comes from `ServerRpcParams` | `cebd2fd` (#2) |
| C3 | Server-side `CanPlayerMove` | `fb0edb9` (#2) |
| C4 | Selection confirm validates card IDs | `44d858f` (#2) |
| C5 | Editor-only `using`s removed | `15b439b` (#2, #5) |
| C6 | Not reproducible: the cast button works | #5 |
| H1 | `RunGuarded` turns exceptions into `AbortMatch` | `e776caf` (#3) |
| H2 | New deck without `UltraRare`/`Unique` after deleting the graveyard | `e7d1b03` (#6) |
| H3 | Block heal uses the block maximum | `e7d1b03` (#6) |
| H4 | Owner null check first in `CheckActivity` | `f08e854` (#4) |
| H5 | End-of-match flow and `ResultScene` | `ad3a46f`, `d44b882` (#4) |
| H6 | Forfeit when a player disconnects | `d44b882` (#4) |
| H7 | Single, server-guarded disconnect cleanup | `57d3467` (#3) |
| H8 | `CardData.OnValidate` no longer wipes activities | `f08e854` (#4) |
| H9 | `UltraRare` deck fill fixed | `f08e854` (#4) |
| H10 | Result screen menu | `ade4aed` (#5) |
| M1 | Pause and resume restores the timer after a respawn | `fe2d4fb` (#6) |
| M2 | Withdrawn (false positive) | #2 |
| M3 | Lava guard keyed by instance | `a0df515` (#7) |
| M4 | `GameMaster` input gates use `IsServer` | `a0df515` (#7) |
| M5 | Fixed by C3 | #2 |
| M6 | `UpdatePlayerHP` null checks | `a0df515` (#7) |
| M7 | Fixed by H4 | #4 |
| M8 | `AnyPlayer` through a block resolves the owner by team on clients | `a0df515` (#7) |
| M9 | `DrawCard(null)` guard | `e7d1b03` (#6) |
| M10 | Async lobby and network errors caught, with state rollback | `a0df515` (#7) |
| M11 | Parked: join-by-code commented out (you) | `b7e7b91` (#7) |
| M12 | Language selection in `SettingsScene` | `b7e7b91` (#7) |
| M13 | Neutral lobby data; localized counts and fallbacks | `a0df515`, `b7e7b91` (#7) |
| M14 | Client card-data null guards; hand rebuild when data arrives | `a0df515` (#7) |
| M15 | Hand limit 10, oldest uncommitted cards discarded | `b7e7b91` (#7) |
| M16 | `SessionManager.Instance` cleared in `OnDestroy` | `b7e7b91` (#7) |
| N1 | `PlayerUI.OnDestroy` overrides NGO's; CS0114 gone | `b7e7b91` (#7) |
| N2 | Session values written in the server's `OnNetworkSpawn` | `391e079` (#8) |
| N3 | Replicated input lock for client-side gates | `391e079` (#8) |
| N4 | Dead `ValidateCardOwner` and `m_cardResolveDelay` removed | `efb88c1` (#8) |
| N5 | Log string interpolated | `efb88c1` (#8) |
| N6 | Finish checked after each resolved card | `391e079` (#8) |
| N7 | `GetCharacter` uses `TryGetComponent` | `391e079` (#8) |
| N8 | Architecture docs and README refreshed | `05cb219` (#8) |
| N9 | Superseded by MANA2 | #5 |
| N10, N11, N13 | Closed with H5 | #4 |
| N12 | Withdrawn: false positive | review 2026-10-20 (#8) |
| N14 | Host returns to the lobby when a match can't start | `fe2d4fb` (#6) |
| N15 | Number not used | #4 |
| N16 | The leaver's suspicious summary is logged on disconnect | `efb88c1` (#8) |
| N17 | Host HUD kept when the opponent forfeits | `05cb219` (#8) |
| N18 | Deaths handled in every phase | `fe2d4fb` (#6) |
| N19 | Leave button with confirmation, counts as a defeat | `fe2d4fb` (#6) |
| N20 | Safe Send; target rule shared with the server | `a0df515` (#7) |
| N21 | Confirm carries the intended phase | `391e079` (#8) |
| N22 | Character HP set in the server's `OnNetworkSpawn` | `391e079` (#8) |
| N23 | `Keyboard.current` null check | `efb88c1`, `05cb219` (#8) |
| N24 | Three scripts converted to UTF-8 | `efb88c1` (#8) |
| N25 | Intended (you) | #5 |
| N26 | Unconfirmed selection cleared at each new selection phase | `391e079` (#8) |
| N28 | Font dynamic data cleared on build; commit rule in `AGENTS.md` | `05cb219` (#8) |
| N30 | Lobby refresh throttled | `05cb219` (#8) |
| N31 | Deck size documented as 84 | `05cb219` (#8) |
| N37 | Announcer shows both lines in full | `05cb219`, UI1, UI6 (#9) |
| N38 | Line-2 prefixes have ": " | `05cb219`, UI1 (#9) |
| N39 | All six scenes Scale With Screen Size / Expand | `05cb219`, UI2 (#9) |
| N40 | HUD anchored to the screen edges and corners | `05cb219`, UI3 (#9) |
| N41 | No non-uniform UI scale left | `05cb219`, UI1 + UI3 (#9) |
| N42 | Login background covers any aspect | `05cb219`, UI2 (#9) |
| N43 | Card preview clamped on screen | `05cb219`, UI4 (#9) |
| N44 | ru "Подтвердить" fits | `05cb219`, UI4 (#9) |
| N45 | Hand compresses to 1800 units | `05cb219`, UI3 (#9) |
| N46 | Old hand views deactivated before `Destroy` | `05cb219`, UI3 (#9) |
| L1 | Countdown from a replicated deadline | `05cb219` (#8) |
| L2 | All three deck mixes reachable | `05cb219` (#8) |
| L3 | `IsGameFinished` is pure and allocation-free | `391e079` (#8) |
| L4 | Duplicate `BlockController.TeamId` removed | `391e079` (#8) |
| L5 | `GameAnnouncerUI` maps every phase | #4 |
| L6 | Change-only announcer, world views, HUD clear, lobby panels | `05cb219` (#8) |
| L7 | Card texts refresh on a locale change | `05cb219` (#8) |
| L8 | `btn_lobbyRefresh` key without the tab | `efb88c1` (#8) |
| L9 | Debug flags forced off in release builds | `05cb219` (#8) |
| L10 | Dead code, legacy scenes, backups and VisualScripting removed | `05cb219` (#8) |
| U1 | The player build compiles and runs | review 2026-10-20, #8 |
| U3, U4 | Answered (null `NetworkBehaviourReference` safe; `DontDestroyWithOwner: 0`) | #3 |
| U5 | GUID `e651dbb3…` is NGO's own `NetworkPrefabsList` | review 2026-10-20 |
| U6 | Each Editor logs its own `GameScene` load | review 2026-10-20 |
| U7 | Normal end reaches `ResultScene` on both instances | #4 |
| U8 | `Shutdown()` leaves no objects behind | review 2026-10-20 |
| U10 | The forfeit works at runtime | review 2026-10-20 |
| U11 | Not reproduced | #7 |
| U12 | Mana replicates to a remote client | review 2026-10-20 |
| U13 | A second lobby and match work after result → main menu | #6 |
| U14 | At 1024×768 the whole board is visible | #10 |

**Tasks completed without an issue ID:**
- MANA1–MANA3 (`ade4aed`, `5eec046`, `058fa63`)
- SEC1 (`f08e854`)
- HYG1 (`efb88c1`)
- UI5 (`72a724c`)
- UI6 (`6d89a09`)
- UI7 (`314498a`)

---

## 10. Folder clean-up

**Deleted this round** (all committed and unchanged, so git keeps them; your Q1 = a and Q2 = a of poll #14, and its Q4 = b):

| File | Why |
|---|---|
| `ManualChecks/2026-10-24_ManualChecks-Report15.xlsx` | Reviewed here (§1). I used it as the template for this round's checklist first. |
| `Polls/2026-10-24_Poll-Report14.xlsx` | Reviewed in report #15 |
| `Polls/2026-10-24_Poll-Report15.xlsx` | Reviewed here (§4); template for the new poll |
| `Reports/2026-10-24_IssueReport-14.md`, `Reports/2026-10-24_IssueReport-15.md` | This report carries the full register |
| `Reports/2026-10-24_N59_N61_F1_F4_F6_N36_UI7_Implementation.md` | Codex's report for work accepted by checklist #15 |
| `Tasks/TASK-N59_N61_F1_PlayerMessages.md`, `TASK-F4_F6_N36_GameLoopRules.md`, `TASK-UI7_MenuLowProfile.md` | Done and accepted |
| `Guides/GUIDE-N27_PonyManeInBlender.md` | Its job (the mane) is done |
| `Research/RESEARCH-UIScaling_Visibility.md` | Its last task, UI7, is accepted |

**Kept on purpose:**

| Kept | Until |
|---|---|
| `Tasks/TASK-N27_ManeColours.md`, `Tasks/TASK-F9_F14a_N62_HudPolish.md`, `Reports/2026-10-24_F9_F14a_N62_Implementation.md` | Checklist #16 passes |
| `Research/RESEARCH-H6C_ReconnectMidMatch.md` | F8 (Later) |
| `Architecture/*.md` | Always (living docs) |

**`build/` is still yours** (git-ignored, no undo). Everything except this round's two folders is accepted work now. In PowerShell, in the project folder:

```powershell
# Keeps only this round's two folders (until checklist #16 passes). Check the list with -WhatIf first.
$keep = 'n27-verification','f9-f14a-n62-verification'
Get-ChildItem build | Where-Object { $keep -notcontains $_.Name } | Remove-Item -Recurse -Force -WhatIf
```

Remove `-WhatIf` to delete for real.

---

## 11. Next steps

1. **Run the checklist** ([2026-10-24_ManualChecks-Report16.xlsx](../ManualChecks/2026-10-24_ManualChecks-Report16.xlsx)): two Editors (the clone with `-logFile`), then a new build.
2. **Before committing:** restore the font, and check that `PonyBase.prefab` isn't modified.
3. **Commit** this round's docs: this report, the two specs, the checklist, the poll, the three Architecture files, and the deletions.
4. **Tell me** when the checklist is filled. I review it, close N27, N62, F9 and F14a, and remove their specs and Codex's report.
5. **Codex:** TASK-F15_F16_N64, then TASK-N63_ManePalette (or the other way round), each with its own report.
6. **The poll:** any time. Q2 may add a small clean-up step to TASK-N63_ManePalette before Codex runs it.
7. **Your card list for F5,** when it's ready next week.
8. **`build/`:** §10's command whenever you like.

## Sources

- **Your files:**
  - `ManualChecks/2026-10-24_ManualChecks-Report15.xlsx`: every sheet and cell, including *Read me* L–N and the *Known issues* "Found during the checks" table (empty)
  - `Polls/2026-10-24_Poll-Report15.xlsx`: *Poll* K5:L10, *Priorities* rows 5–27
- **Logs:** `%LOCALAPPDATA%\Unity\Editor\Editor-prev.log` (the clone, until 17:56: lines 3839, 6215, 6660, 6785, 19319, 19333), `Editor.log` (the clone, until 18:33), `C:\Lesson\clone-editor.log` (until 17:15: lines 1271–1376)
- **Codex:**
  - `Docs/AI/Reports/2026-10-24_F9_F14a_N62_Implementation.md`
  - `build/n27-verification/`: `baseline-results.txt`, `setup-results.txt`, `final-results.txt`, `final-warnings.txt`, `side.png`, `match-pair.png`
  - `build/f9-f14a-n62-verification/`: `final-results.txt`, `build-results.txt`, `release-assembly-check.txt`, `ui6-comparison.txt`, `HUD-ru-1280x720.png`
- **Diff** `314498a..5abd155`: all 25 changed `Assets` files and the three `Architecture/*.md` files
- **Code read for the findings and the new tasks:** `MultiplayerManager.cs` (Disconnect, `ShutdownHostInternal`, `LeaveMatchToResultScene`, `FindAvailableLobbies`, the refresh chain, `CleanupAfterDisconnect`), `CurrentLobbyUI.cs`, `GameMaster.cs`, `GameMaster.Card.cs`, `GameMaster.PackageManaging.cs`, `PlayerSession.cs`, `PlayerUI.cs`, `PlayerUIPrefab.prefab`, `UI_table` (all four assets), NGO 2.11.2's `NetworkSceneManager.cs` / `NetworkManager.cs`, the Lobby SDK's `LobbyExceptionReason.cs`
- **Structure pass:** `Assets/Scripts/**` (line counts), the GUID scan over `Assets/**` (scenes, prefabs, materials, assets), `Packages/manifest.json`
