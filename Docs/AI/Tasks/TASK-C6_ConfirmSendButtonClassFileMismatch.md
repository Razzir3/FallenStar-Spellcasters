# TASK-C6: Make the cast button component loadable (class/file name mismatch)

- **Source issue:** [Issue Report C6](../Reports/2026-10-18_IssueReport.md#-critical)
- **Severity:** 🔴 Critical, if confirmed (cards could never be cast)
- **Recommended order:** C5 → **C6** → C1 → … It's a prerequisite for testing the card-cast flow in C4 and later tasks.
- **Implementer:** Codex. **Step 1 is verification only. Step 2 needs owner approval** (see §10).

## 1. Objective

Make sure the "send/cast card" button in `PlayerUIPrefab` has a loadable `MonoBehaviour`, so clicking it reaches `PlayerSession.TryHandlePackage()`.

## 2. Current behavior

Verified statically:

| Item | Value |
|---|---|
| File | `Assets/Scripts/UI/ConfirmSendButton.cs` |
| Class declared | `public class ConfirmSendButtonUI1 : MonoBehaviour` (same in the committed `HEAD` version) |
| Script GUID (`.meta`) | `f789bb4cc4354b34b84b684f578c1139` |
| Prefab usage | `Assets/Prefab/UiPrefab/PlayerUIPrefab.prefab`: component `&2456797024511787503`, `m_Script` → that GUID, `m_EditorClassIdentifier: Assembly-CSharp::ConfirmButtonUI1` (stale name) |
| Button binding | `onClick` → `m_Target: 2456797024511787503`, `m_MethodName: TryConfirmSend`, `m_TargetAssemblyTypeName: ConfirmSendButtonUI1, Assembly-CSharp` |
| Call chain | `ConfirmSendButtonUI1.TryConfirmSend` → `SessionManager.Instance.LocalPlayer.TryHandlePackage()` → `PlayerSession.CardInputServerRpc` |

Unity resolves a `MonoBehaviour` through the `MonoScript` of the file, and it expects a class whose name matches the file name (`ConfirmSendButton`). Because the names differ, the component **probably** shows as *"The associated script can not be loaded"* / *"Missing (Mono Script)"*. The button would then do nothing, and no card could ever be cast.

**Not confirmed:** the Editor was not opened during the audit.

Related observation (for reporting only, not to fix here): the neighbouring target button's `onClick` has `m_TargetAssemblyTypeName: ConfirmButtonUI`, but the class is `ConfirmTargetButtonUI` (`ConfirmTargetButtonUI.cs`, GUID `621e07a3…`). The `m_Target` reference is correct.

## 3. Relevant files and dependencies

- `Assets/Scripts/UI/ConfirmSendButton.cs` and `ConfirmSendButton.cs.meta`
- `Assets/Prefab/UiPrefab/PlayerUIPrefab.prefab` (in-scene instance in `Assets/Scenes/GameScene.unity`)
- `Assets/Scripts/InGame/PlayerSession.cs`: `TryHandlePackage`, `CheckPackage`, `CardInputServerRpc`
- `AGENTS.md` rules: *don't rename classes or variables*, *don't delete or recreate `.meta` files*, *preserve GUIDs*

## 4. Required changes

### Step 1: verification (no file changes)

1. Open `PlayerUIPrefab` in the Editor and select the GameObject that has component `2456797024511787503` (the Send/Cast button).
2. Record whether the component shows as `Confirm Send Button UI1` (loaded) or *Missing / script can not be loaded*.
3. Check the Console for *"No MonoBehaviour scripts in the file, or their names do not match the file name"* for `ConfirmSendButton.cs`.
4. Write the result into the implementation summary, with screenshots if possible.
5. **If the component loads correctly, stop.** Close the task as "not reproducible" and change nothing.

### Step 2: fix (only if Step 1 confirms the defect, and only with owner approval for the chosen option)

**Option A (recommended):** rename the **file**, not the class.
- In the Unity Editor Project window, rename `ConfirmSendButton.cs` → `ConfirmSendButtonUI1.cs`. Unity moves the `.meta` with it, so the GUID `f789bb4c…` stays. If you do it outside Unity, move `.cs` and `.cs.meta` together (`git mv` both), and never let Unity regenerate the meta.
- Confirm after the rename that the `.meta` still contains `guid: f789bb4cc4354b34b84b684f578c1139`.
- No code changes. No class rename. The prefab needs no edits.

**Option B:** rename the **class** to `ConfirmSendButton`.
- This conflicts with the `AGENTS.md` rule against renaming classes, so it needs explicit owner instruction.
- The prefab's `m_TargetAssemblyTypeName` would be stale, but the persistent call resolves through `m_Target`. Re-check the button in the Inspector after the rename.

## 5. Explicit non-goals

- Changing `TryConfirmSend` logic or `PlayerSession.TryHandlePackage` / `CheckPackage`.
- Fixing the stale `m_TargetAssemblyTypeName`/`m_EditorClassIdentifier` strings in the prefab YAML by hand.
- Renaming `ConfirmTargetButtonUI` or editing its button.
- Any other UI refactor.

## 6. Constraints and compatibility

- The GUID `f789bb4cc4354b34b84b684f578c1139` must be preserved. Don't create a new script and re-attach it.
- Don't edit prefab or scene YAML manually.
- No class rename without explicit approval (Option B).

## 7. Acceptance criteria

- [ ] Step 1 result documented: loaded or missing, with Console evidence.
- [ ] If fixed: the component shows as loaded in `PlayerUIPrefab` and in the `GameScene` instance, with no "script can not be loaded" warnings.
- [ ] The `.meta` GUID is unchanged (diff shows only a rename, with ~100% similarity).
- [ ] Clicking the Send/Cast button during `PlayerAction` with a selected and targeted board card reaches `PlayerSession.CardInputServerRpc`. The server logs from `HandleCardControlPackage` show the package was enqueued, or a specific rejection reason.

## 8. Regression tests

There are no automated tests. Review:
- the git diff must show a pure rename (Option A) or a single class-name line (Option B)
- no prefab changes, unless Unity reserializes the `m_EditorClassIdentifier`, which is acceptable if that's the only change

## 9. Manual Unity Editor checks

1. After the fix, enter Play Mode with ParrelSync host and client, and start a match.
2. `CardSelection`: select Fireball or Iceshard to play, then confirm.
3. `PlayerAction` (your turn):
   - click the card in the hand panel
   - select a target (an opponent block for Fireball; Iceshard targets automatically)
   - press **Confirm target**, then **Send**
4. Expected: the effect applies (block HP −50 / opponent HP −20) and the card leaves the board.
5. Also verify the **Confirm target** button works. Its `onClick` type hint is stale, as noted in §2.

## 10. Open questions and assumptions

- **Q1 (owner decision, required before Step 2):** Option A (rename the file to `ConfirmSendButtonUI1.cs`) or Option B (rename the class to `ConfirmSendButton`)? Option A respects `AGENTS.md` as written. Option B gives a cleaner name but needs explicit permission.
- **A1:** It's possible the component loads anyway in Unity 6000.3. That's why Step 1 is mandatory and the task may close with no changes.
- **A2:** Card casting may still fail for other reasons (target validation, C4). This task only covers loading the component.
