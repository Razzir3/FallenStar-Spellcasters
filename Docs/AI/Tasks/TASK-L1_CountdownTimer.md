# TASK-L1: The HUD shows a countdown, and the timer isn't sent every frame

- **Source issue:** [Issue Report #7 → L1](../Reports/2026-10-20_IssueReport-7.md#-low-23)
- **Severity:** 🟢 Low (Confirmed)
- **Baseline:** `b7e7b91`. Line numbers below are from this commit.
- **Order:** 11 of 20. After TASK-N6, TASK-L3, TASK-N3 and TASK-N26, which edit the same files. Before TASK-L6, which also edits `GameAnnouncerUI`.
- **Owner decision (2026-10-20):** **countdown.** Show the seconds left in the phase. The server sends the phase end once, not a value every frame.
- **Implementer:** Codex

## 1. Objective

- The announcer shows how many seconds are **left** in the current phase. It counts down to 0.
- The server writes one replicated value when a countdown starts, restarts or resumes. It no longer writes one every frame.
- Phase durations and the server's own timing are unchanged.

## 2. Current behavior

- `public NetworkVariable<float> m_timer;` (`InGame/GameMaster.cs:120`) is the only timer.
- **Server writes** (`InGame/GameMaster/GameMaster.GameLoop.cs`):
  - every phase loop sets it to 0 and adds `Time.deltaTime` **every frame** until the phase duration is reached:
    - `PlayerActionPhase` `:175-328` (`m_playerActionTime`, 60 s)
    - `RoundBreakPhase` `:387-398` (`m_roundBreakTime`, 5 s)
    - `CardSelectionPhase` `:515-530` (`m_cardSelectionTime`, 60 s)
    - `WaitForRespawnIfNeeded` `:677-686` (`m_respawnWaitTime`, 30 s)
  - `DrawCardPhase` `:543`, `:573`: 0, then `+= m_cardDrawDelay` per draw. The phase stays `RoundBreak` during the deal (a known feature gap).
  - `HandleDeathPausingCurrentPhase` `:599`, `:608`: saves the value and restores it after a respawn pause
  - `ResetCardSelectionTimer` `:488`: sets 0 (a selection-timer reset, limited per phase and per match)
  - `StartNewGame` (`GameMaster.cs:537`): sets 0
- Every write marks the variable dirty, so it's sent to clients on every network tick for the whole match.
- **Only reader:** `UI/GameAnnouncerUI.cs:56` `((int)GameMaster.Instance.m_timer.Value).ToString("00")`, which shows **elapsed** seconds.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/GameMaster.cs` | Remove the `m_timer` NetworkVariable. Add a private elapsed timer, a private duration and a replicated phase end time. Add `GetPhaseSecondsLeft()`. |
| `Assets/Scripts/InGame/GameMaster/GameMaster.GameLoop.cs` | Use the private timer; set the countdown at each start, restart and resume |
| `Assets/Scripts/UI/GameAnnouncerUI.cs` | Show the seconds left |

## 4. Required changes

1. **`GameMaster.cs`:**
   - Replace `public NetworkVariable<float> m_timer;` with:
     - `private float m_phaseElapsed;` (server loop time; it replaces every `m_timer.Value` in server code)
     - `private float m_phaseDuration;` (the duration of the running countdown, server only)
     - `public NetworkVariable<double> m_phaseEndServerTime = new NetworkVariable<double>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);` (0 = no countdown)
   - Add `private void StartPhaseCountdown(float duration, float alreadyElapsed = 0f)`:
     - `m_phaseDuration = duration;`
     - `m_phaseEndServerTime.Value = NetworkManager.ServerTime.Time + Mathf.Max(0f, duration - alreadyElapsed);`
   - Add `private void ClearPhaseCountdown()`: sets `m_phaseEndServerTime.Value = 0`.
   - Add `public int GetPhaseSecondsLeft()`:
     - returns 0 if `m_phaseEndServerTime.Value <= 0` or `NetworkManager == null`
     - otherwise `Mathf.Max(0, Mathf.CeilToInt((float)(m_phaseEndServerTime.Value - NetworkManager.ServerTime.Time)))`
2. **Replace `m_timer.Value` with `m_phaseElapsed`** in every server use listed in §2. Keep each loop condition and increment exactly as it is, so phase lengths don't change.
3. **Start a countdown** right after each `= 0` reset:
   - `RoundBreakPhase`: `StartPhaseCountdown(m_roundBreakTime)`
   - `DrawCardPhase`: `StartPhaseCountdown(m_startHandCards * m_players.Count * m_cardDrawDelay)`
   - `CardSelectionPhase`: `StartPhaseCountdown(m_cardSelectionTime)`
   - `PlayerActionPhase`: `StartPhaseCountdown(m_playerActionTime)`
   - `WaitForRespawnIfNeeded` (at `:677`): `StartPhaseCountdown(m_respawnWaitTime)`
   - `ResetCardSelectionTimer`: `StartPhaseCountdown(m_cardSelectionTime)`
4. **Pause and resume** in `HandleDeathPausingCurrentPhase`: next to `_pausedTimer`, also save `float _pausedDuration = m_phaseDuration;`. On resume, after restoring `m_phaseElapsed = _pausedTimer;`, call `StartPhaseCountdown(_pausedDuration, _pausedTimer);`. The resumed phase then shows the time it really has left.
5. **End and reset:** call `ClearPhaseCountdown()` in `EndMatch`, in `AbortMatch` and in `StartNewGame` (where `m_timer.Value = 0f` was, `GameMaster.cs:537`).
6. **`GameAnnouncerUI.cs:56`:** `string _timerText = GameMaster.Instance.GetPhaseSecondsLeft().ToString("00");`. Keep the rest of the announcer text and layout unchanged.

## 5. Explicit non-goals

- No change to any phase duration, to the selection-reset limits or to the server's loop logic.
- No new draw phase in `GamePhase` (feature gap). The deal keeps showing "Round preparation", now with a countdown for the deal.
- No other `GameAnnouncerUI` changes. Per-frame allocations are TASK-L6.

## 6. Constraints and compatibility requirements

- `NetworkManager.ServerTime` is synchronized on clients, and is the authoritative time on the host. Clamp at 0: a client's estimate can be slightly ahead.
- Removing `m_timer` and adding `m_phaseEndServerTime` changes `GameMaster`'s network layout. Both peers use the same build.
- `GameMasterPrefab.prefab` may still contain a serialized `m_timer` block. Unity ignores it, and drops it the next time the prefab is saved. Don't hand-edit the prefab.
- The server loop keeps counting with `Time.deltaTime`, as before. The display can differ from it by a fraction of a second on a frame hitch; that's acceptable.
- `grep -rn "m_timer" Assets/Scripts` must find nothing afterwards, apart from unrelated names (check for `m_timer` in other classes first).

## 7. Acceptance criteria

1. Each phase shows a countdown that starts at its duration and reaches 00 when the phase ends:
   - Round preparation 5
   - the deal (about `m_startHandCards × players × 0.8` s)
   - Card Selection 60
   - each Turn 60
   - Waiting for respawn 30
2. A selection-timer reset sets the countdown back to 60.
3. After a respawn pause, the resumed phase shows the time that was left before the pause, not the full duration.
4. The NetworkVariable for the timer is written only at those starts, restarts and resumes, and at the match end. In the Network Profiler (or a temporary write counter) there's no per-frame timer traffic.
5. Host and client show the same number, within ±1 s.

## 8. Regression tests

- Harness:
  - `StartPhaseCountdown` sets the end time to now + (duration − elapsed)
  - `GetPhaseSecondsLeft` returns 0 when there's no countdown, and when the end time has passed
- Roslyn compile. Exit 0.

## 9. Manual Unity Editor checks

1. A two-player match: watch both announcers through Round preparation → the deal → Card Selection → both Turns. Each counts down and reaches 00 as the phase changes.
2. Optional: the selection-timer reset only happens when the server rejects an over-budget selection (`PackageManaging.cs:366-380`). An honest client can't trigger it, so use the checklist's tamper test C4 (temporary change T13: comment out the client mana gate, `PlayerSession.cs:286-287`). Confirm an over-budget selection: the countdown jumps back to 60. Revert T13.
3. Kill a pony in the lava with about 40 s left in a Turn. During "Waiting for respawn" it counts down from 30. After the respawn, the Turn continues from about 40 s.
4. Optional: the NGO Network Profiler shows no constant `GameMaster` variable traffic during a phase.

## 10. Open questions and assumptions

- Assumption: showing 00 when there's no countdown (phase None or GameOver) is fine, because that's the current look.
