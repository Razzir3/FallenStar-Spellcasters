# Issue Report #2: FallenStar – Spellcasters (re-audit after C1–C5)

- **Date:** 2026-10-18
- **Baseline:** `HEAD` = `44d858f` ("task 4"). The working tree is clean.
- **Previous report:** [2026-10-18_IssueReport.md](2026-10-18_IssueReport.md), written against `1f6d2f1` plus the then-uncommitted refactor, which is now commit `50e7502`.
- **Method:**
  1. Read the full diff `50e7502..44d858f` (commits `15b439b`, `61323d5`, `cebd2fd`, `fb0edb9`, `44d858f`) and the complete current versions of every changed file.
  2. Files that did not change since the first audit (checked with `git diff`) were **not** re-read in full. Each earlier finding in them was re-checked by grep or a targeted read at its current line.
  3. New evidence: the Unity Editor logs on this machine (`%LOCALAPPDATA%\Unity\Editor\Editor.log`, from the ParrelSync clone `FallenStar-Spellcasters_clone_1`, last written 11:57; and `Editor-prev.log`, from the main project).
  4. **No build or Play Mode was run by me.** I changed no code, scenes, prefabs or settings.

Severity levels and verdicts are the same as in report #1:
- **Severity:** 🔴 Critical, 🟠 High, 🟡 Medium, 🟢 Low, ⚪ Info.
- **Verdict:** **Confirmed** means visible in the code path. **Plausible** means it needs an Editor or Play Mode check.

## Summary

| | Count |
|---|---|
| Critical fixed (static review) | 4: C1, C2, C3, C4 |
| Critical partly verified | 1: C5 (Editor compile is clean; player build not run) |
| Critical still open | 1: C6, now *likely not reproducible* |
| Earlier issues resolved or withdrawn | 2: M5 fixed by C3; M2 withdrawn (false positive) |
| 🟠 High still open | 9: H1–H9 |
| 🟡 Medium still open | 14, plus 1 new (N1) |
| 🟢 Low still open | 10, plus 6 new (N2–N7) |
| ⚪ Info, new | 2: N8, N9 |

**Overall:** none of the four critical cheating holes remains in the code. What blocks a playable match now is **robustness and match lifecycle** (H1, H2, H5, H6 and H7), not security.

---

## 1. Review of implemented critical fixes

| ID | Commit | What changed | Matches the task spec? | Verdict | Residual notes |
|---|---|---|---|---|---|
| **C5** | `15b439b` | Removed `using UnityEditor.Localization.Plugins.XLIFF.V20;` (`CardPreviewController.cs`) and `using NUnit.Framework;` (`CardControllPackage.cs`) | Yes. Only those 2 lines changed. A grep finds no other unguarded `UnityEditor`/`NUnit`/`TestTools` imports in `Assets/Scripts`. | ✅ **Partly verified** | `Editor.log` shows Editor compiles with **0 errors** and 2 warnings (see N1, N5). **No `BuildPlayer` in either log**, so the acceptance criterion "Windows development build" is still unmet. |
| **C1** | `61323d5` | `HPDeltaProcessing` runs only under `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD` **and** `m_debugMode` (`GameMaster.Processing.cs:77-82`). The client Space key is wrapped in the same `#if` (`PlayerController.cs:184-189`). | Yes | ✅ **Fixed** (static) | In Editor and development builds the debug HP key still works, because `GameMasterPrefab` ships `m_debugMode: 1` (L9). Release builds are safe. The `s_hpDelta` field is still sent over the network but is ignored. |
| **C2** | `cebd2fd` | `InputServerRpc(PlayerControlPackage, ServerRpcParams rpcParams = default)`. It rejects the call if `SenderClientId != OwnerClientId` and overwrites `_package.ClientId` (`PlayerController.cs:233-247`). | Yes. `HandlePlayerControlPackage` has exactly 1 caller (`PlayerController.cs:258`). | ✅ **Fixed** (static) | Host self-call: `SenderClientId` = `ServerClientId` = 0 = the host's `OwnerClientId`. Correct. |
| **C3** | `fb0edb9` | `InputQueueUpdate` resolves the session through `SessionManager.GetPlayerSession`, requires `CanPlayerMove(session)`, and takes the controller from `session.GetCharacter()` with an owner check (`Processing.cs:45-74`). | Yes | ✅ **Fixed** (static). **Also fixes M5** (`PlayerObject` NRE). | Clients still pre-check with data they don't have (see N3). |
| **C4** | `44d858f` | `HandleConfirmSelectionTurn` now returns `bool`, so a repeat or missing selection returns before any `Clear()`. The new `ValidateSelectionCards` requires: in the sender's `m_handCards`, ID ≠ 0, no duplicates across both lists (shared `HashSet`), card exists, `m_ownerId == session.NetworkObjectId`, `m_cardData != null`. The board is built only from accepted cards. `ValidateCardOwner` is null-safe. (`PackageManaging.cs:187-218, 306-370, 442-449`) | Yes. Same side-effect order (`SetLocation`, `m_isRotated`, `SendCardDataClientRpc`, `Add`). | ✅ **Fixed** (static). **Compile not confirmed in a log:** the commit (12:02) is newer than the last logged compile (11:57). By reading, the types match (`List<NetworkCard>`, C# 9 `new()`). | `ValidateCardOwner` is now unused (N4). Behaviour change: a player can no longer change the selection after confirming (N9). This is intended by the spec. |
| **C6** | none | Not implemented. `UI/ConfirmSendButton.cs:3` still declares `ConfirmSendButtonUI1`. | n/a | 🟡 **Likely not reproducible** | New evidence: `GameScene` (which contains `PlayerUIPrefab`) was loaded twice in the clone log (`Editor.log:1127, 1304`). There is **no** "referenced script is missing", "names do not match" or "can not be loaded" message in either log. Do TASK-C6 Step 1 (look in the Inspector) and close it if the component shows as loaded. |

