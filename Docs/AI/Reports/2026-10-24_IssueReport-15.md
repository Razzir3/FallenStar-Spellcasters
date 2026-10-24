# Issue Report #15: FallenStar – Spellcasters (Codex's three tasks, the mane, your poll, two new tasks)

- **Date:** 2026-10-24
- **Baseline:** `HEAD` = `ff8a9dd` ("N32-35, N54-58") plus the uncommitted working tree:
  - **Codex's implementation** of TASK-N59_N61_F1, TASK-F4_F6_N36 and TASK-UI7 (its report: `2026-10-24_N59_N61_F1_F4_F6_N36_UI7_Implementation.md`)
  - my docs from report #14 and this report
  - your deletions from report #14 §8 part 1
  - `Assets/Models/PonyMane.fbx` (new, §3)
- **Previous report:** [#14](2026-10-24_IssueReport-14.md). Report #13 is deleted (poll Q2 = a); git keeps it.
- **Your request:** "Okay, I finished my polls, Codex end his tasks, Mane was created in c:/lesson/ManeWork/. Export mane in current project folder and prepare new report."
- **Method:**
  1. Read Codex's report, then **every line of its code diff** and the relevant scene, prefab and localization diffs, against each spec.
  2. Checked its evidence in `build/oct24-verification/`: build result, harness results, the N36 and N61 measurements, and three screenshots I opened myself.
  3. Read every non-empty cell of every sheet of the answered `Polls/2026-10-24_Poll-Report14.xlsx`, including your two new rows on *Priorities*.
  4. Read the mane agent's `out/README.md`, both check reports and the renders in `C:\Lesson\ManeWork\`, and copied the FBX into the project.
  5. Read the pony prefab, the colour code and the spawn order for the mane-colour task.
- **I didn't run Unity, a build or Play Mode.** I changed no C# file, scene or prefab.
- **What I changed:**
  - **added** `Assets/Models/PonyMane.fbx` (your request; byte-identical copy)
  - this report, two specs, this round's checklist and the new poll
  - the headers of the three `Architecture/*.md` files
  - **deleted** three reviewed, committed docs (§9)
- **New ID:** **N62.** The next new finding would be **N63.**

**This report is the full issue list.** §7 lists every open issue and §8 every closed one.

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
| **Codex's work** | **All three tasks match their specs.** One small finding, **N62** 🟢: Send on the opponent's turn shows the wrong hint. My spec's order caused it, not Codex (§2). Codex's release build succeeded. Its harness measured the old bugs and the fixes:<br>• N61: 2 Lobby loads before, 1 after<br>• N36: a phase ended 0.67–0.68 s late after a 1 s stall, and at most 0.006 s late after the fix |
| **Pending your checklist** | **N59, N61, N36, F1, F4, F6, UI7** close when [this round's checklist](../ManualChecks/2026-10-24_ManualChecks-Report15.xlsx) passes and the work is committed. 43 rows, 31 required. No harness can play a real remote client. |
| **UI7 exceptions** | During Codex's session you approved two layout changes outside the spec: 104-unit message boxes, and a 150-unit Japanese role label in the compact layout. I accept both (§1.1); poll Q6 asks whether Codex may ask you directly like that. |
| **The mane** | **Exported:** `Assets/Models/PonyMane.fbx` is the agent's file byte for byte, and goes through Git LFS like `PonyBase.fbx`. Its alignment checks pass (§3). It isn't on the pony yet: that's TASK-N27. |
| **Your poll** | 8 of 8 answered, plus two new ideas (deck and hand counters, F15 / F16). Applied in §4. |
| **Closed** | **U9** (Q7 = a). |
| **New Codex tasks** | **[TASK-N27](../Tasks/TASK-N27_ManeColours.md):** the mane on the pony, with a random colour per player from a pool of five, fixed for the match (your Q5 answer).<br>**[TASK-F9_F14a_N62](../Tasks/TASK-F9_F14a_N62_HudPolish.md):** the big-card fade (0.7 s / 0.6 s, set in the Inspector), dark boxes behind six HUD texts (Q6), and the N62 fix.<br>Both start after this round's commit. They share no files. |
| **Clean-up** | **Deleted:** last round's checklist, poll and report #13 (your Q1 / Q2). **Blocked:** writing the permission rule you chose in Q4; §9 has the two-minute steps. **Still yours:** the `build/` folders (report #14's part 2 wasn't run). |
| **New poll** | [2026-10-24_Poll-Report15.xlsx](../Polls/2026-10-24_Poll-Report15.xlsx): **6 questions** (mane colours, the permission rule, the forms of F15 / F16, what next, Codex asking you). None blocks the checklist. |
| 🔴 / 🟠 / 🟡 open | **0 / 0 / 1:** N59, implemented, waiting for the checklist |

---

## 1. Codex's implementation

### 1.1 Task by task

| Task | What the spec asked | Verdict | Notes |
|---|---|---|---|
| **N59_N61_F1** | `PlayerNotice` + `NoticeUI`; the N59 post with `ShouldShowConnectionLost`; the N61 guard `IsOwnShutdownTail`; failure posts at every listed site; `MarkMatchCancelled`; `MatchAbortedClientRpc`, `DeckRefilledClientRpc`; four HUD hints; 11 keys; `NoticePanel` in Login and Lobby; N60's `MultiplayerManager` items | ✅ Matches | **One deviation, accepted:** `LoginUI` reads `IsSignedIn` inside the `try`, because the Authentication SDK's `Instance` getter throws when service initialisation failed. That's safer than the spec. **N62** comes from §4.5's order (§2). |
| **F4_F6_N36** | `StartPhaseCountdown` + `PhaseTimeLeft`; no accumulated timers; pause saves the time left; `DrawCards` set and dealt on the countdown; `BlocksDestroyed` appended; `m_finishReason`; `CountSpawnedBlocks` / `RemoveCharacter` / `EliminatePlayerByBlocks`; two result texts; N60's `GetCharacterByID` | ✅ Matches | The code is the spec's code. `BlocksDestroyed` = 3, so the old values keep their numbers. |
| **UI7** | `LowText`; low profiles on the five menu canvases and `LobbyListItemPrefab`; Settings minimum 640×480 in code and scene | ✅ Matches, with two exceptions **you approved** in Codex's session | **(1)** Message boxes 104 units tall in both profiles (the spec's 56, and even +15 %, cut the longest Japanese message), plus small low-profile moves within ±15 %: Login logo, Login notice y, lobby search list, status line. **(2)** `LobbyPlayerItemPrefab` gets a low-only role width of 150, so "クライアント" stays at least 24 units. Checklist E4 lets you look at both. |

### 1.2 What I checked myself

- **Code:** every changed line of `MultiplayerManager`, `LoginUI`, `GameMaster` (+ `.GameLoop`, `.Processing`, `.Player`, `.Card`), `PlayerSession`, `PlayerUI`, `ResultMenuUI`, `ResolutionProfileLayout`, `DisplaySettingsUI`, and the two new scripts.
- **Side effects of the new code:**
  - **`DrawCards` everywhere:**
    - the announcer has `Phase.DrawCards` in en / ru / ja ("Drawing cards" / "Добор карт" / "カードドロー")
    - `PlayerUI` shows the hand in that phase
    - `LeaveMatchUI` only checks `None` / `GameOver`
    - the server ignores a Confirm Turn sent in that phase (the `default` branch of its phase switch, `GameMaster.PackageManaging.cs:265`)
  - **Lives −1:** `IsGameFinished` counts a player only with a character or lives > 0, so an eliminated player ends the match. `PlayerUI` already treats lives < 0 as "dead".
  - **Blocks:** `UpdateBlockHp` is the only caller of `DeleteBlock`. No other path can remove a block without the F6 check.
  - **The N61 guard** needs server + own client + no lobby + `InMenuStatus`. That's only true after `CleanupAfterDisconnect`. A host losing its transport mid-lobby or mid-match keeps today's path.
  - **The draw schedule:** the first card at 0 s, then one every 0.8 s, the end at 3.2 s. After a death pause the remaining draws keep their spacing (Codex measured offsets 0 / 0.83 / 1.87 / 2.67 s with a 0.23 s pause).
- **Assets:**
  - 13 new keys with unique IDs; every text is exactly the spec's
  - `NoticePanel` has the specified structure (Box inactive, raycast off on both graphics, autosize 24–36, margins 16)
  - `SettingsScene` has `m_minWidth: 640`, `m_minHeight: 480`
  - `ResultScene` only gained the profile component (low reference 1280×720)
- **Screenshots I opened:**
  - `LobbyScene-Current-ja-1280x720.png`: the box holds a two-line Japanese message, the role labels are readable, nothing covers a button
  - `LoginScene-ja-1280x720.png`: the box sits between the logo and the buttons
  - `SettingsScene-ResolutionOpen-ru-640x480.png`: the list opens and scrolls
- **Evidence files:**
  - `WindowsPlayer-build-results.txt`: "Succeeded; errors=0; warnings=2"
  - `release-assembly-check.txt`: 3 PASS (release debug guard, enum numbers, no test types)
  - `baseline-results.txt` / `fixed-results.txt` (N36, N61)
  - `gameplay-results.txt`: 60 PASS lines, from the hints to all 25 block destructions
  - `login-source-results.txt`: 6 PASS

### 1.3 Codex's before / after measurements

| Measurement | Before (baseline) | After |
|---|---|---|
| N61: `LobbyScene` loads after the host's own Disconnect (host-only NGO, no lobby service) | 2 | 1 |
| N36: phase end after a 1.0 s stall, RoundBreak / CardSelection / PlayerAction / RespawnWait | 0.675 / 0.684 / 0.684 / 0.673 s late | 0.006 / 0.0001 / 0.001 / 0.0005 s late |
| F4: dealing four cards, no pause | — | 3.21 s, order 0 / 1 / 0 / 1 |
| N36: PlayerAction after a death 20 s in | — | resumes at 40 s |
| UI6 HUD sweep (16 sizes × 3 languages) | 199 inherited diagnostics | the same 199, no new ones |
| UI7 menu sweep (5 scenes × 9 sizes × 3 languages) | many failures | 0 diagnostics after your approved exceptions |

### 1.4 Notes (not defects)

- **`Assets/DefaultNetworkPrefabs.asset` shows "M" in `git status`,** but only its line endings changed. Its content is identical to `HEAD`: Codex removed the test HUD that Unity had auto-registered. Run `git restore Assets/DefaultNetworkPrefabs.asset` before committing (checklist *Read me → Before you commit*).
- **Several scenes and assets are saved with CRLF.** Git normalises them on commit (`eol=lf`); only warnings.
- **The 640×480 Settings screenshot shows "321 × 531" as the current resolution.** That's the harness Editor's window size: N49 always lists the current size. Rows E3.1 and G1.2 check the real list.
- **Codex's remaining risks** are real but not defects. Checklist sections A, G and E cover them:
  - no real two-peer disconnect test
  - the Windows firewall dialog on the first start of its build
  - the final rebuilt player wasn't launched again
- **Codex's report lists its checks and limits honestly.** The font asset and `ProjectSettings` have no diff, and no test script is left in `Assets`.

---

## 2. New finding: N62, the wrong hint for Send on the opponent's turn

| ID | Level | Issue | Location | Verdict |
|---|---|---|---|---|
| **N62** | 🟢 Low | **During the opponent's Player action, Send shows "Choose a target and press Confirm before Send." instead of "It's your opponent's turn."** `TryHandlePackage` checks the package first, and during the opponent's turn there's usually no confirmed package. Following the hint doesn't help: the server accepts card input only from the active player. | `PlayerSession.cs:1083-1107` | Confirmed (code; Codex's report §3 says the same) |

- **Cause:** TASK-N59_N61_F1 §4.5 told Codex to keep the package check first, while its AC5 expected the opponent message. **The spec contradicted itself, and Codex followed §4.5 exactly.**
- **Fix:** TASK-F9_F14a_N62 §4.3 checks the opponent's turn first, through a helper that `ConfirmTurn` shares.
- **This round's checklist** records today's behaviour in row C1.4 and doesn't fail it.

---

## 3. The mane

**What the other agent made** (from `C:\Lesson\ManeWork\out\README.md` and its check reports):
- **A single mesh object `Mane`:**
  - 3,800 triangles (budget 4,000)
  - one closed shell, with no holes, loose parts or self-intersections
  - normals outwards
  - one material slot `Mane`
- **Modelled in `PonyBase.fbx`'s space,** aligned to its body object `Vert.002`.
- **A round-trip import into a fresh scene** reproduces the work file within 0.00000034 m.
- **The FBX headers equal `PonyBase.fbx`'s:** Y up, unit scale 1.0, FBX 7.4.
- **`PonyBase.fbx` was not changed.**
- **I looked at the renders** (`mane_v6_sheet.png`, `alignment_side.png`): the mane sits on the head and neck, with the forelock between the ears.

**What I did:**
- **Copied `out/PonyMane.fbx` to `Assets/Models/PonyMane.fbx`.** SHA-256 `b8a6e3ce0cd1e8fb1e06649e2995dfc82e6c2456255fe6c97526a9b85c12c3ec` on both. `.gitattributes` sends it through Git LFS, like `PonyBase.fbx`.
- **Unity wasn't running,** so `PonyMane.fbx.meta` doesn't exist yet. Unity writes it the next time you open the project. Commit both together.
- **I didn't copy `PonyMane_work.blend`.** Unity would import a `.blend` through Blender as a second model. It stays in `C:\Lesson\ManeWork\`.

**What's left: TASK-N27.**
- **One import detail matters.** The file has a single top node, so by default Unity makes `Mane` itself the model root, together with its −90° / ×100 node transform. The task turns on **Preserve Hierarchy**, so the model is `PonyMane` → `Mane` like `PonyBase` → `Vert.002`. Then both line up at an identity transform.
- **The task also does:**
  - the prefab wiring
  - five mane materials
  - the server-side colour choice
  - an automatic alignment check: the mane's height must be 0.54 ± 0.05 of the body's, from the README's measurements
- **Until then,** "Invalid mane material index." still appears at every spawn (N27).

---

## 4. Your poll answers, and what I did with them

| # | Your answer | Your comment | Applied |
|---|---|---|---|
| **Q1** (folders) | **a:** delete reviewed checklists and polls | — | Deleted the 2026-10-23 checklist and poll (committed, §9). Poll #14 is untracked, so it goes after your commit. |
| **Q2** (old reports) | **a:** keep only the newest | — | Deleted report #13 (§9). Report #14 is untracked, so it goes after your commit. |
| **Q3** (N59 text) | **a:** keep "Connection to the host was lost." | — | Unchanged. Row A2.1 checks it when the host leaves the lobby. |
| **Q4** (clean-up) | **b:** allow deletes of committed `Docs/AI` files | — | **The deletes worked this round.** Writing the permission rule was refused by Claude Code as "self-modification", so I didn't try another way. §9 has the steps for you; poll Q2. |
| **Q5** (mane colour) | **d (other)** | "Create the enum pool with 5 color, different from team color. Save this mane colors as separete material. Before first spawn of characters - select random unique color for each player. Don't change it before match ends." | **[TASK-N27](../Tasks/TASK-N27_ManeColours.md)** does exactly this:<br>• a `ManeColor` enum (White, Black, Brown, Gold, Pink)<br>• five materials in `Assets/Materials/Mane/`<br>• the server shuffles the pool in `SpawnPlayers` before the first spawn, so every player gets a different colour, kept through respawns until the match ends<br>Poll Q1 asks whether you want other colours. |
| **Q6** (HUD readability) | **a:** small task now | — | **[TASK-F9_F14a_N62](../Tasks/TASK-F9_F14a_N62_HudPolish.md) part F14a:** dark 55 % boxes behind HP, Life, Mana, the opponent's Mana, the announcer and the hint line. They follow their texts, so the compact layout needs no extra data. |
| **Q7** (U9) | **a:** close | — | **Closed** (§8). |
| **Q8** (next) | **a:** F9 first | "Simple animation will be fine. Like, at the start - changing alfa from 0 to max when show up by about 0.7 second, and disapere by changing alpha from max to zero by 0.6 sec. Animation time must have variables to change from editor." | **TASK-F9_F14a_N62 part F9:** `CardPreviewController` gets `m_fadeInDuration` = 0.7 and `m_fadeOutDuration` = 0.6, both editable in the Inspector. Only alpha changes, so the big card's click behaviour stays the same. |

**Your *Priorities* sheet:**
- **Now:** F1, F4, F6, all implemented and waiting for the checklist.
- **Next:** F5 and F9. F9 is now in TASK-F9_F14a_N62; F5 still needs your card list.
- **Your two new rows, both Next:**
  - **F15, deck representation:** "how much card still in the deck before new deck creation (Maybe, not a counter itself, but one card fliped by backside and point-like counter when this card was clicked)"
  - **F16, hand representation:** "how much card still in the enemy's hand"
  - Poll Q3 and Q4 ask which form you want; Q5 suggests doing both in one small task next.
- **Later:** unchanged (F2, F3, F7, F8, F10–F14).

---

## 5. The new tasks for Codex

| Task | Covers | Touches | Size |
|---|---|---|---|
| [TASK-N27_ManeColours](../Tasks/TASK-N27_ManeColours.md) | N27 🟢, Q5, N60 (the `PlayerController` `//ToDo`) | `PonyMane.fbx` import settings, five new materials, `ManeColor` (new), `PonyColorScript`, `GameMaster` (+ `.Spawn`), `PlayerController`, `PonyBase.prefab` | M |
| [TASK-F9_F14a_N62_HudPolish](../Tasks/TASK-F9_F14a_N62_HudPolish.md) | F9, F14a, N62 🟢 | `CardPreviewController`, `PlayerUI` (one line), `HudTextBacking` (new), `PlayerUIPrefab`, `PlayerSession` | S–M |

**When and in what order:**
- **Both start after this round's checklist passes and the work is committed.** Their baseline is that commit.
- **They share no file, so the order is free.** I'd do TASK-N27 first: it closes an issue, and it's the one you'll see.
- **No network change:** neither adds an RPC or a NetworkVariable. Both peers still need the same build, because prefabs change.

**What each spec asks Codex to measure first:**
- N27: the "Invalid mane material index." warnings per spawn
- N62: the wrong hint on the baseline

---

## 6. This round's checklist

[ManualChecks/2026-10-24_ManualChecks-Report15.xlsx](../ManualChecks/2026-10-24_ManualChecks-Report15.xlsx) has the same layout as last time: *Read me*, *Checklist*, *Summary*, *Known issues* (with the yellow "Found during the checks" table).

| Section | Rows (required) | Covers |
|---|---|---|
| A. Lost host and leaving | 10 (8) | N59, N61, F1 (cancelled match) |
| B. Messages with the network off | 3 (0, optional) | F1 |
| C. Hints in a match | 6 (5) | F1, N62 (recorded, not failed) |
| D. Draw phase, timers, blocks | 9 (5) | F4, N36, F6 (D4 needs a temporary change, T1) |
| E. Compact menus | 8 (7) | UI7 and your two exceptions |
| G. Release build | 5 (5) | UI7, N59, F1, quiet log |
| H. Editor logs | 2 (1) | all, U2 |

- **For A4,** write the clock time of each Disconnect. I count the Lobby loads in `Editor.log` (N61).
- **Before you commit:**
  - restore the font
  - restore `DefaultNetworkPrefabs.asset` (line endings only)
  - make sure T1 is undone

---

## 7. All open issues

### 🔴 Critical / 🟠 High

None open.

### 🟡 Medium (1)

| ID | Issue | Location | Verdict | Next |
|---|---|---|---|---|
| **N59** | The host quits, crashes or leaves → the client lands in the Lobby with no message | `MultiplayerManager.cs` `HandleLostConnectionToHost` | Confirmed | **Implemented** (TASK-N59_N61_F1); closes with checklist A1, A2, G1.3 |

### 🟢 Low (4)

| ID | Issue | Location | Verdict | Next |
|---|---|---|---|---|
| **N61** | A host's own disconnect ran the cleanup twice and loaded `LobbyScene` twice | `MultiplayerManager.cs` `OnClientDisconnected` | Confirmed | **Implemented**; closes with checklist A4 |
| **N62** | Send on the opponent's turn shows "Choose a target …" instead of "It's your opponent's turn." (§2) | `PlayerSession.cs:1083-1107` | Confirmed | TASK-F9_F14a_N62 |
| N27 | No mane on the pony; every spawn warns "Invalid mane material index." The mesh now exists in the project (§3). | `PonyBase.prefab`; `PonyColorScript.cs:69-86` | Confirmed | TASK-N27 |
| N60 (rest) | Cosmetic leftovers:<br>• the orphan `//ToDo` in `PlayerController.cs:37` (→ TASK-N27)<br>• commented-out blocks in `PlayerCardfWorldArea.cs:47`, `:77`, `:124`, `:131`, `CardControllPackage.cs:11`, `:21`, `NetworkCard.cs:178`, `CardWorldViewManager.cs:764` | as listed | Confirmed | With the next task touching each file (P4 = b) |

### ⚪ Info

- **N36** (Confirmed): "00" could show while a phase went on. **Implemented** (TASK-F4_F6_N36); closes with checklist D2.
- **N29:** the lobby shows the player ID twice. **Parked by you** ("still needed for debugs"). (`UI/LobbyPlayerItemUI.cs:53-65`)
- **Naming and typos** (report only; don't rename):
  - `CardControllPackage`, `PlayerCardfWorldArea.cs`, `SetOponent` / `m_currentOponentNetworkObjectId`, `CheckActiveTaergetSelection`
  - `SingInAnonymous` / `SingOut` / `SingOutAndClearCredentials` / `m_isSingingIn`, `HearthbeatLobby`, `MovePlayeToSpawnPoint`, `ClearupAfterDisconnect`
  - `CardSpecialization.DeffenceSpell`, `m_knowLobbies`, `m_bootsTrap`, `_teamAssing`, `m_phaseAnouncementText`, `#region Valiable`, the GameObject `PhaseAnouncer`
  - log texts `"Packege"`, `"Handle unseccess"`, `"Dissconecting client"`, `"Joinde selected lobby"`, `"Need exatly 2 …"`; the local `_locakPoint`
  - `ConfirmSendButtonUI1` (in `ConfirmSendButton.cs`); `m_startHandCards` (really the per-round draw count); the Confirm binding in `PlayerUIPrefab.prefab` names `ConfirmButtonUI` (it resolves)

### ❓ Unknowns and possible issues

| ID | Question | Status |
|---|---|---|
| U2 | The nested `SpawnPoint` NetworkObject inside `BlockBody` | ❓ No NGO warning in any log so far (checklist H1.2) |
| **U17** | **Sign-out code kept (P2 = a).** `AuthManager.SingOut()` and `SingOutAndClearCredentials()` have no caller. The second has swapped branches: it calls `ClearSessionToken()` while signed in, which the SDK rejects, and `SignOut(true)` while signed out. **Fallback:** fix the branch order when a sign-out button is added, or delete both. | `AuthManager.cs:116-133`. Possible issue, kept by you. |
| **U18** | **No result screen for a lost host (P1 = c).** The client gets the Lobby with a message. If you later want a "Match ended" screen, the fallback is a no-winner Result screen with the same text. | Your choice; revisit with the Server + clients move |

### Feature gaps (not defects)

The new poll's *Priorities* sheet has all 16 with your current ranks:

| # | Gap | Your rank | Status |
|---|---|---|---|
| F1 | On-screen messages | Now | Implemented; checklist A–C |
| F4 | Draw phase on screen | Now | Implemented; checklist D1 |
| F6 | Blocks win rule | Now | Implemented; checklist D4 |
| F9 | Big-card show/hide animation | Now (Q8) | TASK-F9_F14a_N62 |
| F14 | Redraw GUI panels | Later | The quick part (F14a, dark boxes) is in TASK-F9_F14a_N62 |
| F5 | More card effects (discard/combo actions, All* / Random* targets, `ActivityEffect`) | Next | Needs your card list |
| **F15** | **Deck counter** (your new idea) | Next | Form: poll Q3 |
| **F16** | **Opponent's hand counter** (your new idea) | Next | Form: poll Q4 |
| F2 | More settings (audio, graphics, controls, UI scale) | Later | — |
| F3 | Steam login (stub) | Later | — |
| F7 | Spectators | Later | — |
| F8 | Rejoin a running match (`RESEARCH-H6C`) | Later | — |
| F10 | Movement limit per turn | Later | your idea |
| F11 | Player hints | Later | your idea |
| F12 | Hint settings | Later | your idea |
| F13 | Audio and music | Later | your idea |

**UI7** (the compact menus, P12) is implemented and closes with checklist E and G1.2.

**Also known, by design or parked:**
- join by lobby code (M11, parked until Server + clients)
- `RoundEnd` unused and `ResolveCards` transient
- the confirmed target doesn't survive a respawn pause
- with 10 cards the hand overlaps (UI3)

---

## 8. Closed issues

**Closed in this report:**

| ID | How it was closed | Commit / report |
|---|---|---|
| **U9** | NGO `NetworkSceneManager.Dispose` NRE: not seen in two rounds of logs; closed by you (Q7 = a). Reopen if it's seen again. | #15 |

**Closed earlier:**

| ID | How it was closed | Commit / report |
|---|---|---|
| N54 | Board card texts no longer catch UI clicks; right-hand cards open the big card in both windows | `ff8a9dd` (#14) |
| N57 | An early Confirm Turn no longer empties the hand row | `ff8a9dd` (#14) |
| N56 | Confirm and Send are null-safe | `ff8a9dd` (#14) |
| N55 | A missing spawn point or a start exception aborts cleanly | `ff8a9dd` (#14) |
| N58 | The package reader and handler bound the target count | `ff8a9dd` (#14) |
| N32 | The unused owner lookup is gone | `ff8a9dd` (#14) |
| N33 | Dead code, two prefabs and the stale scene override removed | `ff8a9dd` (#14) |
| N34 | Release logs quiet | `ff8a9dd` (#14) |
| N35 | HP and lives written only on change | `ff8a9dd` (#14) |
| Font question | P3 = a: keep the committed font version | #14 |
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

## 9. Folder clean-up and the permission rule

**Deleted this round** (all committed, so git keeps them; your Q1 = a and Q2 = a):

| File | Why |
|---|---|
| `ManualChecks/2026-10-23_ManualChecks-Report13.xlsx` | Reviewed in report #14. I used it as the template for this round's checklist first. |
| `Polls/2026-10-23_Poll-Report13.xlsx` | Reviewed in report #14 |
| `Reports/2026-10-23_IssueReport-13.md` | Report #14 and this one carry the full register |

You had already run report #14's part 1: the three old specs, Codex's old report and reports #1–#12 show as deleted in `git status`.

**Kept on purpose:**

| Kept | Until |
|---|---|
| `Polls/2026-10-24_Poll-Report14.xlsx`, `Reports/2026-10-24_IssueReport-14.md` | **Your commit:** they're untracked, so deleting them now would lose them for good. Reviewed here; they go next round. |
| The three specs of this round's Codex work and its report | The checklist passes and you commit |
| `Guides/GUIDE-N27_PonyManeInBlender.md` | Your commit (untracked). Its job is done; it goes next round. |
| `Research/RESEARCH-UIScaling_Visibility.md` | UI7 is accepted |
| `Research/RESEARCH-H6C_ReconnectMidMatch.md` | F8 (Later) |

**The permission rule (your Q4 = b):**
- **This round my deletes went through:** your poll answer was enough for Claude Code's safety check.
- **Writing the rule was refused** as "self-modification". A new session may refuse the deletes too.
- **To add it yourself,** create `C:\Lesson\FallenStar-Spellcasters\.claude\settings.local.json` with:

  ```json
  {
    "permissions": {
      "allow": [
        "Bash(rm Docs/AI/*)"
      ]
    }
  }
  ```

  Or in Claude Code: `/permissions` → *Allow* → add `Bash(rm Docs/AI/*)` → save to *Project settings (local)*.
- **It covers only `rm` on paths that start with `Docs/AI/`.** I'll keep deleting only committed files there.
- **I added `.claude/settings.local.json` to `.git/info/exclude`,** your local ignore list, so the file is never committed. Nothing else changed.

**`build/` is still yours** (git-ignored, no undo). Report #14's part 2 wasn't run: everything from June and every old verification folder is still there, plus this round's four. In PowerShell, in the project folder:

```powershell
# Keeps only this round's four folders (until the checklist passes). Check the list with -WhatIf first.
$keep = 'n59-verification','f4-f6-n36-verification','ui7-verification','oct24-verification'
Get-ChildItem build | Where-Object { $keep -notcontains $_.Name } | Remove-Item -Recurse -Force -WhatIf
```

This also removes last round's four folders (`hyg2`, `n54`, `n55`, `task-folder`): that work is accepted.

---

## 10. Next steps

1. **Open the project in Unity once.** It imports `PonyMane.fbx` and writes its `.meta`. Leave the import settings alone: TASK-N27 sets them.
2. **Run the checklist** ([2026-10-24_ManualChecks-Report15.xlsx](../ManualChecks/2026-10-24_ManualChecks-Report15.xlsx)): two Editors, then a new build.
3. **Before committing** (*Read me → Before you commit*):
   - restore the font
   - restore `DefaultNetworkPrefabs.asset`
   - check that T1 is undone
4. **Commit everything:**
   - Codex's work
   - the docs (this report, the specs, the checklist, the polls, the guide)
   - `PonyMane.fbx` with its `.meta`
   - the deletions
5. **Tell me** when the checklist is filled. I review it, close what passed, and remove the finished specs and the untracked files listed in §9.
6. **Codex:** TASK-N27, then TASK-F9_F14a_N62 (or the other way round), each with its own report.
7. **The poll:** any time. Q1 (colours) can still change TASK-N27's materials before or after Codex runs it.
8. **`build/`:** §9's command whenever you like.

## Sources

- **Your files:**
  - `Docs/AI/Polls/2026-10-24_Poll-Report14.xlsx`: every sheet and cell, including *Poll* K5:L12 and *Priorities* rows 21–22
  - `C:\Lesson\ManeWork\`: `out/README.md`, `reports/mane_checks.txt`, `reports/alignment_check.txt`, `renders/mane_v6_sheet.png`, `renders/alignment_side.png`
- **Codex:**
  - `Docs/AI/Reports/2026-10-24_N59_N61_F1_F4_F6_N36_UI7_Implementation.md`
  - `build/oct24-verification/`: `WindowsPlayer-build-results.txt`, `release-assembly-check.txt`, `baseline-results.txt`, `fixed-results.txt`, `gameplay-results.txt`, `login-source-results.txt`, `final-status.txt`, three PNGs
- **Diff** against `ff8a9dd`: all 24 changed `Assets` files, the 4 new script files, and the three `Architecture/*.md` files
- **Code read for the new tasks:**
  - `PonyColorScript.cs`, `PlayerController.cs`, `GameMaster.Spawn.cs`, `GameMaster.cs`, `BlockController.cs`, `CardPreviewController.cs`, `PlayerUI.cs`, `PlayerSession.cs`, `GameAnnouncerUI.cs`, `LeaveMatchUI.cs`
  - `PonyBase.prefab`, `PonyBase.fbx.meta`, `PlayerUIPrefab.prefab`, `CardViewUIPrefab.prefab`, `GameMasterPrefab.prefab`, `Assets/Materials/TestMaterial*.mat`
