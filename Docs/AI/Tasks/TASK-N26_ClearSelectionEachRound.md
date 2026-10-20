# TASK-N26: An unconfirmed card selection is cleared when a new Card Selection starts

- **Source issue:** [Issue Report #7 → N26](../Reports/2026-10-20_IssueReport-7.md#-low-23)
- **Severity:** 🟢 Low (Confirmed, design)
- **Baseline:** `b7e7b91`. Line numbers below are from this commit.
- **Order:** 10 of 20. After TASK-N21, which also edits `PlayerSession.ConfirmTurn`.
- **Owner decision (2026-10-20):** **clear each round.** A new Card Selection starts empty. A selection survives only a respawn pause inside the same Card Selection.
- **Implementer:** Codex

## 1. Objective

A card that was selected but not confirmed in one round isn't highlighted or remembered in the next round's Card Selection. Within one Card Selection, a death pause and the resume keep the selection.

## 2. Current behavior

- The local selection lives in `PlayerSession.m_selectedCardsToPlay` / `m_selectedCardsToDiscard` (client, owner only).
- `ClearLocalCardSelection()` (`PlayerSession.cs:327-331`) has only three callers:
  - `:201`, after `ConfirmTurn` sends the selection
  - `:227`, `SelectionRejectedClientRpc`
  - `:741`, `ClearSession()`
- An unconfirmed selection therefore carries over to every later round. `PlayerUI.UpdateCards` re-highlights it in `CardSelection` (`UI/PlayerUI.cs:312-319`).
- Since M15, `PruneLocalCardSelection()` (`PlayerSession.cs:261-265`, called from `PlayerUI.UpdateCards` and `ConfirmTurn`) drops cards that have left the hand, so the carry-over can't produce a false `[Suspicious] InvalidCardSelection` any more.
- **The phase sequence during a death pause in Card Selection:**
  - `HandleDeathPausingCurrentPhase` (`GameMaster.GameLoop.cs:581-610`) saves the phase
  - `WaitForRespawnIfNeeded` sets `RespawnWait` (`:646`) and, at the end, `ResolveCards` (`:750`)
  - then the saved `CardSelection` is restored (`:607`)
  - a client watching only `m_gamePhase` can't reliably tell "new Card Selection" from "resumed Card Selection". On the host, `OnValueChanged` fires for every intermediate write.
- There's no round or selection counter in `GameMaster`.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/GameMaster.cs` | New `NetworkVariable<int> m_selectionPhaseId` |
| `Assets/Scripts/InGame/GameMaster/GameMaster.GameLoop.cs` | `CardSelectionPhase` increments it once at its start |
| `Assets/Scripts/UI/PlayerUI.cs` | Subscribes to it; clears the local selection on a change |

## 4. Required changes

1. **`GameMaster.cs`:** add
   `public NetworkVariable<int> m_selectionPhaseId = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);`
2. **`CardSelectionPhase()`** (`GameLoop.cs:495-535`): right after `m_gamePhase.Value = GamePhase.CardSelection;` (`:497`), add `m_selectionPhaseId.Value++;`.
   - The resume path (`:607`) must **not** touch it, so a resumed Card Selection keeps its ID.
   - Reset it to 0 in `StartNewGame()` (`GameMaster.cs:495`), next to the other resets at `:529-538`.
3. **`PlayerUI`:**
   - Where `Update` subscribes to `GameMaster.Instance.m_gamePhase.OnValueChanged` (`:127-133`), also subscribe `m_selectionPhaseId.OnValueChanged += OnSelectionPhaseChanged;`. Use the same `-=`/`+=` pattern.
   - Unsubscribe in `ClearLocalUI()` next to the phase unsubscribe (`:555-559`) and wherever else `PlayerUI` unsubscribes from `GameMaster` (check `OnDestroy`).
   - `private void OnSelectionPhaseChanged(int previous, int current)`: if `m_playerSession != null`, call `m_playerSession.ClearLocalCardSelection()`, then `UpdateCards(default)`.
4. Don't change `ClearLocalCardSelection()` or its other callers.

## 5. Explicit non-goals

- No change to the server-side selection rules (MANA2), the hand limit (M15) or the pruning.
- No change to the board-card selection or the confirmed target (N20 Q1, by design).
- No change to the N21 confirm tagging.

## 6. Constraints and compatibility requirements

- The client clears only its own local lists. Nothing new is sent to the server.
- A new NetworkVariable changes `GameMaster`'s network layout. Both peers use the same build.
- On the host, `OnValueChanged` fires synchronously on the increment, before any card is dealt into the new Card Selection. That's fine: the hand view rebuilds right after.

## 7. Acceptance criteria

1. Round N: select a card, don't confirm, and let the timer run out. In round N+1's Card Selection, nothing is highlighted, and Confirm sends an empty selection unless you select again.
2. A death pause during Card Selection (lava kill in the Scene view) keeps the cards selected before the pause, and they're still highlighted after the resume.
3. A confirmed selection behaves as before.
4. The host and the client behave the same.

## 8. Regression tests

- Harness: `CardSelectionPhase` increments `m_selectionPhaseId` once per call. The death-pause resume path doesn't change it.
- If `PlayerUI` can be tested in the harness: a change of the ID clears both local lists.
- Roslyn compile. Exit 0.

## 9. Manual Unity Editor checks

1. Two players. Round 1: the client selects a card and doesn't confirm, and the timer runs out. Round 2 Card Selection: the card isn't highlighted on the client. Repeat on the host.
2. During a Card Selection, select a card, then kill your own pony in the lava (host Scene view). After the respawn, the same card is still highlighted, and Confirm plays it.
3. Repeat check A5 (death during Card Selection keeps the selections) from the checklist: it must still pass.
4. Hand limit: run B5.5 again (`m_maxHandCards = 3`, then revert). There's still no `[Suspicious] InvalidCardSelection`.

## 10. Open questions and assumptions

- None. The owner decided the rule on 2026-10-20.
