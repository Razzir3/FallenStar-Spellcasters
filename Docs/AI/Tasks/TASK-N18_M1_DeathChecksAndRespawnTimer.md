# TASK-N18_M1: Handle a death in any phase; pause the current phase for the respawn

- **Source issues:**
  - [Issue Report #5 → N18](../Reports/2026-10-19_IssueReport-5.md#2-new-findings): deaths outside `PlayerAction` are handled late
  - [M1](../Reports/2026-10-18_IssueReport.md#-medium): the shared `m_timer` is reset by the nested respawn wait
- **Owner decision (2026-10-19):** "If a player dies, we must check respawn conditions. If respawn is possible, pause the current phase before the respawn."
- **Severity:** 🟡 Medium (both)
- **Order:** 4 of 5 (H3 → H2 → N14 → **N18_M1** → N19). Both fixes change the same respawn/timer code in `GameMaster.GameLoop.cs`, so they're one task.
- **Implementer:** Codex

## 1. Objective

When a player dies, in **any** phase, the server reacts within one frame:
1. **Check the respawn conditions.** A respawn is possible only if the dead player has lives left (`m_lifeCurrent > 0`) **and** at least one valid own block with a spawn point (`GetRespawnPoints`).
2. **Respawn not possible:** apply the existing rules. No lives left means the game is finished. Lives left but no blocks means elimination (`m_lifeCurrent = -1`). If the game is finished, end the match at once.
3. **Respawn possible: pause the current phase** (RoundBreak, DrawCards, CardSelection or PlayerAction), run the existing respawn wait, then **resume the same phase** where it stopped. Restore the phase value, its timer and the active player.

This also fixes **M1**: a respawn during an action turn no longer eats or resets the active player's turn time.

## 2. Current behavior

All in `Assets/Scripts/InGame/GameMaster/GameMaster.GameLoop.cs` unless noted.

**Round loop (`GameLoop`, `:98-124`):**
```
while (!IsGameFinished())        // the only finish check per round
    RoundBreakPhase → DrawCardPhase → CardSelectionPhase → PlayerTurnPhase
EndMatch(winner, LivesDepleted)
```

**Where deaths are checked:**
- `PlayerActionPhase` (`:153-377`):
  - at the top of every frame (`:175-194`): `m_externalDeathDetected || IsAnyPlayerDead()` → `IsGameFinished()` → break, or `WaitForRespawnIfNeeded()`, then restore `PlayerAction` and `m_activePlayer`
  - after each card activity (`:330-346`), the same pattern
- `PlayerTurnPhase` (`:129-147`): `IsGameFinished()` after each player's turn.
- `RoundBreakPhase` (`:407-431`), `DrawCardPhase` (`:559-573`) and `CardSelectionPhase` (`:521-554`) have **no** death or finish check.

**Lava deaths can happen in any phase:** `LavaKillZone` → `GameMaster.ServerKillPlayerByHazard` (`GameMaster.Processing.cs:370-404`) can fire at any time. It lowers the life, despawns the character and sets `m_externalDeathDetected`.

Typical case: the attacker destroys the block under the opponent with the last card and ends the turn. The opponent's fall ends in the lava during `RoundBreak`. Two outcomes:
- **Last life:** the match runs on for up to about 70 s, until the next `PlayerActionPhase` notices.
- **Not the last life:** the respawn wait runs inside the next round's first action turn, possibly the other player's.

**`WaitForRespawnIfNeeded` (`:598-714`):**
- loops while `IsAnyPlayerDead()`
- `FindDeadPlayer()` picks the first dead player with lives > 0, or returns `null`, which ends the wait
- sets `GamePhase.RespawnWait`, `m_isWaitingRespawn` and `m_waitingRespawnPlayer`
- if `GetRespawnPoints` is empty, sets the life to `-1` and ends the wait
- otherwise **sets `m_timer.Value = 0f` (`:640`)** and waits up to `m_respawnWaitTime` (30 s) for the block choice, then calls `RespawnCharacter`
- at the end sets `GamePhase.ResolveCards`

**M1:** because of `:640`, `PlayerActionPhase` continues from whatever value the respawn wait left, not from the turn time it had.

**Other facts the implementer needs:**
- `IsGameFinished()` (`:756-791`) allocates a small list and calls `FindLostPlayer()` (side effect L3: it sets `m_externalDeathDetected` when a player has ≤ 0 lives).
- `IsAnyPlayerDead()` (`:740-751`): character is `null` and `TeamId != -1`.
- **Client UI during `RespawnWait`:**
  - `PlayerUI.UpdateCards` (`UI/PlayerUI.cs:215-260`) runs on every phase change and has no case for `RespawnWait`, so the hand is hidden. When the phase comes back, it **rebuilds all card views without selection highlights**.
  - The local selection lists in `PlayerSession` (`m_selectedCardsToPlay` / `m_selectedCardsToDiscard`) are kept.
  - So after a paused `CardSelection` the player would see no highlights but would still send the old selection.
- **Client confirm during `RespawnWait`:** `PlayerSession.ConfirmTurn` (`InGame/PlayerSession.cs:129-147`) sends only a respawn block choice, and only the waiting player can send it. It doesn't clear the card selection.
- H6's `HandlePlayerLeft` stops the coroutine from outside. These changes must keep working with that.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/GameMaster/GameMaster.GameLoop.cs` | New pause-and-respawn helper; death and finish checks in every phase; `PlayerActionPhase` uses the helper |
| `Assets/Scripts/InGame/PlayerSession.cs` | Two read-only selection queries for the UI |
| `Assets/Scripts/UI/PlayerUI.cs` | Restore selection highlights when card views are rebuilt |

Unchanged but involved: `RunGuarded` (H1), `EndMatch` (H5), `WaitForRespawnIfNeeded`, `GetRespawnPoints`, `FindDeadPlayer`, `GameAnnouncerUI` (shows `RespawnWait` and `m_timer`), MANA2 selection counters.

## 4. Required changes

### 4.1 Server: one helper for "death → check conditions → pause → respawn → resume"

1. Add `private IEnumerator HandleDeathPausingCurrentPhase()` in `GameMaster.GameLoop.cs`:
   1. `m_externalDeathDetected = false;`
   2. If `!IsAnyPlayerDead()`, `yield break`. Nothing to do; this also clears a stale lava flag.
   3. **Check the respawn conditions:** `PlayerSession _dead = FindDeadPlayer();` (a dead player with lives > 0)
      - if `_dead == null`: no respawn is possible (the dead players have no lives left) → `yield break`. The caller's finish check ends the match.
      - if `GetRespawnPoints(_dead).Count == 0`: no respawn is possible → `_dead.m_lifeCurrent.Value = -1;` (the existing elimination rule) → `yield break`
   4. **Respawn possible: pause.** Save:
      ```csharp
      GamePhase _pausedPhase = m_gamePhase.Value;
      float _pausedTimer = m_timer.Value;
      NetworkBehaviourReference _pausedActivePlayer = m_activePlayer.Value;
      ```
   5. `yield return WaitForRespawnIfNeeded();` (unchanged; it handles several dead players and the 30 s block choice)
   6. If `IsGameFinished()`, `yield break`. Don't restore anything; the match is ending.
   7. **Resume:** restore `m_gamePhase.Value = _pausedPhase; m_timer.Value = _pausedTimer; m_activePlayer.Value = _pausedActivePlayer;`
2. **In every phase**, use the same two-step check:
   ```csharp
   if (IsGameFinished()) yield break;
   if (m_externalDeathDetected || IsAnyPlayerDead())
   {
       yield return HandleDeathPausingCurrentPhase();
       if (IsGameFinished()) yield break;
   }
   ```
   Where it goes:
   - `RoundBreakPhase`: inside the `while`, before increasing the timer
   - `DrawCardPhase`: before each `DrawCard(...)`
   - `CardSelectionPhase`: inside the `while`, before the `AllPlayerConfirmedSelection()` check. A finished game skips `BuildTurnOrderByCardCost`; that's fine.
   - `PlayerActionPhase`: replace **both** existing blocks (`:175-194` and `:330-346`) with the helper. Here, use `break` instead of `yield break` for "game finished", exactly as today:
     - at the top of the frame, `break` out of the `while` loop
     - at the activity site, `break` out of the activity `for` loop

     That way the end-of-phase cleanup (`ClearDiscardedCards` / `ClearPlayedCards`) still runs. The old manual restore of `PlayerAction` / `m_activePlayer` is replaced by the helper's resume. **This is the M1 fix:** the turn timer is restored.
3. **`GameLoop`:** after `RoundBreakPhase`, `DrawCardPhase` and `CardSelectionPhase`, add `if (IsGameFinished()) break;`. The existing end of `GameLoop` (winner selection + `EndMatch`) stays as it is.
4. **Resuming means continuing, not restarting.** Start-of-phase actions don't run again: mana growth in `RoundBreakPhase`, selection setup and `m_selectionTimerResetsThisPhase.Clear()` in `CardSelectionPhase`, draws already made. Confirmed selections in `m_turnSelections` are kept.
5. Don't change `WaitForRespawnIfNeeded`, `FindDeadPlayer`, `GetRespawnPoints` or `RespawnCharacter`.

### 4.2 Client: keep selection highlights across the pause

6. **`PlayerSession`:** add read-only queries:
   ```csharp
   public bool IsCardSelectedToPlay(NetworkCard card)    { return m_selectedCardsToPlay.Contains(card); }
   public bool IsCardSelectedToDiscard(NetworkCard card) { return m_selectedCardsToDiscard.Contains(card); }
   ```
7. **`PlayerUI.CreateCardView`:** after `Setup` / `SetNetworkCard`, when the current phase is `CardSelection`, restore the highlight:
   - selected to play → `SetSelectedColor()`
   - selected to discard → `SetAlterSelectedColor()`

   This keeps what the player sees and what will be sent in sync after any rebuild, including the resume after a paused `CardSelection`.

## 5. Explicit non-goals

- Changing the respawn rules themselves: lives, the 30 s block choice, automatic block choice on timeout, elimination with no blocks.
- N6 (queued card packages still resolve after a decisive activity).
- Making `IsGameFinished()` side-effect free or non-allocating (L3). The extra calls are acceptable: the side effect only fires when a player has ≤ 0 lives, which already means the game is ending.
- Per-player timers, or showing remaining instead of elapsed time (L1).
- Showing the hand during `RespawnWait`, or new announcer texts. `RespawnWait` is already localized.

## 6. Constraints and compatibility

- No renames. Private coroutine bodies, one new private helper and two additive public read-only methods.
- Phase values and their order stay the same. No `GamePhase` changes (RP1). A paused phase gets back its **original** value; `ResolveCards` from the end of `WaitForRespawnIfNeeded` must not remain.
- The loop must keep working with `RunGuarded`:
  - only `yield return null`, nested `IEnumerator`s and `WaitForSeconds`
  - no `CustomYieldInstruction` (N12)
- Behavior with no deaths is unchanged: same phases, same timing.
- MANA2: `ResetCardSelectionTimer` only acts in `CardSelection`, so it does nothing during the pause. Resuming restores the timer value from before the pause, including any reset that was already applied.
- H6 still works: `HandlePlayerLeft` can stop the coroutine during any pause.

## 7. Acceptance criteria

- [ ] **Decisive death in any phase** (last life lost, or no blocks left): `GamePhase.GameOver` and the result screens come within about one frame. No `RespawnWait` is shown.
- [ ] **Non-decisive death during RoundBreak, DrawCards or CardSelection:** `RespawnWait` starts at once. After the respawn, the same phase continues with the timer value it had at the death, and the phase isn't restarted:
  - no second mana growth
  - accepted selections and remaining draws are kept
- [ ] **Death during an action turn** (either player): after the respawn, `PlayerAction` resumes for the same active player with the same elapsed time as before the death (M1).
- [ ] After a paused `CardSelection`, the hand shows the same highlights as before the pause, and confirming sends exactly the highlighted cards.
- [ ] A match with no deaths behaves exactly as before.
- [ ] The H6 forfeit still works in every phase, including during a pause.
- [ ] Compiles without new warnings.

## 8. Regression tests

No automated tests are required. **Review checklist:**
- every phase uses the same check order: finish → death helper → finish
- the helper checks respawn conditions **before** pausing
- the helper saves and restores phase, timer **and** active player
- the old manual restore in `PlayerActionPhase` is gone
- `WaitForRespawnIfNeeded` is unchanged
- `CreateCardView` restores highlights only in `CardSelection`

## 9. Manual Unity Editor checks

ParrelSync host and client, `m_debugMode` on (it's on in `GameMasterPrefab`). Walking off the platform edge into the lava is an easy way to die at a chosen time.
1. **M1, action turn:** in your own turn, wait until about 20 s, walk off the edge, and confirm a respawn block after about 10 s. `PlayerAction` resumes for you at about 20 s.
2. **Pause in RoundBreak:** when you're **second** in the turn order, walk off the edge and press End turn immediately, so you hit the lava during `RoundBreak`.
   - `RespawnWait` starts at once.
   - After the respawn, `RoundBreak` continues from the paused time.
   - Your mana grows only once this round.
3. **Pause in CardSelection (temporary, don't commit):** with the delay from check 2, or a temporary debug key that calls `ServerKillPlayerByHazard` on the host, kill a player during `CardSelection` after the other player has confirmed and you've highlighted 1–2 cards.
   - After the respawn: same timer, same highlights, and the other player's confirmation is kept.
   - Confirm and check the right cards reach the board.
4. **Decisive:** repeat check 2 on your **last** life. The result screen appears on both sides as soon as you hit the lava, with no `RespawnWait`.
5. **No blocks left (temporary):** destroy all of one player's blocks with repeated Fireballs (or temporarily lower block HP), then kill that player. The match ends with no `RespawnWait`.
6. **No deaths:** play two rounds and compare the timing with the previous build. It should be the same.
7. **H6 during a pause:** close the dead player's window during `RespawnWait`. The host gets the forfeit victory.

## 10. Open questions and assumptions

- **A1 (owner decision):** respawn conditions are checked first, and only a possible respawn pauses the phase.
- **A2:** The pause stops the shared phase timer for both players. That's needed anyway, because the timer is shared.
- **A3:** During the pause nobody can select cards, move or cast: every `Can*` gate requires the paused phase value.
