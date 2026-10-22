# UI6 implementation — 2026-10-22

## 1. Task summary

Implemented TASK-UI6 against baseline `72a724c`: automatic Low/Medium-high HUD profiles, larger HUD labels and a wide button column, a docked card preview with hand hover, and work-area-aware Windowed sizing. Implementation follows the prescribed geometry. **Acceptance remains incomplete:** AC3's strict rectangle non-overlap conflicts with the preserved medium layout, and eight AC4 description-size measurements miss their targets. These are detailed below for architecture review; they were not hidden by changing the card prefab or the approved medium geometry.

The release Windows player built successfully. The final Play Mode harness completed without exceptions. Actual native Game-view switches crossed the 1366 threshold, and the Settings-first player selected 1920×1200 Windowed on the owner's display.

## 2. Files changed

| File | Purpose |
|---|---|
| `Assets/Scripts/UI/UiResolutionProfile.cs` + `.meta` | Static width threshold |
| `Assets/Scripts/UI/ResolutionProfileLayout.cs` + `.meta` | Generic authored-layout capture, compact overrides, exact restore |
| `Assets/Scripts/UI/PlayerUI.cs` | Dock reference, local controller cache, hover cleanup, hand-width refresh |
| `Assets/Scripts/InGame/CardViewUI.cs` | Pointer-enter/exit events |
| `Assets/Scripts/InGame/CardPreviewController.cs` | Dock fitting/refitting, same-card reuse, `ShownCard`, hide cleanup |
| `Assets/Scripts/UI/DisplaySettingsUI.cs` | Work-area bounds and aspect-preferred automatic size |
| `Assets/Prefab/UiPrefab/PlayerUIPrefab.prefab` | Label ranges, column, dock and 13 low rectangles |
| `Assets/Scenes/GameScene.unity` | Remove two Confirm-turn label overrides; set effective announcer minimum to 30 |
| `Docs/AI/Architecture/Architecture.md` | UI6 ownership and window-sizing documentation |
| `Docs/AI/Architecture/Dependencies.md` | Profile and hover/preview dependencies |
| This report | Results, screenshots, limitations and manual checklist |

Pre-existing edits to `GameLoop.md`, the UI1–UI5 task deletions, and untracked issue report/task files are preserved. No commit or push was made. Verification sources, measurements, logs and builds are retained under ignored `build/ui6-verification/`; temporary scripts/prefabs are removed from `Assets` and `Temp`.

## 3. Important implementation details

`ResolutionProfileLayout` applies Low below width 1366 in Awake and on subsequent profile changes. It resolves a missing scaler through its parent, captures the authored reference and every target's full transform values, restores nonuniform local scales exactly, leaves a zero low reference alone, and warns once for missing targets. It executes before `PlayerUI`, so a width change re-spaces the hand before rendering. It is a plain MonoBehaviour and adds no networking.

The dock is immediately before `LeaveMatch`, with a CanvasGroup that blocks no raycasts. Preview instances are left-aligned, vertically centred, fitted uniformly and refitted when the dock changes. Repeated requests for the same shown card reuse the view. The UI4 scale-1 pointer/clamp path remains available without a dock. Hide deactivates the old view before deferred destruction, preventing a frame with two visible previews.

Hand events use the local `m_playerSession` controller. An old card's exit cannot hide the next card's preview. Play/discard handlers are unchanged. Removed-hand-card validation and `ClearLocalUI` hide the appropriate preview; subscriptions are removed when hand views are cleared. `PlayerSession`, its ownership/input checks, RPCs and world-click logic are unchanged.

The serialized `GameAnnouncerUI` component added to the GameScene HUD instance also had `m_minFontSize: 24` and reapplied it in Awake. Its value is now 30 in addition to the TMP override; otherwise the specified effective floor would revert at runtime. Announcer logic is unchanged.

Windowed bounds are work-area width minus 16 and height minus 64, inclusive. Missing work-area dimensions use display minus one on both axes. `BuildResolutionList` keeps its signature and current-size exception; Windowed callers pass bounds. `PickWindowedSize` first chooses a fitting entry within 1% of display aspect, then any fitting entry, then the supplied fallback. The fullscreen list and Settings minimum 1024×720 are unchanged.

