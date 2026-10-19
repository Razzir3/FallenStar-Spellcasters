# TASK-M14: Client card-data null guards, and a hand refresh when the data arrives

- **Source issue:** [Issue Report #6 → M14](../Reports/2026-10-19_IssueReport-6.md#-medium-13) (first reported in #1)
- **Severity:** 🟡 Medium (Plausible)
- **Baseline:** `fe2d4fb`. Line numbers below are from this commit.
- **Order:** 2 of 13. Do it after N20, which already guards `CheckPackage`.
- **Implementer:** Codex

## 1. Objective

Client code that reads `NetworkCard.m_cardData` never throws when the data hasn't arrived yet or couldn't be found. The hand UI shows a card as soon as its data arrives.

## 2. Current behavior

**How clients get card data:**
- `m_cardData` isn't networked. `CardPrefab.prefab:74` has `m_cardData: {fileID: 0}`.
- The server sets it in `CreateDeck` (`GameMaster.Card.cs:322`, `:352`).
- Clients receive it through `GameMaster.SendCardDataClientRpc` (`GameMaster.PackageManaging.cs:13-40`) → `NetworkCard.RepairCardData` (`NetworkCard.cs:159-164`). It's sent:
  - to the owner on each draw (`GameMaster.Card.cs:180`)
  - to everyone when a card goes to the board (`PackageManaging.cs:222`, `:232`)

**The race:** the hand-list change (a `m_handCards` NetworkList delta) and the RPC are separate messages. NGO doesn't promise their order, so the list change can arrive first (Plausible; not seen in logs yet).

**Consequences:**
- `PlayerUI.CreateCardView` skips cards without data (`UI/PlayerUI.cs:267-268`). Nothing rebuilds the hand when the data arrives (the RPC only refreshes `CardWorldViewManager`), so the card is missing until the next list or phase change.
- `RepairCardData` overwrites `m_cardData` with `null` when the ID isn't in the client's database. `CardDatabase.GetCard` logs an error and returns `null`. It also reads `GameMaster.Instance` without a check.

**Unguarded reads:**

| Location | Read |
|---|---|
| `NetworkCard.CheckActiveTaergetSelection` (`NetworkCard.cs:143-156`) | `m_cardData.m_activities` |
| `PlayerSession.TryConfirmTarget` (`PlayerSession.cs:828` via the method above, `:841`, `:849`) | `m_selectedCard.m_cardData.m_activities` |
| `PlayerSession.AutoSelectTarget` (`:1061-1079`) | same |
| `CardViewUI.OnPointerClick` (`CardViewUI.cs:85`, `:91`) | `m_networkCard.m_cardData.m_nameKey` in two log lines |
| `PlayerSession.CheckPackage` (`:1038`) | **fixed by N20** |

**Already guarded:** `SelectCardToPlay` (`:265`), `SelectedPlayCost` (`:280`), `CardPreviewController` (`:37`), `CardWorldViewManager` (`:632`), `PlayerUI.CreateCardView` (`:267`).

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/PlayerSession.cs` | Guards in `TryConfirmTarget` and `AutoSelectTarget` |
| `Assets/Scripts/InGame/NetworkCard.cs` | Guard in `CheckActiveTaergetSelection`. `RepairCardData` keeps the old data when the lookup fails. **Not a UTF-8 file (N24), see §6.** |
| `Assets/Scripts/InGame/CardViewUI.cs` | Null-safe log lines |
| `Assets/Scripts/InGame/GameMaster/GameMaster.PackageManaging.cs` | `SendCardDataClientRpc` tells `PlayerUI` that the data arrived |
| `Assets/Scripts/UI/PlayerUI.cs` | New public method that rebuilds the hand when a card that had no view gets its data |

Read-only: `CardDataBase.cs` (`TryGetCard(string, out CardData)` at `:44-54`).

## 4. Required changes

1. **`PlayerSession.TryConfirmTarget`:** right after the existing `m_selectedCard == null` check (`:784-788`), before any package code:
   ```csharp
   if (m_selectedCard.m_cardData == null || m_selectedCard.m_cardData.m_activities == null)
   {
       Debug.LogWarning("Selected card has no card data yet.");
       return false;
   }
   ```
2. **`PlayerSession.AutoSelectTarget`:**
   - return early if `m_selectedCard`, `m_cardData` or `m_activities` is null
   - loop to `Mathf.Min(m_activities.Length, m_currentCardControllPackage.TargetNetworkObjectId.Count)` so a card with more than 4 slots can't index past the package
3. **`NetworkCard.CheckActiveTaergetSelection`:** first line `if (m_cardData == null || m_cardData.m_activities == null) return false;`.
4. **`NetworkCard.RepairCardData`:**
   - `GameMaster.Instance == null` → warning, return
   - `string.IsNullOrEmpty(cardId)` → warning, return
   - get the database with `GameMaster.Instance.GetCardDatabase()`; if it's null → warning, return
   - `TryGetCard(cardId, out CardData _data)` fails → `Debug.LogWarning($"RepairCardData: card id '{cardId}' was not found.")` and return **without** overwriting `m_cardData`
   - otherwise `m_cardData = _data`

   Keep the existing `Debug.Log("RepairCalled")`.
5. **`CardViewUI.OnPointerClick`:** compute `string _nameKey = m_networkCard.m_cardData != null ? m_networkCard.m_cardData.m_nameKey : "<no data>";` once, and use it in both log lines. Keep invoking `Clicked` / `AlterClicked` as now.
6. **`PlayerUI`:** add
   ```csharp
   public void OnCardDataReceived(ulong cardNetworkObjectId)
   {
       if (m_playerSession == null)
           return;

       if (m_cardViews.ContainsKey(cardNetworkObjectId))
           return;

       if (!m_playerSession.m_handCards.Contains(cardNetworkObjectId) &&
           !m_playerSession.m_boardPlayCards.Contains(cardNetworkObjectId))
           return;

       UpdateCards(default);
   }
   ```
7. **`GameMaster.SendCardDataClientRpc`:** after `RepairCardData` and the `CardWorldViewManager` refresh, add `if (PlayerUI.Instance != null) PlayerUI.Instance.OnCardDataReceived(cardNetworkObjectId);`.

## 5. Explicit non-goals

- Networking `m_cardData` (for example a card-ID NetworkVariable) or changing how or when the RPC is sent (Q1).
- `CheckPackage` (N20).
- L7, refreshing card texts on a locale change.
- Removing debug logs.
- Renaming `CheckActiveTaergetSelection` (naming list, report only).
- Converting `NetworkCard.cs` to UTF-8 (N24 needs owner approval).

## 6. Constraints and compatibility requirements

- **Encoding:** `NetworkCard.cs` isn't UTF-8. Its Cyrillic comments use a legacy code page; `file` reports "Non-ISO extended-ASCII text". Edit only `CheckActiveTaergetSelection` and `RepairCardData`. Don't save the file as UTF-8, and keep CRLF line endings. Check `git diff Assets/Scripts/InGame/NetworkCard.cs`: only the intended lines may change, and the comment block at `:179-183` must stay byte-for-byte the same. **If your tool can't keep the encoding, stop and report instead of converting.**
- Client-side code, plus the RPC body, which runs on every client including the host.
- Public API: adds `PlayerUI.OnCardDataReceived`. No renames, no serialized-field changes, no asset changes.
- The extra `UpdateCards` keeps the N18 highlight restore. With N20 in place, `ValidateSelectedCard` won't clear a selected card that's still present.

## 7. Acceptance criteria

- [ ] None of the reads listed in §2 can throw when `m_cardData` is null (review; optional test in §9).
- [ ] When the data RPC arrives after the hand-list change, the card appears in the hand right away.
- [ ] `RepairCardData` with an unknown ID logs a warning and keeps the previous data.
- [ ] Normal play is unchanged: drawn cards appear, and selecting and casting work.
- [ ] The `NetworkCard.cs` diff contains only the intended lines, and the encoding is unchanged.
- [ ] Compiles with no new warnings.

## 8. Regression tests

No automated tests. **Review checklist:**
- search `PlayerSession.cs`, `NetworkCard.cs`, `CardViewUI.cs` and `PlayerUI.cs` for `m_cardData.`; every client-path read is guarded or comes after a guard
- `OnCardDataReceived` rebuilds only for a local hand or board card that has no view yet, so it doesn't rebuild on the opponent's board cards

## 9. Manual Unity Editor checks

1. **Normal match** (ParrelSync host + client): drawn cards appear on both sides. Select and cast all four card types.
2. **Delayed data, temporary code edit (don't commit):** in `GameMaster.DrawCard`, replace the `SendCardDataClientRpc(...)` call with a coroutine that waits 1 s and then sends the same RPC. Expected:
   - each card appears in the client's hand about 1 s after its draw, with no exceptions
   - clicking it after it appears works normally

   Remove the edit afterwards.
3. **Clarity in an action turn:** the two drawn cards appear in the next `CardSelection` hand.

## 10. Open questions and assumptions

- **A1:** The ordering race is Plausible and hasn't been seen in a log. The guards are cheap, so they're worth adding anyway.
- **Q1:** Should `m_cardData` become networked state, for example the card ID in a `NetworkVariable<FixedString32Bytes>`? That would remove the race entirely, but it's a bigger change and isn't in this task.
