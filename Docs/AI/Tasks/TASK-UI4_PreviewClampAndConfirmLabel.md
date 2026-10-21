# TASK-UI4: Keep the card preview on screen; fit the Russian Confirm label

- **Source:** [RESEARCH-UIScaling_Visibility](../Research/RESEARCH-UIScaling_Visibility.md) → N43, N44. Owner decision D7 (2026-10-21): include both.
- **Severity:** 🟢 Low (N43, Plausible), ⚪ Info (N44, Plausible)
- **Baseline:** `391e079` plus the uncommitted working tree, after TASK-UI1–UI3. Line numbers are from the working tree before UI1.
- **Order:** 4 of 4. Small; it touches files that UI3 also touches, so do it after UI3.
- **Implementer:** Codex

## 1. Objective

1. A card preview opened by clicking a world card is always **fully on screen**, even when the click is near an edge or corner. It always opens on the screen-space HUD canvas.
2. The Russian "Подтвердить" fits inside the target-Confirm button.

## 2. Current behavior

- **Preview placement:**
  - `PlayerSession.SelectObject` (`InGame/PlayerSession.cs:425-462`) calls `CardPreviewController.ShowPreview(card, mousePosition)` for a clicked `CardViewWorld`.
  - `ShowPreview` (`InGame/CardPreviewController.cs:31-49`) instantiates `CardViewUIPrefab` under `m_previewRoot` and places its **centre** at the mouse (`:46-48`), with no clamping.
  - The preview draws 200×400 reference units (root 200 wide, `AspectRatioFitter` width-controls-height 0.5). So near an edge, up to half of it is off-screen.
- **Canvas choice:** `CardPreviewController.Start` (`:15-19`) uses `FindFirstObjectByType<Canvas>()`. GameScene also contains world-space canvases (`CardWorldViewPrefab`, one per world card). Today none exists when the session starts, so it works by timing only (latent).
- **Russian label:** the `ConfirmBtn` label TMP (`PlayerUIPrefab.prefab:903`, `&5335355011361859649`) sits in a 100×100 button. It autosizes 18–72 with wrap and Overflow mode (`:953-955`). The ru text "Подтвердить" is about 6.4 em, so even at 18 it's about 115 units wide and spills past the button edges.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/CardPreviewController.cs` | Pick the HUD canvas; clamp the preview position |
| `Assets/Prefab/UiPrefab/PlayerUIPrefab.prefab` | `ConfirmBtn/Text (TMP)` autosize minimum |

Dependencies:
- `CardPreviewController` sits on `PlayerSessionPrefab`; its `m_canvas` and `m_previewRoot` are serialized as empty (`PlayerSessionPrefab.prefab:113-114`).
- `PlayerUI.Instance` is the in-scene HUD, a child of the GameScene screen canvas.

## 4. Required changes

1. **Choose the canvas** in `CardPreviewController.Start`. Keep the serialized fields, and the existing behavior when they're already assigned. Pick in this order:
   1. the root canvas of `PlayerUI.Instance` (`GetComponentInParent<Canvas>().rootCanvas`), if there is one;
   2. otherwise, the first canvas from `FindObjectsByType<Canvas>(FindObjectsSortMode.None)` that is a root canvas and not `RenderMode.WorldSpace`.

   `m_previewRoot` is that canvas's RectTransform. If no canvas is found, log one warning and leave previews disabled; `ShowPreview` returns early.
2. **Clamp in `ShowPreview`.** After computing `_locakPoint`:
   - Force the preview's layout (`LayoutRebuilder.ForceRebuildLayoutImmediate` on the preview, or read the size after the `AspectRatioFitter` has run).
   - Clamp the position so the preview's rect (size × pivot) lies inside `m_previewRoot.rect`.
   - If the preview is larger than the canvas in some dimension, centre it on that axis.
3. **Russian label:** set `m_fontSizeMin` of the `ConfirmBtn` label TMP to **14** in the prefab. Leave the max, wrap and overflow unchanged. At 14, "Подтвердить" is about 90 units wide, which fits the 100-unit button.

## 5. Explicit non-goals

- No change to when a preview opens or closes, which cards can be previewed, or the preview's look.
- No change to the existing debug logs in `CardPreviewController` (N34 covers them).
- No other labels and no button sizes (Send / Confirm-turn already fit).

## 6. Constraints and compatibility requirements

- Keep `CardPreviewController`'s serialized field names and `HasPreview`, and its public methods (`ShowPreview`, `HidePreview`, `TogglePreview`).
- Work in canvas-local units, so the clamp is correct with any CanvasScaler (UI2) and any resolution.
- Don't commit the dynamic font asset (`AGENTS.md`).

## 7. Acceptance criteria

1. Clicking a world card near each of the four screen corners and the four edges shows the whole preview inside the screen, at 2560×1600, 1920×1080 and 1024×768.
2. A click in the middle still shows the preview centred on the mouse, as before.
3. The preview is always a child of the screen-space HUD canvas, even if world-space card canvases exist when the session starts.
4. In ru, "Подтвердить" stays inside the Confirm button. en ("Confirm") and ja ("確認") look the same as before.

## 8. Regression tests

- Harness for the clamp function, with a canvas of 2560×1600, a preview of 200×400 and pivot 0.5. Points: centre → unchanged; (0, 0) → (100, 200) from the bottom-left; (2560, 1600) → (2460, 1400); a preview larger than the canvas → centred.
- Edit Mode check: render the `ConfirmBtn` label in ru and assert that the text's horizontal bounds fit within the button's 100 units.
- Unity compile, exit 0.

## 9. Manual Unity Editor checks

1. Start a match. Click world cards at the left, right, top and bottom of the view: each preview is fully visible.
2. Switch to ru, select a target and look at the Confirm button: the label fits.

## 10. Open questions and assumptions

- Assumption: `PlayerUI.Instance` is set before `CardPreviewController.Start`. The HUD is in the scene, and sessions spawn after the scene loads. The fallback in §4.1 covers the case where it isn't.
