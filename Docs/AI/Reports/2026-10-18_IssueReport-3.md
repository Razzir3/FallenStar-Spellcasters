# Issue Report #3: FallenStar – Spellcasters (re-audit after H7, H1, H5)

- **Date:** 2026-10-18
- **Baseline:** `HEAD` = `ad3a46f` ("task 5-6, on pause"). The working tree is clean.
- **Previous reports:** [#1](2026-10-18_IssueReport.md) (`1f6d2f1`), [#2](2026-10-18_IssueReport-2.md) (`44d858f`)
- **Method:**
  1. Read the full code diff `44d858f..ad3a46f` (commits `57d3467` H7, `e776caf` H1, `ad3a46f` H5 partial).
  2. Checked `ResultScene.unity`, `EditorBuildSettings.asset`, the UWP build profile, and the `UI_table` keys.
  3. Re-checked open items from report #2 at their current lines.
  4. Read the Editor log (`Editor.log`, ParrelSync clone, last written 14:13) and NGO 2.11.2 sources (read only).
- **No build or Play Mode was run by me.** No code, scenes, prefabs or settings were changed.

Severity levels and verdicts are the same as in reports #1 and #2. New findings continue the numbering from report #2 (N10…).

## Summary

| | |
|---|---|
| Tasks completed since #2 | H7 ✅, H1 ✅ |
| Tasks in progress (owner, 2026-10-18) | **H5, H6**: paused mid-work (`ad3a46f` "on pause") |
| Tasks not started | H4, H8, H9, H10; C6 Step 1; C5 player build |
| H5 steps still to do | 2: N10 (scene in build list), N11 (`Phase.GameOver` key), plus probably N13 |
| 🟢 New Low (in completed code) | 1: N12 (H1 runner, latent) |
| Resolved since #2 | H7, H1, U3 (answered) |
| 🟠 High still open | H2, H3, H4, H6, H8, H9 |

**Overall:** H1 and H7 are complete and match their specs. H5 and H6 are **work in progress**: the owner confirmed they're paused. N10, N11 and probably N13 are steps of TASK-H5 that haven't been done yet, **not defects**. They are kept below only as a checklist for when H5 resumes. The one finding in finished code is N12, a latent edge case in H1's runner.

> **Correction (owner feedback, 2026-10-18):** the first version of this report rated N10 as Critical and N11 as Medium. Those ratings were wrong for paused work and have been withdrawn. N10 and N11 correspond to TASK-H5 §4.5.11 and §4.4.10. N12 is **not** caused by the unfinished H5/H6 work. It's in `RunGuarded`, which belongs to the completed H1 task, so it stays a (Low, latent) finding.

---

## 1. Review of implemented tasks

| Task | Commit | What changed | Matches spec? | Verdict | Notes |
|---|---|---|---|---|---|
| **H7** | `57d3467` | `GameMaster.ClearupAfterDisconnect` (`GameMaster.cs:312`) stops `m_gameCoroutine`, clears all plain lists and flags, null-checks spawn points and `SessionManager.Instance`, and has **no despawns and no NetworkVariable writes**. `PlayerSession.ClearSession` (`PlayerSession.cs:619`) only drops references and the local selection. | Yes. `ClearHand`, `ClearBlocks` and `DespawnAllCards` are kept; `StartNewGame` still uses `DespawnAllCards`. | ✅ **Fixed** (static) | The §9.2 Play Mode check ("no leftover objects after `Shutdown`") has no evidence in the log yet. |
| **H1** | `e776caf` | `RunGuarded(IEnumerator)` (`GameMaster.GameLoop.cs:40-92`) is a stack-based runner with `MoveNext` inside `try/catch` and no `yield` inside the `try`. Nested enumerators are pushed and other yields passed through. On an error it calls `LogException`, then `AbortMatch("loop_exception")`. `AbortMatch` (`GameMaster.cs:442`) is idempotent, sets the phase to `None` and calls `MultiplayerManager.Disconnect()`. `StartNewGame` starts `RunGuarded(GameLoop())` and resets the flags. | Yes | ✅ **Fixed** (static) | It sets `m_gameCoroutine = null` before `AbortMatch` and at normal end. That's correct, because H7 cleanup may run synchronously. See N12 for a latent edge case. |
| **H5** | `ad3a46f` | `GamePhase.GameOver` (appended), `MatchEndReason`, `EndMatch` (`GameMaster.cs:407`, idempotent), `MatchEndedClientRpc`, the winner selection at the end of `GameLoop` (`GameLoop.cs:104-121`), `MatchResultData`, `MultiplayerManager.LeaveMatchToResultScene` (`:346-464`), `SceneFlowManager.LoadResultScene`, the `GameAnnouncerUI` and `PlayerUI` cases, `ResultSceneController`, and `ResultScene.unity` (camera + controller, `m_debugMode: 1`) | Mostly | ⚠️ **Partial** | **Missing:** (1) the scene isn't in the build list → **N10**; (2) the `Phase.GameOver` key doesn't exist in en/ru/ja → **N11**. |

### Details checked in `LeaveMatchToResultScene`

- **The redirect guard is set early.** `m_isLeavingToResult` and `m_onDisconnecting` are set **before** the first `await`. `OnClientDisconnected` returns early while the flag is set (`:215-219`). This meets the owner's requirement that no player gets sent back to the main menu or lobby.
- **The host waits for the client.** It waits until `ConnectedClientsIds.Count <= 1`, or `m_resultLeaveTimeout` (3 s) on the real-time clock, before shutting down.
- **Lobby cleanup.** The host deletes the lobby; the client removes itself from it. Both are inside `try/catch`.
- **Shutdown order.** H7 cleanup runs, then `Shutdown()`, then a wait while `ShutdownInProgress`. Then local state is reset without loading the lobby, and finally `LoadResultScene()` runs.
- **Error handling.** Nested `try/finally` blocks make sure the flags are always reset and the scene load is always attempted. `CleanupAfterDisconnect` is unchanged, as required.
- **Compile:** the clone's `Editor.log` shows a recompile where the `CS0414` warning moved from `GameMaster.cs:66` to `:73`. That's the 7 lines added by `GameOver` and `MatchEndReason`, so the H5 code was compiled. There are **0 `error CS`** entries. No end-of-match Play Mode run appears in the log.

---

## 2. New findings

| ID | Level | Issue | Location | Evidence | Verdict |
|---|---|---|---|---|---|
| **N10** | ⏳ H5 to-do (§4.5.11) | **`ResultScene` isn't in the build scene list.** `SceneManager.LoadScene("ResultScene")` refuses scenes that aren't in the list, in Play Mode as well as in a player build. When a match ends, both instances shut the network down and then fail to load the result scene. They stay in a `GameScene` with no network session, with no way forward. | `ProjectSettings/EditorBuildSettings.asset` (6 entries: Sample, Multiplayer, Login, MainMenu, Lobby, Game); `Assets/Settings/Build Profiles/Universal Windows Platform.asset` (`m_OverrideGlobalSceneList: 0`); caller `SceneFlowManager.LoadResultScene` | Files read. TASK-H5 §4.5.11 required **File → Build Profiles → Scene List**. | Confirmed (file). The runtime error message has not been observed, because no end-of-match run is in the log. |
| **N11** | ⏳ H5 to-do (§4.4.10) | The `Phase.GameOver` key is missing from `UI_table` in all locales. During `GameOver`, `GameAnnouncerUI` (`Update` → `GetLocalized`, `:153-160`) requests it every frame. Unity Localization's default for missing entries is to display the missing-translation message and log a warning, so the warning repeats every frame until the scene changes (up to ~3 s on the host). This violates the en/ru/ja rule. | `Assets/Localization/UI_table Shared Data.asset` (0 matches for `Phase.GameOver`); `UI/GameAnnouncerUI.cs:128` | grep | Confirmed (key missing); the visual effect is plausible |
| **N12** | 🟢 Low (latent) | `RunGuarded` treats **any** `IEnumerator` as a nested coroutine. `CustomYieldInstruction` types (`WaitUntil`, `WaitWhile`, `WaitForSecondsRealtime`) are `IEnumerator`s too. If one is ever yielded inside the match loop, the runner calls its `MoveNext()` repeatedly in the same frame without yielding to Unity, which freezes the Editor or player. Nothing in the loop uses them today. | `GameMaster.GameLoop.cs:79-83` | Code; grep finds no such yields in `InGame` | Confirmed (latent). Direction: check `current is CustomYieldInstruction` first and `yield return` it to Unity. |
| **N13** | ⏳ Probably H5 WIP | `ad3a46f` also changed the shared TMP font asset `MPLUSRounded1c-Medium SDF.asset` (+64 lines). This is most likely dynamic-atlas glyphs added while typing. It's harmless, but it mixes an asset change into a code commit. | git stat | — | Confirmed |

### Facts newly established (closing earlier unknowns)

- **U3 answered:** NGO 2.11.2 supports a null `NetworkBehaviourReference` (`NetworkBehaviourReference.cs:22-29` → `NetworkObjectReference.cs:30-36` stores a null ID). `m_activePlayer.Value = default/null` and `GameAnnouncerUI.GetTargetSession` are safe.
- **Related new fact:** creating a reference from an **unspawned** NetworkObject throws an `ArgumentException` (`NetworkObjectReference.cs:38-41`). That's relevant to H6, below.
- **U4 answered** (in the research document): `DontDestroyWithOwner: 0` on all prefabs.

---

## 3. Status of earlier issues

### Resolved

| ID | Status |
|---|---|
| H1 | ✅ Fixed (`e776caf`). The match loop can no longer freeze silently on an exception. |
| H7 | ✅ Fixed (`57d3467`). Play Mode verification is pending. |
| H5 | ⏳ In progress (`ad3a46f`, paused). Remaining steps: N10, N11. The GUI is TASK-H10. |
| U3 | ✅ Answered (see above) |

### 🟠 High: still open

| ID | Issue | Current location | Change since #2 |
|---|---|---|---|
| H2 | Empty deck throws in `DrawCard` | `GameMaster.Card.cs:151` | **Impact reduced by H1:** the match is aborted to the lobby instead of freezing |
| H3 | Block heal uses `_targetCharacter.m_hpMax` | `GameMaster.Processing.cs:237` | Same; latent. H1 now catches it. |
| H4 | `CheckActivity` owner dereference before its null check | `Processing.cs:97` (check at `:99`) | TASK-H4 not started |
| H6 | Opponent disconnect mid-match | `MultiplayerManager.cs:240`; no `HandlePlayerLeft` exists | **TASK-H6 paused.** The behaviour is now *plausibly* one of two things: (a) `RespawnCharacter` for the gone client fails, H1 aborts, and the host goes to the lobby; or (b) NGO creates a client entry for the unknown ID (`NetworkSpawnManager.cs:127-131`) and a phantom pony spawns. Either way, there's no win result. |
| H8 | `CardData.OnValidate` wipes activities 1–3 | `InGame/CardData.cs:99` | TASK-H8 not started |
| H9 | `UltraRare` writes `_nonUniqueList[i]` | `GameMaster.Card.cs:262` | TASK-H9 not started |

### 🔴 Critical from report #1

- C1–C4 are fixed.
- C5: the Editor compiles (0 errors), but **the player build has still not been run**.
- C6: Inspector Step 1 has still not been done.

### 🟡 Medium and 🟢 Low

These are unchanged from report #2. The line numbers below shifted because `GameLoop.cs` and `GameMaster.cs` grew.

- **M1:** respawn timer reset, now `GameLoop.cs:589`
- **M4:** `IsHost` checks at `GameMaster.cs:134` and `PackageManaging.cs:50`
- **M6:** `Processing.cs:356`
- **M9:** `Processing.cs:245`
- **N1:** `PlayerUI.OnDestroy` hides the base method (`CS0114` still in the log)
- **N4:** `m_cardResolveDelay` unused, `CS0414` now at `GameMaster.cs:73`
- **N8:** documentation drift. It's growing: `Docs/AI/Architecture/*` doesn't yet describe `GameOver`, `ResultScene`, `RunGuarded`, `AbortMatch` or `LeaveMatchToResultScene`.

---

## 4. Unknowns: updated

| ID | Question | Status |
|---|---|---|
| U1 / C5 | Player build compiles? | ❓ Not run |
| U2 | Nested `SpawnPoint` NetworkObject | ❓ No warnings in the logs |
| U5 | Unresolved network-prefab GUID `e651dbb3…` | ❓ |
| U6 | Double `GameScene` load in the clone log | ❓ Unchanged (same log section) |
| U7 (new) | Does the H5 flow behave as designed at runtime? Specifically: is the host's `MatchEndedClientRpc` executed immediately or deferred, the client leaving before the 3 s timeout, and `ShutdownInProgress` clearing | ❓ Needs a ParrelSync run after N10 is fixed |
| U8 (new) | H7 assumption: `Shutdown()` leaves no cards, blocks, ponies or sessions behind | ❓ TASK-H7 §9.2 has not been run |

---

## 5. Suggested next steps (in order)

1. **Resume TASK-H5** (remaining steps N10 and N11). In the Editor, add `ResultScene` to **File → Build Profiles → Scene List** after `GameScene`.
2. Add `Phase.GameOver` in en/ru/ja ("Game over" / "Игра окончена" / "ゲーム終了"), as specified in TASK-H5 §4.4.10.
3. **Run the pending checks:**
   - TASK-H5 §9: match end on both sides, host stop while in `ResultScene`
   - TASK-H7 §9.2: no leftover objects
   - TASK-H1 §9.2: fault injection

   That answers U7 and U8.
4. **TASK-H10** (result GUI and Main menu button). Without it, players are stuck in `ResultScene` once N10 is fixed.
5. **Resume TASK-H6.** Its contract (`EndMatch`, `GamePhase.GameOver`, `LeaveMatchToResultScene`) now exists exactly as the task expects.
6. The small independent tasks H4, H8, H9. Optionally a one-line fix for N12.

## Sources

- **Diff:** `git diff 44d858f ad3a46f` for `GameMaster.cs`, `GameMaster.GameLoop.cs`, `PlayerSession.cs`, `MatchResultData.cs`, `ResultSceneController.cs`, `SceneFlowManager.cs`, `MultiplayerManager.cs`, `GameAnnouncerUI.cs` and `PlayerUI.cs`.
- **Assets:**
  - `Assets/Scenes/ResultScene.unity`
  - `ProjectSettings/EditorBuildSettings.asset`
  - `Assets/Settings/Build Profiles/Universal Windows Platform.asset`
  - `Assets/Localization/UI_table Shared Data.asset`
- **Unchanged code re-checked at its current lines:**
  - `GameMaster.Processing.cs` (`:96-99`, `:237`, `:245`, `:356`)
  - `GameMaster.Card.cs` (`:151`, `:262`)
  - `CardData.cs` (`:99`)
  - `GameAnnouncerUI.cs` (`:40-75`, `:153-160`)
- **NGO 2.11.2 (read only):** `Runtime/Serialization/NetworkBehaviourReference.cs`, `NetworkObjectReference.cs`, `Runtime/Spawning/NetworkSpawnManager.cs`.
- **Log:** `%LOCALAPPDATA%\Unity\Editor\Editor.log` (clone project, 14:13).