Manual ParrelSync checks (task section 9) and tamper tests: **no evidence** in the repo or logs that they were run. They should be recorded in `Docs/AI/Reviews/` or in a Codex implementation summary.

---

## 2. New findings

| ID | Level | Issue | Location | Evidence | Verdict |
|---|---|---|---|---|---|
| N1 | 🟡 Medium | `PlayerUI.OnDestroy()` hides `NetworkBehaviour.OnDestroy()`, so NGO's base cleanup of the behaviour and its NetworkVariables never runs for `PlayerUI` | `UI/PlayerUI.cs:61` | Compiler warning **CS0114**, repeated at every compile in `Editor.log` (e.g. line 271) | Confirmed (warning). The runtime effect is plausible: leaked NetworkVariables or warnings on scene unload. |
| N2 | 🟢 Low | NetworkVariables are written before spawn: `PlayerSession.Initialize` sets `ClientId`, `TeamId` and `m_lifeCurrent` before `SpawnWithOwnership` | `SessionManager.CreateSession` (`MultiplayerScripts/SessionManager.cs:229`) → `PlayerSession.cs:519, 531` | `Editor.log:1178, 1276`: "NetworkVariable is written to, but doesn't know its NetworkBehaviour yet" | Confirmed (warning). The values do reach clients (team materials applied correctly in the log), so it's noise plus fragility. |
| N3 | 🟢 Low | Client-side `CanPlayerMove` / `CanPlayerSelectCardTargets` read `m_isResolvingCards` / `m_isWaitingRespawn`. These are plain private `bool`s that are only set on the server, so on a pure client they're always `false`. | `GameMaster.cs:83-84`; `GameMaster.GameLoop.cs:14-28` | After C3 the server drops those packets correctly. The client UI still lets the player try to move or target during resolution. | Confirmed |
| N4 | 🟢 Low | Dead members: `ValidateCardOwner` has no callers after C4. `m_cardResolveDelay` is assigned but never used. | `PackageManaging.cs:442`; `GameMaster.cs:66` (CS0414 in `Editor.log`) | grep and compiler warning | Confirmed |
| N5 | 🟢 Low | Log string is missing the `$` interpolation prefix, so it prints `{_package.ClientId}` literally | `GameMaster.Processing.cs:40` | Code | Confirmed |
| N6 | 🟢 Low | After a decisive (game-ending) activity, card packages already queued in the same action phase are still resolved before the phase exits | `GameMaster.GameLoop.cs:268-291` (inner `break` → `MoveCardOnGraveyard` → `while (m_cardInputQueue.Count > 0)` continues) | Code. This replaces the withdrawn M2. | Confirmed (practically rare: needs 2 packets queued in one frame) |
| N7 | 🟢 Low | `PlayerSession.GetCharacter()` assumes the object behind `CharacterObjectId` has a `PlayerController`. If it doesn't, `m_currentCharacter.GetComponent` throws on the server. | `InGame/PlayerSession.cs:540-548` | Code. The server zeroes `CharacterObjectId` on death (`Processing.cs:359`), so this only happens with a stale ID and network-ID recycling (`RecycleNetworkIds: 1`). C3 now calls `GetCharacter()` for every movement packet. | Plausible (latent) |
| N8 | ⚪ Info | Documentation drift | `README.md` | It describes a `Result` scene and a "destroy all enemy blocks" win, which don't exist (H5, feature gaps). There is a stray `test.md` at the repo root. Line references in `Docs/AI/Architecture/*.md` for `GameMaster.Processing.cs` and `GameMaster.PackageManaging.cs` are now off by up to ~30 lines, and the movement path in `GameLoop.md` no longer describes the server checks. | Confirmed |
| N9 | ⚪ Info | Intended behaviour change from C4: the first confirmation in `CardSelection` is final | `PackageManaging.cs:189-193` | Before, resending overwrote the board. Now it's ignored. The client already clears its local selection on confirm (`PlayerSession.ConfirmTurn` → `ClearLocalCardSelection`). | For the designer to acknowledge |

