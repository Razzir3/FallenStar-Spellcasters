# N19 implementation: leave match with confirmation

## 1. Task summary

Implemented `TASK-N19_LeaveMatchWithConfirmation.md`: a localized confirmation dialog requests server-authoritative surrender during a running match. The existing H5 result transition presents defeat to the leaver and victory to the opponent. Before match start, confirmation uses the existing lobby disconnect path.

## 2. Files changed

- `Assets/Scripts/InGame/GameMaster.cs`: append `MatchEndReason.Surrendered`.
- `Assets/Scripts/InGame/GameMaster/GameMaster.Player.cs`: add `HandlePlayerSurrender`.
- `Assets/Scripts/InGame/PlayerSession.cs`: owner request and validated ServerRpc.
- `Assets/Scripts/UI/LeaveMatchUI.cs` and Unity-generated `.meta`: dialog, button state and timeout.
- `Assets/Scripts/UI/ResultMenuUI.cs`: localized surrender reason selection.
- `Assets/Prefab/UiPrefab/PlayerUIPrefab.prefab`: LeaveMatch controls and serialized bindings.
- `Assets/Scenes/ResultScene.unity`: assign `m_reasonYouLeft`.
- `Assets/Localization/UI_table Shared Data.asset`, `UI_table_en.asset`, `UI_table_ru.asset`, `UI_table_ja.asset`: five new keys and translations.
- `Assets/Fonts/MPLUSRounded1c-Medium SDF.asset`: Unity-generated dynamic glyph/atlas cache updates while previewing the new localized text.
- `Docs/AI/Architecture/GameLoop.md`, `Dependencies.md` and this report.

Existing uncommitted N14 and N18/M1 changes and reports were retained. Their edits to shared files are separate from N19. No commit, push, reset or discard was performed. Temporary Editor setup/preview code was moved out of Assets to ignored `build/task-n19-verification/EditorTools` after use.

## 3. Important implementation details

- Surrender is additive: enum ordering is preserved, and H6 `HandlePlayerLeft`, H5 `EndMatch`/result transition and N14 startup handling remain unchanged. The server stops the running coroutine, clears resolution/respawn flags, retains both sessions for result delivery and selects the other eligible team. No opponent delegates to the existing abort path.
- The client can request surrender only for its owned session. The owner-only ServerRpc independently validates `SenderClientId`, recording a mismatch on the sender's session rather than the victim.
- `LeaveMatchUI` guards duplicate confirmation and stale GameOver confirmation. Its unscaled five-second deadline is set before making the request, allowing an immediate host result to cancel it. GameOver hides the dialog, disables the button and clears the deadline; timeout fires once.
- LeaveBtn occupies the free top-left corner. The new LeaveMatch rect follows the root canvas dimensions at runtime because the existing prefab root is a centered 100 by 100 rect. Existing HUD rects are unchanged. The new full-viewport panel uses the existing HUD canvas, has a raycast-target Image and is initially inactive. All new text uses MPLUSRounded1c and LocalizeStringEvent; all three button bindings and the two UI references are assigned.
- Prefab and ResultScene were saved using Unity PrefabUtility/Editor APIs, not edited as YAML. Translations were imported through Localization Tables' CSV Merge interface. Existing localization entries, including the tab-suffixed lobby-refresh key, are untouched.
- Required Inspector setup: none for the supplied assets. Custom copies need the LeaveMatchUI button/panel references, three click bindings, localized labels and `ResultMenuUI.m_reasonYouLeft` assignment described in the task.

## 4. Tests actually executed and results

- Compiled runtime sources with Unity 6000.3.13f1's bundled Roslyn compiler and the project's Assembly-CSharp response-file references/analyzers. Exit 0; only existing CS0114 (`PlayerUI.OnDestroy`) and CS0414 (`GameMaster.m_cardResolveDelay`) warnings. Evidence is in ignored `build/task-n19-verification/compile.rsp` and `compile-results.txt`.
- Unity Editor imported/compiled the runtime and temporary Editor tooling successfully. The final refresh after tool removal displayed zero Console errors. This does not establish a player build or remote networking success.
- Ran `build/task-n19-verification/verify.ps1`: **30 isolated source scenarios passed**. The harness compiles production surrender/RPC/UI/result methods and unchanged H6/EndMatch methods with Unity/NGO doubles. It covers either leaver in all seven nonterminal phase enum values, server/member/phase guards, no-winner abort, duplicate/natural-end/disconnect races, owner and sender validation, Cancel, None disconnect, GameOver cancellation, five-unscaled-second fallback, missing references and all three result outcomes. These are source-level scenarios, not actual host/client sessions.
- Play Mode visual preview instantiated the actual updated HUD prefab under a temporary canvas and an unspawned GameMaster fixture. Physical Leave button input opened the dialog, and Cancel closed it. English, Russian and Japanese dialog labels fitted and updated when the locale changed. A temporary `MatchResultData.Set(false, false, Surrendered)` fixture loaded the actual ResultScene; its defeat title and new reason rendered correctly in all three languages. No new missing-glyph warning was observed. The preview was stopped and its controls removed.
- Final prefab block comparison: all 66 pre-existing serialized objects retained; 46 new objects/components; the only modified existing block is the root RectTransform's added child reference. ResultScene contains only the nine added localization-reference lines. Localization diffs only add the five required entries.
- Diff review completed. `git -c core.whitespace=-blank-at-eol diff --check` passed. Plain `git diff --check` reports Unity's generated blank-value trailing spaces in the prefab; those native serialized lines were retained rather than hand-editing YAML.

## 5. Remaining risks / checks not executed

- No real two-player ParrelSync host/client surrender, Relay delivery, host shutdown acknowledgement, full match or player build was executed. The fixture result preview does not test the H5 transport transition.
- Card/world click blocking is wired through the panel's raycast-target Image and existing PlayerSession EventSystem pointer guard, but actual card/world interaction behind the modal still needs a running-match check. The temporary standalone raycast diagnostic returned no hits and is not counted as passing evidence.
- The layout was previewed at the Editor's 1920 by 1080 game-view setting. Verify corner placement and panel coverage at the project's other supported aspect ratios and with the live hand/mana HUD.
- Fallback timing and race scenarios passed with doubles; real Unity coroutine/network timing remains to be checked. No penalty/statistics/reconnect behavior was added.

## 6. Manual Unity Editor verification

1. Start a ParrelSync host/client match. On each peer, open Leave match, click the dimmed background over cards/world objects, and verify there is no selection. Cancel must resume unchanged. Check the button stays clear of the hand, announcer and mana labels at supported resolutions.
2. In CardSelection, have the client confirm Leave. Both peers must reach ResultScene: client defeat / "You left the match.", host victory / existing opponent-left reason. Return both to Main menu and start another match.
3. Repeat with the host leaving during its own PlayerAction, then with either peer leaving during the opponent's turn, RoundBreak and RespawnWait. Confirm the client receives its result before host shutdown.
4. Check phase None returns to lobby; GameOver closes/disables the dialog; rapidly double-clicking confirmation and racing an opponent disconnect produces one result and no errors.
5. Temporarily suppress the server surrender call as described in the task, verify about five unscaled seconds to the fallback lobby disconnect and the opponent's H6 result, then restore the call.
6. Switch en/ru/ja while the dialog and result reason are visible; verify exact translations, no clipping/missing glyphs, and correct winner/loser text on both peers. Check the Console and run a player build.
