# L1, L2/N31, N30, N17, L6, L7, L9, L10, N28 implementation

Date: 2026-10-20. Implementation baseline: `391e079` (the preceding task group). Unity: 6000.3.13f1. Task specifications describe the older `b7e7b91` baseline; their changes were applied to the current implementation after inspecting the intervening fixes.

## 1. Task summary

Implemented the requested countdown, three deck mixes, lobby-refresh throttling, host HUD disconnect correction, UI work caches, card locale subscriptions, release debug guard, specified legacy/dead-code cleanup and dynamic-font settings. The source implementation and focused checks are complete. Full multiplayer and Profiler acceptance checks remain for the developer; this report does not claim those passed.

No commit or push was made. Only the explicitly prescribed L10 `git rm` deletions are staged; implementation edits remain unstaged.

## 2. Files changed

All paths below are under `C:/Lesson/FallenStar-Spellcasters/`.

| Files | Purpose |
|---|---|
| `Assets/Scripts/InGame/GameMaster.cs`, `Assets/Scripts/InGame/GameMaster/GameMaster.GameLoop.cs` | L1 countdown, L9 guard and L10 listed dead code |
| `Assets/Scripts/InGame/GameMaster/GameMaster.Card.cs` | L2/N31 third deck mix and accurate comments |
| `Assets/Scripts/MultiplayerScripts/MultiplayerManager.cs` | N30 refresh coordination, L9 guard and L10 legacy disconnect cleanup |
| `Assets/Scripts/MultiplayerScripts/SessionManager.cs` | N17 own-disconnect HUD guard and L9 |
| `Assets/Scripts/UI/GameAnnouncerUI.cs` | L1 countdown display, L6 cached state/localization and unused VisualScripting import removal |
| `Assets/Scripts/Manager/CardWorldViewManager.cs`, `Assets/Scripts/UI/PlayerUI.cs`, `Assets/Scripts/UI/LobbyControlUI.cs` | L6 change-only work and L9 |
| `Assets/Scripts/InGame/CardViewBase.cs`, `Assets/Scripts/InGame/CardViewUI.cs`, `Assets/Scripts/InGame/CardViewWorld.cs` | L7 stored text data and balanced locale subscriptions; CardViewUI also gets L9 |
| `Assets/Scripts/DebugModeGuard.cs` and Unity-generated `.meta` | New L9 helper |
| `Assets/Scripts/InGame/BlockController.cs`, `Assets/Scripts/InGame/LavaKillZone.cs`, `Assets/Scripts/InGame/PlayerController.cs`, `Assets/Scripts/InGame/PlayerSession.cs`, `Assets/Scripts/InGame/ResultSceneController.cs`, `Assets/Scripts/MultiplayerScripts/AuthManager.cs`, `Assets/Scripts/UI/LeaveMatchUI.cs`, `Assets/Scripts/UI/LobbyListUI.cs`, `Assets/Scripts/UI/ResultMenuUI.cs` | Remaining L9 Awake guards; PlayerController also loses the unused echo queue/method |
| `Assets/Scripts/InGame/GameMaster/GameMaster.PackageManaging.cs`, `Assets/Scripts/InGame/GameMaster/GameMaster.Processing.cs` | L10 echo/dead-code removal and identical heal rounding without VisualScripting |
| `Packages/manifest.json`, `Packages/packages-lock.json` | Remove VisualScripting; Unity resolved the lockfile |
| `ProjectSettings/EditorBuildSettings.asset` | Remove only the two disabled legacy scene entries |
| `Assets/Fonts/MPLUSRounded1c-Medium SDF.asset`, `AGENTS.md` | N28 Inspector settings/clear and the prescribed font-cache commit rule |
| `Docs/AI/Architecture/Architecture.md`, `Docs/AI/Architecture/Dependencies.md`, `Docs/AI/Architecture/GameLoop.md` | Focused implementation updates; historical audit text is identified, with the broader N8 refresh still separate |
| This report | Implementation, evidence and pending acceptance checks |

