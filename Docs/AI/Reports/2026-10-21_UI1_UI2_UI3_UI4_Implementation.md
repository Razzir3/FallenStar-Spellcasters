# UI1–UI4 implementation

Date: 2026-10-21. Unity: 6000.3.13f1. Baseline: `391e079` plus the existing uncommitted working tree, as required by the four specifications. Previous implementation changes, staged L10 deletions, the N8 documentation refresh, task-document deletions and other user changes were preserved. No commit, push, reset or new staging operation was performed.

## 1. Task summary

- **UI1:** the announcer has normal proportions, a centered 1560×150 text box, scene font limits 24–48, the specified English/Russian separators and shorter respawn prefixes, and a warning once per distinct truncated text. Existing L6 state/localization caching and countdown behavior remain.
- **UI2:** all six root canvases use Scale With Screen Size / Expand, with 1920×1080 front-end references and 2560×1600 for GameScene. Login's existing background image uses an Envelope Parent AspectRatioFitter at 1.7769.
- **UI3:** the HUD root and LeaveMatch stretch to the canvas; children use the specified edge/corner anchors and positions. The banner and Confirm-turn scales are normalized. The centered hand keeps its vertical position, compresses within 1800 units, and old views deactivate before deferred destruction. The LeaveMatch canvas-size workaround is removed.
- **UI4:** previews keep assigned references or choose the HUD/root screen-space canvas, rebuild their layout and clamp to local canvas bounds. An oversized preview centers on the affected axis. The target-Confirm label's autosize minimum is 14.

The implementation, Editor checks, isolated Play Mode tests and release build are complete. A live two-peer match and the manual visual checks below remain.

## 2. Files changed by this request

The following are the 13 production files changed by UI1–UI4; earlier pending changes in the same scripts remain intact:

| File | Changes |
|---|---|
| `C:/Lesson/FallenStar-Spellcasters/Assets/Scripts/UI/GameAnnouncerUI.cs` | Truncation HashSet/warning after ForceMeshUpdate |
| `C:/Lesson/FallenStar-Spellcasters/Assets/Scripts/UI/PlayerUI.cs` | Cached hand layout, width/spacing calculation, deactivate-before-Destroy |
| `C:/Lesson/FallenStar-Spellcasters/Assets/Scripts/UI/LeaveMatchUI.cs` | Remove only the layout/canvas rect fields, initialization and resize block |
| `C:/Lesson/FallenStar-Spellcasters/Assets/Scripts/InGame/CardPreviewController.cs` | Canvas selection, missing-canvas guard and measured bounds clamp |
| `C:/Lesson/FallenStar-Spellcasters/Assets/Prefab/UiPrefab/PlayerUIPrefab.prefab` | Specified root/child transforms, announcer alignment/margins and Confirm-label minimum |
| `C:/Lesson/FallenStar-Spellcasters/Assets/Scenes/GameScene.unity` | Scaler match mode, effective HUD root overrides and announcer 24/48 settings/overrides |
| `C:/Lesson/FallenStar-Spellcasters/Assets/Scenes/LoginScene.unity` | Scaler match mode and background fitter |
| `C:/Lesson/FallenStar-Spellcasters/Assets/Scenes/MainMenuScene.unity` | Scaler mode, reference and match mode |
| `C:/Lesson/FallenStar-Spellcasters/Assets/Scenes/SettingsScene.unity` | Scaler mode, reference and match mode |
| `C:/Lesson/FallenStar-Spellcasters/Assets/Scenes/LobbyScene.unity` | Scaler mode, reference and match mode |
| `C:/Lesson/FallenStar-Spellcasters/Assets/Scenes/ResultScene.unity` | Scaler mode, reference and match mode |
| `C:/Lesson/FallenStar-Spellcasters/Assets/Localization/UI_table_en.asset` | Only the two prescribed announcer prefix entries |
| `C:/Lesson/FallenStar-Spellcasters/Assets/Localization/UI_table_ru.asset` | Only the two prescribed announcer prefix entries |

Also updated the relevant sections of:

- `C:/Lesson/FallenStar-Spellcasters/Docs/AI/Architecture/Architecture.md`
- `C:/Lesson/FallenStar-Spellcasters/Docs/AI/Architecture/Dependencies.md`
- `C:/Lesson/FallenStar-Spellcasters/Docs/AI/Architecture/GameLoop.md`
- This implementation report.

