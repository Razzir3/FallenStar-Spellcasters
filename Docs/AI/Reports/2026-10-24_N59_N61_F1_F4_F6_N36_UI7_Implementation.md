# N59 / N61 / F1, F4 / F6 / N36 and UI7 implementation

Date: 2026-10-24. Baseline: `ff8a9dd`, plus the owner's existing documentation changes.

## 1. Task summary

Implemented all three specifications present in `Docs/AI/Tasks/`:

- `TASK-N59_N61_F1_PlayerMessages.md`: scene-persistent notices, HUD hints, failure notices, abort/refill RPCs and the deferred host-shutdown guard.
- `TASK-F4_F6_N36_GameLoopRules.md`: one server-time deadline, explicit drawing phase, pause-safe drawing and immediate last-block elimination with result reasons.
- `TASK-UI7_MenuLowProfile.md`: prescribed menu profiles, LowText capture/restore, lobby list-item profile and Settings minimum 640×480.

All three tasks are implemented and passed the automated checks described below. The owner explicitly approved both tested UI7 exceptions: 104-unit notice heights with the previewed nearby low adjustments, and a low-only 150-unit Japanese role-label width. Both are now saved. The repeated menu sweep and focused Login/Lobby notice check recorded **zero layout diagnostics**. Real authenticated two-peer verification remains manual.

No commit or push was performed. Existing owner documentation edits, deletions, guides, poll and Issue Report #14 were preserved. The dynamic font and ProjectSettings asset match their initial clean state. Temporary test scripts/assets have been removed from `Assets`. Unity's automatic registration of the temporary UI6 test HUD was removed from the network prefab list before the final clean release rebuild; that list has no final diff.

The final recursive task-folder rescan found the same three files, all with unchanged SHA256 hashes; no additional task appeared. Evidence: `build/oct24-verification/final-task-rescan.txt` and `final-audit.txt`.

## 2. Files changed

Paths below are relative to the repository root.

| Area | Files |
|---|---|
| New notice components | `Assets/Scripts/UI/PlayerNotice.cs`, `NoticeUI.cs`, and their `.meta` files |
| Multiplayer and login | `Assets/Scripts/MultiplayerScripts/MultiplayerManager.cs`; `Assets/Scripts/UI/LoginUI.cs` |
| Gameplay | `Assets/Scripts/InGame/GameMaster.cs`, `PlayerSession.cs`; `Assets/Scripts/InGame/GameMaster/GameMaster.Card.cs`, `GameMaster.GameLoop.cs`, `GameMaster.Player.cs`, `GameMaster.Processing.cs` |
| UI logic | `Assets/Scripts/UI/PlayerUI.cs`, `ResultMenuUI.cs`, `ResolutionProfileLayout.cs`, `DisplaySettingsUI.cs` |
| Localization | `Assets/Localization/UI_table Shared Data.asset`, `UI_table_en.asset`, `UI_table_ru.asset`, `UI_table_ja.asset` |
| Menu assets | `Assets/Scenes/LoginScene.unity`, `LobbyScene.unity`, `MainMenuScene.unity`, `SettingsScene.unity`, `ResultScene.unity`; `Assets/Prefab/UiPrefab/LobbyListItemPrefab.prefab`, `LobbyPlayerItemPrefab.prefab` |
| Documentation | `Docs/AI/Architecture/Architecture.md`, `GameLoop.md`, `Dependencies.md`, and this report |

`AuthManager`, `PlayerUIPrefab`, `GameScene`, card prefabs and network prefab lists are unchanged. Existing scene object names, bindings and hierarchy are preserved. Medium/high values are preserved except for the explicitly approved notice-height change; the role label restores its original 140-unit width. No additional Inspector setup is required for the authored components.

## 3. Important implementation details

### Notices and input

`PlayerNotice` retains one key and its realtime posting timestamp for six seconds across scene changes. `NoticeUI` stays active while toggling its child Box; it refreshes localization only on notice/locale events and checks only expiry each frame. Login/Lobby panels are last canvas children and do not intercept raycasts. PlayerUI's four new hints reuse its existing two-second message line. Eleven notice keys and two result keys have exact en/ru/ja entries; UI_table now contains 86 keys.