L10 deletes exactly 81 tracked files, including metadata: the 61-file root `26.06.19` backup; `Assets/_Recovery/0.unity`, its metadata and folder metadata; `Assets/Scenes/SampleScene.unity` and `Assets/Scenes/MultiplayerScene.unity` with metadata; `Assets/Scripts/UI/SpawnPanelUI.cs`, `DisconnectMenuUI.cs`, `DisconnectButtonUI.cs`, `LocalizedTMPFormat.cs`; `Assets/Scripts/InGame/TargetDataBackup.cs`, `ToPlayerControlPackage.cs`, with all six script metadata files; and root `test.md`.

No enabled scene or prefab was edited. Runtime script count is now 53. The temporary Editor verification helper and its metadata were moved out of Assets into the ignored verification directory after use.

## 3. Important implementation details

### Countdown and deck construction

L1 replaces `m_timer` with private `m_phaseElapsed`/`m_phaseDuration` and the specified server-write/everyone-read double `m_phaseEndServerTime`. Existing server loop conditions and increments remain. Starts, bounded selection resets and respawn resume set deadlines; EndMatch, AbortMatch and StartNewGame clear them. Display reads use synchronized ServerTime, ceiling and zero clamp. All runtime references to the removed timer are gone.

L2/N31 changes only the random upper bound and the two summaries. All three targets are reachable, whole rarity sets remain unchanged, and today's database produces 84 cards: 20/32/32, 32/20/32 or 32/32/20.

### Lobby concurrency and disconnect context

N30 callers share a pending refresh or use the recent cache. A result is assigned only while its captured lobby ID is still current. RateLimited gets the requested warning; the existing generic exception handling remains. CurrentLobbyUI polling, heartbeat, queries and start validation are unchanged.

The task's instruction to reset the task/cooldown at transitions conflicted with its global single-request/1.5-second requirement. Before implementation, the owner approved: **“Yes, enforce the limit across lobby changes.”** Accordingly, the local cache/task resets, while a separate global task chain and last-request timestamp survive lobby changes. A new lobby refresh can wait for the previous request and remaining cooldown. Unity's main-thread synchronization context is retained.

N17 changes only SessionManager's HUD-clear condition: a remote disconnect does not clear the host's local HUD; an own-client disconnect does. Existing server forfeit handling, session removal and despawn order remain.

### UI, localization and release flags

L6 caches announcer phase, second, target presence/ID, local status, team and Locale, and reuses LocalizedString instances. Unchanged states skip string construction and mesh work. World-view registration prunes departed/unspawned/destroyed sessions with reusable scratch storage. The existing null branch of UnregisterSession could not remove a destroyed/null entry; this discrepancy was reported before coding and the branch now removes it from the registration set. Local HUD clearing runs once per lost binding; lobby panels apply only a changed connected state. LobbyControlUI's missing-manager Update fallback uses the existing singleton instead of a scene-wide search. LeaveMatchUI's sizing logic is unchanged.

L7 puts the stored CardData and balanced SelectedLocaleChanged subscription in CardViewBase. Derived ApplyTexts implementations contain the existing name/description reads; sprites and costs remain in Setup. The world-view null warning and OnDestroy network callbacks remain.

L9 puts DebugModeGuard.Allow as the first Awake statement in all 16 flag-owning classes, including before singleton early returns. Editor/development builds retain Inspector values; release builds force false. No flag use was found in OnValidate, serialization callbacks or static initializers. Existing ungated logs remain by design.

### Cleanup and font decision

L10 removes only the specified obsolete flow, echo RPC/DTO/queue, unused methods and files. Movement ownership/connected-client gates and debug HP handling remain. Both healing conversions use System.Convert.ToInt32, retaining midpoint-to-even behavior. The parked M11 join-by-code block was compared with HEAD and is unchanged; m_lobbyCode/m_lobbyCodePanel and their serialized references remain. The six enabled scenes and order remain Login, MainMenu, Settings, Lobby, Game, Result.

