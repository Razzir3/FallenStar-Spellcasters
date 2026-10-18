# TASK-H9: The UltraRare deck fill must not write into the refill pool

- **Source issue:** [Issue Report #2 → H9](../Reports/2026-10-18_IssueReport-2.md)
- **Severity:** 🟠 High (latent: no UltraRare card exists yet)
- **Chosen solution:** **H9-B.** UltraRare cards aren't added to the refill pool, so there are at most 2 copies from the unique pass.
- **Order:** independent. It can be done first.
- **Implementer:** Codex

## 1. Objective

`GameMaster.FillCardSection` must not throw for UltraRare cards, and must not silently corrupt the refill pool. An UltraRare card enters the deck exactly 2 times and is never picked again in the refill pass.

## 2. Current behavior

`Assets/Scripts/InGame/GameMaster/GameMaster.Card.cs:241-316`, unique pass:

```csharp
case CardRarity.UltraRare:
    maxCard = 2;
    _nonUniqueList[i] = uniqueList[_randomSeed];   // :262, i == 0
    break;
```

- If `_nonUniqueList` is empty, this throws `ArgumentOutOfRangeException`. `StartNewGame` → `CreateDeck` then aborts, and the match never starts.
- If it isn't empty, it **overwrites** element 0 (a Common/Uncommon/Rare card), so that card silently disappears from the refill pool.
- Common, Uncommon and Rare are `Add`ed to the refill pool. Unique isn't (maximum 1).
- The refill-pass `switch` (`:287-297`) still has an `UltraRare` case. With this fix it can't be reached, which is harmless.
- All 5 current cards are `m_rarity: 0` (Common), so this hasn't happened yet.

## 3. Relevant files and dependencies

- `Assets/Scripts/InGame/GameMaster/GameMaster.Card.cs`: `FillCardSection` (the only change)
- `Assets/Scripts/InGame/CardData.cs`: the `CardRarity` enum (read-only)
- Called by `CreateDeck` (`:172-233`) ← `StartNewGame` (`GameMaster.cs:447`)

## 4. Required changes

1. Delete line `:262` (`_nonUniqueList[i] = uniqueList[_randomSeed];`). The `UltraRare` case becomes `maxCard = 2; break;`.
2. Nothing else changes. Leave the refill-pass `UltraRare` case in place.

## 5. Explicit non-goals

- Per-deck copy caps. Today `maxCard` is applied **per pick**, so in the refill pass a Common card can be picked several times and reach 8 or more copies. This is a separate design question (§10).
- Changing section sizes, `Random.Range(0, 2)` (L2), or shuffling.
- Adding UltraRare card assets.

## 6. Constraints and compatibility

- No renames. One line removed.
- Deck contents with the current card set (all Common) must be identical in distribution.

## 7. Acceptance criteria

- [ ] With a temporary UltraRare card in the database, `StartNewGame` completes, and the deck contains exactly 2 copies of it.
- [ ] Common cards' presence in the refill pool is unaffected.
- [ ] With the current database, nothing changes.
- [ ] Compiles without new warnings.

## 8. Regression tests

No automated tests. Review: the diff is the single line removal.

## 9. Manual Unity Editor checks

1. **Temporary (don't commit):** duplicate a card asset, set `m_rarity` to UltraRare, and add it to `CardDatabase`.
2. Start a match with ParrelSync host and client. No exception in `CreateDeck`. Optionally log the deck composition with a temporary debug log.
3. Remove the temporary asset and the database entry, and confirm `git status` shows no leftover asset changes.

## 10. Open questions and assumptions

- **Q1 (design, out of scope):** are 4 for Common/Uncommon/Rare, 2 for UltraRare and 1 for Unique meant as **total per deck**? If so, `FillCardSection` needs a rework (count copies per `CardData` across both passes). This is a candidate for a separate issue.
