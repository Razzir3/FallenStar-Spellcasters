# TASK-N28: Stop committing the dynamic font atlas data

- **Source issue:** [Issue Report #7 → N28](../Reports/2026-10-20_IssueReport-7.md#2-new-findings-and-updates)
- **Severity:** 🟢 Low (Confirmed)
- **Baseline:** `b7e7b91`
- **Order:** 19 of 20. Independent. Best done when no other task has the font asset modified.
- **Owner decision (2026-10-20):** **clear on build.** Turn on "Clear Dynamic Data On Build", and don't commit atlas changes that only come from rendering text in the Editor.
- **Implementer:** Codex

## 1. Objective

- The committed TMP font asset holds no runtime glyph data.
- Player builds start with an empty dynamic atlas and fill it at runtime from the source font.
- From now on, font-asset changes caused only by Editor rendering aren't committed, so unrelated diffs stay small.
- en, ru and ja text still renders everywhere, in the Editor and in builds.

## 2. Current behavior

- `Assets/Fonts/MPLUSRounded1c-Medium SDF.asset` (73,065 lines) is the project's TMP font asset:
  - `m_AtlasPopulationMode: 1`, dynamic (`:170`)
  - `m_IsMultiAtlasTexturesEnabled: 1` (`:5542`), with 5 atlas texture slots and `m_AtlasTextureIndex: 4`
  - `m_ClearDynamicDataOnBuild: 0` (`:5544`)
  - `m_SourceFontFile` references the source font (`:168`, GUID `8dd8438c…`)
  - `m_FallbackFontAssetTable: []` (`:72780`)
- Every glyph rendered in the Editor (ru/ja text, new labels) is added to the asset's glyph and character tables and to its atlas textures. The file then shows as modified, and has been committed in 6 commits so far (report #7 §2: most recently `b7e7b91`, +320/−34).
- The owner's checklist already treats it as "not a temporary change" (Temp changes sheet, note row).

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Fonts/MPLUSRounded1c-Medium SDF.asset` | Enable Clear Dynamic Data On Build; clear the dynamic data once |
| `AGENTS.md` | One commit rule for this asset (§4.3) |

Dependents: every TMP text in all scenes and prefabs uses this asset (directly or as the default). Nothing references glyph indices.

## 4. Required changes

1. Select the font asset. In the Inspector, under Generation Settings, tick **Clear Dynamic Data On Build**. `m_ClearDynamicDataOnBuild` becomes `1`.
2. With the asset selected, run **Clear Dynamic Data** once (the font asset's context menu, ⋮ → *Clear Dynamic Data*). This empties the glyph and character tables and the atlas textures.
   - Keep `m_AtlasPopulationMode: 1`, the atlas size (1024), the padding, the render mode, multi-atlas and `m_SourceFontFile` unchanged.
   - Save the project, then check that the diff touches only the dynamic data and the flag.
3. Add one rule to `AGENTS.md` under "Implementation rules":
   > `Assets/Fonts/MPLUSRounded1c-Medium SDF.asset` is a dynamic font. Rendering text in the Editor changes it. Don't commit those changes: restore the file (`git restore`) before committing, unless the task deliberately changes the font settings (N28).
4. Don't stage the asset again after Play Mode re-populates it during your checks. Restore it to the cleared state from step 2 before you report.

## 5. Explicit non-goals

- No static atlas or fallback font asset. That was the owner's other option, which they didn't choose.
- No change to any text component, font size or material.
- No `.gitattributes` or `skip-worktree` tricks.

## 6. Constraints and compatibility requirements

- A dynamic font needs its source font at runtime. Make sure the source font file is still referenced, and that the player build renders ru/ja (§9.3). If it doesn't, stop and report.
- Unity version 6000.3.13f1, the bundled TextMeshPro (uGUI). Use the Inspector actions; don't edit the 73k-line YAML by hand.

## 7. Acceptance criteria

1. The committed asset has `m_ClearDynamicDataOnBuild: 1` and empty dynamic glyph and character tables. The file is much smaller than 73k lines.
2. In the Editor, en/ru/ja text renders: MainMenu, Settings (日本語 / Русский in the dropdown), Lobby, the match HUD, cards and Result. No □ boxes.
3. In a player build, the same screens render in ja and ru. No □ boxes.
4. `AGENTS.md` contains the rule from §4.3.

## 8. Regression tests

- None automated (asset settings).

## 9. Manual Unity Editor checks

1. After step 2, enter Play Mode from LoginScene → Settings → 日本語 → main menu → Lobby → a match (two windows) → Result. Every text renders. Afterwards the font asset is modified again (expected): restore it with `git restore` and confirm `git status` is clean for it.
2. Same in ru.
3. Build the player (Windows), start it, Settings → 日本語, then the main menu, the lobby and a match HUD. Every text renders. This also covers the pending D.4 / D.5 row of the manual checklist.

## 10. Open questions and assumptions

- Assumption: the first time each glyph is drawn at runtime, rendering it costs a little. That's acceptable for menus and the HUD.
