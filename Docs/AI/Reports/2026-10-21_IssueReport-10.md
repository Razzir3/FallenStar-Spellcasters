# Issue Report #10: FallenStar – Spellcasters (owner's checks for #8–#9, and the UI5 review)

- **Date:** 2026-10-21
- **Baseline:** `HEAD` = `72a724c` ("Ui5"). The working tree is clean.
  - `05cb219` "UI1-4 completed" commits the whole working tree that report #9 reviewed: report #8's tasks, UI1–UI4 and the docs.
  - `72a724c` "Ui5" is Codex's TASK-UI5. It was committed before any review, so §2 is that review.
- **Previous reports:** [#1](2026-10-18_IssueReport.md), [#2](2026-10-18_IssueReport-2.md), [#3](2026-10-18_IssueReport-3.md), [#4](2026-10-18_IssueReport-4.md), [#5](2026-10-19_IssueReport-5.md), [#6](2026-10-19_IssueReport-6.md), [#7](2026-10-20_IssueReport-7.md), [#8](2026-10-20_IssueReport-8.md), [#9](2026-10-21_IssueReport-9.md)
- **Method:**
  1. Read your filled-in [checklist](../ManualChecks/2026-10-21_ManualChecks-Report8-9.xlsx): 58 rows, the Temp changes sheet and the notes.
  2. Matched the rows against the evidence:
     - **`Editor.log`**, with the clone's header (started 21:12):
       - Lines 1–14037 are your session (21:12–21:55).
       - The rest is Codex's UI5 work (22:05–22:21).
       - Both Editors write to this file, so it's a patchwork.
     - **`Editor-prev.log`** (the clone, 13:57–19:39) holds only the match already counted in report #9.
     - **`Player.log` and `Player-prev.log`** were both written by Codex's UI5 test player (22:14–22:20).
     - **Every player build on the disk**, with the write and access times of its files
     - **The two Temp-change files (T1, T2)** and the font asset, with their write times
     - **The build's registry keys**
  3. Measured the B4.3 failure:
     - the GameScene CanvasScaler
     - the text rects and autosize ranges in `CardViewUIPrefab` and `PlayerUIPrefab`
     - Codex's measured label sizes (UI4)
     - Codex's 1280×800 screenshot `Hand-10-PlayMode.png`
  4. Reviewed UI5:
     - compared the spec with `git show 72a724c`: the script, the scene YAML, the localization and the docs
     - read Codex's evidence in `build/ui5-verification/`
  5. Re-located every open issue at `72a724c`.
- **I didn't run a build or Play Mode.** I changed no code, scenes, prefabs or settings. My only changes:
  - this report
  - one corrected line in `GameLoop.md` (§2)
  - the five UI specs removed from `Docs/AI/Tasks/` (§8)
  - the new spec [TASK-UI6](../Tasks/TASK-UI6_ReadabilityProfiles.md)
- **Updated the same day with your answers:** the D and C sessions ran on a second machine (§1.3), and your decisions V1–V3 and N49 (§7).

**This report is the full issue list.** §4 lists every open issue and §6 every closed one. The new IDs are **N47–N49**. The next new finding would be **N50**.

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
| Your checklist | **57 Pass, 1 Fail.** The Fail is B4.3 at 1024×768: "Cant read buttons/cards text". Tested by "Owner" on 2026-10-21. The Applied and Reverted columns are empty again. |
| Backed by the logs | A1, A3, A5, A6, A7, A10, B1.4 and C1 (§1.2) |
| No trace in the logs | The visual rows and A9.2's five lobby cycles. I accept them as you reported them. |
| **Run on a second machine** | **D1–D7** (all 8 build rows) and **C2.1, C3.1–C3.3**. You ran them on another machine, to test the game on another architecture. There's no trace of them here, and I accept them as you reported them. My first reading, "contradicted by the files", was wrong (§1.3). |
| The B4.3 failure | **Confirmed, and wider than 1024×768:**<br>• **N47:** card text<br>• **N48:** HUD button labels<br>Both 🟡 Medium (§3). |
| UI5 review | **Matches the spec. No blocking defect.** One plausible residual, **N49** 🟢: the largest windowed size ignores the taskbar (§2). |
| Compile and build (Codex, UI5) | 0 errors, 0 warnings. The release player and the test player both built. |
| 🔴 / 🟠 / 🟡 open | **0 / 0 / 2:** N47 and N48 |
| 🟢 Low open | **6:** N32, N33, N34, N35, N49, and N27 (parked) |
| ⚪ Info open | N36, N29 (parked), the naming list |
| Unknowns open | **4:** U2, U9, and the new **U15** and **U16**, the possible issues of the large card view (§4). **U14 is closed:** your B7.1 shows the board fits at 4:3. |
| Your decisions (2026-10-21) | • V1: two profiles, low and medium/high<br>• V2: as recommended, with the alternatives tracked as U15 and U16<br>• V3: as recommended<br>• N49: included<br>**[TASK-UI6](../Tasks/TASK-UI6_ReadabilityProfiles.md) is written** (§7). |

**Overall:** report #8's tasks and UI1–UI4 hold up. Here, two matches in two Editors reached the Result screen without a game-code exception, and the build rows passed on your second machine. Two things are still open:
- **UI5 hasn't run in any player you tested.** Your D session ran before UI5 was committed, so UI5's checks still need a build (§5).
- **Text is too small to read on small screens.** My UI research checked that everything **fits**. It never checked that the text is **big enough to read**. Those are N47 and N48.

---

## 1. Your checklist

### 1.1 Result

- **57 Pass, 1 Fail.**
  - **B4.3** (1024×768, card preview): "Cant read buttons/cards text".
  - The row's own expectation was "the preview is fully on screen", and your note doesn't dispute it. So **N43 stays closed**.
  - The readability problem is new: **N47** for the cards and **N48** for the buttons (§3).
- **Temp changes sheet:** Applied and Reverted are empty. T1 and T2 were applied on your second machine, not here (§1.3).
- **Everything is clean now:**
  - `GameMasterPrefab.prefab` has no `m_startHandCards` override.
  - `PlayerSession.cs:297-298` are active.
  - The font asset is in its cleared state, byte-identical to the UI1–UI4 baseline.
  - The build's saved language is `en`.

### 1.2 Evidence per group

✅ backed by the logs · ➖ no trace here

**Group A: two Editors, normal settings**

| Group | Evidence | Verdict |
|---|---|---|
| A1 (N2, N22, N3) | Two match starts (lobbies `NHTGD9` and `TNWGGC`). There are 0 "NetworkVariable is written to …" warnings in the whole log, and no exception from game code. | ✅ A1.1, A1.5<br>➖ A1.2–A1.4 (UI only) |
| A2 (L1) | The countdown leaves no log line. Respawns did happen: a LivesDepleted result needs three deaths. | ➖ |
| A3 (N26) | 9 right-click discards ("Card Alter clicked") | ✅ partly |
| A4 (N21, optional) | 0 `[Suspicious]` lines | ✅ consistent |
| A5 (N6) | • 3 Iceshard casts<br>• `NHTGD9` ended LivesDepleted: the host's "You won" (`:4878`) and the client's "You lost" (`:8567`) | ✅ |
| A6 (N17, N19) | `TNWGGC` ended Surrendered after 9 rounds:<br>• "match ended. Winner: 0. Reason: Surrendered" (`:12797`)<br>• the host's "You won" (`:14024`) and the client's "You lost" (`:7407`) | ✅<br>➖ the HUD itself |
| A7 (L10 heal) | 3 HealingHooves casts (`SendCardDataClientRpc … id_healingHooves`) | ✅ cast<br>➖ HP values |
| A8 (L7) | 0 `MissingReferenceException` | ✅ consistent |
| A9 (N30, L8) | • 0 `TryCatchRequest` NullReferenceException.<br>• The 8 `TryCatchRequest` lines in the log are stack frames of **successful** create and query calls, listed under ordinary `Debug.Log` lines (e.g. `:958`, `:4946`).<br>• Only 2 lobbies are visible, not 5 cycles. | ✅ the NRE<br>➖ the 5 cycles |
| A10 (L4) | 0 "Key: Team Id" | ✅ consistent |
| A11 (optional, Profiler) | Nothing is logged | ➖ |

**Groups B–D**

| Group | Evidence | Verdict |
|---|---|---|
| B1–B7 | Visual rows. For B1.4: 0 "Announcer text truncated". | ➖<br>✅ B1.4 |
| C1 (10-card hand) | • From round 6 of `TNWGGC`, every deal logged "Hand limit 10: discarded 1 oldest card(s)" for both clients: 16 lines, `:11338`–`:12702`.<br>• So both hands held 10 cards for 4 rounds.<br>• You reached 10 cards by not playing, not with T1. That's fine for C1. | ✅ |
| C2.1, C3.1–C3.3 | Run on your second machine (§1.3) | ➖ |
| D1–D7 | Run on your second machine (§1.3) | ➖ |

**Other lines in your session (all expected):**
- 20 N27 mane warnings
- 192 "DespawnBlock" lines (N34)
- 3 font importer warnings (N28 context)

**The only exceptions in the whole `Editor.log`** (none from game code):
- one in Unity's Localization Game-view toolbar at Editor start-up (`GameViewLanguageMenu.cs:119`: Unity's package, Editor only)
- one NullReferenceException in Codex's temporary UI5 harness (`UI5Verification.cs:156`), which Codex fixed before the final run
- one file-sharing IOException in that same harness

