# TASK-UI5: Choose the resolution and display mode in Settings

- **Source:** owner request, 2026-10-21: "create selection of resolution in setting menu". Follows the UI scaling work (UI1–UI4, [report #9](../Reports/2026-10-21_IssueReport-9.md)).
- **Type:** feature (fills the "no other settings" gap). No issue ID.
- **Decisions S1–S5 (§10):** written with Claude's recommended choices. The owner can change any of them before this goes to Codex.
- **Baseline:** `391e079` plus the uncommitted working tree, including UI1–UI4. Line numbers are from the working tree.
- **Implementer:** Codex

## 1. Objective

In `SettingsScene` the player can choose:
1. **Display mode:** Fullscreen or Windowed.
2. **Resolution:** from a list of sizes that suit the monitor.

A choice applies at once, the GUI rescales (UI2), and the game starts with the same choice next time. The language setting keeps working exactly as today.

## 2. Current behavior

- **The Settings screen** (`Assets/Scenes/SettingsScene.unity`):
  - The root `SettingsCanvas` carries `SettingsSceneButtonsUI` and `SettingsUI`. Its CanvasScaler is Scale With Screen Size, 1920×1080, Expand (UI2).
  - Everything sits at the bottom centre:
    - `MenuPanelImage`: 300×300 at (0, 0), pivot at the bottom
    - `SettingsTitle`: y 250
    - `LanguageLabel`: y 195
    - `LanguageDropdown`: 260×38 at y 140, Template height 150
    - `BackButton`: 160×30 at y 31
  - The panel sprite (`UI board Large stone.png`, 921×617) is drawn as a Simple image, so today it's squashed into a square.
- **`SettingsUI`** (`UI/SettingsUI.cs`) waits for localization, fills `m_languageDropdown` with the locales, and saves the choice in `GameSettingsPrefs.LanguageCode` (`setting.language_code`). `Bootstrap.Start` applies the saved language at launch.
- **No script anywhere** uses `Screen.SetResolution`, `Screen.resolutions` or `Screen.fullScreenMode`.
- **Player settings** (`ProjectSettings.asset`):
  - `fullscreenMode: 1` (Fullscreen Window, i.e. borderless) and `defaultIsNativeResolution: 1`
  - `resizableWindow: 0`
  - `allowFullscreenSwitch: 1`, so Alt+Enter toggles windowed/fullscreen
  - `runInBackground: 0`
- **Unity already saves the screen state:** the player stores the size and mode in its own PlayerPrefs and restores them at the next launch.
  - The keys are `Screenmanager Resolution Width/Height`, `… Use Native` and `Screenmanager Fullscreen mode`.
  - Seen in the owner's registry: 2560×1600, native, mode 1.
  - Command-line arguments (`-screen-width`, `-screen-height`, `-screen-fullscreen`) override the saved state.
- **Background play:** `NetworkManagerPrefab` has `RunInBackground: 1`, so NGO sets `Application.runInBackground = true`. A windowed game that loses focus keeps the match running.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/UI/DisplaySettingsUI.cs` (**new**) | Fills and applies the two dropdowns |
| `Assets/Scenes/SettingsScene.unity` | New labels and dropdowns; a bigger panel; the new component on `SettingsCanvas` |
| `Assets/Localization/UI_table Shared Data.asset`, `UI_table_en/ja/ru.asset` | 4 new keys |

Dependencies:
- **Scene objects** (GameObject IDs):

  | Object | ID |
  |---|---|
  | `SettingsCanvas` | `&1301508707` |
  | `MenuPanelImage` | `&1591348257` |
  | `SettingsTitle` | `&1043837270` |
  | `LanguageLabel` | `&881143872` |
  | `LanguageDropdown` | `&1163661070` |
  | `BackButton` | `&1136665385` |

  `SettingsUI.m_languageDropdown` → `{fileID: 1163661071}` (`SettingsScene.unity:1947`).
- **Labels** use a `LocalizeStringEvent` that names the key (`m_Key: label_language`, `m_KeyId: 0`). Copy that setup for the new labels.
- **Locale changes:** subscribe and unsubscribe the way `CardViewBase` does (`OnEnable` / `OnDisable`). Build strings in code the way `GameAnnouncerUI` does (`new LocalizedString("UI_table", key)`).

## 4. Required changes

### 4.1 `DisplaySettingsUI` (new MonoBehaviour on `SettingsCanvas`)

Serialized fields: `m_displayModeDropdown`, `m_resolutionDropdown` (`TMP_Dropdown`), `m_minWidth = 1024`, `m_minHeight = 720`.

1. **Initialize:** use `IEnumerator Start()`, wait for `LocalizationSettings.InitializationOperation` (as `SettingsUI` does), then fill both dropdowns. Use `SetValueWithoutNotify` and `RefreshShownValue`, then add the listeners (remove first, as `SettingsUI` does). A missing dropdown logs one error and that dropdown is skipped.
2. **Display-mode options:**
   - index 0 = `option_fullscreen` → `FullScreenMode.FullScreenWindow`
   - index 1 = `option_windowed` → `FullScreenMode.Windowed`
   - The current mode is shown as Windowed when `Screen.fullScreenMode == Windowed`, and as Fullscreen otherwise. That includes `ExclusiveFullScreen` set from the command line.
3. **Resolution list:** a **pure static** method, so a harness can test it. For example:

   ```
   BuildResolutionList(IEnumerable<Vector2Int> available, Vector2Int display, bool windowed,
                       Vector2Int current, Vector2Int min) → List<Vector2Int>
   ```

   - `available` = `Screen.resolutions`, as width × height only (ignore the refresh rate). Remove duplicates.
   - Keep only sizes with width ≥ `min.x` and height ≥ `min.y`.
   - **Fullscreen:** width ≤ `display.x` and height ≤ `display.y`. **Windowed:** width < `display.x` and height < `display.y`, so the title bar fits.
   - `display` = `Screen.mainWindowDisplayInfo` width × height.
   - If `current` (`Screen.width` × `Screen.height`) isn't in the list, add it, so the dropdown always shows the real size.
   - Sort by width, then by height, largest first.
   - Option text: `"{w} × {h}"` (U+00D7). If the font draws a missing-glyph box for "×", use a plain "x".
4. **Applying a choice** (immediately, S2):
   - **Resolution chosen:** `Screen.SetResolution(w, h, <mode currently selected>)`.
   - **Mode chosen:**
     - Rebuild the resolution list for the new mode.
     - **Fullscreen** → apply the display's native size.
     - **Windowed** → keep the current size if it is in the windowed list; otherwise use the largest windowed entry.
     - Then `Screen.SetResolution(w, h, mode)`, and select that entry without notify.
   - Use only the `SetResolution(int, int, FullScreenMode)` overload. The overload with an `int` refresh rate and `Resolution.refreshRate` are obsolete (CS0618), and the project has no warnings of its own.
5. **Follow outside changes:**
   - Alt+Enter, the command line and the screen size taking effect a frame later can all change the screen without the dropdowns.
   - So in `Update`, compare `Screen.width`, `Screen.height` and `Screen.fullScreenMode` with the last values seen.
   - On a change, rebuild and reselect both dropdowns **without notify**. Never call `SetResolution` from this path.
   - The cost is three property reads per frame, on this screen only.
6. **Locale change:** subscribe to `LocalizationSettings.SelectedLocaleChanged` in `OnEnable` and unsubscribe in `OnDisable`. On a change, relabel the two mode options, keeping the selection. Changing the language in the Language dropdown above must relabel them at once.
7. No PlayerPrefs keys of our own, and no change to `Bootstrap` (S3). No `m_debugMode` flag; if one is added, its `Awake` must call `DebugModeGuard.Allow` first (L9).

### 4.2 `SettingsScene` layout (S5)

Make the panel match its sprite's 3:2 shape, and use one row per setting: the label on the left, the dropdown on the right.

```
 y (from the bottom of the canvas; all anchors bottom-centre)
400 ┌──────────────────────────────────────────────────────┐  MenuPanelImage 600×400 at (0, 0)
350 │                       Settings                       │  SettingsTitle (0, 350)
280 │   Language             [ English              ▼ ]   │  label (−150, 280)  dropdown (140, 280)
220 │   Display mode         [ Fullscreen           ▼ ]   │  label (−150, 220)  dropdown (140, 220)
160 │   Resolution           [ 2560 × 1600          ▼ ]   │  label (−150, 160)  dropdown (140, 160)
 31 │                       [ Back ]                       │  BackButton unchanged (0, 31)
  0 └──────────────────────────────────────────────────────┘
      labels 240×46, left-aligned; dropdowns 260×38
```

- `MenuPanelImage`: 600×400. The sprite is 921×617, so 3:2 removes today's squash.
- `LanguageLabel` / `LanguageDropdown`: move to the first row. Keep their components, the `LocalizeStringEvent` and the `SettingsUI` reference. Make the label left-aligned.
- **New:** `DisplayModeLabel` (key `label_displayMode`), `DisplayModeDropdown`, `ResolutionLabel` (key `label_resolution`), `ResolutionDropdown`.
  - Duplicate the language label and dropdown, so they share the same look and fonts.
  - Set the `ResolutionDropdown` Template height to **300**, so about 10 entries are visible before the list scrolls.
- Wire both dropdowns to `DisplaySettingsUI` on `SettingsCanvas`.
- Codex may shift these positions by a few units if something overlaps in a real render. Report the final values.

### 4.3 Localization

Add four keys to `UI_table`. Use the Localization Tables window, so the key IDs are valid and no key gets a trailing tab (lesson from L8).

| Key | en | ja | ru |
|---|---|---|---|
| `label_displayMode` | Display mode | 表示モード | Режим экрана |
| `label_resolution` | Resolution | 解像度 | Разрешение |
| `option_fullscreen` | Fullscreen | フルスクリーン | Полноэкранный |
| `option_windowed` | Windowed | ウィンドウ | Оконный |

## 5. Explicit non-goals

- No exclusive fullscreen, refresh-rate choice, monitor choice, V-Sync, frame limit, graphics quality or UI-scale slider.
- No "keep these settings?" countdown (S2). No settings access from inside a match.
- No change to `ProjectSettings.asset`: the default mode, native resolution, `resizableWindow`, `runInBackground` and `allowFullscreenSwitch` stay as they are.
- No change to any CanvasScaler (UI2), the HUD (UI3) or other scenes.
- No change to the language behavior, `SettingsUI`'s logic or `setting.language_code`.

## 6. Constraints and compatibility requirements

- Keep `SettingsUI.m_languageDropdown` and every existing serialized reference. `SettingsSceneButtonsUI.OnClickBack` stays wired to the Back button.
- This is client-only UI, with no network code. The Settings scene is reached only from the main menu, before any session exists.
- **The Editor:** `Screen.SetResolution` and `Screen.fullScreenMode` have no effect on the Game view, and `Screen.resolutions` may list a single entry. The script must run there without errors. The real behavior is checked in a player build.
- Follow the naming style: `m_` fields, `_` locals, `SettingsUI`'s structure.
- Unity 6 APIs only, without obsolete members. No compiler warnings of our own.
- Restore the dynamic font asset before handing over (`AGENTS.md`). Scene edits are limited to the objects in §4.2.

## 7. Acceptance criteria

1. **Layout:** Settings shows the title, the three rows and Back, all inside the panel.
   - They are readable and uncut in en, ja and ru at the 9 UI2 resolutions: 1280×720, 1366×768, 1920×1080, 2560×1440, 2560×1600, 3840×2160, 1024×768, 1280×1024, 3440×1440.
   - Each dropdown's open list fits on screen.
2. **Truthful on open:** both dropdowns show the real mode and size, including after a launch with `-screen-fullscreen 0 -screen-width 1366 -screen-height 768`.
3. **List rules:** the resolution list follows §4.1.3: no duplicates, nothing below 1024×720, no size above the display, and nothing at the display's full width or height in Windowed. Largest first.
4. **Apply (player build):**
   - Picking a resolution changes the window size (Windowed) or the render size (Fullscreen) at once.
   - The GUI rescales as in UI2, and nothing throws.
   - Fullscreen → native size. Windowed → the current size if it fits, otherwise the largest that fits. The title bar is visible.
5. **Outside changes:** Alt+Enter on the Settings screen updates the Display-mode dropdown (and the resolution, if it changed) within a frame.
6. **Language:** changing the language on the same screen relabels the two new labels and the two mode options at once. The language dropdown works as before.
7. **Restart:** quit and relaunch the player; the same mode and resolution come back. This relies on Unity's saved screen state (S3).
8. Unity compile with 0 errors and no warnings of our own; the release Windows player builds.

## 8. Regression tests

- **Harness for `BuildResolutionList`:**
  - Display 2560×1600. Input: 3840×2400, 2560×1600 ×3 refresh rates, 2560×1440, 1920×1200, 1920×1080, 1366×768, 1280×720, 1024×768, 800×600.
  - Fullscreen expects: 2560×1600, 2560×1440, 1920×1200, 1920×1080, 1366×768, 1280×720, 1024×768.
  - Windowed expects the same list without 2560×1600 and 2560×1440.
  - A current size of 1600×900, not in the input, is inserted at its sorted place.
  - An empty input gives just the current size.
- **Edit Mode check** (temporary, under `Temp/UI5Verification/`, removed afterwards):
  - `SettingsScene` loads with 0 missing scripts.
  - All new and moved RectTransforms stay inside the panel and the logical canvas at the 9 resolutions.
  - The open popup of the resolution dropdown fits, using the same approach as the UI1–UI4 dropdown test.
- **Localization check:** the 4 keys exist in `UI_table` Shared Data, with non-empty en/ja/ru values, and none of their keys has trailing whitespace.
- Compile, and a release player build.

## 9. Manual Unity Editor checks

Use a **player build**, because the Editor ignores resolution changes.

1. Settings: switch to **Windowed** → a window with a title bar, at the largest size that fits. Pick 1366×768 → the window shrinks, and the menu scales down without cutting anything.
2. Switch to **Fullscreen** → it fills the display at native size. Pick **1920×1080** on the 2560×1600 screen. Expect black bars at the top and bottom, no stretching, and buttons that still click where they're drawn. If Unity stretches the picture instead, report it.
3. Press **Alt+Enter** on the Settings screen → the Display-mode dropdown follows.
4. Change the language to ru, then ja → the new labels and options follow.
5. Quit and relaunch → the same mode and resolution. Then launch with `-screen-fullscreen 0 -screen-width 1366 -screen-height 768` → Settings shows Windowed, 1366 × 768. Afterwards, choose your normal mode in Settings again, because Unity remembers the last size.
6. **Windowed 1024×768:** play a match. The HUD follows the corners (UI3). **Check U14:** are the whole board and both player areas visible?
7. Alt-tab away during a windowed match for about 10 s → the match keeps running for the other player (`RunInBackground`).

## 10. Open questions and assumptions

**Decisions applied (recommended). The owner can change any of them before handing over:**

| # | Decision | Applied | Alternatives |
|---|---|---|---|
| S1 | Which display modes | **Fullscreen (borderless, today's default) and Windowed** | Also offer exclusive fullscreen. Alt-tab is slower, and it changes the monitor's mode. |
| S2 | When a choice applies | **Immediately**, with no confirmation countdown. The list offers only sizes the monitor can show. | An "Apply" button, or a 15 s "Keep these settings?" countdown that reverts |
| S3 | Remembering the choice | **Unity's own saved screen state.** No new keys, and command-line overrides keep working. | Our own keys in `GameSettingsPrefs`, applied in `Bootstrap`. That would override `-screen-*` test launches unless we add an exception for them. |
| S4 | Which sizes | **The monitor's modes**, at least 1024×720 and no larger than the display (smaller than the display when windowed), without refresh rates | A fixed list (e.g. 1280×720, 1600×900, 1920×1080, 2560×1440, 2560×1600), or all modes including 800×600 |
| S5 | Settings layout | **A 600×400 panel with one row per setting.** The Language row moves into it. | Keep the stacked layout and grow the panel to about 300×520. It's simpler, but the stone sprite stretches further. |

**Assumptions:**
- **Unity restores its saved size and mode at launch** (seen in the owner's registry: `Screenmanager …` keys, 2560×1600, native, mode 1). AC7 verifies it. If it fails, report it; the follow-up is S3's alternative, which skips the startup apply when any `-screen-*` or `-window-mode` argument is present.
- In Fullscreen Window mode at a size with a different aspect, Unity is expected to letterbox rather than stretch (manual check 2).
- `Screen.resolutions` lists the main display's modes on Windows. On laptops with display scaling, it lists physical pixels, which is what `SetResolution` expects.
