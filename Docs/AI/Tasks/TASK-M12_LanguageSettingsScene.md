# TASK-M12: Let players choose the language in the game

- **Source issue:** [Issue Report #6 → M12](../Reports/2026-10-19_IssueReport-6.md#-medium-13) (first reported in #1)
- **Severity:** 🟡 Medium (Confirmed: players can't switch between en/ru/ja)
- **Baseline:** `fe2d4fb`. Line numbers below are from this commit.
- **Order:** 12 of 13. Independent of the other tasks.
- **Implementer:** Codex
- **Owner decision (2026-10-19):** a separate **`SettingsScene`**. The code for it already exists (`SceneFlowManager.LoadSettingsScene`, `SettingsUI`, `SettingsSceneButtonsUI`).

## 1. Objective

From the main menu, a player opens Settings, picks English, Русский or 日本語, and the whole game switches right away. The choice is remembered for the next launch.

## 2. Current behavior

Everything except the scene and the entry button already exists:
- `UI/SettingsUI.cs`:
  - waits for `LocalizationSettings.InitializationOperation`
  - fills `m_languageDropdown` (`TMP_Dropdown`) with `AvailableLocales`, using `Locale.LocaleName`
  - selects the saved or current locale
  - on change, sets `LocalizationSettings.SelectedLocale` and saves `GameSettingsPrefs.LanguageCode`
- `GameSettingsPrefs.cs` stores the locale code in `PlayerPrefs` (`setting.language_code`).
- `Bootstrap.ApplySavedLanguage` (`Bootstrap.cs:34-55`) applies the saved code at startup.
- `UI/SettingsSceneButtonsUI.cs`: `OnClickBack()` → `SceneFlowManager.LoadMainMenuScene()`.
- `Manager/SceneFlowManager.cs`: `m_settingsSceneName = "SettingsScene"` (`:15`), `LoadSettingsScene()` (`:57-60`, a plain `SceneManager.LoadScene`).

**Missing:**
- `Assets/Scenes/SettingsScene.unity` doesn't exist, and nothing is in Build Settings (`ProjectSettings/EditorBuildSettings.asset`).
- `SettingsUI` isn't in any scene.
- `MainMenuUI` (`UI/MainMenuUI.cs`) has only `OnClickLobby` and `OnClickExit`, and `MainMenuScene` has no Settings button.

**Locales:** `Assets/Localization/English (en).asset`, `Russian (ru).asset`, `Japanese (ja).asset`. `MainMenuScene` labels use `LocalizeStringEvent` with `UI_table` keys, and its EventSystem uses `InputSystemUIInputModule`.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scenes/SettingsScene.unity` | **New** scene |
| `ProjectSettings/EditorBuildSettings.asset` | Add `SettingsScene` (enabled), after `MainMenuScene` |
| `Assets/Scripts/UI/MainMenuUI.cs` | Add `OnClickSettings()` |
| `Assets/Scenes/MainMenuScene.unity` | Settings button |
| `Assets/Scripts/UI/SettingsUI.cs` | Small hardening (missing dropdown, native names) |
| `Assets/Localization/UI_table*` | 3 new keys in en/ru/ja |

Read-only: `SceneFlowManager.cs`, `GameSettingsPrefs.cs`, `Bootstrap.cs`, `SettingsSceneButtonsUI.cs`.

## 4. Required changes

1. **`MainMenuUI`:** add `OnClickSettings()` with the same null check and log style as `OnClickLobby`, calling `SceneFlowManager.Instance.LoadSettingsScene()`.
2. **`MainMenuScene`:** add a Settings button between Lobby and Exit. Copy the existing buttons' style, size and font. Its label uses `LocalizeStringEvent` → `UI_table/btn_settings`, and `onClick` → `MainMenuUI.OnClickSettings`.
3. **New `SettingsScene`:** build it like `MainMenuScene`, copying its Canvas, `CanvasScaler` settings, camera/background and EventSystem with `InputSystemUIInputModule`.
   - a title text → `settings_title`
   - a "Language" label → `label_language`
   - a `TMP_Dropdown` assigned to `SettingsUI.m_languageDropdown` (the `SettingsUI` component on a root UI object)
   - a Back button → `btn_back` (existing key), `onClick` → `SettingsSceneButtonsUI.OnClickBack`
   - use a font with Latin, Cyrillic and Japanese glyphs (the same font asset and fallbacks as the main menu)
   - **don't** add `SceneFlowManager`, `MultiplayerManager` or other DDOL managers to this scene; they come from the boot scenes
4. **`EditorBuildSettings`:** add `Assets/Scenes/SettingsScene.unity`, enabled. Don't reorder or disable the other scenes.
5. **`SettingsUI`:**
   - if `m_languageDropdown` is null, `Debug.LogError` and return from `InitializeLanguageDropdown`
   - show each locale in its own language: use `_locale.Identifier.CultureInfo?.NativeName`, upper-casing the first letter, which gives "English", "Русский" and "日本語"
   - if that's empty, fall back to `LocaleName`, then to the code, as now
6. **Localization (`UI_table` shared data + en/ru/ja):**

   | Key | en | ru | ja |
   |---|---|---|---|
   | `btn_settings` | Settings | Настройки | 設定 |
   | `settings_title` | Settings | Настройки | 設定 |
   | `label_language` | Language | Язык | 言語 |

## 5. Explicit non-goals

- Changing the language during a match or in the lobby (only from the main menu). L7 (card texts don't refresh on a locale change) therefore isn't reachable.
- Other settings: audio, graphics, controls.
- Changing how the saved locale is applied at startup.
- Steam or OS language detection.

## 6. Constraints and compatibility requirements

- Keep the existing serialized names: `SettingsUI.m_languageDropdown`, `SceneFlowManager.m_settingsSceneName = "SettingsScene"`. The new scene file name must be exactly `SettingsScene`.
- Don't modify `GameScene`, `LobbyScene`, `LoginScene` or `ResultScene`.
- `SettingsSceneButtonsUI` stays as it is; it's L10's "legacy" item, which this task brings into use.

## 7. Acceptance criteria

- [ ] Main menu → Settings opens `SettingsScene`. The dropdown lists English, Русский and 日本語, with the current language selected.
- [ ] Picking a language changes the Settings scene texts right away. Back → the main menu is in that language, and so are the lobby and the match.
- [ ] After stopping and restarting Play Mode, or restarting the player build, the chosen language is still active.
- [ ] `SettingsScene` is in Build Settings, and there are no missing-script or missing-reference warnings.
- [ ] Compiles with no new warnings.

## 8. Regression tests

No automated tests. **Review checklist:**
- `EditorBuildSettings` diff only adds one scene
- `MainMenuScene` diff only adds the button
- the new scene contains no DDOL managers

## 9. Manual Unity Editor checks

Start Play Mode from `LoginScene`, as usual.
1. Log in → main menu → Settings. Pick Русский: the title, label and Back button switch at once. Back: the main menu is in Russian.
2. Lobby → create a lobby → texts are in Russian. Leave.
3. Settings → 日本語 → check the Japanese glyphs render (no boxes) in the dropdown and the labels.
4. Stop Play Mode, start again: Japanese is still active. Set English back.
5. Player build, optional (also covers U1): the same checks once in a Windows build.

## 10. Open questions and assumptions

- **Q1:** resolved. The owner chose a separate `SettingsScene` (2026-10-19).
- **A1:** `CultureInfo.NativeName` works in the Windows player. If it returns an empty string, the fallback keeps today's `LocaleName`.