### 1.3 The build rows and the Temp-change rows ran on a second machine

**A correction to my first version of this report.** I had marked D1–D7, C2.1 and C3.1–C3.3 as "contradicted by the files", because this machine has no trace of them:
- the build folder `C:\Lesson\Spellcasters.Build\` holds the build from 2026-10-20
- no player started here on 2026-10-21 before Codex's UI5 builds at 22:12
- T1 and T2 were never applied here: `GameMasterPrefab.prefab` and `PlayerSession.cs` were last written on 2026-10-20

You ran those sessions **on another machine, to test the game on another architecture**. That explains all three facts. So I accept the 12 rows as you reported them: ➖, no trace here.

**What follows:**
- **D2.1 has now passed in a build:** the language survives a restart. That closes the old D.5 check, and `GameLoop.md` now says so (§2).
- **Your D session ran before UI5 was committed** (22:28), so UI5 still needs its own player check (§5 A).
- **For next time:** a note such as "PC 2" in the notes column tells me where to look.

### 1.4 Why ➖ isn't a Fail

The two Editors overwrite each other's lines in the shared `Editor.log`. Only 2 of your matches can be seen there, so a missing line proves nothing, and only positive lines count.

For a complete record, start the clone with its own log: `-logFile C:\Lesson\clone-editor.log` (see the README's "Testing with two players"). Another option is to paste key Console lines into the notes column.

---

## 2. UI5 review (commit `72a724c`)

| Part | Spec | What was implemented | Verdict |
|---|---|---|---|
| `DisplaySettingsUI` | §4.1 | **Start-up and listeners:**<br>• `IEnumerator Start` waits for Localization, fills both dropdowns without notify, then adds the listeners.<br>• `OnEnable` / `OnDisable` pair the locale event and the listeners.<br>**Outside changes:** `Update` compares width, height and mode, then refreshes without notify. That path never calls `SetResolution`.<br>**Language:** a locale change relabels the modes and keeps the selection.<br>**Modes:**<br>• Only the 3-argument `SetResolution` is used.<br>• Exclusive fullscreen shows as Fullscreen.<br>• Fullscreen → native size.<br>• Windowed → the current size if it fits strictly, otherwise the largest that does.<br>**`BuildResolutionList`:** pure and static. It removes duplicates, filters, inserts the current size and sorts by width, then height, largest first. | ✅ |
| Codex's spec note | — | Because the current size is always in the list, the spec's rule "keep the current size if it's in the windowed list" is always true. Codex checks the strict fit instead (`:171-182`). | ✅ The right reading. My spec's wording was loose. |
| Scene | §4.2 | **Exactly as specified:**<br>• **Layout:** panel 600×400, title at (0, 350). Labels are 240×46 at (−150, 280 / 220 / 160); dropdowns are 260×38 at (140, 280 / 220 / 160). Back is unchanged.<br>• **Templates:** the Resolution template is 300 tall, the others 150.<br>• **Wiring:** `DisplaySettingsUI` sits on `SettingsCanvas` and is wired to both dropdowns (`SettingsScene.unity:4207-4208`). Each new label's `LocalizeStringEvent` targets its own text. | ✅ |
| Localization | §4.3 | 4 keys with Unity-generated IDs and no trailing whitespace. The en, ja and ru values match the spec's table. | ✅ |
| Non-goals | §5 | No change to `ProjectSettings`, the CanvasScalers, `SettingsUI`, `Bootstrap` or other scenes. No PlayerPrefs keys of its own, no debug flag. | ✅ |
| Codex's evidence | §7–8 | **Harness and Unity checks:**<br>• harness: the list rules<br>• Edit Mode: the layout at 9 sizes × 3 locales<br>• Play Mode: the popups fit<br>• release build: 0 errors, 0 warnings<br>**The test player's log (`player-observations.txt`) shows:**<br>• fullscreen 1920×1080, then windowed 1366×768<br>• quit and relaunch restoring windowed 1366×768<br>• the command-line override and Alt+Enter<br>• relabelling ru → ja → en<br>It ends back at 2560×1600 fullscreen, and the registry confirms that. | ✅ |
| Docs | — | `Architecture.md`, `Dependencies.md` and `GameLoop.md` updated. Accurate. | ✅ |

**Evidence notes:**
- **Codex's interactive tests used a Settings-first test player,** without `Bootstrap` or Login.
  - The normal route (Login → Main menu → Settings → Back) hasn't run in a player yet.
  - In that test player, the Language dropdown showed "English" while the UI was Japanese. The reason: `SettingsUI.GetCurrentLanguageIndex` prefers the saved code (`en`), and only `Bootstrap` applies that code at launch.
  - In the normal flow `Bootstrap` runs first, so this can't happen there. **Not a defect.**
- **The resolution list on your display has 20 entries,** including old 4:3 modes (2048×1536, 1920×1440, 1856×1392, 1792×1344). That follows decision S4.
- **Corrected in `GameLoop.md` (Settings row):** it said the 2026-10-20 build "kept the saved language". That build was never restarted (D.5, review of 2026-10-20). Your D2.1 on the second machine has now shown it, so the line names that run.

**Residual N49** (🟢, Plausible): windowed sizes are checked against the full display, not against the space above the taskbar. Details in §3.

---

## 3. New findings

| ID | Level | Issue | Location | Evidence | Verdict |
|---|---|---|---|---|---|
| **N47** | 🟡 Medium | **Card text can't be read on common screens, and nothing shows a card bigger than in the hand.**<br>• **The preview is hand-card size:** it reuses `CardViewUIPrefab` unscaled, at 200×400 units.<br>• **Hand cards have no preview at all.** Only a click on a board card shows one.<br>• **The text can't grow inside the card.** Its boxes already fill the card's name and description plates; for example, the description is 106×81 units.<br>• **Sizes:** the card name is about 11–14 units and the description about 16–21. That's 4–6 px and 6–8 px at 1024×768, and 7–9 px and 11–14 px at 1920×1080 (table 3.1). | `Prefab/CardViewUIPrefab.prefab`: `DesignRoot` scale 0.39; the `CardName` / `CardDesc` rects and margins<br>`Prefab/PlayerSessionPrefab.prefab:115` (`m_cardPreviewPrefab`)<br>`InGame/CardPreviewController.cs:64-82`<br>`InGame/PlayerSession.cs:440-458`<br>`GameScene.unity:795-800` (the 2560×1600 reference) | Your B4.3 note. `Hand-10-PlayMode.png` (1280×800, scale factor 0.5): the card name is about 5 px tall, the description about 7–8 px. Unit sizes were derived from the rects and autosize ranges (±20 %). | Confirmed |
| **N48** | 🟡 Medium | **HUD button labels drop below 12 px on small screens.**<br>• **Russian Confirm** ("Подтвердить") autosizes to 15.65 units in its 100-unit button (UI4's measurement): 8 px at 1366×768 and 6 px at 1024×768. It's N44's accepted residual.<br>• **English Confirm** is 26.15 units: 10 px at 1024×768.<br>• **The Leave dialog's two buttons** (autosize 22–30) are 9–12 px at 1024×768.<br>• **The Leave button** (autosize up to 30) is at most 12 px.<br>• The Send label shares Confirm's button width. Its Russian text wasn't measured, but it's likely similar.<br>• **Fine at every size:** HP, lives and mana (about 36 units) and the announcer (48) stay at 14 px or more at 1024×768. | `Prefab/UiPrefab/PlayerUIPrefab.prefab`:<br>• `ConfirmBtn` / `SendBtn` labels (minimum 14 / 18, buttons 100 wide)<br>• the `LeaveMatch` labels (22–30)<br>`GameScene.unity:795-800` | Your B4.3 note. UI4's measured sizes (report #9 §1) and the scale factors (table 3.1). | Confirmed |
| **N49** | 🟢 Low | **The largest windowed size can sit partly behind the taskbar.**<br>• **The check:** windowed sizes are compared with the display's full size (`Screen.mainWindowDisplayInfo` width × height), not with its `workArea`.<br>• **Your screen:** the work area is 2560×1552, because of a 48 px taskbar at 100 % scaling.<br>• **What happens:** Windowed picks 2048×1536 (Codex's player log). With a title bar of about 31 px, the window is about 1567 px tall. So 15–31 px at the bottom of the game (the hand's lower margin) can be hidden, depending on where Windows places the window.<br>• **Side note:** the automatic pick is 4:3 on a 16:10 screen, because the list is sorted by width first. | `UI/DisplaySettingsUI.cs:129-139` (`GetDisplaySize`, `FitsDisplay`), `:171-182` | `player-observations.txt` (launch 2: "Windowed … 2048 × 1536"). The Windows work area was read with `System.Windows.Forms.Screen`. Codex saw the title bar but didn't mention the bottom edge. | Plausible |

### 3.1 How small the text is

The GameScene canvas uses Scale With Screen Size with a 2560×1600 reference and Expand. Every HUD size is therefore multiplied by the screen factor, `min(width / 2560, height / 1600)`. Values are font sizes in px at 1:1. A common minimum for readable UI text is about **12 px**, a little more for Japanese.

| Text | Canvas units | 2560×1600 (×1.00) | 1920×1080 (×0.675) | 1366×768 (×0.48) | 1024×768 (×0.40) |
|---|---|---|---|---|---|
| Card name (hand and preview) | ≈ 11–14 | 11–14 | **7–9** | **5–7** | **4–6** |
| Card description | ≈ 16–21 | 16–21 | **11**–14 | **8–10** | **6–8** |
| Confirm label, ru (measured) | 15.65 | 16 | **11** | **8** | **6** |
| Confirm label, en (measured) | 26.15 | 26 | 18 | 13 | **10** |
| Leave dialog buttons | 22–30 | 22–30 | 15–20 | **11**–14 | **9–12** |
| Leave button | ≤ 30 | ≤ 30 | ≤ 20 | ≤ 14 | ≤ 12 |
| HP, lives, mana | ≈ 36 | 36 | 24 | 17 | 14 |
| Announcer line 2 (measured) | 48 | 48 | 32 | 23 | 19 |

Bold marks text under 12 px.

**UI5 makes this easy to reach.** Settings now offers 1024×720 and larger, windowed too. A 1024×768 window on your own 2560×1600 screen shows exactly the sizes in the last column.

### 3.2 Why, and my part in it

1. **GameScene keeps the 2560×1600 reference, the size of your display.** The HUD was drawn in "2560×1600 pixels", so it shrinks with the screen: ×0.675 at 1080p, ×0.40 at 1024×768. UI2's Expand (D1) takes a little more at 4:3: ×0.48 becomes ×0.40.
2. **No view shows a card larger than in the hand.** The preview reuses the hand-card prefab at the same size.
3. **A few HUD labels have low autosize ranges,** or a button too narrow for Russian.

**My miss:** the research's assumption A1 said every text must be "readable", but I only checked that text **fits and isn't cut**.
- For the menus I did compute the 1080p text size (D2: 13–14 px), and that's why they moved to a 1920×1080 reference.
- For the HUD I kept 2560×1600 "because it's authored for it", without computing what that means at 1366×768 or 1024×768.

**About your Editor check:** the saved Editor layout has an 846 px-wide Game view (100 % display scaling). A 1024×768 preset was probably scaled down to fit, which makes everything look even smaller. But the table above is at 1:1, and it's already too small.

### 3.3 The fix

You decided V1–V3 and N49 (§7). The work is one task: [TASK-UI6](../Tasks/TASK-UI6_ReadabilityProfiles.md).

---

## 4. All open issues

### 🔴 Critical / 🟠 High

None open.

### 🟡 Medium (2)

| ID | Issue | Current location | Verdict |
|---|---|---|---|
| **N47** | Card text unreadable below about 1920×1080; no enlarged card view; hand cards have no preview (§3). → TASK-UI6 | `CardViewUIPrefab.prefab`; `CardPreviewController.cs:64-82`; `PlayerSession.cs:440-458`; `GameScene.unity:795-800` | Confirmed |
| **N48** | HUD button labels under 12 px at 1366×768 and 1024×768: ru Confirm 6–8 px, Leave dialog 9–12 px (§3). → TASK-UI6 | `PlayerUIPrefab.prefab` (`ConfirmBtn`, `SendBtn`, `LeaveMatch`); `GameScene.unity:795-800` | Confirmed |

### 🟢 Low (6)

| ID | Issue | Current location | Verdict |
|---|---|---|---|
| **N49** | The largest windowed size ignores the taskbar: 2048×1536 plus the title bar is taller than the 1552 px work area. The automatic pick is 4:3 on a 16:10 screen. (§3) → TASK-UI6 | `UI/DisplaySettingsUI.cs:129-139`, `:171-182` | Plausible |
| **N32** | `ProcessActivity` reads the card owner's character without a null check and never uses it. It can't throw today, because `CheckActivity` checks the same owner first. If it ever did, `RunGuarded` would abort the match. | `GameMaster.Processing.cs:139` | Confirmed (latent) |
| **N33** | Dead code and unused prefabs left after L10:<br>• `GameMaster.m_gameLoopCoroutine`, `m_phasesTimerCoroutine`, `m_cardViewWorldPrefab`<br>• `PlayerController.m_isDead` (a NetworkVariable that is never written), `GetCurrentSpeed()`<br>• `PlayerSession.ClearHand()`, `ClearBlocks()`, `RemoveCard()`<br>• `NetworkCard.SetFaceUpServerRpc`, `SetRotatedServerRpc`, `DespawnCardServerRpc`<br>• `CardPreviewController.TogglePreview`, `MultiplayerManager.KnowLobbies`, `SceneFlowManager.LoadLoginScene`, `GameSettingsPrefs.HasLanguageCode`<br>• `BootsTrap.prefab` and `MultiplayerManager.prefab` (the second has `m_debugMode: 1`)<br>• the stale `m_timeText` override on the GameScene HUD instance<br>**Owner's call:** `AuthManager.SingOut()` and `SingOutAndClearCredentials()` have no caller, and the second one's branch order looks inverted. | `GameMaster.cs:56`, `:104-105`<br>`PlayerController.cs:21`, `:118`<br>`PlayerSession.cs:374`, `:583`, `:748`<br>`NetworkCard.cs:73-86`<br>`CardPreviewController.cs:44`<br>`MultiplayerManager.cs:65`<br>`SceneFlowManager.cs:45`<br>`GameSettingsPrefs.cs:20`<br>`AuthManager.cs:116-133`<br>`Assets/Prefab/BootsTrap.prefab`, `Assets/Prefab/MultiplayerManager.prefab`<br>`GameScene.unity` (PrefabInstance `282301674`, target `6674156206700678496`) | Confirmed. The `SingOutAndClearCredentials` branch order is Plausible. |
| **N34** | About 30 log lines aren't behind any debug flag, so they print in release builds. Examples:<br>• "Process called", "Rpc called", "RepairCalled", "DespawnBlock"<br>• "Card clicked" / "Card deselected"<br>• the "Applied … material index" lines<br>• "DrawPhase started/ended"<br>• 9 lines per lobby search<br>• "SetLobbyCode" / "Lobby code"<br>• "Tried ShowPrew" ×2<br>Also:<br>• `PlayerUI.OnCardClicked` / `OnCardAlterClicked` read `cardView.name` before their null check (latent).<br>• One debug message is missing `$`.<br>**TASK-UI6 removes the two "Tried ShowPrew" logs,** because hovering would print them constantly. | `GameMaster.Processing.cs:115`, `:138`<br>`PlayerSession.cs:229`<br>`NetworkCard.cs:164`<br>`BlockController.cs:38`<br>`CardViewUI.cs:97`, `:103`<br>`PlayerUI.cs:395`, `:412`, `:431`, `:448`<br>`PonyColorScript.cs:63`, `:83`<br>`GameMaster.GameLoop.cs:485`, `:520`<br>`MultiplayerManager.cs:765-834`, `:1541-1542`<br>`CardPreviewController.cs:58`, `:63`<br>`GameMaster.PackageManaging.cs:258` | Confirmed (192 "DespawnBlock" lines in today's session) |
| **N35** | `PlayerUI` formats HP and lives every frame, as two Smart-format strings, even when nothing changed | `UI/PlayerUI.cs:173-180` | Confirmed |
| N27 | The pony mane color is never applied, because `maneObject` isn't assigned. Every spawn logs "Invalid mane material index." **Parked by the owner** until the mane model exists. | `Assets/Prefab/BoardPrefab/PonyBase.prefab:368`; `InGame/PonyColorScript.cs:70-88` | Confirmed (20 warnings today) |

### ⚪ Info

- **N36:** the countdown and the server loop use different clocks. After a server hitch, or during drawing, clients can briefly see "00". Plausible and cosmetic. (`GameMaster.cs:124-141`, and the phase loops in `GameMaster.GameLoop.cs`)
- **N29:** the lobby shows the player ID twice. **Parked by the owner.** (`UI/LobbyPlayerItemUI.cs:53-65`)
- **Naming and typos** (report only, don't rename):
  - `CardControllPackage`, `PlayerCardfWorldArea.cs`, `SetOponent` / `m_currentOponentNetworkObjectId`, `CheckActiveTaergetSelection`
  - `SingInAnonymous` / `SingOut` / `SingOutAndClearCredentials` / `m_isSingingIn`, `HearthbeatLobby`, `MovePlayeToSpawnPoint`, `ClearupAfterDisconnect`
  - `CardSpecialization.DeffenceSpell`, `KnowLobbies` / `m_knowLobbies`, `m_bootsTrap`, `_teamAssing`, `m_phaseAnouncementText`, `#region Valiable`
  - the GameObject name `PhaseAnouncer`
  - the log texts `"Packege"`, `"Handle unseccess"`, `"Tried ShowPrew"`; the local variable `_locakPoint`
  - `ConfirmSendButtonUI1` (in `ConfirmSendButton.cs`); `m_startHandCards`, which is really the per-round draw count

