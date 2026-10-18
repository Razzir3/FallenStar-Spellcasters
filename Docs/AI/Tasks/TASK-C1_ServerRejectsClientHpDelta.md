# TASK-C1: The server must not apply a client-supplied HP delta

- **Source issue:** [Issue Report C1](../Reports/2026-10-18_IssueReport.md#-critical)
- **Severity:** 🔴 Critical
- **Recommended order:** C5 → C6 → **C1** → C2 → C3 → C4. C1, C2 and C3 touch the same methods, so implement them in that order, one at a time.
- **Author:** Claude (architecture/review agent)
- **Implementer:** Codex

## 1. Objective

The server must never change a character's HP based on a value sent by a client. HP may change only through authoritative server logic: card effects (`ProcessActivity`) and hazards (`ServerKillPlayerByHazard`).

## 2. Current behavior

- `PlayerControlPackage` (`Assets/Scripts/InGame/PlayerControlPackage.cs`) carries `int s_hpDelta`, which is serialized over the network.
- Client: `PlayerController.SendMovementInput` (`PlayerController.cs:184-186`) sets `_hpDelta -= 10` when **Space** is pressed (a debug feature with no compile guard). It sends the package through `InputServerRpc` (`:231`) if `CanPlayerMove` is true on the client.
- Server: `GameMaster.InputQueueUpdate` (`GameMaster.Processing.cs:53`) calls `HPDeltaProcessing` (`:307`), which runs `controller.m_hpCurrent.Value += _package.s_hpDelta` with **no validation**, then `UpdatePlayerHP`.
- A modified client can send any value, such as `+10000` or `-100`. Combined with C2, it can also apply it to the opponent's character.

## 3. Relevant files and dependencies

| File | Member | Role |
|---|---|---|
| `Assets/Scripts/InGame/PlayerControlPackage.cs` | `s_hpDelta`, `NetworkSerialize` | Network payload |
| `Assets/Scripts/InGame/PlayerController.cs` | `SendMovementInput`, `InputServerRpc` | Client sender |
| `Assets/Scripts/InGame/GameMaster/GameMaster.Processing.cs` | `InputQueueUpdate`, `HPDeltaProcessing`, `UpdatePlayerHP` | Server consumer |
| `Assets/Scripts/InGame/GameMaster.cs` | `m_debugMode` (serialized, `1` in `GameMasterPrefab.prefab`) | Debug flag |

There are no other references to `s_hpDelta` (verified with grep).

## 4. Required changes

1. **Server (mandatory):** in `InputQueueUpdate`, the call to `HPDeltaProcessing` must run only when **all** of these are true:
   - the build is the Editor or a development build: `#if UNITY_EDITOR || DEVELOPMENT_BUILD`
   - `GameMaster.m_debugMode == true`

   In every other build configuration the client's `s_hpDelta` is ignored completely.
2. **Client:** in `PlayerController.SendMovementInput`, wrap the Space-key debug block (`:184-187`) in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`, so release builds never send a non-zero delta.
3. Keep the `s_hpDelta` field, its serialization order and the `HPDeltaProcessing` method signature unchanged. Don't rename or remove them; see `AGENTS.md`.
4. Leave `HPDeltaProcessing` / `UpdatePlayerHP` logic as is otherwise.

## 5. Explicit non-goals

- Fixing the spoofed `ClientId` (that's **C2**) or the phase check (that's **C3**). Don't touch the controller lookup in this task.
- Changing `UpdatePlayerHP`, death or respawn logic, or any card effect.
- Removing `PlayerControlPackage.s_hpDelta` from the struct.
- Any refactor of `InputQueueUpdate` beyond the guard.

## 6. Constraints and compatibility

- Don't rename variables, classes or methods (`AGENTS.md`). If you notice a typo, report it.
- Don't touch `Library`, `Temp`, `Logs`, `obj`, scenes, prefabs or `.meta` files.
- The network struct layout must not change, so host and client builds stay compatible.
- Code style: `m_` fields, `_local` variables, and Allman braces like the surrounding code.

## 7. Acceptance criteria

- [ ] In a **release** (non-development) player build, `HPDeltaProcessing` is never reached, whatever the package contents.
- [ ] In the Editor with `m_debugMode = true`, pressing Space during your own `PlayerAction` turn still deals −10 HP to your own character, as it does today.
- [ ] In the Editor with `m_debugMode = false`, Space has no effect on HP.
- [ ] Movement (WASD) behaves exactly as before.
- [ ] No compiler warnings or errors introduced.

## 8. Regression tests

The project has no automated test assemblies (no `.asmdef`, no Tests folder), so no automated tests are required. If Codex adds an EditMode test, it must not need NGO runtime objects.

Code review checklist:
- `HPDeltaProcessing` call site guarded by both conditions
- Space block guarded with the same define symbols
- No other HP write paths added

## 9. Manual Unity Editor checks

1. Open the project. The Console must show no compile errors.
2. ParrelSync: run host and client, start a match, and reach `PlayerAction` for the host.
   - With `GameMaster` `m_debugMode` on: Space lowers the host's HP by 10.
   - Turn `m_debugMode` off in the prefab (don't save, or revert afterwards): Space does nothing.
3. Repeat on the client instance (client's own turn).
4. Make a **non-development** standalone build if C5 is merged and confirm Space does nothing.

## 10. Open questions and assumptions

- **Q1 (owner decision):** should the debug damage key be removed entirely instead of guarded? This spec assumes "keep it for Editor/development only", which preserves current developer behavior.
- **A1:** `DEVELOPMENT_BUILD` is an acceptable define for "debug allowed". If the team uses its own define, replace it consistently in both places.
- **A2:** Until C2 is merged, the Editor debug path still uses the spoofable `ClientId`. That's acceptable because the path only exists in Editor/dev builds.
