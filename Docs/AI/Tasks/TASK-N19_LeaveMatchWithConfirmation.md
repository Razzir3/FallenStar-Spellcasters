# TASK-N19: "Leave match" button with a confirmation dialog

- **Source issue:** [Issue Report #5 → N19](../Reports/2026-10-19_IssueReport-5.md#2-new-findings)
- **Owner decisions (2026-10-19):**
  - leaving needs a confirmation
  - leaving a running match **counts as a defeat** (confirmed)
- **Severity:** 🟡 Medium (feature gap: today the only way out of a match is to kill the process)
- **Order:** 5 of 5 (H3 → H2 → N14 → N18_M1 → **N19**). Do it last: it touches `GameMaster.cs`, `GameMaster.Player.cs`, `PlayerSession.cs`, `ResultMenuUI.cs`, `PlayerUIPrefab`, `ResultScene` and `UI_table`.
- **Implementer:** Codex (the UI is built in the Unity Editor)
- **Localization requirement:** every new visible string goes in `UI_table` in **English, Russian and Japanese** (`AGENTS.md`). No hard-coded text.

## 1. Objective

During a match, each player has a **Leave match** button. Pressing it opens a dialog: "Leave the match? It will count as a defeat." with **Leave** and **Cancel**.
- **Cancel** closes the dialog. Nothing else happens.
- **Leave** during a running match: the **server** ends the match as a forfeit. Both players go to `ResultScene` through the existing H5 path:
  - the leaver sees **Defeat** with "You left the match."
  - the opponent sees **Victory** with "Your opponent left the match."
- **Leave** before the match loop has started (phase `None`): disconnect and return to the lobby, the same as today's lobby "Leave".

This works the same for the host and the client. A host who leaves must not leave the client without a result. That's why the forfeit goes through the server's `EndMatch` and not through a plain disconnect.

## 2. Current behavior

- `GameScene` and `PlayerUIPrefab` have no leave, surrender or disconnect control. The only button bindings are `ConfirmTargetButtonUI.ConfirmSelect`, `ConfirmSendButtonUI1.TryConfirmSend` and `PlayerUI.ConfirmTurn`. `PlayerUIPrefab` children include `ConfirmBtn`, `SendBtn`, `ConfirmTurnBtn`, `PhaseAnouncer` and the HP, life and mana texts.
- `MultiplayerManager.Update` (`MultiplayerScripts/MultiplayerManager.cs:138-158`) toggles a `DisconnectMenuUI` with Tab. That menu exists only in the disabled legacy scenes (`SampleScene`, `MultiplayerScene`), so it does nothing in `GameScene`.
- **Match end path (H5):**
  1. `GameMaster.EndMatch(PlayerSession winner, MatchEndReason reason)` (`InGame/GameMaster.cs:414-445`) is server-only and idempotent (`m_isMatchEnded`). It sets `GamePhase.GameOver` and calls `MatchEndedClientRpc`.
  2. On each peer, `MatchEndedClientRpc` (`:447-458`) calls `MatchResultData.Set(isWinner, isDraw, reason)`, then `MultiplayerManager.LeaveMatchToResultScene()`. That shuts down the network: the host waits up to 3 s for the client.
- **Forfeit on disconnect (H6):** `GameMaster.HandlePlayerLeft(PlayerSession)` (`InGame/GameMaster/GameMaster.Player.cs:6-49`). The guards are: server, a running loop, phase not `None`/`GameOver`, session in `m_players`. Then it stops the coroutine, picks the remaining player and calls `EndMatch(winner, MatchEndReason.OpponentDisconnected)`.
- `MatchEndReason` (`GameMaster.cs:22-26`): `LivesDepleted`, `OpponentDisconnected`.
- `ResultMenuUI.Start` (`UI/ResultMenuUI.cs:37-47`) maps `LivesDepleted` to win, lose or draw reasons, and maps `OpponentDisconnected` to `m_reasonOpponentLeft` (key `result_reason_opponentLeft`) for **any** outcome.
- RPC pattern to follow: `PlayerSession.ConfirmTurn` → `[ServerRpc] ConfirmTurnServerRpc(..., ServerRpcParams rpcParams = default)` → `GameMaster.HandleConfirmTurn(rpcParams.Receive.SenderClientId, this, ...)`, which checks `OwnerClientId == senderClientId`.
- Existing keys: `btn_back`, `btn_exit`, `result_reason_opponentLeft`, `btn_toMainMenu`. There's no "Cancel" key.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/GameMaster.cs` | Append `MatchEndReason.Surrendered` |
| `Assets/Scripts/InGame/GameMaster/GameMaster.Player.cs` | New `HandlePlayerSurrender(PlayerSession)` |
| `Assets/Scripts/InGame/PlayerSession.cs` | `RequestLeaveMatch()` + `LeaveMatchServerRpc` |
| `Assets/Scripts/UI/LeaveMatchUI.cs` | **New** `MonoBehaviour`: button, dialog and fallback |
| `Assets/Scripts/UI/ResultMenuUI.cs` | New field `m_reasonYouLeft` and a `Surrendered` case |
| `Assets/Prefab/UiPrefab/PlayerUIPrefab.prefab` | Leave button and confirmation panel (in the Editor) |
| `Assets/Scenes/ResultScene.unity` | Assign `ResultMenuUI.m_reasonYouLeft` (in the Editor) |
| `Assets/Localization/UI_table*` | 5 new keys in en/ru/ja |

## 4. Required changes

### 4.1 Server

1. **`MatchEndReason`:** append `Surrendered` as the **last** value. Don't reorder: the value is sent as an int in `MatchEndedClientRpc`.
2. **`GameMaster.HandlePlayerSurrender(PlayerSession session)`** (public, in `GameMaster.Player.cs`):
   - Return unless all of these hold:
     - `IsServer` and `session != null`
     - `m_gameCoroutine != null`
     - the phase is not `None` or `GameOver`
     - `m_players.Contains(session)`

     These are the same guards as `HandlePlayerLeft`.
   - `StopCoroutine(m_gameCoroutine); m_gameCoroutine = null;` then `m_isResolvingCards = false; m_isWaitingRespawn = false;`
   - Winner: the first player in `m_players` that isn't `session`, isn't `null` and has `TeamId.Value != -1`.
   - If there's no winner, call `AbortMatch("player_surrendered_no_winner")` and return.
   - Otherwise call `EndMatch(winner, MatchEndReason.Surrendered)`. Under `m_debugMode`, log `"GameMaster: player {session.OwnerClientId} surrendered."`
   - **Keep the surrendering session in `m_players`.** It's still connected and receives `MatchEndedClientRpc`, and `EndMatch` also logs its suspicious-activity summary.
   - **Don't change `HandlePlayerLeft`** (H6). The small duplication is accepted to keep H6 as it is.
3. **`PlayerSession`:**
   - `public void RequestLeaveMatch()`: return if `!IsOwner`; otherwise call `LeaveMatchServerRpc()`.
   - `[ServerRpc] private void LeaveMatchServerRpc(ServerRpcParams rpcParams = default)`:
     - If `rpcParams.Receive.SenderClientId != OwnerClientId`, call `ReportSuspicious(SuspiciousActionType.RpcSenderMismatch, ...)` on the sender's session (look it up with `SessionManager.Instance.GetPlayerSession`), following the pattern in `HandleConfirmTurn`, and return.
     - If `GameMaster.Instance == null`, return.
     - Otherwise call `GameMaster.Instance.HandlePlayerSurrender(this)`.

### 4.2 Client: `LeaveMatchUI` (new script)

4. Serialized fields:
   - `Button m_leaveButton`
   - `GameObject m_confirmPanel`
   - `float m_leaveFallbackTimeout = 5f`
   - `bool m_debugMode`
5. Behavior:
   - `Start`: hide `m_confirmPanel`.
   - `Update` (polling, as in `PlayerUI`):
     - `m_leaveButton.interactable` is `true` only when `GameMaster.Instance != null`, the phase isn't `GameOver`, and the player isn't already leaving
     - when the phase becomes `GameOver`, hide the panel
   - `public void OnClickLeave()`: show `m_confirmPanel`.
   - `public void OnClickCancelLeave()`: hide `m_confirmPanel`.
   - `public void OnClickConfirmLeave()`:
     1. Guard with a private `bool m_isLeaving`. Set it, then hide the panel.
     2. Running match (`GameMaster.Instance != null`, the phase isn't `None`/`GameOver`, and `SessionManager.Instance?.LocalPlayer != null`, written with explicit null checks): call `LocalPlayer.RequestLeaveMatch()` and start the fallback deadline (`Time.unscaledTime + m_leaveFallbackTimeout`).
     3. Otherwise (match not started, or no `GameMaster`): call `MultiplayerManager.Instance.Disconnect()`, with a null check and `Debug.LogError` if it's null.
   - **Fallback:** if the deadline passes and the phase still isn't `GameOver`, call `MultiplayerManager.Instance.Disconnect()`. This covers a lost RPC or a server that ignored the request in a race. Clear the deadline once it fires or once `GameOver` is seen.

### 4.3 Result screen

6. **`ResultMenuUI`:**
   - add `[SerializeField] private LocalizedString m_reasonYouLeft;`
   - add `case MatchEndReason.Surrendered:` the winner gets `m_reasonOpponentLeft`, the loser gets `m_reasonYouLeft`. A draw can't happen; treat it like the winner case.
7. **`ResultScene`** (Editor): assign `m_reasonYouLeft` → `UI_table/result_reason_youLeft`.

### 4.4 UI setup (Unity Editor, `PlayerUIPrefab`)

8. Add a child `LeaveMatch` with the `LeaveMatchUI` component, containing:
   - **`LeaveBtn`:** a small button in a free corner, for example top-right, that doesn't overlap the announcer, mana or hand areas. TMP label with `LocalizeStringEvent` → `btn_leaveMatch`. `onClick` → `LeaveMatchUI.OnClickLeave`.
   - **`LeaveConfirmPanel`:** inactive by default. A full-screen semi-transparent `Image` with **raycast target on**, so clicks don't reach the cards or the world behind it. A centered box with:
     - TMP text, `LocalizeStringEvent` → `leave_confirm_text`
     - `LeaveConfirmBtn`: label `btn_leaveConfirm`, `onClick` → `OnClickConfirmLeave`
     - `LeaveCancelBtn`: label `btn_cancel`, `onClick` → `OnClickCancelLeave`
9. Every text uses the MPLUSRounded1c TMP font asset, like the other `PlayerUIPrefab` texts, so Japanese renders.
10. Assign `m_leaveButton` and `m_confirmPanel`. Save the prefab through the Editor or `PrefabUtility`. Don't hand-edit the YAML.

### 4.5 Localization keys (`UI_table`, through the Localization Tables window)

| Key | en | ru | ja |
|---|---|---|---|
| `btn_leaveMatch` | Leave match | Покинуть матч | 試合を離れる |
| `leave_confirm_text` | Leave the match? It will count as a defeat. | Покинуть матч? Это будет засчитано как поражение. | 試合を離れますか？敗北として扱われます。 |
| `btn_leaveConfirm` | Leave | Покинуть | 離れる |
| `btn_cancel` | Cancel | Отмена | キャンセル |
| `result_reason_youLeft` | You left the match. | Вы покинули матч. | あなたは試合から退出しました。 |

The texts are proposals; the owner may adjust them. Don't touch the existing key `"btn_lobbyRefresh\t"` (L8).

## 5. Explicit non-goals

- The Tab / `DisconnectMenuUI` legacy code in `MultiplayerManager.Update`. Leave it unchanged.
- An Esc shortcut, pausing the match, or blocking WASD while the dialog is open.
- Leave controls in `LobbyScene` (they already exist) or `ResultScene` (H10 already has Main menu).
- Changing H6 (`HandlePlayerLeft`), H5 (`EndMatch`, `LeaveMatchToResultScene`) or N14.
- Reconnect (RESEARCH-H6C).
- Statistics or penalties for leaving.

## 6. Constraints and compatibility

- No renames. `MatchEndReason` gets one value appended. All other changes are additive.
- Server authority: the client only **requests** the forfeit, and the server decides the winner. A client can only surrender **its own** session (owner-only RPC plus the sender check).
- `EndMatch` and `AbortMatch` are idempotent. A double click, a surrender racing a disconnect, or a surrender racing a natural match end must give exactly one result.
- The new `.cs` file gets its `.meta` from Unity. Prefab and scene changes are made only in the Editor.
- Follow the existing `[ServerRpc]` + `ServerRpcParams` style. Don't introduce the new `[Rpc]` attribute style.

## 7. Acceptance criteria

- [ ] The Leave match button is visible during the match, on host and client.
- [ ] Clicking it opens the confirmation dialog. **Cancel** closes it, and the match continues unchanged.
- [ ] **Client leaves** (confirm): the client sees **Defeat** / "You left the match."; the host sees **Victory** / "Your opponent left the match.". Both end up in `ResultScene`.
- [ ] **Host leaves** (confirm): the same, with roles swapped. The client gets a proper Victory result, not the lobby.
- [ ] This works in every running phase: RoundBreak, CardSelection, PlayerAction (own and opponent's turn), RespawnWait.
- [ ] Leaving in phase `None` (before the loop starts) returns to the lobby with no error.
- [ ] During `GameOver` the button doesn't work and the dialog is closed.
- [ ] Double-clicking Leave gives one result. Leaving while the opponent disconnects at the same moment gives one result.
- [ ] Every new text is correct in en, ru and ja, renders without missing glyphs, and updates when the locale changes.
- [ ] Compiles without new warnings.

## 8. Regression tests

No automated tests are required. **Review checklist:**
- `MatchEndReason.Surrendered` is the last enum value
- `HandlePlayerSurrender` has the same guards as `HandlePlayerLeft`, and the surrendering session stays in `m_players`
- `LeaveMatchServerRpc` checks the sender
- `ResultMenuUI` handles `Surrendered` for both outcomes
- no hard-coded strings
- the panel blocks raycasts

## 9. Manual Unity Editor checks

1. ParrelSync host and client. In `CardSelection`, the client clicks Leave match → Cancel. Nothing changes. Then Leave match → Leave. Check the result titles and reasons on both windows. Press Main menu on both and start a new match.
2. Repeat with the **host** leaving during its own `PlayerAction` turn.
3. Leave during the opponent's turn, and during a `RespawnWait`.
4. Double-click Leave quickly: one result, no errors.
5. **Fallback (temporary, don't commit):** comment out the `HandlePlayerSurrender` call in `LeaveMatchServerRpc`. Confirm Leave: after about 5 s the leaver disconnects to the lobby, and the opponent gets the H6 forfeit victory. Revert.
6. Switch the locale to ru and ja and check the button, the dialog and the "You left the match." reason.
7. Check that clicks on the dimmed background while the dialog is open don't select cards or world objects.

## 10. Open questions and assumptions

- **A1 (confirmed by the owner):** leaving a running match counts as a **defeat** for the leaver and a **victory** for the opponent, shown on the result screen. This follows the existing H6 forfeit rule.
- **A2:** One dialog text covers both host and client.
- **Q1:** Is top-right the right place for the button? It can be moved in the prefab without code changes.
