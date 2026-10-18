# RESEARCH: Mana system for playing cards

- **Date:** 2026-10-18
- **Request (owner):**
  - Each player starts with **base mana**.
  - Each turn, mana grows by a **growth step**, up to a **maximum (cap)**.
  - A player **can't play a card whose cost is higher than the current mana pool**.
- **Status:** research done. **The owner's decisions (2026-10-18) are recorded below, and tasks have been created:** [SEC1](../Tasks/TASK-SEC1_SuspiciousActivityTracking.md), [MANA1](../Tasks/TASK-MANA1_ManaDataAndRoundGrowth.md), [MANA2](../Tasks/TASK-MANA2_ManaCheckAndSpendOnConfirm.md), [MANA3](../Tasks/TASK-MANA3_ManaDisplayAndMessages.md).
- **Decisions:**

  | Question | Decision |
  |---|---|
  | D1, growth model | **B (capacity)** for now. It may become a mixed rule later. |
  | D2, when mana is spent | **At confirm** |
  | D3, discards | **Free** |
  | D4, turn order | **Keep** |
  | D5, when growth happens | **At RoundBreak, before draw** |
  | D6, opponent's mana | **Visible** |
  | D7, numbers | **Base 2, step 2, cap 20** |
  | D8, over-budget confirm | **Reject**, force-clear the client's selection, reset the selection timer, and record it as suspicious (server-side tracking) |
- **Baseline:** `ad3a46f` (H5 and H6 in progress)
- **Method:** I read the card data, selection, confirm, cast and UI code paths and the card assets. Nothing was run.

## 1. What exists today (verified)

| # | Fact | Source |
|---|---|---|
| F1 | Every card already has a cost: `CardData.m_cost` (`int`) | `InGame/CardData.cs:85` |
| F2 | Current costs: Iceshard 2, Fireball 3, Clarity 3, HealingHooves 4 (template `CardData.asset`: 0). The cost is shown on the cards. | `Assets/Card/Data/*.asset`; `CardViewUI.cs:37`, `CardViewWorld.cs:83` |
| F3 | Today the cost has **one** effect: turn order. The sum of the **play and discard** costs (`PlayerTurnSelection.TotalCost()`) orders the players, highest first. | `PlayerTurnSelection.cs`; `GameMaster.GameLoop.cs:766` (`BuildTurnOrderByCardCost`) |
| F4 | Round structure (server, `GameLoop`): `RoundBreak` → `DrawCardPhase` (2 cards per player) → `CardSelection` → `PlayerTurnPhase`, where each player gets one `PlayerAction` turn in cost order. One "turn" per player per round. | `GameMaster.GameLoop.cs:98-121` |
| F5 | **Selection:** on the client, LMB on a hand card calls `PlayerSession.SelectCardToPlay` and RMB calls `SelectCardToDiscard`. Both only check the phase (`CanPlayerSelectCards`). | `CardViewUI.OnPointerClick` (`:83, :89`), `PlayerSession.cs:169-205` |
| F6 | **Confirm:** `PlayerSession.ConfirmTurn` sends the selected IDs, then **clears the local selection immediately** (`ClearLocalCardSelection`), whatever the server decides | `PlayerSession.cs:105-135` |
| F7 | **The server accepts the selection** in `HandleConfirmSelectionTurn` → `ValidateSelectionCards` (C4): hand, owner, no duplicates. The **first confirmation is final** (C4/N9). The board is built from the accepted cards. | `GameMaster.PackageManaging.cs:189-218, 306-370` |
| F8 | **Cast:** `HandleCardControlPackage` and `PlayerActionPhase` accept only cards that are in `m_boardPlayCards`, the cards accepted at confirm | `PackageManaging.cs:74-157`, `GameLoop.cs` (`PlayerActionPhase`) |
| F9 | Per-player networked state uses the pattern `NetworkVariable<int> … (ReadPermission.Everyone, WritePermission.Server)`, e.g. `m_lifeCurrent` | `PlayerSession.cs:39` |
| F10 | Match tuning values live as `[SerializeField]` fields on `GameMaster`, e.g. `m_roundBreakTime`, `m_startHandCards`. `GameMasterPrefab` doesn't serialize them, so the C# defaults apply (L9). | `GameMaster.cs:69-79` |
| F11 | **HP and life display:** `PlayerUI.Update` polls the values and formats them with `LocalizedString` (`label_hp`, `label_life` = "Life : {0}") | `UI/PlayerUI.cs:26-27, 113-126` |
| F12 | `PlayerSession`s are created fresh for every match. `SessionManager` lives in `GameScene`, and sessions are created on scene load. So per-match values don't need resetting from the previous match. | `SessionManager.cs:93-111, 205-237` |
| F13 | `PlayerSession.Initialize` writes NetworkVariables **before** the object is spawned, which NGO warns about (N2). Mana should be initialized **after** spawn, in `StartNewGame`. | `SessionManager.cs:229`, `Editor.log` |
| F14 | No card effect touches mana today. `CardActionType` has no mana actions. | `CardData.cs:3-20` |

