# Review: the owner's manual two-player checks (reports #6 and #7)

- **Date:** 2026-10-20
- **Reviewed:**
  - the filled-in [checklist workbook](../ManualChecks/2026-10-20_ManualChecks-Report6-7.xlsx)
  - the Windows player build in `../Spellcasters.Build/`
- **Baseline:** `HEAD` = `b7e7b91`. `git status` shows only `Docs/AI` changes (the 13 removed specs, report #7, the workbook). No temporary change is left in code, prefabs or card assets.
- **Evidence used:**
  - the build's own log: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\FallenStar-Spellcasters\Player.log` (13:16–13:17). `Player-prev.log` is from 2026-06-03.
  - `%LOCALAPPDATA%\Unity\Editor\Editor.log` (09:36–13:21). Both Editors write to it (see §1.2).
  - the build result: `Editor.log:48994`, "Build completed with a result of 'Succeeded' in 37 seconds [2026/10/20 13:16:03 -> 2026/10/20 13:16:41]"
  - PlayerPrefs in the registry, for the build and the Editor: `setting.language_code` = `en`
- **I didn't run Play Mode or the build myself.** I changed no code or assets.

## Summary

| | |
|---|---|
| Owner's result | **123 of 123 rows Pass** (110 required, 13 optional), tested by "Owner" on 2026-10-20. The Summary sheet shows Pass. The Applied/Reverted columns on the Temp changes sheet are empty, but `git status` confirms nothing was left over. |
| Supported by the logs | A1, A2–A6, A7–A9, A11 (one case), A12, A13, A14, A15, A16, A19 (partly), A21, B1, D.1, D.3 (partly) |
| No trace in the logs (can't confirm or refute) | A10, A11 (6 cases), A17, A18, A20, B2–B8, C1–C7, D.4. See §1.2. |
| Contradicted | **D.5** (restart the build, the language is kept): the build was started only once. **D.2** (optional) ran differently from the steps. |
| Owner notes | Both match issues already in report #7: **N2** (NetworkVariable warning) and **L4** (the `Team Id` exception). Neither was on my Known issues sheet, which was my mistake. |
| New issues | **N30** 🟢 (Lobby SDK exception on refresh in the build, handled), **N31** 🟢 (decks have 84 cards, not 80) |
| Register changes | **U1** answered, **U5** closed, **U6** explained (Plausible), **U8 / U10 / U12** answered, **N12 withdrawn** (false positive), **L4** text corrected |
| Verdict | **No blocking defect.** Nothing Critical, High or Medium is open. Re-run D.5 (2 minutes). The rows without a trace are accepted as reported by the owner. |

---

## 1. Evidence per check group

✅ supported by the logs · ➖ no trace in the logs · ❌ contradicted

### 1.1 Table

| Group | Owner | Evidence | Verdict |
|---|---|---|---|
| A1, A6, A13 | Pass | A normal match end, `ResultScene: You won. Reason: LivesDepleted` (`Editor.log:8314`). 0 `[Suspicious]` lines in the whole log. | ✅ |
| A2–A5 | Pass | `Waiting for Respawn` ×3, `Respawn point confirmed` ×3, lava kills (`LavaKillZone.TryKillPlayer` ×6). The log can't tell which phase each death was in. | ✅ (partly) |
| A7–A9, A21.3 | Pass | `Surrendered` results on both sides in 5 matches (e.g. `:12571` host, `:26172` onward client) | ✅ |
| A10, A17, A18 | Pass | UI only, nothing is logged | ➖ |
| A11 | Pass | One `OpponentDisconnected` result (`:38840` host, `:39446`), at the end of the B1 match. The other 6 moments leave no line. | ✅ for 1 of 7 |
| A12 | Pass | No errors after any match end | ✅ consistent |
| A14 (N20) | Pass | `Send ignored: no card target was confirmed.` ×11 and **0 `NullReferenceException` in the whole Editor log** | ✅ |
| A15, A16 | Pass | 0 NRE. Lava deaths after respawns. | ✅ |
| A19 (M12) | Pass | `SettingsScene` loaded once. Editor PlayerPrefs hold `en`, so the choice was saved. | ✅ (partly) |
| A20 (M10) | Pass | No `LobbyServiceException`, no "unobserved" line and no M10 log line anywhere. An offline create should log one error. | ➖ |
| A21.1–.2 | Pass | Only CS0414 (×4: clone start-up and player build). No CS0114. No error that mentions `SessionManager`. | ✅ |
| B1 | Pass | `GameMasterPrefab` re-imported (`:30232`). 81 × `Hand limit 10: discarded 1 oldest card(s)` alternating clients 0 and 1. `Deck refilled: 84 cards.` (`:37044`). | ✅ |
| B2–B4, B6–B8 | Pass | No re-import of `Clarity`, `HealingHooves`, `Iceshard`, `Fireball` or `NetworkManagerPrefab`. One more `GameMasterPrefab` re-import (`:15728`) may be T2 or T3. | ➖ |
| B5 | Pass | No `Hand limit 3` line. All 81 hand-limit lines say `10`. | ➖ |
| C1, C6 | Pass | No `match start aborted` line, no `game_master_create_failed`, no CS0162 (the unreachable-code warning C6 expects) | ➖ |
| C2, C3, C5 | Pass | Nothing specific to expect, but see the compile count in §1.2 | ➖ |
| C4, C7 (optional) | Pass | C4 should log `[Suspicious]` lines (0 found). C7 should log `M16 check:` (0 found). | ➖ |
| D.1 | Pass | Build succeeded in 37 s, 0 errors | ✅ |
| D.2 (optional) | Pass | See §1.3 | ❌ (ran differently) |
| D.3 | Pass | The build's PlayerPrefs changed from "no saved language" (`Player.log`, start-up) to `en` | ✅ (partly) |
| D.4 | Pass | UI only | ➖ |
| D.5 | Pass | The build was started once (§1.3) | ❌ |

### 1.2 Why a ➖ isn't a Fail

The two Editors write into the same `Editor.log` and overwrite each other's lines, so big parts of the session are missing:
- The same lobby codes appear twice, about 15,000 lines apart, once from each side. For example, `T99PKR` appears at `:11636` (host: "match ended … You lost") and at `:26172` (client: "You won").
- The file has only **6 Play Mode entries**.
- After the clone's start-up, the file has **no script compile** except the player build. C1–C7 each need two compiles: one to apply the edit, one to revert it.

So I can't confirm the ➖ rows, and I can't refute them either. I accept them as you reported them. If some rows were actually skipped, set them to *Not run* so the Summary sheet stays true.

**For next time:**
- Give the clone its own log: start it with `-logFile C:\Lesson\clone-editor.log`. With Unity Hub 3 you can add this under Projects → ⋯ → Add command line arguments.
- Or paste the key Console lines into the notes column.
- Mark the Applied / Reverted columns on the Temp changes sheet.

### 1.3 The build rows (D)

- **D.5 ❌:** the build was started only once today:
  - Unity renames `Player.log` to `Player-prev.log` at every start. `Player-prev.log` is still from 2026-06-03.
  - The build's session counter in the registry is 2 (June plus today).

  So the restart step didn't happen. Saving works: the build's PlayerPrefs hold `en`. Loading at start-up hasn't been seen in the build. **Re-run D.5:** start the .exe → Settings → 日本語 → quit → start again. Japanese should be active. Then set English back.
- **D.2 (optional):** the .exe was the **host** (client 0), not the client. Neither build match reached Card Selection:
  - match 1 ended when the build player left during the first deal
  - match 2 ended when the Editor player left during Round preparation

  So "play one round" and the Space-key check weren't done. The exe-to-Editor connection, the lobby, the match start, the forfeit and the result screen all worked in both directions.

---

## 2. Your notes

1. **A1.1: "NetworkVariable is written to, but doesn't know its NetworkBehaviour yet…"** This is **N2**:
   - `SessionManager.cs:245` calls `PlayerSession.Initialize` (`PlayerSession.cs:636-651`) **before** `SpawnWithOwnership` (`:247`), so `ClientId`, `TeamId` and `m_lifeCurrent` are written before NGO knows the object.
   - The build logs it 4 times per match.
   - It's harmless today: the values still reach the clients with the spawn. It is noise in every match, though.
   - → [TASK-N2](../Tasks/TASK-N2_SessionValuesAfterSpawn.md)
2. **Known issues sheet: `ArgumentException: An item with the same key has already been added. Key: Team Id`.** This is **L4**:
   - NGO's Inspector for network objects (`NetworkBehaviourEditor.Init`, `NetworkBehaviourEditor.cs:43`, Editor-only) lists public NetworkVariable fields by their display name.
   - `BlockController` has `m_teamId` (`:12`) and `TeamId` (`:23`). Both display as "Team Id", so `Dictionary.Add` throws whenever the Inspector shows a block or the block prefab. In this session that was during a match (`Editor.log:20964`, `:20994`). It isn't gameplay code, and it doesn't happen in a build.
   - **Correction to L4:** report #7 says the two variables are "read by different code". In fact `TeamId` is never written or read anywhere, so it's dead. All team checks use `m_teamId`.
   - → [TASK-L4](../Tasks/TASK-L4_RemoveDuplicateBlockTeamId.md)

Both warnings belonged on the workbook's Known issues sheet, and I missed them.

## 3. New findings

| ID | Level | Issue | Location | Evidence | Verdict |
|---|---|---|---|---|---|
| **N30** | 🟢 Low | **The Lobby SDK throws a NullReferenceException on a lobby refresh.** M10's handler catches it, so nothing visible breaks. The exception is thrown inside the SDK's own error handler (`WrappedLobbyService.TryCatchRequest` dereferences `he.ActualError` / `he.Response`), so the request failed with a response the SDK couldn't wrap. Likely cause: too many `GetLobbyAsync` calls. `CurrentLobbyUI` refreshes every 2 s on **both** peers, and the host's Start sends one more right away. Two refreshes can also be in flight at once, and both write `m_currentLobby`. | `UI/CurrentLobbyUI.cs:22`, `:156`; `MultiplayerScripts/MultiplayerManager.cs:927-947`, `:1232`, `:1248-1265` | `Player.log`, second lobby `HBNRB7`: `NullReferenceException at WrappedLobbyService.TryCatchRequest … GetLobbyAsync … MultiplayerManager.RefreshCurrentLobby`, then "lobby refresh failed, using the last known lobby data." The match started normally. | Plausible (cause). The exception itself is Confirmed. → [TASK-N30](../Tasks/TASK-N30_LobbyRefreshThrottle.md) |
| **N31** | 🟢 Low | **Decks have 84 cards, not 80.** `FillCardSection` adds whole sets of 4 copies (`GameMaster.Card.cs:340-400`), so a section with a maximum of 30 gets 32. The method's comment says the maximum is never exceeded. | `GameMaster.Card.cs:268-331`, `:340-400` | `Editor.log:37044` "Deck refilled: 84 cards." Code: 20 + 32 + 32 = 84 | Confirmed. Bundled with L2 → [TASK-L2](../Tasks/TASK-L2_N31_DeckComposition.md) |

**Correction to my checklist:** B1 said the 80-card deck is exactly empty after the round 2 deal. With 84 cards, 4 remain, and the refill happens during the round 3 deal. B1's expected result doesn't change.

## 4. Changes to the report #7 register

| ID | Change | Why |
|---|---|---|
| U1 | ❓ → **answered** | The player build on `b7e7b91` succeeded and runs: two matches, the Settings scene, a saved language. |
| U5 | ❓ → **closed** | `e651dbb3…` is the GUID of NGO's own `NetworkPrefabsList.cs` (in the package, so it isn't under `Assets`). It's used by `Assets/DefaultNetworkPrefabs.asset` and `Assets/Prefab/NetworkPrefabs.asset`. Nothing is unresolved. |
| U6 | ❓ → **explained** (Plausible) | The "double GameScene load" matches the shared log: each Editor logs its own load. For example, `Editor.log:1153` and `:1262` are each followed by their own `CardWorldViewManager Awake`. |
| U8, U12 | ❓ → **answered** (owner-reported) | A12 and A1 passed with two separate Editors |
| U10 | ❓ → **answered** | `OpponentDisconnected` was seen in the log for the first time (`:38840`), and A11 passed |
| U9 | still open | Not seen this time: 0 NRE in the whole Editor log |
| **N12** | **withdrawn** (false positive) | `RunGuarded` (`GameMaster.GameLoop.cs:41-93`) doesn't spin on a `CustomYieldInstruction`. Such an instruction's `MoveNext()` returns `keepWaiting` and its `Current` is `null`, so the runner pushes it and then does `yield return null` once per frame until it's done. That is what Unity's own scheduler does. Reports #3–#7 were wrong. |
| L4 | text corrected; new evidence | §2 |
| N2 | new evidence | §2 |

## 5. Owner decisions (2026-10-20)

| Issue | Decision |
|---|---|
| N26 | Clear an unconfirmed selection when a new Card Selection starts. Keep it across a respawn pause. |
| L1 | Countdown (seconds left). The server sends the phase end once. |
| L2 + N31 | Use all three deck mixes. Keep whole sets of 4 (84-card decks). Fix the comments. |
| N27 | **Parked:** the mane model doesn't exist yet. Keep the code as it is. No task. |
| L9 | Force `m_debugMode` off in release (non-development) builds. Keep the Inspector flags. |
| N28 | Clear Dynamic Data On Build. Don't commit Editor-only atlas changes. |
| L10 | Delete the backups, the legacy scenes and their UI, and the unused code, `test.md` and the VisualScripting package |
| N24 | Convert the 3 files to UTF-8 |
| N29 | **Parked** for later. No task. |

## 6. Tasks written (`Docs/AI/Tasks/`, in order)

| # | Spec | Issues | Notes |
|---|---|---|---|
| 1 | [TASK-N24](../Tasks/TASK-N24_Utf8Conversion.md) | N24 | cp932 → UTF-8. First, because tasks 15 and 18 edit `CardWorldViewManager.cs`. |
| 2 | [TASK-HYG1](../Tasks/TASK-HYG1_SmallFixes.md) | N4, N5, N16, N23 (PlayerController), L8 | Removes the last warning (CS0414) |
| 3 | [TASK-L4](../Tasks/TASK-L4_RemoveDuplicateBlockTeamId.md) | L4 | Fixes the `Team Id` Inspector exception |
| 4 | [TASK-N2](../Tasks/TASK-N2_SessionValuesAfterSpawn.md) | N2 | Fixes the A1.1 warning |
| 5 | [TASK-N6](../Tasks/TASK-N6_StopResolvingAfterMatchEnd.md) | N6 | |
| 6 | [TASK-L3](../Tasks/TASK-L3_PureIsGameFinished.md) | L3 | |
| 7 | [TASK-N7_N22](../Tasks/TASK-N7_N22_CharacterLookupAndStartHp.md) | N7, N22 | |
| 8 | [TASK-N3](../Tasks/TASK-N3_ReplicatedInputLock.md) | N3 | Latent; regression checks only |
| 9 | [TASK-N21](../Tasks/TASK-N21_PhaseTaggedConfirm.md) | N21 | Default chosen by Claude: ignore a mismatched confirm, with no grace period (§10 of the spec) |
| 10 | [TASK-N26](../Tasks/TASK-N26_ClearSelectionEachRound.md) | N26 | Owner decision |
| 11 | [TASK-L1](../Tasks/TASK-L1_CountdownTimer.md) | L1 | Owner decision |
| 12 | [TASK-L2_N31](../Tasks/TASK-L2_N31_DeckComposition.md) | L2, N31 | Owner decision |
| 13 | [TASK-N30](../Tasks/TASK-N30_LobbyRefreshThrottle.md) | N30 | New |
| 14 | [TASK-N17](../Tasks/TASK-N17_HostHudOnForfeit.md) | N17 | |
| 15 | [TASK-L6](../Tasks/TASK-L6_PerFrameWork.md) | L6 | The `LeaveMatchUI` part needs no change |
| 16 | [TASK-L7](../Tasks/TASK-L7_CardTextLocaleRefresh.md) | L7 | |
| 17 | [TASK-L9](../Tasks/TASK-L9_DebugModeOffInRelease.md) | L9 | Owner decision |
| 18 | [TASK-L10](../Tasks/TASK-L10_DeadCodeCleanup.md) | L10, the rest of N23 | Owner-approved deletions. Keeps the parked M11 fields. |
| 19 | [TASK-N28](../Tasks/TASK-N28_FontAtlasClearOnBuild.md) | N28 | Owner decision. Includes a build check that covers D.4. |
| 20 | [TASK-N8](../Tasks/TASK-N8_DocsRefresh.md) | N8 | **Claude** does it after tasks 1–19. The `README.md` part needs the owner's go-ahead. |

**Not tasked:**
- **N27 and N29:** parked by the owner.
- **N12:** withdrawn.
- **The naming and typo list:** report-only, as decided earlier.
- **The feature gaps:** not defects.
- **Open unknowns U2 and U9:** U2 (nested `SpawnPoint` NetworkObject) has no symptom in any log. U9 (NGO teardown NRE) wasn't seen this time.

## 7. Next steps

1. Re-run **D.5** in the build. Optionally re-run D.2 with the .exe as the client and one full round.
2. Codex implements tasks 1–19 in order. Each is small and has its own manual checks.
3. Claude reviews each implementation (`Docs/AI/Reviews/`), then does TASK-N8.
