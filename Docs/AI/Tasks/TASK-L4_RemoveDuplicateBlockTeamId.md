# TASK-L4: Remove the unused `BlockController.TeamId` NetworkVariable

- **Source issue:** [Issue Report #7 → L4](../Reports/2026-10-20_IssueReport-7.md#-low-23), corrected and with new evidence in the [manual-check review §2](../Reviews/2026-10-20_ManualChecks-Report6-7_Review.md#2-your-notes)
- **Severity:** 🟢 Low (Confirmed). Editor-only symptom.
- **Baseline:** `b7e7b91`
- **Order:** 3 of 20. Independent.
- **Implementer:** Codex

## 1. Objective

`BlockController` has one team NetworkVariable, `m_teamId`. The dead duplicate `TeamId` goes. This also stops the NGO Inspector exception `ArgumentException: An item with the same key has already been added. Key: Team Id`.

## 2. Current behavior

- `Assets/Scripts/InGame/BlockController.cs`:
  - `:12` `public NetworkVariable<int> m_teamId = new NetworkVariable<int>();`. It's written in `Initialize` (`:59`) and read by every team check:
    - `GameMaster.PackageManaging.cs:518`
    - `GameMaster.Processing.cs:132`, `:147`
    - `GameMaster.cs:269`
    - the M8 `GetBlockOwnerSession`
  - `:23` `public NetworkVariable<int> TeamId = new NetworkVariable<int>();`. **It's never written or read**: grep for `.TeamId` on blocks finds nothing. Every other `TeamId` reference is to `PlayerSession.TeamId` or `SpawnPoint.TeamId`.
  - Report #7 said the two are "read by different code". That was wrong. Only `m_teamId` is used.
- **Symptom:** NGO's custom Inspector `Unity.Netcode.Editor.NetworkBehaviourEditor.Init` (`Library/PackageCache/com.unity.netcode.gameobjects@…/Editor/NetworkBehaviourEditor.cs:36-49`) collects the public `NetworkVariable` fields into a `Dictionary` keyed by `ObjectNames.NicifyVariableName(field.Name)`. That function drops the `m_` prefix, so both fields become "Team Id" and `Dictionary.Add` throws. It happens whenever the Inspector shows a `BlockController`: a spawned block in Play Mode, or the `BlockBody` prefab. Seen twice in the owner's session (`Editor.log:20964`, `:20994`). It's Editor-only and has no gameplay effect.
- **Serialized data:** `Assets/Prefab/BoardPrefab/BlockBody.prefab` (the only asset with a `BlockController`) serializes `m_teamId` and `m_blockHP` but **not** `TeamId`. So the prefab needs no change.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/BlockController.cs` | Delete the `TeamId` field (`:23`) |

## 4. Required changes

1. Delete `public NetworkVariable<int> TeamId = new NetworkVariable<int>();` (`BlockController.cs:23`) and the blank line around it, so the `#region Variable` block stays tidy.
2. Grep the whole `Assets` folder (scripts, scenes, prefabs) for any use of a block's `TeamId`. There should be none; if there is one, switch it to `m_teamId` and list it in the report.

## 5. Explicit non-goals

- Don't rename `m_teamId`, even though NGO shows it as "Team Id". Renaming would break the serialized prefab data and every caller.
- No other `BlockController` changes.
- No NGO package changes.

## 6. Constraints and compatibility requirements

- Removing a NetworkVariable changes the behaviour's variable layout. Host and client always run the same build, so this is safe. Old player builds can't join new ones, which is already true for any NetworkVariable change.
- `BlockBody.prefab` must stay unchanged in git. If the Editor re-saves it, the only allowed diff is Unity's own formatting; revert anything else.

## 7. Acceptance criteria

1. `BlockController` declares exactly two NetworkVariables: `m_teamId` and `m_blockHP`.
2. The project compiles with no new warnings.
3. Selecting `BlockBody.prefab`, or a spawned block in Play Mode, shows the NGO NetworkVariable list in the Inspector with no exception.

## 8. Regression tests

- Roslyn compile of the runtime sources. Exit 0.
- Grep check: `grep -rnE "\bTeamId\b" Assets/Scripts/InGame/BlockController.cs` returns nothing.

## 9. Manual Unity Editor checks

1. Edit mode: select `Assets/Prefab/BoardPrefab/BlockBody.prefab`. There's no `ArgumentException … Team Id` in the Console, and the Inspector shows "Team Id" and "Block HP".
2. Two-player match: in the host Hierarchy, select a spawned block during the match. There's no exception, and "Team Id" shows 0 or 1 to match the owner.
3. Fireball on an opponent block, and the respawn block choice, work as before (they use `m_teamId`).

## 10. Open questions and assumptions

- None.
