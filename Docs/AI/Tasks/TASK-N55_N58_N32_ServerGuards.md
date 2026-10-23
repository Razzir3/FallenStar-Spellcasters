# TASK-N55_N58_N32: Server guards: a failed match start aborts cleanly, the card package has a target limit, and an unused unsafe read is removed

- **Source:** [Issue Report #12](../Reports/2026-10-22_IssueReport-12.md): N55 and N58 (§3), N32 (§4, from report #8).
- **Severity:**
  - 🟢 Low: **N55** (Confirmed, latent)
  - 🟢 Low: **N58** (Plausible, needs a modified client)
  - 🟢 Low: **N32** (Confirmed, latent)
- **Baseline:** `6d89a09`. Do this task **after** TASK-N54_N56_N57. None of its scripts overlap with that task, so the line numbers below stay valid. Only `Dependencies.md` is shared.
- **Implementer:** Codex
- **Scope:** four scripts, server side only. No asset change.

## 1. Objective

1. **N55:** if the players can't be spawned at match start, the server aborts the match the normal way (`AbortMatch` → everyone back to the lobby). Both players are no longer left in an idle GameScene. That covers a missing team spawn point and any exception while spawning or starting.
2. **N58:** the server never allocates or reads more than 4 targets from a client's `CardControllPackage`.
3. **N32:** `ProcessActivity` no longer reads the card owner's character, which it never uses and doesn't null-check.

## 2. Current behavior

### 2.1 N55: an exception at match start leaves the match idle

- **`MultiplayerManager.StartGameAfterSceneReady`** (`MultiplayerScripts/MultiplayerManager.cs:1381-1462`):
  - It waits for `SessionManager` and both sessions.
  - Then it calls `GameMaster.Instance.SpawnPlayers()` (`:1459`) and `StartNewGame()` (`:1461`).
  - It's a plain coroutine: no `try`/`catch`.
- **`GameMaster.SpawnPlayers`** (`InGame/GameMaster/GameMaster.Spawn.cs:210-235`) runs `CreateBlock` and `SpawnCharacter` for each session.
- **`CreateBlock`** (`:240-282`):
  - `GetSpawnPoint(team)` can return null. That happens when the team has no enabled, unoccupied scene `SpawnPoint`.
  - It then logs `"No Spawn Point"` (`:250`) and **carries on**: `_spawnPoint.transform` at `:257` throws a `NullReferenceException`.
- **The result:**
  - The exception ends the coroutine, so `StartNewGame` never runs.
  - The phase stays `None` and nothing aborts. Both players sit in GameScene until someone presses Leave match. Leave works, because with phase `None` it disconnects directly.
- **When it happens:** GameScene has exactly one scene spawn point per team: PrefabInstance `1977742868` (team 0) and `1585064127` (team 1). So it takes a scene edit or a third team. It's latent today.
- **`SpawnCharacter`** (`:7-77`) handles a missing point by logging `"No spawn point available"` and returning. A match would then start without that character.

### 2.2 N58: the package reader trusts a client-sent count

- **`CardControllPackage.NetworkSerialize`** (`InGame/CardControllPackage.cs:16-46`), when reading:
  - takes `_count` from the stream
  - allocates `new List<ulong>(_count)`
  - then reads `_count` values
- **What a bad count does:**
  - **negative:** throws `ArgumentOutOfRangeException`
  - **huge:** allocates a large array before the read fails
- **Who can send one:** only a modified client calling `CardInputServerRpc`. NGO catches the exception, so the host survives, but it can stall and it logs an error.
- **What the server checks today:** `HandleCardControlPackage` doesn't look at the target count. `PlayerActionPhase` only checks `count ≥ activities` (`GameMaster.GameLoop.cs:249-258`).
- **Honest clients always send exactly 4 targets** (`CreateEmpty`). `CardData.OnValidate` keeps 4 activities (GL5 / RP3).

### 2.3 N32: an unused, unchecked read

- **`GameMaster.Processing.cs:139`:** `PlayerController _ownerPlayer = GetSessionByID(cardOwnerId).GetCharacter();`
- `_ownerPlayer` is never used, and the line throws if the owner's session is gone.
- **It can't throw today,** because `CheckActivity` (`:66-75`) rejects a missing owner first. If it ever did throw, `RunGuarded` would abort the match.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/GameMaster/GameMaster.Spawn.cs` | `SpawnPlayers` checks the spawn points first and returns `bool`; `CreateBlock` returns on a missing point (N55) |
| `Assets/Scripts/MultiplayerScripts/MultiplayerManager.cs` | `StartGameAfterSceneReady` aborts on `false` or on an exception (N55) |
| `Assets/Scripts/InGame/CardControllPackage.cs` | A `MaxTargets` constant and a bounded reader (N58) |
| `Assets/Scripts/InGame/GameMaster/GameMaster.PackageManaging.cs` | `HandleCardControlPackage` rejects a target list that's null, empty or too long (N58) |
| `Assets/Scripts/InGame/GameMaster/GameMaster.Processing.cs` | Delete line 139 (N32) |
| `Docs/AI/Architecture/GameLoop.md`, `Dependencies.md` | The start-abort reason, and the package limit (RP3 / GL5) |

**Dependencies:**
- **`SpawnPlayers` has one caller,** `StartGameAfterSceneReady`. Changing its return type from `void` to `bool` touches nothing else.
- **`AbortGameStart(reason)`** (`MultiplayerManager.cs:1307-1315`) → `GameMaster.AbortMatch` → `MultiplayerManager.Disconnect`. It's the existing path for `"player_left_before_start"` and the others.
- **`CardControllPackage` is serialized in both directions.** The client writes and the server reads. The host's own RPC goes through the same code.

## 4. Required changes

### 4.1 N55

1. **`SpawnPlayers` returns `bool`.**
   - **Check first:** before spawning anything, check every `SessionRole.Player` session. If `GetSpawnPoint(session.TeamId.Value)` is null for any of them, log one error naming the team and return `false`. Spawn nothing.
   - **Then:** spawn as today, run `BindPlayerCardWorldAreas`, set `m_currentMultiplayerStatus`, and return `true`.
2. **`CreateBlock`:** after its `"No Spawn Point"` error, `return`. It's a second guard and should never be reached after step 1.
3. **`StartGameAfterSceneReady`:**
   - Put the two calls in a `try`/`catch (System.Exception e)`. A coroutine can't `yield` inside `try`, and these two calls don't yield.
   - `SpawnPlayers()` returns `false` → `AbortGameStart("spawn_failed")`, then `yield break`.
   - An exception from either call → `Debug.LogException(e)`, then `AbortGameStart("match_start_exception")`, then `yield break`.
   - Otherwise call `StartNewGame()` as today.
4. **No other change** to `SpawnCharacter`, `RespawnCharacter` or `GetSpawnPoint`.

### 4.2 N58

1. **Add `public const int MaxTargets = 4;`** to `CardControllPackage`. `CreateEmpty` uses it instead of the literal list of four zeros. The resulting list is the same.
2. **The reader:** if `_count < 0 || _count > MaxTargets`, set `TargetNetworkObjectId` to an empty list, read no targets, and return. Otherwise read as today.
3. **The writer is unchanged.**
4. **`HandleCardControlPackage`:** right after the active-player checks, reject a package whose `TargetNetworkObjectId` is null, empty, or longer than `MaxTargets`.
   - Log one warning in the style of the others: `"Card package rejected. Invalid target count: {n}."`
   - Return `false`.
   - Don't add a `SuspiciousActionType`.
5. **Unchanged:** `GetTarget` / `SetTarget` and the `PlayerActionPhase` checks.

### 4.3 N32

- Delete line 139. Nothing else in `ProcessActivity` changes.

### 4.4 Docs

- **`GameLoop.md`:** at the match-start steps, add that a missing team spawn point or an exception aborts the start (`spawn_failed` / `match_start_exception`).
- **`Dependencies.md`, RP3:** add `MaxTargets` and the reader limit.

## 5. Explicit non-goals

- **No change to how spawn points are chosen.** That includes the random pick, `IsOccupied` and the block grid.
- **No new suspicious-activity type,** and no change to `SuspiciousActivityLog`.
- **No change to:**
  - the card rules, `CheckActivity` or the rest of `ProcessActivity`
  - the RPC signatures
- **Not in this task:** N33 / N34 log and dead-code work. That includes the ungated `"Process called"` log on the next line. It belongs to the hygiene task.

## 6. Constraints and compatibility requirements

- **Server authority is unchanged.** The new checks run where the old ones did.
- **The wire format is the same for honest clients:** count 4, then 4 ids.
- **No serialized field changes.**
- **Unity 6 APIs, no warnings of our own.** Baseline copies go in `build/n55-verification/baseline/`.
- **Restore the dynamic font to `HEAD`** before handing over.

## 7. Acceptance criteria

1. **N55, a missing team spawn point:**
   - In a test scene with only team 0's scene spawn point, `SpawnPlayers()` returns `false`, spawns nothing and logs one error.
   - `StartGameAfterSceneReady` calls `AbortGameStart("spawn_failed")`. `StartNewGame` doesn't run.
2. **N55, an exception:** when `SpawnPlayers` throws (fixture), the exception is logged and `AbortGameStart("match_start_exception")` runs.
3. **N55, the normal start:** with both spawn points, the spawn is unchanged:
   - 25 blocks per player
   - one character at its team's point
   - the card areas bound
   - `StartNewGame` runs
4. **N58, the reader:**
   - A buffer written with count −1, 5 and 1 000 000 reads as an empty target list, with no exception and no large allocation.
   - Counts 0–4 read exactly as today.
   - A round-trip of `CreateEmpty(id)` with targets set gives the same values.
5. **N58, the handler:** a package with 0 or more than 4 targets is rejected with the warning. A normal 4-target package is accepted as before.
6. **N32:** the line is gone, and a full Damage / Heal / DrawCard cast resolves as before.
7. **Build:** 0 errors, no warnings of our own, and the release player builds.

## 8. Regression tests

- **Edit Mode or Play Mode tests,** temporary, in `Temp/N55Verification/`.
  - **AC4:** `FastBufferWriter` / `FastBufferReader` with a `BufferSerializer`.
  - **AC1–AC3:** a GameMaster fixture in a minimal scene: a `NetworkManager` started as host, two synthetic sessions, and the real `BlockBody`, `PonyBase` and `SpawnPoint` prefabs. If the full fixture is too heavy, test `SpawnPlayers`' pre-check and the coroutine's abort branch separately, and say so in the report.
- **AC6:** run the existing card-resolution harness if one is available (H3 / N20). Otherwise give a code-path argument, and say which.
- **The report lists** each case with its result. Remove the harness from `Assets` and `Temp` afterwards, and keep a copy in `build/n55-verification/`.

## 9. Manual Unity Editor checks

**Setup:** two Editors, the clone as client.
1. **A normal match:** start it, play one round, and cast a Damage, a Heal and a draw card. Nothing changes.
2. **The two logs:** no new warning or error during a normal match.
3. **Optional, N55 on purpose:**
   - In a scratch copy of GameScene that you **don't save**, disable team 1's `SpawnPoint (1)` and start a match.
   - Both players return to the lobby, and the host's log shows `spawn_failed`.
   - Revert the scene afterwards.

## 10. Open questions and assumptions

- **A1, why abort rather than half-start.** A match without one side's blocks or character makes no sense: the dead-player path would end it at once. `AbortGameStart` already exists for the other start failures.
- **A2, the empty list for a bad count.** It keeps the reader simple and leaves the rejection to the handler, which has the sender's context.
- **A3, `MaxTargets` = 4** matches `CardData`'s fixed 4 activities. If cards ever get more activities, both change together (GL5).
