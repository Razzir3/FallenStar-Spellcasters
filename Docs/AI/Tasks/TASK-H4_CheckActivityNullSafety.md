# TASK-H4: Make `CheckActivity` null-safe and reject missing targets

- **Source issue:** [Issue Report #2 → H4 and M7](../Reports/2026-10-18_IssueReport-2.md)
- **Severity:** 🟠 High (H4) plus 🟡 Medium (M7)
- **Chosen solution:** **H4-C.** Reorder the owner check, null-check the opponent, and reject null block or player targets.
- **Order:** independent. It can be done first.
- **Implementer:** Codex

## 1. Objective

`GameMaster.CheckActivity` must never throw. It must return `true` only when the target actually exists and matches the activity's target type.

## 2. Current behavior

In `Assets/Scripts/InGame/GameMaster/GameMaster.Processing.cs:93-174`:

1. **H4.** `:97` reads `_ownerPlayer.m_currentOponentNetworkObjectId` **before** the `_ownerPlayer == null` check at `:99`, so a missing owner throws an NRE.
2. `_ownerPlayerOponent` is never null-checked. It's dereferenced in `OpponentBlock` (`:133`) and `OpponentPlayer` (`:142`), so a missing opponent (for example after a disconnect, H6) also throws.
3. **M7, wrong results:**
   - `OpponentBlock` (`:130-140`) and `SelfBlock` (`:151-161`): when `_targetBlock == null`, the code falls through to `_result = true`.
   - `OpponentPlayer` (`:141-150`) and `SelfPlayer` (`:162-167`): `GetCharacter() == _targetCharacter` is `true` when both are `null` (dead player, or a target ID that isn't a session).
   - Result: an invalid target passes. `ProcessActivity` then does nothing (or logs "Error here"), and the card is still sent to the graveyard (`GameLoop.cs:287`).

The caller is `PlayerActionPhase` (`GameMaster.GameLoop.cs:240`), which only calls it for a non-zero target ID. Zero targets are handled at `:213-235`. The other caller, `ResolvePlayerCardsPhase`, is dead code.

## 3. Relevant files and dependencies

- `GameMaster.Processing.cs`: `CheckActivity` (the only method to change)
- `GameMaster.Player.cs` / `GameMaster.cs`: `GetSessionByID`, `GetBlockById` (read-only use)
- `BlockController.m_teamId`. Keep using this one; see L4 for why there are two team fields.
- Card assets:
  - `Fireball.asset`: `s_actionType: 1` (Damage), `s_targetType: 2` (`OpponentBlock`)
  - `Iceshard`: `OpponentPlayer`

## 4. Required changes

Rewrite the body of `CheckActivity` with the same signature and the same `switch` structure.

1. Get `_ownerPlayer`. If it's `null`: log `Debug.LogError("No owner")` (existing text) and return `false`. **Only after that**, get `_ownerPlayerOponent`, which may be `null`.
2. The target lookups (`_targetSession`, `_targetCharacter`, `_targetBlock`) stay as they are.
3. Per target type:

| Target type | New rule |
|---|---|
| `OpponentBlock` | `_targetBlock != null && _ownerPlayerOponent != null && _targetBlock.m_teamId.Value == _ownerPlayerOponent.TeamId.Value` |
| `SelfBlock` | `_targetBlock != null && _targetBlock.m_teamId.Value == _ownerPlayer.TeamId.Value` |
| `OpponentPlayer` | `_targetCharacter != null && _ownerPlayerOponent != null && _ownerPlayerOponent.GetCharacter() == _targetCharacter`. Keep the existing `Debug.Log("Error")` for the false branch, or replace it with a clearer warning in the same style. |
| `SelfPlayer` | `_targetCharacter != null && _ownerPlayer.GetCharacter() == _targetCharacter` |
| `AnyPlayer`, `AnyPlayerBlock` | Unchanged (already null-checked) |
| `None`, `All*`, `Random*` | Unchanged (`true`) |

4. Nothing else in the file changes.

## 5. Explicit non-goals

- `ProcessActivity`, including H3 and the `GetSessionByID(cardOwnerId).GetCharacter()` call at `:179`. It's reached only after `CheckActivity` has passed, so the owner exists.
- The `m_teamId` vs `TeamId` duplication on blocks (L4).
- Implementing `All*` and `Random*` targets.
- Client-side target selection (`PlayerSession.TryConfirmTarget`, M8).

## 6. Constraints and compatibility

- No renames. Signature unchanged. Server-only code.
- **Intended behaviour change:** a card cast at an invalid target is now **rejected**. `PlayerActionPhase` takes the `!_isMessageValid` path, so the card stays on the board and the player can retarget it. Before, the card was consumed with no effect.
- Honest play with the existing cards (Fireball on an opponent block, Iceshard on the opponent) must behave exactly as before.

## 7. Acceptance criteria

- [ ] No dereference of `_ownerPlayer` or `_ownerPlayerOponent` can happen while they are `null`.
- [ ] Each rule in §4.3 is implemented exactly.
- [ ] Fireball on an opponent block and Iceshard on the opponent still work.
- [ ] Fireball sent with your own block, a player session ID, or a non-existent ID is rejected with the existing "Target validation failed" warning (`GameLoop.cs:242`), and the card stays on the board.
- [ ] Compiles without new warnings.

## 8. Regression tests

No automated tests. Optional: extract the rule table into a pure static function (team IDs and booleans in, `bool` out) and add EditMode tests. That's for Codex to decide.

Review checklist:
- the null check comes before the first dereference
- every target type listed in §4.3 is covered

## 9. Manual Unity Editor checks

1. ParrelSync host and client, start a match.
2. Fireball: select an opponent block, Confirm target, Send. The block loses 50 HP.
3. Iceshard: the opponent loses 20 HP.
4. **Tamper check (temporary client edit, don't commit):** send Fireball with your own block ID, then with the opponent session ID. Both are rejected, the card stays on the board, and there's no exception.

## 10. Open questions and assumptions

- **A1:** target IDs for player types are **session** `NetworkObjectId`s, because `GetSessionByID(targetID)` is used to find `_targetCharacter`. This is verified in the code.
- **Q1:** should a rejected cast be reported to the casting client (UI feedback)? Today it's only a server log. Out of scope.
