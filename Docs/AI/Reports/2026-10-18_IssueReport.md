# Issue Report: FallenStar – Spellcasters

- **Date:** 2026-10-18
- **Baseline:** commit `1f6d2f1` plus the uncommitted working tree (the `GameMaster` partial split and the managers moved to `MultiplayerScripts/`)
- **Method:** static reading of all `Assets/Scripts/**/*.cs`, plus YAML inspection of scenes, prefabs, network prefab lists, build settings and localization tables. **No compilation, Play Mode or build was run.**
- **Related docs:** [Architecture.md](../Architecture/Architecture.md), [GameLoop.md](../Architecture/GameLoop.md), [Dependencies.md](../Architecture/Dependencies.md)

This report only lists problems. Nothing has been fixed. The "Direction" column is a hint for a future task specification, not an implemented change.

## Severity levels

| Level | Meaning |
|---|---|
| 🔴 **Critical** | Breaks the build or the core loop, or lets a client cheat or control the match |
| 🟠 **High** | Freezes or corrupts a match under reachable conditions, or causes silent data loss |
| 🟡 **Medium** | Wrong behaviour, a broken flow, or a latent crash with low probability |
| 🟢 **Low** | Maintainability, performance, cosmetics, dead code |
| ⚪ **Info** | Naming or typos (reported only, per `AGENTS.md`) |

**Verdict:**
- **Confirmed** means the defect is visible directly in the code path.
- **Plausible** means the code strongly suggests the defect, but it needs an Editor or Play Mode check.

## Summary

| Level | Count |
|---|---|
| 🔴 Critical | 6 |
| 🟠 High | 9 |
| 🟡 Medium | 16 |
| 🟢 Low | 10 |
| ⚪ Info | 1 group |
| Unknown (verification only) | 5 |

---

## 🔴 Critical

| ID | Issue | Location | Evidence | Consequence | Verdict | Direction |
|---|---|---|---|---|---|---|
| C1 | Client controls HP | `PlayerControlPackage.s_hpDelta`; `GameMaster.Processing.cs:53` `HPDeltaProcessing`; `PlayerController.cs:184` (Space = −10) | The server adds any client-sent delta to `m_hpCurrent` | Any client can heal itself without limit or kill itself. The debug key ships in builds. | Confirmed | Remove the delta from the server path, or ignore it there. Gate the debug key behind `#if UNITY_EDITOR`. |
| C2 | Movement target is chosen by a client-supplied `ClientId` | `Processing.cs:36,45` (`_package.ClientId`); `PlayerController.cs:231` `InputServerRpc` | The server looks up `ConnectedClients[_package.ClientId]`, which comes from the payload, not from the RPC sender | A client can move the opponent's character, for example into lava, which costs an instant life. Combined with C1, it can also damage the opponent. | Confirmed | Use `ServerRpcParams.Receive.SenderClientId` |
| C3 | The server doesn't check the movement phase | `InputQueueUpdate` (`Processing.cs:7`) | `CanPlayerMove` is only called on the client (`PlayerController.SendMovementInput`) | Out-of-turn movement with a modified client | Confirmed | Call `CanPlayerMove(session)` on the server |
| C4 | Selection confirm writes unvalidated card IDs to the board | `GameMaster.PackageManaging.cs:193-235` (`HandleConfirmTurn`, `CardSelection` branch) | `HandleConfirmSelectionTurn` checks the owner only to calculate cost. The loop after it adds raw IDs to `m_boardPlayCards`/`m_boardDiscardCards`, even after an earlier confirm. | A client can play the opponent's hand, deck or graveyard cards, put one card in both lists, or resubmit after confirming | Confirmed | One validation pass (in sender's hand, not duplicated, not already confirmed), and build the board from that result |
| C5 | Suspected player-build compile failure | `CardPreviewController.cs:1` `using UnityEditor.Localization.Plugins.XLIFF.V20;`; `CardControllPackage.cs:1` `using NUnit.Framework;` | Editor-only and test-only namespaces in runtime `Assembly-CSharp` | A standalone build may not compile at all | Plausible (build not run) | Remove the unused `using` lines, then run a test build |
| C6 | Cast button component may not load | `UI/ConfirmSendButton.cs:3` declares `class ConfirmSendButtonUI1`; used by `UiPrefab/PlayerUIPrefab` | Unity requires the MonoBehaviour class name to match the file name | The "send/cast card" button may do nothing, so cards could never be played | Plausible (check the Inspector) | Inspect the prefab. Any rename needs instructions because of the `AGENTS.md` rules. |

