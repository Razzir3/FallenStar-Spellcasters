# TASK-F4_F6_N36: Game loop: a visible draw phase, losing all blocks loses the match, and phase timers that follow the countdown

- **Source:** [Issue Report #14](../Reports/2026-10-24_IssueReport-14.md): F4 and F6 (poll *Priorities*: Now), N36 (poll P10 = b, verdict raised to Confirmed in §3), N60 in `GameMaster.Player.cs` (poll P4 = b).
- **Severity:** ⚪ **N36** (Confirmed, cosmetic). F4 and F6 are features.
- **Baseline:** `ff8a9dd`, plus TASK-N59_N61_F1 if it's done first.
  - **Do this task second.** It shares `GameMaster.cs` (another region) and the `UI_table` tables with that task.
  - Line numbers below are from `ff8a9dd`.
- **Implementer:** Codex
- **Scope:** server-side loop code, one result-screen script, two localization keys. No scene or prefab edit.

## 1. Objective

1. **F4:** while cards are dealt, the phase is `GamePhase.DrawCards`. The announcer shows "Drawing cards" / "Добор карт" / "カードドロー" with its countdown, instead of "Round preparation".
2. **F6:** when a player's **last block** is destroyed, that player is out at once and the match ends.
   - The winner sees "All your opponent's blocks were destroyed."
   - The loser sees "All your blocks were destroyed."
3. **N36:** every timed phase ends when the replicated countdown reaches zero, not after a separately counted frame time. Clients no longer see "00" while the old phase goes on.

## 2. Current behavior

### 2.1 F4: dealing happens under "Round preparation"

- `GameLoop` (`InGame/GameMaster/GameMaster.GameLoop.cs:101-130`) runs `RoundBreakPhase` → `DrawCardPhase` → `CardSelectionPhase` → `PlayerTurnPhase`.
- `RoundBreakPhase` sets `GamePhase.RoundBreak` (`:382`). **`DrawCardPhase` (`:483-523`) never sets a phase,** so `RoundBreak` stays while 2 × 2 cards are dealt, 0.8 s apart.
- The announcer already maps `DrawCards` to `Phase.DrawCards` (`UI/GameAnnouncerUI.cs:150`), and the key exists in en / ru / ja.
- **Readers of `DrawCards` today:**
  - `PlayerUI.UpdateCards` treats it like `RoundBreak`: it shows the hand (`UI/PlayerUI.cs:312-325`).
  - `HandleConfirmTurn` ignores a confirm in it (default branch).
  - `IsConfirmForCurrentPhase` returns false.
  - `LeaveMatchUI` and `HandlePlayerSurrender` treat every phase but `None` / `GameOver` as in-match.

### 2.2 F6: losing all blocks isn't a rule

- **A Fireball** (50 damage, block HP 100) lowers `m_blockHP`. At 0, `UpdateBlockHp` (`InGame/GameMaster/GameMaster.Processing.cs:328-348`) removes the block from the owner's 5×5 grid (`PlayerSession.DeleteBlock`) and despawns it.
- **Nothing checks how many blocks are left.** The match ends only through lives:
  - `IsGameFinished` (`GameMaster.GameLoop.cs:725-737`) counts players with a character or `m_lifeCurrent > 0`.
  - `HandleDeathPausingCurrentPhase` (`:527-558`) sets `m_lifeCurrent = -1` when a dead player has no block left to respawn on.
- **So with no blocks left,** a player loses only after their pony falls into the lava and dies, and the result reads "lost all their lives".
- `GameLoop` always ends with `EndMatch(winner, MatchEndReason.LivesDepleted)` (`:129`).
- **The result screen** (`UI/ResultMenuUI.cs:39-60`) maps `LivesDepleted`, `OpponentDisconnected` and `Surrendered`.

### 2.3 N36: two clocks

- **The countdown clients see:**
  - `StartPhaseCountdown` (`InGame/GameMaster.cs:121-125`) writes `m_phaseEndServerTime = ServerTime + (duration − alreadyElapsed)`.
  - Clients show `CeilToInt(m_phaseEndServerTime − ServerTime)` (`:132-138`).
- **The loop doesn't use it.** Each timed phase counts its own `m_phaseElapsed += Time.deltaTime` against the duration:
  - RoundBreak: `GameMaster.GameLoop.cs:393-406`
  - CardSelection: `:457-474`
  - PlayerAction: `:177-180`, `:334`
  - RespawnWait: `:605-616`
  - DrawCards: `:487-518`, per-draw delays
- **The mismatch (Confirmed from the code):**
  - NGO advances `ServerTime` with `Time.unscaledDeltaTime` (NGO `Timing/NetworkTimeSystem.cs:161`).
  - `Time.deltaTime` is capped at `Maximum Allowed Timestep` = 0.333 s (`ProjectSettings/TimeManager.asset`).
  - **Any frame longer than 0.333 s** puts the loop behind the countdown by the excess. A 1-second stall gives 0.67 s of "00". Typical causes: the 84-card deck spawn at match start, a scene load, the Editor, two Editors on one PC.
  - **DrawCards also ends late every round:** each per-draw wait starts one frame after the draw, so the phase takes about 4 frames longer than its 3.2 s countdown.
- **Pause and resume:**
  - `HandleDeathPausingCurrentPhase` saves `m_phaseElapsed` / `m_phaseDuration` (`:545-546`).
  - It restores them with `StartPhaseCountdown(_pausedDuration, _pausedTimer)` (`:555-556`).
  - `ResetCardSelectionTimer` (`:414-430`) zeroes `m_phaseElapsed` and restarts the countdown.

### 2.4 N60 leftover

- **`GameMaster.Player.cs:120-139`:** a commented-out `GetCharacterByID` with a `//todo`.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/GameMaster.cs` | `MatchEndReason.BlocksDestroyed`; `m_finishReason`; the countdown helpers; remove `m_phaseElapsed` / `m_phaseDuration` |
| `Assets/Scripts/InGame/GameMaster/GameMaster.GameLoop.cs` | All timed loops use the deadline; `DrawCardPhase` sets `DrawCards` and draws on schedule; pause/resume by time left; `EndMatch(winner, m_finishReason)` |
| `Assets/Scripts/InGame/GameMaster/GameMaster.Processing.cs` | `UpdateBlockHp` checks for the last block; `UpdatePlayerHP` uses a shared character-removal helper |
| `Assets/Scripts/InGame/GameMaster/GameMaster.Player.cs` | `EliminatePlayerByBlocks`, `CountSpawnedBlocks`, `RemoveCharacter`; N60: delete the commented block |
| `Assets/Scripts/UI/ResultMenuUI.cs` | Two reason fields with initializers; the `BlocksDestroyed` case |
| `Assets/Localization/UI_table Shared Data.asset`, `UI_table_en/ru/ja.asset` | 2 new keys (§4.4) |
| `Docs/AI/Architecture/GameLoop.md`, `Architecture.md`, `Dependencies.md` | §4.6 |

**Dependencies:**
- **`MatchEndReason` is an RPC argument** (`MatchEndedClientRpc`). Append the new value at the end, so existing values keep their numbers. Both peers on the same build (RP12).
- **`GamePhase` (RP1):** no new value. `DrawCards` already exists and has its keys.
- **`m_phaseEndServerTime`** is read by `GameAnnouncerUI` through `GetPhaseSecondsLeft()`. Keep both names and behaviors.
- **`m_lifeCurrent = -1`** is the existing "out of the match" marker, used by:
  - `HandleDeathPausingCurrentPhase` / `WaitForRespawnIfNeeded`
  - `PlayerUI` ("Player Dead")
  - `LavaKillZone` / `ServerKillPlayerByHazard` (ignore lives ≤ 0)
  - `RespawnCharacter` (lives ≤ 0 → no respawn)

## 4. Required changes

### 4.1 N36: one clock

In `GameMaster.cs`:

```csharp
private void StartPhaseCountdown(float duration)
{
    m_phaseEndServerTime.Value = NetworkManager.ServerTime.Time + Mathf.Max(0f, duration);
}

// Server-side: seconds left on the replicated countdown (can be negative).
private double PhaseTimeLeft() => m_phaseEndServerTime.Value - NetworkManager.ServerTime.Time;
```

- **Delete** `m_phaseElapsed`, `m_phaseDuration` and the `alreadyElapsed` parameter, and every line that uses them, including `StartNewGame`'s `m_phaseElapsed = 0f;` (`:565`).
- **Unchanged:** `ClearPhaseCountdown`, `GetPhaseSecondsLeft` and the NetworkVariable.

In `GameMaster.GameLoop.cs`:

| Loop | New condition | Remove |
|---|---|---|
| `RoundBreakPhase` | `StartPhaseCountdown(m_roundBreakTime); while (PhaseTimeLeft() > 0) { … }` | `m_phaseElapsed = 0f;`, `+= Time.deltaTime` |
| `CardSelectionPhase` | same with `m_cardSelectionTime`; the all-confirmed `break` stays | same |
| `PlayerActionPhase` | same with `m_playerActionTime`; the card-queue loop and the confirmed `break` stay | same |
| `WaitForRespawnIfNeeded` (inner wait) | `StartPhaseCountdown(m_respawnWaitTime); while (PhaseTimeLeft() > 0) { if (m_respawnConfirmed) break; yield return null; }` | same |
| `ResetCardSelectionTimer` | `StartPhaseCountdown(m_cardSelectionTime);` | `m_phaseElapsed = 0f;` |
| `HandleDeathPausingCurrentPhase` | before the wait: `float _pausedTimeLeft = (float)System.Math.Max(0d, PhaseTimeLeft());` — after: `StartPhaseCountdown(_pausedTimeLeft);` | `_pausedTimer`, `_pausedDuration` |

- **Keep each loop's other checks and their order:** `IsGameFinished`, the death check and the confirm checks. Only the time source changes.
- **The checks are made once per frame,** so a phase ends on the first frame at or past its deadline.

### 4.2 F4: the draw phase, on the same clock

Replace `DrawCardPhase`'s body (`:483-523`). Keep its debug logs.

```csharp
m_gamePhase.Value = GamePhase.DrawCards;
int _drawCount = m_startHandCards * m_players.Count;
StartPhaseCountdown(_drawCount * m_cardDrawDelay);
int _drawn = 0;

while (true)
{
    if (IsGameFinished()) yield break;
    if (m_externalDeathDetected || IsAnyPlayerDead())
    {
        yield return HandleDeathPausingCurrentPhase();
        if (IsGameFinished()) yield break;
    }

    // Draw i is due once no more than (count - i) delays are left on the countdown.
    while (_drawn < _drawCount && PhaseTimeLeft() <= (_drawCount - _drawn) * m_cardDrawDelay)
    {
        DrawCard(m_players[_drawn % m_players.Count]);
        _drawn++;
    }

    if (_drawn >= _drawCount && PhaseTimeLeft() <= 0)
        break;

    yield return null;
}
```

- **Same schedule as today:**
  - draws at 0, d, 2d and 3d
  - the end at 4d (d = `m_cardDrawDelay`, 0.8 s)
  - the order player 0, 1, 0, 1
- **A death pause** resumes with the remaining time (§4.1), so the remaining draws keep their spacing.
- **Edge cases:** with `m_players.Count == 0` or a delay of 0 it ends at once. A long hitch can deal the overdue cards in one frame.
- **`RoundEnd` stays unused, and `ResolveCards` stays transient.** No other phase change.

### 4.3 F6: losing the last block

1. **`MatchEndReason`:** append `BlocksDestroyed` after `Surrendered`.
2. **`GameMaster`:** add `private MatchEndReason m_finishReason = MatchEndReason.LivesDepleted;` and reset it in `StartNewGame`.
3. **`GameLoop`:** the final call becomes `EndMatch(winner, m_finishReason);` (`:129`).
4. **`GameMaster.Player.cs`** (new, server only):

   ```csharp
   private int CountSpawnedBlocks(PlayerSession session) { /* non-null, spawned blocks in session.GetSessionBlock() */ }

   // Shared with UpdatePlayerHP: despawn the character and free its spawn point.
   private void RemoveCharacter(PlayerSession session)
   {
       DespawnCharacter(session);
       SpawnPoint _spawnPoint = session.GetSpawnPoint();
       if (_spawnPoint != null) _spawnPoint.IsOccupied = false;
       session.SetCharacter(null);
       session.CharacterObjectId.Value = 0;
   }

   private void EliminatePlayerByBlocks(PlayerSession session)
   {
       if (!IsServer || session == null || session.m_lifeCurrent.Value < 0) return;
       m_finishReason = MatchEndReason.BlocksDestroyed;
       session.m_lifeCurrent.Value = -1;
       if (session.GetCharacter() != null) RemoveCharacter(session);
       if (m_debugMode) Debug.Log($"GameMaster: client {session.OwnerClientId} lost all blocks.");
   }
   ```

   - **`UpdatePlayerHP`** (`Processing.cs:304-327`): its death branch keeps `m_lifeCurrent.Value -= 1` and then calls `RemoveCharacter(_session)` instead of the four inline steps. Same behavior.
5. **`UpdateBlockHp`** (`Processing.cs:328-348`): after `DeleteBlock` and the despawn, add:

   ```csharp
   if (_session != null && CountSpawnedBlocks(_session) == 0) EliminatePlayerByBlocks(_session);
   ```

6. **The loop needs nothing more.** `PlayerActionPhase` checks `IsGameFinished()` right after each `ProcessActivity` (`:311`), and every other loop checks it each frame. The eliminated player has no character and lives −1, so `IsGameFinished` is true, `GameLoop` exits, and the other player is the winner.
7. **`ResultMenuUI`:**
   - **Add two fields:**

     ```csharp
     [SerializeField] private LocalizedString m_reasonOpponentBlocksDestroyed = new LocalizedString("UI_table", "result_reason_opponentBlocksDestroyed");
     [SerializeField] private LocalizedString m_reasonYouBlocksDestroyed = new LocalizedString("UI_table", "result_reason_youBlocksDestroyed");
     ```

   - **Add the case:** `BlocksDestroyed`. The winner gets the opponent text and the loser the "you" text. A draw (impossible today: one card resolves at a time) uses `m_reasonBothLostLives`.

### 4.4 Localization: 2 new keys

| Key | en | ru | ja |
|---|---|---|---|
| `result_reason_opponentBlocksDestroyed` | All your opponent's blocks were destroyed. | Все блоки соперника разрушены. | 相手のブロックがすべて破壊されました。 |
| `result_reason_youBlocksDestroyed` | All your blocks were destroyed. | Все ваши блоки разрушены. | あなたのブロックがすべて破壊されました。 |

### 4.5 N60 cleanup

- Delete the commented-out `GetCharacterByID` block and its `//todo` (`GameMaster.Player.cs:120-139`).

### 4.6 Docs

- **`GameLoop.md`:**
  - §3 phase sequence: `DrawCards` is set.
  - §3.1 Countdown: the loops use `PhaseTimeLeft()`; `m_phaseElapsed` / `m_phaseDuration` are gone.
  - The death pause: time left.
  - Match end: `BlocksDestroyed` and when it's set.
  - Remove "Destroying all blocks doesn't end the match … [Unfinished]" and "`DrawCards` … never assigned".
- **`Architecture.md`:**
  - §5: the loop-time line
  - §7 table: a "Last block destroyed" row
  - §11: remove the two items from *Unfinished*
- **`Dependencies.md`:**
  - RP1: `DrawCards` is now set
  - a new **RP20:** phase timing must use the replicated deadline; a new timed phase calls `StartPhaseCountdown` and loops on `PhaseTimeLeft()`

## 5. Explicit non-goals

- **No change to durations, mana, draw counts or the hand limit.** `RoundEnd` stays unused, and `ResolveCards` stays transient.
- **No change to the client countdown display** (`GetPhaseSecondsLeft`, `GameAnnouncerUI`), or to how clients estimate `ServerTime`.
- **No new phase key or announcer text.** `Phase.DrawCards` exists.
- **No change to block damage, block healing or overheal (H3),** or to the respawn rules.
- **No "blocks left" HUD counter** and no message when a block is destroyed.
- **No movement limit per turn** (the owner's "Later" idea).
- **N60's other items** belong to other tasks.

## 6. Constraints and compatibility requirements

- **Server authority is unchanged.** Every new check runs on the server, where the old ones did.
- **RP12:** `MatchEndReason` gets an appended value; both peers on the same build. No RPC is added or removed.
- **Keep `m_phaseEndServerTime`'s name and type.** Clients and the announcer read it.
- **Keep serialized field names.** The two new `ResultMenuUI` fields use initializers, so `ResultScene` needs no edit.
- **Unity 6 APIs, no warnings of our own.**
  - Keep baseline copies in `build/f4-f6-n36-verification/baseline/`.
  - Restore the dynamic font before handing over (`AGENTS.md`).

## 7. Acceptance criteria

1. **N36, the clock:**
   - **The stall test:** on the host, inject a single 1.0 s frame stall 1 s into a 5 s RoundBreak.
   - **After the fix,** the phase changes on the first frame whose `ServerTime` is at or past `m_phaseEndServerTime`: no more than one frame late.
   - **On the baseline,** the same run ends about 0.67 s late. Measure it and report the number.
   - Also measure this in CardSelection, PlayerAction and RespawnWait.
2. **N36, pause and resume:** a death 20 s into a 60 s PlayerAction pauses it. After the respawn, the countdown resumes at 40 s (±1 frame), and the phase ends when it reaches 0.
3. **N36, timer reset:** a rejected selection (mana over budget) restarts the selection countdown at 60 s, as today, within the per-phase and per-match limits.
4. **F4:**
   - The phases run RoundBreak → **DrawCards** → CardSelection.
   - DrawCards lasts 3.2 s (±1 frame) and deals 4 cards in the order 0, 1, 0, 1.
   - The announcer shows `Phase.DrawCards` with its countdown in en / ru / ja.
   - The hand row shows the hand during DrawCards.
   - Confirm Turn during DrawCards is ignored, as in RoundBreak.
5. **F4, death during dealing:** a death after 2 draws pauses the phase. After the respawn, the last 2 draws come 0.8 s apart and the phase ends.
6. **F6:**
   - Destroying 24 of a player's 25 blocks changes nothing.
   - Destroying the 25th sets that player's lives to −1 and despawns their pony.
   - The match ends with `EndMatch(other player, BlocksDestroyed)`.
   - Both peers' `MatchResultData.Reason` is `BlocksDestroyed`.
   - The Result screen shows the two new texts, the right one on each side, in en / ru / ja.
7. **F6, no regression:**
   - A normal end by lives still gives `LivesDepleted` and the old texts.
   - A pony dying with blocks left still respawns.
   - `m_finishReason` is back to `LivesDepleted` in the next match on the same host.
8. **Build:** 0 errors, no warnings of our own. The release player builds.

## 8. Regression tests

- **A Play Mode harness** (temporary, in `Temp/F4F6N36Verification/`) with the host fixture used for N55 / N57: the real `GameMasterPrefab`, two synthetic sessions, blocks, ponies and the deck.
  - **Inject the stall** with a test component: `System.Threading.Thread.Sleep(1000)` in one `Update`.
  - **Read private state** through reflection where needed. Don't add test hooks to the game code.
- **Baseline first:** run AC1 on the baseline code and record the late end. That turns N36 from "seen in the code" into "measured", as N54 was.
- **Result texts (AC6, AC7):** load `ResultScene` with each `MatchResultData` combination and read the reason text in all three locales.
- **The report lists each case with its result.** Remove the harness from `Assets` and `Temp`, and keep a copy in `build/f4-f6-n36-verification/`.

## 9. Manual Unity Editor checks

**Setup:** two Editors, main = host, clone = client.

1. **F4:**
   - In three rounds, both windows show "Round preparation" (5 s), then **"Drawing cards" (about 3 s)** while the hand fills, then "Card Selection".
   - Once in ru and once in ja.
2. **N36:**
   - Watch the countdown at the end of the first Round preparation. Today's most likely moment for "00" is right after the match start loads 84 cards.
   - Then watch it at every phase change for three rounds, in both windows.
   - "00" may flash for a frame at most; it must not stay visible.
3. **N36, the respawn pause:** kill a pony mid-action (Read me → Kill a pony). After the respawn, the action countdown continues from where it stopped.
4. **F6, optional, with a temporary change T1.**
   - **Precondition:** `git status` shows `GameMaster.Spawn.cs` unmodified. This task doesn't edit it.
   - **T1:** in `CreateBlock`, skip all but the centre block (`if (i != 2 || j != 2) continue;` at the top of the inner loop), and use `_blockController.Initialize(session, 50);`.
   - **Then:** start a match. In the host's Player action, cast a Fireball at the client's only block.
   - **Expected:** host "Victory!" / "All your opponent's blocks were destroyed."; client "Defeat" / "All your blocks were destroyed."
   - **Undo:** close both Editors and run `git restore Assets/Scripts/InGame/GameMaster/GameMaster.Spawn.cs`.
5. **A normal match** to the end by lives: the result texts are unchanged.
6. **Logs:** no exception in either Editor log.

## 10. Open questions and assumptions

- **A1, "out at once" on the last block.** Today the player is out only at their next death. Losing the board is now its own result, with its own text. If you'd rather keep the lava death as the only way out, say so before Codex starts.
- **A2, draws on the countdown schedule.** This keeps today's 0.8 s spacing and makes the phase end exactly at "00".
- **A3, client display.** The client's `ServerTime` lags the server's by about one-way latency plus a tick. The phase change arrives about when its countdown reaches 0, so at most a one-tick "00" remains (≈ 33 ms). Nothing changes on the client.
- **A4, F6 is hard to reach in normal play.** 25 blocks × 2 Fireballs is a long match. The harness proves the rule, and T1 shows the screens.
