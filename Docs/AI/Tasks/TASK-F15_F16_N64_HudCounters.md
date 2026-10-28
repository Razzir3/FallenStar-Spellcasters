# TASK-F15_F16_N64: Deck counter, opponent's hand counter, a localized "Confirm Turn" label

- **Source:** [Issue Report #16](../Reports/2026-10-24_IssueReport-16.md) §3 (N64), §4 (poll #15 Q3, Q4, Q5), §6.
- **Owner decisions (poll #15, 2026-10-24):**

  | # | Decision |
  |---|---|
  | Q3 | **a.** "A small always-visible HUD line, e.g. 'Deck: 37'." Comment: "keep b idea for future" (a face-down card on the table → F15b, Later). |
  | Q4 | **a.** "A HUD line under the opponent's mana, e.g. 'Opponent's hand: 5'." Comment: "same as q3" (the card-backs row → F16b, Later). |
  | Q5 | **a.** F15 + F16 together, next. |

- **Severity:** N64 🟢 Low (an English label in ru / ja). F15 and F16 are features.
- **Baseline:** the commit after checklist #16 (`ManualChecks/2026-10-24_ManualChecks-Report16.xlsx`) passes. Start only after it. Line numbers below are from `5abd155`.
- **Implementer:** Codex. Independent of TASK-N63_ManePalette: no shared files. One task at a time, each with its own baseline snapshot.

## 1. Objective

1. **F15:** every player sees how many cards are left in the shared deck: "Deck : 37". It drops with each draw and jumps back up when a new deck is made.
2. **F16:** every player sees how many cards the opponent holds in hand: "Opponent's hand : 5". Cards the opponent has already put on the table don't count.
3. **N64:** the "Confirm Turn" button shows its text in the current language.
4. Both new lines get a dark box like the other HUD texts (F14a), in both layout profiles.

## 2. Current behavior

### 2.1 F15: the deck is server-only

- `GameMaster.m_deck` (`GameMaster.cs:62`) is a server-side `List<NetworkCard>`. Clients don't know its size.
- It changes in:
  - `CreateDeck` (`GameMaster.Card.cs:277-341`): `Clear()`, then the section fills, then `Shuffle()`. Called from `StartNewGame` (`GameMaster.cs:655`) and `RefillDeck` (`GameMaster.Card.cs:257`).
  - `DrawCard` (`GameMaster.Card.cs:148-189`): drops leading `null` entries, may call `RefillDeck`, then `m_deck.Remove(card)`.
  - `ClearupAfterDisconnect` (`GameMaster.cs:387`) and `StartNewGame` (`GameMaster.cs:587`): `m_deck.Clear()`.
- Today only the F1 hint "The deck ran out: a new deck was shuffled." (`DeckRefilledClientRpc`) tells players anything about the deck.

### 2.2 F16: the hand lists already replicate

- `PlayerSession.m_handCards`, `m_boardPlayCards` and `m_boardDiscardCards` (`PlayerSession.cs:52-54`) are `NetworkList<ulong>` with default permissions: **every peer reads them**, only the server writes.
- A card stays in `m_handCards` until it reaches the graveyard (`GameMaster.MoveCardOnGraveyard`, `GameMaster.Card.cs:39-70`). Cards committed in Card Selection are **also** in `m_boardPlayCards` / `m_boardDiscardCards` (`GameMaster.PackageManaging.cs:243`, `:253`) and lie on the table from then on.
- So "cards in hand" = entries of `m_handCards` that are in neither board list. That's what the opponent can't see.
- The IDs are public already, and card data goes only to the owner (`SendCardDataClientRpc` targets the owner). A count reveals nothing new.

### 2.3 The HUD lines

- `PlayerUI.UpdateManaTexts` (`PlayerUI.cs:212-248`) runs every frame from `Update` (`:180`). It writes `m_manaText` and `m_opponentManaText` **only when the values change** (N35 style), with `LocalizedString.GetLocalizedString(args)`.
- The opponent comes from `GameMaster.GetSessionByID(m_playerSession.m_currentOponentNetworkObjectId.Value)` (`:223-228`); no opponent → the line is emptied.
- `ResetManaTextCache` (`:250-254`) resets the caches; the locale handler (`:256`) and `ClearLocalUI` (`:725-745`) call it and empty the texts.
- **`PlayerUIPrefab`** (`Assets/Prefab/UiPrefab/`), top-right column, anchor (1, 1), pivot (1, 0.5), right-aligned TMP, size 36, autosize 30–36, white, raycast off:

  | Text | Normal: position, size | Low profile (`ResolutionProfileLayout.m_lowRects`, reference 1600×1000) |
  |---|---|---|
  | `ManaTextBar` | (−40, −200), 650×50 | (−40, −210), 600×50 |
  | `OpponentManaTextBar` | (−40, −250), 650×50 | (−40, −260), 600×50 |
  | `ManaMessageText` (the hint line, centred, anchor (0.5, 1)) | (0, −400), 1000×50 | (0, −330), 1000×50 |

- Each of the six F14a texts has a `…Backing` sibling directly before it (`HudTextBacking`, black 55 %, padding (16, 6)). RP21 in `Dependencies.md`: a new HUD text that needs a box gets such a sibling.
- Keys `label_mana_opponent` ("Opponent mana : {0} / {1}" / "Мана соперника : {0} / {1}" / "相手のマナ : {0} / {1}") show the label style.

### 2.4 N64: "Confirm turn" is hard-coded

- `ConfirmTurnBtn` → child `Text (TMP)` (`PlayerUIPrefab.prefab:3219-3290`) has `m_text: Confirm turn` and **no `LocalizeStringEvent`**. So it reads "Confirm turn" in every language (see `build/f9-f14a-n62-verification/HUD-ru-1280x720.png`).
- The key exists: `btn_confirmTurn` (`UI_table`, id `8236593956286464`): en "Confirm Turn", ru "Подтвердить ход", ja "ターンを確認". Nothing uses it.
- The neighbours are bound: `ConfirmBtn`, `SendBtn` and `LeaveBtn` each have a `LocalizeStringEvent` on their text (`LeaveBtn` → `btn_leaveMatch`, `:3505-3510`). The label has existed since commit `75fab38` (July).

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/GameMaster.cs` | `m_deckCount` NetworkVariable, `SyncDeckCount()` (§4.1) |
| `Assets/Scripts/InGame/GameMaster/GameMaster.Card.cs` | `SyncDeckCount()` calls in `CreateDeck` and `DrawCard` (§4.1) |
| `Assets/Scripts/InGame/PlayerSession.cs` | `GetUncommittedHandCount()` (§4.2) |
| `Assets/Scripts/UI/PlayerUI.cs` | Two text fields, two labels, `UpdateCounterTexts()` (§4.3) |
| `Assets/Prefab/UiPrefab/PlayerUIPrefab.prefab` | Two texts + two backings, three low rects, the `LocalizeStringEvent` on Confirm Turn (§4.4, §4.5) |
| `Assets/Localization/UI_table Shared Data.asset`, `UI_table_en/ru/ja.asset` | Two new keys (§4.6) |
| `Docs/AI/Architecture/Architecture.md`, `GameLoop.md`, `Dependencies.md` | §4.7 |

**Dependencies:**
- **`GameMaster` is a network prefab** (`GameMasterPrefab`). A new NetworkVariable changes its network layout: both peers need the same build (RP12; they do anyway).
- **`PlayerUI` runs on every peer** and reads only replicated values. The client can't see `m_deck`.
- **Client order** (the N50 lesson, `Architecture.md` §3.2): `PlayerUI` may run before `GameMaster.Instance` or the opponent session exist on a client. Both lines must stay empty, without an exception, until they do.

## 4. Required changes

### 4.1 F15: replicate the deck size (server)

In `GameMaster.cs`, next to `m_phaseEndServerTime`:

```csharp
// F15: the deck size every peer can show.
public NetworkVariable<int> m_deckCount = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

private void SyncDeckCount()
{
    if (IsServer && IsSpawned && m_deckCount.Value != m_deck.Count)
        m_deckCount.Value = m_deck.Count;
}
```

Call `SyncDeckCount()`:
- at the end of `CreateDeck`, after `m_deck.Shuffle()` (this covers the start and `RefillDeck`)
- in `DrawCard`, after `m_deck.Remove(card)`, **and** before the `"No card to draw"` return (the `null` clean-up may have changed the count)
- after `m_deck.Clear()` in `ClearupAfterDisconnect` and in `StartNewGame`

- **The count is `m_deck.Count`,** `null` entries included (they're dropped at the next draw). Don't change the deck logic.
- **No other write.** Clients only read `m_deckCount`.

### 4.2 F16: count the cards really in hand (any peer)

In `PlayerSession`, near the card lists:

```csharp
// F16: cards in hand that aren't committed to the table this round. The lists replicate to every peer.
public int GetUncommittedHandCount()
{
    int _count = 0;
    for (int i = 0; i < m_handCards.Count; i++)
    {
        ulong _id = m_handCards[i];
        if (!m_boardPlayCards.Contains(_id) && !m_boardDiscardCards.Contains(_id))
            _count++;
    }
    return _count;
}
```

- No allocation (the hand holds at most 10 cards plus the round's draws).
- It reads only; it's safe on the server and on clients.

### 4.3 `PlayerUI`: two change-only lines

- **Fields,** next to `m_opponentManaText` and its label (keep the existing ones untouched):

  ```csharp
  public TMP_Text m_opponentHandText;
  public TMP_Text m_deckText;
  [SerializeField] private LocalizedString m_labelOpponentHand = new LocalizedString("UI_table", "label_hand_opponent");
  [SerializeField] private LocalizedString m_labelDeck = new LocalizedString("UI_table", "label_deck");
  private int m_opponentHandCount = -1;
  private int m_deckCount = -1;
  ```

- **`UpdateCounterTexts()`,** called right after `UpdateManaTexts()` in `Update` (`:180`):
  - **Opponent's hand:** resolve the opponent exactly as `UpdateManaTexts` does (same null checks; it may share a small private helper). No opponent → empty the text once and set `m_opponentHandCount = -1`. Otherwise `_count = _opponent.GetUncommittedHandCount()`; when it differs from `m_opponentHandCount`, write `m_labelOpponentHand.GetLocalizedString(_count)`.
  - **Deck:** visible only while `GameMaster.Instance != null` and `m_gamePhase.Value` is neither `None` nor `GameOver`. Otherwise empty the text once and set `m_deckCount = -1`. When `GameMaster.Instance.m_deckCount.Value` differs from `m_deckCount`, write `m_labelDeck.GetLocalizedString(value)`.
  - Null-check both text fields (a prefab without them must not throw).
- **`ResetManaTextCache`:** also set both new caches to −1.
- **`ClearLocalUI`:** empty both texts like `m_opponentManaText`.
- **Locale change:** nothing extra; the reset forces a rewrite in the new language.
- **No per-frame allocation, no string formatting when nothing changed.**

### 4.4 `PlayerUIPrefab`: two new lines with boxes

1. **Duplicate `OpponentManaTextBar` twice.** Name the copies `OpponentHandTextBar` and `DeckTextBar`. Clear their text. Remove any `LocalizeStringEvent` the copy might carry (the source has none).
2. **Duplicate `OpponentManaTextBarBacking` twice.** Name the copies `OpponentHandTextBarBacking` and `DeckTextBarBacking`, and point each `HudTextBacking.m_target` at its own new text.
3. **Sibling order:** each backing directly before its text, and the four new objects right after `OpponentManaTextBar`, in this order: `OpponentHandTextBarBacking`, `OpponentHandTextBar`, `DeckTextBarBacking`, `DeckTextBar`. Don't change the relative order of the existing objects.
4. **Normal rects** (same anchor, pivot and text settings as `OpponentManaTextBar`):

   | Object | Position | Size |
   |---|---|---|
   | `OpponentHandTextBar` | (−40, −300) | 650×50 |
   | `DeckTextBar` | (−40, −350) | 650×50 |

5. **`ResolutionProfileLayout.m_lowRects`:** add two entries and change one:

   | Target | Low position | Low size | Why |
   |---|---|---|---|
   | `OpponentHandTextBar` (new entry) | (−40, −310) | 600×50 | 50 below the opponent's mana, like the normal profile |
   | `DeckTextBar` (new entry) | (−40, −360) | 600×50 | |
   | `ManaMessageText` (**change** y only) | (0, **−410**) | 1000×50 | Today's −330 sits between the new lines. On the 1600-unit canvas a long ru hint reaches x ≈ 1275, and the right column starts at about x 1230, so their boxes would overlap. −410 keeps the hint 25+ units below the deck line. |

6. **Wire `PlayerUI`:** `m_opponentHandText` = `OpponentHandTextBar`, `m_deckText` = `DeckTextBar`. The two labels use their code defaults (no Inspector value needed).
7. **Raycast:** both new TMP texts and both new backing Images have `raycastTarget` off (N54).

### 4.5 N64: bind the "Confirm Turn" label

- **Add a `LocalizeStringEvent`** to `ConfirmTurnBtn/Text (TMP)`, set up exactly like `LeaveBtn`'s: table `UI_table`, entry `btn_confirmTurn`, its *Update String* event setting the TMP text.
- The prefab's fallback `m_text` becomes "Confirm Turn" (the en table text).
- **Size:** the label keeps autosize 30–44 and wrapping. "Подтвердить ход" may wrap to two lines inside the 300×92.85 button; that's fine. **It must stay at 30 units or more** (N48) in both profiles.
- No code change. Don't touch the button's `onClick` or position.

### 4.6 Localization: two new keys

Add to `UI_table` with new unique IDs, the same metadata as `label_mana_opponent`:

| Key | en | ru | ja |
|---|---|---|---|
| `label_hand_opponent` | `Opponent's hand : {0}` | `Рука соперника : {0}` | `相手の手札 : {0}` |
| `label_deck` | `Deck : {0}` | `Колода : {0}` | `山札 : {0}` |

### 4.7 Docs

- **`Architecture.md`** §5 (server / client): `m_deckCount` is server-written and read by every HUD; the hand count comes from the replicated lists. §9 (UI): the two new HUD lines; the Confirm Turn label is localized.
- **`GameLoop.md`:** in the deck paragraph, `SyncDeckCount` after every deck change.
- **`Dependencies.md`:**
  - RP12: `GameMaster` gained a NetworkVariable.
  - RP21: the HUD's right column now has five lines; the low hint line sits at y −410.
  - The `PlayerUI` entry: reads `GameMaster.m_deckCount` and `PlayerSession.GetUncommittedHandCount()`.

## 5. Explicit non-goals

- **No face-down deck card or card-backs row** (F15b, F16b: Later).
- **No own-hand counter** (you see your own hand).
- **No change to drawing, refilling, the hand limit or the deck size.**
- **No other label or layout change** apart from the low-profile hint line (§4.4 step 5).
- **No change to `ConfirmTurn` or any button behaviour** (N64 is the label only).

## 6. Constraints and compatibility requirements

- **Keep every serialized name and reference** in `PlayerUIPrefab`, `PlayerUI` and `GameMaster`. Add fields; don't rename.
- **Server authority:** only the server writes `m_deckCount`. Nothing new is sent from clients.
- **No new RPC.** One new NetworkVariable (`m_deckCount`), so both peers need the same build (RP12).
- **N35 style:** HUD texts are written only on change.
- **N34:** no new release-build log line.
- **Unity 6 APIs, no warnings of our own.** Keep baseline copies in `build/f15-f16-n64-verification/baseline/`.
- **UI6 / UI7 regression:** re-run the UI6 HUD sweep (16 sizes × 3 locales × hand sizes 0 / 5 / 10). The 199 inherited diagnostics stay the same set. Add the two new texts and their backings to the sweep's overlap checks.
- Restore the dynamic font before handing over (`AGENTS.md`, N28).
- **Owner approvals:** you may ask the owner directly for a layout exception (poll #15 Q6 = a, "if it's not lead to security issues"). Record each approval in your report, with what you measured.

## 7. Acceptance criteria

1. **F15:**
   - In the first "Round preparation" both peers show "Deck : 84" (or the size `CreateDeck` made). During "Drawing cards" it drops by one per card drawn, the same on both peers within one network tick.
   - When the deck runs out, the line shows 0 until the next draw, then the new deck's size minus the cards drawn. The F1 hint still appears.
   - Before the match starts and after it ends (`None`, `GameOver`), the line is empty.
2. **F16:**
   - Each peer shows the **opponent's** count: after "Drawing cards", 2 more than before (with `m_startHandCards` = 2).
   - When the opponent confirms 2 played cards and 1 discard in Card Selection, the count drops by 3 at once, then stays while the cards resolve.
   - The hand limit discard (M15) lowers it as well.
   - On a client whose opponent session isn't known yet, the line stays empty and nothing throws.
3. **N64:** the Confirm Turn label reads "Confirm Turn" / "Подтвердить ход" / "ターンを確認", follows a locale change at once, and is at least 30 units in both profiles.
4. **Layout:** at 1920×1080 and 1280×720 in en / ru / ja, the two new lines and their boxes overlap no other text, box or button. The low-profile hint line at y −410 overlaps nothing. A two-line announcer and the longest hint are included.
5. **Locale:** switching language rewrites both lines at once.
6. **No regression:** mana lines, HP / Life, hints, the big card and board clicks under the top-right column work as before. The UI6 sweep has no new diagnostic. The release player builds with 0 errors and no warnings of our own.

## 8. Regression tests

- **Play Mode harness** (temporary, `Temp/F15Verification/`), with the host fixture from TASK-F9_F14a_N62 (two real sessions, the real `PlayerUIPrefab`):
  - AC1: start a match, read `m_deckCount` after each draw; force a refill (empty `m_deck` through a test hook or reflection) and check the jump; check the empty line in `None` / `GameOver`.
  - AC2: for each session, compare the opponent line with `GetUncommittedHandCount()` while you add cards to the board lists and run a hand-limit discard.
  - **Client order:** run `PlayerUI.Update` once with `GameMaster.Instance == null` and once with an unknown opponent ID: no exception, empty lines.
  - AC3: read the Confirm Turn text in en / ru / ja and its font size in both profiles.
- **Baseline:** on the baseline, record that the Confirm Turn text is "Confirm turn" in ru (N64).
- **Edit Mode:** `GetUncommittedHandCount()` with hand {1, 2, 3, 4}, play {2}, discard {4} → 2.
- **Screenshots** (into `build/f15-f16-n64-verification/`): the HUD at 1920×1080 and 1280×720 in ru, during Card Selection, with both counters and a hint shown.
- **UI6 sweep** as in §6.
- Remove the harness from `Assets` and `Temp`; keep a copy in `build/f15-f16-n64-verification/`.

## 9. Manual Unity Editor checks

**Setup:** two Editors (main = host, clone = client, `-logFile C:\Lesson\clone-editor.log`), 1920×1080, then 1280×720 for step 5, then a new build for step 7.

1. **Deck, both windows:** during "Drawing cards" the deck line counts down one per card, the same in both windows.
2. **Opponent's hand, host window:** it rises by 2 in "Drawing cards". When the client confirms its cards, it drops at once by the number of cards the client put on the table.
3. **Opponent's hand, client window:** the same for the host's hand. (The client side is the one no harness covers.)
4. **Deck refill** (only if a match lasts long enough): the line reaches 0, then jumps up at the next draw, together with the "deck ran out" hint.
5. **Layout:** at 1280×720 in ru and ja, the right column shows five lines with boxes; the hint line sits below them and touches nothing.
6. **Confirm Turn label:** ru "Подтвердить ход", ja "ターンを確認"; switching language changes it at once.
7. **Release build** against an Editor: both counters in both windows; `Player.log` has no new warning or exception.

## 10. Open questions and assumptions

- **A1, where committed cards count.** A card the opponent put on the table in Card Selection no longer counts as "in hand", although the server keeps it in `m_handCards` until it reaches the graveyard. If you'd rather count everything the opponent drew and hasn't lost yet, it's one line (`m_handCards.Count`).
- **A2, the label texts** ("Opponent's hand : 5", "Deck : 37", ru "Рука соперника", ja "相手の手札" / "山札") follow the existing " : " style. Change them in the tables any time.
- **A3, the low-profile hint line** moves from y −330 to −410 to make room. If you'd prefer the counters elsewhere in the compact layout (for example top left), say so before Codex starts.
- **A4, `m_deckCount` counts `null` entries** that `DrawCard` would skip. They appear only if a card object was destroyed outside the deck code, which no current path does.
