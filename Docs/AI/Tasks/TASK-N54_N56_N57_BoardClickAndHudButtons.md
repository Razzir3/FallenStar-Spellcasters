# TASK-N54_N56_N57: Board clicks reach every table card, and the HUD's Confirm / Send / Confirm-turn buttons are safe

- **Source:** [Issue Report #12](../Reports/2026-10-22_IssueReport-12.md): N54 (§2), N56 and N57 (§3).
- **Severity:**
  - 🟡 Medium: **N54** (Confirmed: your checklist row A2.1 plus the asset chain), **N57** (Confirmed in the code path; the timing window is a manual check)
  - 🟢 Low: **N56** (Confirmed, latent)
- **Baseline:** `6d89a09` ("N50_N52"), clean working tree. Line numbers are from that commit.
- **Implementer:** Codex
- **Scope:** one prefab (`CardWorldViewPrefab`), three small scripts, two architecture docs. No scene, networking or gameplay-rule change.
- **Order:** do this task first. TASK-N55_N58_N32 and TASK-N33_N34_N35 touch some of the same files later.

## 1. Objective

1. **N54:** a left click on any table card opens the big card, in both windows. That covers your own cards and the opponent's, on the left side (team 0) and on the right (team 1).
2. **N57:** pressing **Confirm turn** before the match has started can't break the HUD. The hand row fills normally when the match begins.
3. **N56:** **Confirm** and **Send** never throw when the local `PlayerSession` isn't available. They do nothing and log one warning.

## 2. Current behavior

### 2.1 N54: clicks on the right-hand table cards are swallowed as UI clicks

**The click path**
- `PlayerSession.Update` (`InGame/PlayerSession.cs:728-746`) runs on a left press. It returns early when `EventSystem.current.IsPointerOverGameObject()` is true. That check keeps HUD clicks from reaching the board.
- Otherwise `SelectObject` (`:426-460`) raycasts with Physics. When it hits a `CardViewWorld`, it calls `CardPreviewController.ShowPreview`.

**The table card prefab**
- In `Prefab/BoardPrefab/CardWorldViewPrefab.prefab`, the child `VisualRoot/Front/TextRoot/Canvas` is a **World Space** canvas (`m_RenderMode: 2`, `:506-528`). It has no event camera, so uGUI uses `Camera.main`.
- That canvas has a **`GraphicRaycaster`** (`:552-569`, `m_IgnoreReversedGraphics: 1`).
- Its three `TextMeshProUGUI` children (name, description, cost) have **`m_RaycastTarget: 1`** (`:153`, `:719`, `:920`).
  - Each text rect is 200 × 50 canvas units. With the canvas and `VisualRoot` scales, that's about 20 × 5 world units, centred on the card.
  - So a text rect covers the card's middle band and the gap to its neighbours in the column.

**Why only the right-hand cards**
- `GameScene` has two mirrored card areas (`PlayerWorldCardAreaPrefab` instances):
  - team 0 at x −20, PrefabInstance `632972215`
  - team 1 at x +20, PrefabInstance `1266062148`
- `Front` is turned 180° about Y. As a result, team 1's card text faces `Main Camera`, and team 0's faces away from it.
- uGUI's reversed-graphics rule skips graphics that face away. So:
  - a pointer over a **team 1** card's text counts as "over UI", in **every** window
  - `PlayerSession.Update` returns, and no big card opens
  - **team 0's** cards work
- **The teams:** `SessionManager.CreateSessionsForAlreadyConnectedClients` walks `ConnectedClientsIds` (the host first), and `GetFreeSlot` gives slot 0 first. So the **host is team 0 (left, red)** and the **client is team 1 (right, blue)**.

**Your result:** checklist A2.1 failed. On the client, clicking your own board cards showed nothing. Those are team 1's cards.

### 2.2 N57: Confirm turn can bind the HUD without its subscriptions

- **`PlayerUI.Update`** (`UI/PlayerUI.cs:111-190`):
  - It returns at `:124` while `GameMaster.Instance == null`.
  - It binds the local session only in its `m_playerSession == null` block (`:134-173`). That block subscribes `UpdateCards` to the three card lists and the phase handlers, then builds the row.
- **`PlayerUI.ConfirmTurn`** (`:719-737`) is wired to the Confirm turn button (`PlayerUIPrefab.prefab:2575-2577`).
  - If `m_playerSession` is null, it **assigns** `m_playerSession = SessionManager.Instance.LocalPlayer`. It subscribes nothing.
- **The window, on the host:**
  - The host's `LocalPlayer` registers as soon as GameScene has loaded.
  - `GameMaster` is created only in `MultiplayerManager.OnLoadEventCompleted`, once the client has loaded too.
  - If the host presses Confirm turn while the client is still loading, `m_playerSession` is set.
  - From then on the binding block never runs. The hand row stays empty for the whole match; HP and mana still update.
- **On a client** the window is shorter: from its GameScene load until `GameMaster`'s spawn arrives.

### 2.3 N56: the Confirm and Send buttons dereference without checks

- `ConfirmTargetButtonUI.ConfirmSelect` (`UI/ConfirmTargetButtonUI.cs:7-12`)
- `ConfirmSendButtonUI1.TryConfirmSend` (`UI/ConfirmSendButton.cs:6-11`)
- Both read `SessionManager.Instance.LocalPlayer` and call it without null checks.
- **The result is a `NullReferenceException`** when either is pressed:
  - before the local session registers
  - or after it's gone: disconnect, or the scene closing at match end

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Prefab/BoardPrefab/CardWorldViewPrefab.prefab` | Remove the `GraphicRaycaster` and turn off Raycast Target on the three texts (N54) |
| `Assets/Scripts/UI/PlayerUI.cs` | `ConfirmTurn` only (N57) |
| `Assets/Scripts/UI/ConfirmTargetButtonUI.cs`, `Assets/Scripts/UI/ConfirmSendButton.cs` | Null-safe session lookup (N56) |
| `Docs/AI/Architecture/Dependencies.md` §6 | A new regression point for world-space canvases |

**Dependencies (unchanged)**
- **Who uses `CardWorldViewPrefab`:**
  - `CardWorldManagerPrefab` (`CardWorldViewManager.m_cardWorldViewPrefab`)
  - `GameMasterPrefab` (`GameMaster.m_cardViewWorldPrefab`, an unused field: N33)
  - There are no variants and no scene instances.
- **Nothing listens to UI events on a table card.** `CardViewWorld` implements no pointer interfaces. The texts are only drawn (`CardViewWorld.ApplyTexts`).
- **GameScene's input:** its EventSystem uses `InputSystemUIInputModule`. The HUD has its own screen-space canvas and `GraphicRaycaster`, which must keep blocking world clicks.
- **The button wiring:** `ConfirmSendButtonUI1` is referenced by `PlayerUIPrefab` under that class name (RP9). Don't rename it.

## 4. Required changes

### 4.1 `CardWorldViewPrefab` (N54), prefab only

1. **Remove the `GraphicRaycaster`** from `VisualRoot/Front/TextRoot/Canvas`. Do it in the Unity Editor, in prefab mode, and save.
2. **Turn off Raycast Target** on the three `TextMeshProUGUI` under that canvas. It's a second guard, in case a raycaster is ever added again.
3. **Change nothing else:**
   - Keep the `Canvas` and `CanvasScaler` components.
   - Keep every rect, font size, text, sorting layer, transform and the `BoxCollider`.
4. **Don't rotate the card areas or `Front`, and don't move the camera.**
   - Turning team 0's cards to face up would make their text catch clicks as well.
   - Whether the left-hand cards look mirrored is a separate, cosmetic question that isn't part of this task.
5. **No code change** in `PlayerSession`, `CardViewWorld`, `CardWorldViewManager` or `CardPreviewController`.

### 4.2 `PlayerUI.ConfirmTurn` (N57)

- **Use a local variable:**
  - take `m_playerSession` if it's set
  - otherwise take `SessionManager.Instance.LocalPlayer`, when `SessionManager.Instance` exists
  - **never assign `m_playerSession` in this method**
- **If there's still no session,** keep today's warning (`"ConfirmTurn failed: local PlayerSession is null."`) and return.
- **Otherwise** call `ConfirmTurn()` on that session.
- `PlayerUI.Update` stays the only place that binds the session and subscribes.

### 4.3 The Confirm and Send buttons (N56)

- **In both scripts, look the session up on each press:**
  - if `m_localSession == null` (Unity's null check, so a destroyed session counts), take `SessionManager.Instance != null ? SessionManager.Instance.LocalPlayer : null`
  - if that's still null, log one warning in the style of `PlayerUI.ConfirmTurn` (for example `"ConfirmSelect ignored: local PlayerSession is null."`) and return
  - otherwise call `TryConfirmTarget()` / `TryHandlePackage()` as today
- **Keep** the class names, file names, method names and the `m_localSession` field. The prefab's button bindings use them.

### 4.4 Docs

- **`Dependencies.md` §6:** add a regression point.
  - **What:** world-space canvases on board objects must have no `GraphicRaycaster` and no raycast-target graphics.
  - **Why:** `PlayerSession.Update` treats any UI hit as a HUD click and skips the board (N54).
- **Don't touch** `GameLoop.md`'s input table: I've already corrected its "Target a played card" row in report #12.

## 5. Explicit non-goals

- **No change to the click logic.** Leave `PlayerSession.Update` / `SelectObject` / `TryPrepareSelectionReferences` and `CardPreviewController` alone, as well as the HUD canvas, the dock and the low profile.
- **No layout change.** Leave the card areas' positions and rotations, the camera and `CardViewWorld`'s visuals as they are.
- **No renames** (RP9, and `AGENTS.md`'s naming rule).
- **Not in this task:**
  - N33 / N34 / N35: the hygiene task. That includes the "Card clicked" logs.
  - Player-facing messages for ignored clicks (a feature gap).

## 6. Constraints and compatibility requirements

- **Serialized names and references:**
  - Every serialized field name stays.
  - `CardWorldManagerPrefab` keeps its reference to `CardWorldViewPrefab`.
  - The `PlayerUIPrefab` button bindings stay valid.
- **No networking change:** no RPC, no NetworkVariable, no ownership change.
- **Unity 6 APIs only,** with no compiler warnings of our own.
- **Before you start:** copy every file you'll change to `build/n54-verification/baseline/`.
- **Before handing over:** restore the dynamic font asset to `HEAD` (`AGENTS.md`).

## 7. Acceptance criteria

1. **A team 1 card no longer catches UI raycasts.**
   - **Setup:** place a `CardWorldViewPrefab` instance with GameScene's team 1 transform:
     - position (20, 0, −40)
     - rotation (x −0.5, y 0.5, z 0.5, w 0.5)
   - and a camera like GameScene's `Main Camera`:
     - orthographic, size 40
     - position (0, 53.1, −69), rotation 55° about X
   - **At the screen points** of the card centre and of each text rect's centre:
     - `EventSystem.RaycastAll` returns no object from the table card
     - `Physics.Raycast` hits the card's `BoxCollider`
2. **Team 0 is unchanged:** the same checks with the team 0 transform. Position (−20, 0, −40), rotation (x 0.5, y 0.5, z −0.5, w 0.5).
3. **The board click:**
   - With the pointer over a team 1 card's text and no HUD element under it, `IsPointerOverGameObject()` is false after an EventSystem update.
   - With the HUD fixture, a left press there opens the big card in `CardViewDock`.
4. **The HUD still blocks:** over a HUD hand card and over the Confirm button, `IsPointerOverGameObject()` is still true.
5. **The table card still shows its text:** the three texts are enabled, and they render the name, description and cost as before.
6. **N57:**
   - Set up `LocalPlayer` with `GameMaster.Instance == null`, then call `PlayerUI.ConfirmTurn()`.
   - No exception, and `m_playerSession` is still null (check by reflection).
   - When `GameMaster` appears (fixture), `Update` binds and subscribes as before, and the row is built.
7. **N56:**
   - With `SessionManager.Instance == null`, and again with `LocalPlayer == null`, both buttons return without an exception and log exactly one warning per press.
   - With a session, they call through exactly as before.
8. **Build:** 0 compile errors, no warnings of our own, and the release Windows player builds.

## 8. Regression tests

**Play Mode harness,** temporary, in `Temp/N54Verification/`. Remove it afterwards and keep a copy under `build/n54-verification/`.

- **AC1–AC2:**
  - Instantiate the real prefab, with an `EventSystem` + `InputSystemUIInputModule` and the camera setup above.
  - Report the `RaycastAll` result list and the physics hit for every point.
  - **Run it once on the baseline prefab too.** It should show the text hits for team 1, which proves the test sees the bug.
- **AC3–AC4:** reuse the N50 HUD fixture: the real `PlayerUIPrefab` and `CardViewUIPrefab`.
- **AC6–AC7:** direct calls, with the session fixture from N50's harness.

**Not possible for Codex:** a real two-player match. The owner checks that (§9).

## 9. Manual Unity Editor checks

**Setup:** two Editors with ParrelSync. Start the clone with its own log: `-logFile C:\Lesson\clone-editor.log`. The host creates the lobby, and the clone joins as the client.

1. **The client, Player action.**
   - Click your own table cards (right side, blue). Click the middle, where the name and text are, and the top, where the art is. The big card shows each time.
   - Then click the host's cards (left side).
2. **The host:** the same. Your own cards (left), then the client's cards (right).
3. **Targeting still works.**
   - **To cast:** pick the played card in the **bottom HUD row**, click its target on the board (the opponent's pony for Ice Shard, an opponent block for Fireball), then press Confirm and Send.
   - Clicks on HUD cards and buttons don't select anything on the board.
4. **N57, on the host:**
   - Press Start game.
   - While the client is still loading (the HUD shows but the banner has no phase yet), press Confirm turn two or three times.
   - When the match starts, the hand cards appear in Card selection as usual.
5. **The table cards look as before:** same text and art.
6. **The logs:** no exception from game code in either log.

## 10. Open questions and assumptions

- **A1, the cause.** It's deterministic in the assets (§2.1).
  - It predicts that, in **both** windows, a click on the middle of a **right-hand** card does nothing, while the art at the top may still work. The left-hand cards always work.
  - Your host note (A5.1) can be read either way. If the host really could preview the client's cards by clicking their middle, tell me. The fix is still right: table cards have nothing clickable in their canvas.
- **A2, why a prefab change and not code.**
  - Filtering UI hits by canvas in `PlayerSession.Update` would need the raycast results per module. That's more code for the same effect.
  - The table card canvas has nothing interactive.
- **A3, N57's window is measured manually** (§9.4). I didn't measure the client's load time.