---

## 🟠 High

| ID | Issue | Location | Evidence | Consequence | Verdict | Direction |
|---|---|---|---|---|---|---|
| H1 | Any exception kills the match loop | `GameMaster.GameLoop.cs:44` `GameLoop()` coroutine | No exception boundary. Unity stops a coroutine when it throws. | The match freezes permanently, with no recovery and no message | Confirmed | Guard every throw site (H2–H4, M9). Consider a guarded phase runner. |
| H2 | Empty deck throws | `GameMaster.Card.cs:151` `m_deck.ElementAt(0)` | No empty check. The graveyard is never recycled into the deck (~80 cards). | H1 in a long match | Confirmed | Check for an empty deck. The rule (reshuffle the graveyard, or deck-out loss) is a design decision. |
| H3 | Block heal dereferences a null character | `Processing.cs:209` uses `_targetCharacter.m_hpMax` in the block branch | `_targetCharacter` is null for a block target | NRE leads to H1 as soon as a heal card targets a block (none exists yet) | Confirmed (latent) | Use the block max HP |
| H4 | `CheckActivity` uses the owner before its null check | `Processing.cs:69` | `_ownerPlayer.m_currentOponentNetworkObjectId` is read before `if (_ownerPlayer == null)` | NRE leads to H1 | Confirmed (latent) | Reorder the check |
| H5 | No end-of-match flow | `GameLoop.cs:44-62`; `FindLostPlayer` (`GameLoop.cs:467`) | The loop ends with `Debug.LogError("You won/lost")` on the host only. `m_isLocalPlayerLost` is host-relative and isn't networked. No Result scene exists. | Players stay in `GameScene` forever with no result | Confirmed | `GamePhase.GameOver` (appended) plus a networked winner, a result UI with en/ru/ja keys, and a scene return |
| H6 | Opponent disconnect mid-match isn't handled | `MultiplayerManager.cs:230` ("Host continue"); `SessionManager.OnClientDisconnected`; `GameMaster.m_players` | The session is despawned but stays in `m_players`. NGO destroys the client-owned character and blocks. | Undefined end: a respawn wait, then the life is set to −1, then the loop ends silently (H5) | Confirmed (exact runtime path plausible) | A server-side forfeit, and remove the session from `m_players` |
| H7 | Disconnect cleanup despawns on clients and runs repeatedly | `GameMaster.ClearupAfterDisconnect` (`GameMaster.cs:301`) → `PlayerSession.ClearSession` (`PlayerSession.cs:619`); called from `ShutdownHostInternal`/`DisconnectClientInternal`, `HandleLostConnectionToHost` and `CleanupAfterDisconnect` | `ClearHand`, `ClearBlocks` and the character `Despawn` have no `IsServer` guard. Up to 3 calls per disconnect, some after `Shutdown()`. | Exceptions on clients cut cleanup short, which can leave stale state on the next match | Confirmed | Server guard and a single cleanup path |
| H8 | `CardData.OnValidate` wipes activities 1–3 | `InGame/CardData.cs:92` | It resets `m_activities[1..3]` to `None` on every validation | Silent data loss for designers. Multi-effect cards can't be authored. | Confirmed | Remove the reset, or only resize the array |
| H9 | `UltraRare` deck fill writes to an empty list | `GameMaster.Card.cs:262` `_nonUniqueList[i] = …` | Index 0 on a list that may be empty, where `.Add` is intended | `StartNewGame` throws as soon as any `UltraRare` card is in the database (none yet) | Confirmed (latent) | Use `.Add` |

---

## 🟡 Medium

