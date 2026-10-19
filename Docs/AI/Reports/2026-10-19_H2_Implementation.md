# H2 implementation: refill exhausted decks without the rarest cards

## 1. Task summary

Implemented `TASK-H2_DeckRefillWithoutRarestCards.md`. Empty decks now cause one synchronous server refill per draw, after deleting the old graveyard. Refill decks exclude UltraRare and Unique by default. Null sessions and empty eligible pools warn and return instead of indexing an empty list.

## 2. Files changed

- `Assets/Scripts/InGame/GameMaster.cs`
- `Assets/Scripts/InGame/GameMaster/GameMaster.Card.cs`
- `Docs/AI/Architecture/Architecture.md`
- `Docs/AI/Architecture/GameLoop.md`
- `Docs/AI/Architecture/Dependencies.md`
- This implementation report.

The existing H3 changes, task deletions, new specifications and Issue Report #5 were preserved. No scenes, prefabs, card assets or packages were modified. No commit or push was performed.

## 3. Important implementation details

- Added serialized `m_refillDeckMaxRarity = CardRarity.Rare` next to the mana settings under GameRules. No Inspector setup is required; the current GameMaster prefab uses the C# default. The setting can be changed later in the Inspector.
- `CreateDeck(CardRarity maxRarity = CardRarity.Unique)` filters null entries and cards above the ceiling. `StartNewGame` still calls `CreateDeck()` with no argument, preserving all initial rarities.
- New private `RefillDeck` has an `IsServer` guard. It deletes every non-null graveyard card using `Despawn(true)` for spawned objects or `Destroy(gameObject)` otherwise, removes those entries from `m_spawnedCards`, clears `m_graveyard`, then builds the filtered deck. Debug logging includes the new size; an empty refill always warns.
- `DrawCard` rejects a null session first, trims leading null cards before and after its single refill attempt, checks count before `[0]`, and retains the existing AddCard/SetOwner/owner-targeted identity RPC/removal sequence.
- The two callers remain server game-loop draws and DrawCard activities. Refill introduces no coroutine yields, RPCs or NetworkVariables. NetworkCard despawn already clears its local views and does not mutate the graveyard collection during iteration. Next-match `DespawnAllCards` retains tracking for cards still in hands/boards and all newly created cards.
- Section limits, copy rules, shuffle, graveyard movement, character/block processing and the first-deck call are unchanged. No optional pure filter helper was added.
- Minor specification discrepancy: the current Common-only database produces **84**, rather than 80, cards because sections with a limit of 30 are filled in batches of four. This existing behavior is preserved, as required by H2's non-goals.

## 4. Tests actually executed and results

### Runtime script compilation

Used Unity 6000.3.13f1's bundled Roslyn compiler with the existing Editor `Assembly-CSharp.rsp` source list, references, definitions and analyzers, redirecting outputs to ignored `build/task-h2-verification/`.

Result: exit code 0, no errors and no new warnings. Only existing CS0114 (`PlayerUI.OnDestroy`) and CS0414 (`GameMaster.m_cardResolveDelay`) were emitted. This checks C# compilation; it does not constitute Unity Editor import, NGO IL postprocessing, a player build or Play Mode verification.

Evidence: `build/task-h2-verification/compile.rsp` and `compile-results.txt`.

### Focused source behavioral checks

Ran `build/task-h2-verification/verify.ps1`. It compiles the actual production `DrawCard`, `RefillDeck`, `CreateDeck`, `FillCardSection`, `Shuffle` and `DespawnAllCards` source with Unity/NGO doubles. Production rarity enums and the refill field initializer are also extracted from source.

**13 scenarios passed:**

1. Null session warns without refilling or changing deck/graveyard state.
2. Leading nulls are skipped and an existing card is drawn without a refill, preserving owner-only reveal targeting.
3. Initial deck includes all rarities and retains UltraRare/Unique copy limits and spawn bookkeeping.
4. Spawned/unspawned/null graveyard entries are handled; every deletion/untracking operation precedes the first new instantiation. Hand and board cards remain unchanged.
5. Four consecutive refills discard all old cards without accumulating graveyard objects in the mock tracked set.
6. Both player sessions receive different cards with their respective owner-only reveal targets.
7. Two consecutive draws cross from the last old card to a new deck, like Clarity.
8. An all-null deck refills once and draws.
9. An empty filtered pool warns and returns after one attempt, even with debug logging disabled.
10. A database containing only null entries returns without drawing.
11. Configuring UltraRare permits that rarity while still excluding Unique.
12. A non-server refill neither deletes nor creates cards.
13. Next-match cleanup still despawns active cards and the new deck, without double-despawning deleted graveyard cards.

Static checks also passed: no `ElementAt` remains in DrawCard, there is one refill call site, and the no-argument initial `CreateDeck()` remains. Final Git diff and whitespace checks were reviewed. Harness/output files are git-ignored, outside Assets, and do not add permanent test scripts or test assets.

## 5. Remaining risks / checks not executed

- No Unity Play Mode, separate host/client replication, UI identity visibility test or full player build was run for H2. The behavioral checks use doubles; Unity's deferred destruction and NGO replication require Editor verification.
- Cards retained in hands/boards remain live by design. The total live-card count can still rise if players retain cards indefinitely; H2 removes obsolete graveyard objects, and a hand limit is outside scope.
- A database with no eligible cards intentionally produces warnings and no draw. Other asset/setup failures such as a missing database or prefab are not covered by the empty-deck fix.

## 6. Manual Unity Editor verification

1. Temporarily set GameMasterPrefab `m_startHandCards = 20` and enable debug logging. Run a ParrelSync host/client match until exhaustion; check refill-size logs, continued draws for both players, no exception and no `match aborted`.
2. Confirm each player's newly drawn identities are visible only to that player. Compare spawned card counts around two refills, accounting for cards retained in hands/boards; obsolete graveyard cards should disappear.
3. Temporarily set Clarity to UltraRare. Verify the initial deck can include it and refill decks contain no newly created Clarity. Also cast Clarity with one card left in the deck to test the old/new boundary.
4. Restore every temporary prefab/card change. No persistent Inspector wiring is required for H2.
