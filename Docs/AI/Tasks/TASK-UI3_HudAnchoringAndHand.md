# TASK-UI3: Anchor the GameScene HUD to the screen edges, and keep the hand centred

- **Source:** [RESEARCH-UIScaling_Visibility](../Research/RESEARCH-UIScaling_Visibility.md) → N40, N41, N45, N46.
- **Owner decisions (2026-10-21):**
  - D3: re-anchor the HUD so it looks identical on 2560×1600.
  - **Remark R1:** "I want to see card in hand by horizontal center, vertical position same. You can move and modify any elements of GUI attached to this."
- **Severity:** 🟡 Medium (N40, Confirmed static). 🟢 Low: N41 (Confirmed), N45 (Confirmed static), N46 (Plausible).
- **Baseline:** `391e079` plus the uncommitted working tree, **after TASK-UI1 and TASK-UI2**. Line numbers are from the working tree before UI1.
- **Order:** 3 of 4
- **Implementer:** Codex

## 1. Objective

1. Every HUD element follows the real screen edge or corner it belongs to: top banner, top-right stats, bottom-right buttons, top-left Leave, bottom-centre hand. On **2560×1600 it looks exactly as today**, apart from the expected changes in §6.
2. **The hand (R1):**
   - It is centred horizontally on the screen for 1–10 cards at any resolution.
   - It keeps today's vertical position: the card centre is 300 reference units above the bottom edge, which is y −500 from the centre on 2560×1600.
   - It never runs under the HUD buttons.
   - It doesn't jump for a frame when it refreshes.
3. No UI element uses a non-uniform scale. The per-frame canvas-size workaround in `LeaveMatchUI` goes away.

## 2. Current behavior

- **100×100 root:** the root of the in-scene `PlayerUIPrefab` (a NetworkObject) is a **100×100 rect at the screen centre**. This comes from the prefab root and the scene override at `GameScene.unity:375` (`m_AnchorMin/Max 0.5`, `m_SizeDelta 100`).
- **Children follow the centre:** every child is placed relative to the root centre, or to the root's top-right corner at (50, 50). The HUD therefore follows the screen **centre**, not the edges:
  - 16:9: the right-side HUD sits 142 units further in than on 16:10.
  - 21:9: 631 units further in.
  - 4:3 / 5:4 with match-height: cut. After TASK-UI2 (Expand), it shrinks instead of being cut, but still doesn't follow the edges.
- **`LeaveMatchUI.Update`** copies the canvas size to its own rect every frame to escape the 100×100 root (`UI/LeaveMatchUI.cs:18-19, 23-26, 32-34`).
- **Non-uniform scales:**
  - `Panel` (banner, sliced sprite): ×(16.223, 2.7914)
  - `ConfirmTurnBtn`: ×(1.594591, 3.0950851, 1.1945)
  - `PhaseAnouncer`: already fixed by UI1
- **Hand layout:**
  - `HandContainer` is 1600×250 at (0, −500), with a `HorizontalLayoutGroup`: spacing 220, Middle Center, control child size on, force-expand off (`PlayerUIPrefab.prefab:1818-1842`).
  - Card roots (`CardViewUIPrefab`) have no `ILayoutElement`, so the layout gives them a width of 0. The centre-to-centre step is therefore 220, and each card draws **200×400** (`FrameImage` 512×1024 × 0.390625).
  - With 10 cards (the hand limit, `GameMaster.cs:80`) the hand spans ±1090. On 16:10 and narrower it runs under `ConfirmBtn` (x 950–1050) and `ConfirmTurnBtn` (x 947–1203). This is **N45**.
