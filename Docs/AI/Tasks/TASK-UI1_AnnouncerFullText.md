# TASK-UI1: The game announcer shows its full text, undistorted

- **Source:** [RESEARCH-UIScaling_Visibility](../Research/RESEARCH-UIScaling_Visibility.md) → N37, N38, N41 (announcer part). Owner decisions of 2026-10-21: D4 (centred, font 24–48), D5 = b (": " plus a shorter respawn prefix), D6 (shrink, then "…" as a last resort, plus a warning).
- **Severity:** 🟡 Medium (N37, Confirmed static), 🟢 Low (N38, N41)
- **Baseline:** `391e079` plus the uncommitted working tree. `GameAnnouncerUI.cs` carries the L6 change-only refresh, which is uncommitted; keep it. Line numbers are from the working tree.
- **Order:** 1 of 4. Do this first; it doesn't depend on UI2–UI4. TASK-UI3 later re-anchors the same object and expects the size set here.
- **Implementer:** Codex

## 1. Objective

In every phase, language (en/ja/ru) and player/team combination, the announcer shows **both lines in full**. That means no "…", no cut letters, and glyphs with normal proportions. Line 2 reads naturally, with a separator, e.g. "Current turn: red player. Your turn".

## 2. Current behavior

- **Layout:**
  - `PhaseAnouncer` in `PlayerUIPrefab` is a **200×50** rect at (0, 681).
  - It has a **non-uniform scale of (7.910777, 1.9326, 1)** (`PlayerUIPrefab.prefab:30`).
  - Its TMP margins are (3.54, −9.07, 2.44, −59.71) (`:134`), so the text area is 194 local units wide.
  - Alignment is Left/Top (`:102-103`).
- **`GameAnnouncerUI` is added to that object in the scene, not in the prefab:**
  - It's at `GameScene.unity:599` (`&282301681`): `m_maxVisibleLines 2`, `m_autoSize 1`, `m_minFontSize 14`, `m_maxFontSize 72`, `m_useEllipsis 1`.
  - `ApplyAnnouncerTextFormat()` (`GameAnnouncerUI.cs:202-215`) forces NoWrap, the max lines, autosize min/max and Ellipsis. It runs in `Awake`, `OnValidate` and on every text change.
- **Result:**
  - Every line-2 variant needs a font size of 7.6–12 to fit 194 units. The floor is 14, so TMP cuts the line with "…": 54–86 % is visible in all 24 variants (research §3).
  - The ×7.9 / ×1.9 scale draws every glyph about 4.1× wider than tall.
