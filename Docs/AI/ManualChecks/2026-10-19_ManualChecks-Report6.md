# Manual Unity checks after Issue Report #6

- **Date:** 2026-10-19
- **Baseline:** `fe2d4fb`
- **Source:** [Issue Report #6](../Reports/2026-10-19_IssueReport-6.md) §5, step 1. It covers the tasks from this round (H2, H3, N14, N18_M1, N19) and the older backlog (H6, H7, MANA1–3, SEC1, U1).
  - The steps come from §9 of each removed spec. The specs are in git history: `git show fe2d4fb:Docs/AI/Tasks/<file>` for this round's specs, `git show 058fa63:Docs/AI/Tasks/<file>` for MANA1–3 and H10, and `git show f08e854:Docs/AI/Tasks/<file>` for H6, H7 and SEC1.
- **Who runs it:** the developer, in the Unity Editor with the ParrelSync clone. These checks need two running instances, so the AI agents can't run them.
- **How to report:**
  1. Tick each expected result, and fill in the summary table (§G).
  2. For every failure, copy the Console text (error plus stack trace) into the notes.
  3. Tell Claude when you're done. Claude then updates the report and writes fix specs if needed.

**Order:** do §A first. It needs no changes and covers most of the risk. §B uses temporary Inspector and asset changes, and §C uses temporary code edits. Revert both with §F. §D is the player build.

---

## 0. Preparation

1. Open the main project. Open the clone with **ParrelSync → Clones Manager → Open in New Editor**. The clone shares `Assets`, so make every temporary change in the **main** project only.
2. In both Editors, open the Console: turn **Collapse** off and **Clear on Play** on.
3. Start a match:
   1. Log in and go to the Lobby in both windows.
   2. Window 1 presses **Create Lobby**. From now on it's the **host window**.
   3. Window 2 presses **Find Lobby**, selects the lobby and presses **Join**. It's the **client window**.
   4. The host presses **Start game**.
4. `GameMasterPrefab` has `m_debugMode = 1`, so the **host** Console logs every phase:
   - "Round break phase started"
   - "DrawPhase started"
   - "Card selection phase started."
   - "Player Action phase started"
   - "Waiting for Respawn"
   - "GameMaster: match ended …"

### Useful facts

| Topic | Details |
|---|---|
| **Phases on the announcer (en)** | "Round preparation" (5 s). It still shows during card dealing (~3 s), because the game never switches to a draw phase. Then "Card Selection" (60 s), "Turn" (60 s per player) and "Waiting for respawn" (30 s). **The timer counts up** (elapsed seconds). |
| **Turn order** | The player whose selected cards (play **and** discard) cost more goes first. To go **second**, select fewer or cheaper cards than the opponent. |
| **Controls** | Hand cards: **left-click** = play, **right-click** = discard. The **Confirm turn** button confirms the selection in Card Selection, ends your Turn, and confirms the respawn block in Waiting for respawn.<br>To cast during your Turn: left-click a board card → click the target in the world → **Confirm** → **Send**.<br>**W/A/S/D** = one step (only in your own Turn). **Space** = −10 HP to yourself (debug, own Turn only). |
| **Cards** | Iceshard: 2 mana, 20 damage to the opponent's pony. Fireball: 3 mana, 50 damage to an opponent block (2 hits destroy a block). Clarity: 3 mana, draw 2. HealingHooves: 4 mana, heal yourself 30. |
| **Numbers** | Pony HP 100, block HP 100, 3 lives, 25 blocks per player. Mana 2 → 4 → 6 … up to 20. 2 new cards per player per round. |
| **Kill a pony at any moment (no code needed)** | In the **host window's** Scene view, select the pony (`PonyBase(Clone)`) and drag it sideways off its platform, or add about 20 to its Transform Position X or Z in the Inspector. It falls into the lava and dies, in any phase. This only works in the **host** window, because the lava check runs on the server. |
| **Respawn** | In "Waiting for respawn", the dead player clicks one of their own blocks and presses **Confirm turn**. With no choice, a random block is used after 30 s. |
| **Switch language** | Window → Asset Management → **Localization Scene Controls** → Active Locale. The game has no language menu yet (M12). |

Some errors and warnings are already known. They're listed in **§E**; don't record them as new failures.

---

## A. Normal settings (no temporary changes)

### A1. Baseline rounds and mana (N18 no-death case, MANA1, MANA2, MANA3, U12)
Play 3 rounds with no deaths. Check both windows, so the client is a real remote client (U12).
- [ ] Both windows show their own and the opponent's mana as 2/2 → 4/4 → 6/6 at the start of rounds 1–3.
- [ ] After a confirm, current mana goes down by the cost of the **played** cards on both windows.
- [ ] Round 1 (2 mana): a Fireball or Clarity (3) can't be selected to play (no highlight). An Iceshard (2) can.
- [ ] Round 2 (4 mana): HealingHooves alone can be selected. HealingHooves + Iceshard can't.
- [ ] Discarding any number of cards (right-click) is allowed and costs no mana.
- [ ] Timing is as before: ~5 s preparation, ~3 s dealing, selection ends when both confirm or at 60 s.
- [ ] No errors except the ones in §E.

Result: ☐ Pass ☐ Fail. Notes:

### A2. Death in your own Turn: the timer resumes (M1, N18_M1)
In your own Turn, wait until the timer shows about 20. Walk off the platform edge, or press Space 10 times. In "Waiting for respawn", wait about 10 s, then pick a block and press Confirm turn.
- [ ] "Waiting for respawn" starts as soon as the pony dies.
- [ ] After the respawn, **the same player's** Turn continues from about 20 (not from 0, and not from about 10).
- [ ] The pony appears on the chosen block, and lives went from 3 to 2.

Result: ☐ Pass ☐ Fail. Notes:

### A3. Death during "Round preparation" (N18)
During "Round preparation", kill a pony through the host Scene view.
- [ ] "Waiting for respawn" starts at once. It doesn't wait for the next Turn.
- [ ] After the respawn, "Round preparation" continues from the time it had reached, then cards are dealt, then Card Selection starts.
- [ ] Mana grew **only once** this round. For example, round 2 shows 4/4, not 6/6.

*Without the Scene view trick:* as the **second** player, walk off the edge and press Confirm turn immediately, so the fall ends after your Turn.

Result: ☐ Pass ☐ Fail. Notes:

### A4. Death while cards are dealt (N18)
Right after "Round preparation" reaches 5, the cards are dealt (~3 s; the host Console shows "DrawPhase started"). Kill a pony in that window. *Optional:* to widen the window, temporarily set `GameMasterPrefab` → `m_cardDrawDelay` = 3, then revert it (§F).
- [ ] "Waiting for respawn" starts at once.
- [ ] After the respawn, the remaining cards are dealt. Each player gets exactly 2 new cards this round: none twice, none skipped.

Result: ☐ Pass ☐ Fail. Notes:

### A5. Death during Card Selection: selections are kept (N18_M1)
1. In Card Selection, player A selects cards and presses **Confirm turn**.
2. Player B highlights 1 play card (left-click) and 1 discard card (right-click), but doesn't confirm.
3. Note the timer, then kill either pony.
4. Respawn.

- [ ] Card Selection continues from the noted time.
- [ ] B's two highlights are back, in their play and discard colors.
- [ ] A doesn't need to confirm again: the phase ends when B confirms.
- [ ] After B confirms, exactly the highlighted cards appear on B's board.

Result: ☐ Pass ☐ Fail. Notes:

### A6. A last-life death ends the match at once (N18)
Bring one player down to 1 life with two earlier deaths (A2–A5 count). Then kill them again, once in "Round preparation" or Card Selection. If you play more matches, also try a Turn.
- [ ] There's **no** "Waiting for respawn".
- [ ] Both windows go to the result screen right away, with the right titles and reasons ("You won" / "You lost" plus the lives reason).

Result: ☐ Pass ☐ Fail. Notes:

### A7. Leave match: the client, during Card Selection (N19)
The client clicks **Leave match** → **Cancel**. Then **Leave match** → **Leave**.
- [ ] Cancel closes the dialog and nothing else changes.
- [ ] After Leave, the client sees "You lost" + "You left the match.", and the host sees "You won" + "Your opponent left the match."
- [ ] Press **Main menu** on both windows, create a new lobby and start a new match. It works normally.

Result: ☐ Pass ☐ Fail. Notes:

### A8. Leave match at other moments (N19)
Run one match for each case:
- (a) the host during its own Turn
- (b) either player during the opponent's Turn
- (c) during "Waiting for respawn", once by the dead player and once by the other player
- (d) double-click **Leave** quickly

- [ ] Every case gives one result on both windows. The leaver loses, and there are no errors.
- [ ] (d) gives only one result.

Result: ☐ Pass ☐ Fail. Notes:

### A9. The dialog blocks clicks behind it (N19)
Open the Leave dialog. Click hand cards, board cards, blocks and ponies through the dimmed background.
- [ ] Nothing gets selected or targeted. After Cancel the game continues normally.
- [ ] In the Game view at 16:9 and one other aspect ratio (e.g. 16:10 or 4:3), the Leave button doesn't cover the hand, the mana labels or the announcer.

Result: ☐ Pass ☐ Fail. Notes:

### A10. Languages (N19, MANA3, H10)
During a match, with the Leave dialog open, switch the locale to **ru**, then **ja**, then back to **en**. Do the same on a result screen.
- [ ] The Leave button, dialog text, **Leave** and **Cancel** are translated and fit, with no clipping and no □ boxes.
- [ ] The mana labels are translated.
- [ ] The result title and reason are translated, including "You left the match."

Result: ☐ Pass ☐ Fail. Notes:

### A11. A disconnect counts as a forfeit (H6, U10)
In separate matches, stop Play Mode in the **client** window during:
- (a) "Round preparation"
- (b) Card Selection
- (c) the host's Turn
- (d) the client's Turn
- (e) the client's "Waiting for respawn" (kill the client pony first)

Extra cases:
- (f) end the clone Editor's process in Task Manager instead of stopping Play Mode
- (g) the client leaves in the **lobby**, before the match starts

- [ ] (a)–(e): the host gets "You won" + "Your opponent left the match." within a few seconds.
- [ ] (f): the same result, but only after about 30 s (the transport timeout; expected).
- [ ] (g): no result screen, no errors.
- [ ] There are no errors on the host.

Result: ☐ Pass ☐ Fail. Notes:

### A12. Nothing is left over after a match (H7, U8)
After any result screen, press **Main menu** on both windows. Stay in Play Mode and check the Hierarchy, including **DontDestroyOnLoad**.
- [ ] No gameplay objects remain: no `PonyBase(Clone)`, `BlockBody(Clone)`, `CardPrefab(Clone)`, PlayerSession objects or GameMaster. Only the app managers stay.
- [ ] A new lobby and match afterwards work, and nothing from the previous match is left.

Result: ☐ Pass ☐ Fail. Notes:

### A13. An honest full match has no suspicious-activity warnings (SEC1, MANA2)
Play a full match to a normal end, then search the host Console for `[Suspicious]`.
- [ ] 0 results.

Result: ☐ Pass ☐ Fail. Notes:

---

## B. Temporary Inspector and asset changes (revert with §F)

### B1. The deck refills when it runs out (H2)
**Setup:** in `Assets/Prefab/GameMasterPrefab.prefab`, set **GameRules → `m_startHandCards` = 20** and **Timing → `m_cardDrawDelay` = 0.05**.
Each round now deals 40 cards, so the 80-card deck is **exactly empty after the round 2 deal**.
1. In rounds 1–2, discard about 10 cards (right-click, then Confirm turn), so the graveyard isn't empty.
2. In **round 2's Turn**, cast **Clarity** (draw 2). The deck is empty at this point, so the refill happens during the cast. If nobody casts Clarity, the refill happens during the round 3 deal.
3. Play on into round 3 or 4.

- [ ] The host Console shows "Deck refilled: N cards." with N > 0. There's no exception and no "match aborted".
- [ ] The Clarity cast in round 2 gives 2 cards with no error.
- [ ] Both players keep getting cards. Each player sees their own new cards face up; the opponent's stay hidden.
- [ ] *Optional:* compare a search for `CardPrefab(Clone)` in the host Hierarchy before and after the refill. The discarded cards' objects are gone, and the count doesn't grow by the whole old graveyard.

Result: ☐ Pass ☐ Fail. Notes:

### B2. Refill decks leave out the rarest cards (H2)
**Setup:** keep B1's settings, and also set `Assets/Card/Data/Clarity.asset` → **Rarity = UltraRare**. Run B1 again.
- [ ] Before the refill, Clarity can appear, at most 2 copies (the UltraRare limit).
- [ ] After "Deck refilled", **no newly dealt card is Clarity**. An old Clarity that was already in a hand may still be there.

Result: ☐ Pass ☐ Fail. Notes:

### B3 + B4. Block heal over the cap (H3) and the mana cap (MANA1), in one match
**Setup:**
- `Assets/Card/Data/HealingHooves.asset` → Activities → Element 0 → **Target Type: SelfPlayer → SelfBlock**
- `GameMasterPrefab` → GameRules → **`m_maxMana` = 5**

1. From round 2 on: cast HealingHooves on one of your **full** blocks. Select the card, click your block, then Confirm and Send.
2. The opponent Fireballs one of your blocks (100 → 50), and you heal that block.
3. Read the block's HP in the Inspector on the host and the client: `BlockBody(Clone)` → `BlockController` → `m_blockHP`.

- [ ] A full block goes from 100 to **115**.
- [ ] A damaged block goes from 50 to **80**.
- [ ] *Optional:* a block at 80 goes to **105**.
- [ ] There's no exception and no "match aborted".
- [ ] Mana goes 2 → 4 → **5 → 5** (the cap).

Result: ☐ Pass ☐ Fail. Notes:

---

## C. Temporary code changes (don't commit; revert with §F)

### C1. The host returns to the lobby when a match can't start (N14)
**Edit:** in `Assets/Scripts/MultiplayerScripts/MultiplayerManager.cs`, add this as the **first line** inside `StartGameAfterSceneReady()` (line 1243):
```csharp
yield return new WaitForSeconds(5f);
```
Then run three cases:
- (a) Start a match. Within 5 s after `GameScene` loads, stop Play Mode in the **client** window.
- (b) Same, but stop the **host**.
- (c) After (a), create a new lobby and play one full round.

- [ ] (a) The host Console shows "match start aborted. Reason: player_left_before_start". The host is back in `LobbyScene` within about 1–2 s. The old lobby no longer appears in **Find Lobby**.
- [ ] (b) The client returns to `LobbyScene`.
- [ ] (c) The new match works normally.
- [ ] After reverting the edit, close the client during Card Selection. The host gets the **forfeit result** (as in A11), not the lobby.

Result: ☐ Pass ☐ Fail. Notes:

### C2. A death with no blocks left ends the match at once (N18_M1)
**Edit:** in `Assets/Scripts/InGame/GameMaster/GameMaster.Spawn.cs:252-254`, change
`for (int i = 0; i < 5; i++)` → `for (int i = 2; i < 3; i++)` and
`for (int j = 0; j < 5; j++)` → `for (int j = 2; j < 3; j++)`.
Each player now gets only the **center block**, under their pony. *Optional:* set `m_startHandCards` = 6 to get Fireballs sooner.
1. Cast **Fireball twice** on the opponent's only block: twice in round 3 (6 mana), or once in round 2 and once in round 3 (block HP carries over).
2. The block is destroyed, and the opponent's pony falls into the lava with 2 lives left.

- [ ] There's **no** "Waiting for respawn". Both windows go straight to the result screen, and the player without blocks loses.

Result: ☐ Pass ☐ Fail. Notes:

### C3. Leave match fallback when the server doesn't answer (N19)
**Edit:** in `Assets/Scripts/InGame/PlayerSession.cs:167`, comment out `GameMaster.Instance.HandlePlayerSurrender(this);`.
The **client** clicks **Leave match** → **Leave**.
- [ ] About 5 s later, the client goes to the lobby (fallback disconnect).
- [ ] The host gets "You won" + "Your opponent left the match." (the H6 forfeit).

Result: ☐ Pass ☐ Fail. Notes:

### C4. *(Optional)* Tamper test: over-budget selection (MANA2, SEC1)
**Edit (client-side gate):** in `Assets/Scripts/InGame/PlayerSession.cs:268-269`, comment out the two lines
`if (SelectedPlayCost() + card.m_cardData.m_cost > m_manaCurrent.Value) return false;`.
In the **client** window, select a play set that costs more than your mana and press Confirm turn.
- [ ] The host Console shows `[Suspicious] … ManaOverBudget`.
- [ ] The client's highlights are cleared, and "Not enough mana" appears on the client only.
- [ ] The selection timer jumps back to 0.
- [ ] Confirming a valid set afterwards works.
- [ ] A second over-budget confirm in the **same** phase is rejected **without** a timer reset.
- [ ] Do one over-budget confirm in each of the next rounds. The timer resets in the first 3 phases where you tamper (3 per match), and **not** in the 4th.
- [ ] With debug mode on, the match-end summary lists counts for the tampering client only.

Result: ☐ Pass ☐ Fail ☐ Not run. Notes:

---

## D. Player build (U1)

1. **File → Build Profiles → Windows → Build**. Choose a folder outside `Assets`, e.g. `build/manual-2026-10-19/`.
2. *Optional:* run the `.exe` as the client, join a lobby hosted from the Editor, and play one round. In a non-development build, the Space debug key does nothing (expected).

- [ ] The build finishes with 0 errors.
- [ ] *Optional:* the client round works.

Result: ☐ Pass ☐ Fail. Notes:

---

## E. Known issues you may see (already in Report #6, not new failures)

| You see | ID |
|---|---|
| After a respawn pause in someone's Turn, pressing **Send** throws a `NullReferenceException` in `PlayerSession.CheckPackage`. Select the card and **Confirm** the target again, then Send works. | N20 |
| Cards you selected but didn't confirm last round are highlighted at the start of the next Card Selection | N26 |
| The warning "Invalid mane material index." on every pony spawn | N27 |
| `NullReferenceException` in `LobbyService.GetLobbyAsync` / `RefreshCurrentLobby` when the match starts (the match still starts) | M10 |
| A selection confirm pressed at the very last moment ends your Turn instead, or is lost if a respawn pause starts | N21 |
| The host's HUD goes blank for a moment before the result screen after a forfeit | N17 |
| NGO `NetworkSceneManager.Dispose` NRE when you quit the Editor | U9 |
| Compiler warnings CS0114 (`PlayerUI.OnDestroy`) and CS0414 (`m_cardResolveDelay`) | N1, N4 |
| "Round preparation" still shows while cards are dealt | feature gap |

---

## F. Revert checklist (after §B and §C)

- [ ] `GameMasterPrefab`: `m_startHandCards` 2, `m_cardDrawDelay` 0.8, `m_maxMana` 20. Simplest: `git restore Assets/Prefab/GameMasterPrefab.prefab`.
- [ ] `Clarity.asset` Rarity back to **Common**; `HealingHooves.asset` Target Type back to **SelfPlayer**. Or `git restore Assets/Card/Data/Clarity.asset Assets/Card/Data/HealingHooves.asset`.
- [ ] Temporary code edits removed from `MultiplayerManager.cs`, `GameMaster.Spawn.cs` and `PlayerSession.cs`. Or `git restore` those three files, **only if** they have no other uncommitted work.
- [ ] `git status` shows no changes to any of these files.
- [ ] The clone shares `Assets`, so reverting in the main project reverts the clone too. Let both Editors recompile.

---

## G. Results summary

| Check | Covers | Result (Pass / Fail / Not run) | Notes |
|---|---|---|---|
| A1 Baseline rounds and mana | N18, MANA1–3, U12 | | |
| A2 Death in own Turn | M1, N18_M1 | | |
| A3 Death in Round preparation | N18 | | |
| A4 Death while cards are dealt | N18 | | |
| A5 Death in Card Selection | N18_M1 | | |
| A6 Last-life death | N18 | | |
| A7 Leave: client, Card Selection | N19 | | |
| A8 Leave: other moments | N19 | | |
| A9 Dialog blocks clicks | N19 | | |
| A10 Languages | N19, MANA3, H10 | | |
| A11 Disconnect forfeit | H6, U10 | | |
| A12 No leftovers | H7, U8 | | |
| A13 No `[Suspicious]` in an honest match | SEC1, MANA2 | | |
| B1 Deck refill | H2 | | |
| B2 Rarity filter | H2 | | |
| B3 + B4 Block heal, mana cap | H3, MANA1 | | |
| C1 Match can't start | N14 | | |
| C2 No blocks left | N18_M1 | | |
| C3 Leave fallback | N19 | | |
| C4 Tamper (optional) | MANA2, SEC1 | | |
| D Player build | U1 | | |
