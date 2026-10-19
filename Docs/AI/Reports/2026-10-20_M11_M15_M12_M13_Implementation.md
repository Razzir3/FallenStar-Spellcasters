# M11, M15, M12 and M13 implementation

## 1. Task summary

Implemented the four specifications against `a0df515` plus the existing uncommitted M16/N1 changes:

- M11 parks the unused join-by-code field and method as comments, retaining the method body unchanged.
- M15 limits the uncommitted hand to 10 by default, discards oldest excess cards after successful draws, prunes stale local selections and shows an owner-only localized notice.
- M12 adds a separate language SettingsScene, main-menu entry, native language names and enabled build-scene entry. Existing locale persistence is reused.
- M13 makes shared lobby/player names language-neutral and localizes viewer-side counts and fallback names.

## 2. Files changed

- `Assets/Scripts/MultiplayerScripts/MultiplayerManager.cs`: M11 parking and M13 neutral names/protocol constants.
- `Assets/Scripts/InGame/GameMaster.cs`, `GameMaster/GameMaster.Card.cs`, `PlayerSession.cs`: M15 rule, discard and selection/RPC paths.
- `Assets/Scripts/UI/PlayerUI.cs`: M15 message/pruning support, retaining N1 and earlier card/UI work.
- `Assets/Scripts/UI/MainMenuUI.cs`, `SettingsUI.cs`: M12 navigation and dropdown hardening/native names.
- `Assets/Scripts/UI/LobbyListUI.cs`, `LobbyListItemUI.cs`, `LobbyPlayerItemUI.cs`: M13 localized display and locale callbacks.
- `Assets/Scenes/MainMenuScene.unity`: new Settings button. New `Assets/Scenes/SettingsScene.unity` and Unity-generated `.meta`.
- `ProjectSettings/EditorBuildSettings.asset`: one enabled SettingsScene entry immediately after MainMenuScene.
- `Assets/Prefab/UiPrefab/PlayerUIPrefab.prefab`: only the new `m_handLimitDiscardText` reference.
- `Assets/Localization/UI_table Shared Data.asset` and en/ru/ja tables: six new keys (`btn_settings`, `settings_title`, `label_language`, `lobby_name_unknown`, `lobby_player_unknown`, `hand_limit_discard`).
- `Assets/Fonts/MPLUSRounded1c-Medium SDF.asset`: Unity-generated dynamic glyph/atlas cache updates for localized text.
- Architecture.md, GameLoop.md, Dependencies.md and this report.

M16/N1 changes/report were preserved. GameScene, LobbyScene, LoginScene, ResultScene, GameMasterPrefab and the existing lobby item prefabs were not changed. No commit, push, reset or discard was performed. Temporary setup/verification Editor scripts and their metas were moved out of Assets into ignored `build/task-m11-m15-m12-m13-verification/EditorTools` after use.

## 3. Important implementation details

### M11 and M13

- The parked method body is text-identical to the original. Its input field and method are excluded after stripping comments; serialized lobby-code fields and SetLobbyCode are retained.
- Host/Client remain protocol constants, never localized on the wire. Player and lobby names use the short ID without a role prefix; an empty ID uses a six-digit numeric fallback. Existing create/list-join error handling and role data keys/values remain.
- Counts use the existing smart `lobby_players_count` translation and IntVariables. New fallback fields have initializers, so existing lobby prefabs need no edits. Balanced StringChanged/locale subscriptions refresh fallback names and counts, including the selected-lobby status fallback.

### M15

- The private limit helper additionally guards IsServer and runs only after a successful draw. It builds an oldest-first copy of IDs absent from both committed lists, then reuses MoveCardOnGraveyard for actual cards and RemoveNetworkObjectId for stale IDs. One targeted RPC is emitted per discard event; the receiver also checks IsOwner. Existing graveyard/refill cleanup remains the disposal path.
- Both selection lists prune null/no-longer-held cards on UI rebuild and before ordinary ConfirmTurn ID conversion. The separate respawn-confirm path is untouched. Valid N26 carry-over and committed cards are retained.
- ShowMessage tracks the active localized message and existing unscaled deadline. Either message updates correctly on locale changes; expiry and ClearLocalUI clear the active reference. The existing mana message duration and N1 OnDestroy override remain.

### M12 and assets

- Settings was cloned from the existing main-menu camera, light, canvas/scaler, logo/panel, button style and InputSystem EventSystem. Its skybox was explicitly copied after visual inspection caught the default background. It contains SettingsUI and SettingsSceneButtonsUI, with no DDOL managers. The language dropdown uses the same MPLUS font for caption and template labels.
- The main-menu Settings button sits in the free space between Lobby and Exit without moving either existing button. Back invokes the existing SettingsSceneButtonsUI method. SettingsUI handles a missing dropdown and displays capitalized CultureInfo.NativeName with existing locale-name/code fallbacks.
- Scene, prefab, table and build-list edits were performed through Unity Editor APIs; no scene/prefab YAML was hand-edited. The Editor's Save Project operation persisted the build-scene list. All supplied references are assigned; no additional Inspector setup is required. The limit remains a tunable serialized field using its C# default, without resaving GameMasterPrefab.

