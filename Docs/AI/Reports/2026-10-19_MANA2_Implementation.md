# TASK-MANA2 implementation report

## 1. Task summary

Implemented play-card mana validation and spending at accepted selection confirm, following `TASK-MANA2_ManaCheckAndSpendOnConfirm.md`.

## 2. Files changed

- `Assets/Scripts/InGame/GameMaster.cs`: timer reset configuration, counters and match reset.
- `Assets/Scripts/InGame/GameMaster/GameMaster.GameLoop.cs`: guarded shared timer reset and phase counter reset.
- `Assets/Scripts/InGame/GameMaster/GameMaster.PackageManaging.cs`: server budget validation, rejection and accepted spending.
- `Assets/Scripts/InGame/PlayerSession.cs`: client play selection gating and owner rejection RPC.
- `Assets/Scripts/UI/PlayerUI.cs`: rejection highlight clearing.
- `Docs/AI/Architecture/GameLoop.md` and `Dependencies.md`: updated mana contracts.
- This report.

No scenes, prefabs or other gameplay assets changed.

## 3. Important implementation details

- The server checks the play cost after existing card-ID validation. Invalid IDs remain filtered and recorded even when the valid subset is accepted.
- Over-budget confirmation clears server selection lists, remains unconfirmed, records `ManaOverBudget`, sends an owner-targeted clear RPC and returns before board writes.
- The first accepted confirmation spends play cost through `AddMana`. Duplicate confirmations are ignored. Discards remain free; turn-order cost still includes play and discard cards.
- Shared timer resets require both limits to allow them: defaults are one per player per phase and three per player per match. Further attempts still reject and record suspicion.
- The owner RPC clears local selection lists, selected-card references and hand highlight colors. Message localization remains a MANA3 hook.
- The task's dependency table mentions a certainty argument to `ReportSuspicious`; the existing SEC1 API takes the action type and detail string, matching the task's concrete rejection instruction. No API change was needed.

## 4. Tests actually executed

- Unity 6.3 LTS Editor compilation succeeded, including Netcode IL post-processing. Existing warnings remain: `PlayerUI.OnDestroy` hides the inherited method (CS0114), and `GameMaster.m_cardResolveDelay` is unused (CS0414). No new compiler warnings were observed.
- A temporary Editor menu tool ran in Play Mode against a real local NGO host with spawned project GameMaster, PlayerSession and card prefabs. Both test sessions were owned by the local host. All **30 assertions passed**:
  - Exact-budget selection, additional/initial over-budget refusal, unknown-data refusal, deselection, free discards and no local spending.
  - Rejection preserves mana, board and card locations; clears server lists; remains unconfirmed; records suspicion.
  - The generated owner RPC clears local selection, selected-card references and actual play/discard highlight colors on the host.
  - First timer reset, same-phase reset refusal, independent player budgets, phase resets and match limit across four phases.
  - Valid retry spends exactly once; invalid IDs are still filtered/tracked; duplicate confirm changes neither mana nor board.
  - Both valid confirms end selection; one confirm/rejection does not; turn-order cost still includes free discards.
  - Timeout without an accepted confirm leaves no play cards, no spending and zero selection cost; timer resets outside CardSelection are ignored.
- Initial attempts stopped before assertions because Netcode message registration was stale after script reload during an existing Play Mode session. Restarting Play Mode resolved the test setup issue. No production workaround was added.
- The temporary tool was removed from Assets after testing; its source and assertion output remain under ignored `Temp/MANA2Verification/` and `Temp/MANA2-verification.txt` for local inspection.
- Final Git diff inspection and `git diff --check` passed.

## 5. Remaining risks

- These focused tests exercised host-local RPC dispatch, not delivery to a separate remote owner or a dedicated server.
- Full matches, round progression and match restart were not exercised by this tool. The `StartNewGame` counter clear was inspected statically.
- New mana UI and localized rejection feedback are intentionally deferred to MANA3.

## 6. Manual Unity Editor verification

No Inspector wiring is required. GameMaster exposes the two new reset limits with defaults 1 per phase and 3 per match.

1. Run a ParrelSync host and client. With 2 mana, verify Fireball (3) cannot highlight for play, Iceshard (2) can, and accepting Iceshard reduces current mana to 0.
2. With capacity 4 in round two, verify a 4-cost set is allowed and a 6-cost set is refused. Verify three discards cost no mana.
3. Temporarily bypass client gating in a local test copy and submit an over-budget confirm from the remote client. Verify no mana/board change, cleared client highlights, one suspicion entry and a timer reset. Retry a valid selection successfully.
4. Repeat rejection in the same phase and across four phases: only the first attempt per phase resets the timer, and only three resets per player occur per match. Verify rejection never ends selection early and timeout leaves unconfirmed players empty.
5. Complete an honest full match with no unexpected suspicion records, then restart a match and verify reset allowances are replenished. Restore any temporary gating change.
