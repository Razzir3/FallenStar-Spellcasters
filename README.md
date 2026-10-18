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
Login -> MainMenu -> Lobby -> Game -> Result
## Gameplay
- Player starts on block - based platform. Target - Defeat enemy player character 3 times or destroy all enemy blocks.
- If player was standing on destroyed block - he will fall in lava(instant lose 1 life).
- Players can move on their side platform and play/discard cards from hand at Player Action phase.
- Players turn order builds by summary of mana cost of playing/discarding card by descending
## Game Flow
Time break -> Drawing Cards -> Selecting card to play/discard -> Rotate position on your own blocks, play cards to destroy blocks of enemy side and deal damage to enemy player -> Break ->Repeat 
## Documentation
- Don't done yet
## Current Status
Half - Dead