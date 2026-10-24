# TASK-F9_F14a_N62: Big-card fade, dark boxes behind the HUD texts, the right hint for Send on the opponent's turn

- **Source:** [Issue Report #15](../Reports/2026-10-24_IssueReport-15.md) §2 (N62), §4 (poll Q6 = a, Q8 = a).
- **Owner decisions (poll, 2026-10-24):**

  | # | Decision |
  |---|---|
  | Q6 | **a.** "A small task for the backing boxes now; the full redraw stays Later." Your earlier note: "RN Hp/Mana/Etc are hard to see with lava on the back." |
  | Q8 | **a.** F9 first. Comment: "Simple animation will be fine. Like, at the start - changing alfa from 0 to max when show up by about 0.7 second, and disapere by changing alpha from max to zero by 0.6 sec. Animation time must have variables to change from editor." |

- **Severity:** N62 🟢 Low (a misleading hint). F9 and F14a are features.
- **Baseline:** this round's commit (TASK-N59_N61_F1, TASK-F4_F6_N36 and TASK-UI7 accepted and committed). Start only after it. Line numbers below are from the working tree on 2026-10-24.
- **Implementer:** Codex. Independent of TASK-N27: no shared files. One task at a time, each with its own baseline snapshot.

## 1. Objective

1. **F9:** the big card fades in over 0.7 s when it appears and fades out over 0.6 s when it goes. Both times can be set in the Inspector.
2. **F14a:** the HUD texts get a dark, semi-transparent box behind them, so they read well over the lava: HP, Life, Mana, the opponent's Mana, the phase announcer and the message line.
3. **N62:** pressing Send during the opponent's Player action always says "It's your opponent's turn.", not "Choose a target and press Confirm before Send."

## 2. Current behavior

### 2.1 F9: the big card pops in and out

- **`CardPreviewController`** (`InGame/CardPreviewController.cs`, a component on `PlayerSessionPrefab`) shows the big card:
  - `ShowPreview` (`:59-96`) instantiates `m_cardPreviewPrefab` (`CardViewUIPrefab`) into `PlayerUI.CardViewDock` (or the canvas) and fits it to the dock.
  - `HidePreview` (`:116-125`) deactivates and destroys it at once.
  - `HasPreview` / `ShownCard` tell the callers what is shown.
- **Callers:**
  - `PlayerUI.OnCardPointerEntered` / `OnCardPointerExited` (hover over a hand-row card, `PlayerUI.cs:443-462`) and `ValidateHoveredCard` (`:464-475`)
  - `PlayerSession.SelectObject` (`PlayerSession.cs:428-460`): every world click first hides, then shows the clicked table card
  - `PlayerUI.ClearLocalUI` (`:696-701`): cleanup on disconnect / server stop
- **`CardViewUIPrefab`'s root already has a `CanvasGroup`** (alpha 1, interactable, blocks raycasts). No script uses it.

### 2.2 F14a: the HUD texts sit directly on the 3D scene

`PlayerUIPrefab` (`Assets/Prefab/UiPrefab/`), direct children of the root canvas, all `TextMeshProUGUI` without a background:

| Object | Anchor / position / size | Text | `PlayerUI` field |
|---|---|---|---|
| `HPTextBar` | top-right (−140, −100), 200 × 50 | "HP : 100", white, autosize 30–72 | `m_hpText` |
| `LifeTextBar` | top-right (−140, −150), 200 × 50 | "Life : 3" | `m_lifeText` |
| `ManaTextBar` | top-right, pivot right (−40, −200), 650 × 50 | your mana, white, right-aligned | `m_manaText` |
| `OpponentManaTextBar` | top-right, pivot right (−40, −250), 650 × 50 | the opponent's mana | `m_opponentManaText` |
| `PhaseAnouncer` | top-centre (0, −119), 1560 × 150 | phase + countdown, orange, two lines (`GameAnnouncerUI`) | — |
| `ManaMessageText` | top-centre (0, −400), 1000 × 50 | the 2-second hints, yellow-orange; empty when hidden (`PlayerUI.cs:121-127`, `:738`) | `m_messageText` |

- **UI6's `ResolutionProfileLayout`** on the prefab root moves and resizes all six in the low profile (its `m_lowRects`).
- Over the lava, white and orange text has little contrast (your F14 note).

### 2.3 N62: the wrong hint for Send on the opponent's turn

`PlayerSession.TryHandlePackage` (`PlayerSession.cs:1083-1107`, TASK-N59_N61_F1):

```csharp
if (CheckPackage(package))
{
    if (GameMaster.Instance.CanPlayerSelectCardTargets(this)) { send }
    else if (phase == PlayerAction && active player is another session) ShowOpponentTurn();
}
else ShowConfirmTargetFirst();
```

- During the opponent's action the player usually has no confirmed package, so `CheckPackage` fails first and the hint is **"Choose a target and press Confirm before Send."** Following it doesn't help: the server accepts card input only from the active player.
- Codex followed the spec's §4.5 order exactly. The spec's own acceptance criterion (AC5: "Send during the opponent's PlayerAction → `notice_opponent_turn`") expected the other result. **The fault is in my spec**, not in Codex's code.
- `ConfirmTurn` (`:174-210`) already checks the opponent's turn first (PlayerAction or ResolveCards).

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/CardPreviewController.cs` | Fade in / out (§4.1) |
| `Assets/Scripts/UI/PlayerUI.cs` | `ClearLocalUI` hides at once (§4.1) |
| `Assets/Prefab/PlayerSessionPrefab.prefab` | Only if Unity serializes the two new fields with their defaults; no manual values |
| `Assets/Scripts/UI/HudTextBacking.cs` (**new**) | Box that follows one text (§4.2) |
| `Assets/Prefab/UiPrefab/PlayerUIPrefab.prefab` | Six backing objects (§4.2) |
| `Assets/Scripts/InGame/PlayerSession.cs` | `TryHandlePackage` order (§4.3) |
| `Docs/AI/Architecture/Architecture.md`, `GameLoop.md`, `Dependencies.md` | §4.4 |

**Dependencies:**
- **The big card never takes clicks** (report #13 checklist, A3; N50). The fade must not change that.
- **N54:** HUD graphics must not catch clicks meant for the board. Every new `Image` has `raycastTarget` off.
- **UI6 / UI7:** the low profile moves the six texts. The boxes follow the texts at run time, so `ResolutionProfileLayout` needs no new entries.
- **RP15:** no per-frame string formatting or allocation in `Update` / `LateUpdate`.

## 4. Required changes

### 4.1 F9: fade the big card

**`CardPreviewController`:**

```csharp
[Header("Fade (F9)")]
[SerializeField, Min(0f)] private float m_fadeInDuration = 0.7f;
[SerializeField, Min(0f)] private float m_fadeOutDuration = 0.6f;
```

- **Showing:** after instantiating the preview, take its root `CanvasGroup` (add one if the prefab ever loses it) and start it at alpha 0, then raise alpha to 1 over `m_fadeInDuration`. A duration of 0 shows it at once.
- **Hiding:** `HidePreview()` no longer destroys at once. It moves the current preview into a single **fading-out slot**, clears `m_currentPreview` and `ShownCard` **immediately** (callers see "no preview" at once, as today), lowers alpha to 0 over `m_fadeOutDuration` and then destroys it. A duration of 0 destroys at once.
- **At most one fading view:** if a second view starts fading out while one still fades, destroy the older one at once.
- **Same card again:** if `ShowPreview` is called for the card that is fading out (a world click hides and re-shows the same card; the pointer jitters on a card's edge), **reuse that view**: move it back to current and fade in from its present alpha. No new instance, no flicker.
- **A different card:** the old one fades out while the new one fades in at the same dock spot (a short cross-fade).
- **Change only `alpha`.** Leave `interactable` and `blocksRaycasts` as the prefab has them, so the big card's click behaviour doesn't change.
- **Time:** use `Time.unscaledDeltaTime` (UI time; unaffected by the game's time scale).
- **Driving the fade:** in the existing `LateUpdate` (or `Update`), only while a fade is running. Keep `FitDock` working for the current view.
- **Immediate hide:** add `public void HidePreview(bool immediate)`; the existing `HidePreview()` calls it with `false`. With `true`, destroy both the current and the fading view at once.
- **Lifetime:** in `OnDisable` / `OnDestroy`, destroy any fading view at once. The view lives under the HUD's dock, which can outlive the session object.
- **`PlayerUI.ClearLocalUI`** (`PlayerUI.cs:696-701`): call `HidePreview(true)`. Nothing should fade after a disconnect.
- **The other callers stay as they are.**

### 4.2 F14a: dark boxes behind six HUD texts

**New script `Assets/Scripts/UI/HudTextBacking.cs`:**

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// F14a: a dark box behind one HUD text. It follows the text's rendered bounds,
// so UI6/UI7 profile changes and autosizing need no extra layout data.
[RequireComponent(typeof(Image))]
public class HudTextBacking : MonoBehaviour
{
    [SerializeField] private TMP_Text m_target;
    [SerializeField] private Vector2 m_padding = new Vector2(16f, 6f);
    …
}
```

**Behaviour:**
- **`Awake`:** cache the `Image` and `RectTransform`. Set `raycastTarget = false`, anchors and pivot to (0.5, 0.5).
- **`LateUpdate`:**
  - **Visible** only when `m_target` is active and enabled and its text isn't empty or whitespace (`string.IsNullOrWhiteSpace(m_target.text)`). Otherwise disable the `Image` and stop.
  - **Placement**, only when something changed since the last frame (compare cached values: the target's `textBounds`, `position`, `lossyScale`, `rotation`):
    - `position` = `m_target.rectTransform.TransformPoint(m_target.textBounds.center)`
    - `localScale` and `localRotation` = the target's (both objects share the parent)
    - `sizeDelta` = `textBounds.size` + 2 × `m_padding`
  - No allocation and no string formatting per frame.
- A one-frame lag after a text change is fine (TMP updates its bounds when the canvas renders).

**`PlayerUIPrefab`:** for each of the six texts in §2.2, add a sibling GameObject **directly before it** in the hierarchy, so it draws behind the text:

| New object | `m_target` |
|---|---|
| `HPTextBarBacking` | `HPTextBar` |
| `LifeTextBarBacking` | `LifeTextBar` |
| `ManaTextBarBacking` | `ManaTextBar` |
| `OpponentManaTextBarBacking` | `OpponentManaTextBar` |
| `PhaseAnouncerBacking` | `PhaseAnouncer` |
| `ManaMessageTextBacking` | `ManaMessageText` |

- **Components:** `RectTransform`, `CanvasRenderer`, `Image` (the built-in `UISprite`, Sliced, colour (0, 0, 0, 0.55), **`raycastTarget` off**), `HudTextBacking` (`m_padding` (16, 6)).
- **Don't change the six texts,** their fields, their sibling order relative to each other, or the `ResolutionProfileLayout` lists.
- **Not included:** button labels, the Leave dialog, the lobby and menu scenes. The full panel redraw (F14) stays Later.

### 4.3 N62: Send checks the opponent's turn first

In `PlayerSession`:

```csharp
// True while another player's action (or its card resolution) runs.
private bool IsOpponentActing()
{
    GamePhase _phase = GameMaster.Instance.m_gamePhase.Value;
    return (_phase == GamePhase.PlayerAction || _phase == GamePhase.ResolveCards) &&
           GameMaster.Instance.m_activePlayer.Value.TryGet(out PlayerSession _active) && _active != this;
}
```

- **`TryHandlePackage`:** right after the `GameMaster.Instance == null` check:

  ```csharp
  if (IsOpponentActing())
  {
      if (IsOwner && PlayerUI.Instance != null)
          PlayerUI.Instance.ShowOpponentTurn();
      return;
  }
  ```

  Then the existing `CheckPackage` / `CanPlayerSelectCardTargets` code, **without** its `else if` opponent branch (now unreachable). The package isn't touched on this path: `CheckPackage` isn't called.
- **`ConfirmTurn`:** replace its inline condition (`:195-196`) with `IsOpponentActing()`. Same behaviour.
- **`TryConfirmTarget`:** unchanged. A player may still pick a target during the opponent's turn.

### 4.4 Docs

- **`Architecture.md`** §9 (UI): the big card fades (F9); `HudTextBacking` (F14a).
- **`GameLoop.md`:** the `PlayerUI` messages paragraph: Send on the opponent's turn shows "opponent's turn" first (N62).
- **`Dependencies.md`:**
  - RP21 (menu / HUD layouts): HUD backing boxes follow their texts; new HUD texts that need a box get a `HudTextBacking` sibling placed before them.
  - A note under the big-card entry: `HidePreview()` fades; use `HidePreview(true)` for cleanup.

## 5. Explicit non-goals

- **No other animation:** no movement or scale, no hand-card animation, no change to where the big card docks or its size.
- **No full HUD redraw** (F14), no new art, no font or colour change of the texts.
- **No boxes outside `PlayerUIPrefab`.**
- **No change to `TryConfirmTarget`, `CheckPackage`, the server checks or the message texts.**
- **No new localization keys.**

## 6. Constraints and compatibility requirements

- **Keep all serialized names and references.** New fields use defaults (0.7 / 0.6, padding (16, 6)), so no Inspector setup is needed except the six new prefab objects.
- **No network change:** no RPC or NetworkVariable is added or removed (both peers on the same build anyway).
- **N54:** no new raycast target in the HUD.
- **Unity 6 APIs, no warnings of our own.** Keep baseline copies in `build/f9-f14a-n62-verification/baseline/`.
- **UI6 regression:** re-run the UI6 HUD sweep (16 sizes × 3 locales). The 199 inherited diagnostics must stay the same set; no new entries.
- Restore the dynamic font before handing over (`AGENTS.md`, N28).

## 7. Acceptance criteria

1. **F9:**
   - Hovering a hand card: the big card's alpha goes 0 → 1 in 0.7 s (± one frame). Leaving it: 1 → 0 in 0.6 s, then the object is destroyed.
   - Changing both fields in the Inspector (e.g. 0.2 / 1.5) changes the timing; 0 / 0 shows and hides at once.
   - Moving the pointer quickly across the 10 hand cards never leaves more than two preview objects under the dock, and none after the pointer leaves the row (after 0.6 s).
   - A world click on the same table card twice: no flicker (the same view is reused).
   - A world click on empty ground fades the big card out.
   - The big card still takes no clicks: a click on the board behind it during a fade works as before.
   - After a disconnect (`ClearLocalUI`), no preview object remains, at once.
2. **F14a:**
   - Each of the six texts has a dark box exactly behind its visible text, with 16 / 6 units of padding, in 1920×1080 and in the low profile (1280×720), in en / ru / ja.
   - The message box appears only while a message is shown; the boxes disappear with their texts (empty or inactive).
   - "Player Dead" and the two-line announcer are covered fully.
   - No new raycast target: board clicks under the boxes (top-right corner) work.
3. **N62:**
   - During the opponent's Player action: Send with no confirmed target → "It's your opponent's turn." (not "Choose a target …"). Send with a target confirmed before → the same message, and the package is kept.
   - During your own action: Send with no target → "Choose a target and press Confirm before Send."; a valid package is sent as before.
   - Confirm Turn behaves exactly as before.
4. **No regression:** hover, click, docking and fitting of the big card as before; the UI6 sweep has no new diagnostic; the release player builds with 0 errors and no warnings of our own.

## 8. Regression tests

- **Play Mode harness** (temporary, `Temp/F9Verification/`):
  - AC1 with the real `PlayerSessionPrefab` / `CardPreviewController` and `PlayerUIPrefab`: sample alpha every frame; count preview objects under the dock; test the same-card reuse and `HidePreview(true)`.
  - AC2 with the real `PlayerUIPrefab` at 1920×1080 and 1280×720, en / ru / ja: compare each box's world rect with its text's bounds plus padding; check `raycastTarget` on every new `Image`.
  - AC3 with the host fixture from TASK-N59_N61_F1 (two sessions, the real `PlayerUI`): opponent's action with and without a package, own action. Assert the shown message and whether `CardInputServerRpc` was sent.
- **Baseline:** on the baseline, run AC3's first case and report the "Choose a target …" message (that's N62).
- **Screenshots** (into `build/f9-f14a-n62-verification/`): the HUD in GameScene at 1920×1080 and 1280×720, ru, with all six boxes visible (a message shown); the big card at the middle of its fade-in.
- **UI6 sweep** as in §6.
- Remove the harness from `Assets` and `Temp`; keep a copy in `build/f9-f14a-n62-verification/`.

## 9. Manual Unity Editor checks

**Setup:** two Editors (main = host, clone = client, `-logFile C:\Lesson\clone-editor.log`), 1920×1080, then 1280×720 for step 4.

1. **Big card, hover:** in Card Selection, hover a hand card: the big card fades in (about 0.7 s); move off: it fades out (about 0.6 s). Sweep quickly over all cards: no stuck or doubled card.
2. **Big card, table:** in Player action, click a table card twice: it stays (no flicker); click the ground: it fades out.
3. **Timing fields:** stop Play Mode, select `PlayerSessionPrefab`, set Fade In 2 and Fade Out 2 on `CardPreviewController`, play: both fades take 2 s. Set them back to 0.7 / 0.6 (or keep values you like and tell me).
4. **HUD boxes:** over the lava, HP, Life, Mana, the opponent's Mana, the announcer and a message (press Confirm with no card) each have a dark box behind the text, in both windows, in en and ru, at 1920×1080 and 1280×720.
5. **Board clicks** in the top-right corner (under the HP / Mana boxes) still work, in both windows.
6. **N62:** in the CLIENT window, during the host's Player action, press Send: "It's your opponent's turn." Then in your own action, press Send with no target: "Choose a target and press Confirm before Send."
7. **Logs:** no new warning or exception in either Editor log.

## 10. Open questions and assumptions

- **A1, box colour and padding** (black 55 %, 16 × 6) are a starting point. You can change the colour on each `…Backing` object in the prefab, and the padding on its `HudTextBacking`.
- **A2, the cross-fade** when switching between two cards is a natural side effect of fading both. If you'd rather have the old card vanish at once when a new one appears, say so (a one-line change).
- **A3, the HUD texts' own `raycastTarget`** (`HPTextBar`, `LifeTextBar` and `PhaseAnouncer` have it on) is unchanged. It hasn't caused a reported problem.
