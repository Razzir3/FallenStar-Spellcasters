# TASK-MANA3: Mana display and the "not enough mana" message (en / ru / ja)

- **Source:** [RESEARCH-ManaSystem](../Research/RESEARCH-ManaSystem.md) (steps S6–S7)
- **Owner decisions:**
  - mana is visible (D6)
  - capacity model (D1 = B), so show **current / capacity**
- **Depends on:** TASK-MANA1 and TASK-MANA2
- **Localization requirement:** all new visible text exists in **English, Russian and Japanese** through `UI_table`, with no hard-coded strings (`AGENTS.md`).
- **Implementer:** Codex (UI wiring in the Unity Editor)

## 1. Objective

- The local player always sees their own mana as "current / capacity".
- The opponent's mana is visible too.
- When a confirm is rejected for mana (MANA2), the player sees a short localized "Not enough mana" message.

## 2. Current behavior

- `PlayerUI.Update` polls HP and life and formats them with `LocalizedString` fields: `m_labelHp`, `m_labelLife`, with key `label_life` = "Life : {0}" (`PlayerUI.cs:26-27, 113-126`).
- The texts are cleared in `ClearLocalUI` (`:397-430`).
- `GameAnnouncerUI` shows the phase, the timer and the active player (`UI/GameAnnouncerUI.cs`).
- After MANA2, `PlayerSession.SelectionRejectedClientRpc` has a `//Todo MANA3` hook.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/UI/PlayerUI.cs` | Mana text fields and polling; a rejection message |
| `Assets/Prefab/UiPrefab/PlayerUIPrefab.prefab` | New TMP text objects (in the Editor) |
| `Assets/Scripts/InGame/PlayerSession.cs` | Call the UI message from `SelectionRejectedClientRpc` |
| `Assets/Localization/UI_table*` | New keys |

## 4. Required changes

1. **`PlayerUI` fields:**
   - `public TMP_Text m_manaText;` and `[SerializeField] private LocalizedString m_labelMana;` (`label_mana_capacity`)
   - **Opponent's mana:** `public TMP_Text m_opponentManaText;` and `[SerializeField] private LocalizedString m_labelOpponentMana;` (`label_mana_opponent`)
   - `public TMP_Text m_messageText;` and `[SerializeField] private LocalizedString m_manaNotEnoughText;` (`mana_not_enough`), plus `[SerializeField] private float m_messageDuration = 2f;`
2. **Polling** in the same `Update` block as HP and life:
   - own mana: `m_manaText.text = m_labelMana.GetLocalizedString(current, capacity)`
   - opponent's mana: get the opponent session through `m_playerSession.m_currentOponentNetworkObjectId` and the existing session lookup used elsewhere in the UI, with a null check. Show it the same way, or clear the text if the opponent isn't found.
   - Only update when the values change. Keep the last values in private ints, so the strings aren't rebuilt every frame (L6).
3. **Rejection message:** add `public void ShowManaNotEnough()`, which shows `m_manaNotEnoughText` in `m_messageText` and hides it after `m_messageDuration` (a coroutine or timer). Call it from MANA2's `PlayerUI.OnSelectionRejected()`, or directly from `SelectionRejectedClientRpc` in place of the `//Todo MANA3` hook.
4. **Optional, local feedback:** when `SelectCardToPlay` returns `false` **because of mana**, also call `ShowManaNotEnough()`. This needs MANA2's gating to report the reason. Choose a minimal approach, such as a public `bool CanAffordToPlay(NetworkCard)` on `PlayerSession` that `PlayerUI` checks before calling.
5. **`ClearLocalUI`:** clear the new texts.
6. **Prefab (Editor):** add the three TMP texts to `PlayerUIPrefab`, near the HP and life texts, using the same font asset as the others (MPLUSRounded1c, so Japanese renders). Assign the fields and `LocalizedString` references.
7. **Localization keys** (`UI_table`, through the Localization Tables window):

| Key | en | ru | ja |
|---|---|---|---|
| `label_mana_capacity` | Mana : {0} / {1} | Мана : {0} / {1} | マナ : {0} / {1} |
| `label_mana_opponent` | Opponent mana : {0} / {1} | Мана соперника : {0} / {1} | 相手のマナ : {0} / {1} |
| `mana_not_enough` | Not enough mana | Недостаточно маны | マナが足りません |

The texts are proposals for the owner to adjust. Don't touch the existing key `"btn_lobbyRefresh\t"` (L8).

## 5. Explicit non-goals

- Mana logic (MANA1 and MANA2).
- Dimming unaffordable cards in the hand. That's possible later in `CardViewUI`.
- A language selector (M12).
- Redesigning `PlayerUI` (N1 `OnDestroy` and the polling architecture stay as they are).

## 6. Constraints and compatibility

- No renames. Additive fields. Editing the prefab is allowed only through the Editor.
- All visible text comes from `UI_table` in en/ru/ja.
- No string allocations every frame when nothing changed.

## 7. Acceptance criteria

- [ ] Own mana shows "Mana : 2 / 2" at match start and updates after confirm (e.g. "Mana : 0 / 2") and at each round break ("4 / 4", …), on host and client.
- [ ] The opponent's mana is shown and updated correctly on both sides.
- [ ] A rejected confirm (MANA2 tamper test) shows "Not enough mana" for about 2 s.
- [ ] If step 4.4 is done: clicking an unaffordable play card shows the same message.
- [ ] All three texts are correct in en, ru and ja, and render without missing glyphs.
- [ ] `ClearLocalUI` empties the texts.
- [ ] Compiles without new warnings.

## 8. Regression tests

No automated tests. Review:
- no hard-coded strings
- values are cached so strings are only rebuilt on change

## 9. Manual Unity Editor checks

1. ParrelSync host and client. Watch both mana labels over 3 rounds, including after confirms.
2. Run the MANA2 tamper test. The message appears on the cheating client only.
3. Switch the locale to ru and ja and check all labels.
4. Disconnect and return to the lobby. No stale mana text remains.

## 10. Open questions and assumptions

- **Q1:** should the opponent's mana be shown in `PlayerUI`, or in the announcer area? The proposal is `PlayerUI`, next to your own mana.
- **A1:** `m_currentOponentNetworkObjectId` is set for both sessions in `StartNewGame` (`SetOponent`) and is readable on clients.
