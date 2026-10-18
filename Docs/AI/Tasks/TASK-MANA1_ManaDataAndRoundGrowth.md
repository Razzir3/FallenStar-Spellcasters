# TASK-MANA1: Mana data, initial values and growth at round break

- **Source:** [RESEARCH-ManaSystem](../Research/RESEARCH-ManaSystem.md) (steps S1–S3)
- **Owner decisions (2026-10-18):**
  - **D1 = B for now, the capacity model.** Each round the capacity grows by a step up to a cap, and the pool refills to capacity; unspent mana is lost. The model **may later change to a mixed rule**, so keep the growth rule in **one** method.
  - **D5:** growth happens at **RoundBreak, before DrawCards**.
  - **D6:** mana is visible to both players.
  - **D7:** base **2**, step **2**, cap **20**.
- **Order:** MANA1 → MANA2 → MANA3. Start after H5, H6 and H10 are merged, because of shared files (`GameMaster.cs`, `GameLoop.cs`).
- **Implementer:** Codex

## 1. Objective

Every player has a networked **mana capacity** and **current mana**. Both start at the base value and are updated by the server at the start of each round's break, using one replaceable growth rule. This task does **not** use mana for anything yet; that's MANA2.

## 2. Current behavior

- There's no mana concept. Cards have `CardData.m_cost` (`CardData.cs:85`), used only for turn order (`PlayerTurnSelection.TotalCost`, `BuildTurnOrderByCardCost`).
- Match tuning lives in `[SerializeField]` fields on `GameMaster` (`GameMaster.cs:69-79`). `GameMasterPrefab` doesn't serialize them, so the C# defaults apply.
- `GameLoop()` (`GameMaster.GameLoop.cs:98`) runs `RoundBreakPhase()` → `DrawCardPhase()` → `CardSelectionPhase()` → `PlayerTurnPhase()` for each round.
- `RoundBreakPhase()` (`GameLoop.cs:~380`) sets `GamePhase.RoundBreak` and resets the timer.
- Sessions are created fresh per match (`SessionManager` in `GameScene`). `PlayerSession.Initialize` runs before spawn and triggers the NGO warning N2, so don't write mana there.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/PlayerSession.cs` | 2 new NetworkVariables |
| `Assets/Scripts/InGame/GameMaster.cs` | 3 config fields; initialize in `StartNewGame` |
| `Assets/Scripts/InGame/GameMaster/GameMaster.GameLoop.cs` | Growth at the start of `RoundBreakPhase` |

## 4. Required changes

1. **`PlayerSession`** (next to `m_lifeCurrent`, `:39`, same permission style):
   ```csharp
   public NetworkVariable<int> m_manaCapacity = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
   public NetworkVariable<int> m_manaCurrent  = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
   ```
2. **`GameMaster` config** (next to `m_startHandCards`):
   ```csharp
   [SerializeField] private int m_baseMana = 2;
   [SerializeField] private int m_manaGrowthStep = 2;
   [SerializeField] private int m_maxMana = 20;
   ```
3. **Initialize.** In `StartNewGame`, after `m_players` has been filled and the 2-player check has passed, and before `StartCoroutine`: for each player, `m_manaCapacity.Value = Mathf.Clamp(m_baseMana, 0, m_maxMana)` and `m_manaCurrent.Value = m_manaCapacity.Value`.
4. **Growth rule in one place.** Add `private void ApplyRoundManaGrowth(PlayerSession player, bool isFirstRound)` in `GameMaster.GameLoop.cs`:
   - Server-only, and return if `player == null`.
   - Model B: if `!isFirstRound`, set `m_manaCapacity.Value = Mathf.Min(m_manaCapacity.Value + m_manaGrowthStep, m_maxMana)`. Then set `m_manaCurrent.Value = m_manaCapacity.Value`, which is the refill.
   - Add an XML comment saying this is the **only** place the round-growth rule lives. A future mixed model (for example a partial carry-over) changes only this method.
5. **Call it at RoundBreak.**
   - `GameLoop()` keeps a local `bool _isFirstRound = true`, passes it to `RoundBreakPhase(_isFirstRound)` (a private signature change is allowed), and sets it to `false` after the first iteration.
   - At the **start** of `RoundBreakPhase`, after `m_gamePhase.Value = GamePhase.RoundBreak`, call `ApplyRoundManaGrowth(p, isFirstRound)` for each `p` in `m_players`.
6. **Mutation helper for later tasks.** Add `public void AddMana(PlayerSession player, int delta)` (server-only). It clamps `m_manaCurrent` to `[0, m_manaCapacity]`. MANA2 and future mana card effects use it, so every write goes through one place.

## 5. Explicit non-goals

- Checking or spending mana (MANA2).
- Mana UI and localization (MANA3).
- Mana card effects (no `CardActionType` changes).
- Changing turn order (D4: keep).

## 6. Constraints and compatibility

- No renames. Additive fields, plus a private signature change for `RoundBreakPhase`.
- Server writes only. No client code in this task.
- Timing and behaviour of every phase are unchanged.

## 7. Acceptance criteria

- [ ] At match start, both players have capacity 2 and current 2.
- [ ] At the start of round N (N ≥ 2), capacity = min(2 + 2·(N−1), 20) and current = capacity. Round 10 and later stay at 20.
- [ ] Values are visible on both host and client (check in the Inspector, or with a temporary debug log).
- [ ] The growth rule exists in exactly one method.
- [ ] Compiles without new warnings.

## 8. Regression tests

Optional EditMode test: extract the capacity formula as a pure static function (current capacity, step, cap, isFirstRound) → new capacity, and test the cap and the first round.

## 9. Manual Unity Editor checks

1. ParrelSync host and client, start a match. Select both `PlayerSession` objects in the Hierarchy (or log under debug), and watch `m_manaCapacity` and `m_manaCurrent` across 3 rounds: 2/2 → 4/4 → 6/6.
2. Temporarily set `m_maxMana = 5` on the GameMaster instance, play 3 rounds, and confirm the values stop at 5. Revert.

## 10. Open questions and assumptions

- **A1:** "each turn" means **each round**. Each player gets one action turn per round (F4 in the research).
- **Q1:** for the future mixed model, what should carry over? `ApplyRoundManaGrowth` is the extension point.
