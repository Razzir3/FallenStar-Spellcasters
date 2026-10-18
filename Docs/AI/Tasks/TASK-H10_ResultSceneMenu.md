# TASK-H10: Result screen GUI with a "Main menu" button

- **Source:** owner request (2026-10-18), following [TASK-H5](TASK-H5_EndOfMatchResultScene.md)
- **Severity / type:** feature (UI)
- **Order:** after H5. It works alongside H6, which supplies the `OpponentDisconnected` reason.
- **Implementer:** Codex (UI built in the Unity Editor)
- **Localization requirement:** all new visible text must exist in **English, Russian and Japanese** (`AGENTS.md`). Use Unity Localization (`UI_table`), with no hard-coded strings.

## 1. Objective

`ResultScene` shows a simple menu:
- a title: Victory, Defeat or Draw
- a line with the reason
- one button that leaves the result screen and returns to the **main menu**

There's no network activity here. The network session and lobby were already closed by H5.

## 2. Current behavior

After H5, `ResultScene` has a camera and `ResultSceneController`. The controller prints the result to the Console in debug mode. The result is available in `MatchResultData`: `HasResult`, `IsWinner`, `IsDraw`, `Reason`. There's no UI, so the player is stuck in this scene.

Existing patterns to follow:
- **Button scripts:** `UI/MainMenuUI.cs`, `UI/LobbyControlUI.OnClickBackToMainMenu` (`:54-60`). Each has a public `OnClick…` method, wired through the `Button.onClick` event in the Inspector, and calls `SceneFlowManager.Instance.LoadMainMenuScene()` with a null check.
- **Localized labels:** `MainMenuScene` / `LobbyScene` use `LocalizeStringEvent` components bound to `UI_table` keys.
- **Japanese glyphs:** the project's TMP font asset `Assets/Fonts/MPLUSRounded1c-Medium SDF.asset`.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/UI/ResultMenuUI.cs` | **New** `MonoBehaviour` |
| `Assets/Scenes/ResultScene.unity` | Add Canvas, EventSystem, texts and button (in the Editor) |
| `Assets/Localization/UI_table*` | New keys (§4.3) in en/ru/ja |
| `MatchResultData` (H5) | Read, then `Clear()` on leaving |
| `SceneFlowManager.LoadMainMenuScene()` | Existing |

## 4. Required changes

### 4.1 Script: `ResultMenuUI`

1. Serialized fields:
   - `LocalizeStringEvent m_titleText`, `LocalizeStringEvent m_reasonText`
   - `LocalizedString m_titleWin`, `m_titleLose`, `m_titleDraw`
   - `LocalizedString m_reasonYouLostLives`, `m_reasonOpponentLostLives`, `m_reasonBothLostLives`, `m_reasonOpponentLeft`
   - `bool m_debugMode`
2. In `Start()`:
   - If `!MatchResultData.HasResult`, hide the title and reason objects. Only the button remains.
   - Otherwise:
     - set `m_titleText.StringReference` to `m_titleDraw` / `m_titleWin` / `m_titleLose`
     - set `m_reasonText.StringReference` from `Reason` and the player's own outcome:

       | `Reason` | Outcome | Reason string |
       |---|---|---|
       | `LivesDepleted` | lost | `m_reasonYouLostLives` |
       | `LivesDepleted` | won | `m_reasonOpponentLostLives` |
       | `LivesDepleted` | draw | `m_reasonBothLostLives` |
       | `OpponentDisconnected` | any | `m_reasonOpponentLeft` (only the remaining player ever sees this) |

     - call `RefreshString()` on both

   `LocalizeStringEvent` updates automatically when the locale changes, so don't use a one-shot `GetLocalizedString` (L7).
3. `public void OnClickMainMenu()`:
   - `MatchResultData.Clear();`
   - if `SceneFlowManager.Instance == null`: `Debug.LogError("ResultMenuUI: SceneFlow is null")` and return
   - `SceneFlowManager.Instance.LoadMainMenuScene();`
   - Guard against double clicks with a private `bool m_isLeaving`.

### 4.2 Scene setup (Unity Editor, in `ResultScene`)

