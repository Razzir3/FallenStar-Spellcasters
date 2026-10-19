# TASK-H3: Block heal over the cap works like the character heal

- **Source issue:** [Issue Report #5 → H3](../Reports/2026-10-19_IssueReport-5.md#-high-still-open), first reported in [#1](../Reports/2026-10-18_IssueReport.md#-high)
- **Owner decision (2026-10-19):** "change block heal over the cap like characters do"
- **Severity:** 🟠 High (latent). No current card heals a block, but the first one would abort the match.
- **Order:** 1 of 5 (H3 → H2 → N14 → N18_M1 → N19)
- **Implementer:** Codex

## 1. Objective

When a heal targets a block and would push it over its maximum HP, apply the **same rule as for characters**:
- heal normally up to the maximum
- 50 % of the overflow becomes extra HP above the maximum

The block branch must never touch the character reference, so it can't throw.

## 2. Current behavior

`GameMaster.ProcessActivity`, `CardActionType.Heal` (`Assets/Scripts/InGame/GameMaster/GameMaster.Processing.cs`).

**Character branch (`:198-211`), the reference rule:**
```csharp
if (_targetCharacter.m_hpCurrent.Value <= (_targetCharacter.m_hpMax.Value - activity.s_value))
    _targetCharacter.m_hpCurrent.Value += activity.s_value;
else
{
    double _shieldDelta = (activity.s_value - _targetCharacter.m_hpMax.Value + _targetCharacter.m_hpCurrent.Value) * 0.5;
    _targetCharacter.m_hpCurrent.Value = _targetCharacter.m_hpMax.Value + (_shieldDelta).ConvertTo<int>();
}
```

**Block branch (`:213-227`) has three defects:**
- The maximum is hard-coded as `100` in two places (`:217`, `:223`).
- The overflow result uses `_targetCharacter.m_hpMax.Value` (`:224`). `_targetCharacter` is `null` for a block target, so this throws a `NullReferenceException`. H1 then aborts the match.
- The block's real maximum isn't stored anywhere. `BlockController.Initialize(PlayerSession session, int hpmax = 100)` (`Assets/Scripts/InGame/BlockController.cs`) writes `hpmax` into `m_blockHP` and forgets it.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/BlockController.cs` | Store the maximum HP (server-side) and expose it with a getter |
| `Assets/Scripts/InGame/GameMaster/GameMaster.Processing.cs` | Block heal branch uses the stored maximum and the character formula |

Callers of `BlockController.Initialize`: only `GameMaster.CreateBlock` (`GameMaster.Spawn.cs:275`), which uses the default `100`.

## 4. Required changes

1. **`BlockController`:**
   - Add `private int m_blockHPMax = 100;`
   - In `Initialize`, set `m_blockHPMax = hpmax;` next to the existing `m_blockHP.Value = hpmax;`
   - Add `public int GetMaxHP() { return m_blockHPMax; }`
   - The value is server-only, like `m_ownerSession`. Add a one-line comment saying so. Don't make it a NetworkVariable: clients don't need it.
2. **`GameMaster.ProcessActivity`, Heal, block branch:**
   - Read `int _blockMaxHp = _targetBlock.GetMaxHP();` once.
   - Replace both hard-coded `100` values with `_blockMaxHp`.
   - Replace `_targetCharacter.m_hpMax.Value` with `_blockMaxHp`.
   - Use **exactly** the character formula and conversion: `* 0.5`, then `(_shieldDelta).ConvertTo<int>()`, so blocks and characters round the same way.
   - Keep the target-type check and the `UpdateBlockHp(_targetBlock)` call as they are.
3. **Optional:** extract a `private static int HealWithOverflow(int current, int max, int heal)` and use it in **both** branches. Do this only if character results stay identical for every input, so the helper can be covered by an EditMode test.

## 5. Explicit non-goals

- Changing the character heal rule or the 50 % factor.
- Making block max HP visible on clients, or adding a block HP bar.
- Adding a heal-block card. A temporary local asset change for testing is fine, but **must not be committed**.
- Any change to `Damage` / `DrawCard` branches or `CheckActivity`.

## 6. Constraints and compatibility

- No renames. Additive field and getter only.
- `BlockController.Initialize` keeps its signature and default value.
- Server-only logic. No new RPCs or NetworkVariables.
- Keep the existing `Unity.VisualScripting` `ConvertTo<int>()` usage for consistency. Replacing it is out of scope (L10).

## 7. Acceptance criteria

- [ ] The block heal branch no longer references `_targetCharacter`, and no hard-coded `100` is left in it.
- [ ] With max 100 and heal 30:
  - block HP 50 → **80** (normal heal)
  - block HP 80 → **105** (overflow 10 → +5)
  - block HP 100 → **115** (overflow 30 → +15)
  - character HP 80 with the same heal → **105**
- [ ] Healing a block never throws.
- [ ] The character heal results are unchanged.
- [ ] Compiles without new warnings.

## 8. Regression tests

- **Optional EditMode test** if step 4.3 is done: `HealWithOverflow` with (50,100,30) → 80, (80,100,30) → 105, (100,100,30) → 115, (110,100,30) → 120.
- **Review checklist:**
  - only the block branch changed (plus the optional helper)
  - `UpdateBlockHp` is still called
  - the `ConvertTo<int>()` conversion is the same in both branches

## 9. Manual Unity Editor checks

Run these only in a **temporary local copy**. Revert before committing.
1. Change `HealingHooves.asset` activity 0 target from `SelfPlayer` (21) to `SelfBlock` (1).
2. ParrelSync host and client. In round 2 or later, play HealingHooves on a full-HP own block (100 → 115).
3. Fireball the opponent's block once (100 → 50), then let the opponent heal it (50 → 80).
4. Check the HP values in the Inspector (`BlockController.m_blockHP`) on host and client. Check there's no exception and no `GameMaster: match aborted` log.
5. Revert the asset change.

## 10. Open questions and assumptions

- **A1:** "like characters do" means the same formula with the block's own maximum, including the `ConvertTo<int>()` rounding.
- **A2:** Every block has max 100 today (`CreateBlock` uses the default). Storing the value keeps a future per-block maximum working.
