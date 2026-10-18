# TASK-C5: Remove Editor-only and test-only namespace imports from runtime scripts

- **Source issue:** [Issue Report C5](../Reports/2026-10-18_IssueReport.md#-critical) (also unknown U1)
- **Severity:** 🔴 Critical (suspected build blocker)
- **Recommended order:** **C5 first**, because none of the other fixes can be verified in a player build until this passes.
- **Implementer:** Codex

## 1. Objective

Make sure the runtime assembly (`Assembly-CSharp`) has no compile-time dependency on `UnityEditor.*` or NUnit, so a standalone player build compiles.

## 2. Current behavior

Verified by grep over `Assets/Scripts`:

| File | Line | Import | Used? |
|---|---|---|---|
| `Assets/Scripts/InGame/CardPreviewController.cs` | 1 | `using UnityEditor.Localization.Plugins.XLIFF.V20;` | **No.** No type from that namespace is referenced in the file. |
| `Assets/Scripts/InGame/CardControllPackage.cs` | 1 | `using NUnit.Framework;` | **No.** No `Assert`, `TestCase` or other NUnit type is referenced. |

- `Assets/Scripts` has no `.asmdef`, so both files compile into `Assembly-CSharp`.
- In the Editor this compiles, because Editor and test-framework assemblies are referenced (`Assembly-CSharp.csproj` references `nunit.framework.dll`).
- In a **player build**, `UnityEditor.*` assemblies aren't available, and NUnit is only referenced when test assemblies are included. The expected result is errors CS0234 or CS0246 and a failed build.
- Not verified by an actual build during the audit.

Other `UnityEditor` usage in runtime scripts was checked:
- `PlayerController.cs:4` has the commented-out `//using UnityEditor.Search;`. Harmless.
- `AuthManager.ApplyEditorProfile`, `CardDatabase.OnValidate`, `LocalizedTMPFormat.OnValidate` and `PlayerCardWorldArea.OnDrawGizmosSelected` are already inside `#if UNITY_EDITOR`.

## 3. Relevant files and dependencies

- `Assets/Scripts/InGame/CardPreviewController.cs` (used by `PlayerSessionPrefab.prefab`)
- `Assets/Scripts/InGame/CardControllPackage.cs` (network payload used by `PlayerSession.CardInputServerRpc` and `GameMaster.HandleCardControlPackage`)
- `Packages/manifest.json`: `com.unity.test-framework` 1.6.0, `com.unity.localization` 1.5.11

## 4. Required changes

1. Delete line 1 of `CardPreviewController.cs` (`using UnityEditor.Localization.Plugins.XLIFF.V20;`).
2. Delete line 1 of `CardControllPackage.cs` (`using NUnit.Framework;`).
3. Re-grep `Assets/Scripts` (excluding any `Editor/` folders; currently none exist) for:
   - `using UnityEditor`
   - `using NUnit`
   - `using UnityEngine.TestTools`

   Any other hit that isn't inside `#if UNITY_EDITOR` must be reported in the implementation summary. **Don't** fix additional hits silently; report them.
4. No other code changes.

## 5. Explicit non-goals

- Fixing the other `CardPreviewController` issues: `FindFirstObjectByType<Canvas>()` in `Start`, and debug logs.
- Removing the `Unity.VisualScripting` usages. That's a runtime package, so it's not a build blocker.
- Removing `using Unity.Services.Matchmaker.Models;` from `CardData.cs`. It's a runtime package (part of `com.unity.services.multiplayer`); report it only if the build complains.
- Changing build settings, scenes or player settings.

## 6. Constraints and compatibility

- Don't touch `.meta` files or GUIDs. Only the file contents change.
- No renames.
- The `CardControllPackage` serialization must stay byte-identical. Only the `using` line goes.

## 7. Acceptance criteria

- [ ] Both `using` lines are removed, and no other line in those files is changed.
- [ ] The Editor recompiles with 0 errors.
- [ ] A **Windows standalone development build** of the 4 enabled scenes completes without compile errors.
- [ ] A grep report shows no remaining unguarded `UnityEditor`/`NUnit` imports in `Assets/Scripts`, or lists them if any are found.

## 8. Regression tests

There are no automated tests. The build itself is the regression test. The implementation summary must include the build log excerpt, showing success or the exact error lines.

## 9. Manual Unity Editor checks

1. Open the project and wait for the compile. The Console must be clean.
2. File → Build Profiles → Windows, Development Build ON → Build to a folder outside the repo, or to `build/` if the team uses that (it's ignored by `.gitignore`; verify).
3. Launch the build: Login → Main Menu → Lobby loads.
4. Optional: host in the build and join from the Editor (ParrelSync not needed), then start a match and hover a world card. The preview (`CardPreviewController.ShowPreview`) still appears.

## 10. Open questions and assumptions

- **A1:** No other Editor-only types are referenced indirectly (e.g. through a type from that namespace). The file reading supports this. If the build reveals more errors, Codex must report them and stop, not widen the scope.
- **Q1:** is there a CI or build pipeline that should run this build automatically? None was found in the repo.
