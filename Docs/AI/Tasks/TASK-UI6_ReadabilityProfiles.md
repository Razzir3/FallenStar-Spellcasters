# TASK-UI6: Readable HUD on every screen size — low and medium/high profiles, a large card view, bigger button labels, windowed sizes above the taskbar

- **Source:** [Issue Report #10](../Reports/2026-10-21_IssueReport-10.md): N47 (card text), N48 (HUD labels), N49 (windowed size).
- **Owner decisions (2026-10-21):**

  | # | Decision |
  |---|---|
  | V1 | "Make separated settings for low resolutions (like 640x480 up to 1024x768) and medium with high (1366 and better)." |
  | V2 | The recommended large card view. Record the alternatives as possible issues: they are **U15** and **U16** (§10). |
  | V3 | As recommended: fix the labels and widen Confirm and Send. |
  | N49 | Fix it in this task. |

- **Severity:** 🟡 Medium: N47, N48 (Confirmed). 🟢 Low: N49 (Plausible).
- **Baseline:** `72a724c`. Line numbers are from that commit.
- **Implementer:** Codex
- **Follow-up:** UI7 will apply the low profile to the five menu scenes and let Settings offer sizes down to 640×480. It reuses this task's profile component, so I'll write it after this task's review.

## 1. Objective

1. **Two UI profiles,** chosen automatically from the screen width:

   | Profile | Screen width | Examples |
   |---|---|---|
   | **Low** | below 1366 | 640×480 … 1360×768 |
   | **Medium/high** | 1366 and up | 1366×768 and larger |

   In the GameScene HUD, the low profile uses a smaller canvas reference and a compact layout. Medium/high is today's layout plus the changes in §4.2 and §4.3.
2. **Every HUD text is at least 30 canvas units** after autosize, in both profiles:

   | Profile | Screen | Smallest text |
   |---|---|---|
   | Low | 640×480 | 12 px |
   | Low | 1024×768 | 19 px |
   | Medium/high | 1366×768 | 14 px |
   | Medium/high | 1920×1080 | 20 px |

3. **A large card view, docked at the left side.** It shows when the player clicks a board card (as today) and when the pointer hovers over a hand card.
4. **Confirm, Send and Confirm turn sit in one column,** wide enough for the Russian labels at 30 units or more. The Leave labels are at least 30 units too.
5. **Windowed sizes fit above the taskbar.** The automatic windowed size prefers the display's aspect ratio.

## 2. Current behavior

**The canvas**
- `GameScene`'s canvas is Scale With Screen Size, reference 2560×1600, Expand (`GameScene.unity:795-800`).
- The scale factor is `min(w / 2560, h / 1600)`: 0.25 at 640×480, 0.40 at 1024×768, 0.48 at 1366×768, 0.675 at 1920×1080.
- The canvas has a single child: the HUD, PrefabInstance `282301674` of `PlayerUIPrefab`.

**The HUD layout** (from UI3; the prefab values are the effective scene values):

| Element | Anchor | Pos | Size |
|---|---|---|---|
| Panel (banner) | (0.5, 1) | (0, −167) | 1622.3 × 279.14 |
| PhaseAnouncer | (0.5, 1) | (0, −119) | 1560 × 150 |
| HPTextBar / LifeTextBar | (1, 1) | (−140, −100) / (−140, −150) | 200 × 50 |
| ManaTextBar / OpponentManaTextBar (pivot (1, 0.5)) | (1, 1) | (−40, −200) / (−40, −250) | 650 × 50 |
| ManaMessageText | (0.5, 1) | (0, −400) | 1000 × 50 |
| HandContainer | (0.5, 0) | (0, 300) | 1800 × 250 |
| ConfirmBtn / SendBtn | (1, 0) | (−280, 380) / (−130, 380) | 100 × 100 |
| ConfirmTurnBtn | (1, 0) | (−205, 249) | 255.13 × 92.85 |
| LeaveMatch → LeaveBtn | (0, 1) | (174, −54) | 300 × 64 |
| LeaveMatch → LeaveConfirmPanel → Dialog | (0.5, 0.5) | (0, 0) | 980 × 390 |

**Button images**
- ConfirmBtn and SendBtn use a square plank sprite (`efee20b64d1e73942a29ba61ce3379c3`).
- ConfirmTurnBtn uses a wide plank sprite (`2be7b70e984b51444aca9c12ae4723a7`).
- All three use the Simple image type.

**Autosize ranges** (min–max, in canvas units)

| Text | Range | Where it's set |
|---|---|---|
| ConfirmBtn label | 14–72 | prefab |
| SendBtn label | 18–72 | prefab |
| ConfirmTurnBtn label | 18–72 | GameScene override (target `793786715222444024`: `m_enableAutoSizing` 1, `m_fontSize` 21.5); the prefab has autosize off at 24 |
| LeaveBtn and both dialog-button labels | 22–30 | prefab |
| HP / Life | 18–72 | prefab |
| Mana texts, ManaMessageText | 18–36 | prefab |
| PhaseAnouncer | 24–48 | GameScene override (UI1) |
| ConfirmationText | 34–42 | prefab |

**The card preview**
- `CardPreviewController` sits on `PlayerSessionPrefab`, on the same GameObject as `PlayerSession`.
- It instantiates `CardViewUIPrefab` unscaled (200×400 units) under the HUD canvas, at the mouse, clamped on screen (UI4; `InGame/CardPreviewController.cs:55-82`).
- **When it shows and hides:**
  - Only `PlayerSession.SelectObject` shows it, on a world-card click (`InGame/PlayerSession.cs:438-458`).
  - Every world click hides it.
  - UI clicks never reach `SelectObject` (`:739-743`).
- Two ungated "Tried ShowPrew" logs sit at `:58` and `:63` (N34).

**The hand**
- `CardViewUI` implements only `IPointerClickHandler`, with the `Clicked` / `AlterClicked` events (`InGame/CardViewUI.cs:32-33`, `:84-104`).
- `PlayerUI.CreateCardView` subscribes to them (`UI/PlayerUI.cs:355-391`). `ClearHandUI` unsubscribes and destroys the views (`:541-566`).
- `UpdateHandSpacing` (`:318-338`) uses `m_handContainer.rect.width`. It runs only at the end of `UpdateCards` and in `ClearLocalUI`.

**Text sizes today:** see [report #10 §3.1](../Reports/2026-10-21_IssueReport-10.md). Card names are 4–6 px and the ru Confirm label 6 px at 1024×768.

**Windowed sizes in `DisplaySettingsUI`**
- Windowed sizes must be strictly smaller than the display (`UI/DisplaySettingsUI.cs:135-139`).
- The fit check for the current size is at `:171`. The pick is the largest by width (`:173-182`).
- On the owner's display (2560×1600, work area 2560×1552) Windowed picks 2048×1536. With its title bar, that window is about 1567 px tall, taller than the work area.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/UI/UiResolutionProfile.cs` (**new**) | The threshold and `IsLow` |
| `Assets/Scripts/UI/ResolutionProfileLayout.cs` (**new**) | Applies the low profile and restores the authored one |
| `Assets/Prefab/UiPrefab/PlayerUIPrefab.prefab` | Button column, label ranges, `CardViewDock`, the `ResolutionProfileLayout` component with its low values |
| `Assets/Scenes/GameScene.unity` | Only PrefabInstance overrides: remove or change the ones that would hide this task's prefab values (§4.2) |
| `Assets/Scripts/UI/PlayerUI.cs` | `m_cardViewDock` and its getter; hover handlers; re-spacing when the container width changes |
| `Assets/Scripts/InGame/CardViewUI.cs` | Pointer enter / exit events |
| `Assets/Scripts/InGame/CardPreviewController.cs` | Docked, scaled view; `ShownCard`; remove the two logs |
| `Assets/Scripts/UI/DisplaySettingsUI.cs` | Windowed bounds from the work area; a pick that prefers the display's aspect |

**Dependencies:**
- `PlayerUI.m_handContainer` and `m_cardContainer` both point to `HandContainer` (`&826318986333687161`).
- `PlayerSessionPrefab`'s `PlayerSession.m_cardPreviewController` is the `CardPreviewController` on its own GameObject (`PlayerSessionPrefab.prefab:100`, `:101-115`). The local player's session is the one that handles input.
- `CardViewUIPrefab` is both the hand card and the preview. **Don't change the card prefab:** scale the preview instance instead.

## 4. Required changes

### 4.1 Profiles

**`UiResolutionProfile`** (new static class):

```csharp
public static class UiResolutionProfile
{
    public const int MediumMinWidth = 1366;
    public static bool IsLow(int screenWidth) => screenWidth < MediumMinWidth;
}
```

**`ResolutionProfileLayout`** (new MonoBehaviour). It's generic, and UI7 will reuse it in the menu scenes.
- **Serialized fields:**
  - `m_canvasScaler` (`CanvasScaler`). If it's null, use `GetComponentInParent<CanvasScaler>()`.
  - `m_lowReferenceResolution` (`Vector2`). Zero means "leave the scaler alone".
  - `m_lowRects`, an array of a `[Serializable]` class `LowRect`, with these fields: `RectTransform m_target`, `Vector2 m_anchorMin`, `m_anchorMax`, `m_pivot`, `m_anchoredPosition`, `m_sizeDelta`, and `float m_scale = 1`.
- **`Awake`:**
  1. Capture each target's authored `anchorMin`, `anchorMax`, `pivot`, `anchoredPosition`, `sizeDelta` and `localScale`, and the scaler's `referenceResolution`. These are the medium/high values.
  2. Apply the profile for the current `Screen.width`, so that no frame is drawn with the wrong layout.
- **`Update`:** if `UiResolutionProfile.IsLow(Screen.width)` differs from the applied profile, apply the new one.
  - Low: set the low values, with `localScale` = (s, s, 1), and the low reference.
  - Medium/high: restore the captured values exactly.
- A missing target is skipped, with **one** warning in `Awake`, never per frame.
- No debug flag. If one is added, its `Awake` must call `DebugModeGuard.Allow` first (L9).

**`PlayerUI`:** at the top of `Update`, before the early returns, check whether `m_handContainer.rect.width` differs from the width that `UpdateHandSpacing` last used. If it does, call `UpdateHandSpacing()`. `UpdateHandSpacing` stores the width it used.

### 4.2 The button column and the label ranges (V3, N48): both profiles

**Medium/high**, the authored values in `PlayerUIPrefab`:

| Element | Anchor | Pos | Size | Change |
|---|---|---|---|---|
| ConfirmBtn | (1, 0) | (−190, 475) | 300 × 92.85 | Was (−280, 380), 100×100. Its Image takes ConfirmTurnBtn's sprite (`2be7b70e…`). |
| SendBtn | (1, 0) | (−190, 362) | 300 × 92.85 | Was (−130, 380), 100×100. Same sprite. |
| ConfirmTurnBtn | (1, 0) | (−190, 249) | 300 × 92.85 | Was (−205, 249), 255.13 wide |

- **The column** is x −340 … −40 from the right edge. The buttons are stacked with gaps of 20 units, Confirm on top.
- **Clearance:** on a 2560-wide canvas, the hand (±900 around the centre) ends 40 units left of the column.
- **Why 300 wide:** ru "Подтвердить ход" (15 characters) needs about 270 units at size 30.

**Label ranges** (autosize on, both profiles):

| Label | New min–max | Was |
|---|---|---|
| ConfirmBtn, SendBtn, ConfirmTurnBtn labels | 30–44 | 14–72, 18–72, 18–72 (scene) |
| LeaveBtn, LeaveConfirmBtn, LeaveCancelBtn labels | 30–40 | 22–30 |
| HPTextBar, LifeTextBar | 30–72 | 18–72 |
| ManaTextBar, OpponentManaTextBar, ManaMessageText | 30–36 | 18–36 |
| PhaseAnouncer (GameScene override) | 30–48 | 24–48 |
| ConfirmationText | unchanged, 34–42 | — |

**GameScene overrides**
- **ConfirmTurnBtn label:** set autosize 30–44 in the prefab, then remove the scene's `m_enableAutoSizing` and `m_fontSize` overrides on that label, so that the prefab applies.
- **PhaseAnouncer:** change its `m_fontSizeMin` override from 24 to 30.
- **General rule:** if any other GameScene override shadows a value this task changes, remove it. The effective scene values must be the ones in this spec.

### 4.3 The large card view (V2, N47)

1. **`CardViewDock`:** a new child of `PlayerUIPrefab`'s root.
   - It's an empty RectTransform with a `CanvasGroup` (alpha 1, interactable off, **blocks raycasts off**).
   - Place it just **before `LeaveMatch`** in the sibling order, so that the Leave confirm panel stays on top.
   - Medium/high values: anchors (0, 0)–(0, 1), pivot (0, 0.5), `m_AnchoredPosition` (40, 207), `m_SizeDelta` (700, −626).
   - That's left 40, width 700, bottom 520, top margin 106. On 2560×1600 the dock spans y 520–1494: above the hand (top 500) and below the Leave button (bottom 1514).
2. **`PlayerUI`:** add `[SerializeField] private RectTransform m_cardViewDock;`, wired to the dock, and `public RectTransform CardViewDock => m_cardViewDock;`.
3. **`CardPreviewController`:**
   - **In `Start`:** where it already looks up `PlayerUI.Instance` for the canvas, also take `m_dock = PlayerUI.Instance.CardViewDock`. A null dock keeps today's behavior.
   - **`ShowPreview(NetworkCard, Vector2)` keeps its signature:**
     - If the same card is already shown, return without rebuilding.
     - **With a dock:**
       1. Instantiate the view under the dock.
       2. Set anchors = pivot = (0, 0.5) and `anchoredPosition` (0, 0).
       3. Rebuild its layout.
       4. Set a uniform scale `s = min(dock.rect.height / view.rect.height, dock.rect.width / view.rect.width)`.

       The screen position is ignored.
     - **Without a dock:** UI4's path, unchanged (at the mouse, clamped, scale 1).
   - **New `public NetworkCard ShownCard`:** the card being shown, or null.
   - **Refit:** in `LateUpdate`, if a view is shown in the dock and the dock's rect size has changed since the last fit, recompute `s`. A profile switch changes the dock.
   - **Remove** the two "Tried ShowPrew" logs (`:58`, `:63`; part of N34). Add no new logs.
4. **Hover on hand cards:**
   - **`CardViewUI`:** also implement `IPointerEnterHandler` and `IPointerExitHandler`. Raise `public event Action<CardViewUI> PointerEntered` and `PointerExited`, cleared in `OnDestroy` like `Clicked`. No logs.
   - **`PlayerUI.CreateCardView`:** subscribe to both (remove first, then add, as for `Clicked`). `ClearHandUI` unsubscribes them.
   - **The controller:** `PlayerUI` uses the **local** session's controller, `m_playerSession.GetComponent<CardPreviewController>()`, cached.
   - **Enter:** `ShowPreview(view.GetNetworkCard(), …)`. Remember it as `m_hoveredCard`.
   - **Exit:** if `ShownCard` is that view's card, call `HidePreview()`. Clear `m_hoveredCard`.
   - **After a hand rebuild** (end of `UpdateCards`): if `m_hoveredCard` is no longer in the hand, hide its view if it's shown, and clear `m_hoveredCard`.
   - **`ClearLocalUI`:** hides the preview.
   - **Clicks:** a play or discard click on a hand card keeps the hover view.
   - **Board cards:** keep click-to-show. `PlayerSession` is unchanged.

### 4.4 The low-profile compact HUD (V1)

`PlayerUIPrefab`'s root gets a `ResolutionProfileLayout` with `m_lowReferenceResolution` = **(1600, 1000)**, which is 2560×1600 / 1.6, and these `m_lowRects`:

| Target | Anchor (min = max) | Pivot | Pos | Size | Scale |
|---|---|---|---|---|---|
| Panel | (0.5, 1) | (0.5, 0.5) | (0, −90) | 1080 × 160 | 1 |
| PhaseAnouncer | (0.5, 1) | (0.5, 0.5) | (0, −90) | 1040 × 140 | 1 |
| HPTextBar | (1, 1) | (0.5, 0.5) | (−140, −50) | 200 × 50 | 1 |
| LifeTextBar | (1, 1) | (0.5, 0.5) | (−140, −100) | 200 × 50 | 1 |
| ManaTextBar | (1, 1) | (1, 0.5) | (−40, −210) | 600 × 50 | 1 |
| OpponentManaTextBar | (1, 1) | (1, 0.5) | (−40, −260) | 600 × 50 | 1 |
| ManaMessageText | (0.5, 1) | (0.5, 0.5) | (0, −330) | 1000 × 50 | 1 |
| HandContainer | (0.5, 0) | (0.5, 0.5) | (0, 175) | 1408 × 250 | **0.625** |
| ConfirmBtn | (1, 0) | (0.5, 0.5) | (−190, 302) | 300 × 92.85 | 1 |
| SendBtn | (1, 0) | (0.5, 0.5) | (−190, 189) | 300 × 92.85 | 1 |
| ConfirmTurnBtn | (1, 0) | (0.5, 0.5) | (−190, 76) | 300 × 92.85 | 1 |
| LeaveBtn | **(0, 0)** | (0.5, 0.5) | (174, 56) | 300 × 64 | 1 |
| CardViewDock | (0, 0)–(0, 1) | (0, 0.5) | (24, −35) | 456 × −290 | 1 |

**Result on the narrowest low canvas, 1600×1000** (16:10). 4:3 and 5:4 give a taller canvas (1600×1200, 1600×1280); 16:9 gives a wider one (1778×1000).

| Area | What's there | Position (units, from the bottom-left corner) |
|---|---|---|
| Top | Banner | x 260–1340, y 830–990 |
| | HP / Life | x 1360–1560 |
| | Mana bars | x 960–1560, y 715–815 |
| | Mana message | y 645–695 |
| Bottom | Leave | x 24–324, y 24–88 |
| | 10 hand cards | x 360–1240, y 50–300 |
| | Button column | x 1260–1560, y 30–349 |
| Left | Card dock | x 24–480, y 110–820 |

- **Why Leave moves to the bottom-left:** the banner gets the top width it needs for 30-unit text.
- **Why the hand scales to 0.625:** the hand keeps the same share of the screen height as in medium/high (25 %). The card view is what makes cards readable.
- **The hand math:** spacing uses the container's local width (1408), so 10 cards step 134 local units, which is 84 on screen. 67 % of each card stays visible.
- **Accepted overlaps** while the card view is shown, because it's transient: it covers the top of the leftmost hand cards (x 360–480) and the left end of the mana message.

Codex may move any low value by up to 40 units, or shrink a width by up to 10 %, to remove a real overlap or cut found in the render. Report the final table.

### 4.5 Windowed sizes (N49)

- **`GetWindowedBounds()`:**
  - Read `Screen.mainWindowDisplayInfo.workArea`.
  - If its width or height is 0 or less, fall back to the display size minus (1, 1). That is today's strict rule.
  - Otherwise return `(workArea.width − k_frameAllowance, workArea.height − k_titleBarAllowance)`, with `private const int k_frameAllowance = 16;` and `k_titleBarAllowance = 64;`. These cover the window frame and the title bar up to 200 % display scaling.
- **The windowed fit:** `<=` the bounds. Fullscreen is unchanged (`<=` the display).
- **`BuildResolutionList`** keeps its signature. When windowed, callers pass the windowed bounds as its `display` argument. Document this in a comment.
- **New pure method:** `public static Vector2Int PickWindowedSize(IReadOnlyList<Vector2Int> sortedList, Vector2Int bounds, Vector2Int display, Vector2Int min, Vector2Int fallback)`. It returns:
  1. the first entry that fits `bounds` and `min` and whose aspect ratio is within 1 % of `display.x / display.y`;
  2. otherwise, the first entry that fits `bounds` and `min`;
  3. otherwise, `fallback`.
- **`OnDisplayModeChanged` → Windowed:** keep the current size if it fits the bounds. Otherwise use `PickWindowedSize(…, fallback: current)`.
- **Expected on the owner's display:** bounds 2544×1488, so the automatic pick is **1920×1200**.
- `m_minWidth` / `m_minHeight` stay 1024 / 720. Lowering them is UI7.

## 5. Explicit non-goals

- **No change to the five menu scenes** (UI7): not their CanvasScalers, not their layouts, and not the Settings minimum of 1024×720.
- **No change to `CardViewUIPrefab`:** not the card art, the text boxes or the name plate. Only the preview instance is scaled.
- **No change to the world side:** the board, the camera, world-space card views, and `PlayerSession`'s world-click logic.
- **No user-facing "UI scale" option:** the profile is automatic.
- **Leave alone:** the other N34 logs, N33, N35, the announcer logic (UI1), the hand limit, and the play and discard clicks.

## 6. Constraints and compatibility requirements

- **`PlayerUIPrefab` is an in-scene NetworkObject.**
  - Keep the `GlobalObjectIdHash` (3051432279) and `InScenePlacedSourceGlobalObjectIdHash` (151063585) overrides, and every serialized reference.
  - `ResolutionProfileLayout` is a plain MonoBehaviour. No NetworkBehaviour is added, removed or reordered.
- **Client-only UI.** No network messages. Host and client each apply the layout locally.
- **Medium/high at 2560×1600 stays identical to today,** within 1 unit, except for:
  - the button column (positions, sizes, sprite)
  - the label floors
  - the large card view
- **New public API, and nothing else:**
  - `UiResolutionProfile`, `ResolutionProfileLayout`
  - `PlayerUI.CardViewDock`
  - `CardPreviewController.ShownCard`
  - `CardViewUI.PointerEntered` / `PointerExited`
  - `DisplaySettingsUI.PickWindowedSize`

  Rename nothing.
- **Logging:** no new ungated logs (N34). Setup problems log one warning, once.
- **Unity 6 APIs only,** with no obsolete members and no compiler warnings of our own.
- **Before you start:** keep a baseline copy of every file you'll change in `build/ui6-verification/baseline/`.
- **Before handing over:** restore the dynamic font asset from that baseline (`AGENTS.md`).

## 7. Acceptance criteria

**Resolution sets:**
- **Low:** 640×480, 800×600, 1024×768, 1152×864, 1280×720, 1280×800, 1280×1024, 1360×768
- **Medium/high:** 1366×768, 1600×900, 1920×1080, 1920×1200, 2560×1440, 2560×1600, 3440×1440, 3840×2160

**Criteria:**
1. **Profile:**
   - The low profile applies exactly when the width is below 1366: 1360×768 is low, 1366×768 is medium.
   - Switching low → medium restores the authored values within 0.01 units, and the 2560×1600 reference.
   - A runtime switch (a Game view size change, or Alt+Enter in a player) needs no restart and logs nothing.
2. **The text floor:**
   - Every HUD TMP text from §4.2's list is at least 30 canvas units after autosize, in en, ru and ja, at every size of both sets. That's ≥ 12 px at 640×480, ≥ 14 px at 1366×768 and ≥ 20 px at 1920×1080.
   - Measure with realistic content: HP 100, Life 3, mana "20 / 20", opponent mana "20 / 20", "Not enough mana", UI1's longest announcer lines, and all the button labels.
   - No text overflows its rect.
3. **The layout,** at every size of both sets, with a 10-card hand:
   - Every element of §4.4's table, and the Leave dialog when it's open, is inside the canvas.
   - No two of them overlap. Two exceptions: Panel with PhaseAnouncer, and the card view.
   - The hand keeps at least 10 units of distance from the button column and from LeaveBtn.
   - The hand stays centred, within ±1 unit.
4. **The card view:**
   - Hovering a hand card or clicking a board card shows it inside `CardViewDock`, left-aligned, vertically centred and fully on screen.
   - It blocks no raycasts. A world click through it reaches the board, and hovering a hand card under it doesn't flicker.
   - **Description text:** at least 18 px at 1366×768 and at least 14 px at 640×480, for all 4 cards in all 3 languages. Here px = font size × the view's scale × the canvas factor.
   - **Card names:** measure and report them. The expected range is about 13–16 px at 1366×768 and 10–13 px at 640×480. If one misses 12 px (medium) or 10 px (low), name it and say by how much. The card art limits names (§10, A3).
5. **The hover rules:**
   - Entering a card shows its view. Leaving hides it only if the view still shows that card.
   - Moving across cards switches the view without flicker.
   - Play and discard clicks keep the view.
   - A card that leaves the hand takes its hover view with it, and the end of the match hides the view.
6. **Medium/high at 2560×1600:** as stated in §6.
7. **N49:**
   - The windowed list fits within the bounds (work area − 16 / − 64).
   - With a 2560×1600 display and a 2560×1552 work area, the automatic windowed size is 1920×1200.
   - The fullscreen list and the current-size exception are unchanged.
8. **Build:** the Unity compile has 0 errors and no warnings of our own, and the release Windows player builds.

## 8. Regression tests

**Pure harness**
- `IsLow`: 639, 640 and 1365 → true; 1366 and 3840 → false.
- **`BuildResolutionList`:**
  - **Input:** UI5's list: 3840×2400, 2560×1600 (×3 refresh rates), 2560×1440, 1920×1200, 1920×1080, 1366×768, 1280×720, 1024×768, 800×600. Minimum 1024×720.
  - **Windowed,** with bounds 2544×1488: 1920×1200, 1920×1080, 1366×768, 1280×720, 1024×768.
  - **Fullscreen** on a 2560×1600 display: unchanged from UI5.
- **`PickWindowedSize`:**
  - display 2560×1600, bounds 2544×1488 → 1920×1200
  - display 1920×1080, bounds 1904×968, with 1600×900 in the input → 1600×900
  - no entry with a matching aspect → the largest entry that fits
  - nothing fits → `fallback`
- **A zero work area** → the strict display rule.

**Edit / Play Mode harness** (temporary, in `Temp/UI6Verification/`; remove it afterwards)
- Load the GameScene HUD.
- For both resolution sets × 3 locales × hand sizes 0, 5 and 10: apply the profile, force the layout, and measure AC2–AC4.
- Toggle the profile at runtime (AC1).
- Simulate the hover with `ExecuteEvents` pointer enter / exit on the hand views (AC5).

**Other checks**
- **YAML:** the prefab's new and changed values (§4.2–4.4), the GameScene overrides that were removed or changed, and the two network hashes.
- **Build:** the release player build.

## 9. Manual Unity Editor checks

Use two Editors with ParrelSync and the Game view presets. N49 needs a player build.

1. **2560×1600:** the HUD looks as before, except for the bottom-right column (Confirm, Send and Confirm turn stacked, in the same wooden style) and slightly larger labels.
2. **Card Selection:**
   - Hover over each hand card: the big card appears on the left.
   - Move across the cards: it switches. Move off them: it hides.
   - Left and right clicks still play and discard, and the big card stays while you click.
3. **Player action:**
   - Click a board card, your own and the opponent's: the big card appears on the left.
   - Click its target, even where the big card is drawn: targeting works.
   - The next world click hides it.
4. **Low sizes** (640×480, 800×600, 1024×768, 1280×720, 1280×1024, 1360×768): you see the compact layout.
   - The banner is top-centre, the stats top-right, Leave bottom-left, the hand bottom-centre with smaller cards, and the buttons bottom-right.
   - All text is readable in en, ru and ja.
   - Leave → the dialog fits.
5. **The switch:** during a match, go from 1360×768 to 1366×768 and back. The layout switches at once, the hand re-spaces, and the Console shows no error.
6. **Medium sizes** (1366×768, 1920×1080, 3440×1440): everything is readable and nothing overlaps.
7. **Two players:** the client's HUD matches the host's.
8. **A player build on the owner's display:**
   - Settings → Windowed gives 1920 × 1200, with the whole window (title bar and bottom edge) above the taskbar.
   - Fullscreen gives the native size.
9. **The possible issues U15 and U16:** does the big card ever hide something you need? Does the hover feel distracting? Write it in the checklist notes.

## 10. Open questions and assumptions

- **A1, the threshold is the screen width (1366).** The sizes between 1024×768 and 1366 wide also count as low: 1152×864, 1280×720, 1280×800, 1280×1024 and 1360×768. To change that, change `MediumMinWidth`.
- **A2, the compact layout is calculated, not yet seen.** Codex may adjust it within §4.4's limits. Codex must send ru screenshots at 640×480, 1024×768, 1280×720, 1366×768 and 2560×1600.
- **A3, the card art limits card names.** The name plate is about 3 % of the card's height, so names stay about 10–13 px at 640×480 even in the big view. Making them bigger needs new card art, which is out of scope.
- **A4, Confirm and Send take Confirm turn's wide plank sprite,** because the square one would stretch.
- **A5, how to reach 640×480 in this task:** only through the Game view or the command line (`-screen-width 640 -screen-height 480`). Settings keeps its 1024×720 minimum until UI7.
- **A6, the window allowances (16 px frame, 64 px title bar) are conservative.** At 100 % display scaling, the windowed maximum gives up about 30 px of height it could use.
- **Possible issues: the owner's V2 alternatives, tracked as unknowns in report #10:**

  | ID | Possible issue | Fallback |
  |---|---|---|
  | **U15** | The docked view covers something the player needs: the left side of the board, the left end of the banner on 16:10, or the leftmost hand cards in the low profile. | **V2b:** the large view at the mouse, with UI4's clamp |
  | **U16** | The hover view on hand cards gets in the way during Card Selection: flicker, distraction, or covering. | **V2c:** board cards only. Or a short hover delay (about 0.3 s). |
