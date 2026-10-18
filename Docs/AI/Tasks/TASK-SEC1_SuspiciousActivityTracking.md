# TASK-SEC1: Server-side tracking of suspicious actions per player session

- **Source:** owner request (2026-10-18), together with the mana system ([RESEARCH-ManaSystem](../Research/RESEARCH-ManaSystem.md))
- **Type:** security and diagnostics feature
- **Owner decisions (2026-10-18):**
  - **Server-side tracking only.** No punishment, kick or client UI.
  - Track **only actions an honest client can't produce**: a spoofed or invalid client ID, an over-budget confirm, selecting cards that aren't yours, and so on. **Normal rejections** (timing races, wrong phase, invalid targets, movement out of turn) are **not** suspicious and are not tracked.
- **Order:** **before** TASK-MANA2, which reports into this tracker. Prefer starting after H5 and H6 are merged, because they touch neighbouring files.
- **Implementer:** Codex

## 1. Objective

Every server-side rejection that **only a modified or forged client can cause** is recorded against the **sending player's** `PlayerSession`, with a type, the game phase, the time and a short detail. The data stays on the server and can be read later, for example by a future kick rule.

## 2. Current behavior

Rejections are logged as free text only. There's no per-player record. Each existing rejection point, and whether an honest client can trigger it:

| Location | Check | Can an honest client trigger it? | Tracked? |
|---|---|---|---|
| `PlayerController.InputServerRpc` (`PlayerController.cs:239-245`) | RPC sender ≠ owner | No | **Yes**: `RpcSenderMismatch` |
| `GameMaster.HandleConfirmTurn` (`PackageManaging.cs:179-183`) | sender ≠ session owner | No | **Yes**: `RpcSenderMismatch` |
| `HandleConfirmSelectionTurn` / `ValidateSelectionCards` (`PackageManaging.cs:306-370`) | ID not in own hand, not owned, duplicated, or 0 (C4) | No. The client only sends its own hand cards from local lists. | **Yes**: `InvalidCardSelection` |
| MANA2: over-budget confirm | play cost > mana | No (the client checks first) | **Yes**: `ManaOverBudget` (reported by MANA2) |
| `HandleCardControlPackage` (`PackageManaging.cs:129-137`) | card owned by **another player** | No. The client can only select cards on its own board. | **Yes**: `ForeignCardPackage` |
| `HandleCardControlPackage`: wrong phase, or not the active player | timing at a phase or turn change | **Yes** (race) | No |
| `HandleCardControlPackage`: card not found / not on board | a double send after the card has already resolved | **Yes** | No |
| `PlayerActionPhase`, target validation | target died or moved between selecting and sending | **Yes** | No |
| `InputQueueUpdate`, `!CanPlayerMove` (C3) | the client-side check is wrong during resolution and respawn (N3) | **Yes** | No |
| `HandlePlayerRespawnPoint`, invalid block | the block was destroyed meanwhile | **Yes** | No |
| `ResolvePlayerCardsPhase` `"SuspectActivity"` | dead code | — | No |

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/SuspiciousActivityLog.cs` | **New**: enum plus a plain C# log class |
| `Assets/Scripts/InGame/PlayerSession.cs` | Server-only log instance, `ReportSuspicious(...)`, a getter |
| `PlayerController.cs`, `GameMaster.PackageManaging.cs` | One `ReportSuspicious` call next to each **tracked** rejection |
| `SessionManager.GetPlayerSession(ulong clientId)` | Resolves the **sender's** session from a client ID |

## 4. Required changes

### 4.1 New file `SuspiciousActivityLog.cs`

```csharp
public enum SuspiciousActionType
{
    RpcSenderMismatch,
    InvalidCardSelection,
    ManaOverBudget,        // reported by TASK-MANA2
    ForeignCardPackage
}
```

`public class SuspiciousActivityLog` is a plain C# class with no `MonoBehaviour` and nothing networked. It holds:
- a per-type counter (`Dictionary<SuspiciousActionType, int>`) and a `Total`
- the **last 20** entries in a fixed ring buffer. Each entry has the type, `GamePhase`, `Time.time` and `detail`. The buffer size is a `const`.

Methods: `Record(type, phase, detail)`, `GetCount(type)`, `GetRecentEntries()` (a read-only copy), and `ToSummaryString()`.

The enum is meant to grow. New suspicious checks add a value at the **end**.

### 4.2 `PlayerSession`

- `private readonly SuspiciousActivityLog m_suspiciousLog = new();`
- `public void ReportSuspicious(SuspiciousActionType type, string detail)`:
  - If `!IsServer`, return.
  - Get the phase from `GameMaster.Instance` if it exists, otherwise `GamePhase.None`, and call `Record`.
  - Always log `Debug.LogWarning($"[Suspicious] client {OwnerClientId} {type}: {detail}")`.
- `public SuspiciousActivityLog GetSuspiciousLog()`, documented as server-only.

### 4.3 Report calls (tracked cases only)

Keep every existing check and log. Add the report next to each one:

| Place | Report on | Type |
|---|---|---|
| `PlayerController.InputServerRpc`, sender ≠ owner | `SessionManager.Instance.GetPlayerSession(_senderClientId)` (null → skip) | `RpcSenderMismatch` |
| `HandleConfirmTurn`, sender ≠ owner | the session of `senderClientId` (null → skip) | `RpcSenderMismatch` |
| `HandleConfirmSelectionTurn`, after validation, if `rejectedIds.Count > 0` | `session` | `InvalidCardSelection`; detail = the rejected IDs. Report **once per request**. |
| `HandleCardControlPackage`, `_card.m_ownerId.Value != _activePlayer.NetworkObjectId` | the session of `senderClientId` | `ForeignCardPackage` |

None of the other rejection points in §2 get a report.

### 4.4 Match summary

When the match ends, log `ToSummaryString()` for each player in `m_players`, but only for players with `Total > 0`, under `m_debugMode`. Do this in `GameMaster.EndMatch` and `AbortMatch`. If H5 isn't merged yet, add it only in `AbortMatch` and leave a `//Todo` for `EndMatch`.

