# TASK-N20: Make Send safe after the selected card is cleared, and fix the client target rule

- **Source issue:** [Issue Report #6 → N20](../Reports/2026-10-19_IssueReport-6.md#-medium-13) (first reported in #5, raised to 🟡 in #6)
- **Severity:** 🟡 Medium
- **Baseline:** `fe2d4fb`. Line numbers below are from this commit.
- **Order:** 1 of 13. Do this before M14 and M8, which change nearby code in `PlayerSession`.
- **Implementer:** Codex

## 1. Objective

Pressing **Send** in `PlayerAction` never throws. The client sends a card package only when:
- it belongs to the card that is selected right now
- every activity that needs a target has one

The client's "needs a target" rule is the same as the server's.

## 2. Current behavior

**Flow** (all client-side except the RPC):
1. `PlayerUI.OnCardClicked` (`UI/PlayerUI.cs:299-333`) → `SelectCard` → `PlayerSession.SetSelectedCard(card)`.
2. **Confirm** button → `ConfirmTargetButtonUI.ConfirmSelect` → `PlayerSession.TryConfirmTarget` (`InGame/PlayerSession.cs:774-927`). This fills `m_currentCardControllPackage`: the card ID and up to 4 targets.
3. **Send** button → `ConfirmSendButtonUI1.TryConfirmSend` (`UI/ConfirmSendButton.cs`) → `PlayerSession.TryHandlePackage` (`:1022-1033`) → `CheckPackage` (`:1034-1055`) → `CardInputServerRpc`, then the package is reset.

**Defect (a): `NullReferenceException` on Send.**
`PlayerSession.ClearSelectedCard` (`:736-739`) only sets `m_selectedCard = null`. The package keeps the card ID. `CheckPackage` then reads `m_selectedCard.m_cardData.m_activities` whenever `CardNetworkObjectId != 0` (`:1038`) and throws. Callers that clear the card while a package exists:
- **Deselect:** clicking the same card again after Confirm (`PlayerUI.cs:314-320`).
- **N18 pause:** someone dies during `PlayerAction`, so the phase becomes `RespawnWait`.
  - `PlayerUI.OnGamePhaseChanged` → `UpdateCards`.
  - `UpdateCards` has no case for `RespawnWait`, so no card views are created.
  - `ValidateSelectedCard` (`:437-445`) then calls `ClearSelectedCard`.
  - After the resume, Send throws.
- `PlayerUI.Update` calls it every frame while the local player is eliminated (`:133-138`), and so do `SelectionRejectedClientRpc` (`PlayerSession.cs:226`) and `ClearSession` (`:724`).

Result: an exception in the Console, and nothing is sent until the player selects the card and confirms the target again.

**Defect (b): the client target rule is inverted.**
`CheckPackage` rejects target 0 only for types that are **not** in {`AnyPlayer`, `AnyPlayerBlock`, `OpponentBlock`, `OpponentPlayer`, `RandomBlockSelectedPlayer`, `None`}. So:
- **Need a target, but aren't checked:** `OpponentBlock`, `AnyPlayerBlock`, `AnyPlayer`, `OpponentPlayer`. An untargeted package goes to the server, which rejects it ("Target 0 was not selected").
- **Don't need a target, but are rejected:** `AllBlockSelf`, `AllBlockOpponent`, `AllBlockAnyPlayer`, `AllPlayer`, `RandomSelfBlock`, `RandomOpponentBlock`. Cards with these types can never be sent.

**The server rule is authoritative** (`GameMaster.GameLoop.cs:265-286`). Target 0 is accepted only for:
- `None`, `AllPlayer`, `AllBlockAnyPlayer`, `AllBlockSelf`, `AllBlockOpponent`
- `RandomBlockSelectedPlayer`, `RandomOpponentBlock`, `RandomSelfBlock`

Any other type with target 0 makes the package invalid.

**Defect (c), minor:** when nothing was confirmed (`CardNetworkObjectId == 0`), `CheckPackage` returns `true` and an empty package is sent. The server logs "Card 0 was not found". `TryHandlePackage` also doesn't check `GameMaster.Instance` for null.

**Current cards:**
- Fireball: `OpponentBlock`
- Iceshard: `OpponentPlayer`
- Clarity: `DrawCard` on `SelfPlayer`
- HealingHooves: `Heal` on `SelfPlayer`

Every card has 4 activity slots (`CardData.cs:90`); unused slots are `None`/`None`. So defect (b) is latent for the `All*` and `Random*` types. For `OpponentBlock` it only costs a wasted round trip.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/PlayerSession.cs` | `ClearSelectedCard` also resets the package. Rewrite `CheckPackage`. Null guard in `TryHandlePackage`. |
| `Assets/Scripts/InGame/CardData.cs` | New static helper for the target rule |
| `Assets/Scripts/InGame/GameMaster/GameMaster.GameLoop.cs` | The server switch at `:265-286` uses the helper. **No change in behavior.** |

Read-only context:
- `UI/PlayerUI.cs`: callers `ClearSelectedCard` and `ValidateSelectedCard`
- `UI/ConfirmSendButton.cs`, `UI/ConfirmTargetButtonUI.cs`
- `InGame/CardControllPackage.cs`: `CreateEmpty` creates 4 zero targets

## 4. Required changes

1. **`CardData.cs`:** add a static class in the same file (global namespace, after `CardActivity`). Match the file's existing indentation.
   ```csharp
   public static class CardTargetRules
   {
       // Same rule as the server check in GameMaster.PlayerActionPhase: these types accept target 0.
       public static bool RequiresSelectedTarget(CardTargetType targetType)
       {
           switch (targetType)
           {
               case CardTargetType.None:
               case CardTargetType.AllPlayer:
               case CardTargetType.AllBlockAnyPlayer:
               case CardTargetType.AllBlockSelf:
               case CardTargetType.AllBlockOpponent:
               case CardTargetType.RandomBlockSelectedPlayer:
               case CardTargetType.RandomOpponentBlock:
               case CardTargetType.RandomSelfBlock:
                   return false;
               default:
                   return true;
           }
       }
   }
   ```
2. **`GameMaster.GameLoop.cs:265-286`:** replace the `switch` with the helper. Keep the behavior identical: an optional type with target 0 still skips `CheckActivity` (`continue`), and a required type with target 0 still logs the same warning and invalidates the package.
   ```csharp
   if (_targetNetworkObjectId == 0)
   {
       if (!CardTargetRules.RequiresSelectedTarget(_activity.s_targetType))
           continue;

       Debug.LogWarning(
           $"Target {i} was not selected for " +
           $"card {_currentCard.NetworkObjectId}.");

       _isMessageValid = false;
   }
   ```
   The existing `if (!_isMessageValid) break;` after it stays.
3. **`PlayerSession.ClearSelectedCard`:** also drop a package that belongs to the cleared card. Reset only when it isn't empty, so the per-frame call while eliminated doesn't allocate a new list each frame.
   ```csharp
   public void ClearSelectedCard()
   {
       m_selectedCard = null;

       if (m_currentCardControllPackage.CardNetworkObjectId != 0)
       {
           m_currentCardControllPackage = CardControllPackage.CreateEmpty(0);
       }
   }
   ```
   The `RespawnWait` branch of `TryConfirmTarget` uses `m_respawnBlockNetworkId`, not the package, so it isn't affected.
4. **`PlayerSession.CheckPackage`:** rewrite it in this order:
   1. `package.CardNetworkObjectId == 0` → `Debug.LogWarning("Send ignored: no card target was confirmed.")`, return `false`.
   2. `m_selectedCard == null` or `m_selectedCard.NetworkObjectId != package.CardNetworkObjectId` → reset `m_currentCardControllPackage` to `CreateEmpty(0)`, then `Debug.LogWarning("Send ignored: select the card and confirm the target again.")`, return `false`.
   3. `m_selectedCard.m_cardData == null` or `m_cardData.m_activities == null` → warning, return `false`.
   4. `package.TargetNetworkObjectId == null` or `Count < m_activities.Length` → warning, return `false`.
   5. For each activity `i`: if `CardTargetRules.RequiresSelectedTarget(type)` and `package.TargetNetworkObjectId[i] == 0` → return `false`.
   6. Return `true`.
5. **`PlayerSession.TryHandlePackage`:** return early if `GameMaster.Instance == null`, before `CanPlayerSelectCardTargets`. Everything else stays the same, including the package reset after a successful send.

## 5. Explicit non-goals

- Keeping the selected card and its confirmed target across a `RespawnWait` pause. After the resume, the player selects the card again (Q1).
- Restoring the selected card's highlight when `UpdateCards` rebuilds the views in `PlayerAction`.
- A UI message when Send is ignored.
- Changes to `TryConfirmTarget`, `AutoSelectTarget` or `NetworkCard.CheckActiveTaergetSelection` (M14 and M8 cover those), server validation, N21 or N26.
- Implementing the `All*` / `Random*` effects (feature gap).

## 6. Constraints and compatibility requirements

- No renames and no signature changes: `ClearSelectedCard`, `TryHandlePackage`, `TryConfirmTarget`, `CardInputServerRpc`.
- The `PlayerSession` changes are client-only. The server change only moves the rule into the helper; it accepts and rejects exactly the same packages as before.
- `CardTargetRules` is a new public static class. No serialized fields and no asset changes.
- Don't edit `NetworkCard.cs` in this task. It isn't UTF-8 (N24).

## 7. Acceptance criteria

- [ ] **Deselect:** Fireball → opponent block → Confirm → click Fireball again → Send. No exception and only a warning; the server receives no card package.
- [ ] **Pause:** Fireball → block → Confirm. A death starts `RespawnWait`; respawn and resume; Send. No exception, and nothing is sent. Selecting the card and confirming again, then Send, resolves normally.
- [ ] Send with no confirmed target sends nothing, and the server never logs "Card 0 was not found".
- [ ] Iceshard, Clarity and HealingHooves still resolve with select → Confirm → Send.
- [ ] The helper's "no target needed" set is exactly the 8 types of the old server switch.
- [ ] With target 0, an activity of type `AllBlockOpponent` or `RandomOpponentBlock` passes `CheckPackage`, and `OpponentBlock` doesn't.
- [ ] Compiles with no new warnings.

## 8. Regression tests

There's no test framework in the project. **Review checklist:**
- the helper's `false` cases are exactly the 8 types in the old server switch
- every caller of `PlayerSession.ClearSelectedCard` is fine with the package reset: `PlayerUI.ClearSelectedCard`, `SelectionRejectedClientRpc`, `ClearSession`
- `TryHandlePackage` still resets the package after sending

Optional: a plain C# harness, as in earlier tasks, that checks `RequiresSelectedTarget` for all 14 `CardTargetType` values.

## 9. Manual Unity Editor checks

ParrelSync host + client. Run each check once as the host and once as the client.
1. **Normal casts:** Fireball on an opponent block, Iceshard, HealingHooves, Clarity. All resolve as before.
2. **Deselect:** Fireball → block → Confirm → click Fireball again → Send. Console: a warning, no `NullReferenceException`. Then select it again → block → Confirm → Send → it resolves.
3. **Pause:**
   1. In player A's action turn: Fireball → block → Confirm.
   2. In the **host** Scene view, drag player B's pony off its platform into the lava.
   3. B picks a respawn block, and A's turn resumes.
   4. A presses Send: no exception, nothing sent.
   5. Select again → Confirm → Send: it resolves.
4. **Send without Confirm:** nothing happens. The host Console has no "Card 0 was not found".
5. **Optional, temporary asset change (revert it):** set Fireball's activity 0 target to `RandomOpponentBlock` (5). Select → Confirm → Send. The card is sent and goes to the graveyard; there's no damage, because the effect isn't implemented. Then run `git restore Assets/Card/Data/Fireball.asset`.

## 10. Open questions and assumptions

- **Q1:** Should a selected card and its confirmed target survive a `RespawnWait` pause? This spec clears them, which is safe. Keeping them would need `PlayerUI` to skip clearing during `RespawnWait`; that would be a separate task.
- **A1:** For `RandomBlockSelectedPlayer`, the server accepts target 0, but `CheckActiveTaergetSelection` asks the player to pick a target. This spec follows the server (target optional).
- **A2:** Cards that pick their own target still need Confirm before Send. That's unchanged.
