# Issue Report #4: FallenStar – Spellcasters (re-audit after H5, H6, H4, H8, H9, SEC1)

- **Date:** 2026-10-18
- **Baseline:** `HEAD` = `f08e854` ("h4,h8,h9 and sec1"). The working tree was clean at the start of the audit.
- **Previous reports:** [#1](2026-10-18_IssueReport.md) (`1f6d2f1`), [#2](2026-10-18_IssueReport-2.md) (`44d858f`), [#3](2026-10-18_IssueReport-3.md) (`ad3a46f`)
- **Method:**
  1. Read the agent instructions: `CLAUDE.md`, `AGENTS.md`, `AI_CONTEXT.md`.
  2. Read the full code and asset diff `ad3a46f..f08e854` (commits `d44b882` H5/H6, `f08e854` H4/H8/H9/SEC1).
  3. Compared each diff with its task spec in `Docs/AI/Tasks/`.
  4. Re-checked the open items from reports #1–#3 at their current lines.
  5. Read `%LOCALAPPDATA%\Unity\Editor\Editor.log` (last written 16:04, after `f08e854`) and the NGO source (`com.unity.netcode.gameobjects@beeaefb722f7`, read only).
- **I didn't run a build or Play Mode.** No code, scenes, prefabs or settings were changed. The only changes are this report and removing completed task files (§6).

The severity levels and verdicts are the same as in reports #1–#3. New findings continue the numbering from report #3, starting at N14.

## Summary

| | |
|---|---|
| Tasks completed since #3 | H5 ✅, H6 ✅, H4 ✅, H8 ✅, H9 ✅, SEC1 ✅ |
| Tasks not started | **H10** (result menu), **MANA1–3**, **C6** Step 1 |
| Compile state | 0 `error CS`. Only the known warnings CS0114 (N1) and CS0414 (N4) (`Editor.log:5899-5941`) |
| Runtime evidence | A normal match end reached `ResultScene` on both sides with the correct result (`Editor.log:3585, 4783, 5768`) |
| 🟡 New Medium | 1: N14 (disconnect before the match loop starts) |
| 🟢 New Low | 1: N16 (the leaving player's suspicious summary is lost) |
| 🟠 High still open | **H10 (soft lock in `ResultScene`)**, H2, H3 |
| Resolved since #3 | H4, H5, H6, H8, H9, M7 (via H4), N10, N11, N13, U7 (normal end) |

**Overall:** all six implemented tasks match their specs. The most visible problem now is **H10**. The result scene loads correctly, but it has no UI and no button, so after every match both players are stuck until they close the game. H2 and H3 are the remaining High gameplay defects. H1 now catches them, so they abort the match instead of freezing it.

---

## 1. Review of implemented tasks

| Task | Commit | What changed | Matches spec? | Verdict | Notes |
|---|---|---|---|---|---|
| **H5** (rest) | `d44b882` | `ResultScene` added to `EditorBuildSettings.asset` after `GameScene`. `Phase.GameOver` added to `UI_table` with en "Game over", ru "Игра окончена", ja "ゲーム終了". | Yes (N10 and N11 are closed) | ✅ **Fixed**, seen at runtime | `Editor.log:4773-4783` (client: "You lost") and `:5758-5768` (host: "You won"). The scene loads without errors. |
| **H6** | `d44b882` | `GameMaster.HandlePlayerLeft` (`GameMaster.Player.cs:6-49`) plus a hook at the top of `SessionManager.OnClientDisconnected` (`SessionManager.cs:158-162`) | Yes, step by step. The guards (`IsServer`, null, no running loop, `None`/`GameOver`, not in `m_players`) come first. Then the coroutine is stopped **before** the lists change. The departed session is removed, flags are reset, the winner is the remaining player with `TeamId != -1`, and it falls back to `AbortMatch("player_left_no_winner")`. | ✅ **Fixed** (static) | **Verified in NGO:** `OnClientDisconnectFromServer` despawns the departed objects and calls `Object.Destroy` (`NetworkConnectionManager.cs:1362-1409`) **before** it invokes the callback (`:1558`). `Destroy` is deferred to the end of the frame, so `_leaving != null` is still true inside the hook and the hook fires. `HandlePlayerLeft` only uses the C# reference, as required. **No forfeit run appears in the log** (see U10). |
| **H4** | `f08e854` | `CheckActivity` (`Processing.cs:93-160`): the owner null check now runs before the opponent lookup, and the four target rules were rewritten as in spec §4.3 | Yes, exactly | ✅ **Fixed** (static) | This also closes **M7** (null targets used to pass validation). |
| **H8** | `f08e854` | `CardData.OnValidate` uses `Array.Resize` and no longer resets slots 1–3; `Reset()` added | Yes | ✅ **Fixed** (static) | Existing card assets keep their activities 1–3. Re-open a multi-activity card in the Inspector once to confirm. |
| **H9** | `f08e854` | Deleted `_nonUniqueList[i] = uniqueList[_randomSeed];` in the `UltraRare` case | Yes | ✅ **Fixed** (static) | |
| **SEC1** | `f08e854` | New `SuspiciousActivityLog.cs` (enum, counters, ring buffer of 20, summary). `PlayerSession.ReportSuspicious` / `GetSuspiciousLog`. 4 report points. Match summary in `EndMatch` and `AbortMatch`. | Yes. Exactly the 4 tracked places from §4.3. No accept/reject logic was changed. No client-visible output. | ✅ **Fixed** (static) | `ForeignCardPackage` is reported only after the sender is confirmed to be the active player (`PackageManaging.cs:108-115`), so it always targets the right session. The optional EditMode test (§8) wasn't added. The summary loop is duplicated in `EndMatch` and `AbortMatch` (style only). See N16. |

---

## 2. New findings

| ID | Level | Issue | Location | Evidence | Verdict |
|---|---|---|---|---|---|
| **N14** | 🟡 Medium | **A disconnect before the match loop starts leaves the host stuck in `GameScene`.** H6 deliberately ignores disconnects while `m_gameCoroutine == null`. But between the `GameScene` load and `StartNewGame`, a client that leaves causes one of these: (a) `StartGameAfterSceneReady` waits 10 s for 2 sessions, logs an error and `yield break`s; or (b) `StartNewGame` finds `m_players.Count != 2`, logs a warning and returns. In both cases nothing leads the host back to the lobby or to a result. The match never starts, and the host only has the disconnect menu, if any. | `MultiplayerManager.cs:1224-1266` (`StartGameAfterSceneReady`); `GameMaster.cs:~548` (`StartNewGame`, count check); `GameMaster.Player.cs:11-13` (H6 guard) | Code | Confirmed (code). Not seen at runtime. The window is short (scene load plus spawn), but a client crash during loading reaches it. |
| **N16** | 🟢 Low | **The leaving player's suspicious-activity summary is never logged.** `HandlePlayerLeft` removes the departed session from `m_players` (`GameMaster.Player.cs:21`) **before** `EndMatch`. `EndMatch` only summarises `m_players` (`GameMaster.cs:424-434`). Every `[Suspicious]` warning is still logged on its own when it happens, so no data is lost from the log. Only the per-match summary for the player who left is missing, and that's the most likely tamperer. | `GameMaster.Player.cs:21`, `GameMaster.cs:424-434` | Code | Confirmed. Direction: log `session.GetSuspiciousLog()` inside `HandlePlayerLeft` (under `m_debugMode`, if `Total > 0`) before removing it. |
| **N17** | ⚪ Info | After a forfeit, `SessionManager.OnClientDisconnected` still calls `PlayerUI.Instance.ClearLocalUI()` on the **host** (`SessionManager.cs:180-183`), so the host's HUD goes blank for the moment before `ResultScene` loads. This behaviour predates H6. It's cosmetic. | `SessionManager.cs:180-183` | Code | Confirmed (cosmetic) |

**Withdrawn number:** N15 isn't used. The Editor-quit exception I looked at is listed as U9 instead, because I couldn't link it to project code.

---

## 3. Status of earlier issues

### Resolved since #3

| ID | Status |
|---|---|
| H4 | ✅ Fixed (`f08e854`) |
| H5 | ✅ Fixed (`ad3a46f` + `d44b882`). Normal end seen working on both instances. |
| H6 | ✅ Fixed (`d44b882`). Runtime check pending (U10). |
| H8 | ✅ Fixed (`f08e854`) |
| H9 | ✅ Fixed (`f08e854`) |
| M7 | ✅ Fixed by H4 |
| N10, N11, N13 | ✅ Closed by finishing H5 |
| L5 | ✅ `GameAnnouncerUI` maps `GamePhase.GameOver` (`GameAnnouncerUI.cs:129`), so the phase switch no longer throws for any value that exists today |

### 🟠 High: still open

| ID | Issue | Current location | Note |
|---|---|---|---|
| **H10** | **No UI in `ResultScene`.** The scene now loads after every match, and both players are stuck there (there's no button and no way back to the main menu). | `Assets/Scenes/ResultScene.unity`; spec [TASK-H10](../Tasks/TASK-H10_ResultSceneMenu.md) | **Raised in priority:** H5 now really sends players there. This is the next task. |
| H2 | Empty deck: `m_deck.ElementAt(0)` throws before the `null` check | `GameMaster.Card.cs:151` | H1 turns it into a match abort (to the lobby) instead of a freeze |
| H3 | The block heal over the cap uses `_targetCharacter.m_hpMax` (character, possibly `null`) instead of the block's maximum | `GameMaster.Processing.cs:224` | Latent. It's only reached by a heal card that targets a block. H1 catches it. |

### 🔴 Critical from report #1

- **C1–C4:** fixed (reports #2 and #3).
- **C5:** the runtime `UnityEditor` usings were removed (only a commented line remains, `PlayerController.cs:4`). The Editor compiles with 0 errors. **A player build has still not been run**, so U1 stays open.
- **C6:** still open. `ConfirmSendButton.cs:3` declares `ConfirmSendButtonUI1`, and the prefab references `ConfirmSendButtonUI1` / `ConfirmButtonUI1` (`PlayerUIPrefab.prefab:1127, 1189`). Step 1 (an Inspector check) has not been done. The log doesn't show whether a card was cast in the match it records.

### 🟡 Medium: still open

| ID | Issue | Current location |
|---|---|---|
| M1 | The shared `m_timer` is reset by the nested respawn wait | `GameMaster.GameLoop.cs:589` |
| M3 | Lava immunity after network-ID recycling | `LavaKillZone.cs:13, 71, 86` |
| M4 | `IsHost` used where `IsServer` is meant | `GameMaster.cs:134`; `GameMaster.PackageManaging.cs:50` |
| M6 | `UpdatePlayerHP` assumes a spawn point exists | `GameMaster.Processing.cs:343` |
| M8 | Pure-client NRE: `m_currentSelectedBlock.GetSession()` in `TryConfirmTarget` | `PlayerSession.cs` (`TryConfirmTarget`) |
| M9 | `DrawCard(null)` for a non-player target | `GameMaster.Processing.cs:232` |
| M10 | Async network errors aren't handled | `MultiplayerManager.cs` (`StartHost`, `JoinLobbyByJoinCode`, `HearthbeatLobby`) |
| M11 | Join by code is unusable | `MultiplayerManager.cs:28` |
| M12 | No in-game language selection | `SceneFlowManager.LoadSettingsScene` |
| M13 | Hard-coded user-visible strings | `MultiplayerManager.cs` (`"MyLobby"`, `"Host_"`/`"Client_"`), `LobbyListUI`, `LobbyPlayerItemUI` |
| M14 | The client dereferences `m_cardData` before it arrives | `PlayerSession.CheckPackage`, `NetworkCard`, `CardViewUI` |
| M15 | Unbounded hand growth (design question) | `DrawCardPhase` |
| M16 | `SessionManager.Instance` is never cleared | `SessionManager.cs` |
| N1 | `PlayerUI.OnDestroy` hides `NetworkBehaviour.OnDestroy` (CS0114, still in the log at `:5940`) | `UI/PlayerUI.cs:61` |
| **N14** | New, see §2 | |

### 🟢 Low and ⚪ Info: still open

- **N2:** NetworkVariables written before spawn.
- **N3:** client-side `CanPlayerMove` reads server-only flags.
- **N4:** dead `ValidateCardOwner`; `m_cardResolveDelay` unused (CS0414 at `GameMaster.cs:73`).
- **N5:** missing `$` in a log string (`Processing.cs:40`).
- **N6:** queued card packages still resolve after a decisive activity.
- **N7:** `GetCharacter` assumes a `PlayerController`.
- **N12:** `RunGuarded` treats `CustomYieldInstruction` as a nested enumerator (`GameMaster.GameLoop.cs:79-83`). It's still latent, because the loop only yields `null`, nested phases and `WaitForSeconds` (`:517`), which isn't an `IEnumerator`.
- **N16, N17:** new, see §2.
- **L1–L4, L6–L10:** unchanged from report #1. **L9** now also covers `ResultScene.unity:288` (`ResultSceneController.m_debugMode: 1`) next to `GameMasterPrefab.prefab:74`.
- **N8 (documentation drift), growing:** `Docs/AI/Architecture/*.md` has **0** mentions of any of these:
  - `GameOver`, `EndMatch`, `ResultScene`
  - `RunGuarded`, `AbortMatch`
  - `HandlePlayerLeft`, `SuspiciousActivityLog`

  `README.md` still describes a "destroy all enemy blocks" win, and `test.md` is still at the repo root.
- **N9:** the first selection confirm is final (for the designer to acknowledge). It's still relevant to MANA2, which makes a *rejected* confirm not count.

---

## 4. Unknowns: updated

| ID | Question | Status |
|---|---|---|
| U1 / C5 | Does a player build compile? | ❓ Not run |
| U2 | Nested `SpawnPoint` NetworkObject | ❓ No warnings in the log |
| U5 | Unresolved network-prefab GUID `e651dbb3…` | ❓ |
| U6 | Double `GameScene` load in the clone log | ❓ |
| U7 | Does the H5 flow work at runtime? | ✅ **Answered for a normal end:** both instances reached `ResultScene` with the right winner and loser, with no exceptions between `:3585` and `:5768`. The host-stop-in-`ResultScene` case is still unchecked. |
| U8 | Does `Shutdown()` leave no objects behind (TASK-H7 §9.2)? | ❓ No evidence |
| **U9** (new) | `NullReferenceException` in `NetworkSceneManager.Dispose` (`NetworkSceneManager.cs:600`), called from `NetworkManager.OnDestroy` → `ShutdownInternal` while the **Editor was quitting** (`Editor.log:6034-6044`) | ❓ This is inside NGO, during Editor shutdown, after a session that had already been shut down by `LeaveMatchToResultScene`. Likely harmless. Check whether it also happens when you quit the Editor without playing a match. |
| **U10** (new) | Does H6 forfeit work at runtime in each phase (TASK-H6 §9)? | ❓ No `OpponentDisconnected` in the log |

---

## 5. Suggested next steps (in order)

1. **TASK-H10:** the result menu with a "Main menu" button (en/ru/ja). Without it, every match ends in a soft lock.
2. **Run the pending manual checks** (the specs are in git history at `f08e854`, see §6):
   - H6 §9: forfeit in each phase, a hard drop (~30 s), and a lobby disconnect (no result). This answers U10.
   - H7 §9.2: no leftover objects after `Shutdown` (U8).
   - H1 §9.2: fault injection, which aborts to the lobby.
   - H5 §9: host stop while in `ResultScene`.
   - H8: open a multi-activity card asset and confirm activities 1–3 survive.
   - SEC1 §9: an honest match gives 0 `[Suspicious]` lines; tamper checks.
   - C5: one player build (U1).
   - C6 Step 1: Inspector check of the cast button.
3. **New small task for N14**, together with **H2** and **H3** (all small, independent server fixes). For N14, if the match can't start because a player is missing, call `AbortMatch` or return the host to the lobby.
4. **MANA1 → MANA2 → MANA3** (their dependencies H5, H6, H10 and SEC1 are done, or will be after step 1).
5. Optional one-liners: N16, N12, N1 (`override`), N4.
6. Update `Docs/AI/Architecture/*` for H1, H5, H6 and SEC1 (N8).

---

## 6. Task folder cleanup

I removed these specs from `Docs/AI/Tasks/` because they're implemented and match their spec. They're still in git history (`git show f08e854:Docs/AI/Tasks/<file>`). Their remaining manual checks are listed in §5, step 2.

| Removed | Reason |
|---|---|
| `TASK-C1_ServerRejectsClientHpDelta.md` | Fixed (report #2) |
| `TASK-C2_MovementUsesRpcSenderId.md` | Fixed (report #2) |
| `TASK-C3_ServerValidatesMovementPhase.md` | Fixed (report #2) |
| `TASK-C4_ValidateCardSelectionConfirm.md` | Fixed (report #2) |
| `TASK-C5_RemoveEditorOnlyUsingsFromRuntime.md` | Code fixed. The player-build check is tracked as U1. |
| `TASK-H1_GuardedGameLoop.md` | Fixed (report #3) |
| `TASK-H4_CheckActivityNullSafety.md` | Fixed (this report) |
| `TASK-H5_EndOfMatchResultScene.md` | Fixed (this report) |
| `TASK-H6_ForfeitOnOpponentDisconnect.md` | Fixed (this report). The runtime check is tracked as U10. |
| `TASK-H7_DisconnectCleanupLocalOnly.md` | Fixed (report #3). The check is tracked as U8. |
| `TASK-H8_CardDataOnValidateKeepsActivities.md` | Fixed (this report) |
| `TASK-H9_UltraRareNotInRefillPool.md` | Fixed (this report) |
| `TASK-SEC1_SuspiciousActivityTracking.md` | Fixed (this report) |

**Kept** (not done): `TASK-C6`, `TASK-H10`, `TASK-MANA1`, `TASK-MANA2`, `TASK-MANA3`.

Links from the kept specs to removed specs (for example H10 → H5, MANA2 → SEC1) now point to files that only exist in git history. The content they need is already in the code.

## Sources

- **Diff:** `git diff ad3a46f f08e854`
- **Code re-checked:**
  - `GameMaster.cs` (`:134`, `:407-480`, `:484-560`)
  - `GameMaster.Player.cs`
  - `GameMaster.Processing.cs` (`:93-160`, `:212-232`, `:343`)
  - `GameMaster.Card.cs` (`:39-64`, `:149-160`)
  - `GameMaster.GameLoop.cs` (`:40-105`, `:517`, `:589`)
  - `GameMaster.PackageManaging.cs` (`:95-150`)
  - `SessionManager.cs` (`:156-198`)
  - `MultiplayerManager.cs` (`:213-235`, `:346-464`, `:1178-1266`)
  - `ConfirmSendButton.cs`
  - `GameAnnouncerUI.cs`
- **Assets:**
  - `ProjectSettings/EditorBuildSettings.asset`
  - `UI_table` (shared, en, ru, ja)
  - `PlayerUIPrefab.prefab`
  - `GameMasterPrefab.prefab`
  - `ResultScene.unity`
- **NGO (read only):** `Runtime/Connection/NetworkConnectionManager.cs` (`:1335-1409`, `:1540-1560`)
- **Log:** `%LOCALAPPDATA%\Unity\Editor\Editor.log` (16:04)
