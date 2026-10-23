# TASK-N33_N34_N35: Code hygiene: remove dead code, keep release logs quiet, format the HP and lives texts only on change

- **Source:** [Issue Report #12](../Reports/2026-10-22_IssueReport-12.md) §4: N33, N34 and N35. They've been open since reports #8–#10, with the locations re-checked at `6d89a09`.
- **Severity:** 🟢 Low, all Confirmed. Maintainability, release log noise and a small per-frame cost. No player-facing behavior changes.
- **Baseline:** `6d89a09`. Do this task **last**, after TASK-N54_N56_N57 and TASK-N55_N58_N32.
  - Those tasks change `PlayerUI.cs` (`ConfirmTurn`) and `GameMaster.Processing.cs` (line 139), so some line numbers below may have moved by a few lines.
  - **Find each item by the quoted code.** The quoted text is unique.
- **Implementer:** Codex
- **Scope:** about 18 scripts, two unused prefabs, one scene override, docs. No gameplay, networking-rule or UI-layout change.

## 1. Objective

1. **N33:** remove the code and assets that nothing uses.
2. **N34:**
   - A release build prints no debug chatter. The lines in §4.2 are behind the class's `m_debugMode`, or deleted.
   - Three gated messages get their missing `$`.
   - `PlayerUI` no longer reads `cardView.name` before its null check.
3. **N35:** `PlayerUI` formats the HP and lives texts only when a value or the language changes, as it already does for mana.

## 2. Current behavior

- **N33:** the dead code and assets are listed in §4.1. Each was checked with a project-wide `grep` (and a GUID search for the assets) at `6d89a09`: no caller and no reference.
- **N34:** 31 `Debug.Log` calls run without any debug flag. `DebugModeGuard` (L9) forces `m_debugMode` off in release builds, but these lines don't check it. So, for example:
  - "DespawnBlock" prints 50 times at each match end
  - "RepairCalled" prints for every card-data RPC
  - a lobby search prints 9 lines
  - every hand click prints twice
- **N35:** `PlayerUI.Update` (`UI/PlayerUI.cs:175-189`) runs every frame, even when nothing changed:
  - **alive:** two Smart-format calls, `m_labelHp.GetLocalizedString(m_hp)` and `m_labelLife.GetLocalizedString(m_life)`, written to the texts
  - **dead:** the dead text written again
  - The mana texts already do this only on change (`UpdateManaTexts`, `ResetManaTextCache`).

## 3. Relevant files and dependencies

| File | Items |
|---|---|
| `InGame/GameMaster.cs` | N33 fields; N34 `:626` |
| `InGame/GameMaster/GameMaster.GameLoop.cs`, `GameMaster.Processing.cs`, `GameMaster.PackageManaging.cs` | N34 |
| `InGame/PlayerController.cs` | N33 `m_isDead`, `GetCurrentSpeed`; N34 `$` |
| `InGame/PlayerSession.cs` | N33 three methods; N34 `:229` |
| `InGame/NetworkCard.cs` | N33 three RPCs and `DespawnCard`; N34 `:164` |
| `InGame/BlockController.cs`, `InGame/CardViewUI.cs`, `InGame/PonyColorScript.cs` | N34 |
| `InGame/CardPreviewController.cs` | N33 `TogglePreview` |
| `Manager/CardWorldViewManager.cs`, `Manager/SceneFlowManager.cs` | N34; N33 `LoadLoginScene` |
| `MultiplayerScripts/MultiplayerManager.cs` | N33 `KnowLobbies`; N34 |
| `Bootstrap.cs`, `GameSettingsPrefs.cs`, `UI/DisplaySettingsUI.cs` | N34; N33 |
| `UI/PlayerUI.cs` | N34; N35 |
| `Assets/Prefab/BootsTrap.prefab` (+ `.meta`), `Assets/Prefab/MultiplayerManager.prefab` (+ `.meta`) | N33: delete |
| `Assets/Scenes/GameScene.unity` | N33: the stale override |
| `Docs/AI/Architecture/Architecture.md` (§3.4, §10 A6), `Dependencies.md` | Remove the deleted APIs; N34 note |

**Dependencies to keep in mind**
- **Removing a `NetworkVariable` or an RPC changes the network layout of that `NetworkBehaviour`.** Both peers run the same build, so this is fine. Don't mix old and new builds in a match.
- **Removing a serialized field** (`GameMaster.m_cardViewWorldPrefab`, `SceneFlowManager.m_loginSceneName`) leaves an orphan line in the asset that stored it. Unity drops it the next time that asset is saved for another reason. **Don't re-save an asset just for this.**
- **`BootsTrap.prefab` is referenced only by `MultiplayerManager.prefab`,** and nothing references that one: no scene, prefab, Addressables group, `Resources` folder or `ProjectSettings` entry (GUID search). Delete them together.

## 4. Required changes

### 4.1 N33: dead code and assets

**Delete:**
1. **`GameMaster.cs`:** `m_cardViewWorldPrefab` (`:56`), `m_gameLoopCoroutine` (`:104`) and `m_phasesTimerCoroutine` (`:105`).
2. **`PlayerController.cs`:**
   - `m_isDead` (`:21`)
   - `GetCurrentSpeed()` (`:118-121`)
   - the commented `m_deadProccessed` / `m_isDead` block at the start of `Update` (`:138-147`)
   - the commented `//private bool m_deadProccessed` field
3. **`PlayerSession.cs`:** `RemoveCard(ulong)` (`:374-381`), `ClearBlocks()` (`:583-596`) and `ClearHand()` (`:748-761`).
4. **`NetworkCard.cs`:**
   - `SetFaceUpServerRpc`, `SetRotatedServerRpc` and `DespawnCardServerRpc` (`:71-87`, with their now-empty `#region Rpc`)
   - **`DespawnCard()`** (`:131-141`). Its only caller is `DespawnCardServerRpc`.
5. **`CardPreviewController.cs`:** `TogglePreview` (`:58-66`).
6. **`MultiplayerManager.cs`:** the `KnowLobbies` property (`:65-71`). The field `m_knowLobbies` stays, because `FindAvailableLobbies` uses it.
7. **`SceneFlowManager.cs`:** `LoadLoginScene()` (`:45-48`). The field `m_loginSceneName` then has no reader. Delete it too.
8. **`GameSettingsPrefs.cs`:** `HasLanguageCode()` (`:20-23`).
9. **`DisplaySettingsUI.cs`:** drop `FitsDisplay`'s unused `windowed` parameter (`:137-141`) and update its 4 calls (`:162`, `:181`, `:187`, `:205`). Keep the comment's meaning.
10. **The two prefabs:** `Assets/Prefab/BootsTrap.prefab` and `Assets/Prefab/MultiplayerManager.prefab`, with their `.meta` files. Before deleting, run the GUID search again: `0da4348fd2a840845b22d1dba1d3f2e9` and `50961c410a95804498b90c10a11ba4b0`.
11. **`GameScene.unity`:** remove the stale override `propertyPath: m_timeText` on PrefabInstance `282301674`, target `6674156206700678496` (`:499-502`).
    - That's the old `PlayerUI` field. **Not** `GameAnnouncerUI.m_timeText` at `:607`, which is live.
    - Use the Editor (**Prefab → Remove Unused Overrides** on the HUD instance), or delete those four YAML lines and let Unity reimport.
    - Change nothing else in the scene.

**Keep** (not N33, or still used): `m_knowLobbies`; `SessionRole.Spectator`; everything in `AuthManager` (§10, W3).

### 4.2 N34: release logs

**Rule:**
- In a class that **has** `m_debugMode`, wrap each line in `if (m_debugMode)`.
- In a class **without** one, delete the line.
- **Don't add new serialized fields.**
- `LogWarning` and `LogError` calls stay as they are.

| # | Location (at `6d89a09`) | Quoted code | Action |
|---|---|---|---|
| 1 | `Bootstrap.cs:113` | `"Bootstrap: NetworkManager recreated."` | Delete |
| 2 | `InGame/BlockController.cs:38` | `Debug.Log("DespawnBlock");` | Gate |
| 3–4 | `InGame/CardViewUI.cs:99`, `:105` | `"Card clicked: " + _nameKey`, `"Card alter clicked " + _nameKey` | Gate. **Keep the `_nameKey` local,** which the gated lines still use. |
| 5 | `InGame/GameMaster.cs:626-627` | `"GameMaster: new GameLoop started."` | Gate |
| 6 | `InGame/NetworkCard.cs:164` | `Debug.Log("RepairCalled");` | Delete |
| 7 | `InGame/PlayerSession.cs:229` | `Debug.Log("Rpc called");` | Gate |
| 8–9 | `InGame/PonyColorScript.cs:63`, `:83` | `"Applied body material index…"`, `"Applied mane material index…"` | Delete. **Keep** the two `"Invalid … material index."` warnings: they're N27, which you've parked. |
| 10–11 | `InGame/GameMaster/GameMaster.GameLoop.cs:485`, `:520` | `"DrawPhase started"`, `"Draw phase ended"` | Gate |
| 12 | `InGame/GameMaster/GameMaster.Processing.cs:113-116` | the `else { Debug.Log("Error"); }` branch in `CheckActivity`'s `OpponentPlayer` case | Delete the whole `else`. `PlayerActionPhase` already warns "Target validation failed". |
| 13 | `InGame/GameMaster/GameMaster.Processing.cs:138` | `Debug.Log("Process called");` | Gate |
| 14–15 | `Manager/CardWorldViewManager.cs:39-40`, `:56-57` | `"CardWorldViewManager Awake called on …"`, `"… Start called on …"` | Gate. In `Awake`, the gate must come **after** the existing `m_debugMode = DebugModeGuard.Allow(m_debugMode);`. |
| 16–24 | `MultiplayerScripts/MultiplayerManager.cs:765-836` | the nine `FindAvailableLobbies` lines ("query result count", "Found lobby before filters", the five "Skip lobby…" lines, "Lobby added to known list", "final known count") | Gate. **Keep each `continue`** outside the gate. |
| 25–26 | `MultiplayerScripts/MultiplayerManager.cs:1541-1542` | `"SetLobbyCode"`, `"Lobby code: " + …` | Gate both. Keep the method. |
| 27–30 | `UI/PlayerUI.cs:452`, `:469`, `:488`, `:505` | `"Card clicked: " + cardView.name`, `"Card deselected"` (×2), `"Card Alter clicked: " + cardView.name` | Gate. **Move** the two `cardView.name` lines below the `if (cardView == null) return;` that follows them. |

**The missing `$`** (these are already gated):
- `InGame/BlockController.cs:46`: `"HP changed {previous} to {current}"`
- `InGame/PlayerController.cs:70`: `"Spawned. Owner :{IsOwner} ClientId : {OwnerClientId}"`
- `InGame/GameMaster/GameMaster.PackageManaging.cs:258`: `"Confirm ignored. Current phase{ }"`. Make it `$"Confirm ignored. Current phase: {m_gamePhase.Value}"`.

**Not changed:**
- `ResultSceneController.cs:23` (already gated by its early return)
- `AuthManager.cs:148` (Editor only)
- every `Debug.Log` already inside an `m_debugMode` check

### 4.3 N35: HP and lives, only on change

- **In the alive branch,** keep the last displayed HP and lives, and write the two texts only when one of them differs. The existing `m_hp` / `m_life` fields, or new `-1` caches like the mana ones, are fine.
- **The dead branch:**
  - Write the dead text and the empty lives text only on entering that state.
  - Keep the per-frame `ClearSelectedCard()` call as it is (no behavior change).
- **Reset the caches** wherever `ResetManaTextCache()` runs: `OnSelectedLocaleChanged` (`:235-240`), `OnEnable` (`:628`) and `ClearLocalUI` (`:709`). A language change must reformat at once. Simplest: reset them inside `ResetManaTextCache` and rename nothing.
- **When neither branch applies** (character despawned, lives ≥ 0), keep today's behavior: the texts keep their last value.

### 4.4 Docs

- **`Architecture.md`:**
  - §3.4: the N34 sentence becomes "Release builds print no debug `Log` lines; warnings and errors remain."
  - §10: remove risk **A6** (the `NetworkCard` RPCs).
- **`Dependencies.md`:** remove the rows or mentions of the deleted members, if any: `TogglePreview`, `LoadLoginScene`, the `NetworkCard` RPCs, `KnowLobbies`.

## 5. Explicit non-goals

- **No renames,** including the typos (`AGENTS.md`; RP17).
- **Not in this task:**
  - `AuthManager`: `SingOut` and `SingOutAndClearCredentials` stay until you decide W3
  - N27 (parked), N29 (parked), N36
- **No other behavior change:**
  - no change to warnings or errors
  - no new debug flags
  - no change to `DebugModeGuard`
- **No refactoring** beyond the listed items: for example, `TryConfirmTarget`'s duplicated blocks stay.

## 6. Constraints and compatibility requirements

- **Serialized fields:** every remaining name is unchanged. Only `GameMaster.m_cardViewWorldPrefab` and `SceneFlowManager.m_loginSceneName` disappear. Their orphan YAML lines (in `GameMasterPrefab.prefab` and in the scene that holds `SceneFlowManager`) are harmless. Don't re-save those assets just to drop them.
- **Release behavior:** the game plays exactly as before. The only differences are fewer `Log` lines and HP / lives text written less often.
- **Unity 6 APIs, 0 warnings of our own.** Baseline copies go in `build/hyg2-verification/baseline/`.
- **Restore the dynamic font to `HEAD`** before handing over.

## 7. Acceptance criteria

1. **N33:**
   - `grep` finds none of the deleted members.
   - The project compiles with 0 errors and 0 new warnings.
   - Both prefab GUIDs have no references left.
   - GameScene's HUD instance shows no unused override for `m_timeText`.
2. **N34, in a release (non-development) player:**
   - A full match plus a lobby search prints none of the 31 lines.
   - With `m_debugMode` on in the Editor, the gated lines still print.
   - The three `$` messages print their values.
3. **N34:** `PlayerUI.OnCardClicked(null)` and `OnCardAlterClicked(null)` return without an exception.
4. **N35:**
   - Over 300 frames with no HP or lives change, the HP and lives texts are written at most once.
   - A change shows in the same frame.
   - A language switch reformats both texts at once.
   - Dead → respawn → alive shows the right text at each step.
5. **No other change:** a two-player match plays as before (§9).

## 8. Regression tests

- **AC1:** a scripted `grep` and GUID search, with the output in the report.
- **AC2:**
  - Build the release player, run the Settings-first test player or a scripted start, and search its `Player.log` for each of the 31 strings.
  - If a full match can't be automated, say which lines were covered and how.
- **AC3–AC4:** a Play Mode harness on the real `PlayerUIPrefab`, temporary in `Temp/HYG2Verification/`.
  - Count text writes with a `TMP_Text` subclass, or by checking `text` reference changes per frame.
  - Remove it afterwards and keep a copy under `build/hyg2-verification/`.

## 9. Manual Unity Editor checks

**Two Editors, the clone as client** (its own log).
1. **A full match:** select, cast, take damage, die, respawn and finish.
   - The HP and lives texts are right at every step.
   - Switch the language during the match. Both texts change at once.
2. **The lobby:** refresh the lobby list several times, create, join, start. Nothing changes for you.
3. **The release build:** `Player.log` after one match contains no "Card clicked", "Process called", "DespawnBlock", "RepairCalled", "SetLobbyCode" or lobby-search lines.

## 10. Open questions and assumptions

- **W3 (your call; asked in report #10 and again in #12): `AuthManager.SingOut()` and `SingOutAndClearCredentials()`.**
  - Nothing calls either, and the second one's branches look swapped: it calls `ClearSessionToken()` while signed in.
  - **Default: leave them as they are.** If you answer **"delete"** before Codex starts, delete both in this task (`AuthManager.cs:115-133`).
- **A1, deleting rather than gating** in `Bootstrap`, `NetworkCard` and `PonyColorScript`. Those classes have no debug flag, and the lines carry no information worth a new serialized field.
- **A2, line numbers after the other two tasks.** TASK-N54_N56_N57 and TASK-N55_N58_N32 run first, and only `PlayerUI.ConfirmTurn` and `GameMaster.Processing.cs:139` move. Use the quoted code.
