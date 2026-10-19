# TASK-M15: Hand limit of 10 cards; when over the limit, discard the oldest

- **Source issue:** [Issue Report #6 → M15](../Reports/2026-10-19_IssueReport-6.md#-medium-13) (first reported in #1)
- **Severity:** 🟡 Medium (Confirmed, design question)
- **Baseline:** `fe2d4fb`. Line numbers below are from this commit.
- **Order:** 13 of 13. Independent of the other tasks. If N20 or M14 are already merged, keep their changes in `PlayerSession` / `PlayerUI`.
- **Owner decision (2026-10-19):** the limit is **10 cards for now**. When a draw takes the hand over the limit, **the oldest cards are discarded**.
- **Implementer:** Codex

## 1. Objective

A player never holds more than 10 uncommitted cards. Every draw still happens. If a draw takes the hand over the limit, the server moves the oldest uncommitted hand card(s) to the graveyard, and the player sees a short message.

## 2. Current behavior

- `GameMaster.DrawCardPhase` (`Assets/Scripts/InGame/GameMaster/GameMaster.GameLoop.cs:552-589`) gives each player `m_startHandCards` cards every round (`GameMaster.cs:80`, default 2; despite the name, it's the per-round draw count). Clarity draws 2 more during an action turn (`GameMaster.Processing.cs:230-235`).
- All draws go through `GameMaster.DrawCard` (`GameMaster.Card.cs:149-186`). It adds the card to the **end** of `session.m_handCards` (`PlayerSession.AddCard`, `PlayerSession.cs:336-343`).
- There's no hand limit anywhere.
- **Order = age:**
  - `m_handCards` keeps draw order, oldest first. The only removal (`RemoveNetworkObjectId` in `MoveCardOnGraveyard`) keeps the order of the rest.
  - `PlayerUI.UpdateCards` builds views in list order, so **the oldest card is the leftmost** in the hand.
- **Committed cards:**
  - a card confirmed for play or discard in `CardSelection` stays in `m_handCards`, and is also listed in `m_boardPlayCards` / `m_boardDiscardCards`
  - all of them leave the hand only through `MoveCardOnGraveyard` (`GameMaster.Card.cs:39-78`) at the end of the action phase
  - during Clarity's resolution, the Clarity card and any other cards waiting to be played are committed in this way
- **Graveyard:** `MoveCardOnGraveyard` removes the card from all three lists, sets `CardLocation.Graveyard` and adds it to `m_graveyard`. Since H2, `RefillDeck` despawns graveyard cards when the deck runs out.
- **Client selection (N26 interaction):**
  - an unconfirmed selection (`PlayerSession.m_selectedCardsToPlay` / `m_selectedCardsToDiscard`) carries over to the next round (N26, still open)
  - if one of those cards is discarded by the new limit, the next confirm sends its ID
  - the server rejects that ID and records `[Suspicious] InvalidCardSelection` (`GameMaster.PackageManaging.cs:348-353`), which falsely flags an honest player
- **Existing message pattern:**
  - `PlayerUI.ShowManaNotEnough` (`UI/PlayerUI.cs:200-207`) shows `m_manaNotEnoughText` in `m_messageText` for `m_messageDuration` s
  - `OnSelectedLocaleChanged` (`:193-198`) always re-shows the mana text
  - the server reaches the owner with an owner-targeted client RPC, like `PlayerSession.SelectionRejectedClientRpc` (`PlayerSession.cs:219-232`)

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/GameMaster.cs` | New rule field `m_maxHandCards` |
| `Assets/Scripts/InGame/GameMaster/GameMaster.Card.cs` | Hand count helper; discard-oldest step after a successful draw |
| `Assets/Scripts/InGame/PlayerSession.cs` | `HandLimitDiscardClientRpc`; `PruneLocalCardSelection()`; prune before a selection confirm |
| `Assets/Scripts/UI/PlayerUI.cs` | `ShowHandLimitDiscard()`; prune on hand rebuild; the message survives a locale change for either text |
| `Assets/Prefab/UiPrefab/PlayerUIPrefab.prefab` | Assign `m_handLimitDiscardText` → `UI_table/hand_limit_discard` |
| `Assets/Localization/UI_table*` | 1 new key in en/ru/ja |

## 4. Required changes

1. **`GameMaster.cs`, `[Header("GameRules")]`:** add `[SerializeField] private int m_maxHandCards = 10;` with the comment `// 0 or less = no limit.`
2. **`GameMaster.Card.cs`:** add two private helpers.
   - `private bool IsCardCommitted(PlayerSession session, ulong cardId)`: true if the ID is in `m_boardPlayCards` or `m_boardDiscardCards`. Use the existing `ContainsNetworkObjectId`.
   - `private void DiscardOldestOverHandLimit(PlayerSession session)`:
     1. Return if `m_maxHandCards <= 0`.
     2. Copy `session.m_handCards` to a local `List<ulong>`, oldest first, and keep only the IDs that aren't committed.
     3. `int _excess = _uncommitted.Count - m_maxHandCards;` Return if it's `<= 0`.
     4. For the first `_excess` IDs of that list:
        - get the card with `GetCardByID`
        - if it's null, remove the stale ID from `session.m_handCards` with `RemoveNetworkObjectId`
        - otherwise call `MoveCardOnGraveyard(card)`
        - count the discards
     5. If at least one card was discarded:
        - `if (m_debugMode) Debug.Log($"Hand limit {m_maxHandCards}: discarded {_count} oldest card(s). Client {session.OwnerClientId}");`
        - call `session.HandLimitDiscardClientRpc(...)`, targeted at `session.OwnerClientId` (same `ClientRpcParams` shape as the `SelectionRejectedClientRpc` call)
3. **`DrawCard`:** after a **successful** draw (after `m_deck.Remove(card);`), call `DiscardOldestOverHandLimit(session);`.
   - Don't call it when no card was drawn (the deck is empty even after a refill), so a failed draw never costs a card.
   - The newly drawn card is the newest, so it's never the one discarded.
   - This covers the round draw and Clarity. In Clarity's case, the Clarity card and the other cards waiting to be played are committed, so they're never discarded.
4. **`PlayerSession`:**
   1. `[ClientRpc] public void HandLimitDiscardClientRpc(ClientRpcParams clientRpcParams = default)`. If `!IsOwner`, return; otherwise call `PlayerUI.Instance.ShowHandLimitDiscard()` when the instance exists.
   2. `public void PruneLocalCardSelection()`: remove from `m_selectedCardsToPlay` and `m_selectedCardsToDiscard` every entry that is null, or whose `NetworkObjectId` is no longer in `m_handCards`. Committed cards are still in `m_handCards`, so they're kept.
   3. `ConfirmTurn`, normal (non-`RespawnWait`) path: call `PruneLocalCardSelection()` before `ConvertCardsToNetworkId`.
5. **`PlayerUI`:**
   1. In `UpdateCards`, after the `m_playerSession == null` check, call `m_playerSession.PruneLocalCardSelection();`. This keeps the highlights and the client mana gate (`SelectedPlayCost`) correct after a discard.
   2. Add `[SerializeField] private LocalizedString m_handLimitDiscardText;` next to `m_manaNotEnoughText`.
   3. Add a private `LocalizedString m_activeMessage`.
      - Refactor `ShowManaNotEnough` into a private `ShowMessage(LocalizedString message)` that sets the text, stores `m_activeMessage` and sets the hide time.
      - `ShowManaNotEnough()` and the new public `ShowHandLimitDiscard()` call it.
      - `OnSelectedLocaleChanged` re-shows `m_activeMessage` instead of always `m_manaNotEnoughText`.
      - Clear `m_activeMessage` where the message is hidden (`Update`, `ClearLocalUI`).
6. **`PlayerUIPrefab`:** set `m_handLimitDiscardText` to table `UI_table`, entry `hand_limit_discard`, the same way `m_manaNotEnoughText` is set (`PlayerUIPrefab.prefab:1261-1267`, which references the key by name). Only this field may change in the prefab.
7. **Localization (`UI_table` shared data + en/ru/ja):**

   | Key | en | ru | ja |
   |---|---|---|---|
   | `hand_limit_discard` | Hand limit reached: your oldest card was discarded. | Лимит руки: самая старая карта сброшена. | 手札の上限に達したため、最も古いカードを捨てました。 |

## 5. Explicit non-goals

- Letting the player choose which card to discard.
- A hand-count UI or an animation for the discard.
- Fixing N26 itself. The carry-over of still-valid cards stays as it is; this task only drops cards that left the hand.
- Renaming `m_startHandCards`. It's serialized; report only.
- Changing the draw timing.
- L9 (the `GameMasterPrefab` rule fields aren't serialized, so C# defaults apply; the new field follows the same rule).

## 6. Constraints and compatibility requirements

- The limit and the discard are server-authoritative. The client only prunes its own local selection and shows the message.
- Reuse `MoveCardOnGraveyard` for the discard. Don't add another way to remove hand cards.
- New serialized fields are additive. Existing names don't change.
- The prefab edit touches only `m_handLimitDiscardText`.

## 7. Acceptance criteria

- [ ] After any draw, a player holds at most 10 uncommitted cards.
- [ ] The discarded card is always the oldest uncommitted one (the leftmost in the hand), never the card just drawn, and never a committed card.
- [ ] Discarded cards are in the graveyard (`CardLocation.Graveyard`) and are cleaned up by the next deck refill.
- [ ] The owner sees the localized message once per discard event. The opponent sees nothing.
- [ ] A failed draw (empty deck even after a refill) discards nothing.
- [ ] A selection carried over from the previous round that contained a discarded card doesn't produce `[Suspicious] InvalidCardSelection` on the next confirm.
- [ ] `m_maxHandCards = 0` behaves exactly as before.
- [ ] The mana message still works, and either message switches language with the locale.
- [ ] Compiles with no new warnings.

## 8. Regression tests

No automated tests. **Review checklist:**
- `DiscardOldestOverHandLimit` runs only after a successful draw
- it skips committed cards
- it iterates over a copy, not the `NetworkList` it changes
- the RPC is owner-targeted
- `PruneLocalCardSelection` keeps committed cards

## 9. Manual Unity Editor checks

**Temporary Inspector change (revert it):** select `Assets/Prefab/GameMasterPrefab.prefab` and set `m_maxHandCards = 3`.
1. **Basic:** ParrelSync host + client. Round 1: 2 cards. Play and discard nothing; let the selection timer run out. Round 2:
   - before the draws, note the leftmost card
   - the first draw makes 3 cards
   - the second draw makes 4, then the leftmost card disappears and the message appears
   - the hand stays at 3
2. **Commit protection:** in round 3's selection, confirm one card to play. If it's at the limit, Clarity in the action turn draws 2 and discards the oldest **uncommitted** cards. The cards waiting on the board stay and still resolve.
3. **Carried-over selection:** in a selection phase, select (don't confirm) the leftmost card and let the timer run out. In the next round it gets discarded by the limit. Then select and confirm other cards. The host Console has **no** `[Suspicious] InvalidCardSelection` line.
4. **Locale:** switch the locale to ru and ja while the message is visible. It changes language.
5. Run `git restore Assets/Prefab/GameMasterPrefab.prefab`, then check `git status`.

## 10. Open questions and assumptions

- **A1:** "Oldest" means drawn earliest: the order of `m_handCards`, which is also the left-to-right order in the hand UI.
- **A2:** Cards committed in this round's selection don't count toward the limit and are never discarded, because they leave the hand this round anyway.
- **A3:** The limit is 10 "for now" (owner). It's a serialized field, so it can be tuned without code changes.
- **Note:** N26 (a carried-over unconfirmed selection) still needs its own owner decision. This task only stops it from flagging honest players after a discard.