**Key consequence of F6 and F7:** if the server rejects a confirmation because of mana, the client has already cleared its selection and gets no feedback. **So the client-side check is required for UX, and the server-side check is required for security** (the same principle as C1–C4).

## 2. Design decisions the owner must make

| # | Question | Options | Recommendation |
|---|---|---|---|
| D1 | **How does mana grow?** | **A. Bank:** `pool = min(pool + step, cap)` each turn. Unspent mana carries over. Starts at `base`. **B. Capacity (Hearthstone-style):** `capacity = min(capacity + step, cap)` each turn, and the pool **refills** to capacity. Unspent mana is lost. Starts at `base`. | **A** matches the request's wording ("increases by step to the cap"). B is a classic, easier to balance. *Owner decides.* |
| D2 | **When is mana spent?** | **a. At selection confirm** (`CardSelection`), for the cards chosen to **play**. **b. At cast time** (`PlayerAction`), when the card resolves. | **a.** The selection is the commitment, and turn order already depends on it (F3). Spending at confirm also means the board (F8) can never hold unpaid cards, so the cast path needs no change. With b, the selection would still have to be limited to the pool, or a player could commit to cards they can't pay for. |
| D3 | **Do discards cost mana?** | Free / cost the same as play / a fixed cost | **Free.** Discarding is how you get rid of unaffordable cards. *Owner decides.* |
| D4 | **Turn order (F3)** | Keep "sum of play + discard costs" / change to "mana spent" / other | **Keep unchanged** in the first version, to limit scope. With D3 = free, discarding expensive cards still buys initiative without mana. That may be intended, or a loophole; it's a design call. |
| D5 | **When does growth happen** ("each turn")? | Start of every round, before `DrawCardPhase` / start of each player's `PlayerAction` | **Start of every round, except the first** (the first round uses `base`). Growth must happen before `CardSelection`, because that's when mana is checked and spent. |
| D6 | **Is the opponent's mana visible?** | Visible (like life) / hidden | **Visible.** It's a plain NetworkVariable readable by everyone, so hiding it would need extra work. |
| D7 | **Default numbers** | Base / step / cap | Proposal: **base 3, step 2, cap 10**. With costs of 2–4 and 2 new cards per round, this allows 1 card in round 1 and 2–3 cards later. *Balance is the owner's call.* All three values are tunable on `GameMaster`. |
| D8 | **If the server sees an over-budget confirmation** (modified or out-of-sync client) | Reject the whole confirmation (it stays unconfirmed, and the client may resend) / drop cards until it fits | **Reject the whole confirmation and notify the client.** This needs one targeted ClientRpc (step S4). |

## 3. Proposed model (assuming D1 = A, D2 = a, D3 = free, D5 = per round)

```
Match start (StartNewGame, server, after sessions are spawned):
    for each player: mana = base

Each round, before CardSelection (rounds 2, 3, ...):
    for each player: mana = min(mana + step, cap)

CardSelection, client:
    can select card X to play  ⇔  sum(cost of selected play cards) + X.cost ≤ mana
    discard selection: no mana check

CardSelection, server (confirm):
    accepted = ValidateSelectionCards(...)          // existing C4 logic
    if sum(cost of accepted play cards) > mana → reject, notify client, stay unconfirmed
    else mana -= sum(cost of accepted play cards); confirm as today

PlayerAction: unchanged (board only contains paid cards)
```

With D1 = B, the only differences are the round step (`capacity = min(capacity + step, cap); mana = capacity`) and one more NetworkVariable for capacity.

## 4. Implementation plan (future tasks)

These are steps to turn into `TASK-MANA-*` files once the decisions are made. They are ordered by dependency.