Final Low values are exactly the task table; no discretionary shifts or width reductions were needed:

| Target | Anchors min → max | Pivot | Position | Size delta | Scale |
|---|---|---|---|---|---|
| Panel | (.5,1) → (.5,1) | (.5,.5) | (0,-90) | (1080,160) | 1 |
| PhaseAnouncer | (.5,1) → (.5,1) | (.5,.5) | (0,-90) | (1040,140) | 1 |
| HPTextBar | (1,1) → (1,1) | (.5,.5) | (-140,-50) | (200,50) | 1 |
| LifeTextBar | (1,1) → (1,1) | (.5,.5) | (-140,-100) | (200,50) | 1 |
| ManaTextBar | (1,1) → (1,1) | (1,.5) | (-40,-210) | (600,50) | 1 |
| OpponentManaTextBar | (1,1) → (1,1) | (1,.5) | (-40,-260) | (600,50) | 1 |
| ManaMessageText | (.5,1) → (.5,1) | (.5,.5) | (0,-330) | (1000,50) | 1 |
| HandContainer | (.5,0) → (.5,0) | (.5,.5) | (0,175) | (1408,250) | .625 |
| ConfirmBtn | (1,0) → (1,0) | (.5,.5) | (-190,302) | (300,92.85) | 1 |
| SendBtn | (1,0) → (1,0) | (.5,.5) | (-190,189) | (300,92.85) | 1 |
| ConfirmTurnBtn | (1,0) → (1,0) | (.5,.5) | (-190,76) | (300,92.85) | 1 |
| LeaveBtn | (0,0) → (0,0) | (.5,.5) | (174,56) | (300,64) | 1 |
| CardViewDock | (0,0) → (0,1) | (0,.5) | (24,-35) | (456,-290) | 1 |

No additional Inspector setup is required; all new references and low targets are wired. The null scaler reference intentionally uses the parent fallback. Both network hashes remain 3051432279 / 151063585; existing NetworkBehaviour order and serialized references are preserved.

## 4. Tests actually executed and results

Unity 6000.3.13f1, Windows x64.

| Check | Actual result |
|---|---|
| Pure threshold tests | 639/640/1365 Low; 1366/3840 Medium-high — passed |
| Resolution lists and bounds | UI5 input, duplicates, minimum, fullscreen, current exception, inclusive bounds and zero-work-area fallback — passed |
| Automatic Windowed size | 16:10 → 1920×1200; 16:9 → 1600×900; nonmatching aspect / no-fitting-entry fallbacks — passed |
| GameScene inspection | Zero missing scripts; both network hashes; effective announcer floor 30; authored reference 2560×1600 — passed |
| Generic profile edge cases | Parent scaler, zero reference, nonuniform-scale restore, one missing-target warning — passed |
| Low → Medium restore | All authored target values/scales within .01; reference restored — passed |
| Layout/content matrix | All 16 specified sizes × en/ru/ja × hands 0/5/10 = 144 measured combinations |
| HUD text | All requested HUD TMP floors ≥30; realistic HP/life/mana and longest announcer variants; no overflow/truncation — passed |
| Bounds and hand clearance | Low and medium elements/dialog contained; hand centred; ≥10-unit clearance from button column and Leave — passed |
| Rectangle overlap | Low passed; inherited medium intersections remain — AC3 exception below |
| Card fit | All 4 definitions × 3 locales × 16 sizes contained by the dock/canvas — passed |
| Card pixels | Measured actual TMP font × cumulative preview/nested transform scale × canvas factor; AC4 misses below |
| Hover/click harness | ExecuteEvents enter, exit, crossing, old exit after new enter, same-card reuse, removed card, profile refit, actual play/discard clicks, hand rebuild, local cleanup — passed |
| GraphicRaycaster | Preview graphics produced no pointer hits; group blocksRaycasts=false — passed |
| No-dock regression | Scale 1 and clamping at three screen positions — passed |
| Native Game-view changes in Play Mode | 1366 → 1360 → 1366 switched profile and re-spaced hand without restart/error |
| Final Windows release build | Succeeded; 0 errors; 1 BuildReport warning; Editor warnings described below |
| Release startup | Final release opened LoginScene; native UI observed; no authentication performed; clean quit and no logged error/exception |
| Actual Windowed player | Selected 1920×1200 from native fullscreen; full decorated window fits work area; returned to native fullscreen |

