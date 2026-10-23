# N32–N35 and N54–N58 implementation

Date: 2026-10-22. Baseline: `6d89a09` (`N50_N52`).

## 1. Task summary

Implemented all three specifications found in `Docs/AI/Tasks/`, in their prescribed order:

1. `TASK-N54_N56_N57_BoardClickAndHudButtons.md`: board-card raycasts and safe HUD button handling.
2. `TASK-N55_N58_N32_ServerGuards.md`: spawn failure handling, bounded target packets and removal of the unused unsafe owner lookup.
3. `TASK-N33_N34_N35_CodeHygiene.md`: listed dead members/assets, release logging and change-only HP/lives text.

The folder was scanned again after implementation and verification: the same three specification hashes remained, with no additional or changed task. Evidence: `build/task-folder-verification/final-task-hashes.json` and `final-task-scan.txt`.

The owner's existing GameLoop input-table correction and existing specification removals were preserved. Issue Report #12 and the three new specifications remain untouched. No commit or push was made.

## 2. Files changed

| Area | Files |
|---|---|
| World-card input | `Assets/Prefab/BoardPrefab/CardWorldViewPrefab.prefab` |
| HUD safety, logging and caching | `Assets/Scripts/UI/PlayerUI.cs`, `ConfirmTargetButtonUI.cs`, `ConfirmSendButton.cs` |
| Spawn and packet guards | `Assets/Scripts/InGame/GameMaster/GameMaster.Spawn.cs`, `GameMaster.PackageManaging.cs`, `GameMaster.Processing.cs`, `Assets/Scripts/InGame/CardControllPackage.cs`, `Assets/Scripts/MultiplayerScripts/MultiplayerManager.cs` |
| Remaining prescribed hygiene | `Assets/Scripts/Bootstrap.cs`, `GameSettingsPrefs.cs`; `Assets/Scripts/InGame/BlockController.cs`, `CardPreviewController.cs`, `CardViewUI.cs`, `GameMaster.cs`, `GameMaster/GameMaster.GameLoop.cs`, `NetworkCard.cs`, `PlayerController.cs`, `PlayerSession.cs`, `PonyColorScript.cs`; `Assets/Scripts/Manager/CardWorldViewManager.cs`, `SceneFlowManager.cs`; `Assets/Scripts/UI/DisplaySettingsUI.cs` |
| Stale override | `Assets/Scenes/GameScene.unity` — exactly the four specified YAML lines |
| Deleted unused assets | `Assets/Prefab/BootsTrap.prefab`, `Assets/Prefab/MultiplayerManager.prefab`, and their two `.meta` files |
| Documentation | `Docs/AI/Architecture/Architecture.md`, `Dependencies.md`, `GameLoop.md`, and this report |

All temporary verification scripts, probes, baseline prefab and their metas were removed from `Assets`; copies and logs remain in ignored `build/`. The dynamic font matches HEAD (`abf6953576960933ce8d2f9c8da970fc8f3fc8f3`). `Assets/DefaultNetworkPrefabs.asset` is unchanged.

## 3. Important implementation details

