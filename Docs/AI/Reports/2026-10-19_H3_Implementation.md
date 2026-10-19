# H3 implementation: block heal overflow

## 1. Task summary

Implemented `TASK-H3_BlockHealOverCap.md`. Block healing now uses the block's own maximum and converts 50% of overflow into extra HP, with the same conversion as character healing. The block heal branch no longer dereferences the character target.

## 2. Files changed

- `Assets/Scripts/InGame/BlockController.cs`
- `Assets/Scripts/InGame/GameMaster/GameMaster.Processing.cs`
- `Docs/AI/Architecture/Architecture.md`
- `Docs/AI/Architecture/Dependencies.md`
- `Docs/AI/Architecture/GameLoop.md`
- This implementation report.

Existing task deletions, new task specifications and Issue Report #5 were present before implementation and were preserved. No scenes, prefabs, card assets, packages or Inspector references were changed. No commit or push was performed.

## 3. Important implementation details

- Added private `m_blockHPMax = 100` with a server-only comment and public `GetMaxHP()` getter. `Initialize(PlayerSession session, int hpmax = 100)` stores `hpmax` next to the existing current-HP assignment, behind its existing `IsServer` guard.
- The block Heal branch reads the getter once and uses that maximum in both the threshold and overflow calculation. The formula remains `* 0.5`, followed by `(_shieldDelta).ConvertTo<int>()`.
- Character healing, target-type checks, `UpdateBlockHp`, Damage, DrawCard and `CheckActivity` remain unchanged. No shared helper was introduced.
- `CreateBlock` spawns each network object before calling `Initialize` on the server. The active `ProcessActivity` call is in the server game loop; the new maximum is neither serialized nor networked. Existing current-HP replication is unchanged.
- Updated only the relevant architecture descriptions and marked the old block-heal risk resolved. Broader documentation drift remains outside H3.
- Required Inspector setup: none.

## 4. Tests actually executed and results

### Runtime script compilation

Invoked Unity 6000.3.13f1's bundled Roslyn compiler with the project's existing Editor `Assembly-CSharp.rsp` references, definitions, source list and analyzers. Redirected assembly outputs to ignored `build/task-h3-verification/`, without replacing Unity's cached assemblies.

Result: exit code 0, no errors. Only the existing CS0114 (`PlayerUI.OnDestroy`) and CS0414 (`GameMaster.m_cardResolveDelay`) warnings were emitted. This verifies C# compilation; it is not an Editor import, NGO IL postprocessing, player build or Play Mode result.

Evidence: `build/task-h3-verification/compile.rsp` and `compile-results.txt`.

### Isolated behavioral checks

Ran `build/task-h3-verification/verify.ps1`. It compiles the actual complete `BlockController` source and actual `ProcessActivity` method with Unity/NGO doubles, using the project's real `Unity.VisualScripting.Core.dll` conversion implementation under Unity's Mono runtime.

Result: 139 assertions passed. Checks covered all three supported block target types, normal healing, overflow, already-over-cap HP, custom maximum 150, exact-cap healing, odd-overflow rounding, maximum/current-HP initialization, client initialization guard, retained block update call and target-type guard. Block target IDs deliberately resolve to no character/session, reproducing the original null-character condition. Each result was compared with the unchanged character branch.

Examples with heal 30: `(50, max 100) -> 80`, `(80, 100) -> 105`, `(100, 100) -> 115`, `(110, 100) -> 120`, `(80, 150) -> 110`, `(140, 150) -> 160`. Odd overflow follows the existing conversion: `(100, 100, heal 1) -> 100`, `(100, 100, heal 3) -> 102`.

Static checks also passed: character branch matches HEAD, no character reference or literal 100 remains in the block heal branch, and `UpdateBlockHp` remains. Final scoped Git diff and `git diff --check` were reviewed.

The harness and generated outputs are git-ignored verification artifacts, not production scripts or permanent Unity tests. Its compiler reports the existing unused private `m_spawnPoint` field because the isolated harness does not use Unity's warning suppressions.

## 5. Remaining risks / checks not executed

- No Unity Play Mode session, separate host/client replication test or full player build was run for H3. The isolated checks replace Unity/NGO objects and block-update side effects with doubles.
- No current card targets blocks with Heal, so normal gameplay does not exercise this branch without temporary test authoring.
- The maximum intentionally remains server-only; clients read replicated current HP. Future client maximum displays would need a separate specification.

## 6. Manual Unity Editor verification

1. In a temporary local copy, change only HealingHooves activity 0 target from `SelfPlayer` (21) to `SelfBlock` (1). Do not retain the test asset change.
2. Run a ParrelSync host and client. In round 2 or later, heal a full own block: check current HP becomes 115 on both peers, without an exception or `GameMaster: match aborted` log.
3. Damage an opponent block with Fireball (100 -> 50), then have its owner heal it (50 -> 80). Also check 80 -> 105 and character 80 -> 105 with heal 30 in an appropriate local test setup.
4. Inspect `BlockController.m_blockHP` on host and client. Restore the temporary card target to `SelfPlayer` and verify no test assets or scene changes remain.
