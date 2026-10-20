# TASK-L7: Card texts follow a locale change

- **Source issue:** [Issue Report #7 → L7](../Reports/2026-10-20_IssueReport-7.md#-low-23)
- **Severity:** 🟢 Low (Confirmed, latent). Since M12, players can change the language only in the main menu, so it's reachable only with the Editor's Localization Scene Controls during a match.
- **Baseline:** `b7e7b91`. Line numbers below are from this commit.
- **Order:** 16 of 20. Independent.
- **Implementer:** Codex

## 1. Objective

The hand cards (`CardViewUI`) and the world cards (`CardViewWorld`) re-read their name and description when the selected locale changes, as the HUD texts already do.

## 2. Current behavior

- `InGame/CardViewUI.cs:29-39` `Setup(CardData data)` and `InGame/CardViewWorld.cs:57-87` `Setup(CardData data)` read the name and description **once**, with `LocalizationSettings.StringDatabase.GetLocalizedString("Cards", data.m_nameKey / m_descKey)`.
- Neither class keeps the `CardData` or listens to `LocalizationSettings.SelectedLocaleChanged`. After a locale change, existing cards keep the old language until they're rebuilt. Hand views are rebuilt on every hand change (`PlayerUI.UpdateCards`); world views aren't.
- HUD texts already follow locale changes: `PlayerUI.OnSelectedLocaleChanged` and the `LocalizedString.StringChanged` events.
- `CardViewBase` (`InGame/CardViewBase.cs`) is the shared abstract base: `Setup`, `SetFaceUp`, `SetRotated`.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/CardViewBase.cs` | Keep the last `CardData`; subscribe and unsubscribe to the locale change; a protected virtual `ApplyTexts()` |
| `Assets/Scripts/InGame/CardViewUI.cs` | Move the two text lines into `ApplyTexts()`; `Setup` stores the data and calls it |
| `Assets/Scripts/InGame/CardViewWorld.cs` | Same; keep its null check and warning |

## 4. Required changes

1. **`CardViewBase`:**
   - `protected CardData m_textData;`
   - `protected virtual void OnEnable()` → `LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;`
   - `protected virtual void OnDisable()` → `-=`
   - `private void OnLocaleChanged(Locale _) { if (m_textData != null) ApplyTexts(m_textData); }`
   - `protected abstract void ApplyTexts(CardData data);`
2. **`CardViewUI` / `CardViewWorld`:**
   - In `Setup`, set `m_textData = data;` and replace the two `GetLocalizedString` lines with `ApplyTexts(data);`.
   - `ApplyTexts` holds exactly those two lines.
   - If a derived class already has `OnEnable`, `OnDisable` or `OnDestroy`, make it `override` and call `base`. `CardViewWorld.OnDestroy` (`:42-52`) stays as it is; it isn't in the base.
3. The cost and the sprites don't depend on the locale. Leave them in `Setup`.

## 5. Explicit non-goals

- No change to the `Cards` table, the keys or the card assets.
- No new language option outside the main menu (M12 non-goal).
- No change to how `PlayerUI` rebuilds hand views.

## 6. Constraints and compatibility requirements

- `LocalizationSettings.SelectedLocaleChanged` is a static event. Always unsubscribe in `OnDisable`, or destroyed card views leak.
- Keep the serialized field names in both views; the card prefabs reference them.
- If an `OnEnable` before `Setup` would call `ApplyTexts` with null data, the `m_textData != null` guard covers it.

## 7. Acceptance criteria

1. With cards in the hand and on the board, switching the locale (Localization Scene Controls) updates every visible card name and description within a frame.
2. Destroyed cards don't throw on a later locale change: no `MissingReferenceException`.
3. Card look and behaviour are otherwise unchanged.

## 8. Regression tests

- Roslyn compile. Exit 0.

## 9. Manual Unity Editor checks

1. A two-player match with cards in hand and on the board. Open Window → Asset Management → Localization Scene Controls and switch en → ru → ja. The hand cards and the world cards change language each time. Japanese glyphs render.
2. Play a few cards (they go to the graveyard and are destroyed), then switch the locale again. There are no exceptions.
3. Start from the main menu in ru: the cards are Russian from the start, as before.

## 10. Open questions and assumptions

- None.