### Feature gaps (not defects, listed so they aren't mistaken for bugs)

- **Settings:** language and display (UI5) exist. Audio, graphics quality, controls and a UI-scale setting don't.
- **Join by lobby code is parked** (M11, owner, 2026-10-19). `m_lobbyCode` / `m_lobbyCodePanel` stay serialized for it.
- Steam login is a stub (`AuthManager.SignInSteam`), although `LoginScene` has a button wired to it.
- `GamePhase.DrawCards` and `RoundEnd` are never set, and `ResolveCards` is only transient. During draws the announcer shows "Round break".
- The `DiscardCard` / combo actions, the `All*` / `Random*` targets and `ActivityEffect` have no effect.
- Destroying all enemy blocks doesn't win by itself. A player with no blocks is eliminated on their next death.
- `SessionRole.Spectator` exists, but nothing assigns it.
- No player-facing messages for: lobby or network failures, an ignored Send, an ignored late confirm, or a deck refill.
- The confirmed target of an action card doesn't survive a respawn pause (by design). Card selections do survive it.
- `SessionManager` has 4 slots, but the scene supports only teams 0 and 1.
- With 10 cards, the hand overlaps by about 22 units. The rightmost card is drawn on top and receives clicks in the overlap (UI3, intended).

### ❓ Unknowns: open