- **Missing separator:** line 2 is `$"{_prefix}<color=…>{_coloredPart}</color>"` (`:119-121`). The en/ru prefixes have no trailing separator, so it renders "Current turnred player…". In en, the respawn line also repeats line 1 ("Waiting for respawn").

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Prefab/UiPrefab/PlayerUIPrefab.prefab` | `PhaseAnouncer` RectTransform (`:21-40`) and its TMP (`&4772283208930924661`, `:48-…`) |
| `Assets/Scenes/GameScene.unity` | The added `GameAnnouncerUI` values (`&282301681`, `:599-…`). The PrefabInstance overrides on the PhaseAnouncer TMP (`4772283208930924661`) must not contradict the new values. |
| `Assets/Localization/UI_table_en.asset`, `UI_table_ru.asset` | `Announcer.TurnPrefix` (`m_Id 10388797693652992`), `Announcer.RespawnPrefix` (`m_Id 10389089290055680`) |
| `Assets/Scripts/UI/GameAnnouncerUI.cs` | One warning when the text is still truncated |

Dependencies:
- The font is `MPLUSRounded1c-Medium SDF` (dynamic): point size 139, line height 1.485 em.
- `TMP_Text.isTextTruncated` exists (`com.unity.ugui` `TMP_Text.cs:875`). `TextMeshProUGUI` sets it in Ellipsis/Truncate mode.

## 4. Required changes

1. **`PhaseAnouncer` RectTransform** (prefab):
   - `m_LocalScale` → **(1, 1, 1)**
   - `m_SizeDelta` → **(1560, 150)**
   - Keep anchors and pivot (0.5, 0.5) and `m_AnchoredPosition` (0, 681). TASK-UI3 re-anchors it later.
2. **`PhaseAnouncer` TMP** (prefab):
   - `m_margin` → (0, 0, 0, 0)
   - alignment **Center / Middle** (`m_HorizontalAlignment: 2`, `m_VerticalAlignment: 512`)
   - Keep the font, color, material and rich text.
3. **`GameAnnouncerUI` in `GameScene.unity`:**
   - `m_minFontSize` → **24**
   - `m_maxFontSize` → **48**
   - Keep `m_maxVisibleLines 2`, `m_autoSize 1`, `m_useEllipsis 1`.
   - The TMP values that `OnValidate` writes (font size min/max, autosize, NoWrap, Ellipsis) end up as scene overrides. That's expected. Make sure the overrides show 24/48, not 14/72.
4. **Strings** (keep the trailing space):

   | Key | en | ru | ja |
   |---|---|---|---|
   | `Announcer.TurnPrefix` | `Current turn: ` | `Сейчас ходит: ` | unchanged (`現在の手番：`) |
   | `Announcer.RespawnPrefix` | `Respawning: ` | `Возрождается: ` | unchanged (`復活待ち：`) |

   Edit them in the Localization Tables window, or write YAML **double-quoted** values so the trailing space survives:
   - en: `m_Localized: "Current turn: "` and `m_Localized: "Respawning: "`
   - ru: `m_Localized: "Сейчас ходит: "` and `m_Localized: "Возрождается: "`
   - Don't change keys, IDs or other entries.
5. **`GameAnnouncerUI.SetAnnouncerText`:** after `m_timeText.ForceMeshUpdate()`, if `m_timeText.isTextTruncated`, log one `Debug.LogWarning` per distinct text. Use a `HashSet<string>` field, so the same text doesn't log again every second. Include the text in the message. Don't change the text-building logic, the L6 change detection or the line normalization.

## 5. Explicit non-goals

- No anchoring changes for this or other HUD elements (TASK-UI3), and no CanvasScaler changes (TASK-UI2).
- No change to `Phase.*`, `PlayerColor.*` or the `*ColoredPart` strings, to ja, or to the two-line structure.
- No third line, and no wrapping (D6).
- No change to the colors (`GetPlayerColorHex`) or the countdown.

## 6. Constraints and compatibility requirements

- Keep all serialized field names of `GameAnnouncerUI` and the `m_timeText` reference.
- `PlayerUIPrefab` is an in-scene NetworkObject. Don't touch its `GlobalObjectIdHash` overrides.
- `Assets/Fonts/MPLUSRounded1c-Medium SDF.asset` changes when text renders in the Editor. Restore it before handing over (`AGENTS.md`).
- The warning must not run every frame. L6 already rebuilds the text only on a change, and the HashSet limits it to once per distinct text.

## 7. Acceptance criteria

1. For each locale (en, ja, ru), each of PlayerAction/RespawnWait, your/opponent and red/blue, the composed text (built exactly as `UpdateAnnounceText` does) gives `isTextTruncated == false`.
   - That's 24 line-2 cases.
   - Also check every phase line without a target, "Phase 00".
2. The worst case (en/ru opponent respawn, en opponent turn) renders at a font size of **≥ 30**. The expected size is about 48; report the value you actually get.
3. Glyphs aren't stretched: the object's scale is (1, 1, 1).
4. Line 2 reads "Current turn: <color>red player. Your turn</color>" in en, "Respawning: …" in en, "Сейчас ходит: …" / "Возрождается: …" in ru, and ja as before.
5. On 2560×1600, the announcer stays in the top banner and doesn't overlap the HP/Lives/mana texts.
6. No truncation warning appears in a normal match.

## 8. Regression tests

- Editor check (temporary, under `Temp/UI1Verification/`, removed afterwards as in earlier tasks):
  - Load `GameScene` (or instantiate `PlayerUIPrefab` under a canvas with the GameScene CanvasScaler).
  - For each case in AC1, switch the locale with `LocalizationSettings.SelectedLocale`.
  - Build the string with the same keys and format as `UpdateAnnounceText`, call `SetAnnouncerText`, and assert `!isTextTruncated`.
  - Log the font size it chose.
- Roslyn or Unity compile, exit 0.
- `git diff` shows only the files in §3, plus the expected scene overrides.

## 9. Manual Unity Editor checks

1. Game view at **2560×1600**. Play a two-player match in **en**:
   - Your turn, then the opponent's turn: the full line 2 with ": ", and no "…".
   - Fall into lava so your character respawns, then let the opponent's respawn: "Respawning: <color>… player. This is you / This is the opponent</color>".
2. Repeat in **ru** and **ja** (change the locale in Settings).
3. Look at the glyphs: round letters (o, о, の) look round, not wide ovals.
4. Check the Console: no "Announcer text truncated" warning.

## 10. Open questions and assumptions

- **ja respawn prefix:** "復活待ち：" still repeats line 1 ("復活待ち"). It was kept as agreed. If you want it to match en/ru, a shorter option is "復活中：".
- **Size:** 1560×150 at (0, 681) spans y 606–756, inside the banner (y 493–773). If it overlaps anything at runtime, report it rather than resizing other elements (TASK-UI3 owns the layout).
- **Measurements** come from the committed font atlas without kerning. AC1 is the authoritative check.
