# Issue Report #11: FallenStar – Spellcasters (the client card preview, the UI6 review, the full register)

- **Date:** 2026-10-22
- **Baseline:** `HEAD` = `72a724c` ("Ui5"), plus the working tree:
  - Codex's **UI6** (TASK-UI6), not committed: 6 changed assets and scripts, 2 new scripts, 2 architecture docs
  - report #10's own changes: the `GameLoop.md` line and the removal of the UI1–UI5 specs
  - untracked: report #10, the UI6 spec and Codex's UI6 report
- **Previous reports:** [#1](2026-10-18_IssueReport.md), [#2](2026-10-18_IssueReport-2.md), [#3](2026-10-18_IssueReport-3.md), [#4](2026-10-18_IssueReport-4.md), [#5](2026-10-19_IssueReport-5.md), [#6](2026-10-19_IssueReport-6.md), [#7](2026-10-20_IssueReport-7.md), [#8](2026-10-20_IssueReport-8.md), [#9](2026-10-21_IssueReport-9.md), [#10](2026-10-21_IssueReport-10.md)
- **Your bug report:** "Card preview works only on host client, on client client it doesn't work."
- **Method:**
  1. **Traced both ways a preview opens:**
     - hand hover: `CardViewUI` → `PlayerUI` → `CardPreviewController.ShowPreview`
     - board click: `PlayerSession.Update` → `SelectObject` → `ShowPreview`
  2. **Traced when each `PlayerSession` is created, on the host and on the client:**
     - `SessionManager.CreateSession`
     - the Netcode for GameObjects 2.11.2 source: `NetworkSceneManager`, `NetworkObject.SpawnInternal`, `CreateObjectMessage`
  3. **Read today's `Editor.log`.** Its header is the clone's (`-projectPath …_clone_1`, started 09:48). It holds your two-Editor session up to 09:56: 3 matches, a patchwork of both Editors.
  4. **Reviewed UI6 against [its spec](../Tasks/TASK-UI6_ReadabilityProfiles.md):**
     - `git diff` of all changed files
     - the prefab YAML: the dock, the 13 low rects, the labels, the sprites and the sibling order
     - every GameScene override on the HUD instance, and both network hashes
     - Codex's [report](2026-10-22_UI6_Implementation.md) and `build/ui6-verification/`: the results, `layout-issues.txt`, the measurements and two of the screenshots, which I opened
  5. **Re-located every open issue.**
     - Outside UI6's files the code is unchanged since `72a724c`, so report #10's locations were checked again by grep.
     - I also swept all scripts for the N50 pattern: a network-spawned object that looks up scene objects too early on a client.
- **I didn't run Unity, a build or Play Mode.** I changed no code, scenes, prefabs or settings. My only changes:
  - this report
  - the corrective spec [TASK-N50_N52](../Tasks/TASK-N50_N52_ClientCardPreview.md)
  - one note in `Architecture.md` §3.2 about when a client's `PlayerSession` is created

**This report is the full issue list.** §4 lists every open issue and §6 every closed one. The new IDs are **N50–N53**. The next new finding would be **N54**.

### Severity levels and verdicts (unchanged since report #1)

| Level | Meaning |
|---|---|
| 🔴 **Critical** | Breaks the build or the core loop, or lets a client cheat or control the match |
| 🟠 **High** | Freezes or corrupts a match under reachable conditions, or causes silent data loss |
| 🟡 **Medium** | Wrong behavior, a broken flow, or a latent crash with low probability |
| 🟢 **Low** | Maintainability, performance, cosmetics, dead code |
| ⚪ **Info** | Naming, typos, documentation, design notes |

- **Confirmed:** the defect is visible directly in the code path, assets or logs.
- **Plausible:** the code strongly suggests the defect, but it needs an Editor or Play Mode check.

## Summary

| | |
|---|---|
| **Your bug** | **Confirmed: N50 🟡.** On a client, no card preview can ever open: not on hover, not on a board click.<br>• **Why:** the client's `PlayerSession` is created while **LobbyScene** is still loaded. Its `CardPreviewController.Start` then caches the Lobby's canvas and no dock. GameScene's load destroys that canvas, and `ShowPreview` silently returns from then on.<br>• **The host is fine,** because its sessions are created after GameScene has loaded (§1). |
| **Since when** | Older than UI6. The same `Start` lookup exists since UI4 (`05cb219`), and before UI4 it was a plain `FindFirstObjectByType<Canvas>()`. So the board-click preview has most likely **never** worked on a client. UI6 made it visible, because hover is the first preview you use all the time.<br>**My part:** TASK-UI6 §4.3 told Codex to read the dock in `Start` too. I didn't check when `Start` runs on a client. |
| **The fix** | [TASK-N50_N52](../Tasks/TASK-N50_N52_ClientCardPreview.md), ready for Codex. `CardPreviewController` finds the HUD canvas and dock when a preview is shown, not in `Start`. Two scripts, no scene or prefab change, no networking change. |
| **UI6 review** | **The spec is met except for N50, which blocks acceptance.**<br>• Profiles, the button column, the label ranges, the dock, hover and N49 match the spec. The YAML and the scene overrides are clean.<br>• **N51** 🟢: AC4's description floor is missed for long texts. ru Healing Hooves is 15.0 px at 1366×768 (target 18) and 11.7 px at 640×480 (target 14). Needs your decision (§7).<br>• **N52** 🟢: in Player action, a hand rebuild can hide the view of a card that's still shown.<br>• **N53** ⚪: AC3's "no overlap" can't be met in medium/high, because my spec also froze that layout. The ink doesn't overlap. |
| Compile and build (Codex, UI6) | 0 C# errors and 0 C# warnings. The release player built, with 1 BuildReport warning. The test player confirmed 1920×1200 windowed above the taskbar.<br>**Not run:** a two-player match. Codex says so in its report, and that's where N50 lives. |
| Today's log | 3 matches. No exception and no error from game code. The one client registration that survived the patchwork came from a resumed coroutine (`Editor.log:1305`), so the client's session existed before GameScene's `SessionManager` did. That's the N50 order (§1.3). |
| 🔴 / 🟠 / 🟡 open | **0 / 0 / 1:** N50 |
| Fixed in the working tree, not yet closed | **N47** (the client side waits for N50; residual N51), **N48**, **N49**, and N34's two "Tried ShowPrew" logs |
| 🟢 Low open | **8:** N51, N52, N32, N33, N34, N35, and N27 (parked) |
| ⚪ Info open | N53, N36, N29 (parked), the naming list |
| Unknowns open | **4:** U2, U9, U15, U16. U15 and U16 now need checking **on the client too**, after N50's fix. |

**Overall:** UI6 does what the spec asked, on the host. One defect, which predates UI6, keeps all of it from the joining player. The fix is small and local. After it, one checklist will cover UI5 in a build (#10 §5 A), UI6 (#10 §5 B) and the client preview.

---

## 1. The client card preview (N50)

### 1.1 What happens on the client

1. **The server spawns the sessions inside its own scene-load callback.**
   - The host loads GameScene.
   - NGO spawns the in-scene `SessionManager`.
   - Its `OnNetworkSpawn` calls `CreateSessionsForAlreadyConnectedClients` → `SpawnWithOwnership` (`SessionManager.cs:84-101`, `:251`).
2. **The spawn message goes out before the scene-load event.**
   - `SpawnInternal` sends the create message to every observer at once (NGO `NetworkObject.cs:1862-1874`).
   - The server only sends the GameScene load event after its in-scene objects are spawned: `OnSessionOwnerLoadedScene` spawns them at `NetworkSceneManager.cs:1832` and sends the event at `:1862`.
3. **The client creates its `PlayerSession` in LobbyScene.**
   - NGO only defers a create message while a single-mode scene load is in progress on that client (`ShouldDeferCreateObject`, `NetworkSceneManager.cs:653-684`).
   - The client hasn't received the load event yet, so nothing is deferred. The session is instantiated in the active scene, which is LobbyScene.
   - NGO then carries it through the scene change, so it survives.
4. **`CardPreviewController.Start` runs at that moment** (`CardPreviewController.cs:19-49`):
   - `PlayerUI.Instance` is null, because GameScene isn't loaded. So `m_dock` stays null.
   - The fallback takes the first root screen-space canvas. The only one is LobbyScene's: `LoginCanvas` is gone, and the persistent `AppRoot` has no children.
   - No warning is logged, because a canvas was found.
5. **GameScene loads in single mode and destroys LobbyScene's canvas.**
   - `m_canvas == null` is now true (Unity's destroyed-object rule).
   - `ShowPreview` returns at `:64`.
6. **Nothing ever looks again.** Both callers reach the dead controller:
   - **hover:** `PlayerUI.OnCardPointerEntered` → `m_playerSession.GetComponent<CardPreviewController>()` (`PlayerUI.cs:410-426`)
   - **board click:** `PlayerSession.SelectObject` → `m_cardPreviewController` (`PlayerSession.cs:438-458`)

### 1.2 Why the host works

| | Host | Client |
|---|---|---|
| Its own `PlayerSession` is created | in `OnSessionOwnerLoadedScene`, after GameScene has loaded | when the server's create message arrives, before the load event: **in LobbyScene** |
| `PlayerUI.Instance` at `CardPreviewController.Start` | the GameScene HUD | null |
| Canvas cached | the HUD's root canvas | LobbyScene's canvas, destroyed moments later |
| `m_dock` | `CardViewDock` | null |
| `ShowPreview` | builds the docked view | returns at `:64` |

### 1.3 Evidence

- **Code path:** all steps above, at the lines given. NGO's comment at `NetworkSceneManager.cs:646-652` describes exactly this deferral rule.
- **Your log, `Editor.log` (clone header):**
  - `:1148`: the host's "Local PlayerSession registered … OwnerClientId: 0". Its stack runs `PlayerSession:OnNetworkSpawn` → `StartCoroutine`. So `SessionManager.Instance` already existed, and the coroutine finished in its first step.
  - `:1305`: the client's "… OwnerClientId: 1". Its stack starts at `SetupCoroutine:InvokeMoveNext`, with no `OnNetworkSpawn` frame. So the coroutine had to wait. `SessionManager.Instance` was null when the client's session spawned, because GameScene's `SessionManager` didn't exist yet. That's step 3.
  - `:3356` and `:6152` are later host registrations, synchronous again. The later client lines were overwritten in the patchwork.
  - 0 "no screen-space canvas found" warnings: consistent with step 4, where a canvas *was* found.
- **Scenes:** each of the six scenes has exactly one Canvas. LobbyScene's is a root Screen Space Overlay canvas (`LobbyScene.unity:2720-2729`). The only prefab with a Canvas, `CardWorldViewPrefab`, is world-space (`m_RenderMode: 2`), so the fallback skips it.
- **Verdict: Confirmed.** It's deterministic in code, and the log shows the client order. The fix still needs your check on a real client (§5).

### 1.4 Since when, and my part

- **UI4 (`05cb219`)** gave `Start` its current lookup. A client then got the same dead canvas, and the board-click preview silently did nothing.
- **Before UI4,** `Start` was `m_canvas = FindFirstObjectByType<Canvas>()`, which finds the same Lobby canvas on a client.
- **Your checklists never split this by player.** The #8–#9 checklist's B4 rows ("Card preview stays on screen") don't say host or client, and every Codex harness ran a single local HUD. A missing preview on the client went unnoticed.
- **My part:** TASK-UI6 §4.3 said "In `Start`, where it already looks up `PlayerUI.Instance` for the canvas, also take `m_dock`". Codex followed the spec. My spec reviewed what `Start` finds on the host only.

### 1.5 The fix

Detailed in [TASK-N50_N52](../Tasks/TASK-N50_N52_ClientCardPreview.md):
- **On demand:** `CardPreviewController` resolves the dock, the canvas and the root at `ShowPreview` time, not in `Start`.
- **Re-resolved after a scene change:** a cached object that a scene change destroyed compares equal to null, so the controller looks again.
- **The HUD wins:** whenever `PlayerUI.Instance` exists, its canvas and dock are used.
- **What doesn't change:** the spawn order, `SessionManager`, `PlayerSession` and every asset. Changing when sessions spawn would touch the match start (`StartGameAfterSceneReady` waits for them), which is far riskier than fixing one lookup.

### 1.6 Nothing else has this pattern

- **Only `PlayerSessionPrefab` is spawned during the server's scene-load callback.** The other network prefabs are spawned later, once the clients have the scene:
  - `GameMaster`, from `OnLoadEventCompleted`
  - characters, blocks and cards, from `GameMaster`
  - `SessionManager` is in-scene
- **`PlayerSession` itself is already safe:**
  - `Awake` caches `Camera.main`, which is the Lobby camera on a client. `TryPrepareSelectionReferences` takes it again when it's destroyed (`PlayerSession.cs:119-122`).
  - `RegisterLocalPlayerWhenReady` waits for `SessionManager` (`:615-645`).
- **The scene searches elsewhere** run in GameScene objects (`GameMaster.Awake`, `CardWorldViewManager`) or in the Lobby itself (`LobbyControlUI`).

---

## 2. UI6 review (working tree, not committed)

| Part | Spec | What was implemented | Verdict |
|---|---|---|---|
| `UiResolutionProfile` | §4.1 | `MediumMinWidth = 1366`, `IsLow(w) => w < 1366` | ✅ |
| `ResolutionProfileLayout` | §4.1 | **As specified:**<br>• `Awake` captures the authored rects and the reference, then applies the current profile.<br>• `Update` switches only on a change.<br>• It warns once for missing targets.<br>• It falls back to the parent scaler, and a zero reference leaves the scaler alone.<br>• It's a plain `MonoBehaviour`.<br>**Codex added `[DefaultExecutionOrder(-100)]`,** so the layout is applied before `PlayerUI.Update` re-spaces the hand. That's a sensible addition. | ✅ |
| Hand re-spacing | §4.1 | `PlayerUI.Update` re-spaces when the container's width changes. `UpdateHandSpacing` stores the width it used (`PlayerUI.cs:113-114`, `:333`). | ✅ |
| Button column, sprites | §4.2 | Confirm (−190, 475), Send (−190, 362), Confirm turn (−190, 249), each 300 × 92.85. Confirm and Send have the wide plank sprite `2be7b70e…`. | ✅ YAML checked |
| Label ranges | §4.2 | **Every value as specified:**<br>• the 3 button labels 30–44 (Confirm turn's autosize now on in the prefab)<br>• Leave and both dialog buttons 30–40<br>• HP and Life 30–72<br>• the 3 mana texts 30–36 | ✅ YAML checked |
| GameScene overrides | §4.2 | **As specified:**<br>• Confirm turn's two label overrides are removed.<br>• PhaseAnouncer's `m_fontSizeMin` is 30.<br>**Codex also set `GameAnnouncerUI.m_minFontSize` to 30** (`GameScene.unity:610`). That component re-applies its own minimum in `Awake`, so the floor would otherwise fall back to 24. That's a right catch.<br>**No other override shadows a changed value.** I listed all of them on PrefabInstance `282301674`: the root rect, `m_timeText`, one color, the announcer's TMP values and the two hashes. | ✅ |
| `CardViewDock` | §4.3.1 | **As specified:**<br>• anchors (0, 0)–(0, 1), pivot (0, 0.5), (40, 207), (700, −626)<br>• a `CanvasGroup` that isn't interactable and doesn't block raycasts<br>• sibling 12 of 13, right before `LeaveMatch` | ✅ |
| `PlayerUI.CardViewDock` | §4.3.2 | Serialized and wired | ✅ |
| `CardPreviewController` | §4.3.3 | **As specified:** the dock path, the uniform fit, the refit in `LateUpdate`, `ShownCard`, the same-card early return, and the UI4 path without a dock. Both "Tried ShowPrew" logs are removed.<br>**But the dock and the canvas are read once, in `Start`,** as my spec said. On a client that's too early. | ❌ **N50** (§1) |
| Hover | §4.3.4 | **As specified:**<br>• the enter and exit events, cleared in `OnDestroy`<br>• subscribe and unsubscribe in `CreateCardView` / `ClearHandUI`<br>• the local session's controller, cached<br>• exit hides only the view of its own card<br>• `ClearLocalUI` hides the preview<br>**The rebuild check tests `m_handCards`.** In Player action the HUD row shows the played cards, which aren't in that list. | ✅, except 🟢 **N52** |
| Low profile | §4.4 | The 13 rects match the table exactly. Codex needed none of the allowed adjustments. | ✅ YAML checked |
| Windowed sizes (N49) | §4.5 | **As specified:**<br>• the work area minus 16 / 64, with the strict display rule when there's no work area<br>• an inclusive fit<br>• the pure `PickWindowedSize` (aspect first, then any fit, then the fallback)<br>• callers pass the bounds as `display`, with a comment<br>One leftover: `FitsDisplay`'s `windowed` parameter is now unused (`DisplaySettingsUI.cs:137-141`). I've added it to N33. | ✅ |
| Non-goals and constraints | §5–6 | **Nothing outside scope:**<br>• No change to the menu scenes, `CardViewUIPrefab`, `PlayerSession`, the world side or the NGO prefab list.<br>• Both hashes are kept (`GameScene.unity:465`, `:469`), and no `NetworkBehaviour` was added.<br>• No new ungated log; `ResolutionProfileLayout`'s one warning is the setup warning the spec allows.<br>• The font asset was restored from the baseline (Codex's `final-audit.txt`). | ✅ |
| Docs | — | `Architecture.md` and `Dependencies.md` describe UI6 accurately. The §3.3 line about `CardPreviewController.Start` will change with N50's fix. | ✅ |

### 2.1 Codex's two acceptance exceptions

**AC4, the description size → N51 🟢, your decision (§7).**
- **The measurements** (Codex's tables are in its report, §5):
  - 8 of the 24 description cases at 640×480 and 1366×768 (4 cards × 3 locales × 2 sizes) miss: en Fireball, and ru Fireball, Healing Hooves and Ice Shard.
  - **The worst case is ru Healing Hooves:** 15.0 px at 1366×768 (target 18) and 11.7 px at 640×480 (target 14).
  - Card names: ru Healing Hooves is 11.4 px at 1366×768 and 8.9 px at 640×480. That's the art limit I expected (spec A3).
- **Why it can't be fixed inside UI6's scope:**
  - The dock is limited by its height. In medium/high it sits between the hand (top at 500) and Leave (bottom at 1514).
  - Long descriptions autosize their font down inside the fixed text box. My spec's estimate assumed the largest font.
- **For scale:** N47 was 6–8 px. Every description is now at least 15 px at 1366×768, and at least 11.7 px at 640×480.

**AC3, "no two elements overlap" → N53 ⚪, accept.**
- **Where the rects meet:** in medium/high, the rect of the right-anchored 650-wide `ManaTextBar` meets the banner's rects (`Panel`, `PhaseAnouncer`), and `OpponentManaTextBar` meets `Panel`. That's at 7 of the 8 sizes.
- **It's UI3's layout,** and §6 of my spec required medium/high to stay the same. So AC3 contradicted §6: **my error.**
- **The text doesn't touch:** Codex measured that the ink doesn't overlap. My 1366×768 screenshot check agrees: the banner ends at x 1072, and the right-aligned mana text starts at about x 1136.

### 2.2 Evidence notes

- **Screenshots.** I opened `HUD-ru-1366x768.png` and `HUD-ru-640x480.png`. They really show Russian, unlike the UI1–UI4 screenshots.
  - **1366×768:** the docked view, the column, and 10 cards.
  - **640×480:** the compact layout. The view covers the leftmost hand cards, which the spec accepted.
- **No live match.** Codex used a HUD fixture, not a two-player match. Its report says so (§5, "Multiplayer/manual limits"). That's why N50 wasn't caught.
- **The PC shutdown at 01:25** came from Codex's `shutdown-after-ui6.ps1`. Its report says you asked for it after the work. It ran after the final audit, so it changed nothing in the results.

---

## 3. New findings

| ID | Level | Issue | Location | Evidence | Verdict |
|---|---|---|---|---|---|
| **N50** | 🟡 Medium | **No card preview ever opens on a client.** This covers both hover and board click.<br>• **The cause:** the client's `PlayerSession` is created while LobbyScene is loaded. `CardPreviewController.Start` caches the Lobby canvas and a null dock. GameScene's load destroys that canvas, and `ShowPreview` returns from then on (§1).<br>• **What it costs:** the joining player never gets UI6's readable card view (N47), nor the board-card preview.<br>• **It blocks UI6's acceptance.** → [TASK-N50_N52](../Tasks/TASK-N50_N52_ClientCardPreview.md) | `InGame/CardPreviewController.cs:19-49` (`Start`), `:64` (the guard)<br>`MultiplayerScripts/SessionManager.cs:84-101`, `:251` (when sessions spawn)<br>`UI/PlayerUI.cs:410-426`<br>`InGame/PlayerSession.cs:438-458` | Your report. NGO 2.11.2 source (`NetworkObject.cs:1862-1874`, `NetworkSceneManager.cs:653-684`, `:1832`, `:1862`). `Editor.log:1305` vs `:1148` | Confirmed |
| **N51** | 🟢 Low | **The large view misses UI6's description floor for long texts.**<br>• **The worst case,** ru Healing Hooves: 15.0 px at 1366×768 (target 18) and 11.7 px at 640×480 (target 14).<br>• **8 of the 24 description cases at those two sizes miss.** Every miss is for en Fireball or ru Fireball, Healing Hooves or Ice Shard.<br>• **Names:** ru Healing Hooves is 11.4 px at 1366×768 and 8.9 px at 640×480 (the art limit, spec A3).<br>• **The dock is limited by its height,** and the text box is fixed in `CardViewUIPrefab`. It's the residual of N47. | `Prefab/CardViewUIPrefab.prefab` (`CardDesc`, `CardName`)<br>`PlayerUIPrefab.prefab` (`CardViewDock` and its low rect)<br>`Cards_ru` (texts) | Codex's `card-measurements.csv` and its report §5 | Confirmed |
| **N52** | 🟢 Low | **In Player action, a hand rebuild can hide the view of a card that's still shown.**<br>• **The check:** `ValidateHoveredCard` treats a card as gone when it isn't in `m_handCards`.<br>• **The gap:** in Player action and Resolve cards, the HUD row shows `m_boardPlayCards`. So any rebuild while you hover a played card hides its view. Moving the pointer brings it back.<br>• **A second gap:** a hovered card destroyed by a deck refill returns early (Unity null) instead of hiding. That's an edge case. | `UI/PlayerUI.cs:438-446`; the rebuild cases `:310-320` | Code path | Plausible |
| **N53** | ⚪ Info | **In medium/high, the mana bars' rects meet the banner's rects.** The text doesn't touch. It's UI3's layout. TASK-UI6's AC3 couldn't be met together with its §6. | `PlayerUIPrefab.prefab` (`ManaTextBar` / `OpponentManaTextBar` 650 wide at (−40, −200 / −250); `Panel` 1622 wide) | Codex's `layout-issues.txt` (189 pairs, 7 of 8 sizes); the 1366×768 screenshot | Confirmed (rects); cosmetic only |

**Also corrected in the register:**
- **N34:** three debug messages lack `$`, not one: `BlockController.cs:46`, `PlayerController.cs:70` and `GameMaster.PackageManaging.cs:258`. All three are behind `m_debugMode`.
- **N33** gains `FitsDisplay`'s unused `windowed` parameter.

---

## 4. All open issues

### 🔴 Critical / 🟠 High

None open.

### 🟡 Medium (1)

| ID | Issue | Current location | Verdict |
|---|---|---|---|
| **N50** | No card preview on a client, either on hover or on a board click: the controller caches the Lobby canvas in `Start` (§1). Blocks UI6. → TASK-N50_N52 | `CardPreviewController.cs:19-49`, `:64`; `SessionManager.cs:84-101`, `:251` | Confirmed |

### Fixed in the working tree (UI6), not yet closed

They close when UI6 and N50's fix are committed and your checks pass (§5).

| ID | Status |
|---|---|
| **N47** | **Host:** the docked large view on hover and click (`CardPreviewController.cs`, `PlayerUI.cs`, `CardViewDock`). **Client:** waits for N50. The residual is N51. |
| **N48** | Every HUD label is at least 30 units. Codex measured 16 sizes × 3 locales. |
| **N49** | Windowed fits the work area, and the automatic pick is 1920×1200. Codex saw it in its test player; it still needs your check in your build. |
| N34 (part) | The two "Tried ShowPrew" logs are removed. |

### 🟢 Low (8)

| ID | Issue | Current location | Verdict |
|---|---|---|---|
| **N51** | Long card descriptions miss UI6's floor in the large view: ru Healing Hooves 15.0 px at 1366×768, 11.7 px at 640×480; ru names down to 8.9 px (§2.1). Your decision (§7). | `CardViewUIPrefab.prefab`; `CardViewDock`; `Cards_ru` | Confirmed |
| **N52** | In Player action, a rebuild hides the hover view of a played card that's still shown. → TASK-N50_N52 | `UI/PlayerUI.cs:438-446` | Plausible |
| **N32** | `ProcessActivity` reads the card owner's character without a null check and never uses it. It can't throw today, because `CheckActivity` checks the same owner first. If it ever did, `RunGuarded` would abort the match. | `GameMaster.Processing.cs:139` | Confirmed (latent) |
| **N33** | **Dead code and unused prefabs left after L10:**<br>• `GameMaster.m_gameLoopCoroutine`, `m_phasesTimerCoroutine`, `m_cardViewWorldPrefab`<br>• `PlayerController.m_isDead` (a NetworkVariable that is never written), `GetCurrentSpeed()`<br>• `PlayerSession.ClearHand()`, `ClearBlocks()`, `RemoveCard()`<br>• `NetworkCard.SetFaceUpServerRpc`, `SetRotatedServerRpc`, `DespawnCardServerRpc`<br>• `CardPreviewController.TogglePreview`, `MultiplayerManager.KnowLobbies`, `SceneFlowManager.LoadLoginScene`, `GameSettingsPrefs.HasLanguageCode`<br>• **new:** `DisplaySettingsUI.FitsDisplay`'s unused `windowed` parameter (UI6)<br>• `BootsTrap.prefab` and `MultiplayerManager.prefab` (the second has `m_debugMode: 1`)<br>• the stale `m_timeText` override on the GameScene HUD instance<br>**Your call:** `AuthManager.SingOut()` and `SingOutAndClearCredentials()` have no caller, and the second one's branch order looks inverted. | `GameMaster.cs:56`, `:104-105`<br>`PlayerController.cs:21`, `:118`<br>`PlayerSession.cs:374`, `:583`, `:748`<br>`NetworkCard.cs:73-86`<br>`CardPreviewController.cs:51`<br>`MultiplayerManager.cs:65`<br>`SceneFlowManager.cs:45`<br>`GameSettingsPrefs.cs:20`<br>`DisplaySettingsUI.cs:137-141`<br>`AuthManager.cs:116-133`<br>`Assets/Prefab/BootsTrap.prefab`, `Assets/Prefab/MultiplayerManager.prefab`<br>`GameScene.unity` (PrefabInstance `282301674`, target `6674156206700678496`) | Confirmed. The `SingOutAndClearCredentials` branch order is Plausible. |
| **N34** | **About 30 log lines aren't behind any debug flag,** so they print in release builds. Examples:<br>• "Process called", "Rpc called", "RepairCalled", "DespawnBlock"<br>• "Card clicked" / "Card deselected"<br>• the "Applied … material index" lines<br>• "DrawPhase started/ended"<br>• 9 lines per lobby search<br>• "SetLobbyCode" / "Lobby code"<br>**Also:**<br>• `PlayerUI.OnCardClicked` / `OnCardAlterClicked` read `cardView.name` before their null check (latent).<br>• **Three** gated debug messages lack `$`. | `GameMaster.Processing.cs:115`, `:138`<br>`PlayerSession.cs:229`<br>`NetworkCard.cs:164`<br>`BlockController.cs:38`<br>`CardViewUI.cs:99`, `:105`<br>`PlayerUI.cs:450`, `:467`, `:486`, `:503`<br>`PonyColorScript.cs:63`, `:83`<br>`GameMaster.GameLoop.cs:485`, `:520`<br>`MultiplayerManager.cs:765-834`, `:1541-1542`<br>the `$`: `BlockController.cs:46`, `PlayerController.cs:70`, `GameMaster.PackageManaging.cs:258` | Confirmed |
| **N35** | `PlayerUI` formats HP and lives every frame, as two Smart-format strings, even when nothing changed. | `UI/PlayerUI.cs:175-189` | Confirmed |
| N27 | The pony mane color is never applied, because `maneObject` isn't assigned. Every spawn logs "Invalid mane material index." **Parked by you** until the mane model exists. | `Assets/Prefab/BoardPrefab/PonyBase.prefab:368`; `InGame/PonyColorScript.cs:70-88` | Confirmed (again today) |

### ⚪ Info

- **N53:** in medium/high, the mana bars' rects meet the banner's rects. The text doesn't touch (§2.1). Accept, unless you see text touching.
- **N36:** the countdown and the server loop use different clocks. After a server hitch, or during drawing, clients can briefly see "00". Plausible and cosmetic. (`GameMaster.cs:124-141`, and the phase loops in `GameMaster.GameLoop.cs`)
- **N29:** the lobby shows the player ID twice. **Parked by you.** (`UI/LobbyPlayerItemUI.cs:53-65`)
- **Naming and typos** (report only, don't rename):
  - `CardControllPackage`, `PlayerCardfWorldArea.cs`, `SetOponent` / `m_currentOponentNetworkObjectId`, `CheckActiveTaergetSelection`
  - `SingInAnonymous` / `SingOut` / `SingOutAndClearCredentials` / `m_isSingingIn`, `HearthbeatLobby`, `MovePlayeToSpawnPoint`, `ClearupAfterDisconnect`
  - `CardSpecialization.DeffenceSpell`, `KnowLobbies` / `m_knowLobbies`, `m_bootsTrap`, `_teamAssing`, `m_phaseAnouncementText`, `#region Valiable`
  - the GameObject name `PhaseAnouncer`
  - the log texts `"Packege"`, `"Handle unseccess"`; the local variable `_locakPoint`
  - `ConfirmSendButtonUI1` (in `ConfirmSendButton.cs`); `m_startHandCards`, which is really the per-round draw count

### Feature gaps (not defects, listed so they aren't mistaken for bugs)

- **Settings:** language and display (UI5) exist. Audio, graphics quality, controls and a UI-scale setting don't. The UI6 profile is automatic, by design.
- **Join by lobby code is parked** (M11, you, 2026-10-19). `m_lobbyCode` / `m_lobbyCodePanel` stay serialized for it.
- Steam login is a stub (`AuthManager.SignInSteam`), although `LoginScene` has a button wired to it.
- `GamePhase.DrawCards` and `RoundEnd` are never set, and `ResolveCards` is only transient. During draws the announcer shows "Round break".
- The `DiscardCard` / combo actions, the `All*` / `Random*` targets and `ActivityEffect` have no effect.
- Destroying all enemy blocks doesn't win by itself. A player with no blocks is eliminated on their next death.
- `SessionRole.Spectator` exists, but nothing assigns it.
- No player-facing messages for: lobby or network failures, an ignored Send, an ignored late confirm, or a deck refill.
- The confirmed target of an action card doesn't survive a respawn pause (by design). Card selections do survive it.
- `SessionManager` has 4 slots, but the scene supports only teams 0 and 1.
- With 10 cards, the hand overlaps (UI3, intended). The rightmost card is drawn on top and receives clicks in the overlap.
- The five menu scenes don't have the low profile yet (UI7, planned).

### ❓ Unknowns: open

| ID | Question | Status |
|---|---|---|
| U2 | The nested `SpawnPoint` NetworkObject inside `BlockBody` | ❓ No NGO warning in any log so far, today's included |
| U9 | NGO `NetworkSceneManager.Dispose` NRE on Play Mode stop, Editor quit or domain reload | ❓ Not seen today |
| **U15** | Does the docked large view cover something the player needs? For example, the left side of the board, the left end of the banner on 16:10, or the leftmost hand cards in the low profile (visible in the 640×480 screenshot). | ❓ Check after N50's fix, **on the host and on the client** (§5). Fallback: **V2b**, the view at the mouse with UI4's clamp. |
| **U16** | Does the hover view on hand cards get in the way during Card selection (flicker, distraction, covering)? | ❓ Same. Fallback: **V2c** (board cards only) or a hover delay of about 0.3 s. |

---

## 5. Pending manual checks

**Nothing new has passed since report #10.** Your session today found N50.

**The checklist:** [2026-10-22_ManualChecks-Report10-11.xlsx](../ManualChecks/2026-10-22_ManualChecks-Report10-11.xlsx), 44 rows in 23 groups, written after Codex's N50/N52 change.
- It has its own A–C: **A** = C below (N50/N52), **B** = B below (UI6), **C** = A below (the build).
- **N50/N52 code review:** the diff against Codex's baseline copies matches TASK-N50_N52. Codex's harness passed every case, including the client order, and its release build succeeded. The full review goes into report #12 with your results.
- Codex has finished, so its test players no longer write `Player.log`.

**Start the clone with its own log** (`-logFile C:\Lesson\clone-editor.log`). Today's shared `Editor.log` kept only one of the client's session lines. A separate log gives a complete record of what the client did.

**C. N50 on the client** (new; TASK-N50_N52 §9):
1. **Hover, on the client:** hover each hand card in Card selection. The big card shows at the left, moves with the pointer across the cards, and hides when the pointer leaves.
2. **Board click, on the client:** in your own Player action, click your own and the opponent's board cards. Each shows the big card. A target click through it still works.
3. **Low and medium sizes:** repeat 1 at one low size (1280×720) and one medium size (1920×1080) in the client's Game view.
4. **A second match:** lobby → match → result → lobby → match, without restarting the Editors. The preview works on the client in the second match too.
5. **The host:** the same checks on the host. Nothing should change there.
6. **N52:** in Player action, hover a played card and keep the pointer still for a few seconds. The view stays.

**A. UI5 in a player build** (from report #10 §5 A, still open):
1. Settings → **Windowed** gives a window with a title bar. Since UI6 that's 1920×1200 on your screen, with the whole window above the taskbar. Then pick 1366×768: the window shrinks, and nothing is cut.
2. **Fullscreen** fills the display at its native size. Then pick 1920×1080: black bars at the top and bottom, no stretching, and clicks that land where the buttons are drawn.
3. **Alt+Enter** on the Settings screen: the Display-mode dropdown follows.
4. **Language:** switch to ru, then ja. The new labels and the mode options follow, and the Language dropdown shows the right language.
5. **Restart:**
   - Quit and relaunch: the same mode and size come back.
   - Then launch with `-screen-fullscreen 0 -screen-width 1366 -screen-height 768`: Settings shows Windowed, 1366 × 768. Afterwards, set your normal mode again.
6. **The normal route:** Login → Main menu → Settings → Back.
7. **A windowed match at 1024×768:** the HUD follows the corners. Alt-tab away for about 10 s: the match keeps running for the other player.

**B. UI6** (TASK-UI6 §9, still open):
1. **At 2560×1600:** the HUD as before, except for the bottom-right column and slightly larger labels.
2. **The low sizes** (640×480, 800×600, 1024×768, 1280×720, 1280×1024, 1360×768): the compact layout, and text that's readable in en, ru and ja. Leave → the dialog fits.
3. **The switch:** during a match, go from 1360×768 to 1366×768 and back. The layout switches at once, the hand re-spaces, and the Console shows no error.
4. **Medium sizes** (1366×768, 1920×1080, 3440×1440): readable, and nothing overlaps.
5. **Two players:** the client's HUD matches the host's.
6. **N51:** read ru Healing Hooves in the big view at 1366×768. Is it comfortable?
7. **Your notes on U15 and U16,** from both windows.

---

## 6. Closed issues

Nothing closed in this report: UI6 waits for N50's fix and your checks.

| ID | How it was closed | Commit / report |
|---|---|---|
| C1 | Debug HP delta only under `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD` and `m_debugMode` | `61323d5` (#2) |
| C2 | The movement target comes from `ServerRpcParams` | `cebd2fd` (#2) |
| C3 | Server-side `CanPlayerMove` check | `fb0edb9` (#2) |
| C4 | Selection confirm validates card IDs | `44d858f` (#2) |
| C5 | Editor-only `using`s removed | `15b439b` (#2, #5) |
| C6 | Not reproducible: the cast button works | #5 |
| H1 | `RunGuarded` turns exceptions into `AbortMatch` | `e776caf` (#3) |
| H2 | New deck without `UltraRare`/`Unique` after deleting the graveyard | `e7d1b03` (#6) |
| H3 | Block heal uses the block maximum | `e7d1b03` (#6) |
| H4 | Owner null check comes first in `CheckActivity` | `f08e854` (#4) |
| H5 | End-of-match flow and `ResultScene` | `ad3a46f`, `d44b882` (#4) |
| H6 | Forfeit when a player disconnects | `d44b882` (#4) |
| H7 | Single, server-guarded disconnect cleanup | `57d3467` (#3) |
| H8 | `CardData.OnValidate` no longer wipes activities | `f08e854` (#4) |
| H9 | `UltraRare` deck fill fixed | `f08e854` (#4) |
| H10 | Result screen menu | `ade4aed` (#5) |
| M1 | The pause and resume restores the timer after a respawn | `fe2d4fb` (#6) |
| M2 | Withdrawn (false positive) | #2 |
| M3 | Lava guard keyed by instance | `a0df515` (#7) |
| M4 | `GameMaster` input gates use `IsServer` | `a0df515` (#7) |
| M5 | Fixed by C3 | #2 |
| M6 | `UpdatePlayerHP` null checks | `a0df515` (#7) |
| M7 | Fixed by H4 | #4 |
| M8 | `AnyPlayer` through a block resolves the owner by team on clients | `a0df515` (#7) |
| M9 | `DrawCard(null)` guard (part of H2) | `e7d1b03` (#6) |
| M10 | Async lobby and network errors caught, with state rollback | `a0df515` (#7) |
| M11 | Parked: join-by-code commented out (you); moved to the feature gaps | `b7e7b91` (#7) |
| M12 | Language selection in `SettingsScene` | `b7e7b91` (#7) |
| M13 | Neutral lobby data; localized counts and fallbacks | `a0df515`, `b7e7b91` (#7) |
| M14 | Client card-data null guards; hand rebuild when the data arrives | `a0df515` (#7) |
| M15 | Hand limit 10, oldest uncommitted cards discarded | `b7e7b91` (#7) |
| M16 | `SessionManager.Instance` cleared in the `OnDestroy` override | `b7e7b91` (#7) |
| N1 | `PlayerUI.OnDestroy` overrides NGO's; CS0114 gone | `b7e7b91` (#7) |
| N2 | Session values written in the server's `OnNetworkSpawn` | `391e079` (#8) |
| N3 | Replicated input lock for client-side gates | `391e079` (#8) |
| N4 | Dead `ValidateCardOwner` and `m_cardResolveDelay` removed | `efb88c1` (#8) |
| N5 | Log string interpolated | `efb88c1` (#8) |
| N6 | Finish checked after each resolved card | `391e079` (#8) |
| N7 | `GetCharacter` uses `TryGetComponent`, doesn't write the ID | `391e079` (#8) |
| N8 | Architecture docs and README refreshed | `05cb219` (#8) |
| N9 | Superseded by MANA2 | #5 |
| N10, N11, N13 | Closed when H5 was finished | #4 |
| N12 | Withdrawn: false positive | review 2026-10-20 (#8) |
| N14 | Host returns to the lobby when a match can't start | `fe2d4fb` (#6) |
| N15 | Number not used | #4 |
| N16 | The leaver's suspicious summary is logged on disconnect | `efb88c1` (#8) |
| N17 | Host HUD kept when the opponent forfeits | `05cb219` (#8) |
| N18 | Deaths are handled in every phase | `fe2d4fb` (#6) |
| N19 | Leave button with confirmation, counts as a defeat | `fe2d4fb` (#6) |
| N20 | Safe Send; the target rule is shared with the server | `a0df515` (#7) |
| N21 | Confirm carries the intended phase; mismatches are ignored | `391e079` (#8) |
| N22 | Character HP set in the server's `OnNetworkSpawn` | `391e079` (#8) |
| N23 | `Keyboard.current` null check | `efb88c1`, `05cb219` (#8) |
| N24 | Three scripts converted to UTF-8 | `efb88c1` (#8) |
| N25 | Intended (you, 2026-10-19) | #5 |
| N26 | Unconfirmed selection cleared at each new selection phase | `391e079` (#8) |
| N28 | Font dynamic data cleared on build; commit rule in `AGENTS.md` | `05cb219` (#8) |
| N30 | Lobby refresh throttled | `05cb219` (#8) |
| N31 | Deck size documented as 84 (with L2) | `05cb219` (#8) |
| N37 | Announcer shows both lines in full: unscaled 1560×150 box, font 24–48 (now 30–48, UI6) | `05cb219`, UI1 (#9) |
| N38 | Line-2 prefixes have a ": " separator; shorter en/ru respawn prefix | `05cb219`, UI1 (#9) |
| N39 | All six scenes Scale With Screen Size / Expand; nothing cut at 9 resolutions | `05cb219`, UI2 (#9) |
| N40 | HUD anchored to the screen edges and corners | `05cb219`, UI3 (#9) |
| N41 | No non-uniform UI scale left; the per-frame `LeaveMatchUI` resize removed | `05cb219`, UI1 + UI3 (#9) |
| N42 | Login background covers any aspect (Envelope Parent) | `05cb219`, UI2 (#9) |
| N43 | Card preview clamped on screen; always on the HUD canvas. Readability became N47. (On a client, no preview opens at all: N50.) | `05cb219`, UI4 (#9) |
| N44 | ru "Подтвердить" fits the Confirm button. Its small size on small screens became N48. | `05cb219`, UI4 (#9) |
| N45 | Hand compresses to 1800 units; 10 cards no longer reach the buttons | `05cb219`, UI3 (#9) |
| N46 | Old hand views deactivated before `Destroy`: no one-frame jump | `05cb219`, UI3 (#9) |
| L1 | Countdown from a replicated deadline | `05cb219` (#8) |
| L2 | All three deck mixes reachable | `05cb219` (#8) |
| L3 | `IsGameFinished` is pure and allocation-free | `391e079` (#8) |
| L4 | Duplicate `BlockController.TeamId` removed | `391e079` (#8) |
| L5 | `GameAnnouncerUI` maps every existing phase | #4 |
| L6 | Announcer, world views, HUD clear and lobby panels change-only (HP/lives → N35) | `05cb219` (#8) |
| L7 | Card texts refresh on a locale change | `05cb219` (#8) |
| L8 | `btn_lobbyRefresh` key without the tab | `efb88c1` (#8) |
| L9 | Debug flags forced off in release builds | `05cb219` (#8) |
| L10 | Dead code, legacy scenes, backups and VisualScripting removed (leftovers → N33) | `05cb219` (#8) |
| U1 | The player build compiles and runs | review 2026-10-20, #8 |
| U3, U4 | Answered (null `NetworkBehaviourReference` is safe; `DontDestroyWithOwner: 0`) | #3 |
| U5 | GUID `e651dbb3…` is NGO's own `NetworkPrefabsList` script | review 2026-10-20 |
| U6 | Explained: each Editor logs its own `GameScene` load in the shared log | review 2026-10-20 |
| U7 | Normal end reaches `ResultScene` on both instances | #4 |
| U8 | `Shutdown()` leaves no objects behind | review 2026-10-20 |
| U10 | The forfeit works at runtime | review 2026-10-20 |
| U11 | Not reproduced | #7 |
| U12 | Mana replicates to a remote client, and the rejection reaches it | review 2026-10-20 |
| U13 | A second lobby and match work after result → main menu | #6 |
| U14 | At 1024×768 (4:3) the whole board and both player areas are visible (your B7.1) | #10 |

Tasks completed without an issue ID:
- MANA1–MANA3 (`ade4aed`, `5eec046`, `058fa63`)
- SEC1 (`f08e854`)
- HYG1 (`efb88c1`)
- UI5, display settings (`72a724c`, reviewed in #10)

---

## 7. Your decisions, and the next steps

**Decisions I need:**

| # | Question | Options | My recommendation |
|---|---|---|---|
| **W1** | **N51:** long descriptions in the big view are 15.0 px at 1366×768 and 11.7 px at 640×480 (worst: ru Healing Hooves). | **a.** Accept as the residual of N47.<br>**b.** Shorten the longest en/ru descriptions (Fireball, Healing Hooves, Ice Shard), so autosize keeps a bigger font. Localization only.<br>**c.** Redesign the card's text box (new card art). | **a**, and **b** later if B.6 says it's hard to read. At 1366×768 every description is now at least 15 px, about twice N47's 8–10 px, and above 12 px at every medium/high size. In medium/high the dock can't grow taller without moving the hand or Leave. |
| **W2** | **N53:** accept that the mana bars' rects meet the banner's in medium/high, as long as the text doesn't touch? | **a.** Accept.<br>**b.** Narrow the mana bars to about 450 units. That's a medium-layout change. | **a** |
| — | For the record only: **did the board-card preview ever show on the client** before UI6? | — | It doesn't change the fix. It would confirm that N50 is older than UI6 (§1.4). |

**The next steps:**
1. **Codex implements [TASK-N50_N52](../Tasks/TASK-N50_N52_ClientCardPreview.md)** on top of the uncommitted UI6. It touches two scripts. Then I review it.
2. **One checklist** for §5 A, B and C, in one session with both Editors and a player build.
3. **Then commit UI6 and the fix together.** Include report #10, this report, the UI6 spec, Codex's UI6 report and the UI1–UI5 spec removals. After that commit I'll remove TASK-UI6 and TASK-N50_N52 from `Docs/AI/Tasks/`.
4. **UI7,** after that: the low profile for the five menu scenes, and Settings sizes down to 640×480. It reuses `ResolutionProfileLayout`, now reviewed.
5. **One small hygiene spec (all Low), on your go-ahead:** N32, N33, N34 and N35, as in report #10 §7.
6. **Parked by you:** N27 and N29. **Optional:** N36.

---

## 8. Task folder

| File | State | Action |
|---|---|---|
| `TASK-UI6_ReadabilityProfiles.md` | Implemented. Not accepted because of N50. **Untracked.** | **Keep.** Remove it after the commit (step 3). |
| `TASK-N50_N52_ClientCardPreview.md` | **New**, ready for Codex | Keep |
| `TASK-UI1` … `TASK-UI5` | Removed by report #10, not yet committed. Still in git history (`git show 05cb219:Docs/AI/Tasks/<file>`, UI5 in `72a724c`). | Commit the removal with step 3. |

## Sources

- **Git:**
  - `git status`, `git log`
  - `git diff` of every changed file
  - `git show` of `CardPreviewController.cs` at `72a724c`, `391e079` and `b7e7b91` (the history of `Start`)
- **Logs:** `%LOCALAPPDATA%\Unity\Editor\Editor.log`:
  - header `-projectPath C:/Lesson/FallenStar-Spellcasters_clone_1`, started 09:48, last write 09:56
  - your session, both Editors
  - lines `:1148`, `:1305`, `:3356`, `:6152` (the session registrations)
  - lobbies `M7GB6P` and `9NBLQN`
  - 3 results
  - 0 exceptions
- **NGO 2.11.2** (`Library/PackageCache/com.unity.netcode.gameobjects@beeaefb722f7`):
  - `Runtime/SceneManagement/NetworkSceneManager.cs:640-684`, `:1736-1910`
  - `Runtime/Messaging/Messages/CreateObjectMessage.cs:133-150`
  - `Runtime/Core/NetworkObject.cs:1852-1890`
  - `Runtime/Spawning/NetworkSpawnManager.cs:1160-1460`
- **Assets:**
  - `PlayerSessionPrefab.prefab` (components and serialized references)
  - `PlayerUIPrefab.prefab`: the full UI6 diff, the root's child order, and the names of the 13 low targets
  - `GameScene.unity`: every modification on PrefabInstance `282301674`, the `GameAnnouncerUI` block, and the two hashes
  - the Canvas count and render mode in all six scenes
  - `LoginScene.unity` (`AppRoot` has no children; `LoginCanvas` is a separate root)
  - `NetworkPrefabs.asset` (7 prefabs)
- **Code read:**
  - `CardPreviewController.cs`, `PlayerUI.cs`, `CardViewUI.cs`, `CardViewBase.cs`, `ResolutionProfileLayout.cs`, `UiResolutionProfile.cs`, `DisplaySettingsUI.cs`
  - `PlayerSession.cs:1-200`, `:400-800`
  - `SessionManager.cs`, `SceneFlowManager.cs`, `Bootstrap.cs`, `MultiplayerManager.cs:1317-1440`, `GameMaster.Card.cs:55-90`, `:236-262`
  - every scene search (`Find*ByType`, `Camera.main`) in `Assets/Scripts`
  - the N32–N36 locations
- **Codex output:** `build/ui6-verification/`:
  - `edit-results.txt`, `play-results.txt`, `runtime-results.txt`, `layout-issues.txt`, `card-measurements.csv`, `final-status.txt`
  - `shutdown-after-ui6.ps1` / `shutdown.log`
  - `HUD-ru-1366x768.png`, `HUD-ru-640x480.png` (opened)
