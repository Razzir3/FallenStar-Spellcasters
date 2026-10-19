# M16 and N1 implementation: singleton destruction and NGO cleanup

## 1. Task summary

Implemented `TASK-M16_ClearSessionManagerInstance.md` and `TASK-N1_PlayerUIOnDestroyOverride.md` against `a0df515`. The working tree was clean at the start.

SessionManager and PlayerUI now clear their own singleton reference on destruction and invoke NGO's base cleanup. SessionManager also unsubscribes its session-list handler on despawn when NetworkManager is already absent.

## 2. Files changed

- `Assets/Scripts/MultiplayerScripts/SessionManager.cs`.
- `Assets/Scripts/UI/PlayerUI.cs`.
- `Docs/AI/Architecture/Dependencies.md`.
- This report.

No scene, prefab, asset, serialized-field, ownership, package or project-setting changes. No commit, push, reset or discard was performed.

## 3. Important implementation details

- Confirmed the installed NGO 2.11.2 `NetworkBehaviour.OnDestroy` is public virtual. Both overrides use `public override` and unconditionally call `base.OnDestroy()` after the singleton check. This retains NGO behaviour removal and NetworkVariable disposal.
- `Instance` is set to actual C# null only when `Instance == this`. Destroying a duplicate therefore leaves the active singleton intact. Existing Awake duplicate handling is unchanged.
- SessionManager retains its singleton on network despawn; it is cleared only on destruction. Moving `SessionDataList.OnListChanged -= OnSessionListChanged` before the missing-NetworkManager guard makes that unsubscribe unconditional. Existing NetworkManager callback unsubscribes retain their guard.
- No other PlayerUI code changed. Existing scene transitions, session creation, list rebuilding, ownership and UI event lifetimes are retained. These destruction overrides apply locally on both host and client.
- Required Inspector setup: none.

## 4. Tests actually executed and results

- Compiled all runtime C# with Unity 6000.3.13f1's bundled Roslyn compiler and the current Assembly-CSharp response-file references, defines and analyzers. Exit 0; no errors and no CS0114. Only the existing CS0414 (`GameMaster.m_cardResolveDelay`) warning remains. Output was redirected to ignored `build/task-m16-n1-verification/`; evidence includes `compile.rsp`, `compile-results.txt` and `compile-exit.txt`.
- The open Unity Editor imported/compiled the modified scripts. Its log confirms Netcode IL postprocessing, `Tundra build success` and successful assembly reload. The current compilation emits only CS0414; older historical CS0114 lines remain earlier in the log. Selected current evidence is saved as `editor-compile-evidence.txt` in the verification directory.
- Reviewed the final Git diff: SessionManager has only the specified override/unsubscribe move; PlayerUI has only the OnDestroy signature/base call. Lifecycle documentation matches the change. `git diff --check` passed.
- No automated test harness was added for these small lifecycle edits. Compilation and static diff/lifecycle review were executed; actual singleton destruction and two consecutive matches were not exercised as a new Play Mode test.

## 5. Remaining risks / checks not executed

- No two-peer match/leave/restart regression, runtime duplicate-instance destruction check, `ReferenceEquals` inspection after GameScene unload or player build was run for these tasks.
- Compilation/import verifies the override signatures and removes the hiding warning; it does not prove real NGO destruction/scene-unload ordering. Existing unrelated lifecycle/subscription behavior remains unchanged.

## 6. Manual Unity Editor verification

1. Confirm a fresh Unity compile shows no CS0114 or SessionManager warning; the existing GameMaster CS0414 may remain.
2. Run ParrelSync host/client, end or leave a match, return through ResultScene to the main menu, and start/end a second match in the same Play session. Check sessions and HUD initialize normally and the Console has no new cleanup errors.
3. Optionally inspect or temporarily log `ReferenceEquals(SessionManager.Instance, null)` after GameScene has unloaded; it must be true. Check PlayerUI's reference likewise. Remove any temporary diagnostic afterward.
4. Optionally destroy a duplicate of either component and verify the real singleton remains. For a live SessionManager despawn without destruction, its singleton should remain; destruction should clear it and run base cleanup. Finish with a player-build check.
