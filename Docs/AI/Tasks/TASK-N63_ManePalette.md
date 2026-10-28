# TASK-N63_ManePalette: A quiet lobby refresh after the lobby is gone, and the owner's mane colours

- **Source:** [Issue Report #16](../Reports/2026-10-24_IssueReport-16.md) §3 (N63), §4 (poll #15 Q1), §6.
- **Owner decision (poll #15 Q1, 2026-10-24):** **c.** "White, gold, pink violet and orange." That's five colours: white, gold, pink, violet, orange. Black and brown go.
- **Owner's note that found N63** (checklist #15, row A4.1): "16:58:36; Once Unity.Services.Lobbies.LobbyServiceException: lobby not found; Cant reproduce rn".
- **Severity:** N63 🟢 Low (a red error in the log, no wrong behaviour). The palette is a feature change.
- **Baseline:** the commit after checklist #16 (`ManualChecks/2026-10-24_ManualChecks-Report16.xlsx`) passes. Start only after it. Line numbers below are from `5abd155`.
- **Implementer:** Codex. Independent of TASK-F15_F16_N64: no shared files. One task at a time, each with its own baseline snapshot.

## 1. Objective

1. **N63:** a lobby refresh that gets "lobby not found" because the lobby was just deleted (our own Disconnect, or the host left) logs nothing. A lobby that disappears while we still hold it logs a **warning**, not an error.
2. **Palette:** the five mane colours are white, gold, pink, violet and orange. Nothing else about the mane changes.

## 2. Current behavior

### 2.1 N63: a refresh races the lobby's deletion

- **`CurrentLobbyUI.RefreshLobbyLoop`** (`UI/CurrentLobbyUI.cs:123-147`) calls `MultiplayerManager.RefreshCurrentLobby()` every 2 s while the current-lobby view is open.
- **`RefreshCurrentLobbyCore`** (`MultiplayerManager.cs:984-1007`) reads `m_currentLobby.Id`, awaits `LobbyService.Instance.GetLobbyAsync(id)`, and in `catch (LobbyServiceException e)` logs **`Debug.LogError(e)`** for anything but `RateLimited`.
- **The host's Disconnect** (`Disconnect` → `ShutdownHostInternal`, `MultiplayerManager.cs:332-375`, `:498-536`) awaits `DeleteLobbyAsync(m_currentLobby.Id)` while `m_currentLobby` is still set, and clears it only afterwards (and again in `CleanupAfterDisconnect`, `:1535-1551`).
- **So a refresh that's already in flight, or one that starts during the delete, can reach the service after the lobby is gone.** The service answers 404 `LobbyNotFound`, and the catch prints a red `LobbyServiceException: lobby not found` with its stack trace.
- **Evidence:** the clone's `Editor-prev.log` of 2026-10-24 (the clone was the host there): line 3839, right after a `LobbyScene` load, `GetLobbyAsync` ← `RefreshCurrentLobbyCore () … MultiplayerManager.cs:989`, logged at `:1000`. It happened once, as the owner wrote. It depends on timing.
- **Nothing else goes wrong:** the method returns `false`, the view closes as usual, and N61's single Lobby load isn't affected.
- **The same race can hit a client** when the host deletes the lobby (A2.1): its refresh may get the 404 before NGO reports the lost host.
- `LobbyExceptionReason.LobbyNotFound` (16001) exists in the SDK (`com.unity.services.multiplayer` 2.2.2, `LobbyExceptionReason.cs:23`).

### 2.2 The palette

- **`ManeColor`** (`InGame/ManeColor.cs`): `White, Black, Brown, Gold, Pink`. The value is the index into `PonyColorScript.m_maneMaterials` and travels as an `int` in `NetworkManeColor`. The names aren't serialized anywhere.
- **`PonyBase.prefab` → `PonyColorScript.m_maneMaterials`** lists, by GUID: `ManeWhite` (`ca2f3d97…`), `ManeBlack` (`59debdd8…`), `ManeBrown` (`8789569a…`), `ManeGold` (`bf6de7c3…`), `ManePink` (`7f54deaf…`).
- **The materials** (`Assets/Materials/Mane/`) are copies of `TestMaterial0` (URP Lit) that differ only in `_BaseColor` and `_Color`.
- **RP22** (`Dependencies.md`): the enum order is the material order; append only.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/MultiplayerScripts/MultiplayerManager.cs` | `ShouldReportMissingLobby` + one `catch` clause in `RefreshCurrentLobbyCore` (§4.1) |
| `Assets/Scripts/InGame/ManeColor.cs` | Rename two values (§4.2) |
| `Assets/Materials/Mane/ManeBlack.mat` → `ManeViolet.mat`, `ManeBrown.mat` → `ManeOrange.mat` (+ `.meta`) | Rename, keeping the GUIDs; new colours (§4.2) |
| `Docs/AI/Architecture/Architecture.md`, `Dependencies.md` | §4.3 |

**Dependencies:**
- **`PonyBase.prefab` refers to the materials by GUID.** A rename inside Unity keeps the `.meta`, so the prefab needs **no** change. Don't delete and recreate the materials.
- **No network change:** `NetworkManeColor` keeps its name, type and values 0–4. A build with the old palette still plays against a new one (each side just shows its own material for the same index), but test with one build anyway.

## 4. Required changes

### 4.1 N63: treat "lobby not found" as expected while leaving

In `MultiplayerManager`, next to `ShouldShowConnectionLost` / `IsOwnShutdownTail` (same style):

```csharp
// N63: a "lobby not found" answer is worth a warning only while we still hold that lobby and aren't leaving.
internal static bool ShouldReportMissingLobby(bool isDisconnecting, string currentLobbyId, string requestedLobbyId) =>
    !isDisconnecting && currentLobbyId != null && currentLobbyId == requestedLobbyId;
```

In `RefreshCurrentLobbyCore`, **before** the existing `catch (LobbyServiceException e)`:

```csharp
catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.LobbyNotFound)
{
    // N63: the lobby was deleted while this refresh was in flight (our own Disconnect, or the host left).
    if (ShouldReportMissingLobby(m_onDisconnecting, m_currentLobby?.Id, _lobbyId))
        Debug.LogWarning("MultiplayerManager: the current lobby no longer exists.");
    return false;
}
```

- `Lobby` is a plain C# class, so `m_currentLobby?.Id` is safe.
- **Keep everything else:** the `RateLimited` warning, `LogError` for other reasons, the general `catch`, the return values, `m_currentLobby` handling and the throttling.
- **Don't change** `ShutdownHostInternal`, `Disconnect`, `CleanupAfterDisconnect` or N61's guard.

### 4.2 The palette: rename two values and two materials

- **`ManeColor`:**

  ```csharp
  // Mane colour pool (N27; palette from poll #15 Q1). The order is the index into PonyColorScript.m_maneMaterials.
  public enum ManeColor
  {
      White,
      Violet,
      Orange,
      Gold,
      Pink
  }
  ```

  Black → Violet and Brown → Orange keep their indices 1 and 2. **Don't reorder.**
- **Materials,** renamed in the Unity Project window (or with `AssetDatabase.RenameAsset`), so each `.meta` and GUID stays:

  | Old | New | `_BaseColor` and `_Color` (RGB 0–1) |
  |---|---|---|
  | `ManeBlack` | `ManeViolet` | (0.58, 0.30, 0.88) |
  | `ManeBrown` | `ManeOrange` | (1.00, 0.50, 0.08) |

  `ManeWhite`, `ManeGold` and `ManePink` stay as they are.
- **Distinct from the team colours** (red (0.97, 0, 0), blue (0.05, 0, 0.92), green (0.26, 0.99, 0.08)): violet has clearly more red than the blue team colour; orange has half-green where the red team has none.
- **After the rename,** `PonyBase.prefab`'s `m_maneMaterials` must still list five materials in the order White, Violet, Orange, Gold, Pink, with no missing reference. `git diff` must show no change to the prefab.

### 4.3 Docs

- **`Architecture.md`,** the "Pony appearance" bullet: the palette is white, violet, orange, gold, pink (poll #15 Q1).
- **`Dependencies.md`:**
  - RP22: "renaming a value or a material is safe (indices and GUIDs stay); never reorder or delete."
  - The `MultiplayerManager` entry: `RefreshCurrentLobbyCore` treats `LobbyNotFound` as expected while leaving (N63).

## 5. Explicit non-goals

- **No change to how colours are chosen,** kept or replicated (TASK-N27 stays as accepted).
- **No colour preview** in the lobby or HUD, no player choice.
- **No other lobby error handling:** join, create, query and heartbeat stay as they are. No new message for players.
- **No change to the Disconnect order** or to `m_currentLobby`'s lifetime.

## 6. Constraints and compatibility requirements

- **Keep serialized names and GUIDs.** The material renames must keep their `.meta` files.
- **No new NetworkVariable or RPC.**
- **N34:** the new warning is fine in release (it means something real); the silent path logs nothing.
- **Unity 6 APIs, no warnings of our own.** Keep baseline copies in `build/n63-palette-verification/baseline/`.
- Restore the dynamic font before handing over (`AGENTS.md`, N28).

## 7. Acceptance criteria

1. **N63, decision table** (`ShouldReportMissingLobby`):

   | `isDisconnecting` | current ID | requested ID | Result |
   |---|---|---|---|
   | true | "L1" | "L1" | false |
   | false | null | "L1" | false |
   | false | "L2" | "L1" | false |
   | false | "L1" | "L1" | true |

2. **N63, the catch:** with `LobbyNotFound`, `RefreshCurrentLobbyCore` returns `false` and logs nothing while leaving, or one warning while the lobby is still current. Other reasons keep today's logging.
3. **Palette:** `ManeColor` has five values in the order White, Violet, Orange, Gold, Pink. `m_maneMaterials[1]` is `ManeViolet`, `[2]` is `ManeOrange`, with the colours in §4.2. The GUIDs of both materials are unchanged.
4. **No regression:** TASK-N27's harness checks still pass (two different colours, kept through a respawn, picked again in a new match, no mane warning, the selection highlight). The release player builds with 0 errors and no warnings of our own.

## 8. Regression tests

- **Edit Mode** (temporary, `Temp/N63Verification/`):
  - AC1: the four rows of the table.
  - AC3: load `PonyBase.prefab`, read `m_maneMaterials` by reflection; check names, GUIDs and `_BaseColor`.
- **The catch (AC2):** the service can't be made to answer 404 in a harness. Check the clause by reading the compiled method, or by a test seam that hands `RefreshCurrentLobbyCore`'s catch a `LobbyServiceException(LobbyExceptionReason.LobbyNotFound, …)`; report which you used. Don't change the method's signature for the test.
- **Play Mode:** re-run TASK-N27's host harness (`build/n27-verification/harness/`) against the new palette (AC4).
- **Baseline:** record the old enum names and the two old material colours.
- **Screenshot** (into `build/n63-palette-verification/`): the five manes side by side, or the in-match pair over several matches.
- Remove the harness from `Assets` and `Temp`; keep a copy in `build/n63-palette-verification/`.

## 9. Manual Unity Editor checks

**Setup:** two Editors (main = host, clone = client, `-logFile C:\Lesson\clone-editor.log`).

1. **Palette:** play three or four matches. The manes are only white, violet, orange, gold or pink, two different ones per match, the same on both peers.
2. **Readability:** next to the red and the blue body, violet and orange are easy to tell apart from the body colour.
3. **N63, host:** create a lobby, wait 5 s in the current-lobby view, press Disconnect. Repeat about ten times, once with a client joined. The host's log has no red `lobby not found`. (A yellow "the current lobby no longer exists" warning would mean the lobby vanished while we still held it: note when.)
4. **N63, client:** the client joins, the host presses Disconnect: the client gets "Connection to the host was lost." as before, and no red `lobby not found` in the clone's log.
5. **Logs:** no new warning or exception in either Editor log.

## 10. Open questions and assumptions

- **A1, the two new shades** (violet (0.58, 0.30, 0.88), orange (1.00, 0.50, 0.08)) are my proposal. Retune them any time in the two materials; no code change.
- **A2, "pink violet"** in your answer is read as two colours, pink and violet, which makes five. If you meant one pink-violet shade, name the fifth colour.
- **A3, poll #16 Q2** (unused asset packs) may add a clean-up step to this task if you answer b or c. I'll add it to this spec before Codex starts.