No card prefab, world-card prefab, Japanese localization table, build-scene list, network identity or gameplay/authority code was changed. The temporary Editor verification source and its generated metadata were moved out of Assets after use. The dynamic font was restored byte-for-byte to this request's initial snapshot, preserving the earlier pending N28 changes rather than reverting to HEAD.

## 3. Important implementation details

**Specification clarification reported before coding.** UI2's exact 1920×1080 / Expand front-end settings do not give identical screen-height percentages at both 16:9 and 16:10: 16:10 is narrower than the front-end reference, so Expand fits by width. The exact prescribed settings were followed. At 2560×1600, the front-end canvas is 1920×1200 at scale 1.333; at 1920×1080 it is 1920×1080 at scale 1. This difference was documented rather than claiming the broader height-percentage criterion passed unchanged. GameScene's reference remains 16:10.

**Asset scope and serialization.** Changes were applied to the specified YAML component blocks/properties. The HUD GameScene instance overrides were updated alongside the prefab. GlobalObjectIdHash `3051432279` and InScenePlacedSourceGlobalObjectIdHash `151063585` remain. Serialized UI references and field names remain; only the two private rect fields explicitly removed by UI3 were deleted. Public preview methods and HasPreview remain unchanged. Existing DebugModeGuard calls are preserved.

**Hand layout.** Awake caches the HorizontalLayoutGroup and authored spacing 220. The new serialized `m_handCardWidth` defaults to 200. The spacing calculation reads the current view's LayoutUtility preferred width; the real authored card reports **0**. At width 1800 and preferred width 0, counts up to eight retain spacing 220; nine use 200; ten use approximately 177.78. Preferred width 200 was also tested. Layout work runs after rebuild/clear, not every frame. Both old-view destruction loops deactivate objects first, allowing the existing layout group to ignore them in the same frame.

**Preview placement.** Canvas and preview-root assignments are preserved independently. If the canvas is unassigned, the HUD's root canvas is preferred; the fallback excludes world-space canvases and nested canvases. With no usable canvas/root, Start warns and ShowPreview returns early. After layout rebuild, rect size, local scale and pivot determine the clamp. The final localPosition is in the parent's local coordinates, so placement does not depend on the preview's anchor offset. Original preview debug logs were retained as required.

**Execution context.** These are local UI changes. PlayerUI's existing session binding and RPC/click paths remain. The preview is still opened by PlayerSession.SelectObject. No server/client ownership, spawning, NetworkVariable or RPC contract changed.

## 4. Tests actually executed and results

Evidence and verification source are in the ignored directory:

`C:/Lesson/FallenStar-Spellcasters/build/ui1-ui4-verification/`

| Check | Result |
|---|---|
| Actual Unity compilation | Passed with no C# compiler errors/warnings in the verification runs |
| UI1 actual TMP fitting | **51 cases passed:** all 24 locale × phase × team × own/opponent variants, plus all nine targetless phases in each locale. Every target variant selected **font size 48**, with no truncation. `edit-results.txt` records each case. |
| UI4 actual Confirm label fitting | en: size **26.15**, width **99.9706**; ru: size **15.65**, width **99.81449**; ja: size **50**, width **100**, inside the 100-unit button. Existing wrapping/overflow/max settings remain. |
| UI2 scene audit | All six enabled scenes loaded with **zero missing scripts**; scaler mode/reference checked. All non-template UI RectTransforms, including inactive lobby/leave panels, fit the logical Expand canvas at all nine prescribed resolutions. Login background covered the full canvas with the prescribed aspect at every size. |
| UI3 actual Play Mode hand layout | **198 cases passed:** counts 0–10 × preferred widths 0/200 × nine resolutions. Tested through actual CreateCardView, spacing and ClearHandUI paths with distinct test-card network IDs. Hand is centered within one unit, stays inside ±900, does not overlap any of the three buttons, and old views are inactive in the rebuild frame. ClearLocalUI restores spacing. |
| UI4 actual Play Mode previews | HUD selected despite an existing world-space canvas; screen-space fallback also checked without PlayerUI.Instance. **27 center/edge/corner placements** passed at 2560×1600, 1920×1080 and 1024×768. Actual preview layout measured **200×400**; center clicks remain centered and oversized axes center correctly. |
| Settings dropdown popup | Actual Play Mode Show/Hide at all nine resolutions passed; TMP repositions the popup within the canvas. |
| Static asset checks | **166 assertions passed**, covering preserved unrelated prefab blocks, HUD centers within one reference unit at 2560×1600, uniform authored HUD scales, preserved network hashes, scaler settings, unchanged build-scene list/order, and only the four prescribed localization values changed. `asset-results.txt`. |
| Visual inspection | Inspected rendered English/Russian announcers, lobby panels and a ten-card hand. Full announcer text is visible without stretched glyphs; the hand is centered with a clear gap before the buttons. PNG artifacts are retained. These are controlled harness renders, not screenshots of a live network match. |
| Windows release build | **Succeeded, 0 errors, 2 warnings**, BuildOptions.None / StandaloneWindows64, all six enabled scenes. `build-results.txt`, `WindowsPlayer/FallenStar.exe`. |
| Final cleanup compilation | The reopened main Editor compiled successfully after removal of the temporary helper: Tundra success **2.53 seconds**, successful assembly reload, no C# diagnostics in that compile. `final-editor-cleanup.log`. |
| Final Git/asset review | Staged and unstaged diff whitespace checks passed. Incremental diffs were reviewed against pre-task snapshots to distinguish these changes from earlier uncommitted work. Font hash equals its pre-task snapshot after final refresh. |

