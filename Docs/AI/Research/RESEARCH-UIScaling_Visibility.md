# RESEARCH: GUI scaling across resolutions, and text visibility (announcer cut)

- **Date:** 2026-10-21
- **Request (owner):**
  1. The GUI must take **the same % of the screen at any resolution**.
  2. **Update visibility.** The game-scene announcer cuts its sentence; fix it.
- **Status:** research done. **The owner's decisions (2026-10-21) are recorded below, and tasks have been created:**
  - [UI1](../Tasks/TASK-UI1_AnnouncerFullText.md): the announcer shows its full text
  - [UI2](../Tasks/TASK-UI2_CanvasScalingStandard.md): one canvas-scaling standard
  - [UI3](../Tasks/TASK-UI3_HudAnchoringAndHand.md): the HUD follows the screen edges, and the hand stays centred
  - [UI4](../Tasks/TASK-UI4_PreviewClampAndConfirmLabel.md): the card preview stays on screen, and the Russian Confirm label fits
- **Implemented** by Codex on 2026-10-21 and reviewed in [report #9](../Reports/2026-10-21_IssueReport-9.md): N37–N46 are closed, the visual checks are pending, and U14 is still open. Correction: UI2's AC4 holds on 16:9 and wider. On 16:10, front-end GUI takes 90 % of its 16:9 height share (report #9 §1). Follow-up: [UI5](../Tasks/TASK-UI5_ResolutionSetting.md), resolution and display mode in Settings.
- **Decisions:** **D0–D7 all as recommended** (§5).
- **Owner remark R1 (2026-10-21):** "I want to see card in hand by horizontal center, vertical position same. You can move and modify any elements of GUI attached to this."
  - It goes into UI3: the hand stays centred and keeps its vertical position, it's compressed so it never runs under the buttons, and the one-frame jump is removed.
  - New findings from it: N45 and N46.
- **Baseline:** `391e079` plus the uncommitted working tree. The L6 change in `GameAnnouncerUI.cs` is change-only refresh and doesn't touch layout. `GameScene.unity` and `PlayerUIPrefab.prefab` have no uncommitted changes.
- **Method:**
  - Parsed the YAML of all 6 build scenes and the UI prefabs: Canvas, CanvasScaler, RectTransform, TMP settings and prefab-instance overrides.
  - Measured every announcer line with the glyph advances of the committed font atlas (`MPLUSRounded1c-Medium SDF`, 282 glyphs, point size 139, line height 1.485 em).
  - Fit-checked every localized label against its rect.
  - Read the owner's display and the stored Player resolution from WMI and the registry.
  - **Nothing was run in Unity.** Text widths are estimates (kerning and TMP padding aren't modelled). The proposals keep at least 15 % headroom to cover that.
- **Assumption A1 ("visibility"):** every GUI element and every text is **fully on screen, not cut, not distorted and readable**, in all scenes, all three languages (en/ja/ru) and all resolutions. *Correct me in D0 if you meant something else.*
- **New IDs:** findings continue the issue register from report #8: **N37–N46**, plus unknown **U14**. They go into report #9.

## 1. What exists today (verified)

| # | Fact | Source |
|---|---|---|
| F1 | All UI is **uGUI, Screen Space – Overlay**, one canvas per scene. No UI Toolkit, and no code creates canvases or scalers. | Scene YAML; grep of `Assets/Scripts` |
| F2 | **CanvasScaler per scene:**<br>• `GameScene`: Scale With Screen Size, reference **2560×1600**, match **height** (1)<br>• `LoginScene`: Scale With Screen Size, reference **1920×1080**, match **height** (1)<br>• `MainMenuScene`, `SettingsScene`, `LobbyScene`, `ResultScene`: **Constant Pixel Size**. Their reference 800×600 is ignored in that mode. | `GameScene.unity:791-796`, `LoginScene.unity:1148-1153`, `MainMenuScene.unity:1207`, `SettingsScene.unity:1977`, `LobbyScene.unity:2709`, `ResultScene.unity:1287` |
| F3 | **The owner's display is 2560×1600 (16:10)**, the same as the GameScene reference. The Player stores 2560×1600 native, fullscreen mode 1 (Fullscreen Window). Player settings: `defaultIsNativeResolution: 1`, `resizableWindow: 0`, `fullscreenMode: 1`. | WMI (both GPUs report 2560×1600); `HKCU\Software\DefaultCompany\FallenStar-Spellcasters` `Screenmanager Resolution *`; `ProjectSettings.asset:84, 99, 111` |
| F4 | The Editor Game view in the default layout is fixed at **2560×1440 (16:9)**, shown at 0.33 zoom. The maximized layout uses Free Aspect 989×706. | `UserSettings/Layouts/default-6000.dwlt:1786`, `CurrentMaximizeLayout.dwlt:1067` |
| F5 | The GameScene canvas has one child, the in-scene `PlayerUIPrefab` (a NetworkObject). **Its root is a 100×100 rect anchored at the screen centre** in both the prefab and the scene override. **Every HUD element is positioned relative to that centre**, either at the root centre or at the root's top-right corner, which is (50, 50) from the screen centre. | `PlayerUIPrefab.prefab` root; `GameScene.unity:375` (PrefabInstance overrides `m_AnchorMin/Max = 0.5`, `m_SizeDelta = 100`) |
| F6 | `LeaveMatchUI.Update` resizes its own rect to the canvas size **every frame**, to work around F5. That's why only the Leave button sits in a real screen corner. | `UI/LeaveMatchUI.cs:32-34` |
| F7 | **Non-uniform scales on UI elements:**<br>• `PhaseAnouncer` (200×50) **×(7.91, 1.93)**<br>• `Panel` (top banner, sliced sprite) ×(16.22, 2.79)<br>• `ConfirmTurnBtn` (160×30) ×(1.59, 3.10)<br>They have existed since `75fab38` ("07/17 Update"). | `PlayerUIPrefab.prefab:30`, `:1596`, `:2325` |
| F8 | `PhaseAnouncer` TMP:<br>• margins (L 3.54, T −9.07, R 2.44, B −59.71), so the text area is **194 × 119 local units**<br>• alignment Left/Top<br>• the scene overrides set min font 14, **NoWrap** and **Ellipsis** | `PlayerUIPrefab.prefab:134`; `GameScene.unity` overrides on `4772283208930924661` |
| F9 | `GameAnnouncerUI` is **a component added to the scene instance**, not part of the prefab. Its values live in `GameScene.unity`: `m_maxVisibleLines 2`, `m_autoSize 1`, `m_minFontSize 14`, `m_maxFontSize 72`, `m_useEllipsis 1`. `ApplyAnnouncerTextFormat()` re-applies **NoWrap**, max lines, autosize min/max and **Ellipsis/Truncate** in `Awake`, `OnValidate` and every text change. **So TMP settings changed in the prefab are overwritten.** | `GameScene.unity:599`; `UI/GameAnnouncerUI.cs:28-34, 202-215` |
| F10 | Line 2 of the announcer is built as `$"{_prefix}<color=…>{_coloredPart}</color>"`, **with no separator**. The en/ru prefixes have no trailing space or colon ("Current turn", "Waiting for respawn", "Сейчас ходит", "Ожидает возрождения"). ja ends with "：". | `GameAnnouncerUI.cs:119-121`; `UI_table_en/ru/ja.asset` |
| F11 | TMP exposes `isTextTruncated`. `TextMeshProUGUI` sets it in Ellipsis/Truncate mode. | `com.unity.ugui/Runtime/TMP/TMP_Text.cs:875`, `TextMeshProUGUI.cs:2801, 2817` |
| F12 | The game camera is **orthographic, size 40**, so its vertical extent is fixed and the horizontal extent follows the aspect ratio. | `GameScene.unity:1579-1581` |
| F13 | Card preview: clicking a world card instantiates `CardViewUIPrefab` under the canvas found by `FindFirstObjectByType<Canvas>()`, centred on the mouse, with no clamping. | `CardPreviewController.cs:15-19, 40-48`; `PlayerSession.cs:451-457` |
| F14 | Hand: `HandContainer` (1600×250, `HorizontalLayoutGroup` spacing 220). The hand limit is 10, so about 2 110 units at 10 cards. | `PlayerUIPrefab.prefab`; `GameMaster.cs:80` |

## 2. Findings

| ID | Sev. | Finding | Location | Evidence | Verdict |
|---|---|---|---|---|---|
| **N37** | 🟡 Medium | **The announcer cuts line 2 (whose turn or respawn) in every case and every language.** The text area is only **194 local units** wide (F8). It is then stretched ×7.91 horizontally and ×1.93 vertically (F7). With NoWrap and an autosize floor of 14 (F9), every line-2 variant needs a font size of **7.6–12.0** to fit, so TMP ends it with "…". Only **54–86 %** of the sentence is shown (§3). The same stretch also draws every glyph **4.1× wider than tall**. | `PlayerUIPrefab.prefab:30, 134`; `GameScene.unity:599`; `GameAnnouncerUI.cs:202-215` | Glyph-advance measurement, all 24 line-2 variants (en/ja/ru × your/opponent × red/blue × turn/respawn) | Confirmed (static). Not yet seen in a screenshot. |
| **N38** | 🟢 Low | **No separator between the prefix and the player part (en/ru).** It renders "Current turn**red** player. Your turn" and "Ожидает возрождения**красный** игрок…". In en the respawn case also repeats "Waiting for respawn" on both lines. | `GameAnnouncerUI.cs:121`; `UI_table_en/ru.asset` (`Announcer.TurnPrefix`, `Announcer.RespawnPrefix`) | Code and string data | Confirmed |
| **N39** | 🟡 Medium | **Four scenes use Constant Pixel Size, so their GUI is a fixed number of pixels.** The share of the screen changes with resolution, and small screens cut the layout:<br>• **Main menu, Settings and Lobby buttons (30 px tall):** 4.2 % of screen height at 720p, 2.8 % at 1080p, 1.9 % on the owner's 2560×1600, 1.4 % at 4K. The 1000 px logo covers 78 % → 52 % → 39 % → 26 % of the width.<br>• **Lobby** needs at least **1906×800 px**: the selected-lobby panel reaches x = ±953 and the list panel is 800 tall. **At 1366×768, 1600×900, 1680×1050, 1280×720 and the 1024×768 default it's cut.** | Scene CanvasScalers (F2); `LobbyScene` `SelectedLobbyPanel` (628, 251) 650×400, `LobbyListPanel` 900×800 | YAML and arithmetic | Confirmed (static) |
| **N40** | 🟡 Medium | **GameScene HUD positions are tied to the screen centre (F5), not to the screen edges.** The canvas height is always 1600 and its width is 1600 × aspect.<br>• **16:9:** the right-side HUD sits 142 units further from the edge than on 16:10.<br>• **21:9:** 631 units further, so the HUD bunches in the middle.<br>• **4:3** (canvas 2133 wide): HP/Lives text is mostly off-screen, the **Send button is fully off-screen**, Confirm-turn is half off-screen, and the right end of the mana texts is cut.<br>• **5:4** (canvas 2000 wide): worse.<br>The same happens in any narrow Editor Game view or ParrelSync clone window. | `PlayerUIPrefab.prefab` (all children); `GameScene.unity:375` | Arithmetic from the YAML anchors and positions | Confirmed (static). 4:3 is rare on PC, but there it blocks play. |
| **N41** | 🟢 Low | **Non-uniform scales distort UI (F7).** The announcer glyphs are 4.1:1 wide. The Confirm-turn label is stretched about 1.9× vertically. The banner's rounded corners are stretched 16×. `LeaveMatchUI` resizes itself every frame to compensate for the 100×100 root (F6). | F6, F7 | YAML; code | Confirmed |
| **N42** | 🟢 Low | **The Login background is a fixed 1920×1080 image at the centre.** It leaves bands at the sides on screens wider than 16:9 (21:9 canvas is 2520 wide). It would also leave bands at the top and bottom of 16:10 if the scaler moved to Expand mode. | `LoginScene` `BackGround` (anchors 0.5, 1920×1080) | YAML and arithmetic | Confirmed (static) |
| **N43** | 🟢 Low | **The card preview isn't clamped to the screen (F13).** A world card clicked near an edge shows a partly off-screen preview. Latent: `FindFirstObjectByType<Canvas>()` would pick a world-space card canvas if one existed when the session starts. Today none exists at that time. | `CardPreviewController.cs:17-18, 46-48` | Code | Plausible |
| **N44** | ⚪ Info | **ru "Подтвердить" doesn't fit the 100×100 target-Confirm button** at the minimum size of 18. In Overflow mode it spills about 15 units past both edges; it isn't cut. Every other localized label fits its rect (fit-check of 27 labels). | `PlayerUIPrefab` `ConfirmBtn/Text (TMP)` | Width estimate | Plausible |
| **N45** | 🟢 Low | **A full hand runs under the HUD buttons.** Cards are drawn 200 wide, with centres 220 apart (the layout gives card roots 0 width). 9–10 cards span ±990 / ±1090. On 16:10 and narrower, that overlaps `ConfirmBtn` (x 950–1050) and `ConfirmTurnBtn` (x 947–1203). Found while checking R1. | `PlayerUIPrefab` `HandContainer` (`HorizontalLayoutGroup`, spacing 220); `GameMaster.cs:80` (hand limit 10) | Arithmetic | Confirmed (static). UI3 checks the 0-width assumption. |
| **N46** | 🟢 Low | **The hand jumps sideways for one frame on every refresh.** `UpdateCards` calls `ClearHandUI()`, which uses `Destroy` (applied at the end of the frame), and then adds the new views to the same container. For that frame the layout includes both sets, so the new hand is drawn shifted to the right. | `UI/PlayerUI.cs:258-305, 508-527` | Code; Unity's deferred `Destroy` | Plausible (not observed) |
| **U14** | ❓ | **World view on narrow screens (not GUI).** Because the camera is orthographic with a fixed vertical size (F12), a 4:3 screen shows about 17 % less of the board horizontally than 16:10. It's unknown whether the board edges or player areas get cut. | `GameScene.unity:1579-1581` | — | Unknown. Check during the resolution pass. |

**No other text is cut.** The fit-check covered the Login, Main menu, Settings, Lobby, Result and HUD labels and the Leave dialog. All of them fit at their reference size in en/ja/ru, except N44.

## 3. Announcer measurement (N37)

The text area is 194 local units wide. "Visible" is the share of the line that fits at the autosize floor of 14. Lines are shown as rendered today (without a separator, N38).

| Locale | Line-2 variant (worst and best) | Width (em) | Font size needed | Visible at 14 |
|---|---|---|---|---|
| en | `Waiting for respawnblue player. This is the opponent` | 25.45 | 7.6 | **54 %** |
| en | `Current turnred player. Your turn` | 16.18 | 12.0 | 86 % |
| ru | `Ожидает возрождениякрасный игрок. Это оппонент` | 25.16 | 7.7 | **55 %** |
| ru | `Сейчас ходитсиний игрок. Ваш ход` | 16.85 | 11.5 | 82 % |
| ja | `現在の手番：赤のプレイヤー。あなたの番です` | 21.00 | 9.2 | **66 %** |
| ja | `復活待ち：赤のプレイヤー。相手です` | 17.00 | 11.4 | 82 % |

- **Line 1** ("Phase 12") always fits: 5.5–13.1 em.
- Autosize uses **one size for the whole block**, so the long line 2 also shrinks line 1 to 14.
- All 24 line-2 variants are cut. Phases without a target (draw, selection, round break) show only line 1, so nothing is cut there.

**With the proposed fix** (§6.3: no scale, a box of about 1560×150, font 24–48), the widest line including ": " is about 26.5 em. That is **≈ 1 270 units at size 48**, so it fits with about 18 % headroom. Two lines at 48 need 143 units of height.

## 4. What "same % of screen" means across aspect ratios

At the **same aspect ratio** (1280×720, 1920×1080, 2560×1440, 3840×2160), any Scale-With-Screen-Size setup gives exactly the same % everywhere. The real choice is what happens when the **aspect ratio differs** from the design. With the GameScene design (16:10):

| Mode | 16:9 / 21:9 (wider) | 4:3 / 5:4 (narrower) | Cut? |
|---|---|---|---|
| **Match height** (today's GameScene, Login) | Same % of height. Extra width at the sides. | Same % of height. **The sides are cut.** | Narrower: **yes** |
| **Match width** | Same % of width. **Top and bottom cut** (the announcer at y 729 > 720) | Same % of width | Wider: **yes** |
| Blend 0.5 | Small cuts | Small cuts | Both, a little |
| **Expand** | Behaves like match height | Shrinks just enough that the whole design fits | **Never** |

**Expand** is the standard answer to "same % and never cut": the full reference rect is always on screen. Content anchored to the edges then follows the real screen edges.

## 5. Decisions needed

| # | Question | Options | Recommendation |
|---|---|---|---|
| **D0** | What does "visibility" cover? | A1 (above) / something else | **A1** |
| **D1** | GUI behaviour on other aspect ratios | **Expand** / match height / match width / blend | **Expand.** Same % of height on 16:10 and wider. Shrinks only on 4:3 and 5:4. Never cut. |
| **D2** | Baseline size of the **menu scenes** (Login, Main menu, Settings, Lobby, Result) | **a. Reference 1920×1080**: menus look everywhere as they look on a 1080p screen today. On your 2560×1600 they're **+33 %** (buttons 30→40 units, text about 20→27). Login is −10 % (from ×1.48 to ×1.33).<br>**b. Reference 2560×1600**: menus look everywhere as they look on *your* screen today. On 1080p they're **−32 %**: 13–14 px text and 20 px buttons. | **a.** It matches Login's existing reference, improves readability ("visibility"), and leaves 1080p players unchanged. Option b makes 1080p text too small. |
| **D3** | GameScene HUD layout | **Re-anchor** each element to its screen edge or corner, converted so it looks **identical on 2560×1600** / keep the centre-relative positions and change only the scaler | **Re-anchor.** HUD corners follow the real edges on 16:9 and 21:9, and the 100×100-root workaround (F6) goes away. |
| **D4** | Announcer look | Box about 1560×150 at the same place, no scale, **two lines, centred**, font 24–48 / same box, **left-aligned** (as today) / other | **Centred, 24–48.** The text is about 1.8× taller than today's floor-size glyphs, and not distorted. |
| **D5** | Announcer wording (N38) | **a.** Add ": " to the en/ru prefixes only ("Current turn: red player. Your turn")<br>**b.** a + shorter respawn prefix ("Respawning: …" / "Возрождается: …") so it doesn't repeat line 1<br>**c.** You provide the text | **b**, or **a** if you prefer to keep the current wording |
| **D6** | If a future text is still too long | **Shrink, then "…" as a last resort**, plus a warning log and a test covering all current variants / allow a third line | **Shrink + "…" + warning.** With D4 there's 18 % headroom, and a third line would need a taller banner. |
| **D7** | Optional extras | **N42** Login background fills any aspect (needed with D1 = Expand) · **N43** clamp the card preview to the screen · **N44** ru Confirm label fits (lower that label's autosize floor to 14) | **All three** (each is small) |

## 6. Proposed design (assuming the recommendations)

### 6.1 Canvas standard (N39, D1, D2)

| Scene | Scale mode | Reference | Screen match mode |
|---|---|---|---|
| Login, Main menu, Settings, Lobby, Result | Scale With Screen Size | **1920×1080** | **Expand** |
| Game | Scale With Screen Size | **2560×1600** (unchanged; the HUD is authored for it) | **Expand** |

- These are scene-only changes to the CanvasScaler fields. Keep `m_ReferencePixelsPerUnit` at 100.
- Login `BackGround` (N42): anchors stretch (0,0)–(1,1), plus `AspectRatioFitter` = Envelope Parent, ratio 16:9. It covers any aspect and crops the overflow.

### 6.2 GameScene HUD anchoring (N40, N41, D3)

- **Root:** set the `PlayerUIPrefab` instance root to stretch (anchors 0–1, size 0). The scene override (F5) must be changed too, or the prefab change has no effect.
- **Children:** re-anchor each child with its position converted so it looks **identical on 2560×1600**:

| Element | New anchor (min = max) | New pivot | New anchored pos | New size / scale |
|---|---|---|---|---|
| Panel (banner) | top-centre (0.5, 1) | 0.5, 0.5 | (0, −167) | 1622.3 × 279.1, scale 1 |
| PhaseAnouncer | top-centre (0.5, 1) | 0.5, 0.5 | (0, −119) | **≈1560 × 150**, scale 1 (§6.3) |
| HPTextBar | top-right (1, 1) | unchanged | (−140, −100) | unchanged |
| LifeTextBar | top-right (1, 1) | unchanged | (−140, −150) | unchanged |
| ManaTextBar | top-right (1, 1) | (1, 0.5) | (−40, −200) | unchanged |
| OpponentManaTextBar | top-right (1, 1) | (1, 0.5) | (−40, −250) | unchanged |
| ManaMessageText | top-centre (0.5, 1) | unchanged | (0, −400) | unchanged |
| HandContainer | bottom-centre (0.5, 0) | unchanged | (0, 300) | unchanged |
| ConfirmBtn | bottom-right (1, 0) | unchanged | (−280, 380) | unchanged |
| SendBtn | bottom-right (1, 0) | unchanged | (−130, 380) | unchanged |
| ConfirmTurnBtn | bottom-right (1, 0) | unchanged | (−205, 249) | 255.1 × 92.9, scale 1 |
| LeaveMatch | stretch (0,0)–(1,1) | unchanged | (0, 0) | size 0 |

- **LeaveMatchUI:** with LeaveMatch stretched, delete the per-frame resize (`LeaveMatchUI.cs:32-34`) and the two fields it uses. Otherwise the code would double the size.
- **Expected visual changes on 2560×1600:**
  - The banner's rounded corners become normal-sized. They were stretched before.
  - The Confirm-turn label is no longer stretched, so it's somewhat shorter and wider.
  - Nothing moves.

### 6.3 Announcer (N37, N38, D4–D6)

- `PhaseAnouncer`:
  - scale 1, size ≈1560×150, margins 0
  - alignment Center/Middle (or Left/Middle if D4 = left)
- **The values must change in `GameScene.unity`** on the added `GameAnnouncerUI` (F9): `m_minFontSize` **24**, `m_maxFontSize` **48**. Keep `m_maxVisibleLines` 2 and `m_useEllipsis` 1 (D6).
- Strings (D5):
  - en: "Current turn: ", "Respawning: "
  - ru: "Сейчас ходит: ", "Возрождается: "
  - ja: unchanged
  - The trailing space must be kept: edit in the Localization Tables window, or write YAML double-quoted values.
- Code (small): after `ForceMeshUpdate()`, if `m_timeText.isTextTruncated`, log **one** `Debug.LogWarning` per distinct text (a HashSet). Warnings stay in release (N34 rule).
- Test: an Edit Mode or Play Mode check that renders all 24 line-2 variants plus all phase lines in en/ja/ru and asserts `isTextTruncated == false`.

## 7. Tasks (created 2026-10-21)

| Task | Scope | Findings | Touches |
|---|---|---|---|
| [**UI1**](../Tasks/TASK-UI1_AnnouncerFullText.md) Announcer | §6.3. Independent; **do first** (the visible bug). | N37, N38, N41 (announcer part) | `PlayerUIPrefab.prefab`, `GameScene.unity` (`GameAnnouncerUI` values), `UI_table_en/ru.asset`, `GameAnnouncerUI.cs`, test |
| [**UI2**](../Tasks/TASK-UI2_CanvasScalingStandard.md) Canvas standard | §6.1 | N39, N42 | 6 scenes (CanvasScaler only, plus the Login background) |
| [**UI3**](../Tasks/TASK-UI3_HudAnchoringAndHand.md) HUD anchoring and hand (R1) | §6.2, plus `HandContainer` 1800 wide, spacing compression for 9–10 cards, and deactivating old views before `Destroy` | N40, N41, N45, N46 | `PlayerUIPrefab.prefab`, `GameScene.unity` overrides, `LeaveMatchUI.cs`, `PlayerUI.cs` |
| [**UI4**](../Tasks/TASK-UI4_PreviewClampAndConfirmLabel.md) Preview and ru label | Clamp the preview and pick the HUD canvas; ru Confirm label minimum size 14 | N43, N44 | `CardPreviewController.cs`, `PlayerUIPrefab.prefab` |

**Constraints for all of them:**
- Keep serialized field names and the in-scene NetworkObject (`GlobalObjectIdHash` overrides).
- Don't commit the dynamic font asset (`AGENTS.md`).
- UI3 changes positions only. No gameplay code.

## 8. Manual check plan (after implementation)

1. **Game view presets.** Add to the Game view: 1280×720, 1366×768, 1920×1080, 2560×1440, **2560×1600**, 3840×2160, 1024×768 (4:3), 1280×1024 (5:4), 3440×1440 (21:9). In each, open every scene:
   - nothing is cut or overlaps
   - element sizes are the same share of the screen height (16:10 and wider), or the whole layout fits (4:3, 5:4)
2. **Player build.** Test a non-native window:
   ```
   FallenStar-Spellcasters.exe -screen-fullscreen 0 -screen-width 1920 -screen-height 1080
   ```
   Repeat with 1024×768. Then test fullscreen 2560×1600.
3. **Announcer.** In en, ja and ru, check:
   - your turn and the opponent's turn
   - your respawn and the opponent's respawn
   - red and blue
   The full sentence must be visible, with no "…" and no warning in the log.
4. **2560×1600 regression.** HUD screenshots before and after UI3 should match, apart from the expected changes in §6.2.
5. **U14.** At 4:3, check that the board and both player areas are fully visible.

## 9. Out of scope

- World-space card text (`CardWorldViewPrefab`, a world-space canvas): it isn't affected by screen resolution.
- Camera framing for other aspect ratios (U14). It becomes a task only if the check shows a cut.
- Redesigning the menus or HUD art. This work only makes the existing layout proportional and fully visible.

## Sources

- **Scenes:** `Assets/Scenes/{Login,MainMenu,Settings,Lobby,Game,Result}Scene.unity`
- **Prefabs:** `Assets/Prefab/UiPrefab/PlayerUIPrefab.prefab`, `Assets/Prefab/CardViewUIPrefab.prefab`
- **Code:**
  - `Assets/Scripts/UI/GameAnnouncerUI.cs`
  - `Assets/Scripts/UI/LeaveMatchUI.cs`
  - `Assets/Scripts/InGame/CardPreviewController.cs`
  - `Assets/Scripts/InGame/PlayerSession.cs`
  - `Assets/Scripts/InGame/GameMaster.cs`
- **Localization:** `Assets/Localization/UI_table_{en,ja,ru}.asset`, `UI_table Shared Data.asset`
- **Font:** `Assets/Fonts/MPLUSRounded1c-Medium SDF.asset` (committed version, for glyph advances)
- **TMP:** `Library/PackageCache/com.unity.ugui@8ccc29d23a79/Runtime/TMP/TMP_Text.cs`, `TextMeshProUGUI.cs`
- **Settings:** `ProjectSettings/ProjectSettings.asset`, `UserSettings/Layouts/*.dwlt`
- **Machine:** registry `HKCU\Software\DefaultCompany\FallenStar-Spellcasters`; WMI `Win32_VideoController`