---

## 3. Earlier issues: status at `44d858f`

### Resolved or withdrawn

| ID | Status | Reason |
|---|---|---|
| M5 | ✅ Fixed by C3 | `InputQueueUpdate` no longer touches `PlayerObject`. It uses `session.GetCharacter()` with a null check (`Processing.cs:69-74`). |
| M2 | ❌ **Withdrawn** (false positive in report #1) | Re-traced: after the inner `break` (`GameLoop.cs:272`), the next outer iteration evaluates `IsAnyPlayerDead()` (`:113`). That is true for the dead player whose character was despawned. `IsGameFinished()` is then true, so the loop `break`s within about one frame. The match doesn't idle for the turn time. The real residual is N6. |

### 🟠 High: all still open (code unchanged)

| ID | Issue | Current location | Notes |
|---|---|---|---|
| H1 | Any exception stops the `GameLoop` coroutine permanently | `GameMaster.GameLoop.cs:44` | No exception boundary yet |
| H2 | Empty deck throws (`ElementAt(0)`) | `GameMaster.Card.cs:151` | ~80 cards, 4+ draws per round, so it's reachable in a long match |
| H3 | Block heal uses `_targetCharacter.m_hpMax` | `GameMaster.Processing.cs:237` (was `:209`) | Latent. No heal-block card exists yet. |
| H4 | `CheckActivity` dereferences `_ownerPlayer` before its null check | `Processing.cs:97` (check at `:99`) | Latent |
| H5 | No end-of-match flow | `GameLoop.cs:53-61`, `FindLostPlayer` `:467` | Unchanged |
| H6 | Opponent disconnect mid-match isn't handled | `MultiplayerManager.cs:232` | **Now seen at runtime:** `Editor.log:4385` "Remote client disconnected. Host continue" |
| H7 | Disconnect cleanup despawns on clients and runs several times | `GameMaster.ClearupAfterDisconnect` `GameMaster.cs:301` → `PlayerSession.ClearSession` `:619-625`; callers `MultiplayerManager.cs:277, 366, 396, 1213` | Unchanged |
| H8 | `CardData.OnValidate` wipes activities 1–3 | `InGame/CardData.cs:92-100` | Unchanged |
| H9 | `UltraRare` deck fill writes `_nonUniqueList[i]` on an empty list | `GameMaster.Card.cs:262` | Latent |

### 🟡 Medium: still open

| ID | Issue | Current location |
|---|---|---|
| M1 | Shared `m_timer` is reset by the nested respawn wait. The active player's remaining turn time changes after a death. | `GameLoop.cs:530` |
| M3 | Lava immunity after network-ID recycling | `LavaKillZone.cs:13, 71, 86` |
| M4 | `IsHost` used where `IsServer` is meant | `GameMaster.cs:123`; `PackageManaging.cs:50` |
| M6 | `UpdatePlayerHP` assumes a spawn point exists | `Processing.cs:356` (was `:328`) |
| M7 | Target validation accepts null targets (`OpponentBlock`/`SelfBlock` fall through to `true`; `null == null` for player targets) | `Processing.cs:130-167` |
| M8 | Pure-client NRE: `m_currentSelectedBlock.GetSession()` in `TryConfirmTarget` | `PlayerSession.cs` (`TryConfirmTarget`) |
| M9 | `DrawCard(null)` for a non-player target | `Processing.cs:245` |
| M10 | Async network errors aren't handled (`StartHost`, `JoinLobbyByJoinCode`, `async void HearthbeatLobby`) | `MultiplayerManager.cs:403`, `:730-756` |
| M11 | Join-by-code is unusable (`m_joinCodeInput` is never set) | `MultiplayerManager.cs:28, 740` |
| M12 | No in-game language selection (`SettingsScene` is missing) | `SceneFlowManager.LoadSettingsScene` |
| M13 | Hard-coded user-visible strings | `MultiplayerManager.cs:485` `"MyLobby"`, `"Host_"`/`"Client_"`; `LobbyListUI.cs:209`; `LobbyPlayerItemUI.cs:50` |
| M14 | Client dereferences `m_cardData` before `SendCardDataClientRpc` arrives | `PlayerSession.CheckPackage`, `NetworkCard.CheckActiveTaergetSelection`, `CardViewUI.OnPointerClick` |
| M15 | Unbounded hand growth | `DrawCardPhase` `GameLoop.cs:449` |
| M16 | `SessionManager.Instance` is never cleared (no `OnDestroy`) | `SessionManager.cs` |

### 🟢 Low: still open

L1–L10 are unchanged from report #1. The only line change is L2: `Random.Range(0, 2)` is now at `GameMaster.Card.cs:175`.

L9 matters more after C1. `GameMasterPrefab.prefab:74` has `m_debugMode: 1`, which keeps the debug HP key active in every development build.

### ⚪ Info

The naming and typo list is unchanged (report #1). Add the new log text `"Handle Packege in Update"` (`Processing.cs:21`).

---

## 4. Unknowns: updated

| ID | Question | Status |
|---|---|---|
| U1 | Does the project compile? | ✅ **Answered for the Editor:** 0 errors in `Editor.log` (state up to `fb0edb9`). ❓ The C4 commit compile isn't logged, and the **player build has never been run**. |
| U2 | Nested NetworkObject (`SpawnPoint` inside `BlockBody`) | ❓ No NGO nesting warning appears in the logs. The match did run on the clone (blocks spawned and despawned 80×). Weak evidence that it works. |
| U3 | Null `NetworkBehaviourReference` | ❓ No related errors in the logs. Not explicitly tested. |
| U4 | `DontDestroyWithOwner` on `PonyBase` and `BlockBody` | ❓ Not inspected |
| U5 | Unresolved network-prefab GUID `e651dbb3…` | ❓ No "missing prefab" message in the logs. Still not inspected. |
| U6 (new) | The clone's log shows `GameScene` loaded and `CardWorldViewManager.Awake` called **twice** in one Editor session (`Editor.log:1117-1311`). The first instance ran server code (session creation); the second reports `IsServer: False`. | ❓ Probably two separate host and client runs without a Play Mode exit between them in the log, but this isn't proven. Check whether a single client join causes a double scene load. |
| C6 | Does the cast button component load? | 🟡 Probably yes (no missing-script message). Confirm in the Inspector. |

---

## 5. Suggested next steps (in order)

1. **Close the verification gaps for C4–C6:**
   - recompile after `44d858f`
   - run a Windows development build (C5)
   - do the Inspector check (C6 Step 1)
   - run the ParrelSync tamper checks for C1–C4, and record the results in `Docs/AI/Reviews/`
2. **Keep the match loop alive:** H1, H2, H4, H3, H9, M6, M9. These are small guards, and each one prevents a permanent freeze.
3. **Match lifecycle:** H5 (game over and result), H6 (forfeit on disconnect), H7 (one server-only cleanup path). These need a design decision and en/ru/ja keys.
4. **Quick hygiene:** N1 (`override` `OnDestroy` and call `base`), N5, N4, M4. Then refresh the `Docs/AI/Architecture` line references (N8).

## Sources

- **Code read in full:**
  - `Assets/Scripts/InGame/GameMaster/GameMaster.Processing.cs`
  - `GameMaster.PackageManaging.cs`
  - `GameMaster.GameLoop.cs`
  - `GameMaster.Card.cs`
  - `InGame/PlayerController.cs` (lines 120–300)
  - `InGame/PlayerTurnSelection.cs`
- **Read in part:**
  - `InGame/PlayerSession.cs` (100–235, 490–548)
  - `MultiplayerScripts/SessionManager.cs` (95–260)
  - `UI/PlayerUI.cs` (55–70)
  - `InGame/GameMaster.cs` (grep plus 140–175)
- **Diff and history:** `git log`, and `git diff 15b439b~1 44d858f`.
- **Logs:**
  - `%LOCALAPPDATA%\Unity\Editor\Editor.log`: project `C:/Lesson/FallenStar-Spellcasters_clone_1`, Unity 6000.3.13f1, last written 2026-10-18 11:57.
  - `Editor-prev.log`: project `C:\Lesson\FallenStar-Spellcasters`.