| ID | Question | Status |
|---|---|---|
| U2 | The nested `SpawnPoint` NetworkObject inside `BlockBody` | ❓ No NGO warning in any log so far, today's included |
| U9 | NGO `NetworkSceneManager.Dispose` NRE on Play Mode stop, Editor quit or domain reload | ❓ Not seen today |
| **U15** | **Possible issue of UI6 (your V2 alternative):** does the docked large card view cover something the player needs? For example, the left side of the board, the left end of the banner on 16:10, or the leftmost hand cards in the low profile. | ❓ Check after UI6 (TASK-UI6 §9.9). If it does, the fallback is **V2b**: the large view at the mouse, with UI4's clamp. |
| **U16** | **Possible issue of UI6 (your V2 alternative):** does the hover view on hand cards get in the way during Card Selection (flicker, distraction, covering)? | ❓ Check after UI6 (TASK-UI6 §9.9). If it does, the fallback is **V2c** (board cards only) or a short hover delay (about 0.3 s). |

---

## 5. Pending manual checks

**Done since report #9:** all of report #9's §4, on this machine and on your second machine. That includes D1–D7, C2.1, C3 and D.5 (as D2.1). Only B4.3 failed: N47 and N48.

Both instances must run the **same** build. Make the build after Codex has finished, because Codex's test players write the same `Player.log`.

