# TASK-M16: Clear `SessionManager.Instance` when the object is destroyed

- **Source issue:** [Issue Report #6 → M16](../Reports/2026-10-19_IssueReport-6.md#-medium-13) (first reported in #1)
- **Severity:** 🟡 Medium (Plausible)
- **Baseline:** `fe2d4fb`. Line numbers below are from this commit.
- **Order:** 10 of 13. Independent of the other tasks; same pattern as N1.
- **Implementer:** Codex

## 1. Objective

`SessionManager.Instance` never points to a destroyed object, and `SessionManager` always runs NGO's base `OnDestroy`.

## 2. Current behavior

`Assets/Scripts/MultiplayerScripts/SessionManager.cs`:
- `Awake` sets `Instance = this` (`:56`). A duplicate destroys itself (`:44-48`).
- There's no `OnDestroy`, so `Instance` is never cleared. `SessionManager` is an in-scene object in `GameScene` (prefab instance at `GameScene.unity:1788`). When `GameScene` unloads, the object is destroyed, but the static still references it.
- Unity's overloaded `== null` hides most of the effect, because existing checks like `SessionManager.Instance == null` return true for a destroyed object. A plain C# null check (`is null`, `ReferenceEquals`, `?.`) or a read in the same frame as the destroy would still see the stale object.
- `OnNetworkDespawn` (`:132-143`) returns early when `NetworkManager.Singleton` is null, so `SessionDataList.OnListChanged -= OnSessionListChanged` is skipped in that case.

**NGO base:** `NetworkBehaviour.OnDestroy()` is `public virtual` (NGO 2.11.2, `NetworkBehaviour.cs:1675`). It disposes NetworkVariables such as `SessionDataList`. `SessionManager` doesn't define `OnDestroy` today, so the base already runs. An override must call it.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/MultiplayerScripts/SessionManager.cs` | Add an `OnDestroy` override; move one unsubscribe before the early return |

## 4. Required changes

1. Add, in the `UnityCallingMethods` region:
   ```csharp
   public override void OnDestroy()
   {
       if (Instance == this)
       {
           Instance = null;
       }

       base.OnDestroy();
   }
   ```
   Use `public override`. The base is `public virtual`, so `protected` wouldn't compile (CS0507).
2. In `OnNetworkDespawn`, move `SessionDataList.OnListChanged -= OnSessionListChanged;` above the `if (!NetworkManager.Singleton) return;` guard, so it always runs.
3. Don't clear `Instance` in `OnNetworkDespawn`. The object can still be alive after a despawn, and `Awake` wouldn't set it again.

## 5. Explicit non-goals

- Changing the duplicate-instance handling in `Awake`.
- Other singletons: `GameMaster` clears in `OnNetworkDespawn`; `PlayerUI` is N1.
- N2 (NetworkVariables written before spawn).

## 6. Constraints and compatibility requirements

- No public API changes besides the override. No asset changes.
- A duplicate that destroys itself in `Awake` must not clear the real `Instance` (the `Instance == this` check handles this).

## 7. Acceptance criteria

- [ ] After `GameScene` unloads, `SessionManager.Instance` is really null, not only Unity-null.
- [ ] `base.OnDestroy()` is called.
- [ ] Two matches in a row in the same Play session work. No errors.
- [ ] Compiles with no new warnings, and no CS0114 for `SessionManager`.

## 8. Regression tests

No automated tests. Review the diff.

## 9. Manual Unity Editor checks

1. ParrelSync host + client. Play a match to the end, then result screen → main menu → lobby → a new match → end. No errors or warnings mentioning `SessionManager`.
2. **Optional, temporary log (don't commit):** in `SceneFlowManager.LoadMainMenuScene`, log `ReferenceEquals(SessionManager.Instance, null)`. It should print `True` after a match.

## 10. Open questions and assumptions

- **A1:** No current code path is known to fail because of the stale reference. This removes a latent trap.