## 4. Tests actually executed and results

- Runtime C# compilation with Unity 6000.3.13f1's bundled Roslyn compiler and current Assembly-CSharp response-file references/defines/analyzers: exit 0, no new errors or warnings. Only existing CS0414 (`GameMaster.m_cardResolveDelay`) remains. Evidence is in ignored `compile.rsp`, `compile-results.txt`, `compile-exit.txt`.
- Unity Editor compiled the runtime and temporary Editor tools, including Netcode postprocessing. The final refresh after tool removal also completed successfully (Tundra build success, assembly reload and three deleted source assets). Selected log evidence is saved in `editor-compile-evidence.txt` in the verification directory.
- `verify.ps1`: **16 isolated source scenarios passed** with Unity/NGO doubles. They exercise default/disabled/at-limit behavior, oldest ordering, protection of both committed lists, multiple excess cards, stale IDs, failed/null-session draws, server authority, existing graveyard refill cleanup, pruning and pre-confirm IDs, owner/respawn gates, owner-only notice receiver, active-message locale/deadline behavior, missing HUD text and neutral ID/protocol values. These are not transport or rendered-match tests.
- Actual Play Mode prefab/localization fixture: **three language passes (en/ru/ja)** using real LobbyListItemPrefab, LobbyPlayerItemPrefab, PlayerUIPrefab and local sample Lobby/Player data. Verified neutral IDs remain unchanged, smart counts and roles localize, player/lobby/selected-status fallbacks update, and both HUD messages resolve/refresh correctly. It used no live Lobby service or network session. Evidence: `localization.done`; the fixture was destroyed and its Editor source removed from Assets.
- Actual Settings navigation: booted from LoginScene and used a temporary Editor entry to open the actual main menu without a login interaction. Physical Settings and Back clicks worked. English, Russian and Japanese text/native dropdown names rendered and switched; Russian carried back to the main menu. Japanese persisted through stopping/restarting Play Mode, and the final Settings scene displayed the copied main-menu background. The original Japanese preference was restored.
- Static asset review: all 52 existing MainMenuScene serialized objects retained; ten new objects/components; the only changed existing block is the canvas's added child reference. All 112 existing HUD prefab objects retained, with only PlayerUI's nine-line new localization field. Build settings add only three lines. Existing localization entries, including the tab-suffixed lobby-refresh key, are retained.
- Final Git diff review completed. `git -c core.whitespace=-blank-at-eol diff --check` passed; the plain check flags Unity-generated trailing spaces on empty serialized values in new scene objects. Those native asset lines were retained rather than hand-editing scene YAML.

## 5. Remaining risks / checks not executed

- No live host/client create/list-join/start regression, mixed-language service session, real hand-limit/Clarity match or player build was executed. NetworkList/RPC ordering and remote notice delivery need the manual tests.
- Stopping the old pre-existing Play session logged an NGO NetworkSceneManager.Dispose / NetworkManager.OnDestroy NullReferenceException. Its stack was in package shutdown code; the cause was not established or changed by these tasks. Fresh local settings/localization verification passed, but real network shutdown regression remains necessary.
- Settings was visually checked at the Editor's existing 1920 by 1080 game-view setting. Verify other supported resolutions/aspect ratios and Windows player CultureInfo behavior. Font-cache changes are generated by Unity.
- N26 valid carry-over remains intentionally unchanged. Join-by-code remains parked. No hand animation/count display, player-chosen names or other settings were added.

## 6. Manual Unity Editor verification

1. Normal LoginScene flow: log in, open Settings, choose each language, go Back and verify main-menu/lobby text. Restart Play Mode/player and verify the selected locale persists. Check Japanese glyphs and alternate resolutions.
2. ParrelSync host/client with different locales: create, refresh, select, join and start from the lobby list. Names should be short neutral IDs; counts/roles use each viewer's language. Change the locale with Editor localization controls while viewing items and verify updates. Join-by-code should have no compiled/UI entry.
3. Temporarily set m_maxHandCards to 3 through the GameMaster prefab Inspector. Pass selection without committing, draw across the limit and verify the leftmost uncommitted card goes to the graveyard and only its owner sees one notice. Restore only that temporary Inspector edit afterward.
4. Confirm Clarity and other board cards before resolution; its draws must discard oldest uncommitted cards while preserving both committed lists. Verify failed draws discard nothing and a zero limit retains the previous unlimited behavior.
5. Carry an unconfirmed selection across a round until its card is discarded. Confirm a new valid selection and check no false InvalidCardSelection report. Verify mana costs/highlights stay correct.
6. Switch locale while each HUD message is visible, verify text and expiry, play a round, leave/start again and inspect the Console, especially NGO shutdown. Finish with a Windows player build.
