# TASK-H7: Disconnect cleanup resets local state only; `Shutdown()` owns network objects

- **Source issue:** [Issue Report #2 → H7](../Reports/2026-10-18_IssueReport-2.md)
- **Severity:** 🟠 High
- **Chosen solution:** **H7-C without the server-despawn guard.** Cleanup never despawns. It stops the match loop, clears local lists, queues and UI, and is safe to call any number of times.
- **Order:** do this **first** among H7, H1 and H6. H1's `AbortMatch` relies on it.
- **Implementer:** Codex

## 1. Objective

`GameMaster.ClearupAfterDisconnect()` must never throw, must work the same on host and client, before or after `NetworkManager.Shutdown()`, and must be idempotent. Network objects are despawned and destroyed only by NGO during `Shutdown()`.

## 2. Current behavior

- `ClearupAfterDisconnect` (`Assets/Scripts/InGame/GameMaster.cs:301-328`):
  - `DespawnAllCards()`: server-guarded despawn
  - `m_inputQueue.Clear()`
  - resets spawn points
  - for each `SessionManager.Instance.m_playerSessions` (with no null check on `SessionManager.Instance`), calls `PlayerSession.ClearSession()`
  - `ClearPlayerCardWorldAreas()`
  - `CardWorldViewManager.ClearAllWorldViews()`
  - `PlayerUI.ClearLocalUI()`
- `PlayerSession.ClearSession` (`PlayerSession.cs:619-628`) calls `ClearHand()` (`:599-614`: despawns cards and **writes the NetworkList** `m_handCards.Clear()`), `ClearBlocks()` (`:453-466`: despawns) and a character `Despawn(true)`. **None of these has an `IsServer` guard.**
- Called from 4 places:
  - `HandleLostConnectionToHost` `:277`: client, before `Shutdown`
  - `ShutdownHostInternal` `:366`: host, before `Shutdown`
  - `DisconnectClientInternal` `:396`: client, before `Shutdown`
  - `CleanupAfterDisconnect` `:1213`: both, **after** `Shutdown`

  So it runs up to 2–3 times per disconnect.
- On clients, the despawn and NetworkList writes throw, and the rest of the cleanup is skipped. The match loop (`m_gameCoroutine`) is never stopped.
- **Evidence that `Shutdown()` already cleans up:** `Editor.log` shows `NetworkManager.ShutdownInternal → NetworkSpawnManager.DespawnAndDestroyNetworkObjects → BlockController.OnNetworkDespawn` (80 block despawns).

## 3. Relevant files and dependencies

| File | Member | Change |
|---|---|---|
| `Assets/Scripts/InGame/GameMaster.cs` | `ClearupAfterDisconnect` | Rewrite the body |
| `Assets/Scripts/InGame/PlayerSession.cs` | `ClearSession` | Rewrite the body (local references only) |
| `Assets/Scripts/InGame/PlayerSession.cs` | `ClearHand`, `ClearBlocks` | **Keep, unchanged**; they're no longer called from cleanup |
| `Assets/Scripts/InGame/GameMaster.cs` | `DespawnAllCards` | **Keep, unchanged**; still used by `StartNewGame` (`:382`) |
| `Assets/Scripts/InGame/PlayerCardfWorldArea.cs` | `ClearPlayerSession` | Read-only use (sets a reference only) |
| `Assets/Scripts/Manager/CardWorldViewManager.cs` | `ClearAllWorldViews` (`:769`) | Read-only use (local views) |
| `Assets/Scripts/UI/PlayerUI.cs` | `ClearLocalUI` (`:397`) | Read-only use (unsubscribes and clears text) |
| `MultiplayerManager.cs` | 4 call sites | **No change** |

## 4. Required changes

1. **`GameMaster.ClearupAfterDisconnect()`** new body, in this order:
   1. If `m_gameCoroutine != null`: `StopCoroutine(m_gameCoroutine); m_gameCoroutine = null;`
   2. Clear plain C# state only:
      - `m_inputQueue`, `m_cardInputQueue`
      - `m_players`, `m_turnOrder`, `m_turnSelections`
      - `m_deck`, `m_graveyard`, `m_spawnedCards`. Use `Clear()` only, without despawning, so the C# lists stop referencing cards that `Shutdown()` destroys.
      - set `m_isResolvingCards`, `m_isWaitingRespawn`, `m_activePlayerConfirmedAction`, `m_respawnConfirmed` and `m_externalDeathDetected` to `false`; set `m_respawnBlockNetworkId = 0`
   3. Spawn points: `IsOccupied = false`. This is a plain `bool` (`SpawnPoint.cs:15-23`). Skip `null` entries.
   4. If `SessionManager.Instance != null`: call `ClearSession()` on each non-null session value.
   5. `ClearPlayerCardWorldAreas()`, `CardWorldViewManager.Instance?.ClearAllWorldViews()`, `PlayerUI.Instance?.ClearLocalUI()`. Same as now, but use explicit null checks in the existing style instead of `?.` on Unity objects.
   6. **Do not** write any `NetworkVariable` or `NetworkList`, and **do not** call `Despawn` anywhere in this method.
2. **`PlayerSession.ClearSession()`** new body:
   - `m_currentCharacter = null;`
   - `ClearLocalCardSelection();`
   - `ClearSelectedCard();`
   - Optionally reset other **plain** client-side fields used for targeting. Codex should list any it touches.
   - No `ClearHand()` or `ClearBlocks()`, no `Despawn`, no NetworkList writes.
3. Keep every call site as it is. The method must be safe to run several times in one disconnect.

## 5. Explicit non-goals

- Consolidating the 4 call sites (H7-B was rejected).
- `SessionManager.ClearSessionOnDisconnect`. It already has an `IsServer` guard, and it runs after `Shutdown`, when `IsServer` is false.
- The H6 case "host stays in `GameScene` after the opponent leaves". Per the owner's decision there's no explicit despawn guard. The departed client's character and blocks are removed by NGO automatically (see the TASK-H6 §2 evidence).
- Changing `MultiplayerManager`.

## 6. Constraints and compatibility

- No renames. No method deletions.
- `StartNewGame` behaviour must stay the same. It still despawns old cards with `DespawnAllCards()`.
- Relies on NGO 2.11.2: on `Shutdown()` all dynamically spawned NetworkObjects are despawned and destroyed. This must be verified (§9, step 2).

## 7. Acceptance criteria

- [ ] On host and client, every disconnect path (host Disconnect button, client Disconnect button, host lost or closed, client killed) runs cleanup with **zero exceptions** in both Consoles.
- [ ] Running cleanup 3 times in a row has no side effects beyond the first run.
- [ ] After returning to the lobby, the Hierarchy contains no leftover `NetworkCard`, `BlockController`, `PlayerController` or `PlayerSession` objects.
- [ ] The match loop no longer runs after a disconnect (no phase logs).
- [ ] A new match started afterwards works normally: 10 cards in hand over the rounds, blocks spawned, both ponies spawned.
- [ ] Compiles without new warnings.

## 8. Regression tests

No automated tests.

Review checklist:
- grep the new bodies for `Despawn`, `.Value =` and NetworkList `.Clear()`/`.Add`/`.Remove`; none may appear
- the coroutine is stopped
- `SessionManager.Instance` is null-checked

## 9. Manual Unity Editor checks

1. ParrelSync host and client, start a match, play to `PlayerAction`.
2. **Verify the H7-C assumption:** host presses Disconnect. On both instances, in Play Mode, check the Hierarchy (including DontDestroyOnLoad):
   - no cards, blocks, ponies or sessions remain
   - no errors in either Console
3. Repeat with the **client** pressing Disconnect, then with **killing the client** process, then with **killing the host**.
4. After each run, start a new match from the lobby and play one round.
5. If step 2 shows leftover objects, **stop and report**. The H7-C assumption is wrong, and the owner must reconsider H7-B.

## 10. Open questions and assumptions

- **A1:** In-scene placed NetworkObjects (`SessionManager`, `PlayerUI`, `CardWorldManager` in `GameScene`) are despawned, not destroyed, by `Shutdown()`. They're destroyed by the following scene load to the lobby. That's acceptable.
- **A2:** `GameMaster` is spawned by `Bootstrap.RecreateGameMaster`, so it's a dynamic object and is destroyed on `Shutdown()`. Cleanup running before `Shutdown()` (3 of the 4 call sites) still finds it alive. After `Shutdown()`, the `GameMaster.Instance != null` check at the call site protects the 4th call.