**A. UI5 in a player build** (from the UI5 spec §9; the spec is now in git history, §8). Your D session ran before UI5 existed.
1. Settings → **Windowed** gives a window with a title bar, at the largest size that fits. Then pick 1366×768: the window shrinks, and nothing is cut.
2. **Fullscreen** fills the display at its native size. Then pick 1920×1080. Expected: black bars at the top and bottom, no stretching, and clicks that land where the buttons are drawn.
3. **Alt+Enter** on the Settings screen: the Display-mode dropdown follows.
4. **Language:** switch to ru, then ja. The new labels and the mode options follow, and the Language dropdown shows the right language.
5. **Restart:**
   - Quit and relaunch: the same mode and size come back.
   - Then launch with `-screen-fullscreen 0 -screen-width 1366 -screen-height 768`: Settings shows Windowed, 1366 × 768.
   - Afterwards, set your normal mode again.
6. **The normal route:** Login → Main menu → Settings → Back. Codex didn't run it in a player.
7. **A windowed match at 1024×768:**
   - The HUD follows the corners.
   - Alt-tab away for about 10 s. The match keeps running for the other player (`RunInBackground`).

**B. After UI6** ([TASK-UI6](../Tasks/TASK-UI6_ReadabilityProfiles.md) §9):
- B4.3 again
- the large card view: hover and click
- the low sizes (640×480 … 1360×768) and the medium/high sizes, in en, ru and ja
- the profile switch at 1360 ↔ 1366
- N49 in a player build
- your notes on U15 and U16