- **N54:** removed the world canvas's `GraphicRaycaster` in Unity Prefab Mode and disabled the three TMP raycast targets. A canonical YAML comparison proves those are the only prefab changes. Canvas, CanvasScaler, text content/settings, collider, camera and table transforms are unchanged.
- **N56/N57:** Confirm and Send look up a local session safely and return with one warning per press if it is unavailable. `PlayerUI.ConfirmTurn` uses a local variable; it never binds `m_playerSession`, so the normal `Update` path still creates the hand and subscribes to its lists.
- **N55:** server-only `SpawnPlayers` now returns `bool`. It checks all player-team spawn points before spawning anything. The startup coroutine aborts on `false` with `spawn_failed`, or logs an exception and aborts with `match_start_exception`. The secondary `CreateBlock` guard returns. Character/respawn/spawn-point selection behavior is unchanged.
- **N58:** `MaxTargets = 4`; the reader rejects negative or oversized counts before target allocation/reads. The server handler rejects null, empty or oversized lists after the existing active-player/sender checks. The honest writer, four-slot values, target accessors and activity rules are unchanged.
- **N32:** removed only the unused owner-character lookup from activity processing; N34 separately gates its existing trace.
- **N33:** removed the listed unused members, three NetworkCard RPCs, the unused death NetworkVariable, two unreferenced legacy prefabs and stale HUD override. Both prefab GUIDs were searched before deletion and have no remaining references. `m_knowLobbies`, spectator support and both W3 AuthManager methods remain.
- **N34:** handled every numbered chatter statement: **25 gated and 5 deleted**. The specification's prose says 31, but its numbered table contains 30. No additional debug flag was added; warnings/errors and lobby-filter `continue` behavior are preserved. The three specified interpolations were corrected. Null card-view guards precede name logging.
- **N35:** independent HP/lives comparisons avoid repeated formatting. Dirty/dead-state flags handle first display, death, respawn, enable, locale changes and local cleanup. The dead branch still clears selection every frame. Missing-character states retain the last displayed text.
- Remaining serialized names and Inspector bindings are preserved. Harmless orphan YAML for removed fields was not resaved, including `PonyBase`'s former `m_isDead` data. This follows the explicit member deletion and minimal-asset-change requirements despite the hygiene spec's abbreviated reference to only two removed serialized fields.
- Both multiplayer peers must use the updated build because N33 removes a NetworkVariable and RPCs. No Inspector setup is required.

## 4. Tests actually executed and results

Unity version: **6000.3.13f1**. Final batch Play Mode suite: **passed**, with an empty `captured-errors.txt`. Deliberately triggered guard warnings/errors are retained separately; this is not a claim of an entirely warning-free fixture log.

| Check | Actual result |
|---|---|
| N54 baseline reproduction | Team 1: UI hits at all four tested points (card centre and three text centres). Team 0: zero UI-hit points. Both: all four physics hits. |
| N54 edited real prefab | Both teams: zero UI hits, all four physics hits; three enabled, populated text labels. Real HUD hand/Confirm raycasts still block. |
| Native Editor mouse input | Actual `PlayerSession.Update` opened card 23 from right-hand text and card 22 from the left card, both under `CardViewDock`, with `overUI=False`. Hand and Confirm gave `overUI=True`. Japanese table/dock art and text inspected. |
| N56 | No manager and no local player: exactly one warning per button press, no exception. Live session: cache and existing handlers exercised. |
| N57 | Early ConfirmTurn leaves cache null; later Update binds, creates the row and responds to a live list-change event. |
| N34 null arguments | Both PlayerUI click handlers accept null without throwing. |
| N35 | 300 actual unchanged frames: one HP write and one lives write including initial display. Same-frame changes; English/Russian/Japanese locale callbacks; dead text once; missing-character retention; same-value respawn and enable invalidation passed. |
| N58 buffer reader | Counts −1, 5 and 1,000,000: empty list, capacity zero, reader position 12, no exception. Counts 0–4: exact values and expected byte positions 12–44. Honest four-target round-trip passed. |
| N58 handler | Null, zero and five targets: false plus one count warning each. Four targets accepted. |
| N55 Play Mode preflight | Real spawn-point prefab with team 1 missing: false, no spawned objects/blocks. Secondary CreateBlock guard returns safely. |
| N55 coroutine/source harness | Actual source methods compiled with collaborator doubles: missing spawn aborts without StartNewGame; spawn and StartNewGame exceptions each log and abort; normal grid creates/registers 25 blocks per session, invokes two character spawns, binds areas and starts once. This is not a real two-client NGO spawn test. |
| N32 card-resolution/source harness | Actual ProcessActivity source: Damage with absent owner session, character Heal/shield, block Heal/shield and DrawCard twice passed. Collaborator doubles were used. |
| N33/N34 static audit | All specified deleted code identifiers absent; retained lobby/auth members present; all 25 gates and 5 deletions checked; all three interpolation expressions checked; prefab GUID search empty; exact scene/prefab delta verified. |
| Editor debug-on sample | Real card clicks emitted both CardViewUI and PlayerUI traces. Block message printed `HP changed 7 to 9`; character message printed `Spawned. Owner :False ClientId : 0`. |
| Release Windows build | **Succeeded; 0 errors; 2 build warnings**, `BuildOptions.None`, StandaloneWindows64. No C# compiler errors/warnings found in the build log. |
| Release startup smoke | Launched built executable, inspected LoginScene rendering, closed normally. No enumerated chatter strings or game-code exception in startup Player.log. Authenticated lobby search/full match were not executed. |
| Final repository checks | `git diff --check` passed; production diff inspected; no temporary Assets helpers or unintended font/network-prefab changes. |

