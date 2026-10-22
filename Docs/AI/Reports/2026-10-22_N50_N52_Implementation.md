# N50 / N52 implementation — 2026-10-22

## 1. Task summary

Implemented `TASK-N50_N52_ClientCardPreview.md` on top of the existing uncommitted UI6 working tree.

- **N50:** preview targets are resolved when a preview opens. A controller created before the GameScene HUD exists can find it later, and destroyed scene targets are resolved again on the next request.
- **N52:** hover validation follows the rebuilt HUD row, including played cards in PlayerAction and ResolveCards. Removed or destroyed hovered cards hide their matching preview safely.

The specified Play Mode regression suite passed. A real authenticated two-player match remains the owner's manual check.

## 2. Files changed

| File | Change |
|---|---|
| `Assets/Scripts/InGame/CardPreviewController.cs` | Replace Start-time lookup with private `TryResolveTargets`; one warning latch per instance |
| `Assets/Scripts/UI/PlayerUI.cs` | Change only `ValidateHoveredCard` to use the row dictionary and distinguish reference-null from destroyed cards |
| `Docs/AI/Architecture/Architecture.md` §3.3 | Document on-demand target resolution and HUD-row validation |
| `Docs/AI/Architecture/Dependencies.md` | Replace the preview dependency's Start entry with ShowPreview / TryResolveTargets |
| This report | Verification results and manual checklist |

Start-of-task copies are under `build/n50-verification/baseline/`. Existing UI6 changes and the architecture reviewer's §3.2 client-timing note were preserved. No production scene, prefab, serialized reference, networking setting, RPC or ownership rule was changed. No commit or push was made.

## 3. Important implementation details

`ShowPreview` calls `TryResolveTargets` immediately after rejecting a null card, before its same-card early return. The helper resolves only missing references, in dock → canvas → root order. It prefers the available HUD canvas, then uses the existing root screen-space fallback. All cached Unity objects use `== null` / `!= null`, so destroyed objects can be replaced. Live Inspector-assigned canvas and root references remain intact.

Start no longer performs work or logs; the method was removed as explicitly permitted by the task. When targets cannot be resolved, the existing warning is emitted at most once for the controller's lifetime. Successful recovery does not reset the warning latch. Preview fitting, scaling, same-card reuse, hiding, toggling and UI4 clamping remain unchanged.

`ValidateHoveredCard` first returns for a reference-null hover. It retains a live hovered card when its ID remains in `m_cardViews`. Otherwise it hides the preview only when `ShownCard` matches, then clears the hover. The Unity-null guard prevents reading `NetworkObjectId` from a destroyed component. Enter/exit handlers, hand clearing, local cleanup, play/discard clicks and PlayerSession world-click logic are unchanged.

Both fixes run in local UI code. No server/client messages or network state were added. No Inspector setup is required.

## 4. Tests actually executed and results

Unity **6000.3.13f1**, Windows x64, batch-mode Play Mode with graphics enabled. A temporary Editor harness instantiated the actual PlayerUIPrefab and CardViewUIPrefab. It used a stand-in Lobby canvas and a synthetic NGO spawn table to exercise actual `UpdateCards`, `GetHandCards` and `GetBoardPlayCards` without authentication or a network connection. This is a lifecycle/UI regression test, not a two-player match.

The final Play Mode process exited **0**. `captured-errors.txt` is empty. No C# compiler errors or warnings were found. Per-case observations:

| Case | Preview parent | Uniform scale | Missing-target warnings |
|---|---|---:|---:|
| Client order: controller alive through Lobby destruction, then HUD hover | CardViewDock | 2.43500 | 0 |
| Same client controller, direct ShowPreview as used by board selection | CardViewDock | 2.43500 | 0 |
| Host order: HUD exists before controller lifecycle frames | CardViewDock | 2.43500 | 0 |
| Same controller after destroying and replacing the first HUD | New CardViewDock | 2.43500 | 0 |
| PlayerAction rebuild retains hovered played card | CardViewDock | 2.43500 | 0 |
| ResolveCards rebuild retains hovered played card | CardViewDock | 2.43500 | 0 |
| CardSelection rebuild retains hovered hand card | CardViewDock | 2.43500 | 0 |
| Removal from each of those three HUD rows | None | n/a | 0 |
| Destroyed hovered card, followed by rebuild | None | n/a | 0 |
| Low-profile dock change / LateUpdate refit | CardViewDock | 2.28000 | 0 |
| Medium-profile restore / LateUpdate refit | CardViewDock | 2.43500 | 0 |
| No HUD, root screen-space canvas | Fallback canvas | 1 | 0 |
| No canvas/root, five repeated requests | None | n/a | 1 total |
| Missing-target controller recovers, then loses its canvas again | Fallback canvas, then none | 1, then n/a | Still 1 total |

