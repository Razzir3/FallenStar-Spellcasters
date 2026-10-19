# TASK-M6: Null checks in `UpdatePlayerHP`

- **Source issue:** [Issue Report #6 → M6](../Reports/2026-10-19_IssueReport-6.md#-medium-13) (first reported in #1)
- **Severity:** 🟡 Medium (Confirmed, low probability)
- **Baseline:** `fe2d4fb`. Line numbers below are from this commit.
- **Order:** 8 of 13. Independent of the other tasks.
- **Implementer:** Codex

## 1. Objective

A pony death never throws in `UpdatePlayerHP`, even if the session's spawn point is missing or the controller has no session.

## 2. Current behavior

`GameMaster.UpdatePlayerHP` (`Assets/Scripts/InGame/GameMaster/GameMaster.Processing.cs:335-349`) runs on the server when HP drops to 0 or below:
```csharp
var _session = controller.GetSession();      // :339, can be null
_session.m_lifeCurrent.Value -= 1;           // :340
DespawnCharacter(_session);                  // :342
_session.GetSpawnPoint().IsOccupied = false; // :344, NRE if the spawn point is null
_session.SetCharacter(null);
_session.CharacterObjectId.Value = 0;
```
- `DespawnCharacter` (`GameMaster.Spawn.cs:172-208`) already frees the spawn point with a null check (`:193-198`). Line `:344` repeats that without one.
- `PlayerSession.GetSpawnPoint()` returns `m_spawnPoint`, which is set only by `SetSpawnPoint` during a (re)spawn.
- A throw here ends in H1's `RunGuarded` → `AbortMatch`, so the whole match is lost.

Callers: card damage (`ProcessActivity`), `ServerKillPlayerByHazard` (lava), and the debug HP key (`HPDeltaProcessing`).

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/GameMaster/GameMaster.Processing.cs` | `UpdatePlayerHP` |

## 4. Required changes

In `UpdatePlayerHP`, inside the `<= 0` branch:
1. After `var _session = controller.GetSession();`, add: if `_session == null`, log `Debug.LogError("UpdatePlayerHP: character has no session.")` and `return`.
2. Replace `:344` with:
   ```csharp
   SpawnPoint _spawnPoint = _session.GetSpawnPoint();
   if (_spawnPoint != null)
   {
       _spawnPoint.IsOccupied = false;
   }
   ```
3. Keep everything else, in the same order.

## 5. Explicit non-goals

- Changing `DespawnCharacter`, the respawn flow or how lives are counted.
- Removing the duplicated "free the spawn point" logic. It stays as a fallback for when `DespawnCharacter` returns early.

## 6. Constraints and compatibility requirements

- Server-only. No signature or asset changes.

## 7. Acceptance criteria

- [ ] `UpdatePlayerHP` has no unguarded dereference of `_session` or of its spawn point.
- [ ] Deaths from a card, from the lava and from the debug key still cost one life, despawn the pony and allow a respawn.
- [ ] Compiles with no new warnings.

## 8. Regression tests

No automated tests. Review the diff (two guards only).

## 9. Manual Unity Editor checks

1. **Lava:** drag a pony into the lava in the host Scene view. It loses 1 life, then respawns.
2. **Debug key:** press Space during your turn until HP reaches 0, about 10 presses. This works in the Editor with `m_debugMode` on. One life is lost, then a respawn.
3. **Card:** Iceshard the opponent repeatedly until HP reaches 0. Same result.
4. After a respawn on the same block as before, the pony can stand there (the spawn point was freed).

## 10. Open questions and assumptions

- **A1:** A missing spawn point can't happen in normal play. This is defensive hardening, and the review is the main check.
