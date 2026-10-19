# TASK-M3: Lava must kill a respawned pony even when its network ID was reused

- **Source issue:** [Issue Report #6 → M3](../Reports/2026-10-19_IssueReport-6.md#-medium-13) (first reported in #1)
- **Severity:** 🟡 Medium (Plausible)
- **Baseline:** `fe2d4fb`. Line numbers below are from this commit.
- **Order:** 7 of 13. Independent of the other tasks.
- **Implementer:** Codex

## 1. Objective

Every pony that touches the lava dies once, including a pony that respawned and got a recycled `NetworkObjectId`.

## 2. Current behavior

`Assets/Scripts/InGame/LavaKillZone.cs` (server only, `OnTriggerEnter` / `OnTriggerStay`):
- `m_killedCharacterIds` is a `HashSet<ulong>` of `NetworkObjectId`s (`:13-14`). It's filled at `:86` and checked at `:71-75`, and it's **never cleared** while `GameScene` is loaded.
- NGO recycles IDs: `NetworkManagerPrefab.prefab:65-66` has `RecycleNetworkIds: 1` and `NetworkIdRecycleDelay: 120`. A despawned ID can be given to a new object 120 s later.
- Each respawn creates a **new** pony instance (`GameMaster.Spawn.cs`: `Instantiate` + `SpawnAsPlayerObject`), and a death despawns and destroys the old one (`DespawnCharacter`, `Despawn(true)`).
- **Failure:** a pony dies with ID 50. At least 120 s later a new pony spawns and gets ID 50. That pony is ignored by the lava, can stand in it, and never dies there.

The set isn't needed to prevent a double kill. After a kill, `UpdatePlayerHP` sets the session's character to null and `CharacterObjectId` to 0, so the next trigger call fails `_session.GetCharacter() != _playerController` (`:83-84`). `ServerKillPlayerByHazard` checks the same thing and `m_lifeCurrent <= 0` (`GameMaster.Processing.cs:385-389`). Also, the old pony is despawned in the same call.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/LavaKillZone.cs` | Key the guard by instance instead of by network ID |

Read-only: `GameMaster.Processing.cs` (`ServerKillPlayerByHazard`, `UpdatePlayerHP`), `GameMaster.Spawn.cs`.

## 4. Required changes

1. Replace `HashSet<ulong> m_killedCharacterIds` with `HashSet<PlayerController> m_killedCharacters`.
2. In `TryKillPlayer`:
   - before the contains check, drop destroyed entries: `m_killedCharacters.RemoveWhere(c => c == null);` (Unity's `== null` is true for destroyed objects)
   - check and add `_playerController` instead of `_characterNetworkObjectId`
   - remove the now-unused `_characterNetworkObjectId` local; keep the `NetworkObject` / `IsSpawned` checks
3. Keep every other check and the debug log.

## 5. Explicit non-goals

- Changing the NGO ID recycling settings.
- Changing `ServerKillPlayerByHazard`, `UpdatePlayerHP` or the respawn flow.
- Other hazards (there are none).

## 6. Constraints and compatibility requirements

- Server-only behavior. The component stays a `MonoBehaviour` in `GameScene`; no scene edits.
- `m_debugMode` stays serialized under the same name.

## 7. Acceptance criteria

- [ ] A pony that respawned with a recycled ID dies in the lava (§9 check 2).
- [ ] A normal lava death still happens exactly once: lives drop by 1, not 2.
- [ ] Compiles with no new warnings.

## 8. Regression tests

No automated tests. **Review checklist:** no `NetworkObjectId` is used as a kill guard any more, and the set can't keep growing with destroyed objects.

## 9. Manual Unity Editor checks

1. **Normal:** ParrelSync host + client. In the host Scene view, drag a pony off its platform into the lava. It dies once (lives 3 → 2), the respawn works, and it can die in the lava again.
2. **Recycled ID, temporary prefab change (don't commit):**
   1. Set `NetworkIdRecycleDelay` to `1` in `Assets/Prefab/NetworkManagerPrefab.prefab` (NetworkManager → NetworkConfig).
   2. Start a match and kill pony A in the lava.
   3. Wait more than 1 s before confirming the respawn block, so the new pony is likely to get A's old ID.
   4. Compare the two IDs in the Inspector (`NetworkObject` → `NetworkObjectId`) or in the debug log.
   5. Drag the respawned pony into the lava. **Before the fix** it survives when the ID matches. **After the fix** it dies.
   6. Run `git restore Assets/Prefab/NetworkManagerPrefab.prefab`.

## 10. Open questions and assumptions

- **A1:** The ID match in check 2 is likely but not guaranteed. Other objects may spawn or despawn in between. If the IDs differ, repeat the death.