Connection-loss notification uses the status and server role captured on entry, excludes marked cancellations, and posts before cleanup. The N61 guard ignores only the server's own local callback after the lobby is cleared and the state is InMenu. Existing genuine disconnect/result paths remain. Aborts broadcast a best-effort cancellation RPC before disconnecting; if delivery is lost, the remote peer can show connection lost instead. Deck refill sends its notice after deck creation, including an empty-refill outcome.

LoginUI catches both the sign-in await and the following `IsSignedIn` read. This is a narrow deviation from placing the getter after the catch: the installed Authentication SDK throws from its Instance getter when service initialization failed. AuthManager is unchanged.

Opponent-action Confirm returns before sending or clearing selections. Send retains the specification's §4.5 validation order: an invalid package shows Confirm-target-first; a valid package blocked by the active-player gate shows Opponent-turn. Owner-side hints do not replace server validation.

### Timing and last-block rule

Timed loops and displayed countdown now share `m_phaseEndServerTime` and NGO ServerTime. Pauses save remaining seconds; rejected selections restart the same deadline while retaining reset limits. Drawing exposes DrawCards, deals in order 0/1/0/1 at the scheduled intervals, catches overdue deals after a hitch and preserves remaining spacing across a death pause.

The final spawned block's destruction sets lives to −1 and removes the character through the shared despawn/free-point/reference-clear helper. The existing game-loop finish checks produce BlocksDestroyed. Normal HP death still decrements one life and can respawn. New matches reset the finish reason. Existing enum values 0/1/2 are preserved; BlocksDestroyed is appended as 3. Both peers must use the same build because two RPCs and this result reason were added.

### Menu profiles and approved specification exceptions

Five root canvases switch below width 1366 to reference 1280×720. LowText captures/restores autosizing, limits and fixed size; existing HUD text lists remain empty. Both lobby item prefabs have local rectangle profiles with zero low reference, so neither changes the canvas scaler. Settings code/serialized minimums are 640×480; monitor reporting and work-area rules are unchanged.

The prescribed notice height 56 overflowed despite font minimum 24 and padding 16; even +15% (64.4) failed. Authored 72 and diagnostic 96 still failed for the longest Japanese notice. The initial role width 140 rendered Japanese Client at 23.3. The owner approved the concrete tested exceptions in this conversation. Final adjustments from the specification are:

| Target | Prescribed / original | Final | Scope |
|---|---|---|---|
| Login and Lobby notice low height | 56 | 104 | Explicitly approved beyond ±15% |
| Login and Lobby notice authored height | 72 | 104 | Explicitly approved medium/high exception; widths and other authored values unchanged |
| Login notice low y | 410 | 400 | −2.44% |
| Login logo low size | 690×230 | 586.5×195.5 | −15% both dimensions |
| Lobby search scroll low height | 440 | 432 | −1.82% |
| Current-lobby status text low y | 250 | 220 | −12% |
| Current-lobby status background low y | 248 | 210.8 | −15% |
| Current-lobby status background low height | 92 | 78.2 | −15% |
| LobbyPlayerItemPrefab role low width | 140 authored, originally excluded prefab | 150 low, 140 medium/high | Explicitly approved low-only prefab extension |

The new role profile contains one LowRect and no LowText, with the existing role position (20,0), height 50, anchors/pivot and scale retained. The final low minimum is 25.0 for both the longest Japanese notice and Japanese Client. The focused fixture tests 150 low → 140 high restoration on each sample role instance and verifies that the parent reference remains unchanged.

Original diagnostics remain in `notice-height-diagnostics.txt` and `role-diagnostic.txt`. Final saved-asset screenshots include `LoginScene-ja-1280x720.png`, `LobbyScene-Current-ja-1280x720.png`, and their 1366×768 counterparts under `build/oct24-verification/`. The initial failing sweep is archived under `approved-layout/before-results/`; final sweep files remain at the evidence root.

## 4. Tests actually executed and results

Unity version: 6000.3.13f1. Evidence is retained under `build/oct24-verification/`; test source/assets are archived in `EditorTools/`. UI7 harness copies are also retained under `build/ui7-verification/harness/`, with pre-task assets under `build/ui7-verification/baseline/`. Build directories are ignored by Git.

