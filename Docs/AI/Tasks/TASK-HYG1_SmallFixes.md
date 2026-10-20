# TASK-HYG1: Small fixes (N4, N5, N16, N23, L8)

- **Source issues:** [Issue Report #7 → Low](../Reports/2026-10-20_IssueReport-7.md#-low-23): N4, N5, N16, N23, L8. The "hygiene bundle" of report #7 §5, step 4.1. N12 is left out because it was withdrawn as a false positive in the [manual-check review](../Reviews/2026-10-20_ManualChecks-Report6-7_Review.md#4-changes-to-the-report-7-register). L2 has its own task.
- **Severity:** 🟢 Low (all Confirmed)
- **Baseline:** `b7e7b91`. Line numbers below are from this commit.
- **Order:** 2 of 20. Independent. TASK-L10 later deletes the Tab-key code in `MultiplayerManager.Update`, so this task fixes N23 only in `PlayerController`.
- **Implementer:** Codex

## 1. Objective

Five small, independent fixes:
- the last compiler warning goes
- a log string interpolates its value
- a disconnecting player's suspicious-activity summary is logged
- movement input survives a missing keyboard
- a localization key loses its trailing tab

## 2. Current behavior

| ID | Where | What happens now |
|---|---|---|
| N4 | `InGame/GameMaster.cs:74` | `[SerializeField] private float m_cardResolveDelay = 3f;` is never read. It causes **CS0414**, the only compiler warning left. |
| N4 | `InGame/GameMaster/GameMaster.PackageManaging.cs:484-497` | `private bool ValidateCardOwner(NetworkCard, PlayerSession)` and its XML comment. There are 0 callers (grep: 1 hit, the definition). |
| N5 | `InGame/GameMaster/GameMaster.Processing.cs:40` | `Debug.Log("Client disconnected: {_package.ClientId}");`. The `$` is missing, so the braces are printed literally. |
| N16 | `InGame/GameMaster/GameMaster.Player.cs:45-88` (`HandlePlayerLeft`) | It removes the session from `m_players` (`:60`) **before** `EndMatch` (`:82`). `EndMatch` (`GameMaster.cs:418-447`) logs `[Suspicious] client X match summary: …` only for sessions still in `m_players`, so a disconnecting player's summary is never logged. The surrender path (`HandlePlayerSurrender`) is fine. |
| N23 | `InGame/PlayerController.cs:167-185` (`SendMovementInput`) | `Keyboard.current.wKey…` is used without a null check. With no keyboard (a device removed or not yet enumerated), this throws a `NullReferenceException` every frame for the owner. |
| L8 | `Assets/Localization/UI_table Shared Data.asset:174-175` | Key `m_Id: 6436276898955264`, `m_Key: "btn_lobbyRefresh\t"` (trailing tab). Its only reference is `LobbyScene.unity:1758`, **by ID** (`m_KeyId: 6436276898955264`, `m_Key:` empty). No script uses the key name. There is no other key named `btn_lobbyRefresh` (the neighbour at `:139` is `btn_lobby`). |

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/GameMaster.cs` | Remove `m_cardResolveDelay` |
| `Assets/Scripts/InGame/GameMaster/GameMaster.PackageManaging.cs` | Remove `ValidateCardOwner` and its XML comment |
| `Assets/Scripts/InGame/GameMaster/GameMaster.Processing.cs` | Add `$` at `:40` |
| `Assets/Scripts/InGame/GameMaster/GameMaster.Player.cs` | Log the leaving player's summary before removing it |
| `Assets/Scripts/InGame/PlayerController.cs` | Null-safe keyboard read |
| `Assets/Localization/UI_table Shared Data.asset` | Rename the key name |

## 4. Required changes

1. **N4:**
   - Delete the `m_cardResolveDelay` line (`GameMaster.cs:74`). Check that `GameMasterPrefab.prefab` doesn't serialize it (report #7 L9: the rule and timing fields aren't serialized). If it does, the leftover YAML key is harmless; don't edit the prefab by hand.
   - Delete `ValidateCardOwner` and its `/// <summary>` block (`PackageManaging.cs:484-497`).
2. **N5:** `Debug.Log($"Client disconnected: {_package.ClientId}");`
3. **N16:** in `HandlePlayerLeft`, before `m_players.Remove(session);` (`:60`), log the leaving player's summary with the same format and the same `m_debugMode` gate as `EndMatch`:
   ```csharp
   if (m_debugMode)
   {
       SuspiciousActivityLog _leavingLog = session.GetSuspiciousLog();
       if (_leavingLog.Total > 0)
           Debug.Log($"[Suspicious] client {session.OwnerClientId} match summary: {_leavingLog.ToSummaryString()}");
   }
   ```
   A small private helper `LogSuspiciousSummary(PlayerSession)`, called from both places, is also fine. Then `EndMatch` keeps its loop over the remaining players. The output must be identical.
4. **N23:** at the top of the input block in `SendMovementInput` (before `:167`):
   ```csharp
   Keyboard _keyboard = Keyboard.current;
   if (_keyboard == null)
       return;
   ```
   Then use `_keyboard` for the W/S/A/D reads and for the Space read inside `#if UNITY_EDITOR || DEVELOPMENT_BUILD`. Keep the `else if` order and the behaviour exactly as they are.
5. **L8:** rename the key `btn_lobbyRefresh\t` → `btn_lobbyRefresh`. Use the Localization Tables window (Window → Asset Management → Localization Tables → UI_table → edit the key name), or edit `m_Key` at `:175` to `m_Key: btn_lobbyRefresh`. **Keep `m_Id: 6436276898955264`.** Don't touch the en/ru/ja string tables: they reference entries by ID.

## 5. Explicit non-goals

- No other dead code; TASK-L10 covers it.
- No change to the `[Suspicious]` format or to when `EndMatch` logs.
- No input-system rework. The Tab key in `MultiplayerManager.Update:164` is removed by TASK-L10.
- No renaming of methods or files (the naming list is report-only).

## 6. Constraints and compatibility requirements

- The `GameMaster` partial-class layout and the region comments stay as they are.
- The `m_Id` of the localization key must not change, or the `LobbyScene` button loses its text.
- Behaviour stays identical apart from the five points above.

## 7. Acceptance criteria

1. The Unity Editor compile shows **0 warnings** from project scripts (CS0414 is gone).
2. `grep -rn ValidateCardOwner Assets/Scripts` finds nothing. Neither does `grep -rn m_cardResolveDelay Assets/Scripts`.
3. `GameMaster.Processing.cs:40` uses an interpolated string.
4. When a client disconnects mid-match with `m_debugMode` on and a non-empty suspicious log, the host prints that client's summary once.
5. `SendMovementInput` returns early when `Keyboard.current` is null. It reads `Keyboard.current` once per call.
6. The shared table has `m_Key: btn_lobbyRefresh` with the same `m_Id`, and the LobbyScene refresh button shows its text in en/ru/ja.

## 8. Regression tests

- Roslyn compile of the runtime sources with the project's response file. Exit 0, **no warnings**.
- If the existing harness style allows, a small test that `HandlePlayerLeft` logs the leaving session's summary (a session double with `Total > 0`) and that `EndMatch` still logs the others.

## 9. Manual Unity Editor checks

1. Main project: recompile. The Console shows no CS0414 and no other warning.
2. LobbyScene in en, ru, ja: the refresh button label is correct in each.
3. Two-player match (ParrelSync), `m_debugMode` on in `GameMasterPrefab`: stop Play Mode in the client mid-match. The host gets the forfeit result as before. If the client had a suspicious entry (rare in honest play), its summary line appears. Otherwise there's no new line: that's correct.
4. Movement W/A/S/D and the Space debug key work as before.

## 10. Open questions and assumptions

- Assumption: nothing outside `Assets/Scripts` references `ValidateCardOwner` or `m_cardResolveDelay`. Grep `Assets` for both names before deleting.
