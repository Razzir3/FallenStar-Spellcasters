# FallenStar - Spellcasters
## Overview
3D multiplayer card-base game developed in Unity
## Technology
- Unity: 6.3.13f
- Rendering: URP
- Networking: Unity Relay + Lobby + Authentication
- Localization: Unity Localization

## Project Structure
- Assets/Scripts/ - scripts folders root
- Assets/Scenes/ - game scenes
- Assets/Card/  - cards data roots
- Assets/Fonts/ - in game fonts
- Assets/Localization/ - localization folder
- Assets/Logo/ - logos
- Assets/Materials/ - materials for prefabs
- Assets/Models/ - models for prefabs
- Assets/ParrelSync - utility for multiple windows run and debug
- Assets/Prefab - prefabs
## Scene Flow
Login -> MainMenu (-> Settings) -> Lobby -> Game -> Result -> MainMenu
## Gameplay
- Player starts on block - based platform with 3 lives. Target - make the enemy lose: defeat enemy player character 3 times, or defeat it when the enemy has no blocks left to respawn on. Destroying all enemy blocks does not win by itself: it counts at the enemy's next death.
- A player who presses Leave match or disconnects during a match loses.
- If player was standing on destroyed block - he will fall in lava(instant lose 1 life).
- Players can move on their side platform and play/discard cards from hand at Player Action phase.
- Players turn order builds by summary of mana cost of playing/discarding card by descending
## Game Flow
Time break -> Drawing Cards -> Selecting card to play/discard -> Rotate position on your own blocks, play cards to destroy blocks of enemy side and deal damage to enemy player -> Break ->Repeat 
## Testing with two players
- ParrelSync: open the clone (`Assets/ParrelSync`) as the second Editor. The clone shares `Assets`, so it always runs the same code.
- Both Editors write to the same `%LOCALAPPDATA%\Unity\Editor\Editor.log` and overwrite each other's lines. For a clean log per Editor, start the clone from the command line with its own log file: `Unity.exe -projectPath <clone folder> -logFile <clone>.log`.
- A player build writes `%USERPROFILE%\AppData\LocalLow\DefaultCompany\FallenStar-Spellcasters\Player.log` (the previous run is in `Player-prev.log`).
- Both peers must run the same build: host and client must have the same network code.
## Documentation
- Architecture: `Docs/AI/Architecture/` (`Architecture.md`, `GameLoop.md`, `Dependencies.md`)
- Tasks, reports and reviews: `Docs/AI/Tasks/`, `Docs/AI/Reports/`, `Docs/AI/Reviews/`
## Current Status
Half - Dead