# TASK-N1: `PlayerUI.OnDestroy` must override NGO's `OnDestroy` (CS0114)

- **Source issue:** [Issue Report #6 → N1](../Reports/2026-10-19_IssueReport-6.md#-medium-13) (first reported in #2)
- **Severity:** 🟡 Medium (Confirmed)
- **Baseline:** `fe2d4fb`. Line numbers below are from this commit.
- **Order:** 11 of 13. Independent of the other tasks; same pattern as M16.
- **Implementer:** Codex

## 1. Objective

Remove the CS0114 warning, and make `PlayerUI` run NGO's base cleanup when it's destroyed.

## 2. Current behavior

- `Assets/Scripts/UI/PlayerUI.cs:74-80` declares `private void OnDestroy()` on a `NetworkBehaviour`.
- NGO 2.11.2 declares `public virtual void OnDestroy()` (`NetworkBehaviour.cs:1675`). The base removes the behaviour from its `NetworkObject` if that's still spawned, and disposes NetworkVariables.
- The `private` method hides it, so:
  - the compiler warns CS0114 on every compile (`Editor.log:270-272` in report #6)
  - Unity calls only `PlayerUI.OnDestroy`, and the base cleanup is skipped
- Report #6 suggested `protected override`. **That's wrong:** the base is `public`, so the override must be `public` (CS0507 otherwise).

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/UI/PlayerUI.cs` | Change the `OnDestroy` signature and call the base |

## 4. Required changes

Replace `:74-80` with:
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

## 5. Explicit non-goals

- Other `OnDestroy` methods. `CardViewUI`, `CardViewWorld`, `CardWorldViewManager`, `MultiplayerManager`, `CurrentLobbyUI` and `LobbyListUI` are `MonoBehaviour`s, so there's nothing to override.
- `SessionManager` (M16).
- Any other `PlayerUI` change (N20 and M14 touch other methods).

## 6. Constraints and compatibility requirements

- No other changes in `PlayerUI`. No asset changes.

## 7. Acceptance criteria

- [ ] The Unity Console shows no CS0114 after a recompile. CS0414 (N4) may remain.
- [ ] `base.OnDestroy()` is called.
- [ ] Leaving a match and starting a new one works with no errors.

## 8. Regression tests

No automated tests. Review the diff.

## 9. Manual Unity Editor checks

1. Let Unity recompile. The Console has no CS0114 warning.
2. Play a match, leave through the Leave button, return to the main menu, start another match. No errors.

## 10. Open questions and assumptions

None.
