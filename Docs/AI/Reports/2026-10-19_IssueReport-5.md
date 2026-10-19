# Issue Report #5: FallenStar – Spellcasters (re-audit after H10, MANA1–MANA3)

- **Date:** 2026-10-19
- **Baseline:** `HEAD` = `058fa63` ("Mana task completed"). The working tree was clean at the start of the audit.
- **Previous reports:** [#1](2026-10-18_IssueReport.md) (`1f6d2f1`), [#2](2026-10-18_IssueReport-2.md) (`44d858f`), [#3](2026-10-18_IssueReport-3.md) (`ad3a46f`), [#4](2026-10-18_IssueReport-4.md) (`f08e854`)
- **Method:**
  1. Read the agent instructions: `CLAUDE.md`, `AGENTS.md`, `AI_CONTEXT.md`, and the existing `Docs/AI` documents.
  2. Read the full code diff `f08e854..058fa63` (commits `ade4aed` H10/MANA1, `5eec046` MANA2, `058fa63` MANA3) and compared it with each task spec.
  3. **Fresh full read** of the core runtime code, not only the diff:
     - all `GameMaster*.cs` partial files
     - `PlayerSession`, `PlayerController`, `PlayerUI`, `NetworkCard`, `LavaKillZone`
     - `SessionManager`, `MultiplayerManager`, `Bootstrap`, `SceneFlowManager`
     - `CardWorldViewManager`, `ResultMenuUI`, `MatchResultData`, `CardData`, `CardDatabase`
  4. Checked these assets:
     - `ResultScene.unity`: component wiring, EventSystem, button binding
     - `UI_table` in en/ru/ja: 58 keys, none empty
     - card data assets and `CardDataBase.asset`
     - button bindings in `GameScene`, `LobbyScene` and `PlayerUIPrefab`
     - `m_debugMode` flags on prefabs
  5. Read the Editor logs: `%LOCALAPPDATA%\Unity\Editor\Editor.log` (08:41) and `Editor-prev.log` (08:31). Both are from the ParrelSync clone, which shares `Assets` with the main project. Also read Codex's verification output under the git-ignored `build/` folder.
- **I didn't run a build or Play Mode.** No code, scenes, prefabs or settings were changed. The only changes are this report and removing completed task files (§6).

The severity levels and verdicts are the same as in reports #1–#4. New findings continue the numbering from report #4, starting at N18.

## Summary

| | |
|---|---|
| Tasks completed since #4 | **H10** ✅, **MANA1** ✅, **MANA2** ✅, **MANA3** ✅. All four match their specs. |
| Closed without a code change | **C6**: not reproducible. Runtime evidence shows the cast button works (§1.1). |
| Earlier unknown answered | **U1 / C5**: Codex built a Windows 64-bit player successfully on 2026-10-18 (§4) |
| Compile state | 0 `error CS`. Only the known warnings CS0114 (N1, now at `PlayerUI.cs:74`) and CS0414 (N4) |
| 🟠 High still open | 2: **H2** (empty deck), **H3** (block heal) |
| 🟡 New Medium | 2: **N18** (deaths outside the action phase are handled late), **N19** (a running match can't be left) |
| 🟢 New Low | 4: N20, N21, N22, N23 |
| ⚪ New Info | 2: N24 (file encodings), N25 (free discards buy turn priority) |
| Task files in `Docs/AI/Tasks/` | The five old specs were removed as done or closed. **Five new specs** were written after the owner's decisions (§6). |

### Owner decisions (2026-10-19, after the first version of this report)

| ID | Decision | Task |
|---|---|---|
| **H2** | When the deck is empty, the server first **deletes every graveyard object**, then **creates a new deck without the rarest cards** (`UltraRare` and `Unique`) | [TASK-H2](../Tasks/TASK-H2_DeckRefillWithoutRarestCards.md) |
| **H3** | A block healed over its cap works **like a character**: 50 % of the overflow becomes extra HP above the maximum | [TASK-H3](../Tasks/TASK-H3_BlockHealOverCap.md) |
| **N19** | Leaving a running match **needs a confirmation** dialog and **counts as a defeat** | [TASK-N19](../Tasks/TASK-N19_LeaveMatchWithConfirmation.md) |
| **N18, M1** | When a player dies in any phase, **check the respawn conditions first**. If a respawn is possible, **pause the current phase** (timer included), respawn, then resume the same phase. If not, apply the elimination or finish rules at once. | [TASK-N18_M1](../Tasks/TASK-N18_M1_DeathChecksAndRespawnTimer.md) |
| **N25** | Free discards that still count toward turn order are **intended**. Closed. | — |
| N14 | Fix as proposed in §5 | [TASK-N14](../Tasks/TASK-N14_ReturnToLobbyWhenMatchCannotStart.md) |

**Overall:** the match lifecycle is complete:
- lobby → match → result screen → main menu
- forfeit when a player disconnects
- guarded game loop
- mana model with server validation

The most visible remaining problems are:
- **N19:** a player can't leave a running match except by killing the game.
- **N18:** a lethal lava fall that finishes after the turn ends can delay the end of the match by up to about 70 seconds.
- **H2:** the deck runs out in long matches and aborts the match to the lobby with no result.

---

## 1. Review of implemented tasks

| Task | Commit | What changed | Matches spec? | Verdict | Notes |
|---|---|---|---|---|---|
| **H10** | `ade4aed` | New `UI/ResultMenuUI.cs`. `ResultScene.unity` now has a Canvas, an `EventSystem` with `InputSystemUIInputModule`, title, reason and button. 8 new `UI_table` keys in en/ru/ja. | Yes, step by step (§4.1–4.3). All 7 `LocalizedString` fields and both `LocalizeStringEvent` references are assigned (`ResultScene.unity:1157-1218`). `onClick` → `ResultMenuUI.OnClickMainMenu` (`:932-933`). The double-click guard and the `SceneFlowManager` null check are present. | ✅ **Fixed** (static) | There's no log evidence of the button being pressed or of a second match after returning to the menu (§9 checks). The commit message says "checked". |
| **MANA1** | `ade4aed` | `PlayerSession.m_manaCapacity` / `m_manaCurrent` (server write, everyone reads). Config fields base 2 / step 2 / cap 20. Initialized in `StartNewGame` after the 2-player check. `ApplyRoundManaGrowth` (single rule, XML comment) runs at the start of `RoundBreakPhase`. `AddMana` clamps to `[0, capacity]`. | Yes | ✅ **Fixed** (static) | `Editor.log:3357-5836` shows two full rounds with accepted confirms. The mana values themselves aren't logged. |
| **MANA2** | `5eec046` | Server: the play cost is checked after the C4 ID validation and before `m_isConfirmed`. On rejection the server clears both lists, records `ManaOverBudget`, sends an owner-targeted `SelectionRejectedClientRpc` and calls `ResetCardSelectionTimer` (limits: 1 per phase, 3 per match). On acceptance it calls `AddMana(-cost)`. Client: `SelectCardToPlay` gating and `SelectedPlayCost`. | Yes, including the review checklist. The phase counter is cleared in `CardSelectionPhase` (`GameLoop.cs:525`) and the match counter in `StartNewGame` (`GameMaster.cs:517`). The reject path returns before any board write. | ✅ **Fixed** (static), plus 30 local-host Play Mode assertions by Codex | Remote-client delivery of the rejection RPC is still untested (Codex report §5). |
| **MANA3** | `058fa63` | `PlayerUI`: own and opponent mana labels, cached so text is only rebuilt on a change or a locale switch. Localized "not enough mana" message for 2 unscaled seconds. `ClearLocalUI` resets everything. Three TMP texts added to `PlayerUIPrefab`. 3 keys in en/ru/ja. | Yes. The optional §4.4 (feedback on clicking an unaffordable card) wasn't done; the spec allows that. | ✅ **Fixed** (static) | The 960-line font asset diff is dynamic-atlas glyph caching, as Codex reported. No configuration change. |

### 1.1 C6: closed as not reproducible

TASK-C6 Step 1 asked for an Inspector check that the cast-button component (`ConfirmSendButtonUI1` in `ConfirmSendButton.cs`) loads. There is now stronger evidence than an Inspector check:
- `PlayerSession.CardInputServerRpc` has exactly **one** caller: `TryHandlePackage`. `TryHandlePackage` is called only from `ConfirmSendButtonUI1.TryConfirmSend`, and that is wired only in `PlayerUIPrefab.prefab:1573` (checked with grep across all `.cs`, `.prefab` and `.unity` files).
- `Editor.log` contains **3 × "Handle finished"** (a successful `CardInputServerRpc` → `HandleCardControlPackage`), **12 × "Process called"** (3 cards × 4 activities) and **3 × "moved to graveyard"**.

So the button component loads and casts cards. Per the task's own Step 1 rule, the task is closed with no change. The stale `m_EditorClassIdentifier` strings in the prefab are harmless.

---

## 2. New findings

| ID | Level | Issue | Location | Evidence | Verdict |
|---|---|---|---|---|---|
| **N18** | 🟡 Medium | **Deaths that happen outside `PlayerAction` are only handled at the next action phase.** Death checks exist only inside `PlayerActionPhase` (`:175`, `:330`), and `GameLoop` checks `IsGameFinished()` only at the top of each round (`:102`). A lava death (`ServerKillPlayerByHazard`) can happen in any phase. Typical case: the attacker casts Fireball on the block under the opponent and ends the turn at once, while the opponent is still falling. If the attacker was **last** in the turn order, the opponent hits the lava during `RoundBreak`. Then:<br>• **Last life:** the match continues through RoundBreak (5 s), DrawCards (~3 s) and CardSelection (up to 60 s, the dead player can still select cards). Only the next `PlayerActionPhase` notices and ends the match, about **70 s late**.<br>• **Not the last life:** the respawn wait (up to 30 s) runs inside the first action phase of the next round, which may be the opponent's turn. Combined with M1, it changes that player's turn timer. | `GameMaster.GameLoop.cs:102`, `:175-194`, `:521-573` (`CardSelectionPhase` and `DrawCardPhase` have no death check); `GameMaster.Processing.cs:370-404` | Code trace | Plausible. The code path is certain; how long the fall takes needs a Play Mode check. Direction: check `IsGameFinished()` / `IsAnyPlayerDead()` at phase boundaries, or let the lava kill trigger a finish check. |
| **N19** | 🟡 Medium | **A running match can't be left.** `GameScene` and `PlayerUIPrefab` have no leave, surrender or disconnect button: the only bindings are `ConfirmSelect`, `TryConfirmSend` and `ConfirmTurn`. `MultiplayerManager.Update` toggles `DisconnectMenuUI` with Tab, but that menu exists only in the disabled legacy scenes `SampleScene` and `MultiplayerScene`, so Tab does nothing. The only way out is to kill the process. The opponent then gets a forfeit win through H6. | `MultiplayerManager.cs:138-158`; `Assets/Scenes/GameScene.unity` and `PlayerUIPrefab.prefab` (button bindings); `DisconnectMenuUI` placement (GUID search) | Assets and code | Confirmed. This is a feature gap. A "Leave match" button would need an owner decision: forfeit through the H6 path, a confirmation dialog, and en/ru/ja keys. |
| **N20** | 🟢 Low | **`PlayerSession.CheckPackage` has two defects.**<br>(a) **NRE:** select a board card, confirm a target, click the card again to deselect it (`PlayerUI.OnCardClicked` → `ClearSelectedCard` → `m_selectedCard = null`, but the package keeps its card ID), then press Send. `m_selectedCard.m_cardData` throws in the UI callback.<br>(b) **Inverted target rule:** it rejects a zero target for every type *not* in {AnyPlayer, AnyPlayerBlock, OpponentBlock, OpponentPlayer, RandomBlockSelectedPlayer, None}. So `All*`, `RandomSelfBlock` and `RandomOpponentBlock` cards, whose correct target is 0 (`AutoSelectTarget`), can never be sent. Meanwhile an untargeted `OpponentBlock` or `AnyPlayer` package passes and is only rejected by the server. | `InGame/PlayerSession.cs:993-1014`; trigger path `UI/PlayerUI.cs:305-310` | Code | Confirmed. (a) is client-only: the exception is logged and nothing else breaks. (b) is latent, because no current card uses `All*`/`Random*`. |
| **N21** | 🟢 Low | **A late selection confirm becomes "end turn".** `ConfirmTurnServerRpc` is interpreted by the **server's** current phase (RP2). A confirm sent in the last moments of `CardSelection` that arrives after the server has moved to `PlayerAction` (it does this in the same frame the timer expires) goes to `HandlePlayerActionConfirm`. If that player is first in the turn order, their whole turn ends at once: no movement and no casting that round. | `GameMaster.PackageManaging.cs:204-249`; `PlayerSession.cs:129-159` | Code | Plausible. The window is the network latency at timeout. Direction: send the client's phase or round in the RPC and drop stale confirms. |
| **N22** | 🟢 Low (latent) | **Character HP is initialized one frame after spawn.** `PlayerController.OnNetworkSpawn` starts `DelayedInit`, which runs `Init()` (HP 100/100) a frame later. Until then `m_hpCurrent = m_hpMax = 1` (the NetworkVariable defaults). After a mid-card respawn, the remaining activities of the same card resolve in the same frame (`GameLoop.cs:322-347`) against 1 HP. Any change made before `Init` is then overwritten to 100. | `InGame/PlayerController.cs:17-18`, `:61-63`, `:92-101`, `:127-138` | Code | Confirmed (latent). Every current card has only activity 0 set, so this isn't reachable yet. The HUD shows "1" for one frame. |
| **N23** | 🟢 Low | `Keyboard.current` is used without a null check: every frame in `MultiplayerManager.Update` while in the lobby or a match, and in `PlayerController.SendMovementInput`. On a machine without an active keyboard device this throws every frame. | `MultiplayerManager.cs:142`; `PlayerController.cs:167-185` | Code | Confirmed (low probability on PC) |
| **N24** | ⚪ Info | **Three scripts aren't UTF-8.** Their Russian comments are in a legacy code page and show as garbage: `InGame/NetworkCard.cs`, `InGame/PlayerCardfWorldArea.cs`, `Manager/CardWorldViewManager.cs`. UTF-8-based tools refuse to edit them or may corrupt them, and that includes AI agents. | the 3 files | `iconv` check | Confirmed. Direction: one owner-approved commit that converts them to UTF-8 with no logic change. Until then, specs that touch these files must tell Codex to keep the encoding. |
| **N25** | ⚪ Info (design) | **Free discards still buy turn priority.** D3 makes discards free; D4 keeps turn order by `TotalCost()`, which adds play **and** discard costs. So a player can discard expensive cards at no mana cost to move first in the round. | `InGame/PlayerTurnSelection.cs` `TotalCost`; `GameMaster.GameLoop.cs:813-819` | Code | ✅ **Closed:** the owner confirmed on 2026-10-19 that this is intended. |

**Notes added to existing items (no new ID):**
- **M10 now has runtime evidence.** `Editor.log:1024`: `LobbyService.GetLobbyAsync` threw a `NullReferenceException` inside the SDK. `RefreshCurrentLobby` catches only `LobbyServiceException`. The start-game coroutine logged the faulted task (`MultiplayerManager.cs:1156`) and **continued with stale lobby data** to load `GameScene`.
- **L6:** after a player leaves, `CardWorldViewManager.Update` never sees equal session counts again, because destroyed sessions stay in `m_registeredSessions`. It runs `RegisterAllSessions`, `FindAreas` (`FindObjectsByType`) and `RefreshAllSessions` every frame until the scene changes, which is about 3 s with H6.
- **L10:** `CardData.cs:1` has an unused `using Unity.Services.Matchmaker.Models;`.

---

## 3. Status of earlier issues

### Resolved or closed since #4

| ID | Status |
|---|---|
| H10 | ✅ Fixed (`ade4aed`). The result screen soft lock is gone. |
| C6 | ✅ Closed, not reproducible (§1.1) |
| U1 / C5 | ✅ Answered for the 2026-10-18 baseline (§4) |
| N9 | ✅ Superseded. "First confirm is final" became "first **valid** confirm is final" (MANA2 §6, owner decision). |

### 🟠 High: still open

| ID | Issue | Current location | Note |
|---|---|---|---|
| H2 | Empty deck: `m_deck.ElementAt(0)` throws before the null check | `GameMaster.Card.cs:151` | The deck is 80 cards shared by both players. A round draws 4, plus 2 per Clarity cast. H1 turns the exception into `AbortMatch`, which sends **both players to the lobby with no result**. **Owner decision:** build a new deck without the rarest cards → [TASK-H2](../Tasks/TASK-H2_DeckRefillWithoutRarestCards.md). |
| H3 | A block heal over the cap uses `_targetCharacter.m_hpMax` (null for a block target) | `GameMaster.Processing.cs:224` | Latent: no heal card targets a block. H1 catches it. **Owner decision:** same overflow rule as characters → [TASK-H3](../Tasks/TASK-H3_BlockHealOverCap.md). |

### 🟡 Medium: still open (code unchanged)

| ID | Issue | Current location |
|---|---|---|
| M1 | The shared `m_timer` is reset by the nested respawn wait. Made worse by N18. | `GameMaster.GameLoop.cs:640` |
| M3 | Lava immunity after network-ID recycling (`m_killedCharacterIds` is never cleared) | `LavaKillZone.cs` |
| M4 | `IsHost` used where `IsServer` is meant | `GameMaster.cs:141`; `GameMaster.PackageManaging.cs:50` |
| M6 | `UpdatePlayerHP` assumes a spawn point exists | `GameMaster.Processing.cs:343` |
| M8 | Pure-client NRE: `m_currentSelectedBlock.GetSession()` in `TryConfirmTarget` | `PlayerSession.cs:821` |
| M9 | `DrawCard(null)` for a non-player target | `GameMaster.Processing.cs:232` |
| M10 | Async network errors aren't handled. Now seen at runtime (see §2 notes). | `MultiplayerManager.cs:531-546` (`HearthbeatLobby`), `:565-637` (`StartHost`), `:849-862` (`RefreshCurrentLobby`), `:863-906` (`JoinLobbyByJoinCode`) |
| M11 | Join by code is unusable (`m_joinCodeInput` is never assigned) | `MultiplayerManager.cs:28` |
| M12 | No in-game language selection (`SettingsUI` is in no scene; `SettingsScene` doesn't exist) | `SceneFlowManager.LoadSettingsScene` |
| M13 | Hard-coded user-visible strings | `MultiplayerManager.cs:613` (`"MyLobby"`), `:1306` (`"Host_"`/`"Client_"`); `LobbyListUI`; `LobbyPlayerItemUI` |
| M14 | The client dereferences `m_cardData` before it arrives | `PlayerSession.cs:997`; `NetworkCard.CheckActiveTaergetSelection`; `CardViewUI.cs:85` |
| M15 | Unbounded hand growth (design question) | `GameMaster.GameLoop.cs:559` (`DrawCardPhase`) |
| M16 | `SessionManager.Instance` is never cleared. Unity's null semantics hide most of the effect. | `SessionManager.cs` |
| N1 | `PlayerUI.OnDestroy` hides `NetworkBehaviour.OnDestroy` (CS0114) | `UI/PlayerUI.cs:74` |
| N14 | A disconnect before the match loop starts leaves the host stuck in `GameScene` | `MultiplayerManager.cs:1225-1267`; `GameMaster.cs:562-569` |

### 🟢 Low and ⚪ Info: still open

- **N2:** NetworkVariables written before spawn (`SessionManager.cs:235` → `PlayerSession.Initialize`)
- **N3:** client-side `CanPlayerMove` / `CanPlayerSelectCardTargets` read server-only flags (`GameMaster.GameLoop.cs:14-38`)
- **N4:** dead `ValidateCardOwner` (`PackageManaging.cs:487`); unused `m_cardResolveDelay` (`GameMaster.cs:73`, CS0414)
- **N5:** missing `$` in a log string (`GameMaster.Processing.cs:40`)
- **N6:** queued card packages still resolve after a decisive activity (`GameMaster.GameLoop.cs:330-353`)
- **N7:** `GetCharacter` assumes the object has a `PlayerController` (`PlayerSession.cs:604-610`)
- **N12:** `RunGuarded` would spin on a yielded `CustomYieldInstruction` (`GameMaster.GameLoop.cs:82`). Still latent.
- **N16:** the leaving player's suspicious-activity summary isn't logged (`GameMaster.Player.cs:21`)
- **N17:** the host's HUD goes blank before `ResultScene` after a forfeit (`SessionManager.cs:183-186`)
- **L1–L4, L6–L10:** unchanged from report #1. **L9:** `m_debugMode: 1` is still set on `GameMasterPrefab`, `PlayerSessionPrefab`, `SessionManager.prefab`, `CardWorldManagerPrefab`, `MultiplayerManager.prefab` and `ResultScene` (`ResultSceneController`). The debug HP key (Space) is therefore active in every Editor and development build.
- **N8 (documentation drift), growing:** `Docs/AI/Architecture/*.md` still has **0** mentions of `RunGuarded`, `AbortMatch`, `EndMatch`, `GamePhase.GameOver`, `ResultScene`, `ResultMenuUI`, `MatchResultData`, `HandlePlayerLeft`, `LeaveMatchToResultScene` or `SuspiciousActivityLog`. The scene-flow table still says "End of match → Result: Not implemented". The mana sections were kept current by Codex. `README.md` still describes a "destroy all enemy blocks" win, and `test.md` is still at the repo root.
- **Naming and typos:** unchanged (report #1, ⚪ Info).

---

## 4. Unknowns: updated

| ID | Question | Status |
|---|---|---|
| U1 / C5 | Does a player build compile? | ✅ **Answered for the 2026-10-18 morning baseline.** `build/task-c5-verification-5498ad9b/` and `build/task-c1-verification-cb05f253/` (git-ignored) each contain a `FallenStar-Spellcasters.exe` from a batch-mode `-buildWindows64Player` run that exited with code 0 (11:15 and 11:35). Earlier reports missed these artifacts. ❓ **Not re-run** since H1, H5–H10 and MANA1–MANA3. A grep finds no runtime `using UnityEditor` / `NUnit` / `TestTools` today, so one fresh build should confirm it. |
| U2 | Nested `SpawnPoint` NetworkObject inside `BlockBody` | ❓ No warnings in the logs |
| U5 | Unresolved network-prefab GUID `e651dbb3…` | ❓ |
| U6 | Double `GameScene` load in the clone log | ❓ |
| U8 | Does `Shutdown()` leave no objects behind (H7 §9.2)? | ❓ No evidence |
| U9 | NGO `NetworkSceneManager.Dispose` NRE on Editor quit or domain reload | ❓ Seen again (`Editor-prev.log:6680`). It's inside NGO, during teardown. Likely harmless. |
| U10 | Does the H6 forfeit work at runtime in each phase? | ❓ No `OpponentDisconnected` result in the logs |
| **U11** (new) | `The referenced script (Unknown) on this Behaviour is missing!` after **every** domain reload in the clone (`Editor-prev.log:5997, 6208, 6418, 6604`) | ❓ This started in the session where the temporary MANA2/MANA3 Editor tools were removed from `Assets`. Probably a leftover open window or an object in an open scene that referenced them. Check the Console in the **main** project. If it appears there, find the object with Edit → Select All and look for "Missing (Mono Script)". |
| **U12** (new) | Does mana replicate to a separate remote client, and does the rejection RPC reach a remote owner? | ❓ Codex tested both only with host-owned sessions (MANA2/MANA3 reports §5) |
| **U13** (new) | After result → main menu, does a second lobby and match work, and does it show the new result (H10 §7)? | ❓ No log evidence |

---

## 5. Suggested next steps (in order)

1. **Implement the new specs in this order** (they share files, so do them one after another):
   1. [TASK-H3](../Tasks/TASK-H3_BlockHealOverCap.md): block heal over the cap
   2. [TASK-H2](../Tasks/TASK-H2_DeckRefillWithoutRarestCards.md): new deck when the deck is empty
   3. [TASK-N14](../Tasks/TASK-N14_ReturnToLobbyWhenMatchCannotStart.md): host returns to the lobby when the match can't start
   4. [TASK-N18_M1](../Tasks/TASK-N18_M1_DeathChecksAndRespawnTimer.md): a death in any phase checks the respawn conditions; a possible respawn pauses and later resumes the current phase
   5. [TASK-N19](../Tasks/TASK-N19_LeaveMatchWithConfirmation.md): "Leave match" button with a confirmation dialog (counts as a defeat)
2. *(Folded into step 1.)*
3. **Run the manual check backlog** (the old specs are in git history, see §6):
   - H6 §9: forfeit in each phase (U10)
   - H7 §9.2: no leftover objects (U8)
   - H10 §9: the button, a second match after returning to the menu (U13), and ru/ja rendering
   - MANA1 §9 / MANA2 §9 / MANA3 §9 with a **real remote client** (U12)
   - MANA2 tamper test
   - an honest full match with 0 `[Suspicious]` lines
4. **One fresh Windows player build** on `HEAD` (U1).
5. **Hygiene bundle:** N1 (`override` + `base.OnDestroy()`), N4, N5, N12, N16, N20, N23. N24 (UTF-8 conversion) only with owner approval.
6. **Architecture docs refresh (N8).** Claude can do this directly under `Docs/AI/Architecture`, since documentation is in scope.
7. **Designer acknowledgement:** M15 (hand limit). N25 is closed (intended).

---

## 6. Task folder cleanup

I removed these specs from `Docs/AI/Tasks/`. They're still in git history (`git show 058fa63:Docs/AI/Tasks/<file>`). Their remaining manual checks are listed in §5, step 3.

| Removed | Reason |
|---|---|
| `TASK-H10_ResultSceneMenu.md` | Fixed (`ade4aed`), matches spec (§1) |
| `TASK-MANA1_ManaDataAndRoundGrowth.md` | Fixed (`ade4aed`), matches spec (§1) |
| `TASK-MANA2_ManaCheckAndSpendOnConfirm.md` | Fixed (`5eec046`), matches spec (§1) |
| `TASK-MANA3_ManaDisplayAndMessages.md` | Fixed (`058fa63`), matches spec (§1). The optional §4.4 wasn't done; that's allowed. |
| `TASK-C6_ConfirmSendButtonClassFileMismatch.md` | Closed as not reproducible, with runtime evidence (§1.1). No code change was needed. |

**New specs written after the owner's decisions (2026-10-19):**

| Added | Covers |
|---|---|
| `TASK-H3_BlockHealOverCap.md` | H3 |
| `TASK-H2_DeckRefillWithoutRarestCards.md` | H2. M9 is closed along the way: `DrawCard` gets a null-session guard. |
| `TASK-N14_ReturnToLobbyWhenMatchCannotStart.md` | N14 |
| `TASK-N18_M1_DeathChecksAndRespawnTimer.md` | N18 and M1 (same code area) |
| `TASK-N19_LeaveMatchWithConfirmation.md` | N19 |

The research documents (`Docs/AI/Research/`) are kept, because they're reference material, not tasks. `RESEARCH-H6C` still describes reconnect as a possible future feature on top of H6. Their links to removed task files point to git history.

## Sources

- **Diff:** `git diff f08e854 058fa63`
- **Code read in full at `058fa63`:**
  - `GameMaster.cs`, `GameMaster.GameLoop.cs`, `GameMaster.PackageManaging.cs`, `GameMaster.Processing.cs`, `GameMaster.Card.cs`, `GameMaster.Player.cs`, `GameMaster.Spawn.cs`
  - `PlayerSession.cs`, `PlayerController.cs`, `PlayerUI.cs`, `NetworkCard.cs`, `LavaKillZone.cs`, `CardWorldViewManager.cs`
  - `SessionManager.cs`, `MultiplayerManager.cs`, `Bootstrap.cs`, `SceneFlowManager.cs`
  - `ResultMenuUI.cs`, `ResultSceneController.cs`, `MatchResultData.cs`, `CardData.cs`, `CardDataBase.cs`, `PlayerTurnSelection.cs`, `LobbyControlUI.cs`, `ConfirmSendButton.cs`, `ConfirmTargetButtonUI.cs`
- **Assets:**
  - `ResultScene.unity`, `GameScene.unity` (button bindings, spawn points), `LobbyScene.unity` (button bindings)
  - `PlayerUIPrefab.prefab`, prefab `m_debugMode` flags
  - `UI_table` (shared, en, ru, ja)
  - `Assets/Card/Data/*.asset`, `CardDataBase.asset`
- **Logs:**
  - `%LOCALAPPDATA%\Unity\Editor\Editor.log` (clone, 08:41) and `Editor-prev.log` (clone, 08:31)
  - `build/task-*-verification*/` (Codex build and harness output)