N28 was performed through the native Unity Inspector: Clear Dynamic Data On Build enabled, Clear Dynamic Data applied, Save Project. Source GUID `8dd8438c5656edf48800aa29265b27e9`, dynamic mode, 1024 atlas generation size, padding 11, render mode 4134 and multi-atlas setting were checked against HEAD and are unchanged. Final glyph and character tables are empty; only one 1×1 atlas remains.

The prescribed action retains TMP's generated font-feature table, which Unity also reserializes. When asked whether to use Clear Font Features too, the owner answered **“Keep the prescribed action only.”** Clear Font Features was not used. Consequently the asset remains 67,200 lines (1,890,667 bytes), rather than satisfying the specification's original expectation of a drastically smaller line count. This is an explicit owner-constrained exception, not an unreported pass of that acceptance criterion. Verification-generated glyph data was removed before completion. A final cleared snapshot is retained in the ignored verification directory. The Editor is left in an empty Untitled scene to avoid immediately regenerating glyph data.

Both peers must use the updated build: L1 changes NetworkVariable layout and L10 removes an RPC.

## 4. Tests actually executed and results

Evidence directory: `C:/Lesson/FallenStar-Spellcasters/build/task-l1-l2-n31-n30-n17-l6-l7-l9-l10-n28-verification/` (ignored).

| Check | Actual result and evidence |
|---|---|
| Roslyn runtime compilation with Editor defines | Exit 0, no diagnostics; `compile-editor.rsp`, `compile-editor-exit.txt` |
| Roslyn runtime compilation without UNITY_EDITOR or DEVELOPMENT_BUILD | Exit 0, no diagnostics; `compile-release.rsp`, `compile-release-exit.txt` |
| Production-source harness | 15 scenarios passed in each define configuration, 30 total scenario executions; `SourceHarness.cs`, `Tests.cs`, `results-editor.txt`, `results-release.txt` |
| Installed VisualScripting conversion versus replacement | All seven values identical; `RoundingCheck.cs`, `rounding-results.txt` |
| Unity package resolution and runtime/Editor compilation with NGO IL postprocessing | Succeeded after removing VisualScripting; final cleanup recompile also succeeded. `editor-final-cleanup.log` retains the successful cleanup build/reload. An existing third-party `Assets/JMO Assets/Welcome Screen/CFXR_WelcomeScreen.cs` CS0618 warning appeared during full import. An initial temporary helper used an inaccessible TMP property; that helper error was corrected before the successful checks/build and the helper is no longer under Assets. |
| Actual Editor scene audit | Opened all six enabled scenes and counted missing MonoBehaviour scripts throughout their hierarchies: zero in each; `editor-results.txt` |
| Actual Editor dynamic glyph generation | TryAddCharacters succeeded without missing glyphs for English, Russian and Japanese samples; `editor-results.txt`. This tests source-font coverage, not every rendered screen. |
| Actual release Windows player build | BuildOptions.None, StandaloneWindows64, all six scenes: **Succeeded; errors=0; warnings=0**; `build-results.txt`, `WindowsPlayer/FallenStar.exe` |
| Release player visual smoke check | Launched the actual executable. Owner performed login. English login/main menu/Settings and Japanese/Russian Settings, dropdown options, main menu and lobby-search labels rendered without missing-glyph boxes. Restored English and closed the test player. Its captured log has zero exception lines and zero missing-glyph warnings; `player-menu-smoke-results.txt`, `player-menu-smoke.log`. Full match/HUD/card/result rendering is not covered by this smoke check. |
| Static/diff review | Both staged and unstaged diff whitespace checks passed. Verified all 16 first-statement guards, 53 runtime sources, 81 prescribed staged deletions, no deleted symbols/legacy scene names in remaining runtime scripts/scenes/prefabs, unchanged parked M11 block, no enabled-scene/prefab modifications, and unchanged font generation/source settings. |