| Step | What | Files | Notes |
|---|---|---|---|
| **S1. Data and config** | `GameMaster`: `[SerializeField] int m_baseMana = 3, m_manaGrowthStep = 2, m_maxMana = 10`. `PlayerSession`: `public NetworkVariable<int> m_manaCurrent = new(0, Everyone, Server)`. With D1 = B, also `m_manaCapacity`. | `GameMaster.cs`, `PlayerSession.cs` | Additive only. Follows F9 and F10. |
| **S2. Initialize** | In `StartNewGame`, after building `m_players`: set `m_manaCurrent.Value = m_baseMana` for each player | `GameMaster.cs` (`StartNewGame`) | **Not** in `PlayerSession.Initialize` (F13) |
| **S3. Grow per round** | A new private `GrowMana()` in `GameMaster.GameLoop.cs`, called in `GameLoop()` at the start of each round **except the first** (a local `bool _isFirstRound`), before `DrawCardPhase()` | `GameMaster.GameLoop.cs` | Server-only. No new phase. |
| **S4. Server check and spend** | In `HandleConfirmSelectionTurn`, after `ValidateSelectionCards`: compute the play cost from `turnSelections.m_cardsToPlay`. **If it's over budget:** clear the selection lists, leave `m_isConfirmed = false`, return `false` (so the board isn't touched), and send a targeted `[ClientRpc] SelectionRejectedClientRpc(reason)` to the sender. **Otherwise:** `session.m_manaCurrent.Value -= cost`. | `GameMaster.PackageManaging.cs`, `PlayerSession.cs` (RPC receiver) | This changes C4's "first confirmation is final" into "**first valid** confirmation is final". Keep the turn-order cost as it is (D4). |
| **S5. Client gating** | `PlayerSession.SelectCardToPlay`: if `SelectedPlayCost() + card.m_cardData.m_cost > m_manaCurrent.Value`, return `false`. Null-check `m_cardData` (M14). Discard is unchanged (D3). Optionally refuse to confirm when over budget. | `PlayerSession.cs` | The UI reacts to `false` already (the card doesn't highlight). Check `PlayerUI` / `CardViewUI` handling. |
| **S6. Rejection feedback** | When `SelectionRejectedClientRpc` arrives, show a short localized message, e.g. in the announcer or `PlayerUI`, and allow re-selection | `PlayerUI.cs` or `GameAnnouncerUI.cs` | Only reachable by an out-of-sync or modified client if S5 works. It can be a log-only message in the first version. |
| **S7. Mana display** | `PlayerUI`: a new `TMP_Text m_manaText` and `LocalizedString m_labelMana`, polled like HP and life (F11). Optional: the remaining mana after the current selection, e.g. "Mana: 5 (−3)". Optional: dim unaffordable hand cards in `CardViewUI`. | `PlayerUI.cs`, `PlayerUIPrefab` (Editor), `UI_table` | Text in **en/ru/ja** (§5) |
| **S8. Verification pass** | ParrelSync scenarios (§6) | — | — |

**Code that doesn't need changes:**
- the cast path (F8)
- `ProcessActivity`
- `BuildTurnOrderByCardCost` (D4)
- `ClearupAfterDisconnect` (sessions are per match, F12)
- `EndMatch` / `AbortMatch`

**Effort:** small to medium, about 3–4 tasks: S1–S3, then S4–S5, then S6–S7, then the test pass. The trickiest part is S4 together with the client feedback (F6).

## 5. Localization (en / ru / ja)

| Key | en | ru | ja |
|---|---|---|---|
| `label_mana` | Mana : {0} | Мана : {0} | マナ : {0} |
| `label_mana_capacity` (only if D1 = B, or to show the cap) | Mana : {0} / {1} | Мана : {0} / {1} | マナ : {0} / {1} |
| `mana_not_enough` (S6) | Not enough mana | Недостаточно маны | マナが足りません |

`label_mana` follows the existing `label_life` format ("Life : {0}"). The texts are proposals for the owner to approve.

## 6. Test scenarios (for the future tasks)

1. **Round 1:** both players have `base` (3). Iceshard (2) can be selected; selecting Fireball (3) as well is refused, because 5 > 3. After confirm, mana = 1.
2. **Round 2:** mana = min(1 + 2, 10) = 3 (model A), or 5 (model B with capacity 5).
3. **Cap:** after enough rounds without playing, mana stops at 10.
4. **Discard:** any number of cards can be discarded regardless of mana (D3). The turn order still uses play + discard costs (D4).
5. **Tamper (temporary client edit):** send a confirmation over budget. The server rejects it, nothing is placed on the board, no mana is spent, the client gets the rejection, and a valid re-confirmation then works.
6. **Timeout without confirming:** no mana is spent and no cards are played.
7. **Display:** the mana label updates on both clients in en, ru and ja.

## 7. Risks and interactions

- **Ongoing work:** H5 and H6 (paused) touch `GameMaster.cs`, `GameLoop.cs` and `PlayerUI.cs`, and H10 touches the UI. **Schedule the mana tasks after H5, H6 and H10 are merged** to avoid conflicts.
- **C4 behaviour change** (S4): over-budget confirmations no longer lock the selection. This is intended, but the C4 manual checks should be re-run.
- **D3 + D4 loophole:** free discards still raise turn priority (§2). That's a design issue, not a bug.
- **M14:** `m_cardData` may be null on the client until `SendCardDataClientRpc` arrives. S5 must treat an unknown cost as "can't select".
- **Future card effects** that give or take mana (e.g. Clarity) need a new `CardActionType`. Keep `m_manaCurrent` writes in one server helper (e.g. `GameMaster.AddMana(session, delta)`, clamped to `[0, cap]`) so they're easy to add later.

## 8. Decisions needed before tasks can be written

D1 (bank or capacity), D2 (spend at confirm or at cast), D3 (are discards free), D7 (numbers), D8 (reject or trim). D4–D6 can take the recommended defaults unless the owner says otherwise.

## Sources

- **Scripts:**
  - `Assets/Scripts/InGame/{CardData,PlayerTurnSelection,PlayerSession,CardViewUI,CardViewWorld,GameMaster}.cs`
  - `Assets/Scripts/InGame/GameMaster/{GameMaster.GameLoop,GameMaster.PackageManaging}.cs`
  - `Assets/Scripts/UI/PlayerUI.cs`
  - `Assets/Scripts/MultiplayerScripts/SessionManager.cs`
- **Assets:** `Assets/Card/Data/*.asset`, `Assets/Localization/UI_table Shared Data.asset`, `UI_table_en.asset`
