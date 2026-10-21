# TASK-UI2: One canvas-scaling standard, so the GUI keeps its share of the screen at any resolution

- **Source:** [RESEARCH-UIScaling_Visibility](../Research/RESEARCH-UIScaling_Visibility.md) → N39, N42. Owner decisions of 2026-10-21: D1 = Expand, D2 = a (menus use a 1920×1080 reference), D7 (the Login background fills any aspect).
- **Severity:** 🟡 Medium (N39), 🟢 Low (N42). Both Confirmed (static).
- **Baseline:** `391e079` plus the uncommitted working tree. Line numbers are from the working tree.
- **Order:** 2 of 4. Independent of UI1. Do it before UI3, which checks the HUD with the new GameScene scaler.
- **Implementer:** Codex

## 1. Objective

Every scene's GUI is the **same share of the screen height** on all 16:10, 16:9 and wider screens. On narrower screens (4:3, 5:4) the GUI shrinks just enough to stay fully visible. **Nothing is ever cut**, and no menu depends on the physical pixel size.

## 2. Current behavior

| Scene | Scaler today | Source |
|---|---|---|
| GameScene | Scale With Screen Size, 2560×1600, match height | `GameScene.unity:791-796` |
| LoginScene | Scale With Screen Size, 1920×1080, match height | `LoginScene.unity:1148-1153` |
| MainMenuScene | **Constant Pixel Size** | `MainMenuScene.unity:1207-1212` |
| SettingsScene | **Constant Pixel Size** | `SettingsScene.unity:1977-1982` |
| LobbyScene | **Constant Pixel Size** | `LobbyScene.unity:2709-2714` |
| ResultScene | **Constant Pixel Size** | `ResultScene.unity:1287-1292` |

Consequences:
- **Size changes with resolution.** Menu buttons are 30 px tall: 4.2 % of the screen height at 720p, 1.4 % at 4K. The logo covers 78 % of the width at 720p, 26 % at 4K.
- **The Lobby is cut on small screens.** It needs at least 1906×800 px, so it's cut at 1366×768, 1600×900, 1680×1050 and 1280×720.
- **The Login `BackGround` is a fixed 1920×1080 image** (anchors at the centre; the sprite is `LoginSceneBack.png`, 1919×1080). It leaves bands on 21:9 screens, and would leave bands at the top and bottom on 16:10 once Expand is used.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scenes/LoginScene.unity` | CanvasScaler match mode; `BackGround` fit |
| `Assets/Scenes/MainMenuScene.unity` | CanvasScaler |
| `Assets/Scenes/SettingsScene.unity` | CanvasScaler |
| `Assets/Scenes/LobbyScene.unity` | CanvasScaler |
| `Assets/Scenes/ResultScene.unity` | CanvasScaler |
| `Assets/Scenes/GameScene.unity` | CanvasScaler match mode |

There are no code dependencies: no script creates canvases or reads `CanvasScaler` or `Screen.width/height`. `LeaveMatchUI` reads the root canvas rect, which works with any scaler.

## 4. Required changes

1. **CanvasScaler on each scene's single root canvas:**

   | Scene | `m_UiScaleMode` | `m_ReferenceResolution` | `m_ScreenMatchMode` | Note |
   |---|---|---|---|---|
   | Login | 1 (Scale With Screen Size) | {x: 1920, y: 1080} (unchanged) | **1 (Expand)** | |
   | MainMenu | **1** | **{x: 1920, y: 1080}** | **1** | |
   | Settings | **1** | **{x: 1920, y: 1080}** | **1** | |
   | Lobby | **1** | **{x: 1920, y: 1080}** | **1** | |
   | Result | **1** | **{x: 1920, y: 1080}** | **1** | |
   | Game | 1 (unchanged) | {x: 2560, y: 1600} (unchanged) | **1** | |

   Keep `m_ReferencePixelsPerUnit` (100), `m_ScaleFactor` and `m_MatchWidthOrHeight`. The match value is ignored in Expand mode; leave it as it is.
2. **Login `BackGround`:**
   - Add an `AspectRatioFitter` with **Aspect Mode = Envelope Parent** and **Aspect Ratio = 1.7769** (1919 / 1080, the sprite's own ratio).
   - The fitter drives the anchors, position and size, so the image covers the whole canvas at any aspect, crops the overflow and isn't distorted.
   - Keep the Image and its sprite as they are.

## 5. Explicit non-goals

- No change to any element's position, size, font or anchors. This task changes only the scalers and the Login background.
- No change to the GameScene HUD layout (TASK-UI3), the announcer (TASK-UI1) or the world-space card canvas (`CardWorldViewPrefab`).
- No new Player resolution settings, and no resolution menu.

## 6. Constraints and compatibility requirements

- Edit the scene files only through the CanvasScaler and `BackGround` objects. Keep every other object, override and reference.
- **Expected visual change on the owner's 2560×1600 screen** (agreed in D2):
  - Main menu, Settings, Lobby and Result become **33 % larger**: the canvas becomes 1920×1200 at ×1.333.
  - Login becomes **10 % smaller**: ×1.333 instead of ×1.481.
  - GameScene is **unchanged**: Expand gives ×1.0.
- **Expected on 1920×1080:** all front-end scenes look exactly as they do today (×1.0).
- Don't commit the dynamic font asset (`AGENTS.md`).

## 7. Acceptance criteria

1. All six CanvasScalers have the values in §4.1, checked in the YAML.
2. At **1280×720, 1366×768, 1920×1080, 2560×1440, 2560×1600, 3840×2160, 1024×768, 1280×1024 and 3440×1440**, no element in any scene is cut or off-screen. That includes the Lobby with the selected-lobby panel open.
3. At the same aspect ratio, a screenshot at one resolution matches a screenshot at another, scaled. For example, 1280×720, 1920×1080 and 3840×2160 differ only in sharpness.
4. On 16:10 and wider, an element's height as a share of the screen height matches 1920×1080 (front-end scenes) or 2560×1600 (Game).
5. The Login background covers the whole screen at every resolution in AC2, with no bands and no stretching.

## 8. Regression tests

- A YAML check (script or grep) of the six CanvasScaler blocks and the new `AspectRatioFitter`.
- The scene still has 0 missing scripts, and its build-list order is unchanged.
- Unity compile, exit 0. No code is expected to change.

## 9. Manual Unity Editor checks

1. Add Game view presets for the resolutions in AC2. Open each scene and switch through the presets. For LobbyScene, enter Play Mode and open the create-lobby and selected-lobby states.
2. Player build in a window:
   ```
   FallenStar-Spellcasters.exe -screen-fullscreen 0 -screen-width 1366 -screen-height 768
   ```
   Then go Login → Main menu → Settings → Lobby → match → Result. Nothing is cut.
3. Player build, fullscreen at the native 2560×1600: the menus are larger than before (expected), and the match HUD is unchanged.

## 10. Open questions and assumptions

- In Expand mode on 4:3 / 5:4, the GameScene HUD shrinks to keep 2560×1600 visible. Until TASK-UI3 re-anchors it, the HUD stays grouped around the centre on wide screens. That's expected at this stage.
- If the +33 % makes any menu element overlap on 2560×1600, report it with a screenshot. Don't move the element here.
