# TASK-N7 + N22: Safe character lookup, and full HP from the first frame

- **Source issues:** [Issue Report #7 → N7, N22](../Reports/2026-10-20_IssueReport-7.md#-low-23)
- **Severity:** 🟢 Low (both Confirmed, latent)
- **Baseline:** `b7e7b91`. Line numbers below are from this commit.
- **Order:** 7 of 20. Independent. TASK-L10 later deletes `PlayerController.ReceiveValidatedInput`; keep this task's change to `m_inputQueue` either way.
- **Implementer:** Codex

## 1. Objective

- **N7:** `PlayerSession.GetCharacter()` never throws. It returns `null` when the stored character ID doesn't belong to a spawned `PlayerController`.
- **N22:** a spawned or respawned pony has its real HP (100/100) and speed from the moment it's spawned, not one frame later.

## 2. Current behavior

### N7: `InGame/PlayerSession.cs:659-688` `GetCharacter()`

- When the cached `m_currentCharacter` is null, it looks up `CharacterObjectId.Value` in `NetworkManager.Singleton.SpawnManager.SpawnedObjects` and takes `_obj.GetComponent<PlayerController>()`.
- On the server it then writes `CharacterObjectId.Value = m_currentCharacter.GetComponent<NetworkObject>().NetworkObjectId;` (`:668`). That's the same ID it just looked up, so the write does nothing useful. But if the object has **no** `PlayerController`, `m_currentCharacter` is null here and this line throws a `NullReferenceException`.
- **When the ID can point at the wrong object:**
  - `CharacterObjectId` is reset to 0 only in the death path (`GameMaster.Processing.cs:355-356`)
  - `RecycleNetworkIds` is on (`NetworkManagerPrefab`, delay 120 s)
  - so a stale ID may later belong to another object, for example a card
- The lookup runs before the `CharacterObjectId == 0` check (`:677`), so ID 0 is looked up too.
- `NetworkManager.Singleton` isn't null-checked. `GetCharacter()` is called several times per frame from `IsAnyPlayerDead`, `IsGameFinished` and `FindDeadPlayer`, and once during teardown.

### N22: `InGame/PlayerController.cs`

- `m_hpMax` and `m_hpCurrent` start at 1 (`:17-18`), and `m_moveSpeedCurrent` at 0 (`:13`).
- `OnNetworkSpawn` (`:63-79`) only starts `DelayedInit()`. `DelayedInit` (`:92-109`) does `yield return null`, and only **then** calls `Init()` on the server (`:98-101`). `Init()` (`:127-137`) sets speed 3, HP 100/100 and creates `m_inputQueue`.
- So for one frame after every spawn and respawn:
  - the pony has 1/1 HP: a damage card, the lava or the debug key resolved in that frame kills it at once
  - its speed is 0
  - `m_inputQueue` is null, so `ReceiveValidatedInput` (`:289-298`) would throw
- `GameMaster.Spawn.cs` calls `SpawnAsPlayerObject` (`:53`, `:150`), then `Initialize(session)` and the session bookkeeping in the same frame.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/PlayerSession.cs` | `GetCharacter()` |
| `Assets/Scripts/InGame/PlayerController.cs` | `Init()` on the server inside `OnNetworkSpawn`; `m_inputQueue` initialized at declaration |

Callers of `GetCharacter()`: `GameMaster.GameLoop.cs` (`IsAnyPlayerDead`, `IsGameFinished`, `FindDeadPlayer`, the end-of-match winner), `GameMaster.Spawn.cs` (`DespawnCharacter`), `GameMaster.Processing.cs` (movement), `PlayerUI` (HP display).

## 4. Required changes

### N7

Rewrite `GetCharacter()` with the same contract (return the character or `null`):
1. If `m_currentCharacter != null`, return it. A Unity-destroyed object compares as null, so this works as today.
2. If `CharacterObjectId.Value == 0`, return `null`. Keep the existing `m_debugMode` log `"CharacterObjId is 0"`.
3. If `NetworkManager.Singleton == null` or its `SpawnManager == null`, return `null`.
4. Look up the ID. If it's not found, or the object has no `PlayerController` (`TryGetComponent`), return `null` and **don't** cache. Keep the existing `m_debugMode` log `"No Character"` for "not found".
5. Otherwise cache it in `m_currentCharacter` and return it. **Delete** the server-side `CharacterObjectId.Value = …` write: it only ever wrote the same value.

### N22

1. In `OnNetworkSpawn`, call `Init()` **synchronously** when `IsServer`, before `StartCoroutine(DelayedInit())`.
2. Remove the `if (IsServer) Init();` from `DelayedInit`. Keep the rest: the `m_ponyColorScript` lookup, the wait for the session, `ApplyMaterial()`.
3. Initialize `m_inputQueue` at its declaration: `private Queue<ToPlayerControlPackage> m_inputQueue = new();`. Remove the assignment from `Init()`.

## 5. Explicit non-goals

- No change to when `CharacterObjectId` is reset (`Processing.cs:356`).
- No change to the pony materials (N27 is parked by the owner).
- No change to HP values (100), speed (3) or lives.
- Don't remove `DelayedInit`: the material still needs the session's `TeamId`.

## 6. Constraints and compatibility requirements

- The values written in `OnNetworkSpawn` on the server are part of the spawn payload (NGO 2.11.2: `SpawnInternal` sends the spawn message after `AuthorityLocalSpawn` / `OnNetworkSpawn`). Clients therefore see 100/100 in their first `OnNetworkSpawn`.
- Keep the NetworkVariable field names; they're serialized on the pony prefab.
- `GetCharacter()` stays cheap: no allocation, no `Find*` call.

## 7. Acceptance criteria

1. `GetCharacter()` returns `null`, without an exception, when `CharacterObjectId` points to a non-pony object or to nothing.
2. `GetCharacter()` makes no NetworkVariable write.
3. Right after `SpawnAsPlayerObject`, the server reads `m_hpCurrent == 100`, `m_hpMax == 100` and `m_moveSpeedCurrent == 3`. A client's `OnNetworkSpawn` reads 100/100 too.
4. Respawn after a lava death: the HUD never shows 1/1, even for a frame.

## 8. Regression tests

- Harness: `GetCharacter()` with `CharacterObjectId`:
  - = 0 → null
  - = an unknown ID → null
  - = an object without `PlayerController` → null, no exception, nothing cached
  - = a valid pony → the controller
- Harness or Play Mode test: `PlayerController` spawned on a server double has HP 100 before the next frame.
- Roslyn compile. Exit 0.

## 9. Manual Unity Editor checks

1. A two-player match starts: both HUDs show 100 HP for both ponies from the first frame.
2. Lava death with lives left → respawn: the HP display goes straight to 100. Movement works in your next Turn.
3. Iceshard (20 damage) on the opponent right after their respawn: their HP goes 100 → 80, and the pony doesn't die.
4. The Space debug key (−10 HP) works after a respawn.

## 10. Open questions and assumptions

- Assumption: nothing relies on HP being 1 in the first frame. Grep found no such reader.
