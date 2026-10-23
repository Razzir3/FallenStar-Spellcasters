# Issue Report #12: FallenStar – Spellcasters (your N50/N52 checklist, a full revision, tasks for every open Medium and Low)

- **Date:** 2026-10-22
- **Baseline:** `HEAD` = `6d89a09` ("N50_N52"), with a clean working tree. That commit holds:
  - UI6 and N50/N52
  - reports #10 and #11, Codex's UI6 and N50/N52 reports
  - the TASK-UI6 / TASK-N50_N52 specs and the UI1–UI5 spec removals
  - your filled checklist
- **Previous reports:** [#1](2026-10-18_IssueReport.md), [#2](2026-10-18_IssueReport-2.md), [#3](2026-10-18_IssueReport-3.md), [#4](2026-10-18_IssueReport-4.md), [#5](2026-10-19_IssueReport-5.md), [#6](2026-10-19_IssueReport-6.md), [#7](2026-10-20_IssueReport-7.md), [#8](2026-10-20_IssueReport-8.md), [#9](2026-10-21_IssueReport-9.md), [#10](2026-10-21_IssueReport-10.md), [#11](2026-10-22_IssueReport-11.md)
- **Your request:** "Check my manual check report. After analyze all project. Make tasks for all not cosmetic issues (medium and low)."
- **Method:**
  1. **Your checklist:** [2026-10-22_ManualChecks-Report10-11.xlsx](../ManualChecks/2026-10-22_ManualChecks-Report10-11.xlsx), all 44 rows, your notes and the Clean-up sheet.
  2. **The logs:**
     - the clone's own `C:\Lesson\clone-editor.log` (19:05–20:13)
     - the main Editor's `Editor.log` (header `-projectpath C:\Lesson\FallenStar-Spellcasters`)
     - your build's `Player.log` and `Player-prev.log`
  3. **Traced A2.1** through:
     - `PlayerSession.Update` / `SelectObject`
     - `CardWorldViewPrefab`
     - GameScene's card areas, spawn points and camera
     - the uGUI 2.0 `GraphicRaycaster` source (its reversed-graphics rule)
     - how teams are assigned
  4. **Reviewed commit `6d89a09`** for N50/N52 against TASK-N50_N52. UI6 itself was reviewed in #11.
  5. **Read all 56 scripts again** (12,601 lines), looking for defects that the earlier reports missed.
  6. **Re-located every open issue** by grep at `6d89a09`.
- **I didn't run Unity, a build or Play Mode.** I changed no code, scenes, prefabs or settings. My changes:
  - this report
  - three new specs in `Docs/AI/Tasks/`
  - one corrected row in `GameLoop.md`
  - the removal of two finished specs (§8)

**This report is the full issue list.** §4 lists every open issue and §6 every closed one. The new IDs are **N54–N58**. The next new finding would be **N59**.

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
| **Your checklist** | **43 of 44 rows pass. One fails: A2.1.** On the client, clicking your own table cards opened no big card.<br>• **A:** 16 of 17 (N50 / N52 on the client and the host).<br>• **B:** 12 of 12 (UI6).<br>• **C:** 15 of 15 (UI5 and N49 in your build, and the big card in a release player as host and as client).<br>• **The three logs are clean** of game-code exceptions (§1.3). |
| **Why A2.1 failed** | **N54 🟡, Confirmed.** Each table card carries a small world-space UI canvas with a `GraphicRaycaster`, and its three texts are raycast targets.<br>• **On the right-hand cards** (team 1, which is always the client), those texts face the camera, so uGUI counts the pointer as "over UI". `PlayerSession.Update` then skips the board click.<br>• **The left-hand cards** (team 0, the host) face away and aren't hit.<br>• **So the client's cards can't be opened by a click in either window** (§2). It's older than N50, which hid it until now.<br>• **The fix** is prefab-only: remove that raycaster. |
| **N50 / N52 review** | **Accepted.** The commit matches the spec and what I reviewed before you committed. Your A1, A3–A8 and C8.3 confirm it on a real client and in a release player.<br>**Closed:** N50, N52, and with them UI6's N47 / N48 / N49. |
| **Your decisions from the notes** | • **W1 = a:** B6.1 says "Fine", so **N51 is accepted and closed.**<br>• **U15 answered:** A8.1, "it never got in the way". Closed.<br>• **U16 answered:** A8.2, "Fine". Closed. Your show/hide animation idea is on the ideas list.<br>• **W2 = a:** B4.1 passed with N53 named in its expected result, so **N53 is accepted and closed.** Tell me if you meant otherwise. |
| **Full revision: new** | **Five new findings:**<br>• **N54** 🟡: the client's table cards can't be clicked open (above).<br>• **N57** 🟡: pressing **Confirm turn** before the match starts leaves the hand row empty for the whole match.<br>• **N56** 🟢: Confirm and Send throw when there's no local session.<br>• **N55** 🟢: a missing team spawn point stalls the match start instead of aborting it.<br>• **N58** 🟢: the server trusts a client-sent target count. Only a modified client could abuse it. |
| 🔴 / 🟠 / 🟡 open | **0 / 0 / 2:** N54, N57 |
| 🟢 Low open | **8:** N55, N56, N58, N32, N33, N34, N35, and N27 (parked by you) |
| ⚪ Info open | N36, N29 (parked), the naming list |
| Unknowns open | **2:** U2, U9 (neither seen in today's logs) |
| **Tasks written** | **Three specs cover every open, non-cosmetic Medium and Low issue:**<br>1. [TASK-N54_N56_N57](../Tasks/TASK-N54_N56_N57_BoardClickAndHudButtons.md): the board click and the HUD buttons. Both Mediums.<br>2. [TASK-N55_N58_N32](../Tasks/TASK-N55_N58_N32_ServerGuards.md): server guards.<br>3. [TASK-N33_N34_N35](../Tasks/TASK-N33_N34_N35_CodeHygiene.md): dead code, release logs, HP text.<br>**Not tasked:** N27, which you parked. The Info items are cosmetic. |

**Overall:** UI6 and the client preview are done and accepted. One older defect showed up once the client could see previews at all, and the fix is a one-prefab change. The rest of the code is in good shape: two Medium findings with narrow triggers, and small Low items.

---

## 1. Your checklist

### 1.1 Results

| Section | Rows | Pass | Fail | Notes |
|---|---:|---:|---:|---|
| A: the big card, two Editors | 17 | 16 | 1 | **A2.1** (you wrote "False"; I read it as Fail). Notes on A3.1, A5.1, A8.1, A8.2 |
| B: UI6 HUD sizes | 12 | 12 | 0 | Note on B6.1 |
| C: player build | 15 | 15 | 0 | — |
| **Total** | **44** | **43** | **1** | |

### 1.2 Your notes, and what I take from them

| Row | Your note | What it means |
|---|---|---|
| A2.1 | (status "False") | Clicking your own table cards on the client opens nothing. → **N54** (§2) |
| A3.1 | "Work if point card in bottom HUD, don't work if point and click on board card" | The first half is N52 working: the hover view stays on a played card in the HUD row. The second half is A2.1 again (N54). |
| A5.1 | "Same issue. We can preview clients card on map. Host's card OK." | The host has the same problem. **One reading doesn't fit the code.** My question is in §2.4. |
| A8.1 | "it never got in the way, all visible" | **U15 is answered.** The docked view stays; fallback V2b isn't needed. |
| A8.2 | "Fine, in future possible add disapering/show on animation" | **U16 is answered.** No hover delay is needed. The fade or slide animation goes on the ideas list (cosmetic, not tasked). |
| B6.1 | "Fine" | **W1 = a:** the long Russian descriptions are readable. N51 is accepted. |
| B4.1 | (Pass) | Its expected result named N53: "The mana texts don't touch the banner (their boxes do)". I take the pass as **W2 = a**. |

### 1.3 The evidence

**`clone-editor.log`, the clone's own log:**
- The setup worked: the client's side is complete this time.
- **10 matches as the client:** KBL998, WBCJJ6, THRM7D, 6DCLPQ, P6FRC7, FC9NTL, RDD8TJ, 9CB7TG, 9RDMCR and BBQFBM. Those are the 10 matches the main Editor hosted before your build, and each logs "Local PlayerSession registered … OwnerClientId: 1".
- **0 exceptions,** and 0 "no screen-space canvas" warnings (A7.1 ✓).
- **40 × "Invalid mane material index."** That's N27, expected.
- **One "No card selected for control package." and one "Send ignored: no card target was confirmed."** at the start of the first match. That was my A2.3 step (§1.5).

**`Editor.log`, the main Editor:**
- **11 matches as the host.**
- **Your build at 19:52:** "Build completed with a result of 'Succeeded' in 26 seconds".
- **2 matches with the build:**
  - **67C8MN:** the Editor hosts, the build joins. The Editor's session is OwnerClientId 0.
  - **MWF9FQ:** the build hosts, the Editor is the client (OwnerClientId 1). That's C8.1–C8.3.
- **0 exceptions from game code.**
- **4 × "Only active player can confirm action phase."** That's Confirm turn pressed during the other player's action. The server ignores it, as designed; there's no on-screen message (a feature gap).

**`Player.log`, your build** (`Mono path[0] = 'C:/Lesson/Spellcasters.Build/…'`, last write 20:13):
- both lobbies, 67C8MN and MWF9FQ
- **no warning, no error, no exception** (C9.1 ✓)
- `Player-prev.log` is from 19:54, so the build was started more than once. That's consistent with C6.

**Matches ending:** 8 of the matches ended by Leave match or by stopping one side ("Surrendered" / "OpponentDisconnected"). Those paths are already closed issues (N19, H6) and worked every time.

### 1.4 What commit `6d89a09` also contains

- **The Clean-up sheet** (K1–K3) has no "Yes". K1 (language) and K2 (the build's window mode) are your local settings.
- **K3, the font:** `Assets/Fonts/MPLUSRounded1c-Medium SDF.asset` was committed with **+554 lines**.
  - **What changed:** TMP's font feature table (ligature and mark-positioning records) grew while Russian and Japanese text rendered in the Editor.
  - **What didn't:** the glyph and character tables are still empty, so N28's clear-on-build rule is intact. It's harmless at runtime, but it goes against `AGENTS.md`'s commit rule.
  - **Nothing to do now.** Note that `git restore` now restores this committed version.
  - **Optional:** to go back to the earlier state, run `git checkout 72a724c -- "Assets/Fonts/MPLUSRounded1c-Medium SDF.asset"` before your next commit.
- **`ProjectSettings.asset`:** two UWP fields, `metroPackageVersion: 1.0.0.0` and `metroTileShortName`. Unity filled them in, probably when you opened the build settings. Harmless.

### 1.5 My checklist error

- **A2.3 said "click your board card → click its target".** That's wrong:
  - A click on a table card only opens the big card.
  - Casting starts by picking the played card in the **bottom HUD row** (`PlayerUI.OnCardClicked` → `SelectCard` → `PlayerSession.SetSelectedCard`).
- **The clone's log shows the effect:** one "No card selected…" and one "Send ignored…", then you found the right way, and A2.3 passed.
- **The cause:** `GameLoop.md`'s input table described "Target a played card" as starting with a world click. **I've corrected that row.**

---

## 2. Why A2.1 failed (N54)

### 2.1 The chain

1. **`PlayerSession.Update`** (`InGame/PlayerSession.cs:728-746`) returns on a left press when `EventSystem.current.IsPointerOverGameObject()` is true. That's how HUD clicks are kept off the board. Otherwise `SelectObject` raycasts with Physics and opens the big card for a `CardViewWorld` (`:426-460`).
2. **The canvas on each table card:** `CardWorldViewPrefab` has a canvas at `VisualRoot/Front/TextRoot/Canvas`.
   - It's **World Space**, with no event camera, so uGUI uses `Camera.main`.
   - It has a **`GraphicRaycaster`** (`CardWorldViewPrefab.prefab:552-569`).
   - Its name, description and cost texts are **raycast targets** (`:153`, `:719`, `:920`).
   - Each text rect is about 20 × 5 world units, centred on the card. It covers the card's middle band and the gap to the next card.
3. **The two card areas in GameScene are mirrored:**
   - team 0 at x −20 (PrefabInstance `632972215`)
   - team 1 at x +20 (`1266062148`)
   - `Front` is turned 180° about Y.
   - So the text of **team 1's** cards faces `Main Camera` (orthographic, at (0, 53.1, −69), tilted 55°), and team 0's text faces away.
4. **uGUI's reversed-graphics rule** (`GraphicRaycaster.cs:232-244`, `m_IgnoreReversedGraphics: 1`) skips graphics that face away from the camera. So only team 1's texts are hit.
5. **Who is team 1:**
   - `SessionManager.CreateSessionsForAlreadyConnectedClients` walks `ConnectedClientsIds`, with the host first.
   - `GetFreeSlot` gives slot 0 first.
   - So the **host is always team 0 (left, red)** and **the client team 1 (right, blue)**.

### 2.2 What each window sees

| Click on… | Host window | Client window |
|---|---|---|
| Left cards (the host's, team 0) | ✅ big card | ✅ big card |
| Right cards (the client's, team 1), middle band | ❌ nothing | ❌ nothing (your A2.1) |
| Right cards, top (art), away from the text | may work | may work |

### 2.3 Since when

- **The canvas, its raycaster, `PlayerSession`'s UI check and the board-click preview** all date from the oldest code commit, `75fab38` (2026-07-17).
- **On the host it went unnoticed,** because the host's own cards work. On a client, N50 blocked every preview until today's fix.

### 2.4 Your host note (A5.1): one question

- **"We can preview clients card on map"** doesn't fit the chain above if it means clicking the middle of the right-hand cards in the host window.
- **I think you meant "can't",** or you clicked the top of the card, which can work. "Same issue" points that way too.
- **Could you confirm?** In the host window, did a click on the **middle** of a client's (right, blue) table card open the big card?
  - The answer doesn't change the fix. The table card canvas has nothing clickable, so its raycaster can only cause harm.

**A side note:** for the same geometric reason, the **left-hand** cards are seen from the back. If their art or text looks mirrored to you, that's cosmetic and not part of the task. Tell me if you want it fixed.

### 2.5 The fix

[TASK-N54_N56_N57](../Tasks/TASK-N54_N56_N57_BoardClickAndHudButtons.md) §4.1:
- **The change:** remove the `GraphicRaycaster` from that canvas, and turn off Raycast Target on its three texts.
- **What doesn't change:** no code, no layout, no rotation.
- **A guard against regressions:** a new point in `Dependencies.md` §6.

---

## 3. New findings

| ID | Level | Issue | Location | Evidence | Verdict |
|---|---|---|---|---|---|
| **N54** | 🟡 Medium | **The client's table cards can't be clicked open, in either window.**<br>• Their world-space text faces the camera and is a raycast target, so `PlayerSession.Update` treats the click as a HUD click (§2).<br>• **What it costs:** in every match nobody can read the client's played cards by clicking them. On the client that's your own cards, which you can still hover in the HUD row. On the host that's the opponent's cards, with no other way to read them.<br>→ TASK-N54_N56_N57 | `Prefab/BoardPrefab/CardWorldViewPrefab.prefab:552-569` (`GraphicRaycaster`), `:153`, `:719`, `:920` (`m_RaycastTarget: 1`)<br>`InGame/PlayerSession.cs:739-743`<br>`GameScene.unity` PrefabInstances `632972215` / `1266062148`<br>`SessionManager.cs:70-82`, `:112-121`, `:246` | Your A2.1 and A3.1 notes; the asset chain; uGUI `GraphicRaycaster.cs:232-244`, `:296-307` | Confirmed |
| **N57** | 🟡 Medium | **Confirm turn pressed before the match starts leaves the hand row empty for the whole match.**<br>• **`PlayerUI.ConfirmTurn`** assigns `m_playerSession` when it's null, but subscribes nothing.<br>• **`PlayerUI.Update`** binds and subscribes only when `m_playerSession` is null, and only once `GameMaster` exists.<br>• **The trigger:** on the host, `LocalPlayer` exists as soon as GameScene loads, but `GameMaster` only after the client has loaded too. A click in that time (the client's load, usually a few seconds) means `UpdateCards` never runs. HP and mana still update.<br>• **A client's window** is short.<br>→ TASK-N54_N56_N57 | `UI/PlayerUI.cs:719-737` (`ConfirmTurn`)<br>`:124` (early return)<br>`:134-173` (the binding)<br>`MultiplayerManager.cs:1317-1379` (`GameMaster` created in `OnLoadEventCompleted`) | Code path | Confirmed (code path); the window's length is a manual check |
| **N56** | 🟢 Low | **Confirm and Send can throw.**<br>• `ConfirmTargetButtonUI.ConfirmSelect` and `ConfirmSendButtonUI1.TryConfirmSend` read `SessionManager.Instance.LocalPlayer` and call it without null checks.<br>• **When:** a press before the session registers, or while the scene closes after a disconnect, logs a `NullReferenceException`. No state is damaged.<br>→ TASK-N54_N56_N57 | `UI/ConfirmTargetButtonUI.cs:7-12`<br>`UI/ConfirmSendButton.cs:6-11` | Code path | Confirmed (latent) |
| **N55** | 🟢 Low | **A missing team spawn point stalls the match start.**<br>• `CreateBlock` logs "No Spawn Point" and then uses the null point (NRE).<br>• `StartGameAfterSceneReady` has no `try`, so `StartNewGame` never runs and nothing aborts. Both players wait in GameScene until someone presses Leave match.<br>• **Today** each team has exactly one scene spawn point, so it takes a scene edit.<br>→ TASK-N55_N58_N32 | `InGame/GameMaster/GameMaster.Spawn.cs:248-257`<br>`MultiplayerManager.cs:1459-1461` | Code path; GameScene spawn points `1977742868` (team 0), `1585064127` (team 1) | Confirmed (latent) |
| **N58** | 🟢 Low | **The card package reader trusts a client-sent count.**<br>• `CardControllPackage.NetworkSerialize` allocates `new List<ulong>(_count)` from the stream, with no bound.<br>• **The effect:** a modified client can make the host throw (negative count) or allocate a large array (huge count). NGO catches the exception, so the host survives, but it stalls and logs.<br>• **Honest clients always send 4.**<br>→ TASK-N55_N58_N32 | `InGame/CardControllPackage.cs:26-37` | Code path | Plausible (needs a modified client) |

**Checked and not a finding:**
- **`PlayerSessionPrefab`'s `m_cardPreviewController`** points to its own component (`PlayerSessionPrefab.prefab:100`). The `FindFirstObjectByType` fallback in `TryPrepareSelectionReferences` never picks the other session's controller.
- **`GameAnnouncerUI.GetTargetSession`** assigns `null` to a `NetworkBehaviourReference`. That's safe (U3, report #3).
- **The lobby, relay, disconnect and result paths** (`MultiplayerManager`, `SessionManager`, `SceneFlowManager`, `LeaveMatchUI`, `ResultMenuUI`): no new defect. Your eight Leave and stop endings today went through them cleanly.

---

## 4. All open issues

### 🔴 Critical / 🟠 High

None open.

### 🟡 Medium (2)

| ID | Issue | Current location | Verdict | Task |
|---|---|---|---|---|
| **N54** | The client's table cards (right, team 1) can't be clicked open in either window. Their world-space texts catch the UI raycast (§2). | `CardWorldViewPrefab.prefab:552-569`, `:153`, `:719`, `:920`; `PlayerSession.cs:739-743` | Confirmed | TASK-N54_N56_N57 |
| **N57** | Confirm turn before the match starts binds the HUD without its subscriptions, so the hand row stays empty all match. | `PlayerUI.cs:719-737`, `:124`, `:134-173` | Confirmed (code path) | TASK-N54_N56_N57 |

### 🟢 Low (8)

| ID | Issue | Current location | Verdict | Task |
|---|---|---|---|---|
| **N56** | Confirm and Send dereference the local session without checks. | `ConfirmTargetButtonUI.cs:7-12`, `ConfirmSendButton.cs:6-11` | Confirmed (latent) | TASK-N54_N56_N57 |
| **N55** | A missing team spawn point throws in `CreateBlock`, and the match start stalls without aborting. | `GameMaster.Spawn.cs:248-257`; `MultiplayerManager.cs:1459-1461` | Confirmed (latent) | TASK-N55_N58_N32 |
| **N58** | The package reader allocates from a client-sent count. | `CardControllPackage.cs:26-37` | Plausible | TASK-N55_N58_N32 |
| **N32** | `ProcessActivity` reads the card owner's character without a null check and never uses it. It can't throw today, because `CheckActivity` checks the same owner first. | `GameMaster.Processing.cs:139` | Confirmed (latent) | TASK-N55_N58_N32 |
| **N33** | **Dead code and unused assets:**<br>• `GameMaster.m_gameLoopCoroutine`, `m_phasesTimerCoroutine`, `m_cardViewWorldPrefab`<br>• `PlayerController.m_isDead` (never written), `GetCurrentSpeed()`<br>• `PlayerSession.ClearHand()`, `ClearBlocks()`, `RemoveCard()`<br>• `NetworkCard.SetFaceUpServerRpc`, `SetRotatedServerRpc`, `DespawnCardServerRpc`, and `DespawnCard()`, which only that RPC calls<br>• `CardPreviewController.TogglePreview`, `MultiplayerManager.KnowLobbies`, `SceneFlowManager.LoadLoginScene`, `GameSettingsPrefs.HasLanguageCode`<br>• `DisplaySettingsUI.FitsDisplay`'s unused `windowed` parameter<br>• `BootsTrap.prefab` and `MultiplayerManager.prefab`<br>• the stale `m_timeText` override on the GameScene HUD instance<br>**Your call (W3):** `AuthManager.SingOut()` and `SingOutAndClearCredentials()`. | `GameMaster.cs:56`, `:104-105`<br>`PlayerController.cs:21`, `:118`<br>`PlayerSession.cs:374`, `:583`, `:748`<br>`NetworkCard.cs:71-87`, `:131-141`<br>`CardPreviewController.cs:58`<br>`MultiplayerManager.cs:65`<br>`SceneFlowManager.cs:45`<br>`GameSettingsPrefs.cs:20`<br>`DisplaySettingsUI.cs:137-141`<br>`AuthManager.cs:116-133`<br>`GameScene.unity:499-502` | Confirmed. The `SingOutAndClearCredentials` branch order is Plausible. | TASK-N33_N34_N35 |
| **N34** | **31 `Debug.Log` lines run without a debug flag,** so they print in release builds. All are listed in the task's §4.2.<br>**Also:**<br>• `PlayerUI.OnCardClicked` / `OnCardAlterClicked` read `cardView.name` before their null check<br>• three gated messages lack `$` | `PlayerUI.cs:452`, `:469`, `:488`, `:505`<br>`CardViewUI.cs:99`, `:105`<br>`MultiplayerManager.cs:765-836`, `:1541-1542`<br>`GameMaster.Processing.cs:115`, `:138`<br>`GameMaster.GameLoop.cs:485`, `:520`<br>and others<br>the `$`: `BlockController.cs:46`, `PlayerController.cs:70`, `GameMaster.PackageManaging.cs:258` | Confirmed | TASK-N33_N34_N35 |
| **N35** | `PlayerUI` formats HP and lives every frame, as two Smart-format strings, even when nothing changed. | `PlayerUI.cs:175-189` | Confirmed | TASK-N33_N34_N35 |
| N27 | The pony mane color is never applied, because `maneObject` isn't assigned. Every spawn logs "Invalid mane material index." (40 times in today's clone log). **Parked by you** until the mane model exists. | `PonyBase.prefab:368`; `PonyColorScript.cs:70-88` | Confirmed | — (parked) |

### ⚪ Info

- **N36:** the countdown and the server loop use different clocks. After a server hitch, or during drawing, clients can briefly see "00". Plausible and cosmetic. (`GameMaster.cs:124-141`, the phase loops in `GameMaster.GameLoop.cs`)
- **N29:** the lobby shows the player ID twice. **Parked by you.** (`UI/LobbyPlayerItemUI.cs:53-65`)
- **Naming and typos** (report only, don't rename):
  - `CardControllPackage`, `PlayerCardfWorldArea.cs`, `SetOponent` / `m_currentOponentNetworkObjectId`, `CheckActiveTaergetSelection`
  - `SingInAnonymous` / `SingOut` / `SingOutAndClearCredentials` / `m_isSingingIn`, `HearthbeatLobby`, `MovePlayeToSpawnPoint`, `ClearupAfterDisconnect`
  - `CardSpecialization.DeffenceSpell`, `KnowLobbies` / `m_knowLobbies`, `m_bootsTrap`, `_teamAssing`, `m_phaseAnouncementText`, `#region Valiable`
  - the GameObject name `PhaseAnouncer`
  - the log texts `"Packege"`, `"Handle unseccess"`, `"Dissconecting client"`, `"Joinde selected lobby"`; the local variable `_locakPoint`
  - `ConfirmSendButtonUI1` (in `ConfirmSendButton.cs`); `m_startHandCards`, which is really the per-round draw count
  - **New:** the Confirm button's binding in `PlayerUIPrefab.prefab:1585` names the type `ConfirmButtonUI`. The class is `ConfirmTargetButtonUI`. The call still resolves, because Unity matches on the target object. Today's log shows `ConfirmTargetButtonUI:ConfirmSelect`.

### Feature gaps and ideas (not defects, listed so they aren't mistaken for bugs)

- **Settings:** language and display (UI5) exist. Audio, graphics quality, controls and a UI-scale setting don't. The UI6 profile is automatic, by design.
- **Join by lobby code is parked** (M11, you, 2026-10-19).
- **Steam login is a stub** (`AuthManager.SignInSteam`), although `LoginScene` has a button wired to it.
- **Phases:** `GamePhase.DrawCards` and `RoundEnd` are never set, and `ResolveCards` is only transient. During draws the announcer shows "Round break".
- **No effect yet:** the `DiscardCard` / combo actions, the `All*` / `Random*` targets and `ActivityEffect`.
- Destroying all enemy blocks doesn't win by itself. A player with no blocks is eliminated on their next death.
- `SessionRole.Spectator` exists, but nothing assigns it. `SessionManager` has 4 slots, but the scene supports only teams 0 and 1.
- **No player-facing messages for:** lobby or network failures, an ignored Send, an ignored Confirm turn during the opponent's action (4 times today), or a deck refill.
- The confirmed target of an action card doesn't survive a respawn pause (by design).
- With 10 cards, the hand overlaps (UI3, intended).
- The five menu scenes don't have the low profile yet (UI7, planned).
- **Idea (your A8.2):** a short show/hide animation for the big card.

### ❓ Unknowns: open

| ID | Question | Status |
|---|---|---|
| U2 | The nested `SpawnPoint` NetworkObject inside `BlockBody` | ❓ No NGO warning in any log so far, today's 12 matches included |
| U9 | NGO `NetworkSceneManager.Dispose` NRE on Play Mode stop, Editor quit or domain reload | ❓ Not seen today |

---

## 5. Pending manual checks

**Everything from report #11 §5 passed except A2.1,** which is now N54.

**The next checks** come with the tasks, in each spec's §9:
- **TASK-N54_N56_N57:**
  - click both sides' table cards in both windows
  - cast through the HUD row
  - press Confirm turn while the client is loading
- **TASK-N55_N58_N32:** a normal match with Damage, Heal and Draw casts. Optionally, a spawn-point abort in an unsaved scene copy.
- **TASK-N33_N34_N35:** one full match with a language switch; the release `Player.log` has no debug lines.

When all three are done, I'll write one short checklist for them, as before.

---

## 6. Closed issues

**Closed in this report:**

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

**Closed earlier:**

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
- UI5, display settings (`72a724c`, reviewed in #10; your build checks today, section C)
- UI6, readability profiles (`6d89a09`, reviewed in #11; your checks today, section B)

---

## 7. Your decisions, and the next steps

**Decisions I need:**

| # | Question | Options | My recommendation |
|---|---|---|---|
| **Q1** | **A5.1:** in the host window, did a click on the **middle** of a client's (right, blue) table card open the big card? | yes / no | — It only confirms §2. The fix doesn't depend on it. |
| **W3** | **`AuthManager.SingOut()` and `SingOutAndClearCredentials()`** have no caller, and the second one calls `ClearSessionToken()` while signed in (swapped branches). | **a.** Keep both for a future sign-out button, and fix the branch order then.<br>**b.** Delete both now, in TASK-N33_N34_N35. | **b.** Git history keeps them. A sign-out feature would need a spec of its own anyway. Until you answer, the hygiene spec leaves them alone. |

**The next steps:**
1. **Codex implements [TASK-N54_N56_N57](../Tasks/TASK-N54_N56_N57_BoardClickAndHudButtons.md)** (both Mediums). Then I review it.
2. **Then [TASK-N55_N58_N32](../Tasks/TASK-N55_N58_N32_ServerGuards.md),** then **[TASK-N33_N34_N35](../Tasks/TASK-N33_N34_N35_CodeHygiene.md).**
   - Each spec states its order and how to find code whose line numbers moved.
   - Committing after each reviewed task gives the next one a clean baseline.
3. **One checklist for all three,** then commit.
   - **Before that commit, restore the font file** (`git restore "Assets/Fonts/MPLUSRounded1c-Medium SDF.asset"` with both Editors closed). Today's commit took it along (§1.4).
4. **Later, on your go-ahead:** UI7 (the low profile for the five menu scenes).
5. **Parked by you:** N27 and N29. **Optional:** N36.

**Coverage of your request:**
- **Every open, non-cosmetic Medium and Low issue is in one of the three tasks:** N54, N57, N56, N55, N58, N32, N33, N34 and N35.
- **Left out on purpose:**
  - N27: parked by you.
  - The cosmetic or Info items: N36, N29, the naming list, the possible mirrored look of the left cards, and the animation idea.

---

## 8. Task folder

| File | State | Action |
|---|---|---|
| `TASK-UI6_ReadabilityProfiles.md` | Implemented, accepted, committed in `6d89a09` | **Removed** (not committed). It's still in git history: `git show 6d89a09:Docs/AI/Tasks/TASK-UI6_ReadabilityProfiles.md`. |
| `TASK-N50_N52_ClientCardPreview.md` | Implemented, accepted, committed in `6d89a09` | **Removed** (not committed). It's still in git history: `git show 6d89a09:Docs/AI/Tasks/TASK-N50_N52_ClientCardPreview.md`. |
| `TASK-N54_N56_N57_BoardClickAndHudButtons.md` | **New**, ready for Codex | First |
| `TASK-N55_N58_N32_ServerGuards.md` | **New**, ready for Codex | Second |
| `TASK-N33_N34_N35_CodeHygiene.md` | **New**, ready for Codex | Third |

**Also changed:** `Docs/AI/Architecture/GameLoop.md`, the "Target a played card" row (§1.5).

## Sources

- **Your checklist:** `Docs/AI/ManualChecks/2026-10-22_ManualChecks-Report10-11.xlsx`
  - every Checklist row: status, notes, steps and expected results
  - the Clean-up sheet
- **Git:**
  - `git log` and `git show --stat 6d89a09`
  - `git show 6d89a09` for the font asset, `ProjectSettings.asset`, `CardPreviewController.cs` and `PlayerUI.cs`
  - `git log -S` for the history of the world card raycaster and of `IsPointerOverGameObject` (`75fab38`)
- **Logs:**
  - **`C:\Lesson\clone-editor.log`** (`-projectPath …_clone_1`, 19:05–20:13): 10 matches as the client; the card-click and target warnings; 0 exceptions
  - **`%LOCALAPPDATA%\Unity\Editor\Editor.log`** (main project): 11 matches as the host; the build at 19:52; 67C8MN and MWF9FQ with the build; 0 exceptions
  - **`Player.log`** (20:13) and **`Player-prev.log`** (19:54) of `C:\Lesson\Spellcasters.Build`
- **Assets:**
  - `CardWorldViewPrefab.prefab`: the canvas, `GraphicRaycaster`, the three texts with their rects and raycast flags, the `BoxCollider`, the transforms from the root to the texts
  - `GameScene.unity`: `Main Camera`; the two card areas and two spawn points (overrides, all root objects); the HUD instance's `m_timeText` override; the `GameAnnouncerUI` block
  - `PlayerSessionPrefab.prefab` (the preview controller reference)
  - `PlayerUIPrefab.prefab` (the button bindings)
  - the GUID searches for `CardWorldViewPrefab`, `BootsTrap.prefab`, `MultiplayerManager.prefab` and the `GraphicRaycaster` script
- **Packages:** `com.unity.ugui` `GraphicRaycaster.cs:232-244`, `:296-307`
- **Code read in full:** all 56 scripts in `Assets/Scripts`. The new findings' lines are cited above.
- **Codex output:** `build/n50-verification/` (reviewed before your commit, see report #11 §5)
