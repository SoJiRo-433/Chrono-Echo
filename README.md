# Chrono Echoes

Chrono Echoes is a top-down, single-player time-loop adventure built with Godot .NET and C#. Aionia uses a pendant to record a short stretch of movement and project it as an Echo. Echoes replay movement and interactions, letting the player solve puzzles that need actions in two places at once.

## Run the project

1. Install Godot .NET and its matching .NET SDK.
2. Open `project.godot` in Godot.
3. Build the C# project, then run the project. The main scene is `main_menu.tscn`.

The project registers `GameStateManager`, `DisplayManager`, and `PauseMenu` as autoloads in `project.godot`. The main menu offers Start, Load Game, New Game, CGs, and Quit. The pause menu provides Resume, Save Game, Load Game, and Quit to the main menu.

## Controls

| Input | Action |
| -> | -> |
| W, A, S, D or arrow keys | Move |
| E | Interact with nearby characters, objects, and puzzles |
| Space | Advance dialogue; jump when not in dialogue |
| 1 -> 9 | Select a dialogue choice |
| R | Start a recording; press again to project it as an Echo |
| J | Attack |
| Esc | Pause |
| F3 | Toggle the debug readout |
| F11 | Toggle fullscreen |

The Echo ability is unavailable until Aionia picks up the pendant in the tutorial. Recording ends automatically when the current room's capture limit is reached.

## Playable route

The normal route is:

`Room01  ->  RoomLibrary  ->  RoomAtrium  ->  Room04Medical  ->  Room05Observation  ->  Room06Business  ->  Room07Atrium  ->  Room08Facility  ->  Room09SectorAlpha  ->  Room10Rooftop  ->  EndingScreen`

The tutorial goes directly from `Room01` to the Library. `Room02.tscn` remains in the project as a separate legacy scene; it is not part of the normal route.

| Level | Scene | Main beats |
| -> | -> | -> |
| 1 | `Scenes/Rooms/Room01.tscn` | Tutorial, pendant, first Echo, security door |
| 2 | `Scenes/Rooms/RoomLibrary.tscn` | Archives, Yoruka, collapsed shelves, terminal, exit corridor |
| 3 | `Scenes/Rooms/RoomAtrium.tscn` | Timed hazards, skybridge, terminal, stairway, memory window |
| 4 | `Scenes/Rooms/Room04Medical.tscn` | Medical wing puzzles, Adellaine, diagnostic scan |
| 5 | `Scenes/Rooms/Room05Observation.tscn` | Glass timing, security footage, conduit route |
| 6 | `Scenes/Rooms/Room06Business.tscn` | Business lobby, audit archive, Hesiod, return route |
| 7 | `Scenes/Rooms/Room07Atrium.tscn` | Safe Atrium return, conversations, camera roll, Felix, service hatch |
| 8 | `Scenes/Rooms/Room08Facility.tscn` | Time research facility, resonance puzzle, trust check |
| 9 | `Scenes/Rooms/Room09SectorAlpha.tscn` | Chessboard, shared office, divergence terminal, prototype gears |
| 10 | `Scenes/Rooms/Room10Rooftop.tscn` | Last Echo, companions, final choice |

Each room sets its own level number, exit, and next scene. Levels 1 through 9 transition to the next room when their exit conditions are met. Level 10 leads to the ending screen.

## Core gameplay

### Echo recording

The player records movement and actions while a capture window is open. Pressing **R** again stops the recording and projects an Echo. The Echo replays the recorded positions and interactions. Rooms configure capture duration, Echo lifetime, and the number of active Echoes; some puzzles use repeating Echoes.

The room owns the shared loop clock used by recordings and timed hazards, keeping replays aligned with the original actions. Echoes can press switches, hold plates, and repeat recorded interactions.

### Movement and character sprites

The player uses the supplied front-facing sprite when idle and moving vertically. Horizontal movement displays one frame from the supplied walk strip and flips it to face the direction of travel. Named characters use supplied static, front-facing sprites.

The game uses rectangle-based room collision and a top-down jump state. A jump is recorded and replayed by Echoes. Attack is available, but the current game has no enemies to attack.

### Damage, death, and restart

The player starts with three lives. Each death clears the current level's numbered flags, its completion flag, and configured legacy flags such as `library_` and `medical_`. If lives remain, the player returns to the room start with restored health; with no lives remaining, the game enters game over. Other level flags and persistent story fragments are retained.

After game over, press **R** to restart the run. This restores lives, clears inventory and narrative flags, removes Echoes, starts a new room loop, and resets the room's puzzle state. The pendant is restored with the flag reset only in the tutorial room, where it can be picked up again.

Some hazards cause a scratch or local setback: they may damage the player and move them to a checkpoint without consuming a life.

## Saving and loading

There are three save slots. A save stores the current scene, the player's world position, narrative flags, and affection values. Loading changes to the saved scene and restores the saved position and story state.

A save does not snapshot active Echoes, loop clock, room-local puzzle state, inventory, or remaining lives. Those are initialized by the loaded room and player.

## Story state, dialogue, and gallery

Dialogue and choice data are built in `Scripts/Dialogue/DialogueManager.cs`. Choices can set flags, require or hide behind flags, and change affection. Affection is tracked for Yoruka, Adellaine, Hesiod, and Felix.

The CG gallery is available from the main menu. Memory fragments and gallery entries unlock from narrative flags. The ending screen displays the ending selected by the final rooftop choice.

## Project structure

| Area | Location |
| -> | -> |
| Player and input | `Scripts/Player/` |
| Shared entities and game state | `Scripts/Core/` |
| Rooms and level-specific logic | `Scripts/Rooms/` |
| Puzzle objects | `Scripts/Puzzles/` |
| Dialogue and story content | `Scripts/Dialogue/` |
| Menus and saves | `Scripts/Menu/` |
| HUD and ending presentation | `Scripts/UI/` |
| Levels, player, Echo, and ending scenes | `Scenes/` |
| Maps, sprites, portraits, and UI art | `Assets/` |

The base `Room` handles the player, HUD, camera, doors, switches, Echoes, exits, and shared loop timing. Room subclasses add their level's map, interactions, and puzzle rules. `GameStateManager` holds narrative flags, affection, current room/player references, and the save/load handoff.

## Current implementation notes

- The game is playable through Level 10, including a final choice and ending screen.
- The normal tutorial route skips the standalone `Room02.tscn` bridge scene and enters the Library directly.
- Many level maps and room interactions are assembled in C# rather than as prebuilt objects in scene files.
- The ending presentation is a separate scene; story outcomes are selected from narrative flags and rooftop choices.


