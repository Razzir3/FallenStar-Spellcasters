# Issue Report #14: FallenStar – Spellcasters (your checklist and poll, three new Codex tasks, the mane guide)

- **Date:** 2026-10-24
- **Baseline:** `HEAD` = `ff8a9dd` ("N32-35, N54-58"), clean working tree.
  - **The commit equals what report #13 reviewed:** no script, scene, prefab or localization file changed after Codex's report (2026-10-22 21:17).
  - The asset files touched by your build (`PC_RPAsset`, `GraphicsSettings`, …) match `HEAD`.
- **Previous report:** [#13](2026-10-23_IssueReport-13.md). Reports #1–#12 are waiting for deletion (P7 = a, §8). Git keeps them.
- **Your request:** "Check my reports. Prepare tasks for Codex."
- **Method:**
  1. Read every non-empty cell of every sheet of both workbooks: the filled checklist `ManualChecks/2026-10-23_ManualChecks-Report13.xlsx` and the answered `Polls/2026-10-23_Poll-Report13.xlsx`.
  2. Checked the evidence on this PC:
     - `Editor.log` (main Editor), `C:\Lesson\clone-editor.log`
     - `Player.log` / `Player-prev.log`
     - the file times of `C:\Lesson\Spellcasters.Build\`
  3. Read the code each answer leads to, and the NGO 2.11 source for the disconnect and time paths. That found N61 (§2) and confirmed N36's cause (§3).
  4. Wrote three specs, a Blender guide and a new poll.
- **I didn't run Unity, a build or Play Mode,** and I changed no code, scene, prefab or setting.
- **What I changed:**
  - this report
  - three specs in `Tasks/`, one guide in `Guides/` (a new folder), the new poll
  - the headers and three lines of the `Architecture/*.md` files
- **New IDs:** **N61, U17, U18.** The next new finding would be **N62.**

**This report is the full issue list.** §6 lists every open issue and §7 every closed one.

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
| **Your checklist** | **All 38 rows Pass,** including every optional row except I1, which is for after a commit. The *Found during the checks* table is empty. The logs agree: the build succeeded, and there's no exception and no guard warning in either Editor log or the build's `Player.log` (§1). |
| **Closed** | **9 issues: N54, N57, N56, N55, N58, N32, N33, N34, N35** (all in `ff8a9dd`). Also the font question (P3 = a). |
| **Your poll** | 13 of 13 answered, plus five new ideas on the *Priorities* sheet. Every answer is applied in §4. |
| **New** | **N61** 🟢 Confirmed: after a host leaves the lobby or aborts a match, NGO's delayed shutdown runs the disconnect cleanup a second time and loads `LobbyScene` twice. Your `Editor.log` shows two loads 12 lines apart (§2).<br>**U17:** the sign-out code you chose to keep (P2).<br>**U18:** a result screen for a lost host, the alternative to your P1 choice. |
| **Raised** | **N36:** the cause is now Confirmed in the code. The loops add `Time.deltaTime`, which Unity caps at 0.333 s per frame, while the countdown follows NGO's uncapped `ServerTime`. Any frame longer than a third of a second makes "00" show while the phase goes on (§3). |
| **New Codex tasks (in this order)** | 1. **[TASK-N59_N61_F1](../Tasks/TASK-N59_N61_F1_PlayerMessages.md):** "Connection to the host was lost." in the Lobby (your P1 = c), a single cleanup (N61), and on-screen messages for lobby/network failures, ignored inputs and deck refills (F1). Plus N60's `MultiplayerManager` part.<br>2. **[TASK-F4_F6_N36](../Tasks/TASK-F4_F6_N36_GameLoopRules.md):** "Drawing cards" phase (F4), losing the last block loses the match (F6), phase timers on the countdown's clock (N36). Plus N60's `GameMaster.Player.cs` part.<br>3. **[TASK-UI7](../Tasks/TASK-UI7_MenuLowProfile.md):** the compact menu layout below 1366 px, Settings down to 640×480 (P12 = a). |
| **For you** | [GUIDE-N27](../Guides/GUIDE-N27_PonyManeInBlender.md): how to let another Claude agent make the mane in Blender 4.4, and the 5-minute Unity step after it (your P8 comment). |
| **Cleanup: blocked** | Claude Code's safety check refused my deletes, both the committed docs and `build/` (git-ignored, no undo). §8 has the commands for you, and poll **Q4** asks how to handle it next time. |
| **New poll** | [2026-10-24_Poll-Report14.xlsx](../Polls/2026-10-24_Poll-Report14.xlsx): **8 questions.** None blocks Codex. Q3 only changes one string in task 1. |
| 🔴 / 🟠 / 🟡 open | **0 / 0 / 1:** N59 (task 1 written) |

---

## 1. Your checklist (2026-10-23)

### 1.1 Results

| Section | Rows | Result | Covers |
|---|---|---|---|
| A, table-card clicks, both sides in both windows | 7 | Pass | N54 |
| B, casting through the HUD row; Confirm / Send at odd moments | 6 | Pass | N54, N56, N58, N32 |
| C, early Confirm Turn | 3 | Pass | N57 |
| D, HP and Life texts | 5 | Pass | N35 |
| E, lobby, full match, second match | 3 | Pass | N33, N34, N55 (normal start) |
| F, research: leave and quit in both directions | 5 | Pass: today's behavior as described, incl. **N59** (F1.3, G1.6) | N59, H6, N19 |
| G, release build vs Editor | 6 | Pass | N34, N33, N57 |
| H, both Editor logs | 3 | Pass (H1.3: nothing to note) | all, U2, U9 |
| I, optional, after the commit | 2 | Not run (as planned) | N55 abort |

**Notes:**
- Every row says "Owner" and 2026-10-23. No row is marked "PC 2".
- **The *Known issues* sheet:** the yellow *Found during the checks* table is empty, and there's nothing below it. **The *Read me*:** unchanged.

### 1.2 What the files on this PC show

| Evidence | What it shows |
|---|---|
| `Editor.log:14606` | "Build Finished, Result: Success." (G1.1). The build's DLLs are dated 2026-10-24 00:20. |
| `Player.log` (one build session, ended 00:24) | Ponies spawned (4 × "Invalid mane material index.", N27). **None of G1.4's 20 debug strings.** No "Exception". A normal shutdown. (G1.4, G1.5) |
| `Editor.log:3393`, `:4528` | "match ended … Reason: Surrendered" twice: the host's Leave match (F1.4) |
| `Editor.log:5708`; `clone-editor.log:8366` | The main Editor back in `LobbyScene` mid-match while the clone hosted: the N59 path (F1.3 with the roles swapped) |
| Both Editor logs | No exception from game code. No N55 / N56 / N58 guard warning. No NGO nested-object or `NetworkSceneManager.Dispose` line (U2, U9). |

- **The logs cover only part of your session:** the main Editor's log restarts with the Editor, and earlier matches aren't in it. As always, only positive lines count, and nothing contradicts a row.
- **The build log** has 4 mane warnings: 2 ponies × 2 match starts in that session. G1.2, G1.3 and G1.6 fit into two matches if G1.3 and G1.6 shared one. That's consistent.

### 1.3 Closed by your checklist and `ff8a9dd`

**N54, N57, N56, N55, N58, N32, N33, N34, N35:** see §7.
- N55's abort path was tested only by Codex's harness. I1 stays available as an optional check after any commit.

---

## 2. New finding: N61, the host cleans up twice after leaving

| Step | What happens | Where |
|---|---|---|
| 1 | The host presses Leave lobby. The same applies after `AbortMatch` / `AbortGameStart`. `Disconnect()` sets `m_onDisconnecting = true`, deletes the lobby and calls `NetworkManager.Shutdown()`. | `MultiplayerManager.cs:308-353`, `:477-512` |
| 2 | **NGO only marks the shutdown.** It finishes in a later update and, with a client connected, first waits for it to disconnect (up to 5 s). | NGO `NetworkManager.cs:1574-1588`, `:531-577` |
| 3 | `Disconnect()` continues at once with `CleanupAfterDisconnect()`: the guard is reset, `InMenuStatus` is set, and `LobbyScene` loads. | `:1514-1552` |
| 4 | When NGO finishes, it raises the **host's own** disconnect callback. | NGO `NetworkManager.cs:1664-1668` |
| 5 | `OnClientDisconnected(0)` → `HandleLostConnectionToHost()`. The guard is already false, so it runs a second full cleanup and **loads `LobbyScene` again.** | `:218-306` |

- **Evidence:** in your `Editor.log`, after the main Editor hosted a lobby (Relay QoS lines), `Loaded scene 'Assets/Scenes/LobbyScene.unity'` appears at lines 5929 and 5941 with nothing in between.
- **`LeaveMatchToResultScene` doesn't have this problem:** it keeps its flag until the shutdown ends (`:426-431`).

| ID | Level | Issue | Location | Verdict |
|---|---|---|---|---|
| **N61** | 🟢 Low | **After the host's own Leave lobby or a match abort, the disconnect cleanup runs twice and `LobbyScene` loads twice.** The visible cost is a second load of about 200 ms. With task 1's messages, the second load could have hidden a message (task 1 handles that). | `MultiplayerManager.cs:218-258`, `:308-353`; NGO `NetworkManager.cs:1664-1668` | Confirmed (NGO source + log) |

**The fix is in task 1** (§4.3): `OnClientDisconnected` ignores the host's own callback when the cleanup has already run (no lobby, `InMenuStatus`). A host that really loses its connection mid-lobby or mid-match still gets today's path.

---

## 3. N36 confirmed, and why it matters for timing

- **NGO advances `ServerTime` with `Time.unscaledDeltaTime`** (NGO `Timing/NetworkTimeSystem.cs:161`). The countdown clients see is `m_phaseEndServerTime − ServerTime`.
- **The server's phase loops count `m_phaseElapsed += Time.deltaTime`.** Unity caps that at `Maximum Allowed Timestep` = **0.333 s** (`ProjectSettings/TimeManager.asset`).
- **The result:** a 1 s stall puts the loop 0.67 s behind the countdown, and the clients show "00" for that long. Likely moments for a long frame:
  - the match start, which spawns 84 cards
  - Editor hitches
  - two Editors on one PC
- **Separately,** the draw phase always ends about 4 frames after its countdown: each draw's wait starts a frame late.
- **N36 stays ⚪ Info** (cosmetic) **but is now Confirmed** from the code. Task 2 measures it on the old code first, then makes every loop wait on the countdown itself.

---

## 4. Your poll answers, and what I did with them

| # | Your answer | Your comment | Applied |
|---|---|---|---|
| **P1** (N59) | **c:** Lobby + message | "Right now game running like Host + client, in future in will change in Server + clients. So better show connection was lost message." | **Task 1.** The message is shown only to non-server peers, so it stays right after a Server + clients move. Your plan is now in `Architecture.md` §5. The rejected result-screen option is **U18**. |
| **P2** (W3) | **a:** keep | "Mark is as possible issue, but keep it now." | **U17** (§6). `AuthManager` stays untouched; task 1 handles sign-in failures in `LoginUI` instead. |
| **P3** (font) | **a:** keep the committed version | — | Closed. `git restore` returns the font to `HEAD` as before. |
| **P4** (N60) | **b:** with the next task | — | `MultiplayerManager` items → task 1; `GameMaster.Player.cs` → task 2. The rest stays open until a task touches those files. |
| **P5** (`build/`) | **a:** delete | "If it not in the mine project, and was existed only for tests, we can delete it this time." | **Blocked** (§8). Your command is ready. |
| **P6** (June IL2CPP) | **a:** delete | "You can delete old build This time" | **Blocked** (§8). Same command. |
| **P7** (old reports) | **a:** delete #1–#12 | — | **Blocked** (§8). #13 stays as the previous report; poll Q2 asks about keeping only the newest. |
| **P8** (N27) | **a:** still parked | "You can make me instruction, how I can delegate to other claude agent create mane for model in Blender 4.4" | **[GUIDE-N27](../Guides/GUIDE-N27_PonyManeInBlender.md).** The mane colour is poll Q5. |
| **P9** (N29) | **a:** still parked | "Still needed for debugs" | Parked. UI7 leaves the debug ID texts alone. |
| **P10** (N36) | **b:** write a task | — | **Task 2** (my recommendation was a; your call). |
| **P11** (M11) | **a:** still parked | "Will be still parked before migration to Server + clients system" | Parked until then. |
| **P12** (UI7) | **a:** yes, next | — | **Task 3.** |
| **P13** (format) | **a:** poll workbook | "Here is more usefull, I suppose" | The new poll uses the same format. |

**Your *Priorities* sheet:**
- **Now:** F1 (messages), F4 (draw phase), F6 (blocks win). All three are in tasks 1 and 2.
- **Next:** F5 (card effects), F9 (big-card animation). Poll Q8 asks which first.
- **Later:** F2, F3, F7, F8, and your five new ideas:
  - **F10:** a movement limit per turn
  - **F11:** hints
  - **F12:** hint settings
  - **F13:** audio and music
  - **F14:** redraw the GUI panels. Your note: "HP/Mana/Etc are hard to see with lava on the back". Poll Q6 offers a small fix for that part now.

---

## 5. The new tasks for Codex

| Order | Task | Covers | Touches | Size |
|---|---|---|---|---|
| 1 | [TASK-N59_N61_F1](../Tasks/TASK-N59_N61_F1_PlayerMessages.md) | N59 🟡, N61 🟢, F1, N60 (part) | `MultiplayerManager`, `GameMaster` (abort RPC), `GameMaster.Card` (refill RPC), `PlayerSession`, `PlayerUI`, `LoginUI`, two new scripts, `NoticePanel` in LobbyScene and LoginScene, 11 localization keys | M |
| 2 | [TASK-F4_F6_N36](../Tasks/TASK-F4_F6_N36_GameLoopRules.md) | F4, F6, N36 ⚪, N60 (part) | `GameMaster` (timing, end reason), `GameMaster.GameLoop`, `.Processing`, `.Player`, `ResultMenuUI`, 2 localization keys | M |
| 3 | [TASK-UI7](../Tasks/TASK-UI7_MenuLowProfile.md) | UI7 (P12) | `ResolutionProfileLayout` (font limits), five menu scenes, `LobbyListItemPrefab`, the Settings minimum | M |

**Why this order:**
- Task 1 holds the only open Medium defect.
- Tasks 1 and 2 share `GameMaster.cs` and the `UI_table` tables.
- Task 3 lays out the `NoticePanel` that task 1 adds.

**What each spec asks Codex to measure:** like N54 last round, every spec has a baseline measurement before the fix:
- the double `LobbyScene` load (N61)
- the late phase end after a stall (N36)
- the menu sizes (UI7)

**Remote-client cases:** no harness can play a real remote client. Every spec therefore has client rows in its §9, split by host and client where it matters.

---

## 6. All open issues

### 🔴 Critical / 🟠 High

None open.

### 🟡 Medium (1)

| ID | Issue | Location | Verdict | Next |
|---|---|---|---|---|
| **N59** | The host quits, crashes or leaves → the client lands in the Lobby with no message | `MultiplayerManager.cs:218-306`, `:1514-1552` | Confirmed (your F1.3, G1.6) | Task 1 |

### 🟢 Low (3)

| ID | Issue | Location | Verdict | Next |
|---|---|---|---|---|
| **N61** | A host's own disconnect runs the cleanup twice; `LobbyScene` loads twice (§2) | `MultiplayerManager.cs:218-258`, `:308-353` | Confirmed | Task 1 |
| N27 | The mane colour is never applied (no mane object); every spawn warns "Invalid mane material index." **Parked by you** (P8). | `PonyBase.prefab`; `PonyColorScript.cs:39-41`, `:85` | Confirmed | GUIDE-N27, poll Q5 |
| **N60** (rest) | Cosmetic leftovers not covered by tasks 1–2:<br>• the orphan `//ToDo` in `PlayerController.cs:37`<br>• commented-out blocks in `PlayerCardfWorldArea.cs:47`, `:77`, `:124`, `:131`, `CardControllPackage.cs:11`, `:21`, `NetworkCard.cs:178`, `CardWorldViewManager.cs:764` | as listed | Confirmed | With the next task touching each file (P4 = b) |

### ⚪ Info

- **N36** (Confirmed, §3): "00" can show while a phase goes on. → Task 2.
- **N29:** the lobby shows the player ID twice. **Parked by you** ("still needed for debugs"). (`UI/LobbyPlayerItemUI.cs:53-65`)
- **Naming and typos** (report only; don't rename):
  - `CardControllPackage`, `PlayerCardfWorldArea.cs`, `SetOponent` / `m_currentOponentNetworkObjectId`, `CheckActiveTaergetSelection`
  - `SingInAnonymous` / `SingOut` / `SingOutAndClearCredentials` / `m_isSingingIn`, `HearthbeatLobby`, `MovePlayeToSpawnPoint`, `ClearupAfterDisconnect`
  - `CardSpecialization.DeffenceSpell`, `m_knowLobbies`, `m_bootsTrap`, `_teamAssing`, `m_phaseAnouncementText`, `#region Valiable`, the GameObject `PhaseAnouncer`
  - log texts `"Packege"`, `"Handle unseccess"`, `"Dissconecting client"`, `"Joinde selected lobby"`, `"Need exatly 2 …"`; the local `_locakPoint`
  - `ConfirmSendButtonUI1` (in `ConfirmSendButton.cs`); `m_startHandCards` (really the per-round draw count); the Confirm binding in `PlayerUIPrefab.prefab:1585` names `ConfirmButtonUI` (it resolves)

### ❓ Unknowns and possible issues

| ID | Question | Status |
|---|---|---|
| U2 | The nested `SpawnPoint` NetworkObject inside `BlockBody` | ❓ No NGO warning in any log so far (H1.3 again) |
| U9 | NGO `NetworkSceneManager.Dispose` NRE on Play Mode stop | ❓ Not seen in the 2026-10-22 or 2026-10-23 logs. Poll **Q7:** close? |
| **U17** | **Sign-out code kept (P2 = a).** `AuthManager.SingOut()` and `SingOutAndClearCredentials()` have no caller. The second has swapped branches: it calls `ClearSessionToken()` while signed in, which the SDK rejects, and `SignOut(true)` while signed out. **Fallback:** fix the branch order when a sign-out button is added, or delete both (option b). | `AuthManager.cs:116-133`. Possible issue, kept by you. |
| **U18** | **No result screen for a lost host (P1 = c).** After task 1 the client gets the Lobby with a message. If you later want a "Match ended" screen, the fallback is option b of P1: a no-winner Result screen with the same text. | Your choice; revisit with the Server + clients move |

### Feature gaps (not defects)

The new poll's *Priorities* sheet has all 14 with your current ranks:

| # | Gap | Your rank | Status |
|---|---|---|---|
| F1 | On-screen messages | Now | Task 1 |
| F4 | Draw phase on screen | Now | Task 2 |
| F6 | Blocks win rule | Now | Task 2 |
| F5 | More card effects (discard/combo actions, All* / Random* targets, `ActivityEffect`) | Next | Needs your card designs (Q8) |
| F9 | Big-card show/hide animation | Next | Q8 |
| F2 | More settings (audio, graphics, controls, UI scale) | Later | — |
| F3 | Steam login (stub) | Later | — |
| F7 | Spectators | Later | — |
| F8 | Rejoin a running match (`RESEARCH-H6C`) | Later | — |
| F10 | Movement limit per turn | Later | your idea |
| F11 | Player hints | Later | your idea |
| F12 | Hint settings | Later | your idea |
| F13 | Audio and music | Later | your idea |
| F14 | Redraw GUI panels (HUD hard to read over lava) | Later | your idea; Q6 offers a quick fix |

**Also known, by design or parked:**
- join by lobby code (M11, parked until Server + clients)
- `RoundEnd` unused and `ResolveCards` transient
- the confirmed target doesn't survive a respawn pause
- with 10 cards the hand overlaps (UI3)

---

## 7. Closed issues

**Closed in this report:**

| ID | How it was closed | Commit / report |
|---|---|---|
| **N54** | Board card texts no longer catch UI clicks; right-hand cards open the big card in both windows (your A1–A3, B1.4) | `ff8a9dd` (#14) |
| **N57** | An early Confirm Turn no longer empties the hand row (C1, G1.3) | `ff8a9dd` (#14) |
| **N56** | Confirm and Send are null-safe (B2, H1) | `ff8a9dd` (#14) |
| **N55** | A missing spawn point or a start exception aborts cleanly (Codex's harness; E1 normal start; I1 optional) | `ff8a9dd` (#14) |
| **N58** | The package reader and handler bound the target count (B1, H1) | `ff8a9dd` (#14) |
| **N32** | The unused owner lookup is gone (B1) | `ff8a9dd` (#14) |
| **N33** | Dead code, two prefabs and the stale scene override removed (E1, G1) | `ff8a9dd` (#14) |
| **N34** | Release logs quiet (G1.4 and the build's `Player.log`) | `ff8a9dd` (#14) |
| **N35** | HP and lives written only on change (D1) | `ff8a9dd` (#14) |
| Font question | P3 = a: keep the committed font version | #14 |

**Closed earlier:**

| ID | How it was closed | Commit / report |
|---|---|---|
| Q1 | Moot: Codex measured the team-1 block through the shared camera | #13 |
| N50 | The preview resolves the HUD canvas and dock when it opens, so it works on a client | `6d89a09` (#12) |
| N52 | Hover validation follows the HUD row | `6d89a09` (#12) |
| N47 | The docked, readable big card (on hover and click) | `6d89a09`, UI6 (#12) |
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

---

## 8. Folder cleanup: blocked, the commands for you

**What happened:**
- I tried to delete what your poll and report #13 agreed on. Claude Code's automatic safety check refused both attempts as "irreversible local destruction":
  - the committed docs (which git can bring back)
  - the git-ignored `build/` folders (which nothing can bring back)
- I didn't try another way around it.
- **Poll Q4** asks how you want this from now on.

**Run this in PowerShell, in the project folder, before Codex starts** (Codex will create new `build/` folders):

```powershell
cd C:\Lesson\FallenStar-Spellcasters

# 1. Committed docs (git keeps them): the three finished specs, Codex's report, reports #1–#12 (P7)
Remove-Item Docs\AI\Tasks\TASK-N54_N56_N57_BoardClickAndHudButtons.md, `
            Docs\AI\Tasks\TASK-N55_N58_N32_ServerGuards.md, `
            Docs\AI\Tasks\TASK-N33_N34_N35_CodeHygiene.md, `
            Docs\AI\Reports\2026-10-22_N32-N35_N54-N58_Implementation.md, `
            Docs\AI\Reports\2026-10-18_IssueReport.md, Docs\AI\Reports\2026-10-18_IssueReport-2.md, `
            Docs\AI\Reports\2026-10-18_IssueReport-3.md, Docs\AI\Reports\2026-10-18_IssueReport-4.md, `
            Docs\AI\Reports\2026-10-19_IssueReport-5.md, Docs\AI\Reports\2026-10-19_IssueReport-6.md, `
            Docs\AI\Reports\2026-10-20_IssueReport-7.md, Docs\AI\Reports\2026-10-20_IssueReport-8.md, `
            Docs\AI\Reports\2026-10-21_IssueReport-9.md, Docs\AI\Reports\2026-10-21_IssueReport-10.md, `
            Docs\AI\Reports\2026-10-22_IssueReport-11.md, Docs\AI\Reports\2026-10-22_IssueReport-12.md

# 2. build/ (git-ignored, NO undo): the June IL2CPP output (P6) and old verification folders (P5).
#    Keeps last round's four. Run once with -WhatIf, check the list, then again without it.
$keep = 'hyg2-verification','n54-verification','n55-verification','task-folder-verification'
Get-ChildItem build | Where-Object { $keep -notcontains $_.Name } | Remove-Item -Recurse -Force -WhatIf
```

- **Part 2 removes about 3.2 GB:**
  - `Il2CppOutputProject` (1.1 GB)
  - `FallenStar-Spellcasters\`, the `.sln`, `UnityCommon.props` and the Burst folder
  - `n50-verification`
  - 23 `task-*` / `ui*` folders of accepted work
- **Kept on purpose:**
  - **This round's four `build/` folders,** as P5 said. They belong to accepted work now, so they can go next time.
  - **Report #13**, the previous one (poll Q2).
  - **The 2026-10-23 checklist and poll:** reviewed here and committed (poll Q1).
  - **`Research/RESEARCH-UIScaling_Visibility.md`:** UI7 builds on it; it goes after UI7.
  - **`Research/RESEARCH-H6C_ReconnectMidMatch.md`:** F8, Later.

---

## 9. Next steps

1. **Run §8's commands,** then **commit** this round's docs: this report, the three specs, the guide, the new poll and the architecture headers. With the specs committed, I can safely remove them later.
2. **Codex:** task 1 → task 2 → task 3, each with its own implementation report.
   - Task 1's strings depend on poll Q3 only if you pick b or c. Answer it before Codex reaches that step, or I'll correct it in the review.
3. **The poll:** any time.
4. **The mane:** [GUIDE-N27](../Guides/GUIDE-N27_PonyManeInBlender.md) whenever you like. Tell me when `PonyMane.fbx` exists.
5. **After Codex:** I review each task, and write one checklist for all three, with host and client rows kept apart.

## Sources

- **Your files:**
  - `Docs/AI/ManualChecks/2026-10-23_ManualChecks-Report13.xlsx`: every sheet and cell, incl. *Known issues* rows 33–40
  - `Docs/AI/Polls/2026-10-23_Poll-Report13.xlsx`: *Poll* K5:L17, *Priorities* B5:F20
- **Logs:**
  - `%LOCALAPPDATA%\Unity\Editor\Editor.log` (main Editor, `-projectpath C:\Lesson\FallenStar-Spellcasters`): lines 3393, 4528, 5708, 5929, 5941, 14606
  - `C:\Lesson\clone-editor.log`: line 8366
  - `%USERPROFILE%\AppData\LocalLow\DefaultCompany\FallenStar-Spellcasters\Player.log` (mono path `C:/Lesson/Spellcasters.Build`) and `Player-prev.log` (Codex's verification build, 2026-10-22)
  - `C:\Lesson\Spellcasters.Build\` file times
- **Code:**
  - `MultiplayerManager.cs`, `PlayerSession.cs`, `PlayerUI.cs`, `LoginUI.cs`, `AuthManager.cs`
  - `GameMaster.cs`, `GameMaster.GameLoop.cs`, `.Processing.cs`, `.Card.cs`, `.Spawn.cs`, `.Player.cs`, `.PackageManaging.cs`
  - `GameAnnouncerUI.cs`, `ResultMenuUI.cs`, `ResolutionProfileLayout.cs`, `DisplaySettingsUI.cs`, `PonyColorScript.cs`, `PlayerController.cs`
- **Scenes and assets:**
  - the five menu scenes' RectTransforms and TMP settings, `LobbyListItemPrefab`, `LobbyPlayerItemPrefab`, `PonyBase.prefab`, `Assets/Models/PonyBase.fbx`, `Assets/Materials/TestMaterial*.mat`
  - `UI_table` en/ru/ja, `ProjectSettings/TimeManager.asset`
- **NGO 2.11** (`Library/PackageCache/com.unity.netcode.gameobjects@beeaefb722f7`): `Core/NetworkManager.cs` (`Shutdown`, `ProcessServerShutdown`, `ShutdownInternal`), `Connection/NetworkConnectionManager.cs` (client disconnect and `Shutdown`), `Timing/NetworkTimeSystem.cs`
