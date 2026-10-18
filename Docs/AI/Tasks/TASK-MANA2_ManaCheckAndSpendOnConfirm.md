# TASK-MANA2: Check and spend mana when the card selection is confirmed

- **Source:** [RESEARCH-ManaSystem](../Research/RESEARCH-ManaSystem.md) (steps S4–S5)
- **Owner decisions (2026-10-18):**
  - **D2:** mana is spent **at selection confirm**, for the cards chosen to **play**.
  - **D3:** discards are **free**.
  - **D4:** turn order is **unchanged**: the sum of play and discard costs.
  - **D8:** if the server receives an **over-budget** confirm, it **rejects it**, **force-clears the selection on that client**, and **resets the card-selection timer to its default** for this phase. The attempt is recorded as suspicious (TASK-SEC1).
  - **Timer-reset limits:** at most **once per player per selection phase**, and at most **3 times per player per match**.
  - **Phase end:** `CardSelection` ends **only** when **both** players have sent a **valid** (accepted) confirm, or when the timer runs out. A rejected confirm doesn't count as confirmed.
- **Depends on:** TASK-MANA1 (mana values, `AddMana`) and TASK-SEC1 (`ReportSuspicious`)
- **Implementer:** Codex

## 1. Objective

A player can't play cards whose total cost is higher than their current mana:
- The **client** refuses to select a play card that wouldn't fit.
- The **server** refuses a confirm that doesn't fit, makes the client clear its selection, and gives the full selection time again.

When a confirm is accepted, the play cost is subtracted from mana.

## 2. Current behavior

- **Client selection:** `PlayerSession.SelectCardToPlay` / `SelectCardToDiscard` (`PlayerSession.cs:169-205`) only check `CanPlayerSelectCards`. `PlayerUI.SelectCardToPlay` (`PlayerUI.cs:~295`) colors the card if the call returns `true`, and clears the color if it returns `false`.
- **Client confirm:** `PlayerSession.ConfirmTurn` (`:105-135`) sends `ConfirmTurnServerRpc` and immediately calls `ClearLocalCardSelection()`.
- **Server confirm:** `HandleConfirmTurn` → `HandleConfirmSelectionTurn` (`PackageManaging.cs:306-344`):
  - returns early when the selection is missing or already confirmed
  - validates the IDs (`ValidateSelectionCards`, C4)
  - sets `m_isConfirmed = true`
  - the caller then writes the board (`:189-218`)
- **Timer:** `CardSelectionPhase` (`GameLoop.cs:~472-503`) runs `m_timer.Value` from 0 up to `m_cardSelectionTime` (60 s). It ends early when everyone has confirmed. The timer is **shared by both players**.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `GameMaster.PackageManaging.cs` | Mana check and spend in `HandleConfirmSelectionTurn` |
| `GameMaster.GameLoop.cs` | Timer-reset support in `CardSelectionPhase` |
| `GameMaster.cs` | Reset counter field / config |
| `PlayerSession.cs` | Client gating in `SelectCardToPlay`; `SelectionRejectedClientRpc` |
| `UI/PlayerUI.cs` | Clear selection highlights when forced |
| MANA1 | `m_manaCurrent`, `AddMana` |
| SEC1 | `ReportSuspicious(ManaOverBudget, Certain, …)` |

## 4. Required changes

### 4.1 Server: check, spend, reject

1. In `HandleConfirmSelectionTurn`, **after** both `ValidateSelectionCards` calls and **before** `m_isConfirmed = true`:
   - `int _playCost = sum of m_cardData.m_cost over turnSelections.m_cardsToPlay`. Discards are free (D3).
   - **If `_playCost > session.m_manaCurrent.Value`:**
     1. `turnSelections.m_cardsToPlay.Clear(); turnSelections.m_cardToDiscard.Clear();` and leave `m_isConfirmed = false`.
     2. `session.ReportSuspicious(SuspiciousActionType.ManaOverBudget, $"cost {_playCost} > mana {mana}")` (SEC1).
     3. `session.SelectionRejectedClientRpc(...)`, targeted at `session.OwnerClientId` (§4.3).
     4. `ResetCardSelectionTimer(session)` (§4.2).
     5. `return false`. The existing caller then doesn't touch the board.
   - **Otherwise:** `AddMana(session, -_playCost)` (MANA1), then continue as today (`m_isConfirmed = true`, board written by the caller).
2. Keep `TotalCost()` and `BuildTurnOrderByCardCost` unchanged (D4).

### 4.2 Timer reset ("reset time to select cards to default for this stage")

3. Add `private void ResetCardSelectionTimer(PlayerSession session)` in `GameMaster.GameLoop.cs`:
   - Only if `m_gamePhase.Value == GamePhase.CardSelection`. Then set `m_timer.Value = 0f`, which restarts the full `m_cardSelectionTime` for **both** players. The timer is shared.
   - **Abuse guard (owner decision):** reset only while **both** limits allow it:
     - **per phase:** `Dictionary<PlayerSession, int> m_selectionTimerResetsThisPhase`, cleared at the start of every `CardSelectionPhase`, limit `[SerializeField] private int m_maxSelectionTimerResetsPerPhase = 1`
     - **per match:** `Dictionary<PlayerSession, int> m_selectionTimerResetsThisMatch`, cleared in `StartNewGame`, limit `[SerializeField] private int m_maxSelectionTimerResetsPerMatch = 3`

     Increase both counters on every reset. When either limit is reached, the confirm is **still rejected and reported**, but the timer isn't reset. Without this guard a modified client could stall the match forever.
