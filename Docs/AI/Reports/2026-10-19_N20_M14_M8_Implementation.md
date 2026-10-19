# N20, M14 and M8 implementation: safe card Send, late data and target ownership

## 1. Task summary

Implemented the three specifications in their required order: N20, then M14, then M8, against `fe2d4fb`. Send now ignores stale, empty or incomplete card packages; client card-data reads tolerate delayed/missing data; the local UI can create a skipped card view when its data arrives; AnyPlayer confirmation through a block resolves ownership from replicated team data.

Specifications: `TASK-N20_SafeSendAndTargetRule.md`, `TASK-M14_ClientCardDataNullGuards.md`, `TASK-M8_AnyPlayerTargetViaBlockOnClient.md` under `Docs/AI/Tasks/`.

## 2. Files changed

- `Assets/Scripts/InGame/CardData.cs`: shared static `CardTargetRules` helper.
- `Assets/Scripts/InGame/PlayerSession.cs`: package clearing/validation, missing-data guards, bounded automatic targeting, private block-owner resolver and guarded character targeting.
- `Assets/Scripts/InGame/GameMaster/GameMaster.GameLoop.cs`: use the shared rule in the existing server zero-target check.
- `Assets/Scripts/InGame/NetworkCard.cs`: guards in `CheckActiveTaergetSelection` and safe `RepairCardData` lookup.
- `Assets/Scripts/InGame/CardViewUI.cs`: safe click-log name while preserving both events.
- `Assets/Scripts/InGame/GameMaster/GameMaster.PackageManaging.cs`: notify the local UI after the existing repair/world refresh.
- `Assets/Scripts/UI/PlayerUI.cs`: add `OnCardDataReceived`.
- `Docs/AI/Architecture/GameLoop.md`, `Dependencies.md` and this report.

No scenes, prefabs, card/font/localization assets, serialized fields or package settings were changed. The user's existing removed old task files and new task/report/manual-check documents were preserved. No commit, push, reset or discard was performed. Verification sources/output live only in ignored `build/task-n20-m14-m8-verification/`.

## 3. Important implementation details

### N20

- The shared helper's false cases are exactly the former server switch's eight cases. An optional zero target still continues before `CheckActivity`; a required zero target retains the same warning and rejection. No server validation or effect behavior changed.
- `ClearSelectedCard` resets a nonempty package, retaining the existing empty target list on repeated clears. This covers deselection, rejection, cleanup, elimination and the N18 pause path without per-frame list allocation.
- `CheckPackage` validates confirmed ID, selected-card identity, data/activities, target-list size and required targets in the specified order. Stale identity resets the current package. Empty Send no longer reaches the server. `TryHandlePackage` guards a missing GameMaster and preserves the phase permission and reset after a successful RPC call.
- Selection and confirmed targets intentionally do not survive RespawnWait. Selecting and confirming again after resuming remains required.

### M14

- Confirmation checks data before package mutation, while RespawnWait continues through its existing separate block-confirmation path. Automatic targeting returns for missing data and caps writes to the smaller activity/target count.
- `RepairCardData` keeps `RepairCalled`, checks GameMaster, ID and database, uses `TryGetCard`, and retains the prior data on failure. RPC routing and timing remain unchanged.
- The RPC notifies `PlayerUI` after world refresh. The new method ignores an unbound UI, existing views and cards outside the local hand/played board; otherwise it uses the existing phase-dependent `UpdateCards`. Existing N18 highlight restoration and selected-card validation remain unchanged.
- `NetworkCard.cs` was edited through a reversible single-byte mapping and byte writes, without a UTF-8 conversion. Only the two specified methods changed; CRLF and all other bytes, including the legacy Cyrillic comment block, were verified unchanged against the saved original bytes.

### M8 and authority

- The private resolver matches `BlockController.m_teamId` to the local/opponent session's `TeamId`. It deliberately does not use the block's second `TeamId` variable or its server-only `GetSession` reference. Unknown owners and pending character sessions warn and return false.
- `BlockController.GetSession`, server `CheckActivity`, network ownership and RPC authority remain unchanged. These fixes affect client preparation/presentation, including the host's local client. The only server-side change is extracting the equivalent zero-target rule.
- Required Inspector setup: none. No public API changed except the specified new `CardTargetRules` class and `PlayerUI.OnCardDataReceived` method.