The third interpolation is in HandleConfirmTurn's default switch branch, which current `IsConfirmForCurrentPhase` rejects before it can be reached. Its source expression was checked; it was not represented as a runtime test. The other gated match/lobby lines were checked statically, with representative card logs exercised live.

The two reported build warnings concern the unlinked Unity Services project and missing Unity Cloud token for native-symbol upload. The build's earlier Addressables import also printed its existing empty-profile-id diagnostic. No project setting was changed to suppress these.

Evidence, relative to the repository root:

- `build/task-folder-verification/play-results.txt`, `captured-errors.txt`, `warnings.txt`, `play-editor.log`
- `build/task-folder-verification/native-clicks.txt`, `native-editor.log`, `native-ready.txt`
- `build/task-folder-verification/build-results.txt`, `build-editor.log`, `release-player.log`, `release-smoke.txt`, `cleanup.txt`
- `build/task-folder-verification/WindowsPlayer/FallenStar.exe` and sibling player data
- `build/task-folder-verification/EditorTools/` — final temporary fixture sources and baseline prefab
- `build/n55-verification/source_checks.py`, `run-source.ps1`, `source-results.txt`
- `build/hyg2-verification/audit.py`, `static-results.txt`
- Task-specific pre-change copies in `build/n54-verification/baseline/`, `build/n55-verification/baseline/`, `build/hyg2-verification/baseline/`

Earlier harness-development logs are retained in numbered run directories. The final root results/logs above are the passing run.

## 5. Remaining risks

The real two-Editor match, authenticated lobby query and full-match release-log acceptance checks require owner verification. Automated network fixtures use synthetic sessions, and the normal-spawn/card-resolution source harness uses explicit doubles; these do not prove real transport/ownership/scene-transition integration. The release smoke covers startup only.

No Inspector rewiring is needed. Do not mix this build with the previous peer build. Parked N27 material-index warnings and W3 authentication cleanup remain outside scope.

## 6. Manual Unity Editor verification steps

1. Open both updated Editors; launch the ParrelSync clone with its own `-logFile C:\Lesson\clone-editor.log`. Host creates the lobby, clone joins, start a match.
2. In both windows, during Player action click each side's table cards at text, centre and art. Each opens its big dock card. Verify unchanged table appearance.
3. Select the played card in the bottom HUD, select its board target, then Confirm and Send. Check hand/button clicks do not also select board objects.
4. During client loading, press host Confirm turn several times before the phase banner appears. The selection hand must populate normally when the match starts.
5. Play through Damage, Heal and DrawCard, take damage, die, respawn and finish. Check HP/lives through every state; switch English/Russian/Japanese while alive and dead.
6. Refresh the lobby list several times, create/join/start again. Inspect both logs for new game-code exceptions or unexpected guard warnings.
7. Repeat one full match and lobby search in the non-development player; inspect Player.log for the N34 quoted messages, including card clicks, Process called, DespawnBlock, RepairCalled, SetLobbyCode and all lobby-search chatter.
8. Optional failure test: in an unsaved scratch GameScene, disable team 1's scene spawn point. Starting must return both players to the lobby with `spawn_failed`, without half-spawning the match. Close the scratch scene without saving.
