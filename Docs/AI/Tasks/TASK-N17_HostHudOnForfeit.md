# TASK-N17: The host's HUD stays visible until the result screen after a forfeit

- **Source issue:** [Issue Report #7 → N17](../Reports/2026-10-20_IssueReport-7.md#-info)
- **Severity:** ⚪ Info (Confirmed, cosmetic)
- **Baseline:** `b7e7b91`. Line numbers below are from this commit.
- **Order:** 14 of 20. Independent.
- **Implementer:** Codex

## 1. Objective

When the **opponent** disconnects, the host's own HUD (HP, lives, mana, hand) stays as it is until `ResultScene` loads. Only the instance whose **own** connection ended clears its local HUD.

## 2. Current behavior

- `MultiplayerScripts/SessionManager.cs` `OnClientDisconnected(ulong clientId)` (`:166-209`), on every instance where it runs:
  1. on the server, `GameMaster.HandlePlayerLeft` → `EndMatch(…, OpponentDisconnected)` → `MatchEndedClientRpc` → `LeaveMatchToResultScene` (`:168-172`)
  2. frees the game slot and the `SessionDataList` entry (`:179-192`)
  3. **always** calls `PlayerUI.Instance.ClearLocalUI()` (`:193-196`)
  4. despawns the leaving session (`:198-208`)
- On the host, `clientId` is the **remote** client. Step 3 clears the host's own HUD (`UI/PlayerUI.cs:541-…`: unsubscribes, empties HP, lives, mana and hand), so the HUD is blank for the moment before `ResultScene` loads.
- On a client, NGO raises the callback with the client's **own** ID when it loses the connection. There, clearing the local HUD is correct.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/MultiplayerScripts/SessionManager.cs` | Clear the local HUD only for the local client's own disconnect |

## 4. Required changes

1. In `OnClientDisconnected`, change `:193-196` to:
   ```csharp
   bool _isLocalDisconnect = NetworkManager.Singleton != null &&
       clientId == NetworkManager.Singleton.LocalClientId;

   if (_isLocalDisconnect && PlayerUI.Instance != null)
   {
       PlayerUI.Instance.ClearLocalUI();
   }
   ```
2. Nothing else in the method changes. The order of steps 1, 2 and 4 stays.

## 5. Explicit non-goals

- No change to the forfeit rule (H6), `EndMatch` or the scene flow.
- No change to `PlayerUI.ClearLocalUI()`. `PlayerUI.Update` still calls it when `LocalPlayer` is missing.

## 6. Constraints and compatibility requirements

- `PlayerUI` is destroyed with `GameScene` when `ResultScene` loads, so nothing stays subscribed after the scene change.
- The host's `LocalClientId` is the server client ID (0), so a remote ID never matches it.

## 7. Acceptance criteria

1. The client stops Play Mode mid-match: the host's HUD keeps its values until the result screen appears, then shows "You won" + "Your opponent left the match." as before.
2. The host stops: the client's HUD clears and the client gets its normal disconnect flow, as before.
3. No new errors on either side.

## 8. Regression tests

- Roslyn compile. Exit 0.

## 9. Manual Unity Editor checks

1. Checklist A11.1–A11.5 (client stops Play Mode in each phase): the host never shows an empty HUD before the result screen.
2. Checklist A11.6 (end the clone's process): after the timeout, the same behaviour.
3. Checklist C1.2 / A8 (the host leaves): the client behaves as before.

## 10. Open questions and assumptions

- None.