4. **Phase end rule (owner decision):** `CardSelectionPhase` keeps its current loop: it ends when `AllPlayerConfirmedSelection()` is true or `m_timer.Value >= m_cardSelectionTime`. Because a rejected confirm leaves `m_isConfirmed = false`, the phase **continues** until both players have a **valid** confirm or the time runs out. **Don't** end the phase on a rejected confirm.
   - On a timeout, a player without a valid confirm plays nothing. Their selection stays empty, no mana is spent, the board is empty, and their turn-order cost is 0. That is today's behaviour, unchanged.

### 4.3 Client: forced clear and gating

5. Add `[ClientRpc] public void SelectionRejectedClientRpc(ClientRpcParams clientRpcParams = default)` on `PlayerSession`:
   - Return if `!IsOwner`.
   - `ClearLocalCardSelection();` and `ClearSelectedCard();`
   - Tell the UI to clear all selection highlights: a new `PlayerUI.Instance.OnSelectionRejected()` that calls `ClearSelectedColor()` on every hand card view, or rebuilds them with the existing `UpdateCards`. Null-check `PlayerUI.Instance`.
   - The localized "Not enough mana" message is TASK-MANA3. Here, just leave a `//Todo MANA3` hook.
6. **Client gating** in `PlayerSession.SelectCardToPlay` (after the existing checks, before `Add`):
   - If `card.m_cardData == null` → return `false` (the cost is unknown yet; M14).
   - If `SelectedPlayCost() + card.m_cardData.m_cost > m_manaCurrent.Value` → return `false`. `SelectedPlayCost()` is a new private sum over `m_selectedCardsToPlay`, skipping null data.
   - `SelectCardToDiscard`: **no mana check** (D3).
   - The existing `PlayerUI` handling of `false` (clearing the color) needs no change.

## 5. Explicit non-goals

- The mana display and the "not enough mana" text (MANA3).
- Changing turn order (D4), the cast path, or `HandleCardControlPackage`. Unpaid cards can never reach the board.
- Per-player selection timers (the timer stays shared).
- Kicking cheaters (SEC1 is tracking only).

## 6. Constraints and compatibility

- No renames. The private `HandleConfirmSelectionTurn` keeps its `bool` contract from C4.
- **Behaviour change to C4, intended:** "the first confirmation is final" becomes "the first **valid** confirmation is final". A rejected over-budget confirm leaves the player unconfirmed and able to confirm again.
- An honest client (with the gating in §4.3, step 6) never triggers a rejection, a reset or a suspicion record.
- Server-only state changes. The only new client-side code is the RPC handler and the gating.

## 7. Acceptance criteria

- [ ] Round 1 (mana 2): Iceshard (2) can be selected; selecting another play card is refused locally. After confirm, mana = 0.
- [ ] Discards are always selectable and cost nothing.
- [ ] Mana is subtracted exactly once per accepted confirm. A second confirm is ignored, as today.
- [ ] **Tamper** (client gating temporarily disabled): an over-budget confirm causes all of the following:
  - no board change and no mana change
  - the client's selection and highlights are cleared
  - `m_timer` goes back to 0 (once per player per phase)
  - one `ManaOverBudget` record
  - a valid re-confirm afterwards works
- [ ] The second over-budget confirm from the same player in the same phase doesn't reset the timer again, but is still rejected and recorded.
- [ ] A player's 4th reset in the same match (over 4 phases) doesn't happen (per-match limit of 3), but the confirm is still rejected and recorded.
- [ ] `CardSelection` doesn't end after a rejected confirm. It ends only when both players have valid confirms, or on timeout.
- [ ] On timeout, a player without a valid confirm plays nothing and spends no mana.
- [ ] Turn order is unchanged (play + discard costs).
- [ ] Compiles without new warnings.

## 8. Regression tests

Optional EditMode test: the budget rule (a list of costs plus mana → allowed?) as a pure function.

Review checklist:
- the check happens **after** C4 validation and **before** `m_isConfirmed = true`
- the reject path returns before any board write
- the guard dictionary is cleared each phase

## 9. Manual Unity Editor checks

1. ParrelSync host and client. Round 1: try to select Fireball (3) as a play card with 2 mana. It doesn't highlight. Iceshard does. Confirm, and check mana is 0 (Inspector or log).
2. Round 2 (capacity 4): select Iceshard + another 2-cost card, or Clarity (3). A 4-cost HealingHooves alone works; HealingHooves + Iceshard is refused.
3. Discard 3 cards in one round: allowed, and mana is unchanged.
4. **Tamper (temporary, don't commit):** remove the gating in `SelectCardToPlay`, select an over-budget set and confirm. Check:
   - the host Console shows the `[Suspicious] … ManaOverBudget` warning
   - the client highlights are cleared
   - the timer jumps back to 0
   - confirming again with a valid set works

   Repeat the tamper a second time in the same phase: rejected, no timer reset. Then tamper once in each of the next 3 rounds: the timer resets in rounds 2 and 3 (3 resets in total) but **not** in round 4. In every case the phase only ends after both valid confirms or on timeout. Revert.
5. Play a full match to make sure honest play shows no `[Suspicious]` warnings.

## 10. Open questions and assumptions

- **Q1 (resolved, owner 2026-10-18):** at most 1 reset per player per phase and 3 per player per match. Both limits are configurable on `GameMaster`.
- **Q2 (resolved, owner 2026-10-18):** keep C4 as it is. Invalid card IDs are **silently filtered**, and the rest of the confirm is accepted. The attempt is **only tracked** server-side (SEC1 `InvalidCardSelection`). The player gets no sign that it was detected. The over-budget case (D8) is the only one with client-visible handling.
- **A1:** resetting the shared timer gives the **opponent** extra time too. That's accepted, because the timer is shared.
- **A2:** the server doesn't need to re-check mana when a card is cast. `HandleCardControlPackage` only accepts cards on `m_boardPlayCards`, which only holds paid cards.
