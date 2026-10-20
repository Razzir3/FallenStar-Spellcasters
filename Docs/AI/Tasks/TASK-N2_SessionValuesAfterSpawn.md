# TASK-N2: Write the PlayerSession start values during spawn, not before it

- **Source issue:** [Issue Report #7 → N2](../Reports/2026-10-20_IssueReport-7.md#-low-23). Seen in the owner's manual check A1.1: [review §2](../Reviews/2026-10-20_ManualChecks-Report6-7_Review.md#2-your-notes).
- **Severity:** 🟢 Low (Confirmed). Log noise in every match; no wrong values.
- **Baseline:** `b7e7b91`
- **Order:** 4 of 20. Independent.
- **Implementer:** Codex

## 1. Objective

The warning `NetworkVariable is written to, but doesn't know its NetworkBehaviour yet. Are you modifying a NetworkVariable before the NetworkObject is spawned?` no longer appears when a match starts. Clients still receive the correct `ClientId`, `TeamId` and lives in their **first** `OnNetworkSpawn`.

## 2. Current behavior

- `MultiplayerScripts/SessionManager.cs` `CreateSession` (around `:220-262`):
  - `:245` `_session.Initialize(clientId, _teamAssing);`
  - `:247` `_networkObject.SpawnWithOwnership(clientId);`
- `InGame/PlayerSession.cs:636-651` `Initialize(ulong clientId, int teamId)` writes `ClientId.Value`, `TeamId.Value` and `m_lifeCurrent.Value` (plus the plain field `m_lifeMax = 3`).
- NGO 2.11.2 links a NetworkVariable to its behaviour in `InitializeVariables()`, called from `InternalOnNetworkSpawn()` just **before** `OnNetworkSpawn()` (`NetworkBehaviour.cs:797-803`). A write before that hits `MarkNetworkBehaviourDirty` with `m_NetworkBehaviour == null` and logs the warning (`NetworkVariableBase.cs:314-323`).
- Today the values still arrive, because the initial spawn payload carries the current values. The warning is logged 4 times per match in the player build (`Player.log`) and in the Editor.
- `PlayerSession.OnNetworkSpawn` (`:592-600`) only starts `RegisterLocalPlayerWhenReady` for the owner.
- Right after `SpawnWithOwnership`, `SessionManager` reads `_session.TeamId.Value` for a debug log (`:257-261`) and adds the session to `m_playerSessions` / `SessionDataList`.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/PlayerSession.cs` | `Initialize` stores the values; `OnNetworkSpawn` applies them on the server |
| `Assets/Scripts/MultiplayerScripts/SessionManager.cs` | No change expected (call order stays). Read it to confirm. |

Dependents to check for timing: everything that reads `PlayerSession.TeamId` or `ClientId` during or right after spawn:
- `PlayerCardWorldArea` binding by `TeamId`
- `CardWorldViewManager.FindAreas` (`m_sessionSearchId`)
- `GameMaster.SpawnCharacter` / `PlayerController.Initialize` (`m_playerTeam.Value = m_session.TeamId.Value`)
- `GameAnnouncerUI`
- `PlayerUI`

## 4. Required changes

1. **`PlayerSession`:** add private fields for the pending start values, e.g. `m_pendingClientId`, `m_pendingTeamId`, `m_hasPendingInit`.
2. **`Initialize(clientId, teamId)`:**
   - Store the pending values and set `m_lifeMax = 3` (a plain field, so it's safe before spawn).
   - If the object **is already spawned** (`IsSpawned`), apply at once (step 4). This keeps any later re-initialization working.
   - Keep the two debug logs. Log the **pending** values, so the log text stays the same.
3. **`OnNetworkSpawn`:** after `base.OnNetworkSpawn()`, if `IsServer && m_hasPendingInit`, apply the pending values (step 4), then continue with the existing owner registration.
4. **Applying** (a private `ApplyPendingInit()`): `ClientId.Value = m_pendingClientId; TeamId.Value = m_pendingTeamId; m_lifeCurrent.Value = m_lifeMax; m_hasPendingInit = false;`
5. `SessionManager.CreateSession` keeps calling `Initialize` before `SpawnWithOwnership`. `OnNetworkSpawn` runs synchronously inside `SpawnWithOwnership` on the server, so the `TeamId` debug log after it prints the real team.
6. Grep for other writes to any NetworkVariable before `Spawn()` in `Assets/Scripts`. Known pattern: `NetworkCard` writes `m_isFaceUp` **after** `Spawn()`, which is fine. List what you find in the report, and fix any pre-spawn write the same way only if it logs this warning in a match.

## 5. Explicit non-goals

- Don't move the `Initialize` call after `SpawnWithOwnership`. Clients would then see `TeamId = -1` in their first `OnNetworkSpawn`, and the team bindings would flicker or fail.
- Don't use `OnNetworkPreSpawn`. It runs before `InitializeVariables`, so it would log the same warning.
- No change to team assignment (`GetFreeSlot`) or lives (3).
- Don't silence NGO with `NetworkVariableBase.IgnoreInitializeWarning`.

## 6. Constraints and compatibility requirements

- NGO 2.11.2: on the server, `NetworkObject.SpawnInternal` first calls `AuthorityLocalSpawn`, which runs `OnNetworkSpawn` (`NetworkObject.cs:1852`, `NetworkSpawnManager.cs:1379`). Only after that does it send the spawn message to each client (`SendSpawnCallForObject`, `NetworkObject.cs:1862-1874`). So values written in `OnNetworkSpawn` are part of the initial payload. **Still verify this in the client log** (§9). If it doesn't hold, stop and report instead of trying another approach.
- Server-only writes. Clients never call `Initialize`.
- Keep the field names `ClientId`, `TeamId` and `m_lifeCurrent`: they're serialized NetworkVariables on `PlayerSessionPrefab`.

## 7. Acceptance criteria

1. Starting a two-player match logs **0** `NetworkVariable is written to, but doesn't know its NetworkBehaviour yet` warnings on the host and the client (Editor and player build).
2. On the client, a temporary debug line in `PlayerSession.OnNetworkSpawn` shows `TeamId` 0/1 and `ClientId` 0/1, not -1/0, for both sessions. Remove the line before finishing, or gate it with `m_debugMode`.
3. Both players start with 3 lives. Team colors, the card areas and the HUD are correct for both.

## 8. Regression tests

- Roslyn compile. Exit 0.
- If the harness style allows: a `PlayerSession` double test for `Initialize` before and after spawn. Pending values are applied once; when spawned, they're applied at once.

## 9. Manual Unity Editor checks

1. ParrelSync, two players: host creates a lobby, the client joins, start. Search both Consoles for "doesn't know its NetworkBehaviour": 0 results.
2. Both ponies have their team colors. Each player's hand and board areas show on the correct side. The HUD shows 3 lives for both.
3. A second match in the same Play session behaves the same.
4. Player build (exe as host, Editor as client): no warning in `Player.log`.

## 10. Open questions and assumptions

- Assumption: nothing reads `PlayerSession.TeamId` on the server between `Initialize` and `SpawnWithOwnership`. Confirm by reading `CreateSession`.
