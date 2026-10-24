# TASK-UI7: The low (compact) profile for the five menu scenes, and Settings sizes down to 640×480

- **Source:** [Issue Report #14](../Reports/2026-10-24_IssueReport-14.md) §4 (poll P12 = a, "Yes, next"). It was planned in TASK-UI6 (V1: "separated settings for low resolutions (like 640x480 up to 1024x768) and medium with high (1366 and better)").
- **Baseline:** `ff8a9dd` **plus TASK-N59_N61_F1**, which adds `NoticePanel` to LobbyScene and LoginScene. **Do this task third.** Line numbers and YAML values below are from `ff8a9dd`.
- **Implementer:** Codex
- **Scope:**
  - one script extended (`ResolutionProfileLayout`)
  - five scenes (one component each on the root canvas, with its values)
  - one prefab (`LobbyListItemPrefab`)
  - one serialized value pair (`DisplaySettingsUI` minimum)
  - **No gameplay code.**

## 1. Objective

1. **Below 1366 pixels wide** (`UiResolutionProfile.IsLow`), Login, Main menu, Settings, Lobby and Result switch to a **low profile**:
   - a canvas reference of **1280×720** instead of 1920×1080
   - a compact layout where today's would not fit or would be too small
2. **In the low profile, every menu text is at least 24 canvas units** after autosize: at least **12 px at 640×480** and **19 px at 1024×768**. Only the debug ID texts are exempt (§7).
3. **The medium/high profile (1366 and up) is unchanged.**
4. **Settings offers windowed and fullscreen sizes down to 640×480,** and the profile follows a size change at once.

## 2. Current behavior

### 2.1 The canvases

- **All five scenes:** Scale With Screen Size, reference **1920×1080**, Expand (UI2).
- **The scale factor is** `min(w / 1920, h / 1080)`:

  | Screen | Factor |
  |---|---|
  | 640×480 | 0.33 |
  | 1024×768 | 0.53 |
  | 1280×720 | 0.67 |
  | 1366×768 | 0.71 |
  | 1920×1080 | 1.0 |

- **So at 640×480:**
  - an 18-unit label is 6 px
  - a 160×30 button is 53×10 px
- **With a 1280×720 reference the factor becomes** 0.5 at 640×480, 0.8 at 1024×768, 1.0 at 1280×720 and 1.06 at 1360×768. The canvas is then **at least 1280×720 units**: 1280×960 at 4:3, 1280×1024 at 5:4, 1280×800 at 16:10.

### 2.2 The layouts (authored, medium/high)

**Login, Main menu**
- `Logo`: top-centre, 1000×333.
- `MenuPanelImage`: bottom-centre, 300×300 (Login at y 31.3, Main menu at 0).
- Three buttons, 160×30, bottom-centre:
  - Login: `ButtonLoginAnonymous` y 257, `ButtonLoginSteam` 208, `ButtonExit` 79
  - Main menu: `PlayButton` 235, `SettingsButton` 133, `ExitButton` 31
- Button labels: stretched, scale 0.9, autosize 18–72.
- Login `BackGround`: stretched, with an Envelope `AspectRatioFitter` (unchanged here).

**Settings**
- `Logo`: 1000×333, top.
- `MenuPanelImage`: 600×400, bottom.
- `SettingsTitle`: y 350, 280×46, autosize 26–30.
- Three rows (anchor bottom-centre, pivot centre):
  - labels at x −150, 240×46, autosize **18–22**
  - dropdowns at x 140, 260×38
  - row y: 280, 220, 160
- Each dropdown:
  - `Label`: stretched, size (−35, −13), autosize **16–20**
  - `Arrow`: 20×20
  - `Template`: height 150 / 150 / 300; `Content` 28; `Item` 20
  - `Item Label`: autosize **16–20**; `Item Checkmark`: 20×20
- `BackButton`: y 31, 160×30.

**Lobby: search view**
- `LobbySearchPanel` stretches.
- **`LobbyListPanel`** (sprite `UI board Large Set`, 921×617): centre (−269, 0), **900×800**. It already reaches y ±400 and doesn't fit a 720-unit canvas.
  - `LobbyListScrollView`: 800×500 at (0, 0). Its `Content` is a `VerticalLayoutGroup` that controls child width, so **list items are as wide as the view**.
  - The status background `Image` (child of the scroll view): (82, −290), 250×80.
  - `LobbyListStatusText` (child of the panel): (82, −289), 200×50, autosize 18–72.
- `HeaderPanel` (stretches): `CreateLobbyButton` (−582, −280), `RefreshLobbyListButton` (−394, −280), `BackButton` (28, −280), all 160×30.
- `SelectedLobbyPanel` (650×400 at (628, 251)) is **inactive in the scene, and no code activates it.**

**Lobby: current lobby view**
- `CurrentLobbyPanel` (sprite `UI board Large parchment`): (80, 18), 800×600.
- Status background `Image`: (0, 230.9), 400×100.
- `CurrentLobbyStatusText`: (0, 233), 300×50, autosize 18–72. The longest text is "Waiting the second player ：1/2", about 16 em, so **18 units today.**
- `CurrentLobbyPlayerPanel` / `CurrentLobbyPlayerList`: 600×400 at (0, 47) inside a stretched panel offset by −74.5.
- `StartGameButton` (272.8, −250) and `DisconnectLobbyButton` (−267.4, −250), 160×30.

**Lobby: `LobbyListItemPrefab`** (800×100, children centre-anchored)

| Child | Position | Size | Text |
|---|---|---|---|
| `LobbyNameText` | (−255, 0) | 200×50 | autosize 18–72 |
| `PlayerCountText` | (−79, 0) | 100×50 | autosize 18–72. "Players: 1 / 2" is 18 units today. |
| `LobbyIdText` | (50, 0) | 100×50 | the full lobby ID, a debug text |
| `SelectButton` | (191, 0) | 100×50 | label fixed at 24 |
| `JoinButton` | (320, 0) | 100×50 | label fixed at 24 |

- **`LobbyPlayerItemPrefab`** (600×120): name, role and ID texts, autosize 18–72. The ID is the full player ID, a debug text (N29, parked).

**Result**
- `Title`: 8–92 % width, y 110, autosize 18–56.
- `Reason`: y 0, autosize 18–30.
- `MainMenuButton`: 320×70, label 18–28.
- Everything is already 24 units or more, so Result needs only the reference change.

### 2.3 `ResolutionProfileLayout` (UI6)

- It captures the authored rectangles and the scaler reference in `Awake`.
- It applies the low rectangles and the low reference when `IsLow(Screen.width)` is true, and restores the authored ones when it becomes false. It's polled in `Update`.
- A zero `m_lowReferenceResolution` leaves the scaler alone.
- **It can't change TMP font limits,** so the autosize maximums of 20–22 in Settings stay too small whatever the rectangle.

### 2.4 The Settings minimum

- `DisplaySettingsUI.m_minWidth` / `m_minHeight` = 1024 / 720: code defaults, and serialized in `SettingsScene.unity:4209-4210`.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/UI/ResolutionProfileLayout.cs` | A `LowText` list (font limits), captured and restored like the rectangles |
| `Assets/Scenes/LoginScene.unity`, `MainMenuScene.unity`, `SettingsScene.unity`, `LobbyScene.unity`, `ResultScene.unity` | A `ResolutionProfileLayout` on each root canvas, with the values in §4.3 |
| `Assets/Prefab/UiPrefab/LobbyListItemPrefab.prefab` | Its own `ResolutionProfileLayout` (no scaler change) for the item children |
| `Assets/Scripts/UI/DisplaySettingsUI.cs` + `SettingsScene.unity` | Minimum 640×480 |
| `Docs/AI/Architecture/Architecture.md` (§9), `Dependencies.md` | §4.5 |

**Dependencies:**
- **`PlayerUIPrefab` already has a `ResolutionProfileLayout`.** Its new `m_lowTexts` list stays empty, so the HUD doesn't change.
- **`LobbyListItemPrefab` is instantiated at runtime** under `Content` (`LobbyListUI.OnClickRefreshLobbyList`). A component on its root runs `Awake` on every instance.
- **`NoticePanel`** (TASK-N59_N61_F1) is in LobbyScene and LoginScene.
- **The menus' button labels are scaled 0.9.** The size on screen is font size × 0.9 × the canvas factor.

## 4. Required changes

### 4.1 `ResolutionProfileLayout`: font limits

```csharp
[Serializable]
public class LowText
{
    public TMP_Text m_target;
    public float m_fontSizeMin;
    public float m_fontSizeMax;
}

[SerializeField] private LowText[] m_lowTexts = Array.Empty<LowText>();
```

- **In `Awake`:** capture each target's `enableAutoSizing`, `fontSizeMin`, `fontSizeMax` and `fontSize` with the rectangles. Missing targets are skipped with the existing one-time warning.
- **`ApplyProfile(true)`:**
  - For autosize targets, set `fontSizeMin` / `fontSizeMax` to the low values.
  - For fixed-size targets, set `fontSize = m_fontSizeMax`.
  - **`ApplyProfile(false)`** restores the captured values.
- **No other behavior change.** The rectangles, the scaler and the polling stay as UI6 made them.

### 4.2 Where the component goes

- **Each root canvas:** `LoginCanvas`, `MainMenuCanvas`, `SettingsCanvas`, `LobbyCanvas`, `ResultCanvas`.
  - `m_canvasScaler` = that canvas's scaler
  - `m_lowReferenceResolution` = **(1280, 720)**
- **The root of `LobbyListItemPrefab`:** `m_lowReferenceResolution` = (0, 0), so it leaves the scaler alone.

### 4.3 Low values

All values are in canvas units at the 1280×720 reference. Anchors and pivots stay as authored unless a row says otherwise. "Size" is `sizeDelta`. Every `m_scale` is 1.

**LoginScene** (`LoginCanvas`)

| Target | Authored pos / size | Low pos / size |
|---|---|---|
| `Logo` | (0, 0) / 1000×333 | (0, −16) / **690×230** |
| `MenuPanelImage` | (0, 31.3) / 300×300 | (0, 24) / 400×380 |
| `ButtonLoginAnonymous` | (0, 257) / 160×30 | (0, 300) / **300×56** |
| `ButtonLoginSteam` | (0, 208) / 160×30 | (0, 220) / 300×56 |
| `ButtonExit` | (0, 79) / 160×30 | (0, 80) / 300×56 |
| `NoticePanel` | (0, 400) / 1200×72 | (0, 410) / 1100×56 |

- The labels stretch with their buttons. Autosize 18–72 in a 56-unit button gives about 33 units on screen.
- No `LowText` is needed.
- **The vertical stack:** buttons 80–356, panel 24–404, notice 410–466, logo 474–704.

**MainMenuScene** (`MainMenuCanvas`)

| Target | Authored | Low |
|---|---|---|
| `Logo` | (0, 0) / 1000×333 | (0, −16) / 690×230 |
| `MenuPanelImage` | (0, 0) / 300×300 | (0, 24) / 400×380 |
| `PlayButton` | (0, 235) / 160×30 | (0, 300) / 300×56 |
| `SettingsButton` | (0, 133) / 160×30 | (0, 190) / 300×56 |
| `ExitButton` | (0, 31) / 160×30 | (0, 80) / 300×56 |

**SettingsScene** (`SettingsCanvas`)

| Target | Authored | Low |
|---|---|---|
| `Logo` | (0, 0) / 1000×333 | (0, −8) / 540×180 |
| `MenuPanelImage` | (0, 0) / 600×400 | (0, 12) / 840×508 |
| `SettingsTitle` | (0, 350) / 280×46 | (0, 470) / 420×56 |
| `LanguageLabel` | (−150, 280) / 240×46 | (−200, 390) / 340×56 |
| `LanguageDropdown` | (140, 280) / 260×38 | (195, 390) / 380×52 |
| `DisplayModeLabel` | (−150, 220) / 240×46 | (−200, 310) / 340×56 |
| `DisplayModeDropdown` | (140, 220) / 260×38 | (195, 310) / 380×52 |
| `ResolutionLabel` | (−150, 160) / 240×46 | (−200, 230) / 340×56 |
| `ResolutionDropdown` | (140, 160) / 260×38 | (195, 230) / 380×52 |
| `BackButton` | (0, 31) / 160×30 | (0, 48) / 300×56 |
| each dropdown → `Label` | (−7.5, −0.5) / (−35, −13) | (−12, 0) / (−52, −8) |
| each dropdown → `Arrow` | (−15, 0) / 20×20 | (−22, 0) / 28×28 |
| each dropdown → `Template` | (0, 2) / (0, 150 · 150 · 300) | (0, 2) / (0, **140 · 100 · 360**) for Language · Display mode · Resolution |
| each `Template` → … → `Content` | (0, 0) / (0, 28) | (0, 0) / (0, 52) |
| each `Content` → `Item` | (0, 0) / (0, 20) | (0, 0) / (0, 44) |
| each `Item` → `Item Checkmark` | (10, 0) / 20×20 | (18, 0) / 28×28 |
| each `Item` → `Item Label` | (5, −0.5) / (−30, −3) | (10, 0) / (−50, −4) |

| `LowText` target | Authored autosize | Low autosize |
|---|---|---|
| `SettingsTitle` | 26–30 | 30–40 |
| `LanguageLabel`, `DisplayModeLabel`, `ResolutionLabel` | 18–22 | 24–30 |
| each dropdown `Label` | 16–20 | 24–30 |
| each `Item Label` | 16–20 | 24–28 |

- **The dropdown items are clones of `Item`,** made when the list opens, so they inherit the template's current (low or authored) values.
- TMP_Dropdown flips the list upward when it would leave the canvas. That's expected for Resolution at 1280×720.

**LobbyScene** (`LobbyCanvas`). Positions are relative to each object's parent, as in the YAML.

| Target | Authored | Low |
|---|---|---|
| `LobbyListPanel` | (−269, 0) / 900×800 | (0, −8) / **1040×672** |
| `LobbyListScrollView` | (0, 0) / 800×500 | (0, 40) / 920×440 |
| `LobbyListScrollView` → `Image` (status background) | (82, −290) / 250×80 | (130, −304) / 300×76 |
| `LobbyListStatusText` | (82, −289) / 200×50 | (130, −264) / 280×56 |
| `HeaderPanel` → `CreateLobbyButton` | (−582, −280) / 160×30 | (−380, −272) / 220×56 |
| `HeaderPanel` → `RefreshLobbyListButton` | (−394, −280) / 160×30 | (−140, −272) / 220×56 |
| `HeaderPanel` → `BackButton` | (28, −280) / 160×30 | (390, −272) / 200×56 |
| `CurrentLobbyPanel` | (80, 18) / 800×600 | (0, 0) / 860×640 |
| `CurrentLobbyPanel` → `Image` (status background) | (0, 230.9) / 400×100 | (0, 248) / 560×92 |
| `CurrentLobbyStatusText` | (0, 233) / 300×50 | (0, 250) / 520×56 |
| `StartGameButton` | (272.8, −250) / 160×30 | (250, −272) / 260×56 |
| `DisconnectLobbyButton` | (−267.4, −250) / 160×30 | (−250, −272) / 260×56 |
| `NoticePanel` | (0, −40) / 1400×72 | (0, −6) / 1100×56 |

- **The search view's bottom row,** in canvas x, left to right:
  - Create: −490 … −270
  - Refresh: −250 … −30
  - status: −20 … 280
  - Back: 290 … 490

  The row is at y −300 … −244, and the list panel at ±520 × −344 … 328.
- **The scroll view** ends at y −188, so it doesn't touch the row.
- **`NoticePanel` overlaps** the top frames of both panels, not their lists or status texts. Its dark box keeps it readable.
- **`SelectedLobbyPanel` is unchanged** (§5).

**`LobbyListItemPrefab`** (item width in low = the scroll view width, 920)

| Child | Authored | Low |
|---|---|---|
| `LobbyNameText` | (−255, 0) / 200×50 | (−345, 0) / 200×50 |
| `PlayerCountText` | (−79, 0) / 100×50 | (−120, 0) / **220×50** |
| `LobbyIdText` | (50, 0) / 100×50 | (90, 0) / 180×50 |
| `SelectButton` | (191, 0) / 100×50 | (265, 0) / 140×56 |
| `JoinButton` | (320, 0) / 100×50 | (400, 0) / 120×56 |

- The extents are −445 … 460 inside ±460.
- The button labels are fixed at 24 units and stay so.
- **In medium/high the item is 800 wide,** so the authored positions still apply.

**ResultScene:** only `m_lowReferenceResolution` (1280, 720). Both lists are empty.

**Adjusting the numbers:** they're calculated from the YAML, not seen. You may change any low value by up to ±15 % to remove an overlap or a cut that the screenshots show, keeping §7's limits. List every change in the report.

### 4.4 Settings minimum

- Set `m_minWidth = 640` and `m_minHeight = 480`: both the code defaults and the values serialized in `SettingsScene.unity`.
- **No other change** to `DisplaySettingsUI`. The list still shows only what the monitor reports, within the work-area bounds (N49).

### 4.5 Docs

- **`Architecture.md` §9:** a "UI7 menu low profile" paragraph:
  - the 1280×720 reference
  - `LowText`
  - the item prefab component
  - the 640×480 minimum
- **`Dependencies.md`:** a new **RP21**. Menu scenes have `ResolutionProfileLayout` low values. A new menu element must get a low rectangle (or fit as authored), and a text that autosizes below 24 must get a `LowText`.

## 5. Explicit non-goals

- **No change to the medium/high layout** of any scene, the HUD, `PlayerUIPrefab` or the card prefabs.
- **`SelectedLobbyPanel`:** no change. It's inactive and never shown. If it's ever enabled, it needs its own low rectangle.
- **`LobbyPlayerItemPrefab`:** no change. Its name and role texts already reach 24 units.
- **No new art, sprites or fonts.** Sprites stretch as they do today. The list panel's low aspect (1.55) is closer to its sprite's (1.49) than the authored one (1.13).
- **No change to `UiResolutionProfile`'s threshold** (1366).
- **No change to the debug ID texts** (N29, parked).
- **No GUI redesign.** The owner's "Redraw GUI panels" idea is ranked Later.

## 6. Constraints and compatibility requirements

- **Keep every serialized field name, every scene object, every UnityEvent binding and every localization component.** Add components and values only.
- **Don't move objects in the hierarchy.** `NoticePanel` stays the last child of its canvas.
- **The scene canvases keep their authored reference (1920×1080) in the YAML.** The component switches it at runtime.
- **Unity 6 APIs, no warnings of our own.**
  - Keep baseline copies in `build/ui7-verification/baseline/`.
  - Restore the dynamic font before handing over (`AGENTS.md`).

## 7. Acceptance criteria

**Sizes:**
- **Low:** 640×480, 800×600, 1024×768, 1280×720, 1280×1024, 1360×768
- **Medium/high:** 1366×768, 1920×1080, 2560×1600

1. **The profile switches at the threshold** in all five scenes: low at 1365 wide and below, medium/high at 1366 and above, in both directions, without a scene reload.
2. **Text size in low:** at every low size, in **en, ru and ja**, every active TMP text in the five scenes has `fontSize × lossy scale ≥ 24` canvas units after `ForceMeshUpdate`. That includes:
   - each dropdown with its list open
   - a lobby list with one item (sample data)
   - the current-lobby view with two player items
   - `NoticePanel` with the longest notice
   - both result texts

   **Exempt, but measured and reported:** `LobbyIdText`, `PlayerIdText`, and the dropdowns' template "Option A" placeholder.
3. **No cut:** none of the texts above has `isTextTruncated`. Every RectTransform of an active element lies inside its canvas rect, with no element off screen.
4. **No overlap:**
   - No two `Selectable`s overlap at any size.
   - In the Lobby search view, the scroll view doesn't overlap the bottom row.
   - `NoticePanel` doesn't overlap any button, list or status text.
5. **Medium/high unchanged:** at 1366×768, 1920×1080 and 2560×1600, every RectTransform and TMP font limit in the five scenes and in `LobbyListItemPrefab` equals the baseline values.
6. **The HUD is unchanged:** `PlayerUIPrefab`'s low and medium profiles behave as before. Its `m_lowTexts` is empty.
7. **Settings:**
   - The resolution list includes the monitor's sizes down to 640×480.
   - Choosing 1280×720 (windowed) switches the Settings scene to low at once, and choosing 1920×1080 switches back.
   - Other scenes open in the matching profile.
8. **Build:** 0 errors, no warnings of our own. The release player builds.

## 8. Regression tests

- **A Play Mode harness** (temporary, in `Temp/UI7Verification/`). It loads each scene at each size (Game view sizes or `Screen.SetResolution` in a player), sets each locale, fills the lobby views with sample data, and checks AC2–AC5. Report the smallest text per scene and size.
- **Screenshots, ru (the longest), at:**
  - 640×480, 1024×768, 1280×720 and 1366×768: all five scenes
  - plus the Lobby current-lobby view and an open Resolution dropdown
  - plus en at 2560×1600 for one scene, as the unchanged reference
- **Check what each image shows** before naming it (wrong names happened in UI1–UI4).
- **Run the existing UI6 HUD harness** once, to show AC6.
- **The report lists each case with its result** and every §4.3 adjustment. Remove the harness from `Assets` and `Temp`, and keep a copy in `build/ui7-verification/`.

## 9. Manual Unity Editor checks

**Setup:** Game view presets 640×480, 1024×768, 1280×720, 1366×768 and 1920×1080, plus a player build.

1. **Each menu scene at 640×480 and 1024×768, in ru:**
   - every text is readable
   - nothing is cut or overlaps
   - the buttons are big enough to click easily
2. **Lobby:**
   - Find Lobby with no lobby, then with a lobby: the list item shows the name, the player count, Select and Join
   - join, so the current-lobby view shows two players and the status line
3. **Settings:**
   - open each dropdown at 640×480
   - in the build, pick 800×600 windowed: the scene switches to the compact layout at once
   - pick 1920×1080: it switches back
4. **1366×768 and 1920×1080:** every menu looks exactly as before.
5. **Messages (from TASK-N59_N61_F1) at 640×480:** the Lobby and Login notices are readable and don't cover a button. Use Find Lobby with the network off.

## 10. Open questions and assumptions

- **A1, the 1280×720 reference.** It's two thirds of the authored 1920×1080, so most rectangles only need enlarging, not re-planning. Except the Lobby, the existing layouts fit 1280×720 after that.
- **A2, the 24-unit floor.** It's 12 px at 640×480 and 19 px at 1024×768, the same pixel targets as UI6's HUD (30 units at a 1600×1000 reference).
- **A3, the numbers are calculated, not seen** (as in UI6). That's why Codex may adjust by ±15 % and must send screenshots.
- **A4, the jump at 1366.** At 1360×768 (low), labels are about 1.5× larger than at 1366×768 (medium). That's the same kind of step as in the HUD, and accepted in UI6.
