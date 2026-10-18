# TASK-H8: `CardData.OnValidate` must not wipe activities 1–3

- **Source issue:** [Issue Report #2 → H8](../Reports/2026-10-18_IssueReport-2.md)
- **Severity:** 🟠 High (silent data loss for designers)
- **Chosen solution:** **H8-B.** `OnValidate` only fixes the array length without wiping. Defaults go in `Reset()`.
- **Order:** independent. It can be done first.
- **Implementer:** Codex

## 1. Objective

Designers can author up to 4 activities per card in the Inspector, and the values survive validation, saving, reimport and domain reload.

## 2. Current behavior

`Assets/Scripts/InGame/CardData.cs:92-108`:

```csharp
private void OnValidate()
{
    if(m_activities == null || m_activities.Length != 4)
        m_activities = new CardActivity[4];        // wipes everything if length != 4
    for(int i = 1;i<=3;i++)
        m_activities[i] = new CardActivity { None, None, 0, None };   // wipes slots 1–3 always
}
```

- `CardActivity` is a `struct` (`CardData.cs:58`), and all its enums have `None = 0`, so `default(CardActivity)` is identical to the reset value.
- **Current data:** all 5 assets in `Assets/Card/Data/`:
  - `CardData`, `Clarity`, `Fireball`, `HealingHooves`, `Iceshard`
  - all have exactly 4 entries, with slots 1–3 all zero
  - nothing is lost by the fix

## 3. Relevant files and dependencies

- `Assets/Scripts/InGame/CardData.cs`: `OnValidate`, plus a new `Reset`
- Card assets (read-only check): `Assets/Card/Data/*.asset`
- Downstream readers that depend on `Length == 4`:
  - `GameMaster.PlayerActionPhase` (`GameLoop.cs:192-203`) requires `TargetNetworkObjectId.Count >= m_activities.Length`
  - `PlayerSession` target selection (around `:744`) loops over `m_activities.Length`

## 4. Required changes

1. `OnValidate()`:
   ```csharp
   if (m_activities == null)
       m_activities = new CardActivity[4];
   else if (m_activities.Length != 4)
       System.Array.Resize(ref m_activities, 4);
   ```
   Remove the `for` reset loop. `Array.Resize` keeps the existing entries, and new slots are `default` (= None).
2. Add `private void Reset()`, which Unity calls when the asset is created or when "Reset" is chosen in the Inspector:
   ```csharp
   m_activities = new CardActivity[4];
   ```
   Optionally set explicit `None`/`0` values for readability; they equal `default`.
3. Keep the field initializer `= new CardActivity[4];`.

## 5. Explicit non-goals

- Validating the content of activities (gaps, invalid combinations), for example with an Editor warning (H8-C was not chosen).
- Making multi-effect cards actually work end to end (client targeting, `CardControllPackage` target count). See §10.
- Editing any card asset.

## 6. Constraints and compatibility

- No renames. Serialized field name and type unchanged, so no asset migration is needed.
- `OnValidate` and `Reset` are Editor callbacks. Don't add `#if` around them (they don't exist in a player build anyway).

## 7. Acceptance criteria

- [ ] Setting slot 1 of a card (e.g. `Damage` / `OpponentPlayer` / 10) in the Inspector survives deselecting, saving the project, reimporting the asset and a script recompile.
- [ ] A new `CardData` asset (Create menu) starts with 4 empty activities.
- [ ] Shrinking or growing the array in the Inspector snaps back to 4 and keeps slots 0..3.
- [ ] Opening the project causes **no changes** to the 5 existing `.asset` files (`git status` is clean).
- [ ] Compiles without new warnings.

## 8. Regression tests

No automated tests. Optional EditMode test: `ScriptableObject.CreateInstance<CardData>()`, set slot 2, call `OnValidate` via reflection or by making it `internal`. Codex should keep it `private` unless the owner agrees otherwise, and assert that slot 2 is kept.

## 9. Manual Unity Editor checks

1. Open `Assets/Card/Data/Fireball.asset`. Set activity 1 to any non-None value, save and reimport. The value stays. **Set the value back to None by hand afterwards**, and confirm `git status` shows no change to the asset. Don't use `git checkout` or `git reset`.
2. Create a new CardData in a scratch folder, check it has 4 empty activities, then delete the scratch asset.
3. Play one round to confirm the existing cards still work.

## 10. Open questions and assumptions

- **Risk note (not in scope):** once a designer authors a second activity, these paths run for the first time:
  - multi-target selection on the client
  - the per-activity validation loop in `PlayerActionPhase`

  The owner may want a follow-up test task before shipping such a card.