| ID | Issue | Location | Consequence | Verdict |
|---|---|---|---|---|
| M1 | Shared `m_timer` is reset by the nested respawn wait | `GameLoop.cs:530` inside `PlayerActionPhase` (`:91`) | The active player's turn time changes after any death mid-turn | Confirmed |
| M2 | A lethal activity only `break`s the inner `for` loop | `GameLoop.cs:272` | The match idles up to 60 s (the turn time) after it is already decided | Confirmed |
| M3 | Lava immunity after network ID recycling | `LavaKillZone.cs:13,71` (`m_killedCharacterIds` never cleared); `NetworkManagerPrefab`: `RecycleNetworkIds: 1`, delay 120 | A respawned character with a reused ID ignores lava | Plausible |
| M4 | `IsHost` used where `IsServer` is meant | `GameMaster.Update` (`GameMaster.cs:115`), `HandlePlayerControlPackage` | All movement is dropped on a dedicated server | Confirmed |
| M5 | `InputQueueUpdate` NRE while the character is dead | `Processing.cs:47` `_client.PlayerObject.GetComponent` | `PlayerObject` is null between death and respawn. Error spam, lost packets. | Plausible |
| M6 | `UpdatePlayerHP` assumes a spawn point exists | `Processing.cs:328` `_session.GetSpawnPoint().IsOccupied` | NRE inside the coroutine leads to H1 if the spawn point is missing | Confirmed (low probability) |
| M7 | Target validation accepts null targets | `CheckActivity` `OpponentBlock`/`SelfBlock` (`Processing.cs:102-133`): a null block falls through to `true`. `OpponentPlayer`/`SelfPlayer`: `null == null` is true. | Invalid targets pass validation (no effect today, but fragile) | Confirmed |
| M8 | Pure-client NRE when targeting with a selected block | `PlayerSession.TryConfirmTarget` `AnyPlayer` → `m_currentSelectedBlock.GetSession()` | `BlockController.m_ownerSession` is set only on the server | Plausible (needs a non-host client) |
| M9 | `DrawCard(null)` | `ProcessActivity` DrawCard → `DrawCard(_targetSession)` | NRE leads to H1 for a draw card with a non-player target (none yet) | Confirmed (latent) |
| M10 | Async network setup errors aren't handled | `MultiplayerManager.StartHost` (`:501` catches only `LobbyServiceException`), `JoinLobbyByJoinCode` (`:735`, no catch), `HearthbeatLobby` (`:403`, `async void`, no null check or catch) | A host can run with no lobby; unobserved exceptions | Confirmed |
| M11 | Join by code is unusable | `MultiplayerManager.cs:28` `m_joinCodeInput` is never assigned, and there is no UI caller | The feature is dead | Confirmed |
| M12 | No language selection in game | `SettingsUI` isn't in any scene. `"SettingsScene"` (`SceneFlowManager.LoadSettingsScene`) doesn't exist and isn't in the build. | Players can't change locale. The RU/EN/JP requirement is only partly met. | Confirmed |
| M13 | Hard-coded user-visible strings | `MultiplayerManager.CreateLobbyPlayerData` (`"Host_"`/`"Client_"`), `"MyLobby"`, `LobbyListUI` `"Unknown Lobby"`, `LobbyPlayerItemUI` `"Unknown Player"`, `LobbyListItemUI` `"x / y"` | Violates the `AGENTS.md` localization rule | Confirmed |
| M14 | Client code dereferences `m_cardData` without a null check | `PlayerSession.CheckPackage`, `NetworkCard.CheckActiveTaergetSelection`, `CardViewUI.OnPointerClick` | NRE if used before `SendCardDataClientRpc` arrives | Plausible |
| M15 | Unbounded hand growth | `DrawCardPhase` (2 cards per player per round), `CardSelectionPhase` | Unselected cards stay in hand forever. There's no hand limit. | Confirmed (design question) |
| M16 | `SessionManager.Instance` is never cleared | `SessionManager.cs` (no `OnDestroy`) | A stale reference after leaving `GameScene`. `CleanupAfterDisconnect` uses it. | Plausible |

---

## 🟢 Low