## 5. Explicit non-goals

- Tracking normal rejections (§2, "No" rows).
- Kicking, banning, thresholds or any automatic reaction.
- Sending the data to clients, UI, analytics or files. **The player must never be told that suspicious activity was detected** (owner, 2026-10-18). Tracking is silent, and server logs only.
- Changing any accept/reject decision or existing log text.

## 6. Constraints and compatibility

- No renames. Additive only.
- Server-only. No new RPCs or NetworkVariables.
- Nothing runs on the honest path. Reporting happens only on the tracked rejections.
- The log is per match, because sessions are recreated for each match.

## 7. Acceptance criteria

- [ ] Each row in §4.3 produces exactly one record of the stated type on the sender's session, plus one `[Suspicious]` warning.
- [ ] The untracked rejections in §2 produce **no** records.
- [ ] A full honest match produces **zero** records and no `[Suspicious]` warnings.
- [ ] The ring buffer keeps only the last 20 entries, while the counters keep counting.
- [ ] Calling `ReportSuspicious` on a client does nothing.
- [ ] Compiles without new warnings.

## 8. Regression tests

Optional EditMode test: `SuspiciousActivityLog` is pure C#. Test the counters, the ring-buffer wrap-around and the summary.

Review checklist:
- the report always targets the **sender's** session
- only the 4 listed places report
- no accept/reject logic changed

## 9. Manual Unity Editor checks

1. ParrelSync host and client. Play a full honest match. There are no `[Suspicious]` warnings.
2. **Tamper checks (temporary client edits, don't commit):**
   - put the opponent's card ID in the selection confirm → `InvalidCardSelection`
   - send a card package with the opponent's board card → `ForeignCardPackage`
3. End the match with debug mode on. The summary lists the counts for the tampering player only.

## 10. Open questions and assumptions

- **A1:** `RequireOwnership = true` on `InputServerRpc` and `ConfirmTurnServerRpc` makes NGO drop non-owner calls before our code runs, so `RpcSenderMismatch` may never actually fire. It's kept as defence in depth.
- **Q1:** later, thresholds such as a kick after N records. Out of scope.