**The checklist workbook:** I'll write one checklist after UI6 is reviewed. It will cover A and B in one session, because both need a player build.

---

## 6. Closed issues

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
| N25 | Intended (owner, 2026-10-19) | #5 |
| N26 | Unconfirmed selection cleared at each new selection phase | `391e079` (#8) |
| N28 | Font dynamic data cleared on build; commit rule in `AGENTS.md` | `05cb219` (#8) |
| N30 | Lobby refresh throttled | `05cb219` (#8) |
| N31 | Deck size documented as 84 (with L2) | `05cb219` (#8) |
| N37 | Announcer shows both lines in full: unscaled 1560×150 box, font 24–48 | `05cb219`, UI1 (#9) |
| N38 | Line-2 prefixes have a ": " separator; shorter en/ru respawn prefix | `05cb219`, UI1 (#9) |
| N39 | All six scenes Scale With Screen Size / Expand; nothing cut at 9 resolutions | `05cb219`, UI2 (#9) |
| N40 | HUD anchored to the screen edges and corners | `05cb219`, UI3 (#9) |
| N41 | No non-uniform UI scale left; the per-frame `LeaveMatchUI` resize removed | `05cb219`, UI1 + UI3 (#9) |
| N42 | Login background covers any aspect (Envelope Parent) | `05cb219`, UI2 (#9) |
| N43 | Card preview clamped on screen; always on the HUD canvas. Readability is a new item, N47. | `05cb219`, UI4 (#9) |
| N44 | ru "Подтвердить" fits the Confirm button. Its small size on small screens is now N48. | `05cb219`, UI4 (#9) |
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
| **U14** | **Answered: at 1024×768 (4:3) the whole board and both player areas are visible** (your B7.1, Editor Game view) | **#10** |

Tasks completed without an issue ID:
- MANA1–MANA3 (`ade4aed`, `5eec046`, `058fa63`)
- SEC1 (`f08e854`)
- HYG1 (`efb88c1`)
- **UI5**, display settings (`72a724c`, reviewed in §2)

---

## 7. Your decisions, and the next steps

**Your decisions (2026-10-21), as applied in [TASK-UI6](../Tasks/TASK-UI6_ReadabilityProfiles.md):**

| # | Your decision | How TASK-UI6 applies it |
|---|---|---|
| V1 | "Separated settings for low resolutions (like 640x480 up to 1024x768) and medium with high (1366 and better)" | **Two profiles, chosen automatically by the screen width:**<br>• **Low,** below 1366: a 1600×1000 canvas reference (the HUD is 1.6× larger on screen) and a compact layout. Leave moves to the bottom-left, the banner gets the full top width, and hand cards shrink to 0.625 (they're read in the large view).<br>• **Medium/high,** 1366 and up: today's layout.<br>In both, every HUD text is at least 30 canvas units: ≥ 12 px at 640×480, ≥ 19 px at 1024×768, ≥ 14 px at 1366×768, ≥ 20 px at 1920×1080.<br>The sizes between 1024 and 1366 wide (1280×720, 1280×1024, 1360×768, …) count as low (TASK-UI6 §10, A1). |
| V2 | The recommended view; the alternatives as possible issues | **One large card view, docked at the left side.** It shows on a board-card click (as now) and on hover over a hand card, and it blocks no clicks.<br>• **Medium/high:** about 61 % of the screen height on 16:10, between the hand and the Leave button.<br>• **Low:** about 71 %.<br>The alternatives are tracked as **U15** (at the mouse) and **U16** (board cards only, or a hover delay). |
| V3 | As recommended | Confirm, Send and Confirm turn are stacked in one 300-unit column, bottom-right, all with the wide plank sprite. The labels are 30–44 units, and the Leave labels 30–40. |
| N49 | Yes | Windowed sizes fit the work area minus 16 / 64 px. The automatic pick prefers the display's aspect ratio: 1920×1200 on your screen. |

**The next steps:**
1. **Codex implements TASK-UI6.** Then I review it and write one checklist for §5 A and B.
2. **UI7, after UI6's review:**
   - the low profile for the five menu scenes, with the Lobby needing a compact layout
   - Settings offering sizes down to 640×480 (`DisplaySettingsUI` minimum)

   It reuses UI6's `ResolutionProfileLayout`, so I'll write it once that component has been reviewed. Until then, 640×480 can be reached through the Game view or the command line.
3. **One small hygiene spec (all Low), on your go-ahead:**
   - N32: delete the unused line.
   - N33: delete the dead code, the two old prefabs and the stale override. Your call on the sign-out methods.
   - N34: gate the logs, and fix the null-check order and the missing `$`. UI6 already removes the two "Tried ShowPrew" logs.
   - N35: change-only HP and lives.
4. **Parked by you:** N27 (until the mane model exists) and N29 ("later").
5. **Optional:** N36, only if "00" is noticed in play.

---

## 8. Task folder

All five UI specs are implemented, match their specs, have no open blocking defect, and are **committed in `05cb219`**. Report #9 kept UI1–UI4 only because they weren't in git yet. I've removed them from the working tree now, without committing; you can commit the removal with the next commit. They can still be read with `git show 05cb219:Docs/AI/Tasks/<file>`.

| File | Why it was removed |
|---|---|
| `TASK-UI1_AnnouncerFullText.md` | Done (`05cb219`), reviewed in #9 |
| `TASK-UI2_CanvasScalingStandard.md` | Done (`05cb219`), reviewed in #9; AC4 corrected in #9 §1 |
| `TASK-UI3_HudAnchoringAndHand.md` | Done (`05cb219`), reviewed in #9 |
| `TASK-UI4_PreviewClampAndConfirmLabel.md` | Done (`05cb219`), reviewed in #9. Its readability follow-ups are N47 and N48. |
| `TASK-UI5_ResolutionSetting.md` | Done (`72a724c`), reviewed in §2. Its manual checks (§9) are copied into §5 B. |

**New:** [`TASK-UI6_ReadabilityProfiles.md`](../Tasks/TASK-UI6_ReadabilityProfiles.md), ready for Codex. It covers N47, N48 and N49, with your decisions V1–V3.

The research documents, Codex's implementation reports and the checklists stay as reference material.

## Sources

- **Git:** `git log`, `git show --stat 05cb219`, `git show 72a724c` (all files), `git status` (clean), `git ls-files Docs/AI`
- **Checklist:** `Docs/AI/ManualChecks/2026-10-21_ManualChecks-Report8-9.xlsx`, saved at 21:55 (all four sheets)
- **Logs:**
  - `%LOCALAPPDATA%\Unity\Editor\Editor.log`: header `-projectPath C:/Lesson/FallenStar-Spellcasters_clone_1`, started 21:12. Your session is lines 1–14037; Codex's UI5 work comes after.
  - `Editor-prev.log` (the clone, 13:57–19:39)
  - `Player.log` / `Player-prev.log`: both from `build/ui5-verification/TestPlayer` (`Mono path[0]`)
- **Files and times:**
  - `C:\Lesson\Spellcasters.Build\` (all files 2026-10-20 13:16)
  - every `*_Data/Managed/Assembly-CSharp.dll` and `globalgamemanagers` on C: and D:, with their write and access times
  - `fsutil behavior query disablelastaccess` (= 2, enabled)
  - the write times of `GameMasterPrefab.prefab`, `PlayerSession.cs`, and every asset changed since 15:18
  - the font asset compared with `build/ui1-ui4-verification/baseline/`
- **Registry:**
  - `HKCU\Software\DefaultCompany\FallenStar-Spellcasters`: `setting.language_code` = `en`; 2560×1600, native, mode 1
  - `HKCU\Control Panel\Desktop` (100 % scaling)
  - the Windows work area 2560×1552
- **Assets:**
  - the CanvasScalers of all six scenes
  - `CardViewUIPrefab.prefab`: rect tree, scales, TMP margins and autosize ranges
  - `PlayerUIPrefab.prefab`: every TMP size and range
  - `GameScene.unity` PrefabInstance `282301674` (font overrides)
  - `PlayerSessionPrefab.prefab` (`m_cardPreviewPrefab`)
  - `SettingsScene.unity`: the UI5 objects, wiring and label events
  - `UI_table*` (the UI5 keys), `Cards_en/ru`
  - `UserSettings/Layouts/default-6000.dwlt` (the Game view panel)
- **Code read:** `DisplaySettingsUI.cs`, `SettingsUI.cs`, `CardPreviewController.cs`, `PlayerSession.cs:420-470`, and the N32–N35 locations
- **Codex output:**
  - `build/ui5-verification/`: `edit-results.txt`, `play-results.txt`, `build-results.txt`, `test-build-results.txt`, `player-observations.txt`, `final-audit-results.txt`, `failure.txt` (the fixed harness NRE), `Settings-*.png`
  - `build/ui1-ui4-verification/Hand-10-PlayMode.png` (1280×800)
