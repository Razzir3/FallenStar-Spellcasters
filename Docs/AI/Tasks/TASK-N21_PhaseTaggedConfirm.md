# TASK-N21: A late confirm can't be read as a different action

- **Source issue:** [Issue Report #7 → N21](../Reports/2026-10-20_IssueReport-7.md#-low-23)
- **Severity:** 🟢 Low (Plausible: a latency window)
- **Baseline:** `b7e7b91`. Line numbers below are from this commit.
- **Order:** 9 of 20. Before TASK-N26 (both touch `PlayerSession.ConfirmTurn`).
- **Implementer:** Codex

## 1. Objective

The server handles a confirm only in the phase the player pressed it for. A selection confirm that arrives after Card Selection has ended no longer ends that player's Turn. A respawn-block confirm that arrives after the pause no longer ends the Turn either.

## 2. Current behavior

- **Client:** `PlayerUI.cs:601` → `PlayerSession.ConfirmTurn()` (`PlayerSession.cs:170-202`):
  - in `RespawnWait`, it sends `ConfirmTurnServerRpc(empty, empty, m_respawnBlockNetworkId)`
  - otherwise it prunes and sends the selected play and discard IDs, then clears the local selection (`:201`)
  - the RPC (`:204-217`) carries no information about which action it means
- **Server:** `GameMaster.PackageManaging.cs:180-251` `HandleConfirmTurn` checks the sender, then switches on the **current** phase (`:207`):
  - `CardSelection` → accept the selection (`HandleConfirmSelectionTurn`)
  - `PlayerAction` / `ResolveCards` → `HandlePlayerActionConfirm(player)` (`:423-431`): if the sender is the active player, `m_activePlayerConfirmedAction = true`, which **ends their Turn**
  - `RespawnWait` → `HandlePlayerRespawnPoint(player, respawnBlockNetworkId)`. A selection confirm has ID 0 here, so it's rejected with "Respawn point rejected: NetworkObjectId is 0."
- **Windows where this goes wrong:**
  1. A selection confirm sent in the last moment of `CardSelection` arrives in `PlayerAction`. If the sender acts first, their Turn ends at once and the selection is lost.
  2. A selection confirm that arrives just after a death pause starts is lost (rejected as a respawn point).
  3. A respawn-block confirm that arrives just after the pause timed out (`m_respawnWaitTime`) lands in the restored phase. In `PlayerAction` it ends the sender's Turn if they're active.
- The client has already cleared its selection after sending (`:201`), so in cases 1 and 2 the player has to select again, if there's still time.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/PlayerSession.cs` | `ConfirmTurn` sends the intended phase; `ConfirmTurnServerRpc` gets one parameter |
| `Assets/Scripts/InGame/GameMaster/GameMaster.PackageManaging.cs` | `HandleConfirmTurn` ignores a confirm whose intended phase doesn't match |
| `Assets/Scripts/UI/PlayerUI.cs` | No change (the caller stays `ConfirmTurn()`) |

`GamePhase` is the existing enum used by `m_gamePhase`. It's already network-serializable as an enum.

## 4. Required changes

1. **`PlayerSession.ConfirmTurn()`:** read `GamePhase _intended = GameMaster.Instance.m_gamePhase.Value;` once at the start. Pass it as a new **first** parameter of `ConfirmTurnServerRpc(GamePhase intendedPhase, ulong[] …, ulong[] …, ulong respawnBlockNetworkId, ServerRpcParams rpcParams = default)`, in both branches.
2. **`ConfirmTurnServerRpc`:** forward it to `GameMaster.Instance.HandleConfirmTurn(sender, this, intendedPhase, play, discard, respawnBlock)`. Add a null check for `GameMaster.Instance`, like the other RPCs since N20.
3. **`HandleConfirmTurn`:** after the sender checks and before the `switch`, add
   ```csharp
   if (!IsConfirmForCurrentPhase(intendedPhase, m_gamePhase.Value))
   {
       if (m_debugMode)
           Debug.Log($"Confirm ignored: sent for {intendedPhase}, server is in {m_gamePhase.Value}.");
       return;
   }
   ```
   `IsConfirmForCurrentPhase` (private, static):
   - `CardSelection` ↔ `CardSelection`
   - `PlayerAction` or `ResolveCards` ↔ `PlayerAction` or `ResolveCards`
   - `RespawnWait` ↔ `RespawnWait`
   - anything else → `false`
4. **Don't** report a phase mismatch as suspicious activity: an honest player with latency hits it.
5. **Don't** send `SelectionRejectedClientRpc` for a mismatch. Its UI shows the "not enough mana" message (`PlayerUI.OnSelectionRejected` → `ShowManaNotEnough`), which would be wrong here.

## 5. Explicit non-goals

- No grace period that accepts a late selection after `CardSelection` has ended.
- No new on-screen message for an ignored confirm. Like the N20 Send feedback, this is a feature gap.
- No change to the `CardSelection` validation (MANA2: first valid confirm is final), the action-confirm rule or the respawn-point rules.
- The selection-clearing rule is TASK-N26.

## 6. Constraints and compatibility requirements

- An RPC signature change breaks compatibility with older builds. Host and client always run the same build.
- Keep the existing sender check and the `RpcSenderMismatch` report exactly as they are, before the new check.
- `GamePhase` values must not be renumbered.

## 7. Acceptance criteria

1. A confirm sent while the client saw `CardSelection` and received in `PlayerAction` doesn't change `m_activePlayerConfirmedAction`. The sender's Turn isn't ended.
2. A respawn-block confirm received after the pause has ended doesn't end the sender's Turn.
3. Normal flows are unchanged:
   - selection confirm in `CardSelection`
   - end-turn confirm in `PlayerAction`
   - respawn-block confirm in `RespawnWait`
4. A mismatch logs one debug line (only with `m_debugMode`) and no `[Suspicious]` entry.

## 8. Regression tests

- Harness:
  - each pair of intended and current phase in `IsConfirmForCurrentPhase`
  - `HandleConfirmTurn` with a mismatched phase doesn't call the selection, action or respawn handlers
- Roslyn compile. Exit 0.

## 9. Manual Unity Editor checks

1. Normal two-player round: select → Confirm in Card Selection. In your Turn → Send → Confirm ends the Turn. Respawn: pick a block → Confirm. All as before.
2. Late confirm (needs some luck, or a temporary `m_cardSelectionTime = 5` in `GameMasterPrefab`; revert it afterwards): press Confirm in the last few frames of Card Selection. If the confirm arrives late, the host logs `Confirm ignored: sent for CardSelection, server is in PlayerAction`, and your Turn **doesn't** end by itself.
3. Respawn pause: let the respawn timer almost run out, then pick a block and Confirm. If it arrives late, your restored Turn continues.

## 10. Open questions and assumptions

- Assumption (default chosen by Claude): an ignored late confirm simply loses that selection. The owner can ask later for a grace period or an on-screen message.
