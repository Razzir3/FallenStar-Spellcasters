# TASK-L10: Delete dead code, legacy scenes, backups and the VisualScripting dependency

- **Source issue:** [Issue Report #7 → L10](../Reports/2026-10-20_IssueReport-7.md#-low-23)
- **Severity:** 🟢 Low (Confirmed)
- **Baseline:** `b7e7b91`. Line numbers below are from this commit.
- **Order:** 18 of 20. Run it after the gameplay tasks: it touches `GameMaster.*`, `PlayerController` and `MultiplayerManager`, which they also edit. Re-check the line numbers then.
- **Owner decision (2026-10-20):** all four groups approved:
  - delete the backups
  - delete the legacy scenes and their UI scripts
  - delete the unused code, `test.md` and the VisualScripting package
  - (UTF-8 conversion is TASK-N24)
- **Implementer:** Codex

## 1. Objective

Remove everything listed in §4 that no scene, prefab or script in the build uses. The game builds and plays exactly as before, with one package fewer.

## 2. Current behavior (verified on `b7e7b91`)

### A. Backups (owner: delete)

- `26.06.19/` at the repo root: an old copy of scripts (61 tracked files). Not under `Assets`, so Unity ignores it.
- `Assets/_Recovery/` (`0.unity` + `.meta`, and `Assets/_Recovery.meta`): an Editor crash-recovery scene. It has `m_debugMode: 1` objects and uses `SpawnPanelUI`. It's not in the build list.

### B. Legacy scenes and their UI (owner: delete)

- `Assets/Scenes/SampleScene.unity` and `Assets/Scenes/MultiplayerScene.unity` (+ `.meta`). Both are in `ProjectSettings/EditorBuildSettings.asset` as **disabled** entries (`:8-12`). No script loads them by name (grep).
- `Assets/Scripts/UI/SpawnPanelUI.cs` (used only by SampleScene, MultiplayerScene and `_Recovery/0.unity`) and `Assets/Scripts/UI/DisconnectMenuUI.cs` (used only by SampleScene and MultiplayerScene).
- `MultiplayerManager` still references `DisconnectMenuUI`:
  - `private DisconnectMenuUI m_disconnectMenuUI;` (`:63`)
  - the Tab-key toggle in `Update()` (`:160-…`, which reads `Keyboard.current` without a null check: the N23 part not fixed in TASK-HYG1)
  - `CheckRef()` (`:1443-…`, `FindObjectsByType<DisconnectMenuUI>`)
  - These must go too, or the project won't compile.

### C. Unused code (owner: delete)

| Item | Where | Evidence |
|---|---|---|
| `DisconnectButtonUI.cs` | `Assets/Scripts/UI/` | GUID `cb37dbb7…` is referenced by no scene or prefab |
| `LocalizedTMPFormat.cs` | `Assets/Scripts/UI/` | GUID `581e8ae8…` is referenced by no scene or prefab |
| `TargetDataBackup.cs` (struct) | `Assets/Scripts/InGame/` | no code reference |
| `ResolvePlayerCardsPhase` | `GameMaster.GameLoop.cs:406-473` (with its `//ToDo`) | no caller |
| commented-out `CardMessageQueueUpdate` | `GameMaster.PackageManaging.cs:255-309` (with the `//Todo //Rework?` lines) and the commented call at `GameMaster.cs:148` | dead |
| `SendToClientRpc` echo | `GameMaster.PackageManaging.cs:310-319`. Its only caller is `GameMaster.Processing.cs:84-89`, together with `_packageNew` (`:13-20`) and `_rpcParams` (`:23-33`), which exist only for it. | the RPC only logs; the client does nothing with it |
| `PlayerController.ReceiveValidatedInput` and `m_inputQueue` | `PlayerController.cs:288-299`, `:25`, `:136` | no caller; the queue is never read |
| `ToPlayerControlPackage.cs` (struct) | `Assets/Scripts/InGame/` | unused once the three items above are gone |
| `MultiplayerManager.DisconnectClient()` | `MultiplayerManager.cs:1139-…` | no caller in scripts, scenes or prefabs |
| `test.md` | repo root | "# Test" only |

### D. VisualScripting (owner: delete)

- `Packages/manifest.json:18` has `"com.unity.visualscripting": "1.9.11"`. It's a direct dependency (`packages-lock.json` depth 0), and no other package depends on it.
- **Only uses:**
  - `using Unity.VisualScripting;` in `GameMaster.cs:5`, `GameMaster.Processing.cs:2` and `UI/GameAnnouncerUI.cs:6`
  - `(_shieldDelta).ConvertTo<int>()` at `GameMaster.Processing.cs:208` and `:225`
- `ConvertTo<int>()` on a `double` ends in `System.Convert.ChangeType(value, typeof(int))` (`ConversionUtility.NumericConversion`). That rounds to the nearest even number. `_shieldDelta` is `(int) * 0.5`, so `.5` values occur. The replacement must use the **same rounding**.
- No asset in `Assets` references VisualScripting types (grep in `.unity`, `.prefab` and `.asset`).

## 3. Relevant files and dependencies

See §2. Files deleted: 2 folders, 2 scenes, 6 scripts (with `.meta`) and `test.md`. Files edited:
- `EditorBuildSettings.asset`
- `MultiplayerManager.cs`
- `GameMaster.cs`, `GameMaster.GameLoop.cs`, `GameMaster.PackageManaging.cs`, `GameMaster.Processing.cs`
- `PlayerController.cs`
- `GameAnnouncerUI.cs`
- `Packages/manifest.json` (Unity updates `packages-lock.json` itself)

## 4. Required changes

1. **A:** `git rm -r 26.06.19 Assets/_Recovery Assets/_Recovery.meta`
2. **B:**
   - `git rm` both scenes with their `.meta`, and `SpawnPanelUI.cs` and `DisconnectMenuUI.cs` with their `.meta`.
   - Remove the two disabled scene entries from `EditorBuildSettings.asset`, using Build Profiles → Scene List or by editing the YAML. The enabled scenes and their order must stay identical: `LoginScene`, `MainMenuScene`, `SettingsScene`, `LobbyScene`, `GameScene`, `ResultScene`.
   - In `MultiplayerManager`, delete `m_disconnectMenuUI`, the Tab-key block in `Update()` and `CheckRef()`. If `Update()` is then empty, delete it. If the `using UnityEngine.InputSystem;` is then unused, delete it.
3. **C:**
   - Delete the scripts with their `.meta`.
   - Delete the methods and blocks listed, including their XML comments.
   - In `InputQueueUpdate`, keep the movement handling unchanged: the connected-client check at `:35-43` (with the N5 `$` fix from TASK-HYG1), the gates, `MoveInputProcessing` and the debug HP delta. Only the echo (`_packageNew`, `_rpcParams`, the second connected check and `SendToClientRpc` at `:84-89`) goes.
4. **D:**
   - Replace both `(_shieldDelta).ConvertTo<int>()` with `System.Convert.ToInt32(_shieldDelta)`. This is the same banker's rounding, so heal-over-cap results are unchanged.
   - Delete the three `using Unity.VisualScripting;`.
   - Remove the `com.unity.visualscripting` line from `Packages/manifest.json`, then let Unity resolve the packages. Commit the updated `packages-lock.json`.
   - If Unity then reports a missing type anywhere, stop and report it. Don't add the package back silently.
5. After all deletions: grep `Assets` for the deleted class names and scene names. 0 hits.

## 5. Explicit non-goals

- **Keep `m_lobbyCode` / `m_lobbyCodePanel` (`MultiplayerManager.cs:38-39`)** and the commented-out join-by-code block. They belong to the parked M11 feature (owner decision 2026-10-19), and their serialized Inspector references are needed when it's restored.
- Don't delete the third-party demo scenes (`500FreeSkillIcons`, `JMO Assets` and others).
- No refactoring of the remaining code.
- No renames (the naming list is report-only).

## 6. Constraints and compatibility requirements

- Delete with `git rm`, so that the `.meta` files go too. No orphan `.meta`, no missing-script warnings in the remaining scenes.
- `SendToClientRpc` is an RPC, so removing it changes `GameMaster`'s RPC table. Both peers use the same build.
- Behaviour must stay identical. The only runtime difference is one debug log line fewer per movement step.

## 7. Acceptance criteria

1. All listed files and folders are gone. `git status` shows only deletions plus the edits in §3.
2. The Editor compiles with 0 errors and no new warnings. Package resolution succeeds without VisualScripting.
3. A player build succeeds. Its scene list is the six enabled scenes in the same order.
4. Opening `GameScene`, `LobbyScene`, `MainMenuScene`, `LoginScene`, `SettingsScene` and `ResultScene` shows no missing-script warning.
5. Heal over the cap gives the same values as before (checklist B3: 50 → 80, and 80 → 105 when that row is run).
6. A full two-player match, movement, the leave flow, forfeit and the result screen work as before.

## 8. Regression tests

- Roslyn compile with the project's response file, after removing the VisualScripting reference. Exit 0.
- A unit check: `System.Convert.ToInt32(x)` equals the old `ConvertTo<int>()` for x in {0, 0.5, 1, 1.5, 2.5, 12.5, −0.5}. Run it once before removing the package, and paste the output in the report.

## 9. Manual Unity Editor checks

1. Reopen the project; Package Manager resolves without errors.
2. Open each of the six scenes: no missing scripts in the Hierarchy or the Console.
3. A two-player match: movement W/A/S/D, a heal over the cap (HealingHooves on yourself at full HP: the shield value is as before), Leave, a forfeit, the result screen.
4. Build Profiles: the scene list shows only the six scenes. Build and run the exe once.

## 10. Open questions and assumptions

- Assumption: nothing outside the repo (an external tool or a CI job) opens `SampleScene` or `MultiplayerScene`. The owner approved deleting them.
