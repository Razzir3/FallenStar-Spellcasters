# TASK-N6: Stop resolving queued cards once the match is decided

- **Source issue:** [Issue Report #7 → N6](../Reports/2026-10-20_IssueReport-7.md#-low-23)
- **Severity:** 🟢 Low (Confirmed)
- **Baseline:** `b7e7b91`. Line numbers below are from this commit.
- **Order:** 5 of 20. Independent. TASK-L3, TASK-N3 and TASK-L1 edit the same file later; keep this change when they're applied.
- **Implementer:** Codex

## 1. Objective

When a card activity decides the match (the last life of a player is lost), the server stops resolving any **other** card packages that are still queued in that action phase.

## 2. Current behavior

`InGame/GameMaster/GameMaster.GameLoop.cs`, `PlayerActionPhase` (`:157-344`):
- The phase loop (`:177-331`) checks `IsGameFinished()` once per iteration (`:179`).
- Inside it, `while (m_cardInputQueue.Count > 0)` (`:185-321`) drains **all** queued packages:
  - it validates each one (`:189-298`)
  - it runs the activities (`:300-314`)
  - it moves the card to the graveyard (`:316`), clears `m_isResolvingCards` (`:318`) and yields one frame (`:320`)
- `if (IsGameFinished()) break;` at `:308` and `:312` only leaves the **activity** `for` loop. The queue loop continues with the next package, so a second card can still deal damage, heal, draw or change mana after the match is decided.
- The match then ends normally: `GameLoop` sees `IsGameFinished()`, then `EndMatch` (`:127`) clears `m_cardInputQueue` (`GameMaster.cs:428`). The extra effects are visible to both players for a moment and in the logs, but the result is the same.
- `ResolvePlayerCardsPhase` (`:407`) has the same pattern, but it's dead code (TASK-L10 removes it).

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/GameMaster/GameMaster.GameLoop.cs` | `PlayerActionPhase`: leave the queue loop when the match is decided |

## 4. Required changes

1. In the queue loop, after `MoveCardOnGraveyard(_currentCard);` and `m_isResolvingCards = false;` (`:316-318`), and **before** `yield return null;` (`:320`), add:
   ```csharp
   if (IsGameFinished())
       break;
   ```
   This leaves the queue loop. The outer loop then reaches its own `IsGameFinished()` check (`:179`) and breaks, and the existing cleanup at `:333-343` (`ClearDiscardedCards`, `ClearPlayedCards`) still runs.
2. Keep the existing inner `break`s at `:308` and `:312`: they stop the remaining activities of the deciding card.
3. Leave the remaining queued packages in `m_cardInputQueue`. `EndMatch` clears the queue. Don't move their cards to the graveyard.

## 5. Explicit non-goals

- No change to `IsGameFinished()` itself (TASK-L3).
- No change to `ResolvePlayerCardsPhase` (TASK-L10 deletes it).
- No change to how a respawn pause inside an activity works (`:309-313`).

## 6. Constraints and compatibility requirements

- Server-only code path; no network or API changes.
- `RunGuarded` behaviour unchanged: no new `yield` types.

## 7. Acceptance criteria

1. With two packages queued in the same frame, where the first one's activity takes the opponent's last life, the second package's activities **don't run**: no HP, mana, block or draw change from it.
2. The match ends with `LivesDepleted` and the correct winner, as before.
3. When the first card doesn't decide the match, both packages resolve as before, one per frame.

## 8. Regression tests

- A harness test (same style as the earlier GameMaster harnesses): queue two packages with stubbed activities. Make the first set the opponent's lives to 0, and assert the second activity's stub wasn't called. Second case: the first doesn't decide, so both are called.
- Roslyn compile. Exit 0.

## 9. Manual Unity Editor checks

1. Normal two-player match to a lives-depleted end. The result screen appears on both sides as before.
2. Optional, hard to time by hand: give the opponent one life and low HP (Space debug key), confirm two attack cards, and Send both quickly in your Turn. Only the first one's effect is seen, then the match ends.

## 10. Open questions and assumptions

- Assumption: a deciding activity always makes `IsGameFinished()` true in the same frame, because character death and the life update are synchronous on the server. If a death only becomes visible a frame later (despawn timing), the outer check at `:179` catches it on the next iteration. That's acceptable.