## 4. Tests actually executed and results

- Runtime C# compilation using Unity 6000.3.13f1's bundled Roslyn compiler and the current `Assembly-CSharp.rsp` references, definitions, source list and analyzers: **exit 0**, no errors, no new warnings. Only existing CS0114 (`PlayerUI.OnDestroy`) and CS0414 (`GameMaster.m_cardResolveDelay`) were emitted. Outputs were redirected outside Unity's assembly cache. Evidence: `compile.rsp`, `compile-results.txt`, `compile-exit.txt` in the ignored verification directory. This standalone compiler invocation does not by itself establish NGO IL postprocessing or a player build.
- The open Unity 6000.3.13f1 Editor also compiled/imported the changes. Its log records Netcode IL postprocessors, `Tundra build success` and a successful assembly reload, with the same two warnings and no `error CS`. Selected evidence was saved to `editor-compile-evidence.txt` in the verification directory. This verifies Editor compilation/import, not a gameplay acceptance run.
- `verify.ps1`: **35 isolated scenarios passed**, compiling the actual changed methods and existing package/confirmation/UI update methods with Unity/NGO/view doubles. Coverage includes all 14 target enums plus an unknown value; empty/stale/missing-data/malformed packages; single allocation on clear; permitted/blocked Send; normal block/player/self-target packages; respawn bypass; automatic-target bounds; failed/successful repair; both click events without data; local-only refresh gating; delayed hand data with N18 highlights; selected action-package retention during a board refresh; RPC refresh order; and own/opponent/unknown block and pending character-session targeting. The block double throws if its server-only `GetSession` is used.
- These scenarios simulate state/order and view creation. They are not real network sessions, actual rendered cards, damage/effect resolution or measured latency tests.
- Final source/diff checks passed: the helper's eight optional cases exactly match the original server switch; `PlayerSession` has no client block `GetSession` read; all bytes outside the two specified `NetworkCard` methods and its CRLF line endings are retained; `git diff --check` passes. No scene, prefab or card/font/localization asset changes are present.

## 5. Remaining risks / checks not executed

- No full host/client match, delayed network delivery simulation in Unity, temporary AnyPlayer card-asset test, actual respawn/deselect cast sequence or player build was executed for these tasks.
- M14 handles card data arriving after the local card-list delta. It does not change the existing behavior when the data RPC arrives before the NetworkCard is spawned/found, nor replace identity RPCs with replicated state.
- All*/Random* effects, selection persistence across respawn, locale-driven card text refresh, the other block team variable and other task IDs remain outside scope. The existing empty package normally owns a four-slot target list; the specified automatic-target bound does not redesign that invariant.

## 6. Manual Unity Editor verification

1. With ParrelSync host/client, run each normal cast on both peers: Fireball on an opponent block, Iceshard on the opponent, HealingHooves and Clarity. Verify effects and no new Console errors.
2. Select Fireball, select a block, Confirm, deselect Fireball, then Send. Expect a warning and no RPC or exception. Select/confirm again and verify a normal cast. Send without Confirm must not produce a server "Card 0 was not found" warning.
3. Confirm Fireball, cause the other player's death/lava respawn during PlayerAction, resume, then Send. Expect no exception or cast until the card is selected and confirmed again. Check respawn selection itself remains functional.
4. Temporarily delay the existing draw-data RPC by one second as described in M14; verify each missing hand view appears when its data arrives, clicking works, and N18 play/discard highlights remain. Remove only that temporary edit afterward. Verify Clarity's drawn cards appear in the next CardSelection.
5. Temporarily set Iceshard activity 0 to AnyPlayer as described in M8. On the client, confirm through an opponent block, an own block and the opponent character; expect the correct session and 20 HP loss on that character. Repeat the opponent-block case on the host, then restore only the temporary asset edit.
6. Optionally test a temporary AllBlockOpponent/RandomOpponentBlock target: Confirm then Send should accept zero target under the existing rule; the effect itself remains unimplemented. Restore the temporary asset edit. Check the Console and run a player build.
