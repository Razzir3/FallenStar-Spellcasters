# TASK-M13: Remove hard-coded user-visible strings from the lobby

- **Source issue:** [Issue Report #6 → M13](../Reports/2026-10-19_IssueReport-6.md#-medium-13) (first reported in #1)
- **Severity:** 🟡 Medium (Confirmed: breaks the en/ru/ja rule)
- **Baseline:** `fe2d4fb`. Line numbers below are from this commit.
- **Order:** 6 of 13. Do it after M10 and M11 (same file, `MultiplayerManager`).
- **Implementer:** Codex

## 1. Objective

Nothing in the lobby shows fixed English text. Anything stored in the shared Lobby service data is language-neutral, because the host and the other player can use different languages. Words are added only on the viewer's side, from `UI_table`.

## 2. Current behavior

| Location | String | Where it shows |
|---|---|---|
| `MultiplayerScripts/MultiplayerManager.cs:594`, `:808` | `CreateLobbyPlayerData("Host")` / `("Client")` | Role value **and** name prefix |
| `MultiplayerManager.cs:1363` | `playerName = playerRole + "_" + _shortId`, e.g. `Host_a1b2c3` | Player name in both lobby panels (`LobbyPlayerItemUI`) |
| `MultiplayerManager.cs:613` | Lobby name `"MyLobby"` | Lobby list (`LobbyListItemUI`) and "Selected Lobby：{lobbyName}" (`LobbyListUI`) for **other** players |
| `UI/LobbyListUI.cs:209` | Fallback `"Unknown Lobby"` | Status text |
| `UI/LobbyPlayerItemUI.cs:50` | Fallback `"Unknown Player"` | Player name |
| `UI/LobbyListItemUI.cs:45` | `count + " / " + max` | List item player count |

**Already localized:** the role label (`role_host` / `role_client`, `LobbyPlayerItemUI.SetRoleText`), and the player count in the selected-lobby panel (`lobby_players_count` = "Players: {currentPlayers} / {maxPlayers}", used by `LobbyPlayerListUI`).

`LobbyPlayerListUI` decides who the host is with `player.Id == lobby.HostId` (`:94`). The `"role"` data value isn't read anywhere for display. A text search over `.text =` assignments found no other hard-coded literals in `Assets/Scripts`.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/MultiplayerScripts/MultiplayerManager.cs` | Role constants, a neutral player name, a neutral lobby name |
| `Assets/Scripts/UI/LobbyListUI.cs` | Localized "unknown lobby" fallback |
| `Assets/Scripts/UI/LobbyPlayerItemUI.cs` | Localized "unknown player" fallback |
| `Assets/Scripts/UI/LobbyListItemUI.cs` | Localized player count and name fallback |
| `Assets/Localization/UI_table*` | 2 new keys in en/ru/ja |

## 4. Required changes

1. **`MultiplayerManager`:**
   1. Add the protocol constants `private const string m_lobbyRoleHost = "Host";` and `private const string m_lobbyRoleClient = "Client";`, named like `m_maxLobbyPlayers`. Comment them as lobby data values that are never shown. Use them at `:594` and `:808`. Leave the commented-out join-by-code block (M11) unchanged.
   2. Add a private helper `GetShortPlayerId()`: the first 6 characters of `AuthenticationService.Instance.PlayerId`. If that's empty, return `UnityEngine.Random.Range(100000, 1000000).ToString()`, so the value is never empty and never a word.
   3. `CreateLobbyPlayerData`: set `playerName` to `GetShortPlayerId()` only, with no role prefix. Keep the `"role"` entry with the constant value.
   4. `StartHost`: replace the name `"MyLobby"` with `GetShortPlayerId()`. The lobby is then listed under the host's short ID, which reads the same in every language.
2. **`LobbyListUI`:** add `[SerializeField] private LocalizedString m_unknownLobbyString = new LocalizedString("UI_table", "lobby_name_unknown");`. In `SetSelectedLobbyStatus`, use `m_unknownLobbyString.GetLocalizedString()` instead of `"Unknown Lobby"`.
3. **`LobbyPlayerItemUI`:**
   - add `[SerializeField] private LocalizedString m_unknownPlayerString = new LocalizedString("UI_table", "lobby_player_unknown");`
   - pass `null` as the default to `GetPlayerDataValue`; if the result is null, use the localized fallback
   - remember whether the fallback is in use, and in `OnSelectedLocaleChanged` set the name text again when it is
4. **`LobbyListItemUI`:**
   - **Player count:** add `[SerializeField] private LocalizedString m_playersCountString = new LocalizedString("UI_table", "lobby_players_count");` with two `IntVariable`s named `currentPlayers` and `maxPlayers`, using the same pattern as `LobbyPlayerListUI` (`:106-110`).
     - subscribe `StringChanged` in `OnEnable` and unsubscribe in `OnDisable`
     - the handler writes to `m_playerCountText`
     - in `RefreshUI`, set the variable values instead of building the string
   - **Name:** if `m_lobby.Name` is empty, use `lobby_name_unknown`. Use a second `LocalizedString` field, or `GetLocalizedString()` in `RefreshUI`.
5. **Localization (`UI_table` shared data + en/ru/ja):**

   | Key | en | ru | ja |
   |---|---|---|---|
   | `lobby_name_unknown` | Unknown lobby | Неизвестное лобби | 不明なロビー |
   | `lobby_player_unknown` | Unknown player | Неизвестный игрок | 不明なプレイヤー |

The new `LocalizedString` fields have initializers, so the existing prefabs and scenes need no edits: Unity uses the initializer when a field is missing from the YAML. Don't hand-edit prefabs.

## 5. Explicit non-goals

- Player-chosen names or lobby names (no name input).
- `Debug.Log` texts, which aren't shown to players.
- The legacy `SpawnPanelUI` / `DisconnectMenuUI` (L10).
- L7 (card texts on a locale change), L8 (the `"btn_lobbyRefresh\t"` key).
- Changing the `"role"` data key or its values. Older builds don't matter, but keep the values stable anyway.

## 6. Constraints and compatibility requirements

- Lobby data stays language-neutral: IDs and numbers only.
- No public API changes. The new fields are additive and serialized with defaults.
- Keep the existing `LocalizedString` subscribe and unsubscribe patterns, so no handler leaks when items are destroyed.

## 7. Acceptance criteria

- [ ] The lobby list shows the host's 6-character ID as the lobby name, and "Players: 1 / 2" in the current language (en/ru/ja).
- [ ] Both lobby panels show each player's 6-character ID as the name, plus the localized role.
- [ ] Changing the locale while the lobby list is open updates the player counts.
- [ ] Search confirms no `"MyLobby"`, `"Unknown Lobby"`, `"Unknown Player"` or `" / "` user-visible literals remain in these files.
- [ ] Creating, joining and starting a match works as before.
- [ ] Compiles with no new warnings.

## 8. Regression tests

No automated tests. **Review checklist:**
- the `"role"` value is still `"Host"` / `"Client"`
- `playerName` and the lobby name contain no words
- every new `StringChanged` subscription has a matching unsubscribe

## 9. Manual Unity Editor checks

ParrelSync host + client.
1. The host creates a lobby. The client refreshes the list and sees an item named with the host's 6-character ID and "Players: 1 / 2".
2. Switch the client's locale to ru, then ja, with the Localization Scene Controls. The count text changes language, and the name stays the same.
3. The client selects and joins the lobby. Both panels show two 6-character IDs with the localized Host and Client roles.
4. Start a match and play one round: no change.

## 10. Open questions and assumptions

- **Q1 (owner):** Is the host's short ID acceptable as the lobby name? An alternative is a localized display such as "Lobby a1b2c3", built on the viewer's side with one more key. Easy to add later.
- **A1:** The "unknown" fallbacks almost never appear; they cover lobbies or players created without data.
