# UI5 implementation — resolution and display mode

## 1. Task summary

Implemented `TASK-UI5_ResolutionSetting.md` on baseline `05cb219` (clean working tree at start). Settings now has Language, Display mode and Resolution rows. Display choices apply immediately and use Unity's existing screen-state persistence.

## 2. Files changed

- `Assets/Scripts/UI/DisplaySettingsUI.cs` and Unity-generated `.meta`: new display controller.
- `Assets/Scenes/SettingsScene.unity`: panel dimensions, row positions, two duplicated labels/dropdowns and the wired controller.
- `Assets/Localization/UI_table Shared Data.asset` and `UI_table_en/ja/ru.asset`: four specified keys and translations. Created through Localization Tables → CSV(Merge), with Unity-generated IDs; existing entries preserved.
- `Docs/AI/Architecture/Architecture.md`, `Dependencies.md`, `GameLoop.md`: display settings ownership and behavior.
- This report.

## 3. Important implementation details

- Pure `BuildResolutionList` filters monitor modes by minimum size and display bounds, removes refresh-rate duplicates, always includes the actual current size, and sorts width then height descending. Resolution captions use U+00D7, verified in the existing font.
- Fullscreen maps to `FullScreenWindow`; Windowed maps to `Windowed`. Exclusive fullscreen is displayed as Fullscreen. Only the three-argument `Screen.SetResolution` overload is used.
- Fullscreen selection requests native display size. Windowed selection retains a strictly fitting current size or selects the largest fitting monitor mode.
- Specification conflict reported before implementation: always including the real current size makes the prescribed "current is in the windowed list" check tautological. The current entry remains visible, while a switch to Windowed independently checks strict display bounds. The current-size exception also applies to unusual command-line sizes, including those below the normal minimum.
- `Update` compares actual width, height and mode, then refreshes without notifications or another resolution request. Locale and dropdown listeners are removed on disable; initialization avoids attaching listeners if disabled while waiting for localization.
- This is local, offline UI. No networking, authority, ownership, new PlayerPrefs keys or startup overrides were added. `SettingsUI`, `GameSettingsPrefs`, `Bootstrap`, all CanvasScalers, ProjectSettings and other scenes are unchanged.
- Inspector wiring is already complete. Both new dropdowns duplicate the language control's appearance. Resolution Template height is 300; Language and Display mode remain 150.

Final layout follows the specification exactly: panel 600×400; title (0,350); labels 240×46 at (−150,280/220/160), left-aligned; dropdowns 260×38 at (140,280/220/160). Back remains 160×30 at (0,31), with its original callback and references.

## 4. Tests actually executed

- Unity 6000.3.13f1 compiled the controller and recompiled after temporary test scripts were removed. No C# compiler errors or warnings were found.
- Pure list harness passed: prescribed fullscreen and windowed examples, duplicate rates, current 1600×900 insertion/order, empty input, real-current exception, and equal-width height sorting.
- Scene checks passed with zero missing scripts. Existing language reference, Back geometry, new references and template heights were checked in Unity.
- Populated en/ru/ja text was measured at all nine required resolutions: rows fit the panel/canvas and text has no overflow or missing-glyph substitutions. Rendered locale previews were inspected.
- Play Mode passed actual `Start` initialization, truthful captions, callback-free external refresh/re-enable, and mode relabeling. All three actual TMP popups fit at the nine sizes, including a 20-entry resolution list.
- Release Windows player: **Succeeded; errors=0; warnings=0**, normal six-scene order. Output: `build/ui5-verification/WindowsPlayer/FallenStar.exe`. Its runtime assembly contains `DisplaySettingsUI` and no temporary probe.
- Separate Settings-first Windows test player also built with 0 errors/0 warnings. Native player interactions plus a temporary read-only screen logger confirmed:
  - 2560×1600 native fullscreen on the current display.
  - 1920×1080 fullscreen applies, with black bars above/below rather than stretching; clicks still align.
  - Windowed retains fitting 1920×1080; selecting 1366×768 resizes immediately with a visible title bar.
  - Normal quit/relaunch restores Windowed at 1366×768.
  - Alt+Enter updates mode and resolution captions.
  - Native fullscreen → Windowed chooses the largest fitting advertised mode, 2048×1536, with title bar visible.
  - Language selection ru → ja → en updates the labels/options immediately and preserves the display selection.
  - Launch with `-screen-fullscreen 0 -screen-width 1366 -screen-height 768` overrides the prior 2048×1536 setting and initializes truthful captions.
- Player logs contain no runtime exceptions from these runs. Final diff and whitespace checks passed; pre-existing serialized references were audited. The font asset was restored byte-for-byte from the pre-task snapshot.

Temporary Editor/Play Mode harnesses were removed from `Assets` and `Temp/UI5Verification`. Their sources, test logs and locale renders remain in ignored `build/ui5-verification/`. The existing Addressables Editor warning `ProfileValueReference: GetValue called with empty id` appeared during build preparation; both BuildReport summaries above contain zero warnings. Harness development/reload issues were corrected before the final passing run.

## 5. Remaining risks / limits

- Player interaction tests used a separate Settings-first build to exercise the production controller without authentication. The normal Login → Main Menu → Settings → Back route was inspected statically but not exercised in the new release player.
- Multi-monitor moves, an actual exclusive-fullscreen launch, and a two-player match at 1024×768/background play were not run. No match behavior or networking was changed.
- If a display advertises no fitting windowed mode, only the actual current size may remain, as required for an empty input. No synthetic resolution fallback was introduced.

The original native fullscreen state (2560×1600), saved English language choice and open LoginScene were restored. No commit or push was made.

## 6. Manual Unity/player verification

1. Run the normal release player, log in, open Settings from Main Menu and return with Back. Check the three rows and original navigation.
2. Repeat mode/resolution, Alt+Enter and language selections on the owner's other displays; quit/relaunch to confirm Unity persistence there.
3. Choose Windowed 1024×768, play a two-player match and check U14: the complete board, both player areas and HUD corners remain visible.
4. Alt-tab away for about 10 seconds during that match; verify play continues for the other player. Restore the preferred display setting afterwards.
