# F9 / F14a / N62 implementation

## 1. Task summary

Implemented `TASK-F9_F14a_N62_HudPolish.md` on baseline `314498a`, preserving the existing uncommitted N27 work.

- Big-card previews fade in/out using unscaled time, with Inspector defaults 0.7 / 0.6 seconds.
- Six HUD text labels have black, 55% opaque backing Images following their rendered text bounds.
- Send checks the opponent's PlayerAction/ResolveCards before package validation and preserves the package on that path.

No commit or push was made. The temporary verification helpers from this task and the interrupted N27 session were archived outside Assets and removed before the release build. The dynamic font was restored to the pre-task baseline.

## 2. Files changed

Relative to the F9 baseline snapshot under `build/f9-f14a-n62-verification/baseline/`:

- `Assets/Scripts/InGame/CardPreviewController.cs`
- `Assets/Scripts/InGame/PlayerSession.cs`
- `Assets/Scripts/UI/PlayerUI.cs`
- New `Assets/Scripts/UI/HudTextBacking.cs` and `.meta`
- `Assets/Prefab/UiPrefab/PlayerUIPrefab.prefab`
- `Docs/AI/Architecture/Architecture.md`, `GameLoop.md`, `Dependencies.md`
- This report

`PlayerSessionPrefab`, `CardViewUIPrefab`, scenes, localization assets and ResolutionProfileLayout are unchanged by this task. The earlier N27 code/assets remain in the working tree. They are not part of the F9 diff.

## 3. Important implementation details

The preview controller holds one current view and at most one outgoing view. Hiding clears HasPreview/ShownCard immediately. Re-entering the outgoing card transfers its existing view back to current and starts fading from its current alpha; switching cards cross-fades. An older outgoing view is deactivated, detached and destroyed before a new outgoing slot replaces it. Zero durations apply the final alpha and remove the view immediately. OnDisable, OnDestroy and `PlayerUI.ClearLocalUI` use immediate cleanup, removing both slots even though their parent dock can outlive the controller.

Fades change only CanvasGroup alpha, retaining the prefab's interaction flags. Dock fitting and the no-dock pointer/clamp fallback remain. New fields use their code defaults; PlayerSessionPrefab needs no manual Inspector override.

HudTextBacking caches its Image/RectTransform, checks active/non-whitespace text, and uses textBounds plus 16 horizontal / 6 vertical units of padding. Bounds, position, scale, rotation and padding are cached so unchanged frames do not rewrite placement. LateUpdate creates no collections or formatted strings. Each backing is the immediately preceding sibling of its target; the existing text objects, serialized references, relative order and profile lists remain unchanged. The six new Images use UISprite/Sliced, black alpha 0.55 and raycastTarget=false. No additional Inspector wiring is needed.

The shared IsOpponentActing predicate is used by ConfirmTurn and TryHandlePackage. Opponent Send returns before CheckPackage, so even a stale package is retained rather than cleared by validation. Own-turn package validation and the server RPC/handler are unchanged. TryConfirmTarget, CheckPackage, server authority, RPC layout and localization keys are unchanged.

## 4. Tests actually executed and results

Unity 6000.3.13f1. Evidence and archived harness source are under `build/f9-f14a-n62-verification/`.