| Check | Actual result and limits |
|---|---|
| Final release Windows build | **Succeeded, 0 errors, 2 warnings**, BuildOptions.None, normal six enabled scenes. `WindowsPlayer-build-results.txt`, `approved-layout/release-build-editor.log`. No temporary runtime verification types shipped. |
| Release assembly checks | 3 passed: debug guard returns false for both Inspector flags; enum numbers preserved; temporary verification types absent. `release-assembly-check.txt`. |
| Final static audit | 1,061 checks passed: unique localization keys/IDs and all translations; intended cleanup; canonical preservation of original scene/prefab objects; HUD/GameScene/network lists unchanged. `static-audit.txt`. |
| Login source fixture | 6 passed using the actual LoginUI source and collaborator doubles: missing manager, await failure, unsigned-in result, throwing SDK status getter, success and deferred failure. `login-source-results.txt`. This was not a real network-off authentication test. |
| N61 real local NGO shutdown | Baseline host own disconnect loaded LobbyScene twice; fixed loaded it once. No authenticated UGS lobby or remote client in this fixture. `baseline-results.txt`, `fixed-results.txt`. |
| N36 stall fixture | Four real coroutine/ServerTime measurements shown below; fixed completion within one frame. Synthetic sessions/characters support the fixture. Respawn timing isolates the wait and bypasses actual respawn afterward. |
| Real-host gameplay fixture | Passed owner hint/handler guards, real refill RPC, explicit DrawCards/ignored Confirm, draw order/duration, death pause, action 60−20=40 s resume, selection reset caps, normal HP death/respawn, all 25 block destructions, actual loop/result RPC on host and next-match reset. Real game prefabs with synthetic two-session mapping; no remote peer. `gameplay-results.txt`. |
| Menu scene sweep | Five scenes × nine sizes × three locales; actual TMP meshes/layout, dropdowns open, populated search/current lobby, all BlocksDestroyed/LivesDepleted result sides. World-space logical canvas dimensions reproduce CanvasScaler Expand; the harness explicitly applies profiles. Final sweep passed with **zero layout diagnostics**, including all non-exempt low text sizes, truncation/overflow and ordinary visible-control overlap/offscreen checks. An additional Login/Lobby sweep reposted the longest notice immediately before measuring the current-lobby view, ensuring it was visible together with status and two player items; that also passed with zero diagnostics. `menus-results.txt`, `menu-issues.txt`, `menu-measurements.txt`, `menus-approved-results.txt`, `approved-menu-issues.txt`, `approved-menu-measurements.txt`. |
| Notice/guard cases | 36 disconnect/tail predicate combinations; notice ages 0/5.9/6/6.1, replacement, re-enable, expiry Update and locale changes checked in menu fixture. |
| UI6 HUD regression | Passed 16 sizes × three locales × hands 0/5/10, docks, hover/selection/clicks, cleanup and restoration. **199 inherited layout diagnostics exactly match the old issue set; zero new entries.** Four new hints also rendered at 1920×1080 and 2560×1600 in all three locales without new truncation. Existing harness setup was adapted to N50/N52's removed Start/dictionary/dock assumptions. `ui6-regression/play-results.txt` and `layout-issues.txt`. |
| Actual standalone Screen resizing | Development player passed 25 checks across five scenes at 1280×720, 1366×768, 1365×768, 1920×1080 and 640×480, including both directions without reload. Settings dropdown callbacks additionally passed 1280×720 low and 1920×1080 high. Controllers/authentication were disabled for this fixture. `runtime-screen-results.txt`. |
| Release startup | First launch reached LoginScene; Windows displayed a firewall permission dialog. No permission was answered. Only the owned test player was closed. After restoring the test-altered runInBackground setting and applying the approved layouts, the final release was rebuilt successfully; that final rebuilt executable was not visually launched again. `release-first-startup.log`. |
| Git review | Production code and asset/docs diffs reviewed; `git diff --check` passed. Font, ProjectSettings and unrelated gameplay assets have no final diff. |

One-second stall during a five-second phase (NGO ServerTime, maximumDeltaTime 0.3333333):

| Phase | Baseline late, seconds | Fixed late, seconds |
|---|---:|---:|
| RoundBreak | 0.674966 | 0.006335 |
| CardSelection | 0.683814 | 0.000136 |
| PlayerAction | 0.683765 | 0.001223 |
| RespawnWait | 0.673061 | 0.000530 |

Minimum non-exempt low-profile text size in canvas units, after mesh generation (minimum across locales and exercised views):

