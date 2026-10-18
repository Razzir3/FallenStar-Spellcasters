# TASK-C4: Validate card-selection confirmation before writing to the board

- **Source issue:** [Issue Report C4](../Reports/2026-10-18_IssueReport.md#-critical)
- **Severity:** 🔴 Critical
- **Recommended order:** C5 → C6 → C1 → C2 → C3 → **C4**. It's independent of C1–C3 code-wise and can be done in parallel by a separate branch.
- **Implementer:** Codex

## 1. Objective

During `CardSelection`, a player may put only cards from **their own hand** onto their board. Each card may be used at most once across play and discard, and only on the first confirmation of the phase. The cards placed on the board must be exactly the cards used to calculate turn order.

## 2. Current behavior

Flow:
- `PlayerUI.ConfirmTurn` → `PlayerSession.ConfirmTurn` → `ConfirmTurnServerRpc(play[], discard[], respawnBlock)` (`PlayerSession.cs:138`)
- → `GameMaster.HandleConfirmTurn` (`GameMaster.PackageManaging.cs:166`), which checks that the sender owns the session
- → `case GamePhase.CardSelection` (`:186-237`)

In that branch:
1. `HandleConfirmSelectionTurn(...)` (`:323`):
   - If the selection is already confirmed, it returns early.
   - Otherwise it fills `PlayerTurnSelection.m_cardsToPlay/m_cardToDiscard` with cards that pass `ValidateCardOwner` (`:440`, a **team** comparison through `GetSessionByID(card.m_ownerId)`).
   - It sets `m_isConfirmed = true`.
   - `PlayerTurnSelection.TotalCost()` → `BuildTurnOrderByCardCost` uses these lists.
2. **Then, no matter what step 1 decided,** it runs `m_boardPlayCards.Clear()`, `m_boardDiscardCards.Clear()`, and for every **raw** ID in `play[]` / `discard[]` it:
   - checks the card exists and `m_cardData` is non-null
   - calls `SetLocation(Board)` and sets `m_isRotated`
   - calls `SendCardDataClientRpc` (all clients)
   - adds the ID to the board list

Consequences:
- Cards owned by the opponent (in their hand or on their board) can be placed on your own board and then cast. Casting is later rejected in `PlayerActionPhase`, because `m_ownerId != player`, but the card is shown on your board and **revealed to everyone**.
- Duplicates: the same ID twice, or in both lists.
- After confirming once, a player can resend different IDs and the board is rewritten, while the turn-order cost stays from the first confirm.
- An ID of an unowned deck card (`m_ownerId == 0`) makes `ValidateCardOwner` dereference a null session, so a `NullReferenceException` is thrown inside the RPC.

## 3. Relevant files and dependencies

| File | Member | Role |
|---|---|---|
| `Assets/Scripts/InGame/GameMaster/GameMaster.PackageManaging.cs` | `HandleConfirmTurn`, `HandleConfirmSelectionTurn`, `ValidateCardOwner` | The place to change |
| `Assets/Scripts/InGame/GameMaster/GameMaster.Card.cs` | `GetCardByID`, `MoveCardOnGraveyard`, `ClearPlayedCards`, `ClearDiscardedCards` | Card lookup and later cleanup |
| `Assets/Scripts/InGame/GameMaster.cs` | `ContainsNetworkObjectId(NetworkList<ulong>, ulong)`, `m_turnSelections` | Helper, selections |
| `Assets/Scripts/InGame/PlayerTurnSelection.cs` | `m_cardsToPlay`, `m_cardToDiscard`, `m_isConfirmed`, `TotalCost` | Turn-order input |
| `Assets/Scripts/InGame/PlayerSession.cs` | `m_handCards`, `m_boardPlayCards`, `m_boardDiscardCards`, `ConfirmTurn` | Server-written lists, client sender |
| `Assets/Scripts/InGame/NetworkCard.cs` | `m_ownerId`, `SetLocation`, `m_isRotated` | Card state |
| Downstream readers | `PlayerUI.UpdateCards`, `CardWorldViewManager.RefreshSession`, `GameMaster.PlayerActionPhase`, `HandleCardControlPackage` | Must keep working |

## 4. Required changes

1. **Reject repeat or invalid confirms before touching the board.** In the `CardSelection` branch:
   - If there's no `PlayerTurnSelection` for the session, or it's already `m_isConfirmed`, **return** without modifying `m_boardPlayCards`/`m_boardDiscardCards`.
   - Change `HandleConfirmSelectionTurn` to report its result (for example, return `bool`; the method is private, so the signature change is allowed), and use that instead of duplicating the check.
2. **One validation pass** (a new private helper in `GameMaster.PackageManaging.cs` is fine), applied to `play[]` first and then `discard[]`, sharing one `HashSet<ulong>` of used IDs. A card ID is accepted only if **all** of these hold:
   - the ID isn't 0 and isn't already in the used set (no duplicates within or across lists)
   - `ContainsNetworkObjectId(session.m_handCards, id)` is true
   - `GetCardByID(id) != null`
   - `card.m_ownerId.Value == session.NetworkObjectId`
   - `card.m_cardData != null`

   Treat a `null` array as empty.
3. Fill `turnSelections.m_cardsToPlay` / `m_cardToDiscard` **only** with the accepted cards, and then set `m_isConfirmed = true`. An empty selection is a valid confirmation.
4. Write the board lists **from the accepted cards only**, keeping the existing side effects in the same order: `SetLocation(CardLocation.Board)`, set `m_isRotated` (false for play, true for discard), `SendCardDataClientRpc(...)`, `Add` to the list.
5. Make `ValidateCardOwner` null-safe (return false when the owner session isn't found), or stop calling it if step 2 makes it redundant. Don't delete the method.
6. Log rejected IDs as warnings in the existing style (`Debug.LogWarning(...)`), one line per request, not per frame.

## 5. Explicit non-goals

- **Don't** remove selected cards from `m_handCards` at confirm time. Today they stay in the hand until `MoveCardOnGraveyard` at the end of the player's action phase, and `PlayerUI` behavior depends on that. See Q1.
- Don't change the `PlayerAction` / `RespawnWait` branches of `HandleConfirmTurn`.
- Don't change the client selection UX (`PlayerSession.SelectCardToPlay/Discard`, `PlayerUI`).
- No hand-size limits or cost limits (game design).
- Don't change `BuildTurnOrderByCardCost`.

## 6. Constraints and compatibility

- No renames (`AGENTS.md`). Private signatures may change. Public ones (`HandleConfirmTurn`, `ConfirmTurnServerRpc`) must not.
- The RPC payload (`ulong[]`, `ulong[]`, `ulong`) stays the same.
- The network-visible end state for an honest player must be identical to today:
  - same board list contents and order
  - same `m_isRotated` values
  - same `SendCardDataClientRpc` calls
- Server-only code. Keep the `if (!IsServer) return;` guard.

## 7. Acceptance criteria

- [ ] An honest player's selection (any mix of play and discard from their own hand) produces the same board, rotation, card reveal and turn order as before.
- [ ] IDs not in the sender's hand (opponent's cards, deck cards, graveyard cards, non-card objects, 0) are ignored, and no exception is thrown.
- [ ] A card listed twice, or in both play and discard, is used once (first occurrence, play list first).
- [ ] A second confirm in the same `CardSelection` phase changes nothing: board lists, selection lists and cost all stay as they were.
- [ ] `PlayerTurnSelection` lists and the board lists contain exactly the same cards.
- [ ] Compiles without new warnings.

## 8. Regression tests

There's no automated test infrastructure. If Codex wants a unit test, the validation rule can be extracted into a pure static function over `IEnumerable<ulong>`/sets (an EditMode test, no NGO objects). That's optional.

Code review checklist:
- Early return happens before any `Clear()`
- The validation is shared by both lists
- The board is written from validated data only
- The `ValidateCardOwner` null-safety is in place

## 9. Manual Unity Editor checks

1. ParrelSync, host and client. Start a match and reach `CardSelection`.
2. Honest play: each player selects 1 card to play (LMB) and 1 to discard (RMB), then confirms. Check:
   - the board shows the cards
   - the discard card is rotated
   - turn order follows total cost
   - the cast still works in `PlayerAction`
3. Empty confirm: confirm with nothing selected. The phase still ends when both have confirmed.
4. **Tamper checks (temporary client edits, don't commit):**
   - a. Send the opponent's hand card ID → not placed on your board, no exception.
   - b. Send the same ID in play and discard → appears once, as a play card.
   - c. Call `ConfirmTurn()` twice with different selections → the second call has no effect.
   - d. Send an ID of a card still in the deck → ignored, no exception in the host Console.
5. Play a full round to make sure `ClearPlayedCards/ClearDiscardedCards` still empty the board at the end of each action phase.

## 10. Open questions and assumptions

- **Q1 (owner decision):** should confirmed cards leave the hand immediately? This is currently out of scope, to preserve the UI behavior.
- **Q2:** if a client sends invalid IDs, should the whole confirmation be rejected, or should only the invalid IDs be filtered out? This spec assumes **filter** (more forgiving to network races).
- **A1:** `m_ownerId` holds the owner's **PlayerSession NetworkObjectId** (set in `GameMaster.DrawCard` → `card.SetOwner(session.NetworkObjectId)`). Verified in code.
