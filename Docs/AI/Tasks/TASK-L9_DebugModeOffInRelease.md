# TASK-L9: `m_debugMode` is always off in release player builds

- **Source issue:** [Issue Report #7 → L9](../Reports/2026-10-20_IssueReport-7.md#-low-23)
- **Severity:** 🟢 Low (Confirmed)
- **Baseline:** `b7e7b91`
- **Order:** 17 of 20. Independent. It touches many files, so run it after the gameplay tasks to avoid merge noise.
- **Owner decision (2026-10-20):** **force off in release builds.** Keep the Inspector flags for Editor work; code turns `m_debugMode` off in non-development player builds.
- **Implementer:** Codex

## 1. Objective

In a player build **without** "Development Build", every `m_debugMode` flag is `false` at runtime, whatever the Inspector says. In the Editor and in development builds, the flags work as today.

## 2. Current behavior

- 16 classes declare a `m_debugMode` bool (public or `[SerializeField]`):
  - `InGame/`: `BlockController`, `CardViewUI`, `GameMaster`, `LavaKillZone`, `PlayerController`, `PlayerSession`, `ResultSceneController`
  - `Manager/CardWorldViewManager`
  - `MultiplayerScripts/`: `AuthManager`, `MultiplayerManager`, `SessionManager`
  - `UI/`: `LeaveMatchUI`, `LobbyControlUI`, `LobbyListUI`, `PlayerUI`, `ResultMenuUI`
- No code writes the flag at runtime (grep).
- **`m_debugMode: 1`** is set in:
  - `CardWorldManagerPrefab`, `GameMasterPrefab`, `MultiplayerManager.prefab`, `PlayerSessionPrefab`, `SessionManager.prefab`
  - `ResultScene.unity`
  - the legacy `SampleScene.unity` and `Assets/_Recovery/0.unity`, which TASK-L10 deletes
- **Effect in a release build:**
  - verbose logs (for example "No Character", "Player session initialized…", "Round break phase started" in the owner's `Player.log`)
  - the debug paths behind the flag
- The HP debug key itself is compiled only under `#if UNITY_EDITOR || DEVELOPMENT_BUILD` (`PlayerController.cs:184-189`, `GameMaster.Processing.cs:77-82`), so it's already absent from release builds.
- Of the 16 classes, 10 have an `Awake`. `BlockController`, `CardViewUI`, `ResultSceneController`, `MultiplayerManager`, `LeaveMatchUI` and `ResultMenuUI` don't.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/DebugModeGuard.cs` (new, next to `GameSettingsPrefs.cs`) | A static helper |
| The 16 classes above | Call it once, at the start of `Awake` |

## 4. Required changes

1. **New `Assets/Scripts/DebugModeGuard.cs`:**
   ```csharp
   public static class DebugModeGuard
   {
       // Release player builds never run debug paths, whatever the Inspector says (L9).
       public static bool Allow(bool inspectorValue)
       {
   #if UNITY_EDITOR || DEVELOPMENT_BUILD
           return inspectorValue;
   #else
           return false;
   #endif
       }
   }
   ```
2. **In each of the 16 classes,** the first statement of `Awake()` becomes `m_debugMode = DebugModeGuard.Allow(m_debugMode);`.
   - If the class has no `Awake`, add a `private void Awake()` with just that line. For a `NetworkBehaviour`, a plain `Awake` is fine.
   - If a class's existing `Awake` returns early (a singleton duplicate check), put the line **before** the early return.
3. Grep for any `m_debugMode` use that could run before `Awake` (for example `OnValidate` or a static method), and list it in the report.

## 5. Explicit non-goals

- Don't change any `m_debugMode` value in prefabs or scenes. The owner keeps the Inspector flags.
- No change to what the debug paths do.
- No logging framework.

## 6. Constraints and compatibility requirements

- Keep the field names and the serialization (`public` / `[SerializeField]`) as they are.
- A disabled component's `Awake` still runs when its GameObject is active. For an object that starts inactive, `Awake` runs on its first activation, before any `Update`, so the guard still comes first.
- Uses only standard Unity scripting defines.

## 7. Acceptance criteria

1. In a **release** player build (Development Build off), none of the `m_debugMode`-gated log lines appear in `Player.log` during a full match. Examples: "Round break phase started", "Player session initialized for client", "Session created for client". Some lines aren't gated and stay, for example "DespawnBlock" and "DrawPhase started".
2. In a **development** build and in the Editor, the same lines appear as before.
3. The game behaves the same in all three.

## 8. Regression tests

- Roslyn compile of the runtime sources twice: once with the default defines, once with `DEVELOPMENT_BUILD` removed and `UNITY_EDITOR` undefined (a player-like define set, if the harness supports it). Both exit 0.

## 9. Manual Unity Editor checks

1. Editor, two players: the Console shows the usual debug lines.
2. Build Profiles → Windows, **Development Build off** → Build → run one match (exe + Editor). `Player.log` has no `m_debugMode` lines: the build's own startup lines, errors and warnings only. "Invalid mane material index." stays, because it isn't gated (N27 is parked).
3. Optional: a **Development Build** shows the debug lines again.

## 10. Open questions and assumptions

- Assumption: no class needs its debug output in release builds for support purposes. If one does, that's a later decision.
