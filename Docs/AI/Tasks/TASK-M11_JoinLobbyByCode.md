# TASK-M11: Park the join-by-code code (comment it out for now)

- **Source issue:** [Issue Report #6 → M11](../Reports/2026-10-19_IssueReport-6.md#-medium-13) (first reported in #1)
- **Severity:** 🟡 Medium (Confirmed: the feature is dead code)
- **Baseline:** `fe2d4fb`. Line numbers below are from this commit.
- **Order:** 5 of 13. Independent; best done after M10, which edits the same file.
- **Owner decision (2026-10-19):** **comment the code out for now.** Don't wire it and don't delete it.
- **Implementer:** Codex

## 1. Objective

The unused, non-working join-by-code code no longer compiles into the game. It stays in the file as commented-out code, so it can be restored later.

## 2. Current behavior

In `Assets/Scripts/MultiplayerScripts/MultiplayerManager.cs`:
- `private string m_joinCodeInput = "";` (`:28`) is never assigned.
- `public async Task JoinLobbyByJoinCode()` (`:863-906`) reads that field, so it only logs "Join code is empty!". Nothing calls it: no script, no scene, no prefab `onClick`.
- The file already parks unused code like this: `//todo Dont use rn` followed by a `/* ... */` block around `InitUnityServices` (`:548-564`).

These are **not** part of this task and stay as they are:
- `SetLobbyCode()` (`:1414-1418`), which `StartHost` and `JoinLobbyById` call
- the serialized fields `m_lobbyCode` / `m_lobbyCodePanel` (`:37-38`), referenced in `MultiplayerManager.prefab:48-49` and two legacy scenes

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/MultiplayerScripts/MultiplayerManager.cs` | Comment out one field and one method |

## 4. Required changes

1. **`:28`:** replace the field with a commented-out line plus a note:
   ```csharp
   //todo Join by lobby code is parked (M11). Restore together with JoinLobbyByJoinCode.
   //private string m_joinCodeInput = "";
   ```
2. **`:863-906`:** comment out the whole `JoinLobbyByJoinCode()` method, following the existing pattern at `:548-564`:
   ```csharp
   //todo Join by lobby code is parked (M11): no UI calls it yet. See §10 of TASK-M11 before restoring.
   /*
   public async Task JoinLobbyByJoinCode()
   {
       ...unchanged body...
   }
   */
   ```
   The body contains no `*/`, so a block comment is safe. Don't change the body.
3. Nothing else changes in the file.

## 5. Explicit non-goals

- Wiring an input field or showing the lobby code (option A from the first draft of this spec).
- Deleting the code.
- Touching `m_lobbyCode`, `m_lobbyCodePanel` or `SetLobbyCode()`. They're serialized or still used.
- M10's error handling and M13's strings, apart from leaving the commented block as it is.

## 6. Constraints and compatibility requirements

- Removing the public `JoinLobbyByJoinCode()` from compilation is allowed: it has no callers. A search for `JoinLobbyByJoinCode` and `m_joinCodeInput` in `Assets/` must find only the commented lines.
- No asset changes.

## 7. Acceptance criteria

- [ ] The field and the method exist only as comments, with the `//todo ... (M11)` notes.
- [ ] Compiles with no new errors or warnings.
- [ ] Creating, joining from the list and starting a match work as before.

## 8. Regression tests

No automated tests. Review the diff: comment markers only, and the body text is unchanged.

## 9. Manual Unity Editor checks

1. Let Unity recompile. The Console has no new errors or warnings.
2. ParrelSync: create a lobby, join from the list, start, and play one round. No change.

## 10. Open questions and assumptions

**Notes for whoever restores the feature later.** The commented method has known problems compared with the working `JoinLobbyById` (`:785-847`). Fix them when restoring:
- it doesn't call `UnsubscribeNetworkEvents()` / `SubscribeNetworkEvents()` around `RecreateNetworkManager()`
- it sends no player data (`JoinLobbyByCodeOptions { Player = CreateLobbyPlayerData(...) }`), so the lobby shows "Unknown Player"
- it doesn't check that the `"joinCode"` key exists
- it has no `catch`; follow the M10 pattern (leave the lobby and shut down on failure)
- it needs a UI: a code input field and button in the lobby search panel, and the lobby code shown in the current-lobby panel (`Lobby.LobbyCode` is filled in only for members)
