# TASK-M4: Use `IsServer` instead of `IsHost` in `GameMaster`

- **Source issue:** [Issue Report #6 → M4](../Reports/2026-10-19_IssueReport-6.md#-medium-13) (first reported in #1)
- **Severity:** 🟡 Medium (Confirmed; matters only on a dedicated server, which isn't used today)
- **Baseline:** `fe2d4fb`. Line numbers below are from this commit.
- **Order:** 9 of 13. Independent of the other tasks.
- **Implementer:** Codex

## 1. Objective

The authoritative gameplay code in `GameMaster` runs whenever the instance is the server, not only when it's a host (server + local client).

## 2. Current behavior

- `GameMaster.Update` (`Assets/Scripts/InGame/GameMaster.cs:135-146`): `if (!IsHost) return;` before `InputQueueUpdate()`. The comment at `:142` says "check if we are the host".
- `GameMaster.HandlePlayerControlPackage` (`Assets/Scripts/InGame/GameMaster/GameMaster.PackageManaging.cs:46-66`): `if (!IsHost)` rejects the package. It's called from `PlayerController`'s movement server RPC (`PlayerController.cs:267`).
- On a dedicated server, `IsHost` is false, so every movement input would be dropped. Everything else in `GameMaster` already uses `IsServer`.

The `IsHost` uses in `MultiplayerManager` (`:127`, `:315`, `:326`, `:355`, `:533`, `:584`) are about the **lobby host role** (who deletes the lobby, who sends heartbeats). They're correct as they are.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/GameMaster.cs` | `Update`: `IsHost` → `IsServer`; update the comment |
| `Assets/Scripts/InGame/GameMaster/GameMaster.PackageManaging.cs` | `HandlePlayerControlPackage`: `IsHost` → `IsServer` |

## 4. Required changes

1. `GameMaster.cs:143`: `if (!IsServer) return;`. Change the comment above it to "Only the server handles input packages."
2. `GameMaster.PackageManaging.cs:50`: `if (!IsServer)`. Keep the debug log.

## 5. Explicit non-goals

- `MultiplayerManager`'s lobby-host checks.
- Building or supporting a dedicated server.
- N3 (client-side `CanPlayerMove` reads server-only flags).

## 6. Constraints and compatibility requirements

- In host mode `IsServer` is true wherever `IsHost` was, so host behavior doesn't change.
- No signature or asset changes.

## 7. Acceptance criteria

- [ ] No `IsHost` is left in `Assets/Scripts/InGame/**`.
- [ ] Host and client movement in their own turns works as before.
- [ ] Compiles with no new warnings.

## 8. Regression tests

No automated tests. Review the two-line diff.

## 9. Manual Unity Editor checks

1. ParrelSync host + client. During your own action turn, move with the movement keys, first on the host and then on the client. Each step moves one tile, and moving outside your turn is still rejected.

## 10. Open questions and assumptions

- **A1:** A dedicated server isn't planned now. The change is free in host mode and removes a trap for later.