The live observations were reference/hand/spacing = 2560×1600 / 1800 / 177.7778 at width 1366, 1600×1000 / 1408 / 134.2222 at 1360, and the original values again at 1366. A later return to the 1024×768 preset was visually observed, but the observer did not record that final frame; the session was stopped manually. This is not counted as an additional logged pass.

The final full harness completed at 01:16:46 with no runtime exceptions. Its warnings were the intentional missing-target warning and two NGO warnings from modifying the synthetic unspawned hand list. Earlier harness-development failures are separate from this completed run. No C# compiler errors or warnings were found. The final release build completed at 01:22:22 with 0 errors and 1 BuildReport warning. The Editor log also contains an Addressables empty-profile-id warning and a dynamic-font importer inconsistency warning; these are not C# warnings from the implementation.

For N49 the read-only Settings-first test player reported display 2560×1600, work area (0,0,2560,1552), and Windowed client 1920×1200. Native capture metadata gave decorated origin (319,180), size 1922×1232: right 2241 < 2560, bottom 1412 < 1552. Fullscreen returned to 2560×1600. The test player quit cleanly and its saved log contains no error/exception. Its temporary probe is excluded from the release build and removed from the project. The Settings-first fixture bypasses Bootstrap, so OS Japanese text with an English saved-language dropdown is expected fixture behavior; language preferences were not changed.

Evidence is in `build/ui6-verification/`: `edit-results.txt`, `play-results.txt`, `runtime-results.txt`, `final-verification-editor.log`, `card-measurements.csv`, `layout-issues.txt`, `build-results.txt`, `release-build-editor.log`, `release-assembly-check.txt`, `player-observations.txt`, `window-fit-observation.txt`, and `test-player.log`. The release is `WindowsPlayer/FallenStar.exe`; `TestPlayer` is a separate Settings-first diagnostic build.

Final audit: all 112 original HUD prefab object IDs and original component order are retained; exactly four objects/components were added for the dock and plain profile. The network prefab list, all five menu scenes, PlayerSession code/prefab, card prefab, ProjectSettings and build-scene settings match their original contents. The existing GameLoop edit matches the start-of-task copy. Unity was closed from saved LoginScene, then the dynamic font was restored byte-for-byte from the baseline (SHA256 recorded in `final-audit.txt`). `git diff --check` passed. The final release assembly includes the two new production profile classes and excludes all temporary verification/probe classes; its serialized assets contain no `UI6TestHUD` fixture. The final release was rebuilt after removing the fixture and its automatic NGO prefab-list registration. `final-editor.log`, `final-status.txt`, `final-audit.txt`, `release-startup.txt` and `release-player.log` preserve the final checks.

Required Russian screenshots (rendered from the GameScene HUD in Play Mode with 10 cards and a docked preview; these are HUD fixtures, not live network matches):

- [640×480](C:/Lesson/FallenStar-Spellcasters/build/ui6-verification/HUD-ru-640x480.png)
- [1024×768](C:/Lesson/FallenStar-Spellcasters/build/ui6-verification/HUD-ru-1024x768.png)
- [1280×720](C:/Lesson/FallenStar-Spellcasters/build/ui6-verification/HUD-ru-1280x720.png)
- [1366×768](C:/Lesson/FallenStar-Spellcasters/build/ui6-verification/HUD-ru-1366x768.png)
- [2560×1600](C:/Lesson/FallenStar-Spellcasters/build/ui6-verification/HUD-ru-2560x1600.png)

## 5. Remaining risks and acceptance exceptions

**Description pixels, AC4:** the exact prescribed dock plus the unchanged card text boxes does not meet every minimum. Actual results and shortfalls, in pixels:

