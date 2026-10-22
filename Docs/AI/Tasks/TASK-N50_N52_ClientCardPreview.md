# TASK-N50_N52: The card preview works on a client: find the HUD when a preview opens, and keep a played card's hover view

- **Source:** [Issue Report #11](../Reports/2026-10-22_IssueReport-11.md): N50 (§1), N52 (§3).
- **Severity:** 🟡 Medium: N50 (Confirmed). It blocks UI6's acceptance. 🟢 Low: N52 (Plausible).
- **Baseline:** `72a724c` **plus the uncommitted TASK-UI6 working tree.** Line numbers are from that working tree. Don't revert or rewrite UI6. Build on it.
- **Implementer:** Codex
- **Scope:** two scripts. No scene, prefab or networking change.

## 1. Objective

1. **N50:** on a remote client, the large card view opens exactly as on the host. That means on hover over a hand card, and on a board-card click.
2. **N52:** a hand rebuild keeps the hover view while its card still has a view in the HUD row. That includes the played cards shown in Player action.

## 2. Current behavior

**When the client's session is created**
- `SessionManager.OnNetworkSpawn` spawns one `PlayerSession` per client (`MultiplayerScripts/SessionManager.cs:84-101`, `:251`). It runs inside the server's GameScene load callback.
- NGO sends that create message **before** it sends the GameScene load event. A client therefore creates its `PlayerSession` while **LobbyScene** is still loaded. NGO then carries the session into GameScene.
- The host creates its own session after GameScene has loaded.
- Details: report #11 §1.1–1.3.

**What `CardPreviewController.Start` caches** (`InGame/CardPreviewController.cs:19-49`)
- `m_dock` from `PlayerUI.Instance.CardViewDock`. On a client, `PlayerUI.Instance` is null at that point, so `m_dock` stays null.
- `m_canvas`: the HUD's root canvas, otherwise the first root canvas that isn't world-space. On a client that's LobbyScene's canvas.
- `m_previewRoot`: that canvas's transform.

**What goes wrong**
- GameScene's load destroys LobbyScene's canvas.
- From then on `m_canvas == null`, and `ShowPreview` returns at `:64`. Nothing ever looks again.
- Both callers end there:
  - hover: `PlayerUI.OnCardPointerEntered` (`UI/PlayerUI.cs:417-426`)
  - board click: `PlayerSession.SelectObject` (`InGame/PlayerSession.cs:438-458`)

**`PlayerUI.ValidateHoveredCard`** (`UI/PlayerUI.cs:438-446`)
- It runs at the end of `UpdateCards` and keeps the view only if `m_handCards` contains the hovered card.
- In `PlayerAction` and `ResolveCards`, the HUD row shows `m_boardPlayCards` (`:310-320`). So any rebuild hides the view of a played card that's still under the pointer.
- If the hovered card has been destroyed, `m_hoveredCard == null` is true, and the method returns without hiding.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/CardPreviewController.cs` | Resolve the targets on demand; remove the work from `Start` |
| `Assets/Scripts/UI/PlayerUI.cs` | `ValidateHoveredCard` only |
| `Docs/AI/Architecture/Architecture.md` §3.3, `Docs/AI/Architecture/Dependencies.md` | Update the `CardPreviewController` lines |

**Dependencies (unchanged):**
- `PlayerSessionPrefab.prefab:101-115`: `CardPreviewController` sits on the session's GameObject, with `m_canvas` and `m_previewRoot` **unassigned** and `m_cardPreviewPrefab` set. `PlayerSession.m_cardPreviewController` points to it.
- `PlayerUI.Instance` is set in `PlayerUI.Awake` and cleared in `OnDestroy`. `PlayerUI.CardViewDock` (UI6).
- `PlayerUI.GetCardPreviewController()` (`:410-415`) uses the local session's controller.
- One controller instance can outlive a GameScene. The session is carried across the scene load, and later matches may reuse a controller. A cached dock or canvas can therefore be a destroyed object.

## 4. Required changes

### 4.1 `CardPreviewController` (N50)

1. **`Start` resolves nothing and logs nothing.** No code calls `Start()` directly (checked with grep), so you may delete the method.
2. **Add `private bool TryResolveTargets()`.** Call it in `ShowPreview` right after the null-card check. It replaces the `m_canvas == null || m_previewRoot == null` guard at `:64`. If it returns false, `ShowPreview` returns.
3. **Its rules, in this order:**
   1. **Dock:** if `m_dock == null` and `PlayerUI.Instance != null`, set `m_dock = PlayerUI.Instance.CardViewDock`. It may stay null, which means UI4's path.
   2. **Canvas:** if `m_canvas == null`, take the HUD's root canvas through `PlayerUI.Instance`, as `Start` does today. If there's no HUD, use today's fallback: the first root canvas that isn't world-space.
   3. **Root:** if `m_previewRoot == null` and `m_canvas != null`, set `m_previewRoot = m_canvas.transform as RectTransform`.
   4. **Result:** return `m_canvas != null && m_previewRoot != null`.
4. **Use Unity's `==` null check on these fields.** A cached object destroyed by a scene change must compare equal to null, so that the next call looks again. Don't use `is null`, `?.` or `??` on `m_dock`, `m_canvas` or `m_previewRoot`.
5. **Assigned references stay.** If a prefab or Inspector ever assigns `m_canvas` or `m_previewRoot`, they're kept while they're alive.
6. **The warning.**
   - When `TryResolveTargets` returns false, log `"CardPreviewController: no screen-space canvas found; previews are disabled."`.
   - Log it at most **once per controller instance**: keep a private bool. Don't reset it.
7. **Unchanged:**
   - `LateUpdate`: its `m_dock != null` check already handles a destroyed dock.
   - `FitDock`, `HidePreview`, `ShownCard`, `HasPreview`, `TogglePreview`
   - the dock path and the UI4 path inside `ShowPreview`
   - the same-card early return. Keep it after `TryResolveTargets`.

### 4.2 `PlayerUI.ValidateHoveredCard` (N52)

- **"Still shown"** means `m_cardViews` contains the hovered card's `NetworkObjectId`. That's the HUD row after the rebuild: the hand in Card selection, the played cards in Player action and Resolve cards.
- **When the card is gone,** hide the preview if `ShownCard` is that card, then clear `m_hoveredCard`. "Gone" means either case:
  - the hovered card has no view in `m_cardViews`
  - the hovered card is a destroyed object: `!ReferenceEquals(m_hoveredCard, null) && m_hoveredCard == null`. **Don't read `NetworkObjectId` on a destroyed card.**
- If `m_hoveredCard` is reference-null, return as today.
- **Unchanged:** the enter and exit handlers, `ClearHandUI`, `ClearLocalUI`, and the play and discard clicks.

### 4.3 Docs

- **`Architecture.md` §3.3:** replace "`CardPreviewController.Start` uses the HUD root canvas…" with the on-demand rule.
- **`Dependencies.md`:** the `CardPreviewController` row lists `ShowPreview` / `TryResolveTargets` instead of `Start`.
- **Don't touch** the client-timing note I added to `Architecture.md` §3.2.

## 5. Explicit non-goals

- **No change to when sessions spawn.** Leave alone `SessionManager`, `MultiplayerManager.StartGameAfterSceneReady`, the NGO settings, `PlayerSessionPrefab` and its `NetworkObject` flags.
- **No change to:**
  - `PlayerSession`, including `SelectObject` and `TryPrepareSelectionReferences`
  - the dock's geometry, the low profile, `CardViewUIPrefab`, or any scene or prefab
- **Not in this task:** N51 (description size), N53, U15 / U16, or a hover delay.
- **No other N34 log work.** No new logs except the one warning in §4.1.6.

## 6. Constraints and compatibility requirements

- **Public API unchanged.** Keep `ShowPreview`, `HidePreview`, `TogglePreview`, `HasPreview`, `ShownCard` and `PlayerUI.CardViewDock`. The serialized fields keep their names, so the prefab references stay valid.
- **The host is unchanged:** the same docked view, the same scale and the same timing as in UI6.
- **Client-only UI.** No RPCs, NetworkVariables or ownership changes.
- **Unity 6 APIs only.** No obsolete members and no compiler warnings of our own.
- **Before you start:** keep a baseline copy of every file you'll change in `build/n50-verification/baseline/`. The working tree already holds uncommitted UI6 changes, so the baseline separates this task's diff.
- **Before handing over:** restore the dynamic font asset from that baseline (`AGENTS.md`).

## 7. Acceptance criteria

1. **The client order:**
   - Set up a controller like the session prefab's: canvas and root unassigned, the same preview prefab.
   - Let its `Start` run while the only screen-space canvas is a stand-in Lobby canvas. Then destroy that canvas and bring up the GameScene HUD.
   - `ShowPreview` must open the view under `CardViewDock`, with the same fit as on the host. Check both ways in: the `PlayerUI` hover handler, and a direct `ShowPreview` as from `SelectObject`.
2. **The host order:** with the HUD present before `Start`, the result is identical to UI6.
3. **No HUD, but a screen-space canvas:** UI4's path (at the pointer, clamped, scale 1).
4. **Nothing at all:**
   - `ShowPreview` returns, with no exception.
   - Exactly one warning per controller instance, even after several calls.
5. **A second HUD:**
   - Destroy the HUD and create a new one, keeping the same controller instance.
   - The next preview opens under the **new** dock, and nothing is parented to a destroyed object.
6. **N52:**
   - **Player action:** a rebuild while a played card with a view is hovered keeps the view. A rebuild that removes that card hides it.
   - **Card selection:** behavior as in UI6.
   - **A destroyed hovered card:** the view is hidden, with no exception.
7. **UI6's harness checks** for hover, crossing, clicks, clean-up and the profile refit still pass.
8. **Build:** the Unity compile has 0 errors and no warnings of our own, and the release Windows player builds.

## 8. Regression tests

**Play Mode harness**, temporary, in `Temp/N50Verification/`. Remove it afterwards. Reuse UI6's HUD fixture approach.
- **AC1:**
  - a scene with one Screen Space Overlay canvas, then the controller, then one frame for `Start`
  - destroy the canvas, then load the GameScene HUD (or the fixture), then `ShowPreview`
  - assert the parent is `CardViewDock`, the scale is the fit, and the view is active
- **AC2–AC5:** one case each. Count the warnings with `Application.logMessageReceived`.
- **AC6:**
  - drive `UpdateCards` in `PlayerAction` with a played card hovered (`ExecuteEvents` pointer enter), then trigger a rebuild
  - then the removal case and the destroyed-card case
- **Report:** the parent, the scale and the warning count for each case.

**Not possible for Codex:** a real two-player match (it needs authentication). Say so in the report. It's the owner's check (§9).

## 9. Manual Unity Editor checks

**The setup**
- Two Editors with ParrelSync. Start the clone with its own log: `-logFile C:\Lesson\clone-editor.log`.
- The host creates the lobby, and the clone joins as the **client**.

**On the client**
1. **Card selection, hover:** hover each hand card. The big card shows at the left, switches across the cards without flicker, and hides when the pointer leaves. Left and right clicks still play and discard, and the view stays while you click.
2. **Player action, board click:** click your own and the opponent's board cards. The big card shows. A target click where the big card is drawn still reaches the board. The next world click hides it.
3. **N52:** in Player action, hover a played card and keep the pointer still for a few seconds. The view stays.
4. **Sizes:** repeat 1 at 1280×720 (low) and 1920×1080 (medium/high).
5. **A second match:** lobby → match → result → lobby → match, without restarting the Editors. Repeat 1 and 2.

**On the host**

6. **No change:** repeat 1 and 2.

**The logs**

7. Neither log has a "no screen-space canvas found" warning or an exception from game code.

## 10. Open questions and assumptions

- **A1, the cause is the `Start`-time lookup.** The evidence is in report #11 §1.3: the NGO 2.11.2 source and the client's coroutine-resumed registration at `Editor.log:1305`.
  - If the client still shows nothing after this fix, check `networkCard.m_cardData` on the client (M14's path).
  - That's unlikely, because a hand view is only built once the card data has arrived.
- **A2, the spawn order isn't changed,** on purpose. Spawning sessions after every client has loaded would move work into `StartGameAfterSceneReady`, which waits for the sessions. That's a larger and riskier change than fixing one lookup.
- **A3, "the hand" in TASK-UI6 §4.3.4** meant the HUD row (`HandContainer`), not `m_handCards`. N52 corrects that reading. It was my spec's ambiguity, not Codex's error.
