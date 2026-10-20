# TASK-L2 + N31: Use all three deck mixes; document the 84-card decks

- **Source issues:**
  - [Issue Report #7 → L2](../Reports/2026-10-20_IssueReport-7.md#-low-23)
  - [Manual-check review → N31](../Reviews/2026-10-20_ManualChecks-Report6-7_Review.md#3-new-findings) (new)
- **Severity:** 🟢 Low (both Confirmed)
- **Baseline:** `b7e7b91`. Line numbers below are from this commit.
- **Order:** 12 of 20. Independent.
- **Owner decision (2026-10-20):** use **all three mixes**, keep **whole sets of 4**, so decks stay **84 cards**. Fix the comments.
- **Implementer:** Codex

## 1. Objective

- `CreateDeck` picks each of its three section mixes (20/30/30, 30/20/30, 30/30/20) with equal chance.
- The code comments describe what really happens: sections are filled in whole sets, so a section can go up to 3 cards over its target, and decks have 84 cards.

## 2. Current behavior

- `InGame/GameMaster/GameMaster.Card.cs:268-331` `CreateDeck(CardRarity maxRarity)`:
  - `int _randomSeed = Random.Range(0, 2);` (`:271`). The int overload's upper bound is exclusive, so it returns 0 or 1. `case 2` (Attack 30, Defence 30, Draw 20; `:291-295`) is never used.
  - It then calls `FillCardSection` once per section (`:327-329`).
- `FillCardSection` (`:340-400`, comment `:333-339`):
  - adds cards in **whole sets**: 4 copies for Common/Uncommon/Rare, 2 for UltraRare, 1 for Unique
  - first one set of each card, in random order (`:346-377`), then random repeat sets of the non-unique cards (`:378-400`)
  - continues while the count is below the maximum, so it overshoots by up to 3
  - its comment says it ensures "the maximum number of cards for that type is not exceeded", which isn't true
- **Today's database** (`Assets/Card/CardDataBase.asset`), all `Common` (rarity 0):
  - Attack: Fireball, Iceshard
  - Defence: HealingHooves
  - Draw: Clarity
- **Resulting decks:**
  - mix 0: 20 + 32 + 32 = **84**
  - mix 1: 32 + 20 + 32 = **84**
  - mix 2 (unreachable today): 32 + 32 + 20 = **84**
  - seen in the owner's session: `Deck refilled: 84 cards.` (`Editor.log:37044`)
- `RefillDeck` (`:237-264`) uses the same `CreateDeck` with `m_refillDeckMaxRarity` (H2).

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/GameMaster/GameMaster.Card.cs` | `Random.Range(0, 3)`; comments of `CreateDeck` and `FillCardSection` |

## 4. Required changes

1. `:271` → `int _randomSeed = Random.Range(0, 3);`. Keep the `switch` and its three cases unchanged.
2. Rewrite the `CreateDeck` `<summary>` (`:265-267`) to say that it:
   - picks one of three section mixes (Attack/Defence/Draw = 20/30/30, 30/20/30 or 30/30/20) at random
   - fills each section, filtered by `maxRarity`
   - shuffles the deck
   - "With whole sets of 4, a 30-card section becomes 32, so decks have 84 cards."
3. Rewrite the `FillCardSection` `<summary>` (`:333-339`): it adds whole sets (4 Common/Uncommon/Rare, 2 UltraRare, 1 Unique), one set of each card first, then random repeat sets, **until the target is reached or passed**. A section can therefore exceed `maxCardType` by up to one set minus one card. Keep the `<param>` lines.
4. No other code changes.

## 5. Explicit non-goals

- Don't trim sections to exactly 20/30: the owner chose to keep sets of 4.
- No change to the rarity copy counts, the shuffle or the refill rules (H2).
- No change to the card database.

## 6. Constraints and compatibility requirements

- Server-only; no network changes.
- `UnityEngine.Random.Range(int, int)` has an exclusive upper bound. That's why the fix is 3.

## 7. Acceptance criteria

1. Across many `CreateDeck` calls, each of the three mixes occurs (roughly a third each).
2. Every deck built from today's database has 84 cards, with sections of 20/32/32 in some order.
3. The two summaries match the behaviour described in §4.

## 8. Regression tests

- Harness: call `CreateDeck` 300 times with a card-database double (2 attack, 1 defence, 1 draw card, all Common) and record the section counts. All three mixes appear, and each deck has 84 cards.
- Roslyn compile. Exit 0.

## 9. Manual Unity Editor checks

1. With `m_debugMode` on in `GameMasterPrefab`, play a few matches. Card types vary between matches; some matches have fewer Clarity cards (the new 30/30/20 mix).
2. Deck refill (checklist B1, temporary T1 and T2): `Deck refilled: 84 cards.` still appears. Revert T1 and T2.

## 10. Open questions and assumptions

- None. The owner decided on 2026-10-20.