- **One-frame jump:** `PlayerUI.UpdateCards` (`UI/PlayerUI.cs:258-305`) calls `ClearHandUI()`, which uses `Destroy` (end of frame), and then immediately creates the new views in the same container (`:322-358`, `:508-527`). For that frame, the layout includes both the old and the new views, so the new hand is drawn shifted to the right. This is **N46** (Plausible; follows from Unity's deferred `Destroy`).

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Prefab/UiPrefab/PlayerUIPrefab.prefab` | Root and child RectTransforms; `Panel`, `ConfirmTurnBtn` scales; `HandContainer` size |
| `Assets/Scenes/GameScene.unity` | PrefabInstance `282301674` root overrides (anchors, size, pivot, position) |
| `Assets/Scripts/UI/LeaveMatchUI.cs` | Remove the canvas-size workaround |
| `Assets/Scripts/UI/PlayerUI.cs` | Hand spacing compression; deactivate old views before `Destroy` |

Dependencies:
- `PlayerUI.m_handContainer` and `m_cardContainer` both point to `HandContainer` (`&826318986333687161`).
- `CardViewUIPrefab` is also the card-preview prefab (`CardPreviewController`). **Don't change the card prefab.**

## 4. Required changes

### 4.1 Root

`PlayerUIPrefab` root RectTransform, in the prefab **and** in the effective GameScene instance: anchors (0, 0)–(1, 1), `m_SizeDelta` (0, 0), `m_AnchoredPosition` (0, 0), pivot (0.5, 0.5). Either update or remove the scene's PrefabInstance overrides for these properties; the effective values in the scene must be the ones above.

### 4.2 Children

Set anchor min = max as listed. The positions are converted so that on a 2560×1600 canvas each element sits exactly where it is today:

| Element | Anchor | Pivot | `m_AnchoredPosition` | Size / scale |
|---|---|---|---|---|
| Panel (banner) | (0.5, 1) | (0.5, 0.5) | (0, −167) | **1622.3 × 279.14**, scale **1** |
| PhaseAnouncer | (0.5, 1) | (0.5, 0.5) | (0, −119) | 1560 × 150 (from UI1), scale 1 |
| HPTextBar | (1, 1) | unchanged (0.5, 0.5) | (−140, −100) | unchanged |
| LifeTextBar | (1, 1) | unchanged | (−140, −150) | unchanged |
| ManaTextBar | (1, 1) | unchanged (1, 0.5) | (−40, −200) | unchanged |
| OpponentManaTextBar | (1, 1) | unchanged (1, 0.5) | (−40, −250) | unchanged |
| ManaMessageText | (0.5, 1) | unchanged | (0, −400) | unchanged |
| **HandContainer** | **(0.5, 0)** | unchanged (0.5, 0.5) | **(0, 300)** | **1800 × 250** (was 1600) |
| ConfirmBtn | (1, 0) | unchanged | (−280, 380) | unchanged |
| SendBtn | (1, 0) | unchanged | (−130, 380) | unchanged |
| ConfirmTurnBtn | (1, 0) | unchanged | (−205, 249) | **255.13 × 92.85**, scale **(1, 1, 1)** |
| LeaveMatch | stretch (0, 0)–(1, 1) | unchanged | (0, 0) | `m_SizeDelta` (0, 0) |

LeaveMatch's children (LeaveBtn top-left, the stretched confirm panel, the dialog) are unchanged.

### 4.3 `LeaveMatchUI`

Remove `m_layoutRect`, `m_canvasRect`, their assignment in `Start` and the resize block in `Update` (`:32-34`). Keep everything else (`HideConfirmPanel`, the button state, the leave timeout) unchanged.

### 4.4 Hand centring and spacing (R1, N45)

In `PlayerUI`:
1. Add `[SerializeField] private float m_handCardWidth = 200f;` (the drawn card width in container units). Cache the `HorizontalLayoutGroup` of `m_handContainer`, and its authored `spacing` (220) as the maximum spacing, in `Awake`.
2. Add a private method that sets the layout spacing for the current number of views, `n = m_cardViews.Count`:
   - Let `childW` be the layout width of a hand card: `LayoutUtility.GetPreferredWidth` of a view's RectTransform (0 today). Then `step = maxSpacing + childW`.
   - If `n ≥ 2` and `(n − 1) · step + m_handCardWidth > m_handContainer.rect.width`, set `step = (m_handContainer.rect.width − m_handCardWidth) / (n − 1)`.
   - Set `spacing = step − childW`, which is negative when the cards overlap.
   - Call it at the end of `UpdateCards` (after the views are created) and after `ClearHandUI` in `ClearLocalUI`.
   - With a 1800-wide container: up to 8 cards keep spacing 220; 9 cards use 200 (touching); 10 cards use about 177.8 (22 units of overlap). The hand stays within ±900, clear of the button column at x ≥ 947 on 16:10 and narrower.
3. In `ClearHandUI`, call `SetActive(false)` on each old view (both loops) **before** `Destroy`. The layout then ignores them in the same frame (N46).

## 5. Explicit non-goals

- No change to the card prefab, card visuals, selection colors, click handling, hand limit or draw logic.
- No change to text content, fonts or the announcer (UI1), and no CanvasScaler change (UI2).
- No change to world-space UI or the camera (U14 is checked only).
- Moving other HUD elements beyond the table isn't needed. R1 allows it; if a real overlap remains, report it with a screenshot and a proposed position rather than redesigning.

## 6. Constraints and compatibility requirements

- `PlayerUIPrefab` is an in-scene NetworkObject. Keep its `GlobalObjectIdHash` / `InScenePlacedSourceGlobalObjectIdHash` overrides, and keep all serialized references (`m_handContainer`, `m_cardContainer`, `m_hpText`, …).
- **Expected visual changes on 2560×1600** (everything else is identical):
  - The banner's rounded corners become normal-sized (they were stretched 16×).
  - The Confirm-turn label is no longer stretched: shorter and wider glyphs. It autosizes in its new 255×93 rect.
  - 9–10 cards in hand sit closer together, or overlap. The rightmost card is drawn on top, so clicks on an overlap go to it.
- The new serialized field gets its default (200) on the prefab. Don't rename existing fields.
- Don't commit the dynamic font asset (`AGENTS.md`).

## 7. Acceptance criteria

1. **Identical at 2560×1600:** every element in the table appears where it does today, within 1 unit. Compare before/after screenshots, or compute world corners.
2. **Edge-following on 2560×1440 (16:9) and 3440×1440 (21:9):**
   - HP, Lives and mana keep their distance from the top-right corner.
   - The buttons keep their distance from the bottom-right corner.
   - Leave stays top-left.
   - The banner, announcer and hand stay centred.
3. **The hand, at every resolution in the UI2 list, for n = 1, 5, 8, 9 and 10:**
   - Its horizontal centre is the screen centre (±1 unit).
   - The card centre is 300 reference units above the bottom edge: 18.75 % of the screen height on 16:10 and wider.
   - No hand card overlaps `ConfirmBtn`, `SendBtn` or `ConfirmTurnBtn`.
4. **No one-frame jump:** in the frame where `UpdateCards` rebuilds the hand, the layout contains only the new views.
5. **Leave works as before:** the confirm panel covers the whole screen at every resolution, and `LeaveMatchUI` has no per-frame resize.
6. No RectTransform under `PlayerUIPrefab` has a non-uniform scale.

## 8. Regression tests

- A YAML check that the root, the 12 rows in §4.2 and the scene overrides have the listed values.
- Harness: the spacing function for n = 0–10, container 1800, card 200, max spacing 220, childW 0. The expected spacing is 220 for n ≤ 8, 200 for n = 9 and 177.78 for n = 10. Also check childW = 200 (so `step` uses the preferred width).
- Play Mode check (temporary, `Temp/UI3Verification/`):
  - Fill the hand container with 10 `CardViewUIPrefab` instances through the same path.
  - Force the layout to rebuild, then assert the centre and the button clearance (AC3).
  - Rebuild once more and assert that the old views are inactive in the same frame (AC4).
- Unity compile, exit 0.

## 9. Manual Unity Editor checks

1. **2560×1600:** take a HUD screenshot before and after the change. Only the expected changes in §6 differ.
2. **2560×1440, 3440×1440, 1024×768:** the HUD follows the corners, the hand is centred, nothing is cut. Check U14 at the same time: is the board fully visible at 4:3?
3. **Full hand:** skip playing cards for about three rounds until the hand holds 10 cards. All 10 are visible and clickable (left click selects to play, right click to discard). Confirm-turn and Confirm/Send aren't covered. Then play cards: the hand recentres without a visible jump.
4. **Leave:** Leave → confirm dialog → Cancel, then Leave → Confirm. Same as before.
5. **Two players (ParrelSync):** the client's HUD looks the same as the host's.

## 10. Open questions and assumptions

- **Assumption:** the layout gives card roots a width of 0 (no `ILayoutElement` on `CardViewUIPrefab`'s root). The spacing formula reads the preferred width, so it stays correct either way. Report the measured value.
- The 1800 width keeps the hand clear of the button column on the narrowest canvas (2560 wide). If the owner later raises the hand limit, the compression keeps the hand inside the container automatically.
