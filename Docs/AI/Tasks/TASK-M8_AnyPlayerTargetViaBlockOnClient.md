# TASK-M8: Resolve an `AnyPlayer` target through a block on a non-host client

- **Source issue:** [Issue Report #6 → M8](../Reports/2026-10-19_IssueReport-6.md#-medium-13) (first reported in #1)
- **Severity:** 🟡 Medium (Plausible, latent: no current card uses `AnyPlayer`)
- **Baseline:** `fe2d4fb`. Line numbers below are from this commit.
- **Order:** 3 of 13. Do it after N20 and M14 (same file, `TryConfirmTarget`).
- **Implementer:** Codex

## 1. Objective

On a non-host client, confirming an `AnyPlayer` target by clicking a **block** picks the session that owns that block, without throwing. Picking through a **character** never throws either.

## 2. Current behavior

`PlayerSession.TryConfirmTarget`, `AnyPlayer` branch (`InGame/PlayerSession.cs:855-869`):
- **Through a character** (`:856-859`): `m_currentSelectedCharacter.GetSession().NetworkObjectId`.
  - `PlayerController.GetSession()` works on clients; it resolves through the replicated `SessionObjectId` (`PlayerController.cs:311-322`).
  - It can return `null` before that value arrives, which gives a `NullReferenceException`.
- **Through a block** (`:860-863`): `m_currentSelectedBlock.GetSession().NetworkObjectId`.
  - `BlockController.GetSession()` returns `m_ownerSession` (`BlockController.cs:94-97`).
  - That field is set only on the server: `PlayerSession.AddBlock` → `BlockController.SetSession`, during server spawning.
  - On a non-host client it's always `null`, so the code throws at `:862`. The host works because the host is the server.

**Server check** (`GameMaster.Processing.cs:123-126`): an `AnyPlayer` target must be a session whose character exists.

**Already replicated team data:** `BlockController.m_teamId` (set in `Initialize`, `:59`), `PlayerSession.TeamId`, and the opponent session (resolved in the same method at `:834`). The other block checks in the method already use `m_teamId`: `:888`, `:899`, and `TryConfirmRespawnBlock` at `:960`.

**Current cards:** Fireball `OpponentBlock`, Iceshard `OpponentPlayer`, Clarity and HealingHooves `SelfPlayer`. So the defect is latent.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/PlayerSession.cs` | New private helper; `AnyPlayer` branch of `TryConfirmTarget` |

Read-only: `BlockController.cs`, `PlayerController.cs`, `GameMaster.Processing.cs` (`CheckActivity`).

## 4. Required changes

1. Add a private helper to `PlayerSession`:
   ```csharp
   private PlayerSession GetBlockOwnerSession(BlockController block, PlayerSession opponent)
   {
       if (block == null)
           return null;

       int _blockTeam = block.m_teamId.Value;

       if (_blockTeam == TeamId.Value)
           return this;

       if (opponent != null && _blockTeam == opponent.TeamId.Value)
           return opponent;

       return null;
   }
   ```
   Use `m_teamId`, not `BlockController.TeamId` (L4: two team variables; the rest of this method uses `m_teamId`).
2. **`AnyPlayer` branch:**
   - **Character:** `PlayerSession _targetSession = m_currentSelectedCharacter.GetSession();` If it's null → `Debug.LogWarning("Selected character has no session yet.")` and return `false`. Otherwise `ConfirmSelectTarget(i, _targetSession.NetworkObjectId)`.
   - **Block:** `PlayerSession _blockOwner = GetBlockOwnerSession(m_currentSelectedBlock, _oponent);` If it's null → `Debug.LogWarning("Selected block has no known owner.")` and return `false`. Otherwise `ConfirmSelectTarget(i, _blockOwner.NetworkObjectId)`.
3. Don't change `BlockController.GetSession()`. Server code depends on it, for example `UpdateBlockHp`.

## 5. Explicit non-goals

- Merging the two block team variables (L4).
- Replicating `m_ownerSession` to clients.
- Other target branches, N20, M14.
- Implementing `AnyPlayerBlock` or `RandomBlockSelectedPlayer` effects.

## 6. Constraints and compatibility requirements

- Client-only change. The host resolves the same session as before (same team, same owner).
- No public API change. The helper is private.
- No asset changes in the final diff (the §9 asset change is temporary).

## 7. Acceptance criteria

- [ ] On a non-host client, an `AnyPlayer` card confirmed through an opponent block targets the opponent; through an own block, it targets the player. No exception.
- [ ] Confirming through a character still works on both host and client.
- [ ] The host's behavior is unchanged.
- [ ] Compiles with no new warnings.

## 8. Regression tests

No automated tests. **Review checklist:** no `BlockController.GetSession()` call is left on any client path in `PlayerSession`.

## 9. Manual Unity Editor checks

**Temporary asset change (don't commit):** set `Assets/Card/Data/Iceshard.asset` activity 0 `s_targetType` to `AnyPlayer` (23). Then, with ParrelSync host + client, **on the client**:
1. In your action turn, select Iceshard, click an **opponent block**, Confirm, Send. The opponent's pony loses 20 HP. No exception.
2. Next round, Iceshard through your **own block** → your own pony loses 20 HP.
3. Iceshard through the opponent's **pony** → the opponent loses 20 HP.
4. Repeat check 1 on the host.

Then run `git restore Assets/Card/Data/Iceshard.asset`.

## 10. Open questions and assumptions

- **A1:** A block whose team matches neither player can't exist in a 2-player match. The warning covers it anyway.
