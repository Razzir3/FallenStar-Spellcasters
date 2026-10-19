# N18/M1 implementation: death checks and paused phase timers

## 1. Task summary

Implemented `TASK-N18_M1_DeathChecksAndRespawnTimer.md`: the active gameplay phases detect death, check respawn eligibility before pausing, and resume the same phase with its saved elapsed timer and active player. Decisive deaths exit toward the existing match-end path. Rebuilt selection views restore their play/discard highlights.

## 2. Files changed

- `Assets/Scripts/InGame/GameMaster/GameMaster.GameLoop.cs`
- `Assets/Scripts/InGame/PlayerSession.cs`
- `Assets/Scripts/UI/PlayerUI.cs`
- `Docs/AI/Architecture/GameLoop.md`
- `Docs/AI/Architecture/Dependencies.md`
- This implementation report.

Existing uncommitted N14 changes and its report were preserved. No scenes, prefabs, card/font/localization assets, serialized fields or package settings were changed. No commit or push was performed.

## 3. Important implementation details

- Added private `HandleDeathPausingCurrentPhase`. It clears stale external-death state, identifies a dead player with lives left, checks valid own block spawn points before pausing, and uses the existing no-block elimination rule. For eligible respawns it saves phase, timer and active-player reference, yields the unchanged respawn routine, then restores state only if the match continues.
- Added the specified finish/death/finish order in RoundBreak, drawing and CardSelection, and replaced both PlayerAction death blocks with the helper. PlayerAction uses `break` so its existing card cleanup remains reachable. The outer loop checks finish after each non-action phase before progressing.
- Mana growth, selection setup, accepted confirmations, timer-reset counters and completed draws are retained in the existing suspended iterators. No phase restarts or enum/order changes were introduced.
- Resolved a specification timing gap: checking only before a draw would leave deaths undetected during its existing 0.8-second WaitForSeconds. The draw delay now yields per frame and checks death/finish during the delay. Its local elapsed value survives a pause; the configured scaled delay and the original shared-timer increment after each completed delay remain. A zero/negative configured delay still yields at least one frame, as the previous wait did. Exact Unity scheduling equivalence remains a manual check.
- Drawing keeps its existing phase value, normally RoundBreak; this task does not start assigning the previously unused DrawCards enum value.
- Added the two public read-only membership queries to PlayerSession. CreateCardView uses them after Setup/SetNetworkCard and only during CardSelection, applying the existing play/discard color methods. Selection lists are not changed by rebuilding views.
- Server authority remains the existing StartNewGame/guarded game-loop path. The helper adds no independent background coroutine or client rule execution. Existing Can* phase gates prevent movement/card input during RespawnWait. H6 can still stop the guarded coroutine from outside.
- `WaitForRespawnIfNeeded`, `FindDeadPlayer`, `GetRespawnPoints`, `RespawnCharacter`, `RunGuarded`, H6 and EndMatch are unchanged. The known N6 queued-package issue remains outside scope.
- Required Inspector setup: none.

## 4. Tests actually executed and results

### Runtime C# compilation

Compiled with Unity 6000.3.13f1's bundled Roslyn compiler and the project's existing Editor Assembly-CSharp response file, references, definitions, source list and analyzers. Outputs were redirected to ignored `build/task-n18-m1-verification/`, leaving Unity's cached assemblies intact.

Result: exit code 0, no errors and no new warnings. Only existing CS0114 (PlayerUI.OnDestroy) and CS0414 (GameMaster.m_cardResolveDelay) were emitted. This is C# compilation, not Editor import, NGO IL postprocessing, a player build or Play Mode.

Evidence: `build/task-n18-m1-verification/compile.rsp` and `compile-results.txt`.

### Focused source behavioral checks

Ran `build/task-n18-m1-verification/verify.ps1`. It compiles production phase coroutines, RunGuarded, the new helper, the unchanged respawn wait and death/finish lookup methods, MANA2 timer reset, H6 HandlePlayerLeft, the selection queries and CreateCardView with Unity/NGO/view doubles.

**24 isolated scenarios passed**, covering:

- Stale death flags, exact restoration of phase/timer/active player for all four phase values, and input gates during a pause.
- No-block and last-life eligibility checks without entering RespawnWait, and sequential respawns for two dead players.
- RoundBreak resumption without repeated mana growth.
- CardSelection resumption retaining accepted confirmation objects, local selected cards, MANA2 reset counters and elapsed time.
- Either player's death during an action turn, activity-triggered death, timer restoration and retained action cleanup.
- Next-iteration death detection within the draw delay, completed draw count and remaining-delay preservation.
- Decisive death exiting each active phase on the next iterator frame, plus GameLoop termination from RoundBreak/draw and no-block elimination.
- No-death phase counts and elapsed timer totals.
- H6 stopping a paused runner with the forfeit result retained.
- Play/discard/unselected view colors, rebuilding after a pause and no selection-color restoration outside CardSelection.

Static comparisons confirmed WaitForRespawnIfNeeded and FindDeadPlayer are identical to HEAD. Final diff review also verified unchanged respawn-point/character-spawn logic, guarded runner and H6 method. `git diff --check` passed. Verification files are git-ignored, outside Assets; no permanent test scripts were added.

The harness replaces network references/state, actual character spawning, view rendering and Unity coroutine scheduling with doubles. Its next-frame assertions concern iterator progress, not measured network/UI latency.

## 5. Remaining risks / checks not executed

- No Unity Play Mode, real host/client replication, physics/lava timing, rendered highlight check or full player build was run for this task.
- Verify the interruptible draw delay's real Unity timing and immediate pause/result delivery on both peers. The existing drawing phase value is preserved rather than corrected as unrelated work.
- The existing respawn routine and its handling of conditions changing during an already active respawn wait remain unchanged, as required by the specification. N6 queued packages after a decisive activity also remain unchanged.
- Existing IsGameFinished allocation and side effects remain; the specification accepts the extra calls.

## 6. Manual Unity Editor verification

1. Run a ParrelSync host/client match. During an action turn at about 20 seconds, die and choose a block about 10 seconds later. Confirm the same active player resumes at about 20 seconds.
2. Arrange a lava death during RoundBreak. Verify immediate RespawnWait, resumption from the saved elapsed value and only one mana growth for the round. Also test death between card draws; already dealt cards and the remaining delay should be retained.
3. During CardSelection, accept one player's selection, highlight play/discard cards on the other peer, then trigger a death. After respawn verify the same timer, highlights and accepted confirmation; confirming must send the highlighted cards.
4. Test last-life death and a player with lives but no valid blocks in each active phase. Verify prompt GameOver/result flow without a new RespawnWait.
5. Play two rounds without deaths and compare timing/draw counts with the prior build.
6. Disconnect during RespawnWait and verify the existing H6 forfeit result. Restore any temporary debug/testing changes. No persistent Inspector setup is needed.
