# TASK-N59_N61_F1: Player messages: lost connection, lobby and network failures, ignored inputs, deck refill; a single cleanup after a host's own disconnect

- **Source:** [Issue Report #14](../Reports/2026-10-24_IssueReport-14.md): N59 (§3, poll P1 = c), N61 (§2, new), feature gap F1 (poll *Priorities*: Now), N60 in `MultiplayerManager.cs` (poll P4 = b).
- **Owner decisions (2026-10-23 poll):**

  | # | Decision |
  |---|---|
  | P1 | **c.** "Back to the Lobby as now, but with an on-screen message 'Connection to the host was lost.'" Comment: "Right now game running like Host + client, in future in will change in Server + clients. So better show connection was lost message." |
  | F1 | **Now:** lobby and network failures, an ignored Send, an ignored Confirm Turn during the opponent's action, and a deck refill get on-screen messages. |
  | P4 | **b.** Clean N60's cosmetic leftovers with the next task that touches the same file. |

- **Severity:**
  - 🟡 Medium: **N59** (Confirmed: owner observation, code path, checklist F1.3 / G1.6)
  - 🟢 Low: **N61** (Confirmed: NGO source and `Editor.log`)
  - F1 is a feature, not a defect.
- **Baseline:** `ff8a9dd`. Line numbers below are from that commit.
- **Implementer:** Codex
- **Order:** do this task **first**. TASK-F4_F6_N36 also edits `GameMaster.cs` and the `UI_table` tables, and TASK-UI7 lays out the new notice objects added here.

## 1. Objective

1. **N59:** when a client loses its connection during a lobby or a match, it returns to the Lobby as today **and shows "Connection to the host was lost."** The host quitting, crashing or stopping Play Mode, and the client's own network dropping, all look the same to the client, so they share this message.
2. **N61:** a host's own deliberate disconnect (Leave lobby, `AbortMatch`, `AbortGameStart`) runs the disconnect cleanup and loads `LobbyScene` **once**, not twice.
3. **F1, failures outside a match:** a failed sign-in, lobby creation, lobby join, lobby search or match start shows a short localized message on screen. So does a match that's aborted or cancelled.
4. **F1, inside a match:** an ignored Confirm (no card picked), an ignored Send (no confirmed target), a Confirm Turn or Send pressed during the opponent's Player action, and a deck refill show a short localized message in the existing HUD message line.

## 2. Current behavior

### 2.1 N59: no message when the host is gone

- **The client's path** (`MultiplayerScripts/MultiplayerManager.cs`):
  1. NGO raises `OnClientDisconnectCallback(LocalClientId)`.
  2. `OnClientDisconnected` (`:218-258`) calls `HandleLostConnectionToHost` (`:260-306`).
  3. That method removes the player from the lobby (awaited), clears the match and shuts NGO down.
  4. Its `finally` calls `CleanupAfterDisconnect` (`:1514-1552`), which runs `SceneFlowManager.LoadLobbyScene()`.
- `MatchResultData` is never set, and nothing tells the player why they're back in the Lobby.
- **The same happens:**
  - when the host leaves the lobby before a match
  - when the client's own network drops
- **The owner's checklist confirms today's behavior:** F1.3 (host stops Play Mode) and G1.6 (the build host closed with X). `Editor.log:5708` shows the client's `LobbyScene` load, with no message.

### 2.2 N61: the host cleans up twice after its own disconnect

- **`Disconnect()`** (`:308-353`) sets `m_onDisconnecting = true`. For a host it awaits `ShutdownHostInternal()` (`:477-512`), which deletes the lobby and calls `NetworkManager.Shutdown()`.
- **`Shutdown()` is deferred.** NGO sets `m_ShuttingDown` and finishes later in its update. When clients are connected, it waits for them to disconnect, up to 5 s (`ProcessServerShutdown`, NGO `NetworkManager.cs:531-577`).
- `Disconnect()` then runs `CleanupAfterDisconnect()` at once. That resets `m_onDisconnecting = false`, sets `InMenuStatus` and loads `LobbyScene`.
- **When the deferred `ShutdownInternal` runs,** NGO calls `ConnectionManager.InvokeOnClientDisconnectCallback(LocalClientId)` for a host (NGO `NetworkManager.cs:1664-1668`).
- **The second run:** `OnClientDisconnected(0)` sees the server's own local client and calls `HandleLostConnectionToHost()`. The guard is already reset, so it runs a **second** full cleanup and **loads `LobbyScene` again**.
- **Evidence:** `Editor.log` (2026-10-23 session, main Editor as host after a lobby was created): `Loaded scene 'Assets/Scenes/LobbyScene.unity'` at lines 5929 and 5941, 12 lines apart, with no scene in between.
- **Why `LeaveMatchToResultScene` doesn't have it:** it keeps `m_isLeavingToResult` set until `ShutdownInProgress` ends (`:426-431`), and `OnClientDisconnected` returns early while that flag is set (`:220-224`).
- **Consequences:** a second scene load (about 200 ms) and a second `SessionManager` / `GameMaster` cleanup. With this task's messages, the second load would also destroy a message that was just shown (§4.1 handles that anyway).

### 2.3 F1: failures appear only in the Console

| Situation | Where | Today |
|---|---|---|
| Sign-in fails (no internet, service error) | `UI/LoginUI.cs:5-19`; `AuthManager.SingInAnonymous` catches `AuthenticationException` / `RequestFailedException` only | Stays on Login, no message. Other exceptions escape the `async void`. |
| Create lobby fails | `StartHost` (`:599-679`): `"host was not started"` (`:620`), the two `catch` blocks (`:664-674`) | Error in the Console only |
| Join lobby fails | `JoinLobbyById` (`:853-929`): no join code (`:885`), relay join failed (`:894-897`), the two `catch` blocks (`:909-924`) | Error in the Console only |
| Lobby search fails | `FindAvailableLobbies` (`:726-851`), `catch` blocks (`:841-848`) | Returns an empty list, so the status reads "Lobby not found" |
| Match start fails in the lobby | `StartGameSceneWhenReady` (`:1237-1270`): connect timeout (`:1248`), not 2 lobby players (`:1261`) | Warning in the Console only |
| Match aborted | `GameMaster.AbortMatch` (`InGame/GameMaster.cs:486-516`), `MultiplayerManager.AbortGameStart` (`:1310-1318`) | Both players back in the Lobby, no message |
| Confirm with no card picked | `PlayerSession.TryConfirmTarget` (`InGame/PlayerSession.cs:790-962`), `"No card selected for control package."` (`:802`) | Warning only |
| Send with no confirmed target | `PlayerSession.TryHandlePackage` / `CheckPackage` (`:1073-1123`) | Warnings `"Send ignored: …"` only |
| Send during the opponent's action | `TryHandlePackage`: `CanPlayerSelectCardTargets` is false, so nothing is sent | Silent |
| Confirm Turn during the opponent's action | `PlayerSession.ConfirmTurn` (`:174-208`) sends the RPC. The server logs `"Only active player can confirm action phase."` (`GameMaster.PackageManaging.cs:391-395`). | Server warning only |
| Deck refill | `GameMaster.RefillDeck` (`InGame/GameMaster/GameMaster.Card.cs:237-264`) | Debug log on the server only |

**The message line that exists today:**
- `PlayerUI` has a one-line message text (`m_messageText`, the `ManaMessageText` object), `ShowMessage(LocalizedString)` (`UI/PlayerUI.cs:269-277`) and a 2-second hide (`:118-124`).
- It refreshes on a locale change (`:252-257`).
- It's used by `ShowManaNotEnough` and `ShowHandLimitDiscard` (`:259-267`).
- It exists only in GameScene.

### 2.4 N60 leftovers in `MultiplayerManager.cs`

- `SetLobbyCode` checks `m_debugMode` twice in a row (`:1554-1560`).
- The commented-out `InitUnityServices` block, with its `//todo Dont use rn` line (`:582-598`).

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/UI/PlayerNotice.cs` (**new**) | Static store for the current out-of-match notice (key + time) |
| `Assets/Scripts/UI/NoticeUI.cs` (**new**) | Shows the current notice in a scene, localized, for a fixed time |
| `Assets/Scripts/MultiplayerScripts/MultiplayerManager.cs` | N59 post, N61 guard, failure posts, `MarkMatchCancelled`, N60 cleanup |
| `Assets/Scripts/InGame/GameMaster.cs` | `MatchAbortedClientRpc` in `AbortMatch` |
| `Assets/Scripts/InGame/GameMaster/GameMaster.Card.cs` | `DeckRefilledClientRpc` in `RefillDeck` |
| `Assets/Scripts/InGame/PlayerSession.cs` | Client-side messages in `ConfirmTurn`, `TryConfirmTarget`, `TryHandlePackage` |
| `Assets/Scripts/UI/PlayerUI.cs` | Four new `LocalizedString` fields and `Show…` methods |
| `Assets/Scripts/UI/LoginUI.cs` | Sign-in failure notice; catch unexpected exceptions |
| `Assets/Scenes/LobbyScene.unity`, `Assets/Scenes/LoginScene.unity` | A new `NoticePanel` (Image + TMP text + `NoticeUI`) under the root canvas |
| `Assets/Localization/UI_table Shared Data.asset`, `UI_table_en/ru/ja.asset` | 11 new keys (§4.6) |
| `Docs/AI/Architecture/Architecture.md`, `GameLoop.md`, `Dependencies.md` | §4.8 |

**Dependencies:**
- **`MultiplayerManager` lives on `AppRoot`** (LoginScene, DontDestroyOnLoad). It exists in every scene. Scene UIs don't.
- **`GameMaster` and `PlayerSession` are NetworkBehaviours.** A new `[ClientRpc]` changes the RPC table, so both peers must run the same build (RP12). The ParrelSync clone shares `Assets`.
- **`HandleLostConnectionToHost` and `Disconnect` share** `m_onDisconnecting` and `CleanupAfterDisconnect` with `LeaveMatchToResultScene` (RP6). Don't change `LeaveMatchToResultScene`.
- **`LobbyControlUI.RefreshUI`** shows the search panel or the current-lobby panel. The notice must sit outside both panels, so it stays visible in either.

## 4. Required changes

### 4.1 `PlayerNotice` and `NoticeUI` (new)

**`PlayerNotice`** (static, like `MatchResultData`). It survives scene loads.

```csharp
public static class PlayerNotice
{
    public static event System.Action Changed;
    public static string Key { get; private set; }
    public static float PostedAt { get; private set; } = -1f;

    public static void Post(string key)
    {
        Key = key;
        PostedAt = Time.realtimeSinceStartup;
        Changed?.Invoke();
    }

    public static bool IsActive(float duration) =>
        !string.IsNullOrEmpty(Key) && PostedAt >= 0f &&
        Time.realtimeSinceStartup - PostedAt < duration;

    public static void Clear() { Key = null; PostedAt = -1f; Changed?.Invoke(); }
}
```

- **Don't add "consume" semantics.** A notice stays valid for its duration, and any `NoticeUI` that enables during that time shows it for the rest of the time. That way a second `LobbyScene` load, or one that comes a moment after the post, still shows it.
- A new `Post` replaces the previous notice.

**`NoticeUI`** (MonoBehaviour, on the new `NoticePanel` object):
- **Serialized fields:**
  - `m_root` (`GameObject`): the panel to show and hide. If null, use this GameObject's first child.
  - `m_text` (`TMP_Text`)
  - `m_duration` (`float`, default **6**)
- **`OnEnable`:** subscribe to `PlayerNotice.Changed` and `LocalizationSettings.SelectedLocaleChanged`, then `Refresh()`.
- **`OnDisable`:** unsubscribe from both.
- **`Update`:** if the notice is shown and `!PlayerNotice.IsActive(m_duration)`, hide it. Only compare state; don't format text every frame (RP15).
- **`Refresh()`:**
  - If `PlayerNotice.IsActive(m_duration)`: resolve the key once per key with a cached `new LocalizedString("UI_table", key)` and its `GetLocalizedString()`, set the text and activate `m_root`.
  - Otherwise deactivate `m_root`.
  - The locale handler calls `Refresh()`.
- **Clicks:** `NoticeUI` never blocks them. `raycastTarget` is off on the panel image and the text.
- **The `NoticeUI` component goes on the `NoticePanel` object itself,** which stays active. `m_root` is its child `Box`. Otherwise `OnEnable` / `Update` would stop once the box is hidden.

### 4.2 N59: post the message on a lost connection

In `HandleLostConnectionToHost` (`:260-306`):

1. **At entry,** after the `m_onDisconnecting` guard, capture:
   - `bool _wasServer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;`
   - `MultiplayerStatus _statusAtEntry = m_currentMultiplayerStatus;`
2. **In the `finally`,** right before `CleanupAfterDisconnect()`:

   ```csharp
   if (ShouldShowConnectionLost(_statusAtEntry, _wasServer, m_isMatchCancelled))
       PlayerNotice.Post("notice_connection_lost");
   ```

   Post it here, not at entry: the `RemovePlayerAsync` await can take seconds, and the 6-second window should start when the Lobby loads.
3. **Add the pure helper** so it can be tested:

   ```csharp
   internal static bool ShouldShowConnectionLost(MultiplayerStatus statusAtEntry, bool wasServer, bool matchCancelled) =>
       !wasServer && !matchCancelled &&
       (statusAtEntry == MultiplayerStatus.InLobbyStatus || statusAtEntry == MultiplayerStatus.InGameStatus);
   ```

   - `InMenuStatus` covers a failed join: the join path posts its own message (§4.4). It also covers the tail of a cleanup that already ran.
   - A host that loses its own transport gets no message. That's out of scope (§5).
4. **`m_isMatchCancelled`** (new private bool):
   - set by `MarkMatchCancelled()` (§4.4)
   - reset to `false` in `CleanupAfterDisconnect()`

### 4.3 N61: ignore the host's own shutdown callback after cleanup

1. In `OnClientDisconnected` (`:218-258`), after `_isServer` and `_isLocalClientDisconnected` are computed, add:

   ```csharp
   if (IsOwnShutdownTail(_isServer, _isLocalClientDisconnected, m_currentLobby != null, m_currentMultiplayerStatus))
   {
       if (m_debugMode) Debug.Log("Host shutdown finished after cleanup; nothing to do.");
       return;
   }
   ```

   ```csharp
   internal static bool IsOwnShutdownTail(bool isServer, bool isLocalClient, bool hasLobby, MultiplayerStatus status) =>
       isServer && isLocalClient && !hasLobby && status == MultiplayerStatus.InMenuStatus;
   ```

2. **This must not swallow a real host-side loss.** A host whose transport fails mid-lobby or mid-match is still `InLobbyStatus` / `InGameStatus` and keeps today's path.
3. **`Disconnect()` and `CleanupAfterDisconnect()` stay as they are otherwise.** Don't make `Disconnect()` wait for `ShutdownInProgress`: with a connected client that could delay the return to the Lobby by up to 5 s.

### 4.4 F1: failures outside a match

| Where | Add | Key |
|---|---|---|
| `LoginUI.OnClickLoginAnonymous` | Wrap the `await` in `try` / `catch (System.Exception e) { Debug.LogException(e); }`. Afterwards, if `AuthManager.Instance.IsSignedIn` is false, post. **Don't change `AuthManager`:** poll P2 keeps it untouched. | `notice_sign_in_failed` |
| `StartHost`, the `"host was not started"` return (`:620`) and both `catch` blocks (`:664-674`) | `PlayerNotice.Post(...)` | `notice_create_lobby_failed` |
| `JoinLobbyById`: the missing join code (`:885`), `!_relayJoined` (`:894-897`) and both `catch` blocks (`:909-924`) | `PlayerNotice.Post(...)`. Not for the null-ID guard (`:858-862`): it's a programming guard. | `notice_join_lobby_failed` |
| `FindAvailableLobbies`, both `catch` blocks (`:841-848`) | `PlayerNotice.Post(...)`. The list stays empty and the status still says "Lobby not found". | `notice_lobby_search_failed` |
| `StartGameSceneWhenReady`, the two `yield break` failures (`:1248`, `:1261`) | `PlayerNotice.Post(...)` | `notice_start_game_failed` |
| **New** `public void MarkMatchCancelled()` | `m_isMatchCancelled = true; PlayerNotice.Post("notice_match_cancelled");` | `notice_match_cancelled` |
| `AbortGameStart` (`:1310-1318`), the branch with no `GameMaster` | Call `MarkMatchCancelled()` before `Disconnect()`. The branch with a `GameMaster` gets the message through the RPC below. | — |
| `GameMaster.AbortMatch` (`GameMaster.cs:486-516`) | Before `MultiplayerManager.Instance.Disconnect()`, call a new `[ClientRpc] private void MatchAbortedClientRpc()` that calls `MultiplayerManager.Instance?.MarkMatchCancelled()`. The host runs it locally too. Delivery is best effort: if the client's copy is lost in the shutdown, the client shows `notice_connection_lost` instead. | — |

- **Don't post from `RefreshCurrentLobbyCore` errors.** They repeat on the polling interval and fall back to the last known data (RP13).
- **Don't post from `StartGameScene`'s early rejections** (`:1193-1235`). The Start button is shown only when they can't happen.

### 4.5 F1: messages inside a match (client side)

**`PlayerUI`:** add four fields in the existing pattern, each with a field initializer. Unity uses the initializer when the prefab has no saved value, so `PlayerUIPrefab` doesn't need an edit:

```csharp
[SerializeField] private LocalizedString m_selectCardFirstText = new LocalizedString("UI_table", "notice_select_card_first");
[SerializeField] private LocalizedString m_confirmTargetFirstText = new LocalizedString("UI_table", "notice_confirm_target_first");
[SerializeField] private LocalizedString m_opponentTurnText = new LocalizedString("UI_table", "notice_opponent_turn");
[SerializeField] private LocalizedString m_deckRefilledText = new LocalizedString("UI_table", "notice_deck_refilled");
```

Add the matching public methods `ShowSelectCardFirst()`, `ShowConfirmTargetFirst()`, `ShowOpponentTurn()` and `ShowDeckRefilled()`, like `ShowManaNotEnough()`. They use the existing `ShowMessage`, its 2-second duration and its locale refresh.

**`PlayerSession`** (owner only; every new branch starts with `if (IsOwner && PlayerUI.Instance != null)`):

| Method | Change |
|---|---|
| `TryConfirmTarget`, the first `m_selectedCard == null` branch (`:800-804`) | Keep the warning; also call `ShowSelectCardFirst()`. The duplicate check at `:834` stays as it is. |
| `TryHandlePackage` (`:1073-1086`) | If `CheckPackage` returns false, call `ShowConfirmTargetFirst()`. If `CheckPackage` is true but `CanPlayerSelectCardTargets(this)` is false, call `ShowOpponentTurn()` **only when** the phase is `PlayerAction` and `m_activePlayer` is set to another session. Otherwise (input locked, resolving) stay silent as today. |
| `ConfirmTurn` (`:174-208`) | After the `RespawnWait` branch: if the phase is `PlayerAction` or `ResolveCards` and `GameMaster.Instance.m_activePlayer.Value.TryGet(out PlayerSession _active)` gives a session that isn't `this`, call `ShowOpponentTurn()` and **return without sending the RPC**. (The server would reject it. Not sending also stops a late press from ending the player's own next turn.) Keep the local selection; don't call `ClearLocalCardSelection()` on this path. |

**`GameMaster`:**
- In `RefillDeck` (`GameMaster.Card.cs:237-264`), after `CreateDeck`, call a new `[ClientRpc] private void DeckRefilledClientRpc()`. It calls `PlayerUI.Instance?.ShowDeckRefilled()` on every peer, the host included.
- Send it even when the new deck is empty; the warning stays.

**Unchanged:** the server-side checks and warnings, `HandleConfirmTurn`, the N21 phase tag, `CheckPackage`'s rules, the mana and hand-limit messages.

### 4.6 Localization: 11 new `UI_table` keys

Add them to `UI_table Shared Data.asset` and to all three locale tables, with unique ids. Use the Localization Tables window, or matching YAML in the four assets (double-quoted values keep the punctuation).

| Key | en | ru | ja |
|---|---|---|---|
| `notice_connection_lost` | Connection to the host was lost. | Соединение с хостом потеряно. | ホストとの接続が切れました。 |
| `notice_match_cancelled` | The match couldn't continue and was cancelled. | Матч не удалось продолжить, он отменён. | 試合を続行できなかったため、中止されました。 |
| `notice_sign_in_failed` | Couldn't sign in. Check your connection and try again. | Не удалось войти. Проверьте подключение и попробуйте снова. | サインインできませんでした。接続を確認して、もう一度お試しください。 |
| `notice_create_lobby_failed` | Couldn't create a lobby. Check your connection and try again. | Не удалось создать лобби. Проверьте подключение и попробуйте снова. | ロビーを作成できませんでした。接続を確認して、もう一度お試しください。 |
| `notice_join_lobby_failed` | Couldn't join the lobby. It may be full or closed. | Не удалось войти в лобби. Возможно, оно заполнено или закрыто. | ロビーに参加できませんでした。満員か、閉じられた可能性があります。 |
| `notice_lobby_search_failed` | Couldn't load the lobby list. Try again. | Не удалось загрузить список лобби. Попробуйте снова. | ロビー一覧を読み込めませんでした。もう一度お試しください。 |
| `notice_start_game_failed` | Couldn't start the match. Both players must be connected; try again. | Не удалось начать матч. Оба игрока должны быть подключены; попробуйте снова. | 試合を開始できませんでした。両方のプレイヤーが接続している必要があります。もう一度お試しください。 |
| `notice_select_card_first` | Pick a played card in the bottom row first. | Сначала выберите разыгранную карту в нижнем ряду. | まず下の列からプレイしたカードを選んでください。 |
| `notice_confirm_target_first` | Choose a target and press Confirm before Send. | Выберите цель и нажмите «Подтвердить» перед «Отправить». | 送信の前に対象を選んで「確認」を押してください。 |
| `notice_opponent_turn` | It's your opponent's turn. | Сейчас ход соперника. | 相手の番です。 |
| `notice_deck_refilled` | The deck ran out: a new deck was shuffled. | Колода закончилась: перемешана новая колода. | 山札が尽きたため、新しい山札を作りました。 |

- The button names match the existing labels:
  - `btn_confirm`: Confirm / Подтвердить / 確認
  - `btn_send`: Send / Отправить / 送信
- "Соперник" matches the ru result texts.

### 4.7 Scenes: `NoticePanel` in LobbyScene and LoginScene

Add under the root canvas (`LobbyCanvas` / `LoginCanvas`), as its **last** child so it draws on top:

```
NoticePanel  (RectTransform; NoticeUI; no Image)
└─ Box       (Image: built-in UISprite, Sliced, color (0, 0, 0, 0.6), raycastTarget off)
   └─ Text   (TextMeshProUGUI: the font asset of the scene's other labels, white, centre/middle,
              wrapping on, autosize 24–36, margins 16 on all four sides, raycastTarget off)
```

- **`NoticePanel` fields:** `m_root` = `Box`, `m_text` = `Text`, `m_duration` = 6.
- **`Box`** stretches to `NoticePanel`; `Text` stretches to `Box`.
- **`Box` starts inactive** in the scene.

| Scene | `NoticePanel` anchor = pivot | Position | Size | Why there |
|---|---|---|---|---|
| LobbyScene | top-centre (0.5, 1) | (0, −40) | 1400 × 72 | Above the list panel (its top is at 940 of 1080) and the current-lobby panel (top at 858) |
| LoginScene | bottom-centre (0.5, 0) | (0, 400) | 1200 × 72 | Between the menu panel (top at 331) and the logo (bottom at 747) |

**TASK-UI7** adds the low-profile rectangles for these objects. This task only sets the authored (medium/high) layout.

### 4.8 N60 cleanup in `MultiplayerManager.cs`

- **`SetLobbyCode`:** one `if (m_debugMode) { … }` with both log lines. Keep the method: it's parked with M11.
- **Delete** the commented-out `InitUnityServices` block and its `//todo Dont use rn` line (`:582-598`).
- **Don't touch** the commented-out `JoinLobbyByJoinCode` block (M11, parked).

### 4.9 Docs

- **`Architecture.md`:**
  - §7: lost connection and abort now show a Lobby message
  - §9: the notice pattern
  - §10: A5 mentions N61's guard
- **`GameLoop.md`:**
  - §3.8: `MatchAbortedClientRpc`
  - §4: the two new RPCs
  - `PlayerUI` messages: the four new ones
- **`Dependencies.md`:**
  - RP6: the N61 guard and `ShouldShowConnectionLost`
  - RP12: the two new RPCs
  - a new **RP19:** `PlayerNotice` is a static, time-windowed store. Scenes that should show out-of-match notices need a `NoticeUI`.

## 5. Explicit non-goals

- **No result screen** for a lost host (P1 = c). There's no new `MatchEndReason`, and `MatchResultData` is unchanged.
- **No reconnect or rejoin** (research H6C).
- **No message for a host that loses its own transport** (`wasServer`). It's rare, and with the planned Server + clients model it becomes the server's concern.
- **No message from lobby-refresh polling errors** or from `StartGameScene`'s early guards.
- **No change to `AuthManager`**, including the sign-out methods (P2 = a; tracked as U17).
- **No hints for target types** ("which target can I pick"): the owner's later feature.
- **No change to `LeaveMatchToResultScene`**, the H6 forfeit or the N19 leave flow.
- **No new message for Confirm Turn** in RoundBreak or DrawCards. It's ignored there as before.
- **N60's other items** (`PlayerController`, `PlayerCardfWorldArea`, `CardControllPackage`, `NetworkCard`, `CardWorldViewManager`, `GameMaster.Player.cs`) belong to other tasks.

## 6. Constraints and compatibility requirements

- **Keep every serialized field name and every existing scene / prefab reference.** The new `LocalizedString` fields use initializers, so `PlayerUIPrefab` and `ResultScene` need no edit.
- **Server authority is unchanged.** The client-side checks only decide whether to show a message, or whether to skip an RPC the server would reject.
- **RP12:** two new ClientRpcs. Both peers on the same build; make a new player build for the checks.
- **No per-frame string formatting** in `NoticeUI.Update` (RP15).
- **`DebugModeGuard`:** no new `m_debugMode` class. New logs stay behind the existing flags, and warnings and errors stay as they are (N34).
- **Unity 6 APIs, no warnings of our own.**
  - Keep baseline copies in `build/n59-verification/baseline/`.
  - Restore the dynamic font to `HEAD` before handing over (`AGENTS.md`, N28).

## 7. Acceptance criteria

1. **`PlayerNotice` / `NoticeUI`:**
   - A notice posted 0–5.9 s before a `NoticeUI` enables is shown, with the rest of its 6 s. One posted 6 s or more before is not.
   - A second enable within the window shows it again (the double-load case).
   - A new post replaces the text at once.
   - A locale change re-localizes the shown text.
   - The box hides when the time is up.
   - No raycast target on the panel.
2. **`ShouldShowConnectionLost`:** true only for (`InLobbyStatus` or `InGameStatus`) and `!wasServer` and `!matchCancelled`. All 12 input combinations are tested.
3. **`IsOwnShutdownTail`:** true only for (`isServer`, `isLocalClient`, `!hasLobby`, `InMenuStatus`). The host's own callback during `InLobbyStatus` / `InGameStatus` still reaches `HandleLostConnectionToHost`.
4. **N61 in Play Mode** (host only is enough): after `Disconnect()` from a hosted lobby with no client, `CleanupAfterDisconnect` runs once and `LobbyScene` loads once.
5. **In-match messages:**
   - Confirm with no card → `notice_select_card_first`.
   - Send with no confirmed target → `notice_confirm_target_first`.
   - Confirm Turn and Send during the opponent's `PlayerAction` → `notice_opponent_turn`, **and no `ConfirmTurnServerRpc` / `CardInputServerRpc` is sent.**
   - During the local player's own action, Confirm Turn still ends the turn as before.
6. **Deck refill:** forcing an empty deck before `DrawCard` runs `RefillDeck`, and `PlayerUI` shows `notice_deck_refilled` on the host's peer.
7. **Localization:** all 11 keys exist with non-empty en / ru / ja values. In both scenes' `NoticePanel` and in `ManaMessageText`, every notice in every language renders with `isTextTruncated == false`, at 1920×1080 and 2560×1600.
8. **No regression:**
   - The normal match end, Leave match, the H6 forfeit and the N55 abort reasons behave as before.
   - A client pressing Leave lobby gets **no** message.
   - A completed match followed by Main menu → Lobby shows no stale message.
9. **Build:** 0 errors, no warnings of our own. The release player builds, and its `Player.log` gets no new debug lines.

## 8. Regression tests

- **Edit Mode tests** (temporary, in `Temp/N59Verification/`): AC2, AC3, and AC1 with a fake clock if needed.
- **Play Mode harness:**
  - AC1 and AC7 with the real `NoticePanel` from both scenes
  - AC4 with a host-only `NetworkManager`
  - AC5 and AC6 with the host fixture used for N55 / N57: `GameMaster`, two synthetic sessions, the real `PlayerUIPrefab`
  - Count RPC sends by patching or wrapping, or by asserting the server-side handler isn't reached.
- **Baseline:** run AC4 on the baseline too, and report the two `LobbyScene` loads it should show. This measures N61 the way N54 was measured.
- **The report lists every case with its result.** Remove the harness from `Assets` and `Temp`, and keep a copy in `build/n59-verification/`.
- **Not testable by a harness:** the real two-player disconnect paths. Say so; §9 covers them.

## 9. Manual Unity Editor checks

**Setup:** two Editors, main = host, clone = client (with `-logFile C:\Lesson\clone-editor.log`), plus a new player build for 3.

1. **N59, Play Mode:** mid-match, stop Play Mode in the **host** Editor. The client returns to the Lobby and shows "Connection to the host was lost." for about 6 s. Repeat once in ru and once in ja.
2. **N59, in the lobby:** the client joins, then the host presses Leave lobby. The client shows the same message.
3. **N59, player build:** build = host, Editor = client. Mid-match, close the build with X. The Editor client shows the message.
4. **No false message:**
   - the client presses Leave lobby
   - the client presses Leave match → Result as before
   - a normal match end → Result → Main menu → Lobby

   None of these shows a message.
5. **N61:** the host creates a lobby and presses Leave lobby, once with no client and once with a client joined. Each time `Editor.log` shows **one** `Loaded scene 'Assets/Scenes/LobbyScene.unity'` for that leave.
6. **Match cancelled (optional):** the host presses Start game, and right after it the client stops Play Mode while GameScene loads. The host returns to the Lobby with "The match couldn't continue and was cancelled."
7. **Lobby and network failures (optional; needs the network off):**
   - Turn the PC's network off, then press Find Lobby / Refresh, then Create Lobby. Each shows its message.
   - In the Login scene with the network off, Login anonymous shows "Couldn't sign in …".
   - Turn the network back on afterwards.
8. **In-match messages:**
   - In your own Player action, press Confirm with no card picked, then Send with no confirmed target: two different messages.
   - In the opponent's Player action, press Confirm Turn and Send: "It's your opponent's turn.", and the opponent's turn doesn't end.
   - In your own action, Confirm Turn still ends your turn.
9. **Logs:** no exception in either Editor log or in `Player.log`.

## 10. Open questions and assumptions

- **A1, the same message when the host leaves the lobby on purpose.** The client can't tell that apart from a crash (N59). The owner's chosen text is used. If it reads wrong in that case, the alternative is "The host left, or the connection was lost." (new poll **Q3**).
- **A2, a time window instead of consume-once.** It survives N61-style double loads, and a load that comes after a slow `RemovePlayerAsync`. Six seconds matches the length of the longest message.
- **A3, skipping the Confirm Turn RPC** during the opponent's action. The server rejects it today, so nothing is lost. It also removes a race where a late press ends the player's own next turn.
- **A4, the abort message is best effort on the client** (§4.4). The fallback is the connection-lost message, which is also true.
- **A5, `notice_start_game_failed`** covers the two failures after the Start button; it never shows for an honest two-player lobby unless the relay connection didn't finish.
- **A6, the Server + clients migration** the owner plans: `ShouldShowConnectionLost` keys on "not the server", so a dedicated server keeps the same client behavior.