| ID | Issue | Location |
|---|---|---|
| L1 | `m_timer` is written every frame (network traffic each tick) and shown as **elapsed**, not remaining | `GameLoop.cs` phases; `GameAnnouncerUI.UpdateAnnounceText` |
| L2 | `CreateDeck` `Random.Range(0, 2)` never reaches `case 2` | `GameMaster.Card.cs:175` |
| L3 | `IsGameFinished()` has a side effect (`FindLostPlayer` sets `m_externalDeathDetected`) | `GameLoop.cs:679` |
| L4 | Two team NetworkVariables on a block (`m_teamId`, `TeamId`) read by different code | `BlockController.cs` |
| L5 | `GameAnnouncerUI` phase switch **throws** on an unknown `GamePhase` | `GameAnnouncerUI.GetPhaseLocalizationKey` |
| L6 | Per-frame polling, `FindObjectsByType` and `LocalizedString` allocation | `PlayerUI`, `GameAnnouncerUI`, `CardWorldViewManager`, `LobbyControlUI`, `DisconnectMenuUI`, `SpawnPanelUI` |
| L7 | Card texts don't refresh on locale change (one-shot `GetLocalizedString` in `Setup`) | `CardViewUI.Setup`, `CardViewWorld.Setup` |
| L8 | `UI_table` key `"btn_lobbyRefresh\t"` has a trailing tab | `Assets/Localization/UI_table Shared Data.asset` |
| L9 | `GameMasterPrefab` ships with `m_debugMode: 1`. Timing and rule fields aren't serialized (C# defaults apply). | `Assets/Prefab/GameMasterPrefab.prefab` |
| L10 | Dead or legacy code | `ResolvePlayerCardsPhase`, `CardMessageQueueUpdate` (commented), `SendToClientRpc` echo, `PlayerController.ReceiveValidatedInput`, `TargetDataBackup`, `SpawnPanelUI`/`DisconnectMenuUI` (legacy scenes only), `DisconnectButtonUI`, `LocalizedTMPFormat`, `SettingsSceneButtonsUI`, the `26.06.19/` backup folder, the `Unity.VisualScripting` dependency for `ConvertTo<int>()` |

---

## ⚪ Info: naming and typos (report only, do not rename)

`CardControllPackage`, `PlayerCardfWorldArea.cs`, `SetOponent` / `m_currentOponentNetworkObjectId`, `CheckActiveTaergetSelection`, `SingInAnonymous` / `SingOut` / `SingOutAndClearCredentials` / `m_isSingingIn`, `HearthbeatLobby` (also used through `nameof`), `MovePlayeToSpawnPoint`, `ClearupAfterDisconnect`, `CardSpecialization.DeffenceSpell`, `KnowLobbies` / `m_knowLobbies`, `m_bootsTrap`, `_teamAssing`, `m_phaseAnouncementText`, `#region Valiable`, the log text `"Packege"`, the local variable `_locakPoint`.

---

## Feature gaps (not defects; listed so they aren't mistaken for bugs)

- No Result scene or game-over state (see H5)
- Steam login is a stub (`AuthManager.SignInSteam`)
- `GamePhase.DrawCards` and `RoundEnd` are never set, and `ResolveCards` is only transient
- `DiscardCard*` and `DrawCard*` combo actions, the `All*` and `Random*` targets, and `ActivityEffect` have no effect
- The "destroy all enemy blocks" win rule isn't implemented directly
- `SessionRole.Spectator` exists but nothing uses it

---

## Unknown: needs Editor, Play Mode or a build

| ID | Question | How to check |
|---|---|---|
| U1 | Does the uncommitted refactor compile in the Editor? | Open the project and check the Console |
| U2 | Nested NetworkObject: `SpawnPoint.prefab` inside `BlockBody` | Host a match and watch for NGO warnings |
| U3 | Does a null `NetworkBehaviourReference` work in NGO 2.11.2? (`m_activePlayer.Value = null`, `GameAnnouncerUI.GetTargetSession`) | Play Mode: watch the RoundBreak and CardSelection phases on the client |
| U4 | `DontDestroyWithOwner` on `PonyBase` and `BlockBody` | Inspect the prefabs |
| U5 | Unresolved network prefab GUID `e651dbb3fbac04af2b8f5abf007ddc23` | NetworkManager inspector, network prefab list |

## Suggested verification order

1. C5 and U1 (build and compile). Until these pass, nothing else can be tested.
2. C6 (cast button), so the core gameplay loop can be tested at all.
3. C1–C4 (cheating holes): small and isolated fixes.
4. H1–H4, H9, M6, M9 (guards that keep the coroutine alive).
5. H5–H7 (match end and disconnect lifecycle). This needs a design decision and localization keys.
