# Issue Report #8: FallenStar – Spellcasters (re-audit after the 20 tasks from report #7)

- **Date:** 2026-10-20
- **Baseline:**
  - `HEAD` = `391e079` ("L4, N2, N6, L3, N7, N22, N3, N21, N26")
  - **plus the uncommitted working tree:** tasks L1, L2/N31, N30, N17, L6, L7, L9, L10 and N28 (Codex), and N8 (Claude: architecture docs and README). The L10 file deletions are staged; everything else is unstaged.
- **Previous reports:** [#1](2026-10-18_IssueReport.md), [#2](2026-10-18_IssueReport-2.md), [#3](2026-10-18_IssueReport-3.md), [#4](2026-10-18_IssueReport-4.md), [#5](2026-10-19_IssueReport-5.md), [#6](2026-10-19_IssueReport-6.md), [#7](2026-10-20_IssueReport-7.md), and the [manual-check review](../Reviews/2026-10-20_ManualChecks-Report6-7_Review.md), which added N30 and N31.
- **Method:**
  1. Read the agent instructions: `CLAUDE.md`, `AGENTS.md` (now with the N28 font rule), `AI_CONTEXT.md`, the `Docs/AI` documents.
  2. Read the full diff:
     - `b7e7b91..391e079`: commits `efb88c1` (N24, HYG1) and `391e079` (L4, N2, N6, L3, N7, N22, N3, N21, N26)
     - `391e079..working tree` (tasks 11–19)
  3. Compared each change with its spec in `Docs/AI/Tasks/` and with Codex's implementation reports (`2026-10-20_N24_HYG1_…`, `2026-10-20_L4_N2_N6_L3_N7_N22_N3_N21_N26_…`, `2026-10-20_L1_L2_N31_N30_N17_L6_L7_L9_L10_N28_…`).
  4. Re-read all 53 runtime scripts for the N8 docs refresh, and re-checked every open issue from report #7 at its current line.
  5. Checked these assets:
     - the encoding and line endings of all scripts
     - the font asset settings and tables
     - `EditorBuildSettings`, `Packages/manifest.json` and the lock file
     - the `UI_table` key fix
     - script placement by GUID across scenes and prefabs
  6. Read the logs and Codex's verification output:
     - `Editor.log` (16:28)
     - `Player.log` (16:24, from Codex's verification player)
     - `build/task-l1-…-n28-verification/`
- **I didn't run a build or Play Mode.** No code, scenes, prefabs or settings were changed. The only changes are this report, the task-folder cleanup (§7), and two cross-references in the architecture docs.

**This report is the full issue list.** §3 lists every open issue, §5 every closed one. IDs are unchanged. New findings continue from N31, starting at **N32**.

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
| Tasks completed since #7 | **All 20** ✅. 10 committed: N24, HYG1 (N4, N5, N16, N23, L8), L4, N2, N6, L3, N7+N22, N3, N21, N26. 10 in the working tree: L1, L2+N31, N30, N17, L6, L7, L9, L10, N28, N8. Each matches its spec and the owner decisions of 2026-10-20. One accepted deviation: N28 keeps the font-feature table (owner choice), so the asset is still about 67k lines. **No blocking defect.** |
| Compile state | Unity Editor compile of the working tree succeeded. The only compiler warnings in the whole log are 2 × CS0618 in a third-party asset (`JMO Assets/Welcome Screen`), so **the project has 0 warnings of its own**. Codex: Roslyn compile with Editor defines and with release defines, both exit 0. |
| Player build | Codex built a release Windows player of the working tree: **succeeded, 0 errors, 0 warnings**. A menu smoke test in en/ja/ru rendered without missing glyphs. The owner's build of `b7e7b91` ran two matches (U1, answered). |
| Runtime evidence for the 20 tasks | **None from a real two-player match yet.** The owner's manual checks (all Pass) were done on `b7e7b91`, before these tasks. |
| 🔴 Critical / 🟠 High / 🟡 Medium open | **0 / 0 / 0** |
| 🟢 Low open | **5**, of which 4 are new: N32, N33, N34, N35. N27 is parked by the owner. |
| ⚪ Info open | **3** plus the naming list: N29 (parked), N36 (new), and the naming list. |
| Unknowns open | **2**: U2, U9 |
| Task files in `Docs/AI/Tasks/` | All 20 specs removed as done. **The folder is empty** (§7). |

**Overall:** every issue from report #7 that was approved for work is now fixed in code, and the open register is down to Low and Info hygiene plus two parked owner items. The remaining risk is runtime: none of the 20 tasks has been seen in a two-player match, and tasks 11–19 aren't committed yet. The next step is the manual checks in §4, then the owner's commit.

---

## 1. Review of implemented tasks

### 1.1 Committed (`efb88c1`, `391e079`)

| Task | Commit | What changed | Matches spec? | Verdict | Notes |
|---|---|---|---|---|---|
| **N24** | `efb88c1` | `NetworkCard.cs`, `PlayerCardfWorldArea.cs`, `CardWorldViewManager.cs`: cp932 → UTF-8 without BOM | Yes | ✅ **Fixed** | The diff touches only the Russian comment and tooltip lines, which now read correctly. All 53 scripts are valid UTF-8, none has a BOM, and the three files keep CRLF on disk. |
| **HYG1** | `efb88c1` | N4: `m_cardResolveDelay` and `ValidateCardOwner` removed. N5: `$` added. N16: the leaver's suspicious summary is logged in `HandlePlayerLeft` before the removal. N23: `Keyboard.current` null check in `PlayerController.SendMovementInput`. L8: key renamed to `btn_lobbyRefresh`, `m_Id` kept. | Yes | ✅ **Fixed** | No leftover reference to the removed symbols in code, prefabs or scenes. The N23 part in `MultiplayerManager` went away with L10 (the whole `Update` is deleted). CS0414 is gone. |
| **L4** | `391e079` | `BlockController.TeamId` removed. `SetSession` and `GetSpawn` use `m_teamId`. | Yes | ✅ **Fixed** (static) | This removes the Inspector `ArgumentException … Key: Team Id` the owner found. `BlockBody.prefab` unchanged. |
| **N2** | `391e079` | `PlayerSession.Initialize` stores pending values; the server's `OnNetworkSpawn` → `ApplyPendingInit` writes `ClientId`, `TeamId` and lives. Already-spawned sessions apply at once. | Yes | ✅ **Fixed** (static) | Should remove the "NetworkVariable is written to, but doesn't know its NetworkBehaviour yet" warning the owner saw (A1.1). Manual check in §4. |
| **N6** | `391e079` | `PlayerActionPhase`: `IsGameFinished()` → `break` after each card's graveyard move | Yes | ✅ **Fixed** (static) | `EndMatch` clears the rest of the queue. |
| **L3** | `391e079` | `IsGameFinished` is one side-effect-free loop. `FindLostPlayer`, `m_isLocalPlayerLost` and `m_lostClientId` are removed. `IsAnyPlayerDead` / `FindDeadPlayer` skip nulls. | Yes | ✅ **Fixed** | The real external-death signal (`ServerKillPlayerByHazard`) is unchanged. |
| **N7 + N22** | `391e079` | `GetCharacter` uses `TryGetComponent` and doesn't write the ID. `PlayerController.Init` runs in the server's `OnNetworkSpawn` (before the spawn message). The materials still wait for the session. | Yes | ✅ **Fixed** (static) | `m_inputQueue` initialization moved, then the queue was deleted by L10. |
| **N3** | `391e079` | New `GameMaster.m_isInputLocked`, mirrored by the server in `Update` only on a change, reset on new game and cleanup. `IsInputLocked()` uses the server's own flags on the server and the NetworkVariable on clients. | Yes | ✅ **Fixed** (latent issue, as the spec said) | `m_isResolvingCards` is set and cleared inside one frame unless a death pause happens, so clients mostly see the lock during respawn waits. That's expected. |
| **N21** | `391e079` | `ConfirmTurnServerRpc(intendedPhase, …)`. `IsConfirmForCurrentPhase` accepts CardSelection→CardSelection, RespawnWait→RespawnWait, PlayerAction/ResolveCards→the same family. A mismatch is ignored (a debug log only). | Yes (no grace period: "ignore" kept) | ✅ **Fixed** (static) | Peers must run the same build (the RPC signature changed). |
| **N26** | `391e079` | New `m_selectionPhaseId`, incremented at each `CardSelectionPhase` start and reset on new game. `PlayerUI.OnSelectionPhaseChanged` clears both local lists and rebuilds. Subscribed at bind, removed in `ClearLocalUI` and `OnDestroy`. | Yes (owner: clear each round) | ✅ **Fixed** (static) | A respawn pause doesn't change the ID, so selections survive it. On the host the change fires locally at once; on a client it arrives with the phase change. |

### 1.2 Working tree (not committed)

| Task | What changed | Matches spec? | Verdict | Notes |
|---|---|---|---|---|
| **L1** | `m_timer` replaced by private `m_phaseElapsed` / `m_phaseDuration` and `NetworkVariable<double> m_phaseEndServerTime`. `StartPhaseCountdown` at RoundBreak, drawing, CardSelection, PlayerAction, respawn, a selection reset and a pause resume (with the time already used). `ClearPhaseCountdown` in `EndMatch`, `AbortMatch`, `StartNewGame`. `GetPhaseSecondsLeft()` (ceiling, clamped at 0) used by `GameAnnouncerUI`. | Yes | ✅ **Fixed** (static) | No per-frame network write is left. The server loop still counts frame time while the display uses server time (**N36**, Info). |
| **L2 + N31** | `Random.Range(0, 3)`. The `CreateDeck` / `FillCardSection` comments now describe the three mixes and the 84-card result. | Yes (owner: 3 mixes, sets of 4) | ✅ **Fixed** | Codex built 300 decks per configuration: 92 / 117 / 91 of the three mixes, all 84 cards. |
| **N30** | `RefreshCurrentLobby` shares the in-flight refresh for the lobby, returns the cache for 1.5 s (`m_minLobbyRefreshInterval`), and a second chain keeps `GetLobbyAsync` calls one at a time and 1.5 s apart **across lobby changes** (owner-approved during implementation). A stale result for another lobby is dropped. `RateLimited` logs a warning. All reset points also reset the per-lobby cache. | Yes | ✅ **Fixed** (static; mitigation) | The chain can't fault: `RefreshCurrentLobbyCore` catches everything. This throttles the likely cause of the SDK NRE; it doesn't prove another HTTP failure can't produce it (the M10 catch still handles it). |
| **N17** | `SessionManager.OnClientDisconnected` clears the HUD only when the local client disconnected | Yes | ✅ **Fixed** (static) | |
| **L6** | `GameAnnouncerUI` caches one `LocalizedString` per key and skips frames where phase, seconds, target, team, local flag and locale are unchanged. `CardWorldViewManager.Update` unregisters departed, despawned or destroyed sessions first (reused list), and the null branch of `UnregisterSession` now really removes the entry. `PlayerUI` clears the HUD once (`m_isLocalUiCleared`). `LobbyControlUI` applies panels on a change only. | Yes | ✅ **Fixed** (static) | `LeaveMatchUI` needed no change. Outside the spec's scope: `PlayerUI` still formats HP and lives every frame (**N35**). |
| **L7** | `CardViewBase` keeps the last `CardData` and calls `ApplyTexts` on `SelectedLocaleChanged` (subscribed in `OnEnable`, removed in `OnDisable`). `CardViewUI` / `CardViewWorld` moved their name/description reads into `ApplyTexts`. | Yes | ✅ **Fixed** (static) | Neither derived class declares its own `OnEnable`/`OnDisable`, so the base subscription isn't hidden. |
| **L9** | New `DebugModeGuard.Allow`, the first statement of `Awake` in all 16 classes with a `m_debugMode` flag (6 got a new `Awake`) | Yes (owner: force off in release) | ✅ **Fixed** | Codex compiled with release defines and checked all 16 statically. About 30 log lines aren't behind any flag (**N34**). |
| **L10** | 81 tracked files deleted: `26.06.19/`, `Assets/_Recovery/`, `SampleScene`, `MultiplayerScene`, `SpawnPanelUI`, `DisconnectMenuUI`, `DisconnectButtonUI`, `LocalizedTMPFormat`, `TargetDataBackup`, `ToPlayerControlPackage`, `test.md`. Removed code: `ResolvePlayerCardsPhase`, the commented `CardMessageQueueUpdate`, the `SendToClientRpc` echo, `ReceiveValidatedInput` and its queue, `DisconnectClient`, the Tab toggle and `CheckRef`. VisualScripting removed; both heal conversions use `System.Convert.ToInt32`. Two disabled scene entries removed from the build list. | Yes (owner: delete all four groups) | ✅ **Fixed** | `m_lobbyCode` / `m_lobbyCodePanel` and the parked M11 block are kept, as specified. Codex's rounding check gave identical results for all 7 values. The six enabled scenes keep their order and have 0 missing scripts. Dead code the spec didn't list → **N33**. |
| **N28** | Font asset: `m_ClearDynamicDataOnBuild: 1`, glyph and character tables empty, one 1×1 atlas. The source font, dynamic mode, 1024 size, padding and render mode are unchanged. `AGENTS.md` has the commit rule. | Yes, with one **accepted deviation**: the owner chose to keep TMP's font-feature table, so the file is 67,200 lines, not "much smaller" (acceptance criterion 1) | ✅ **Fixed** | Any Editor run with text refills the asset. Before committing, restore it to the cleared working-tree state, not to `HEAD` (that would undo the flag). Checked now: the tables are still empty. |
| **N8** | Claude rewrote `Architecture.md`, `GameLoop.md` and `Dependencies.md` for the current code. `README.md`: the win rule, the scene flow, two-player testing notes and documentation links. | Yes | ✅ **Fixed** | The docs describe the working tree, so they're only fully correct once tasks 11–19 are committed as they are. |

**Assets checked:**
- `EditorBuildSettings`: six enabled scenes, in order.
- `packages-lock.json`: only the VisualScripting entry removed.
- `UI_table`: 69 keys, en/ru/ja complete, no key with a trailing tab.
- Every component script is used by an enabled scene or prefab.
- `Assets/Editor` (Codex's temporary helper) is gone. The CS1061 errors in `Editor.log:6853-6860` came from that helper before it was fixed and removed, as Codex reported.

---

## 2. New findings and updates

| ID | Level | Issue | Location | Evidence | Verdict |
|---|---|---|---|---|---|
| **N32** | 🟢 Low | **`ProcessActivity` reads the card owner's character without a null check, and never uses it.** `GetSessionByID(cardOwnerId).GetCharacter()` would throw if the owner session were gone. Today it can't be: `CheckActivity` checks the same owner just before, and a disconnect stops the loop (`HandlePlayerLeft`) before the session despawns. If it ever threw, `RunGuarded` would abort the match. | `GameMaster.Processing.cs:139` | Code: `_ownerPlayer` isn't read after `:139` | Confirmed (latent). Delete the line, or null-check it. |
| **N33** | 🟢 Low | **Dead code left after L10** (L10 deleted only what its spec listed). Safe to delete:<br>• `GameMaster.m_gameLoopCoroutine`, `m_phasesTimerCoroutine` (never used); `m_cardViewWorldPrefab` (serialized, never read)<br>• `PlayerController.m_isDead`: a NetworkVariable that is never written (only read in commented-out code), yet still sent with every character spawn; `GetCurrentSpeed()` (no caller)<br>• `PlayerSession.ClearHand()` (private, no caller), `ClearBlocks()`, `RemoveCard()`<br>• `NetworkCard.SetFaceUpServerRpc`, `SetRotatedServerRpc`, `DespawnCardServerRpc` (no caller; cards are server-owned, so client calls would be rejected anyway)<br>• `CardPreviewController.TogglePreview`, `MultiplayerManager.KnowLobbies`, `SceneFlowManager.LoadLoginScene`, `GameSettingsPrefs.HasLanguageCode`<br>• `Assets/Prefab/BootsTrap.prefab` and `MultiplayerManager.prefab`: old copies of the `AppRoot` managers, used by no scene (`MultiplayerManager.prefab` still has `m_debugMode: 1`)<br>**Owner's call (future API?):** `AuthManager.SingOut()` and `SingOutAndClearCredentials()` have no caller. The second one looks inverted: it calls `ClearSessionToken()` when the player **is** signed in, which the Authentication SDK refuses. | `GameMaster.cs:56`, `:104-105`; `PlayerController.cs:21`, `:118`; `PlayerSession.cs:374`, `:583`, `:748`; `NetworkCard.cs:73-86`; `CardPreviewController.cs:21`; `MultiplayerManager.cs:65`; `SceneFlowManager.cs:45`; `GameSettingsPrefs.cs:20`; `AuthManager.cs:116-133`; the two prefabs | grep: no caller in scripts, scenes or prefabs | Confirmed. The `SingOutAndClearCredentials` branch order is Plausible (SDK behavior not run). |
| **N34** | 🟢 Low | **About 30 log lines aren't behind any debug flag, so they also print in release builds.** Some run often:<br>• "Process called" for every card activity<br>• "Rpc called" for every confirm<br>• "RepairCalled" for every card-data RPC on every client<br>• "DespawnBlock" for every block at match end (50)<br>• "Card clicked …" / "Card deselected" for every hand click<br>• "Applied … material index" per character spawn<br>• "DrawPhase started/ended" every round<br>• 9 lines per lobby search<br>• "SetLobbyCode" / "Lobby code"<br>Also:<br>• `PlayerUI.OnCardClicked` / `OnCardAlterClicked` read `cardView.name` **before** their null check. This is latent, because the event always passes the view.<br>• A debug message is missing `$`: `"Confirm ignored. Current phase{ }"`. | `GameMaster.Processing.cs:115`, `:138`; `PlayerSession.cs:229`; `NetworkCard.cs:164`; `BlockController.cs:38`; `CardViewUI.cs:97`, `:103`; `PlayerUI.cs:362`, `:379`, `:398`, `:415`; `PonyColorScript.cs:63`, `:83`; `GameMaster.GameLoop.cs:485`, `:520`; `MultiplayerManager.cs:765-834`, `:1541-1542`; `GameMaster.PackageManaging.cs:258` | Code | Confirmed. Gate them behind `m_debugMode` (or delete them). Keep the warnings and errors. |
| **N35** | 🟢 Low | **`PlayerUI` formats HP and lives every frame.** `Update` calls `m_labelHp.GetLocalizedString(m_hp)` and `m_labelLife.GetLocalizedString(m_life)` every frame. That means two Smart-format string builds, boxing and an argument array, even when nothing changed. L6 made the mana labels and the announcer change-only, but HP and lives weren't in its scope. | `UI/PlayerUI.cs:156-170` | Code | Confirmed. Cache the last HP, lives and locale like `UpdateManaTexts` does. |
| **N36** | ⚪ Info | **The countdown and the server loop use different clocks.** The loop adds `Time.deltaTime` (scaled, capped at `Time.maximumDeltaTime`). The deadline uses `NetworkManager.ServerTime` (real time). After a server hitch longer than about 0.33 s, or during drawing (each draw also waits one extra frame), the phase ends a little later than the countdown shows, so clients can see "00" briefly. | `GameMaster.cs:124-141`; phase loops in `GameMaster.GameLoop.cs` | Code reasoning | Plausible (cosmetic). A fix would end each phase on the deadline itself (`ServerTime >= m_phaseEndServerTime`). Only worth it if it's noticed in play. |

**Updates to existing items:**
- **N30** (from the review): fixed by the throttle (working tree). The manual check in §4 should show no `TryCatchRequest` NRE over several create/join/start cycles.
- **N27:** still open. The owner parked it: the mane model doesn't exist yet. Every character spawn still logs "Invalid mane material index." (`PonyColorScript.cs:87`).
- **N29:** still open; the owner parked it ("later").
- **D.5 of the manual checklist** (the language survives a restart) is still not proven.
  - The newest `Player.log` (16:24) comes from Codex's verification player (`Mono path … task-l1-…-verification/windowsplayer`). `Player-prev.log` (13:17) is the owner's run from the review.
  - Codex's smoke test switched languages but then restored English, so it didn't test a restart.
- **Docs:** the architecture docs now cover this report's IDs:
  - `GameLoop.md` §6 GL1 = N32
  - `Architecture.md` §3.4 = N34
  - `Architecture.md` A6 = part of N33

---

## 3. All open issues

### 🔴 Critical / 🟠 High / 🟡 Medium

None open.

### 🟢 Low (5)

| ID | Issue | Current location | Verdict |
|---|---|---|---|
| **N32** | `ProcessActivity` reads the owner's character without a null check and never uses it (new, §2) | `GameMaster.Processing.cs:139` | Confirmed (latent) |
| **N33** | Dead code and unused prefabs left after L10 (new, §2) | see §2 | Confirmed |
| **N34** | About 30 log lines print in release builds; two read `cardView.name` before the null check; one message is missing `$` (new, §2) | see §2 | Confirmed |
| **N35** | `PlayerUI` formats HP and lives every frame (new, §2) | `UI/PlayerUI.cs:156-170` | Confirmed |
| N27 | The pony mane color is never applied: `maneObject` isn't assigned, and every spawn logs "Invalid mane material index." **Parked by the owner** until the mane model exists. | `Assets/Prefab/BoardPrefab/PonyBase.prefab:368`; `InGame/PonyColorScript.cs:70-88` | Confirmed |

### ⚪ Info

- **N36:** the countdown and the loop use different clocks (new, §2). Plausible, cosmetic.
- **N29:** the lobby shows the player ID twice: the short ID as the name (`m_playerNameText`), the full ID under it (`m_playerIdText`). **Parked by the owner.** (`UI/LobbyPlayerItemUI.cs:53-63`)
- **Naming and typos** (report only, don't rename):
  - `CardControllPackage`, `PlayerCardfWorldArea.cs`, `SetOponent` / `m_currentOponentNetworkObjectId`, `CheckActiveTaergetSelection`
  - `SingInAnonymous` / `SingOut` / `SingOutAndClearCredentials` / `m_isSingingIn`, `HearthbeatLobby`, `MovePlayeToSpawnPoint`, `ClearupAfterDisconnect`
  - `CardSpecialization.DeffenceSpell`, `KnowLobbies` / `m_knowLobbies`, `m_bootsTrap`, `_teamAssing`, `m_phaseAnouncementText`, `#region Valiable`
  - the log texts `"Packege"`, `"Handle unseccess"` and `"Tried ShowPrew"`; the local variable `_locakPoint`
  - `ConfirmSendButtonUI1` (in `ConfirmSendButton.cs`); `m_startHandCards`, which is really the per-round draw count

### Feature gaps (not defects, listed so they aren't mistaken for bugs)

- **Join by lobby code is parked** (M11, owner 2026-10-19). It's commented out in `MultiplayerManager`, and `m_lobbyCode` / `m_lobbyCodePanel` stay serialized for it. §10 of the old M11 spec (git history) lists what to fix before restoring it.
- The language can be changed only in `SettingsScene` (from the main menu). There are no other settings (audio, graphics, controls).
- Steam login is a stub (`AuthManager.SignInSteam`), although `LoginScene` has a button wired to it.
- `GamePhase.DrawCards` and `RoundEnd` are never set, and `ResolveCards` is only transient. During draws the announcer shows "Round break" with a short second countdown.
- The `DiscardCard` / combo actions, the `All*` / `Random*` targets and `ActivityEffect` have no effect.
- Destroying all enemy blocks doesn't win by itself. A player with no blocks is eliminated on their next death (the README now says so).
- `SessionRole.Spectator` exists, but nothing assigns it.
- No player-facing messages for lobby or network failures (M10 Q1, N14 Q1), for an ignored Send (N20), for an ignored late confirm (N21), or for a deck refill (H2 Q1).
- The confirmed target of an action card doesn't survive a respawn pause (N20 Q1, by design). Card selections do survive it (N26).
- `SessionManager` has 4 slots, but the scene supports only teams 0 and 1.

### ❓ Unknowns: open

| ID | Question | Status |
|---|---|---|
| U2 | Nested `SpawnPoint` NetworkObject inside `BlockBody` | ❓ No NGO warning in any log so far |
| U9 | NGO `NetworkSceneManager.Dispose` NRE on Play Mode stop, Editor quit or domain reload | ❓ Inside NGO, during teardown; probably harmless. Not seen in the latest log. |

---

## 4. Pending manual checks

Static review and Codex's harnesses can't replace these. Both instances must run the **same** build: L1, N3, N26 add NetworkVariables, N21 changes the confirm RPC, and L10 removes an RPC.

**A. Left from the last checklist**
- **D.5:** start the player build, pick 日本語, quit, start again. The language must still be Japanese.

**B. Tasks 1–10 (committed in `efb88c1`, `391e079`)**
- **N2:** start a match as host and as client. The "NetworkVariable is written to, but doesn't know its NetworkBehaviour yet" warning doesn't appear.
- **L4:** in Play Mode, select a block in the Hierarchy. No `ArgumentException … Team Id` when the Editor gains focus.
- **N22:** the client sees 100/100 HP on its character from the first frame.
- **N6:** with two cards to play, kill the opponent with the first one. The second card doesn't resolve, and the result screen appears.
- **N26:** select cards but don't confirm. The next round starts with no selection. A respawn pause inside one CardSelection keeps the selection.
- **N21:** confirm exactly as CardSelection ends. Nothing happens: no turn ends, and no `[Suspicious]` line.
- **N3** (regression only; the phase already blocks input during a respawn wait): movement, card selection and targeting still work in their own phases, on the host and on the client.
- **L8:** the lobby list's Refresh label is localized in en/ru/ja.

**C. Tasks 11–19 (working tree).** Details are in Codex's report §6.
- **L1:**
  - The countdown matches on both instances within ±1 s in every phase.
  - After a respawn, the interrupted phase resumes with its remaining time.
  - The over-budget timer reset needs the tamper test (C4/T13).
- **L2/N31:** over a few matches, all three mixes appear. With debug on, the refill log shows 84 cards.
- **N30:** 5 create/join/start/leave cycles with the Editor as host and 5 with the player build as host.
  - No `TryCatchRequest` NRE.
  - The count and the Start button follow joins and leaves within about 2 s.
- **N17:** when the client forfeits, the host's HUD stays until `ResultScene`.
- **L6:** in the Profiler, no announcer allocations in an unchanged second. After the opponent leaves, no `FindObjectsByType` every frame.
- **L7:** during a match, switch en → ru → ja with Localization Scene Controls. Hand and board card texts follow. No `MissingReferenceException` after cards are played or destroyed.
- **L9:** in a release build, no gated debug lines (for example "Waiting for Respawn"). In the Editor, the flags still work.
- **L10:**
  - Movement W/A/S/D works.
  - HealingHooves on yourself at full HP gives 100 → 115 (overflow 30 × 0.5). From 50 HP it gives 80.
  - Leave, forfeit and Result work.
  - Package Manager shows no VisualScripting and no errors.
- **N28:** in a player build, Japanese and Russian render in the match HUD, on the cards and on Result. After Editor checks, restore the font asset to its cleared state before committing.

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
| **N2** | Session values written in the server's `OnNetworkSpawn` | `391e079` (**#8**) |
| **N3** | Replicated input lock for client-side gates | `391e079` (**#8**) |
| **N4** | Dead `ValidateCardOwner` and `m_cardResolveDelay` removed; no own compiler warning left | `efb88c1` (**#8**) |
| **N5** | Log string interpolated | `efb88c1` (**#8**) |
| **N6** | Finish checked after each resolved card | `391e079` (**#8**) |
| **N7** | `GetCharacter` uses `TryGetComponent`, doesn't write the ID | `391e079` (**#8**) |
| **N8** | Architecture docs and README refreshed | working tree (**#8**) |
| N9 | Superseded by MANA2 | #5 |
| N10, N11, N13 | Closed when H5 was finished | #4 |
| **N12** | Withdrawn: false positive. The runner yields one frame per wait. | review 2026-10-20 (**#8**) |
| N14 | Host returns to the lobby when a match can't start | `fe2d4fb` (#6) |
| N15 | Number not used | #4 |
| **N16** | The leaver's suspicious summary is logged on disconnect | `efb88c1` (**#8**) |
| **N17** | Host HUD kept when the opponent forfeits | working tree (**#8**) |
| N18 | Deaths are handled in every phase | `fe2d4fb` (#6) |
| N19 | Leave button with confirmation, counts as a defeat | `fe2d4fb` (#6) |
| N20 | Safe Send; the target rule is shared with the server | `a0df515` (#7) |
| **N21** | Confirm carries the intended phase; mismatches are ignored | `391e079` (**#8**) |
| **N22** | Character HP set in the server's `OnNetworkSpawn` | `391e079` (**#8**) |
| **N23** | `Keyboard.current` null check (`PlayerController`); the `MultiplayerManager` reader was deleted | `efb88c1`, working tree (**#8**) |
| **N24** | Three scripts converted to UTF-8 | `efb88c1` (**#8**) |
| N25 | Intended (owner, 2026-10-19) | #5 |
| **N26** | Unconfirmed selection cleared at each new selection phase (owner choice) | `391e079` (**#8**) |
| **N28** | Font dynamic data cleared on build; commit rule in `AGENTS.md` (feature table kept by owner choice) | working tree (**#8**) |
| **N30** | Lobby refresh throttled: one request at a time, 1.5 s apart, across lobby changes | working tree (**#8**) |
| **N31** | Deck size documented as 84 (with L2) | working tree (**#8**) |
| **L1** | Countdown from a replicated deadline; no per-frame network writes | working tree (**#8**) |
| **L2** | All three deck mixes reachable | working tree (**#8**) |
| **L3** | `IsGameFinished` is pure and allocation-free | `391e079` (**#8**) |
| **L4** | Duplicate `BlockController.TeamId` removed (also fixes the Inspector exception) | `391e079` (**#8**) |
| L5 | `GameAnnouncerUI` maps every existing phase | #4 |
| **L6** | Announcer, world views, HUD clear and lobby panels change-only (HP/lives → N35) | working tree (**#8**) |
| **L7** | Card texts refresh on a locale change | working tree (**#8**) |
| **L8** | `btn_lobbyRefresh` key without the tab | `efb88c1` (**#8**) |
| **L9** | Debug flags forced off in release builds | working tree (**#8**) |
| **L10** | Dead code, legacy scenes, backups and VisualScripting removed (leftovers → N33) | working tree (**#8**) |
| **U1** | The player build compiles and runs (owner's build of `b7e7b91`; Codex's release build of the working tree) | review 2026-10-20, **#8** |
| U3, U4 | Answered (null `NetworkBehaviourReference` is safe; `DontDestroyWithOwner: 0`) | #3 |
| **U5** | GUID `e651dbb3…` is NGO's own `NetworkPrefabsList` script | review 2026-10-20 |
| **U6** | Explained: each Editor logs its own `GameScene` load in the shared log | review 2026-10-20 |
| U7 | Normal end reaches `ResultScene` on both instances | #4 |
| **U8** | `Shutdown()` leaves no objects behind (owner check A12) | review 2026-10-20 |
| **U10** | The forfeit works at runtime (`OpponentDisconnected` seen, A11 passed) | review 2026-10-20 |
| U11 | Not reproduced | #7 |
| **U12** | Mana replicates to a remote client, and the rejection reaches it (owner check A1) | review 2026-10-20 |
| U13 | A second lobby and match work after result → main menu | #6 |

Tasks completed without an issue ID: MANA1–MANA3 (`ade4aed`, `5eec046`, `058fa63`), SEC1 (`f08e854`) and HYG1 (`efb88c1`, a bundle of N4, N5, N16, N23 and L8).

---

## 6. Suggested next steps (in order)

1. **Run the manual checks in §4**, at least groups B and C, with two instances on the same build. A new Excel checklist like the last round's can be generated on request.
2. **Commit the working tree** once group C passes. Restore the font asset to its cleared state first (`AGENTS.md` rule). The staged L10 deletions and the unstaged edits belong together; committing only one part would break the build (for example `MultiplayerManager` without the deleted `DisconnectMenuUI`).
3. **One small hygiene spec (all Low), on the owner's go-ahead:**
   - N32: delete the unused line
   - N33: delete the dead code and the two old prefabs. Owner's call on the sign-out methods.
   - N34: gate the logs, fix the null-check order and the `$`
   - N35: change-only HP and lives
4. **Parked by the owner:** N27 (until the mane model exists), N29 ("later").
5. **Optional:** N36, only if "00" is noticed in play.

---

## 7. Task folder cleanup

I removed all 20 specs from `Docs/AI/Tasks/`. Each was implemented, matches its spec, and has no open blocking defect (§1). Each was added in commit `efb88c1` and can be read with `git show efb88c1:Docs/AI/Tasks/<file>`. Their manual checks are in §4.

| Removed | Reason |
|---|---|
| `TASK-N24_Utf8Conversion.md` | Fixed (`efb88c1`) |
| `TASK-HYG1_SmallFixes.md` | Fixed (`efb88c1`): N4, N5, N16, N23, L8 |
| `TASK-L4_RemoveDuplicateBlockTeamId.md` | Fixed (`391e079`) |
| `TASK-N2_SessionValuesAfterSpawn.md` | Fixed (`391e079`) |
| `TASK-N6_StopResolvingAfterMatchEnd.md` | Fixed (`391e079`) |
| `TASK-L3_PureIsGameFinished.md` | Fixed (`391e079`) |
| `TASK-N7_N22_CharacterLookupAndStartHp.md` | Fixed (`391e079`) |
| `TASK-N3_ReplicatedInputLock.md` | Fixed (`391e079`) |
| `TASK-N21_PhaseTaggedConfirm.md` | Fixed (`391e079`) |
| `TASK-N26_ClearSelectionEachRound.md` | Fixed (`391e079`) |
| `TASK-L1_CountdownTimer.md` | Fixed (working tree, not committed) |
| `TASK-L2_N31_DeckComposition.md` | Fixed (working tree, not committed) |
| `TASK-N30_LobbyRefreshThrottle.md` | Fixed (working tree, not committed) |
| `TASK-N17_HostHudOnForfeit.md` | Fixed (working tree, not committed) |
| `TASK-L6_PerFrameWork.md` | Fixed (working tree, not committed). The out-of-scope remainder is N35. |
| `TASK-L7_CardTextLocaleRefresh.md` | Fixed (working tree, not committed) |
| `TASK-L9_DebugModeOffInRelease.md` | Fixed (working tree, not committed) |
| `TASK-L10_DeadCodeCleanup.md` | Fixed (working tree, not committed). Leftovers are N33. |
| `TASK-N28_FontAtlasClearOnBuild.md` | Fixed (working tree, not committed), with the owner-approved size deviation |
| `TASK-N8_DocsRefresh.md` | Done by Claude (working tree, not committed) |

**`Docs/AI/Tasks/` is now empty.** No new specs were written; §6 lists the candidates. The Codex implementation reports, the research documents, the manual checklists and the review are kept as reference material.

## Sources

- **Diffs:** `git show efb88c1`, `git show 391e079`, `git diff HEAD` (working tree, staged and unstaged)
- **Code read in full:** all 53 scripts under `Assets/Scripts` (working tree)
- **Assets:**
  - `MPLUSRounded1c-Medium SDF.asset` (flags and tables)
  - `EditorBuildSettings.asset`, `Packages/manifest.json`, `packages-lock.json`
  - `UI_table` (shared, en, ru, ja), `Cards` tables
  - `GameMasterPrefab`, `PonyBase`, `BootsTrap` and `MultiplayerManager` prefabs
  - script GUIDs across `Assets/Scenes` and `Assets/Prefab`
- **Logs:**
  - `%LOCALAPPDATA%\Unity\Editor\Editor.log` (2026-10-20 16:28): Codex's session. Last compile succeeded; only third-party CS0618 warnings; the CS1061 lines are from the removed temporary helper.
  - `…\LocalLow\DefaultCompany\FallenStar-Spellcasters\Player.log` (16:24, Codex's verification player) and `Player-prev.log` (13:17, the owner's run)
- **Codex output:** `build/task-l1-l2-n31-n30-n17-l6-l7-l9-l10-n28-verification/`
  - `build-results.txt`: release player succeeded, 0 errors, 0 warnings
  - compile exit files: 0
  - harness: 15 scenarios × 2 define sets
  - `rounding-results.txt`: 7/7 identical

  The harnesses use Unity, NGO and SDK doubles; they aren't two-player Play Mode results.