| Check | Result / evidence |
|---|---|
| N62 baseline | Real local NGO host with actual session/HUD prefabs reproduced `notice_confirm_target_first` for opponent Send without a package. `baseline-results.txt`. |
| N62 final | Passed empty, stale and valid opponent packages in PlayerAction/ResolveCards; packages retained and no server queue entry added. Own empty Send kept the target hint; own valid Send reached the actual server queue and cleared the package. Confirm's opponent guard retained. `final-results.txt`. |
| Fade timing | Sampled alpha each frame with timeScale=0. Default 0.7 s in measured 0.770 s, including a first-use 0.521 s warmup frame; 0.6 s out measured 0.616 s. Custom 0.2 s in / 1.5 s out measured 0.217 / 1.517 s, at approximately 0.0168 s per frame. Completion stayed within one observed frame; alpha was monotonic. 0 / 0 applied immediately. `alpha-samples.csv`, `final-results.txt`. |
| Fade lifecycle | Passed same-card identity/alpha reuse, return to an outgoing card, cross-fade, 40 rapid switches with at most two dock children, eventual empty dock, immediate ClearLocalUI cleanup and controller-disable cleanup. Prefab interaction flags remained unchanged. An underlying raycast/click probe received a click during a fade. `final-results.txt`. |
| Backing bounds | All six targets passed center, padding, scale/rotation, sibling order, visibility and non-raycast checks at 1920×1080 / 1280×720 in en/ru/ja. Player Dead and the two-line announcer were covered; whitespace and inactive targets hid the boxes and re-enable restored placement. `final-results.txt`. |
| UI6 regression | Existing 16-size × three-locale × hand-size 0/5/10 sweep passed, including hover, click, selection, docking, cleanup and profile restoration. **199 inherited diagnostics, identical set: zero added and zero removed.** Fade durations were set to zero only in this layout fixture; animation was checked separately above. `ui6-regression/play-results.txt`, `ui6-comparison.txt`. |
| Static audit | **149 checks passed**, including preservation of every original HUD YAML object except the six root sibling insertions, exactly 30 new YAML objects, unchanged CheckPackage/TryConfirmTarget/CardInputServerRpc, material/raycast configuration and unchanged scene/localization/profile assets. `static-audit.txt`. |
| Screenshots | HUD at 1920×1080 and 1280×720 in Russian, plus an actual mid-fade frame at alpha about 0.5. The real HUD prefab is rendered against the loaded GameScene lava with sample values and gameplay controllers disabled. Logical canvas dimensions match the normal CanvasScaler profile; the capture camera/view is a fixture. Final images were visually inspected. `HUD-ru-1920x1080.png`, `HUD-ru-1280x720.png`, `HUD-mid-fade-ru-1280x720.png`, `pictures-results.txt`. |
| Release build | **Succeeded, 0 errors, 2 warnings**, normal six scenes and BuildOptions.None. Four built-assembly checks passed: debug guard compiled off, HudTextBacking and both HidePreview overloads present, enum values preserved, and temporary verification types absent. `build-results.txt`, `release-build-editor.log`, `release-assembly-check.txt`. No test HUD registration is retained. |

Early fixture failures are retained separately: an absent scene-only announcer component and the initial raycast setup were corrected in the harness. A zero-duration fade-out check prompted explicitly setting outgoing alpha to zero before destruction. The final Play Mode run exited successfully. Screenshot capture was corrected to include both the UI and GameScene environment; only the final inspected captures are delivery evidence.

## 5. Remaining risks

The host fixture uses two real spawned session prefabs with a synthetic registry; it does not establish an authenticated remote client or run a complete online match. Remote input, disconnect timing and visual acceptance remain the owner's two-peer checks below. The 199 inherited UI6 layout/art diagnostics remain unchanged.

The BuildReport records two warnings; the build log contains environment/service diagnostics for an Addressables profile value with an empty ID, an unlinked Unity Services project, and a missing symbol-upload token. No C# compiler warning or error was reported. The release player was built but not manually authenticated or played online in this task.

The task intentionally preserves the existing text raycast flags (some HP/phase text targets are enabled); the new backing Images and preview views add no click interception. No claim is made that unrelated existing HUD hit regions changed.

The release build includes the earlier N27 work already present at task start. Its previous host verification evidence remains in `build/n27-verification/`; no new remote-client mane verification was performed in this task.

## 6. Manual Unity Editor verification steps

1. Open the project in Unity 6000.3.13f1. Use the same build in host and client. No new Inspector wiring is required.
2. During Card Selection, hover a card and leave: check roughly 0.7 s in / 0.6 s out. Sweep across ten cards and confirm no orphan preview remains after leaving. Click the same table card twice, then empty ground; the first action reuses the view, and ground starts the fade-out.
3. On PlayerSessionPrefab's CardPreviewController, temporarily set the two Fade fields to 2 / 2 and verify the longer fades; restore 0.7 / 0.6. Check 0 / 0 if desired. Confirm board clicks behind a fading preview still work.
4. Over the lava, inspect all six backing boxes, including a two-line announcer, Player Dead, and a message, in both peers at 1920×1080 and 1280×720 in en/ru/ja. Empty/inactive message text should hide its box. Existing text appearance/profile layout should match the baseline.
5. In the client window during the host's action, press Send with no package and with a previously confirmed package: both should show the opponent-turn hint and keep the package. In your own action, empty Send should show the target hint; valid Send should work normally. Confirm Turn should behave as before.
6. Disconnect while a preview is visible or cross-fading: both views must disappear immediately. Check both logs for new warnings/exceptions and run the release player through the same visual checks.
