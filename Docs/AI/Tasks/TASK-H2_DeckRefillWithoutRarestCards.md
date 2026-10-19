# TASK-H2: Build a new deck without the rarest cards when the deck runs out

- **Source issue:** [Issue Report #5 → H2](../Reports/2026-10-19_IssueReport-5.md#-high-still-open), first reported in [#1](../Reports/2026-10-18_IssueReport.md#-high)
- **Owner decisions (2026-10-19):**
  - "Create new deck without most rare cards". "Most rare" means `UltraRare` and `Unique` (confirmed).
  - "Delete all objects from Graveyard before we create the new deck" (confirmed)
- **Severity:** 🟠 High. Reachable in long matches. Today it aborts the match to the lobby with no result.
- **Order:** 2 of 5 (H3 → **H2** → N14 → N18_M1 → N19)
- **Implementer:** Codex

## 1. Objective

A draw never fails because the deck is empty. When the server needs a card and the deck is empty, it:
1. discards the old graveyard
2. builds a **new deck** with the existing deck algorithm, leaving out the rarest cards
3. draws from the new deck

The match continues normally.

## 2. Current behavior

- `GameMaster.DrawCard(PlayerSession session)` (`Assets/Scripts/InGame/GameMaster/GameMaster.Card.cs:149-168`) calls `m_deck.ElementAt(0)`. On an empty list this throws `ArgumentOutOfRangeException` **before** the `card == null` check. `RunGuarded` (H1) catches it and calls `AbortMatch("loop_exception")`, so both players are sent to the lobby with no result.
- `DrawCard` has two callers:
  - `DrawCardPhase` (`GameMaster.GameLoop.cs:567`): `m_startHandCards` (2) per player per round
  - `ProcessActivity` → `CardActionType.DrawCard` (`GameMaster.Processing.cs:232`): Clarity draws 2. The target session can be `null` for a non-player target (M9).
- `CreateDeck()` (`Card.cs:172-233`) picks section limits randomly (20/30/30 or 30/20/30) and fills each section from all cards in `m_cardDatabase.m_cards` with `FillCardSection`:
  - Common, Uncommon and Rare: 4 copies, and they can repeat
  - UltraRare: 2 copies; Unique: 1 copy (since H9, these never repeat)
  - every card is `Instantiate`d, `Spawn()`ed, set face up, and added to `m_spawnedCards` and `m_deck`
- With today's database (4 cards, all `Common`) the deck has 80 cards, shared by both players.
- Cards that leave play go to `m_graveyard` through `MoveCardOnGraveyard` (`Card.cs:39-78`) and stay spawned forever. The graveyard is never reused.
- `CardRarity` order: `Common, Uncommon, Rare, UltraRare, Unique` (`InGame/CardData.cs:42-49`).

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/GameMaster.cs` | One config field under `[Header("GameRules")]` |
| `Assets/Scripts/InGame/GameMaster/GameMaster.Card.cs` | `DrawCard` guards and refill; `RefillDeck`; rarity filter parameter for `CreateDeck` |

Also involved, but unchanged:
- `StartNewGame` calls `CreateDeck()` and `DespawnAllCards()`
- `ClearupAfterDisconnect` clears the lists (H7)
- `SendCardDataClientRpc` still reveals each drawn card only to its owner
- `GameMasterPrefab.prefab` doesn't serialize the GameRules fields, so the C# default applies

## 4. Required changes

1. **Config** (`GameMaster.cs`, next to the mana fields):
   ```csharp
   [SerializeField] private CardRarity m_refillDeckMaxRarity = CardRarity.Rare;
   ```
   Cards with a rarity **higher** than this value (`UltraRare`, `Unique` by default) are left out of refill decks.
2. **`CreateDeck`:** add an optional parameter, `private void CreateDeck(CardRarity maxRarity = CardRarity.Unique)`.
   - In the `foreach` over `m_cardDatabase.m_cards`, skip `null` entries and cards with `card.m_rarity > maxRarity`.
   - Everything else stays the same: section limits, `FillCardSection`, `Shuffle`.
   - `StartNewGame` keeps calling `CreateDeck()`, so the first deck still contains every rarity.
3. **`RefillDeck()`** (new, private, server-only):
   1. **First, delete every graveyard object** (owner decision). For each card in `m_graveyard`:
      - if it's `null`, skip it
      - if `NetworkObject.IsSpawned`, call `NetworkObject.Despawn(true)`, which also destroys the GameObject
      - otherwise call `Destroy(card.gameObject)`
      - remove it from `m_spawnedCards`

      Then `m_graveyard.Clear()`.
   2. **Only then** call `CreateDeck(m_refillDeckMaxRarity)`.
   3. Under `m_debugMode`, log the new deck size. Log a warning if it's 0.

   Why despawn the graveyard: without it, every refill adds about 80 network objects that are never used again. Graveyard cards aren't in any hand or board list, and no view shows them. Any queued package that refers to one is already dropped safely ("NetworkCard … was not found").
4. **`DrawCard`:**
   1. If `session == null`, log a warning and return. This also closes M9.
   2. Remove `null` entries from the front: `while (m_deck.Count > 0 && m_deck[0] == null) m_deck.RemoveAt(0);`
   3. If `m_deck.Count == 0`, call `RefillDeck()` and repeat step 2.
   4. If the deck is still empty, log `"No card to draw"` and return. **Never throw.**
   5. Otherwise draw `m_deck[0]` as today: `AddCard`, `SetOwner`, the owner-targeted `SendCardDataClientRpc`, `m_deck.Remove`. Replace `ElementAt(0)` with `[0]`.
5. Call `RefillDeck` at most **once** per `DrawCard` call, so an empty filtered pool can't loop.

## 5. Explicit non-goals

- Changing the first deck, the section limits, the copy limits or the shuffle.
- Telling players about the refill (announcer text, UI, sound). If wanted later, it needs en/ru/ja keys.
- A hand limit (M15) or deck-out loss rules.
- Changing `MoveCardOnGraveyard`, `ClearPlayedCards` or `ClearDiscardedCards`.
- The `CreateDeck` `Random.Range(0, 2)` quirk (L2).

## 6. Constraints and compatibility

- No renames. `DrawCard` and `CreateDeck` are private, and the optional parameter keeps the existing call compiling unchanged.
- Server-only changes. Card identity stays hidden: the refill uses the same spawn and reveal path as match start.
- `m_spawnedCards` must stay consistent, so `DespawnAllCards` in the next `StartNewGame` still finds every live card.
- A refill can happen inside `ProcessActivity` (Clarity) or `DrawCardPhase`. It's synchronous, so there are no new yields in the loop.

## 7. Acceptance criteria

- [ ] Drawing with an empty deck never throws, and the match isn't aborted.
- [ ] The first refill creates a new shuffled deck. Draws continue for both players, and each card is revealed only to its owner.
- [ ] Refill decks contain no `UltraRare` or `Unique` cards (default setting). The first deck still contains them.
- [ ] At refill, every graveyard object is deleted **before** the new deck is created. The number of card objects doesn't keep growing across refills.
- [ ] If the filtered pool is empty, `DrawCard` logs a warning and returns. The match goes on with no draw.
- [ ] `DrawCard(null)` logs a warning instead of throwing (M9).
- [ ] Compiles without new warnings.

## 8. Regression tests

- **Optional EditMode test:** extract the rarity filter as a pure function (card list + max rarity → eligible list) and test it with Common/UltraRare/Unique entries.
- **Review checklist:**
  - `ElementAt(0)` is gone from `DrawCard`
  - `RefillDeck` is called at most once per draw
  - `m_spawnedCards` loses the despawned graveyard cards and gains the new deck
  - `StartNewGame` still calls `CreateDeck()` with no argument

## 9. Manual Unity Editor checks

1. **Fast deck exhaustion (temporary, don't commit):** on `GameMasterPrefab`, set `m_startHandCards = 20` and `m_debugMode = 1`. ParrelSync host and client. Round 1 draws 40 cards; by round 2 or 3 the deck runs out.
   - Expected: a refill log, no exception, no `match aborted`.
   - Both players keep receiving cards they can see. The opponent's cards stay hidden.
2. **Object count:** in the host Hierarchy, or with a temporary log of `NetworkManager.SpawnManager.SpawnedObjectsList.Count`, compare the count before and after two refills. It shouldn't grow by about 80 each time.
3. **Rarity filter (temporary, don't commit):** set `Clarity.asset` to `m_rarity: UltraRare` (3). After a refill, no new Clarity is drawn, while the first deck had Clarity. Revert.
4. Cast Clarity when the deck has 1 card left: one card comes from the old deck, one from the new deck, and there's no error.
5. Revert every temporary change.

## 10. Open questions and assumptions

- **A1 (confirmed by the owner):** "most rare cards" means `UltraRare` and `Unique`, so `m_refillDeckMaxRarity = Rare`. The owner can change it in the Inspector. For example, set it to `UltraRare` to exclude only `Unique`.
- **A2:** Cards in hands and on the board keep their state. Only the graveyard is discarded.
- **A3:** Every current card is `Common`, so today the refill deck has the same mix as the first deck.
- **Q1:** Should a refill ever be shown to players? This spec says no (non-goal).