Harness coverage includes countdown start/resume/expiry/no-manager and 10,000 read calls without deadline writes; 300 actual deck builds per configuration; overlap/cached/stale/transition/rate-limit/error refresh cases; own versus remote disconnect; each announcer state input/locale/formatting and 10,000 unchanged warmed calls (zero bytes allocated with doubles); removed world sessions; one-time HUD clearing; panel transitions; balanced base locale events; and release/Editor debug behavior. Unity, NGO and Lobby dependencies are doubled in the source harness; this is not a live networking or Unity Profiler measurement.

The 300-deck distributions in each harness run were 92 × 20/32/32, 117 × 32/20/32 and 91 × 32/32/20; every deck had 84 cards.

Actual conversion output before package removal:

```text
PASS 0: ConvertTo<int>=0, Convert.ToInt32=0
PASS 0.5: ConvertTo<int>=0, Convert.ToInt32=0
PASS 1: ConvertTo<int>=1, Convert.ToInt32=1
PASS 1.5: ConvertTo<int>=2, Convert.ToInt32=2
PASS 2.5: ConvertTo<int>=2, Convert.ToInt32=2
PASS 12.5: ConvertTo<int>=12, Convert.ToInt32=12
PASS -0.5: ConvertTo<int>=0, Convert.ToInt32=0
```

## 5. Remaining risks

- No full two-peer Play Mode match, real Lobby request counter trace or ten lobby/Start cycles was executed. Throttling addresses the suspected request burst; it does not prove the SDK's original internal NullReferenceException can never occur for another HTTP failure.
- No Unity CPU/Network Profiler capture was taken. The source harness's allocation/write checks support the implementation but do not replace those acceptance checks.
- Card locale changes, respawn countdown continuity, bounded selection resets, host HUD retention through each forfeit phase, live heal overflow, and rendered match/card/result glyph coverage remain manual checks.
- N28's retained feature cache keeps the YAML large by the owner's explicit choice. Dynamic glyphs will regenerate when normal scenes render; preserve the deliberately cleared task state before accepting this change.
- The release executable was built before final whitespace-only cleanup; no production behavior changed after its build. The final Editor compilation after cleanup succeeded (Tundra 2.54 seconds, successful assembly reload, no compiler errors/warnings in that compilation).

## 6. Manual Unity Editor verification steps

No new Inspector references or prefab setup are required. N30's serialized interval defaults to 1.5 seconds. The N28 flag is already enabled.

1. Open LoginScene in the main Editor and a matching updated peer. Run a two-player match through break, deal, selection and both turns. Compare countdowns within ±1 second; check a respawn resumes the remaining interrupted duration. Exercise the existing over-budget reset test without committing its temporary client changes.
2. Use CPU/Network Profiler during an unchanged selection second: no announcer allocation/mesh rework each frame and no per-frame deadline writes. Disconnect the opponent and verify departed world registrations stop scene searches and the host HUD remains until ResultScene.
3. Repeat create/join/start/leave cycles five times with Editor host and five with release-player host. Check both logs for refresh failures/SDK exceptions and confirm the count/Start button follows joins/leaves within about two seconds. A temporary SDK-call counter should show one active call and at least 1.5 seconds between starts, including across lobby changes.
4. With hand and board cards visible, switch en → ru → ja using Localization Scene Controls. Names/descriptions and announcer text should update; destroy/play cards and switch again without MissingReferenceException.
5. Check movement, heal overflow, leave/forfeit and result flow. Confirm all three 84-card mixes occur. In a release match, verify gated debug lines are absent; Editor/development flags should still work.
6. Finish N28 visual coverage in Editor and player for Japanese/Russian match HUD, cards and Result. Menus/Settings/lobby-search were already checked in the release player. After Editor checks repopulate the dynamic font, restore the deliberately cleared snapshot for this task rather than restoring pre-task HEAD (which would remove the new flag). Keep source/settings and the feature cache as approved.