4. Add an `EventSystem` with the same input module as `MainMenuScene`, and a `Canvas` with the same render mode and `CanvasScaler` settings as `MainMenuScene`.
5. Add these children:
   - **Title:** TMP text with `LocalizeStringEvent`
   - **Reason:** TMP text with `LocalizeStringEvent`
   - **Main menu button:** TMP label with `LocalizeStringEvent` bound to `btn_toMainMenu`; `onClick` → `ResultMenuUI.OnClickMainMenu`

   All texts use the MPLUSRounded1c TMP font asset, or the font the other scenes use, so Japanese renders.
6. Add `ResultMenuUI` to the Canvas (or a child) and assign all fields. `ResultSceneController` from H5 stays.

### 4.3 Localization keys (`UI_table`, en / ru / ja)

| Key | en | ru | ja |
|---|---|---|---|
| `result_title_win` | Victory! | Победа! | 勝利！ |
| `result_title_lose` | Defeat | Поражение | 敗北 |
| `result_title_draw` | Draw | Ничья | 引き分け |
| `result_reason_youLostLives` | You lost all your lives. | Вы потеряли все жизни. | あなたのライフがすべて尽きました。 |
| `result_reason_opponentLostLives` | Your opponent lost all their lives. | Соперник потерял все жизни. | 相手のライフがすべて尽きました。 |
| `result_reason_bothLostLives` | Both players lost all their lives. | Оба игрока потеряли все жизни. | 両方のプレイヤーのライフが尽きました。 |
| `result_reason_opponentLeft` | Your opponent left the match. | Соперник покинул матч. | 相手が試合から退出しました。 |
| `btn_toMainMenu` | Main menu | Главное меню | メインメニュー |

The texts are proposals; the owner may adjust them. Add the keys through the Localization Tables window, not by editing the YAML. Don't touch the existing key `"btn_lobbyRefresh\t"` (L8).

## 5. Explicit non-goals

- Network calls of any kind. The session is already closed (H5).
- Rematch, "back to lobby" or statistics.
- Language selection on this screen (M12).
- Changing `MainMenuScene`.
- Visual polish beyond a readable, centered layout.

## 6. Constraints and compatibility

- No renames. New files only, plus scene and localization assets.
- Every visible string comes from `UI_table` in en/ru/ja.
- The `.meta` files for the new script are generated by Unity. Don't hand-write GUIDs or scene YAML.

## 7. Acceptance criteria

- [ ] The winner sees "Victory!" with "Your opponent lost all their lives.", the loser sees "Defeat" with "You lost all your lives.", and a draw shows "Draw" with "Both players lost all their lives."
- [ ] With `Reason == OpponentDisconnected` (H6), the remaining player sees "Your opponent left the match."
- [ ] Opening `ResultScene` directly (no result) shows only the button, with no errors.
- [ ] The button loads `MainMenuScene`, and `MatchResultData.HasResult` is false afterwards. Double clicks cause no errors.
- [ ] From the main menu, a new lobby and match can be started, and the next result shows the new outcome, not the old one.
- [ ] All texts display correctly in en, ru and ja, and update when the locale is changed while the scene is open.
- [ ] Compiles without new warnings.

## 8. Regression tests

No automated tests. Review checklist:
- no hard-coded user-visible strings
- `MatchResultData.Clear()` is called before the scene change
- the `SceneFlowManager` null check is present

## 9. Manual Unity Editor checks

1. ParrelSync host and client. Finish a match (H5 §9). Check the title and reason on both windows.
2. Press **Main menu** on both and confirm `MainMenuScene` loads. Start a second match, finish it with the opposite winner, and check the titles are swapped.
3. H6 case: close the client mid-match. The host shows Victory with "Your opponent left the match."
4. Change the locale to ru and ja (Localization Scene Controls window or saved setting). All three texts are translated and render without missing-glyph boxes.
5. Open `ResultScene` directly in the Editor and press Play: only the button shows, and it works.

## 10. Open questions and assumptions

- **Q1 (resolved by the owner, 2026-10-18):** the reason line differs by outcome: "You lost…" for the loser, "Your opponent lost…" for the winner. A third sentence covers the rare draw, where both players lose their last life in the same resolution.
- **A1:** `AppRoot` (with `SceneFlowManager`) persists across scenes (`DontDestroyOnLoad`), so `SceneFlowManager.Instance` is available in `ResultScene`. That was verified in the first audit.