| Locale / card | Screen | Actual | Target | Shortfall |
|---|---|---:|---:|---:|
| en / Fireball | 640×480 | 12.957 | 14 | 1.043 |
| en / Fireball | 1366×768 | 16.642 | 18 | 1.358 |
| ru / Fireball | 640×480 | 12.548 | 14 | 1.452 |
| ru / Healing Hooves | 640×480 | 11.695 | 14 | 2.305 |
| ru / Ice Shard | 640×480 | 12.548 | 14 | 1.452 |
| ru / Fireball | 1366×768 | 16.117 | 18 | 1.883 |
| ru / Healing Hooves | 1366×768 | 15.021 | 18 | 2.979 |
| ru / Ice Shard | 1366×768 | 16.117 | 18 | 1.883 |

The other measured description cases at these two target sizes meet the minimum. For the worst low case, reaching 14 from 11.695 requires about 20% more preview scale, beyond the permitted compact-layout adjustments. Medium dock dimensions are prescribed, with no equivalent adjustment permission. Reaching the missing targets requires an architecture decision about dock geometry or card text/art; no such out-of-scope changes were made.

**Card names:** all 12 names, measured pixels at the two required sizes:

| Locale / card | 640×480 | 1366×768 |
|---|---:|---:|
| en / Clarity | 12.726 | 16.345 |
| en / Fireball | 12.726 | 16.345 |
| en / Healing Hooves | 12.317 | 15.820 |
| en / Ice Shard | 12.726 | 16.345 |
| ru / Clarity | 12.726 | 16.345 |
| ru / Fireball | 12.637 | 16.231 |
| ru / Healing Hooves | 8.869 | 11.391 |
| ru / Ice Shard | 10.611 | 13.628 |
| ja / Clarity | 12.726 | 16.345 |
| ja / Fireball | 12.726 | 16.345 |
| ja / Healing Hooves | 11.055 | 14.199 |
| ja / Ice Shard | 12.726 | 16.345 |

Russian Healing Hooves misses 10 px by 1.131 at 640×480 and 12 px by .609 at 1366×768. This is the task's reportable art/name-plate limitation.

**Rectangles, AC3:** the unchanged medium `ManaTextBar` rectangle intersects Panel and PhaseAnouncer; `OpponentManaTextBar` intersects Panel. This occurs at seven of eight medium resolutions (not 3440×1440), across the three locales and hand counts: 189 recorded pair intersections. Measured text ink does not intersect announcer ink. Preserving medium positions per §6 and eliminating all these pre-existing rectangle intersections are contradictory requirements. Low has no such intersections outside the explicitly allowed banner/card exceptions.

**Multiplayer/manual limits:** no live two-Editor authenticated match was executed for UI6. ExecuteEvents and GraphicRaycaster checks establish local event behavior and UI pass-through, but do not prove an actual board-target click through the dock, host/client visual parity, or hover feel during real play. U15 (covering useful board content) and U16 (hover distraction) remain subjective checks from issue report #10. The main Editor was returned to LoginScene; Game view is 1024×768.

## 6. Manual Unity Editor verification steps

1. Open the main project and a ParrelSync clone, authenticate manually, and start a two-player match. No new Inspector assignments are required.
2. At 2560×1600 confirm the wide three-button column, HUD labels, unchanged medium positioning, and Leave dialog drawn above the dock.
3. Hover all four hand cards in en/ru/ja, move between overlapping cards and off the hand, and left/right-click to play/discard. Confirm no flicker and preview retention through clicks. Remove a hovered card and end the match; the view should hide.
4. Click own/opponent board cards, then a target under the dock. Confirm the world click reaches the board and the next world click hides the preview. Repeat on host and client.
5. Check the task's low presets, then switch 1360×768 ↔ 1366×768 during the match. Confirm immediate profile/hand re-spacing, containment, dialog fit, and no Console errors.
6. Review the measured description/name exceptions and inherited rectangle intersections with the architecture reviewer before accepting AC3/AC4. Check U15/U16 during actual play.
7. Launch the release player, navigate to Settings, choose Windowed and confirm 1920×1200 fits above the taskbar; choose Fullscreen and confirm native size. Recheck on different display scaling/work-area arrangements if supported.

The user requested PC shutdown after the work. The shutdown request is issued only after the saved report, final source/diff audit and verification cleanup.
