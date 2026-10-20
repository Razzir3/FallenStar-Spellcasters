# TASK-N3: Clients see the server's input lock

- **Source issue:** [Issue Report #7 → N3](../Reports/2026-10-20_IssueReport-7.md#-low-23)
- **Severity:** 🟢 Low (Confirmed, latent). No visible effect today; it matters as soon as resolving a card takes time.
- **Baseline:** `b7e7b91`. Line numbers below are from this commit.
- **Order:** 8 of 20. After TASK-N6 and TASK-L3 (same file). Keep their changes.
- **Implementer:** Codex

## 1. Objective

`CanPlayerMove`, `CanPlayerSelectCardTargets` and `CanPlayerSelectCards` give the same answer on a client as on the server. The server-only lock flags are replicated as one read-only NetworkVariable.

## 2. Current behavior

- `InGame/GameMaster/GameMaster.GameLoop.cs:15-39`: all three gates combine the phase and the active player (both NetworkVariables) with `!m_isResolvingCards && !m_isWaitingRespawn`.
- `m_isResolvingCards` and `m_isWaitingRespawn` (`GameMaster.cs:101-102`) are plain private bools, written only on the server: about 20 assignments in `GameLoop.cs`, `GameMaster.Player.cs:20-21`, `:63-64` and `GameMaster.cs:342-343`, `:529-530`.
- On a client they're always `false`, so the client-side gates ignore them.
- **Client callers:**
  - `PlayerController.cs:205`: movement input is only sent when `CanPlayerMove`
  - `PlayerSession.cs:275`, `:315`: select to play or discard
  - `PlayerSession.cs:1088`: Send in `TryHandlePackage`
- **Effect today: none visible.**
  - `m_isWaitingRespawn` is only `true` while the phase is `RespawnWait`, and the phase check already blocks then.
  - `m_isResolvingCards` is `true` while a card resolves (`GameLoop.cs:240-318`):
    - normally inside one frame
    - longer only during a death pause, and then the phase is `RespawnWait` again
    - after the resume, the rest of the card resolves in that same frame
  - So the client and server answers differ at most within a single frame.
- **Why it's still worth fixing:** with any future resolve delay or animation (the unused `m_cardResolveDelay` suggests one was planned), clients would offer input the server always refuses. The server rejects movement in `GameMaster.Processing.cs:59` with a debug warning.
- The server's own card-input path (`PackageManaging.cs:100-162`) queues packages regardless of the lock. That's intended: the queue sequences them.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/GameMaster.cs` | New `NetworkVariable<bool> m_isInputLocked`; the server mirrors it in `Update` |
| `Assets/Scripts/InGame/GameMaster/GameMaster.GameLoop.cs` | The three gates use one private helper |

## 4. Required changes

1. **`GameMaster.cs`:** next to `m_gamePhase`, add
   `public NetworkVariable<bool> m_isInputLocked = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);`
2. **`GameMaster.Update`** (`:137-149`), in the server branch, before `InputQueueUpdate()`:
   ```csharp
   bool _locked = m_isResolvingCards || m_isWaitingRespawn;
   if (m_isInputLocked.Value != _locked)
       m_isInputLocked.Value = _locked;
   ```
   One place mirrors the flags, so no assignment can be missed. The value replicates at most once per change.
3. **`GameMaster.GameLoop.cs`:** add
   `private bool IsInputLocked() => IsServer ? (m_isResolvingCards || m_isWaitingRespawn) : m_isInputLocked.Value;`
   Replace `!m_isResolvingCards && !m_isWaitingRespawn` in the three gates with `!IsInputLocked()`. The server keeps using the live flags, so its decisions don't change by a frame.
4. Reset `m_isInputLocked.Value = false` where the match state is reset on the server: `ClearupAfterDisconnect()` (`GameMaster.cs:342-343`) and `StartNewGame()` (`:529-530`), the two places that set both flags to `false`. Only when `IsServer` and the object is spawned.

## 5. Explicit non-goals

- No change to the server-side rules or to the card-input queue.
- No UI feedback for a locked state. The existing gates simply return `false`.
- No change to `m_isResolvingCards` / `m_isWaitingRespawn` semantics or assignments.

## 6. Constraints and compatibility requirements

- NGO 2.11.2: a NetworkVariable write that doesn't change the value isn't sent. Step 2 also compares before writing.
- A new NetworkVariable changes `GameMaster`'s network layout. Host and client always run the same build.
- `GameMasterPrefab` gets no manual edit. A default-initialized NetworkVariable needs none.

## 7. Acceptance criteria

1. Whenever the server's `m_isResolvingCards || m_isWaitingRespawn` stays `true` for more than one frame, the clients' `CanPlayerMove` / `CanPlayerSelectCardTargets` / `CanPlayerSelectCards` return `false` from the next replicated tick. Prove it with the harness test (§8): today's game flow has no such window.
2. Otherwise all three gates behave exactly as before on host and client.

## 8. Regression tests

- Harness:
  - the gate helper returns the live flags when `IsServer`, and the NetworkVariable value otherwise
  - the `Update` mirror writes only on a change
- Roslyn compile. Exit 0.

## 9. Manual Unity Editor checks

These are regression checks only: the lock window doesn't exist in today's flow.
1. A two-player match: movement in your own Turn works, and the opponent can't move.
2. A lava death with a respawn pause in a Turn: no movement during the pause, then movement works again after the respawn.
3. Card selection, Send and Confirm work as in A1 / A14.

## 10. Open questions and assumptions

- Assumption: a one-frame delay before clients see the lock is acceptable. The server still decides authoritatively.
