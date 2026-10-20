# TASK-L6: Stop the per-frame allocations and rework in the UI and card views

- **Source issue:** [Issue Report #7 → L6](../Reports/2026-10-20_IssueReport-7.md#-low-23)
- **Severity:** 🟢 Low (Confirmed)
- **Baseline:** `b7e7b91`. Line numbers below are from this commit.
- **Order:** 15 of 20.
  - After TASK-N24: `CardWorldViewManager.cs` is UTF-8 then.
  - After TASK-L1: `GameAnnouncerUI` shows the countdown then. Keep it.
  - `DisconnectMenuUI` and `SpawnPanelUI` are deleted by TASK-L10, so they aren't covered here.
- **Implementer:** Codex

## 1. Objective

The listed `Update` methods do work only when their inputs change. There are no per-frame `LocalizedString` allocations, no per-frame `FindObjectsByType`, and no per-frame UI resets. Everything looks and behaves exactly as before.

## 2. Current behavior

| Where | Per-frame work |
|---|---|
| `UI/GameAnnouncerUI.cs:41-98` (`Update` → `UpdateAnnounceText`) | Every frame: 2–3 `new LocalizedString(…)` in `GetLocalized` (`:152-159`), several string concatenations, `SetAnnouncerText` → `ApplyAnnouncerTextFormat()` + `ForceMeshUpdate()` (`:161-170`), even when nothing changed. Also `SessionManager.Instance.LocalPlayer` is read without a null check on `Instance` (`:71`). |
| `Manager/CardWorldViewManager.cs:74-88` (`Update`) | It compares `SessionManager.Instance.m_playerSessions.Count` with `m_registeredSessions.Count`. `UnregisterSession` (`:343-365`) has **no callers**, so after a player leaves, the counts differ until the scene unloads. Every frame it then runs `RegisterAllSessions()`, `FindAreas()` (`FindObjectsByType<PlayerCardWorldArea>`) and `RefreshAllSessions()`. |
| `UI/PlayerUI.cs:87-…` (`Update`) | While `SessionManager.Instance.LocalPlayer` is null (scene start, after a disconnect), it calls `ClearLocalUI()` every frame (`:101-105`): unsubscribes, empties texts, destroys or clears hand views. |
| `UI/LobbyControlUI.cs:31-38` (`Update`) | `RefreshUI()` every frame: two `SetActive` calls (`:87-100`). Cheap, but needless. |
| `UI/LeaveMatchUI.cs:25-29` | Copies the canvas size **only when it differs** (`sizeDelta != rect.size`). Already change-only. **No change needed.** Listed in L6 by report #7; this task closes that sub-item. |

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/UI/GameAnnouncerUI.cs` | Cache the localized strings; rebuild the text only on a change |
| `Assets/Scripts/Manager/CardWorldViewManager.cs` | Unregister departed sessions, so the counts converge |
| `Assets/Scripts/UI/PlayerUI.cs` | Clear the local UI once per loss of the local player, not every frame |
| `Assets/Scripts/UI/LobbyControlUI.cs` | Apply the panels only when the connected state changes |

## 4. Required changes

1. **`GameAnnouncerUI`:**
   1. Replace `GetLocalized(key, args)` with a cached lookup: a `Dictionary<string, LocalizedString>` filled on first use. For keys with an argument (the colored part), set the argument on the cached instance (`Arguments = new object[] { … }`, or a `StringVariable` as other UI classes do) before `GetLocalizedString()`.
   2. Build a small state key each frame from:
      - phase
      - seconds left (`GameMaster.GetPhaseSecondsLeft()` after TASK-L1)
      - target session ID
      - whether it's the local player
      - team
      - the current locale (`LocalizationSettings.SelectedLocale`)

      Use a struct or a few cached fields, not a string. Rebuild and call `SetAnnouncerText` only when the key differs from the last one.
   3. Null-check `SessionManager.Instance` before `.LocalPlayer`.
   4. The visible text and formatting must be identical to today's for every phase and locale.
2. **`CardWorldViewManager.Update`:** before the count comparison, unregister every session in `m_registeredSessions` that is null, destroyed, not spawned, or no longer a value in `SessionManager.Instance.m_playerSessions`. Use the existing `UnregisterSession`, iterating over a copy (as `UnregisterAllSessions` does). Then keep the existing register, find and refresh logic for the case where a session was **added**. Result: after a player leaves, the counts match again and the per-frame work stops.
3. **`PlayerUI.Update`:** call `ClearLocalUI()` only if there is something to clear, i.e. `m_playerSession != null` or the HUD isn't yet cleared. Track this with a private `bool m_isLocalUiCleared`: set it in `ClearLocalUI()`, reset it when a session is bound (`:107-135`). The message-hide logic at the top of `Update` stays unchanged.
4. **`LobbyControlUI`:** keep `Update`, but remember the last `_connected` value in a nullable field, and call `SetActive` only when it changes. Call `RefreshUI()` from `Start` and after `OnClickCreateLobby` as now.

## 5. Explicit non-goals

- No change to any text, color, layout, localization key or timing.
- No event-system rework (keep `Update`, just skip unchanged work).
- No change to `LeaveMatchUI`.
- No change to `DisconnectMenuUI` / `SpawnPanelUI` (TASK-L10 deletes them).

## 6. Constraints and compatibility requirements

- Cached `LocalizedString` objects must still follow a locale change. Include the locale in the state key, so the text is rebuilt on a change.
- `CardWorldViewManager` runs on host and client. On clients, `m_playerSessions` is rebuilt from `SessionDataList` (`SessionManager.cs:129-137`), so the pruning works there too.
- No new serialized fields.

## 7. Acceptance criteria

1. During a phase with an unchanged second, `GameAnnouncerUI` allocates nothing per frame (Profiler: 0 B GC alloc in `GameAnnouncerUI.Update`), and `ForceMeshUpdate` runs at most once per second.
2. After the opponent leaves, `CardWorldViewManager.Update` no longer calls `FindObjectsByType` every frame. Use a temporary counter or the Profiler.
3. `PlayerUI.ClearLocalUI()` runs once, not every frame, while there's no local player.
4. `LobbyControlUI` toggles its panels only when the connected state changes.
5. All UI looks the same as before in en, ru and ja: announcer, HUD, lobby panels.

## 8. Regression tests

- Harness: the announcer state key changes on each of its inputs, and not otherwise.
- Harness: `CardWorldViewManager` with a departed session → unregistered, and the counts match.
- Roslyn compile. Exit 0.

## 9. Manual Unity Editor checks

1. Two-player match in en, then ru and ja. The announcer text for every phase, the countdown and the colored player part are as before. Switch the locale mid-match with the Editor's Localization Scene Controls: the announcer follows within a second.
2. The Profiler (host) during Card Selection: no per-frame GC allocation from `GameAnnouncerUI`.
3. The client leaves mid-match: the host's world card areas still look correct until the result screen. No per-frame `FindObjectsByType` spike in the Profiler.
4. LobbyScene: create, join, leave. The search and current-lobby panels switch as before.

## 10. Open questions and assumptions

- Assumption: TASK-L1 is merged first. If not, use the current timer value (as an `int`) in the state key instead of `GetPhaseSecondsLeft()`.