Scales are **fixture measurements** from a fixed 2560×1600 world-space HUD canvas; they are not pixel-size claims for a particular screen preset. Every dock assertion compared the actual scale with `min(dock.height / view.height, dock.width / view.width)` and checked active state, left alignment and vertical centring.

Additional passed assertions: no target lookup/log during lifecycle frames before the first request; null-card requests stay silent; same-card requests reuse the preview; crossing cards and a late exit do not hide the next card; leaving hides the matching view; actual left/right pointer clicks preserve the preview and play/discard selections; local cleanup hides it; the dock CanvasGroup remains nonblocking; no-dock scale-1 clamping works at both screen corners; live assigned canvas/root references are retained.

Two preliminary harness runs exposed synthetic NGO teardown setup gaps. The fixture was corrected to supply a NetworkConfig and remove its synthetic spawn-table entry before destroying a card. The production fix did not change during those corrections. Earlier logs are retained separately in `first-run/` and `second-run/`; the root result files contain the final successful run.

The release **StandaloneWindows64**, `BuildOptions.None`, built successfully: **0 errors, 3 BuildReport warnings**, process exit **0**. The log reports an Addressables empty-profile-ID warning, a Unity Services unlinked-project warning and the dynamic-font importer inconsistency. No C# compiler errors or warnings occurred in the Play Mode or release-build runs. The release executable is `build/n50-verification/WindowsPlayer/FallenStar.exe`. Its runtime assembly contains `TryResolveTargets` and the warning latch, and excludes the temporary `N50Verification` class.

Final review compared the two scripts and architecture documents against their start-of-task copies, separating this fix from uncommitted UI6. The §3.2 client-timing note is unchanged. The dynamic font was restored byte-for-byte from this task's baseline, and the NGO prefab list matches its baseline. Temporary `Assets/Editor` and `Temp/N50Verification` sources were removed. `git diff --check` passed; the saved status comparison contains no new asset/settings changes and preserves every pre-existing modified, deleted and untracked entry. The only new repository file added by this task is this report.

Evidence and the retained verification source are under `build/n50-verification/`: `play-results.txt`, `play-editor.log`, `captured-errors.txt`, `EditorTools/N50Verification.cs`, the build log/results, baseline copies and final audit. Temporary verification source is removed from `Assets` and `Temp` after verification.

## 5. Remaining risks

- A real two-Editor match requires user authentication and was not run by Codex, as the task explicitly specifies. The lifecycle harness proves the target lookup and hover rules; actual host/client play, world targeting through the preview and repeated authenticated matches remain manual checks.
- N51 description sizing, N53 inherited rectangle intersections and U15/U16 usability decisions are outside this task and unchanged.

## 6. Manual Unity Editor verification steps

1. Open the main project and a ParrelSync clone; use a separate clone log (`-logFile C:\Lesson\clone-editor.log`). Authenticate manually. Have the host create a lobby and the clone join as the client.
2. On the client, hover every hand card, cross between cards, leave the row, and left/right-click to play/discard. Confirm the docked view appears and survives clicks.
3. In PlayerAction, click own and opponent board cards, click a target under the preview, and confirm the next world click hides it. Hover a played row card with the pointer stationary through rebuilds; its preview should remain until the card leaves the row.
4. Repeat client checks at 1280×720 and 1920×1080, then repeat on the host.
5. Complete result → lobby → match without restarting either Editor and repeat hover and board-click checks. Confirm the new HUD receives the preview.
6. Inspect both logs: no game-code exception and no missing-screen-space-canvas warning during normal play. No new Inspector assignments are needed.