The nine layout sizes were 1280×720, 1366×768, 1920×1080, 2560×1440, 2560×1600, 3840×2160, 1024×768, 1280×1024 and 3440×1440. Scene geometry tests used actual Unity RectTransforms with logical canvas dimensions computed from the configured Expand formula; they do not constitute physical Game-view or player screenshots at every resolution. The Play Mode tests used isolated local UI and unspawned test cards, with no online lobby or second peer.

Two initial harness checks needed corrections, without production-code changes: the inactive dropdown Template lies below the canvas until TMP.Show repositions its popup, and unspawned NetworkBehaviour IDs defaulted to zero until assigned in the fixture. The corrected visible-popup and distinct-ID hand tests passed. The initial failure logs are retained. Two final compile-only batch launches exited before initialization because a normal Editor had reopened and held the project lock; the cleanup compile was verified in that Editor instead.

The build log contains an empty Addressables profile-reference warning, a Unity Services project-link warning and a missing Unity Cloud token message for native symbol upload. The project already has a serialized cloudProjectId; no account/link/project settings were changed. These environment/build warnings are recorded, not suppressed or reported as a warning-free build. Normal announcer cases emitted no truncation warning.

## 5. Remaining risks and checks not executed

- No live two-player match, ParrelSync comparison, real mouse-click routing across overlapping cards, surrender/leave flow, or gameplay-driven hand rebuild was run. The isolated Play Mode tests exercise the production UI paths but not NGO replication timing.
- No complete standalone player navigation run at 1366×768 or fullscreen native resolution was performed for this task. The release executable was built successfully; its online login/lobby operation was not revalidated under the batch build's Cloud warnings.
- U14 (the world board's visibility under the real orthographic game camera at 4:3) remains a manual check. No camera/world geometry was changed.
- Hand compression intentionally overlaps cards at ten cards; sibling order makes the rightmost card receive clicks in the overlap. Confirm the intended interaction during a normal match.
- The preview clamp supports oversized axes by centering, as specified; it does not shrink previews larger than their parent.
- Rendering normal scenes will regenerate the dynamic font again. Preserve the earlier N28 flag/cleared state when reviewing subsequent incidental glyph-cache changes.

## 6. Manual Unity Editor verification steps

No new Inspector references are required. The background fitter is already added; `m_handCardWidth` defaults to 200 and the cached spacing comes from the authored layout.

1. At 2560×1600, run an updated host/client match in en, ru and ja. Check own/opponent turns and respawns: full two-line announcer at normal proportions, separators as specified, no truncation warnings, no overlap with stats.
2. Switch through the nine resolution presets. Check the top-right stats, bottom-right buttons, top-left Leave and centered banner/hand. Open create/current/selected lobby states and the Settings dropdown. Login's background must cover the full display without stretching or bands.
3. Reach ten cards naturally, verify every card remains visible/clickable, select with left/right click, then play/draw to rebuild the hand. Check no sideways jump and no overlap with Confirm, Send or Confirm-turn. Compare the remote client's HUD.
4. Open previews by clicking world cards near each corner/edge and the middle at 2560×1600, 1920×1080 and 1024×768. Confirm the preview stays on the HUD canvas and inside the display.
5. In Russian, verify the target-Confirm label fits. Test Leave → Cancel and Leave → Confirm; the confirmation panel should cover the canvas at every aspect.
6. Check the real game board/camera at 4:3 (U14), then run the release player windowed at 1366×768 and fullscreen 2560×1600 through the menu, lobby, match and Result. Record any remaining environment/network issue separately from these UI changes.