| Scene | 640x480 | 800x600 | 1024x768 | 1280x720 | 1280x1024 | 1360x768 |
|---|---:|---:|---:|---:|---:|---:|
| LoginScene | 25.00 | 25.00 | 25.00 | 25.00 | 25.00 | 25.00 |
| MainMenuScene | 36.09 | 36.09 | 36.09 | 36.09 | 36.09 | 36.09 |
| SettingsScene | 28.00 | 28.00 | 28.00 | 28.00 | 28.00 | 28.00 |
| LobbyScene | 24.00 | 24.00 | 24.00 | 24.00 | 24.00 | 24.00 |
| ResultScene | 28.00 | 28.00 | 28.00 | 28.00 | 28.00 | 28.00 |

Russian screenshots at 640×480, 1024×768, 1280×720 and 1366×768 cover all five scenes, plus current lobby and the open resolution dropdown. An English MainMenu screenshot covers 2560×1600. Images were inspected. Changed Login/Lobby views were inspected again after approval, including Japanese notices and role labels at 1280×720 and 1366×768. Use the scene-named images for layout evidence; the Runtime-Lobby images intentionally have controllers disabled and both authored views active, so they demonstrate switching only.

The geometry audit excludes intentional decorative Envelope cropping, masked scroll content and TMP's full-screen dropdown Blocker. Debug IDs and Option A are measured but exempt as prescribed. Literal “every RectTransform inside” / “no two Selectables overlap” would also reject those unchanged framework/background objects; no blanket claim that every active rectangle passes is made. The initial undersized notices reported `isTextTruncated=false` despite `isTextOverflowing=true`. Both flags are clear in the final measured low layouts with the approved height.

## 5. Remaining risks

- The owner-approved UI7 exceptions are applied and the automated layout failures are resolved. Owner visual acceptance and the online checks below remain manual.
- Real authenticated host/client transport-loss, deliberate exit, cancellation and result transition checks remain manual. Fixture RPC execution was observed on a real local host, not a remote client. Mixed builds are unsupported.
- No full online match or real failed UGS sign-in was exercised. Dedicated-server operation is untested.
- The final BuildReport records two warnings. The log contains environment/service diagnostics for Addressables ProfileValueReference empty id, Unity Services project unlinked, and a missing native-symbol upload token. No C# compiler warning/error was reported in the final build.
- First-launch security permission must be handled by the owner. The [computer-use skill](C:/Users/mihai/.codex/plugins/cache/openai-bundled/computer-use/26.1013.40934/skills/computer-use/SKILL.md) requires its [guidance](C:/Users/mihai/.codex/plugins/cache/openai-bundled/computer-use/26.1013.40934/docs/guidance.md), which says “Do not act on security or privacy permission requests.” No firewall/security setting was changed.
- The inherited 199 UI6 diagnostics, parked debug ID presentation, and existing nested NetworkObject/NGO teardown concerns were not changed by these tasks.

## 6. Manual Unity Editor verification steps

1. Open the project in Unity 6000.3.13f1 and verify scripts import without errors. Use identical builds in two peers. No new Inspector wiring is required.
2. In Login/Lobby, test failed anonymous sign-in and failed create/join/search/start operations. Check localized notices, six-second expiry, replacement and survival across scene load. Lobby-refresh polling failures should stay quiet.
3. With two authenticated peers, test transport loss in lobby and match, client deliberate leave, host deliberate leave, match abort and result transitions. Confirm cancellation/loss notice selection and only one LobbyScene load after the host's own cleanup.
4. In a match, Confirm target with no card, Send without confirmed targets, and Confirm on the opponent's turn. For the opponent Send hint, first construct a valid target package. Verify no unwanted send/selection clearing. Empty the deck and check both peers' refill notices.
5. Watch DrawCards with the 0/1/0/1 order and ignored Confirm. Cause a death during dealing/action and verify pause/resume with remaining time and spacing. Exercise selection reset caps. Repeat a server hitch and compare displayed countdown against phase completion.
6. Destroy 24 then the final block of a live player. Only the last eliminates; verify pony removal, freed point, winner/loser localized reason and next match reset. Separately verify normal life depletion and respawn with blocks intact.
7. For every menu in en/ru/ja, test the six low and three high sizes listed above, populated lobby views, longest notices, result sides and open Settings dropdowns. Confirm the approved 104-unit notices fit alongside the controls/status and the Japanese Client label stays readable. Both exception changes have already passed the repeated automated sweep/build.
8. In the standalone player, manually handle Windows' permission prompt, authenticate, change windowed size between 800×600/1280×720 and 1920×1080, and verify live switching. Confirm high layouts and the gameplay HUD remain as before.

