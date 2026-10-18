# TASK-C2: Movement input must be bound to the RPC sender, not to a payload `ClientId`

- **Source issue:** [Issue Report C2](../Reports/2026-10-18_IssueReport.md#-critical)
- **Severity:** 🔴 Critical
- **Recommended order:** C5 → C6 → C1 → **C2** → C3 → C4. Implement after C1, because both touch `InputServerRpc` / `InputQueueUpdate`.
- **Implementer:** Codex

## 1. Objective

The server must decide which character to move from the identity of the client that sent the RPC (`ServerRpcParams.Receive.SenderClientId`), never from data inside the payload.

## 2. Current behavior

- Client: `PlayerController.SendMovementInput` fills `PlayerControlPackage.ClientId = NetworkManager.Singleton.LocalClientId` and calls `InputServerRpc(_package)` (`PlayerController.cs:231`). `[ServerRpc]` has the default `RequireOwnership = true`, so only the owner of the character object may invoke it. **The payload content is still client-controlled, though.**
- Server: `InputServerRpc` forwards the package unchanged to `GameMaster.HandlePlayerControlPackage` (`GameMaster.PackageManaging.cs:45`), which enqueues it.
- `GameMaster.InputQueueUpdate` (`GameMaster.Processing.cs:36-61`) uses `_package.ClientId` to:
  - check `ConnectedClients`
  - pick `ConnectedClients[_package.ClientId].PlayerObject` → `PlayerController` → `MoveInputProcessing` (+ `HPDeltaProcessing`)
  - target `SendToClientRpc`
- Result: a modified client can put the opponent's client ID in the payload and move the opponent's character, for example off its blocks into the lava (`LavaKillZone` → instant life loss).

## 3. Relevant files and dependencies

| File | Member | Role |
|---|---|---|
| `Assets/Scripts/InGame/PlayerController.cs` | `InputServerRpc`, `SendMovementInput` | RPC entry point |
| `Assets/Scripts/InGame/PlayerControlPackage.cs` | `ClientId` | Untrusted field |
| `Assets/Scripts/InGame/GameMaster/GameMaster.PackageManaging.cs` | `HandlePlayerControlPackage` | Enqueue |
| `Assets/Scripts/InGame/GameMaster/GameMaster.Processing.cs` | `InputQueueUpdate` | Consumer |
| `Assets/Scripts/InGame/GameMaster/GameMaster.Spawn.cs` | `SpawnCharacter`/`RespawnCharacter` → `SpawnAsPlayerObject(session.ClientId.Value)` | Why `PlayerObject` == the character |

Other `ClientId` fields (`PlayerSession.ClientId`, `SessionData.ClientId`) aren't involved.

## 4. Required changes

1. Change the RPC signature to `public void InputServerRpc(PlayerControlPackage _package, ServerRpcParams rpcParams = default)`. Keep the name and attribute. Existing call sites (`InputServerRpc(_package)`) must compile unchanged.
2. In `InputServerRpc`, before forwarding:
   - `ulong _senderClientId = rpcParams.Receive.SenderClientId;`
   - If `_senderClientId != OwnerClientId`, log a warning (use the existing style, e.g. `"Suspect Activity"`) and return.
   - Overwrite the payload: `_package.ClientId = _senderClientId;`
3. `InputQueueUpdate` keeps using `_package.ClientId`. After step 2 the value is trustworthy, because the only producer of queue items is `InputServerRpc` (verify with grep that `HandlePlayerControlPackage` has no other callers).
4. The client may keep filling `ClientId` (it's overwritten on the server). Don't remove the field.

## 5. Explicit non-goals

- Phase or turn validation on the server (that's **C3**).
- HP delta handling (that's **C1**).
- `IsHost` → `IsServer` in `HandlePlayerControlPackage` / `GameMaster.Update` (issue M4).
- The `PlayerObject == null` crash between death and respawn (issue M5, partly handled in C3).
- Changing `MoveInputProcessing` movement math.

## 6. Constraints and compatibility

- No renames (`AGENTS.md`). Adding an optional `ServerRpcParams` parameter is allowed. NGO 2.11.2 supports it for `[ServerRpc]` methods.
- Keep `RequireOwnership` at its default (true).
- The payload struct layout must not change.
- Don't modify prefabs or scenes.

## 7. Acceptance criteria

- [ ] The character moved by a package is always the one whose `OwnerClientId` equals the RPC sender.
- [ ] A package whose `ClientId` differs from the sender still moves only the sender's own character, because the ID is overwritten.
- [ ] Legitimate movement for host and client is unchanged: one 3-unit step per W/A/S/D press during your own `PlayerAction`.
- [ ] `HandlePlayerControlPackage` has exactly one caller: `PlayerController.InputServerRpc`.
- [ ] Compiles without new warnings.

## 8. Regression tests

There's no automated test infrastructure in the project. Code review checklist:
- `SenderClientId` is used, not `LocalClientId` or the payload
- The ownership check is present
- No other enqueue paths into `m_inputQueue` (`GameMaster.cs:41`)

## 9. Manual Unity Editor checks

1. ParrelSync, host and client. Start a match.
2. On the host's turn: the host moves with WASD, and the client's WASD doesn't move anything (client-side `CanPlayerMove` is false).
3. On the client's turn: the client moves its own pony, and the host's pony stays still.
4. **Spoof check (temporary, don't commit):** locally change the client's `SendMovementInput` to set `ClientId = 0` (the host). On the client's turn, press W. Expected: the **client's** pony moves and the host's doesn't. Revert the temporary change.
5. Check the Console for unexpected warnings during normal play.

## 10. Open questions and assumptions

- **A1:** `PlayerObject` for a client is its current character, because `SpawnAsPlayerObject` is used in `SpawnCharacter`/`RespawnCharacter`. If that ever changes, `InputQueueUpdate` must resolve the character through `PlayerSession.GetCharacter()` instead. C3 does this.
- **Q1:** should repeated spoof attempts disconnect the client? This spec assumes no; it only logs.
