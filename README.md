# Chrono Echoes - Back End (Godot 4 .NET / C#)

Compiles clean (`dotnet build`, 0 warnings, 0 errors).
## Story (one-minute version)
**Aionia Avgerinos** is a brilliant, nonchalant student of chronology at the **Aetherion Institute**.
With her partner **Felix Lasarewitch** she leads **Project Hyperion**, a machine built to traverse
time and space - until she finds that every timeline in existence is spiralling toward collapse.
Her answer is to erase herself from all of them; Felix's answer is **Project Theia**, built to bind
her to reality no matter the cost. Then a sabotage tears the institute apart and fractures time
itself. Aionia wakes with her memory wiped, in a ruined room, with a **pendant** on the floor: a
temporal anchor disguised as a remote controller for the Hyperion core. That pendant is what lets
her unpin a few seconds of her own past and project them as an **Echo** - which is the whole game
mechanic. To learn what happened she must collect **memory fragments** hidden across the campus by
Felix, and finally choose: erase herself and save the universe, or stay in this tangled reality with
the people who refuse to let her go.

This demo covers the intro/tutorial beats only (waking up, the report log, the pendant, the first
Echo, the pressure-switch puzzle, leaving the room). The wider cast (Yoruka, Adellaine, Hesiod),
the Law of Temporal Displacement and the ending branches are not implemented yet.

| Cast in the demo | Role |
|---|---|
| **Aionia Avgerinos** | Protagonist. Chronology student, amnesiac after the Hyperion disaster. |
| **Felix Lasarewitch** | Her partner on Project Hyperion; he appears in the pendant's memory flash. |
| **Pendant** | Her key item and the source of the Echo ability; it also speaks to her. |

Every line of narrative text lives in one place: `DialogueManager.BuildDemoContent()`.
The full story bible - backstory, the Law of Temporal Displacement, the cast, the tutorial beats and
the ending branches - is in [DESIGN.md](DESIGN.md).

## 1. Setup (5 minutes)
1. Install Godot 4 **.NET** edition and the .NET SDK (6 for Godot 4.3, 8 for 4.4+).
2. Create/open the project folder. If the supplied `project.godot` / `ChronoEchoes.csproj`
   do not match your Godot version, let Godot generate its own (Project > Tools > C# >
   Create C# solution), then copy in `Scripts/` and `Scenes/`.
3. Make sure the autoload exists (exact snippet for `project.godot`):

```ini
[autoload]

GameStateManager="*res://Scripts/Core/GameStateManager.cs"
DisplayManager="*res://Scripts/Core/DisplayManager.cs"
PauseMenu="*res://Scripts/Menu/PauseMenu.cs"

[dotnet]

project/assembly_name="ChronoEchoes"
```
   (Or: Project Settings > Globals > Autoload > add `Scripts/Core/GameStateManager.cs`,
   name `GameStateManager`, Enable.)
4. Build once (hammer icon, top right), then press F5. Main scene: `main_menu.tscn`. The menu provides Start/Continue, New Game, Load Game, and Quit.

## 2. Scene setup (already provided as .tscn; to rebuild by hand)
Gameplay visuals are drawn in code (`_Draw`); the main menu uses the supplied video, title image, and font assets.
- **Echo.tscn**: root `Node2D`, script `Echo.cs`.
- **Player.tscn**: root `Node2D`, script `Player.cs`; set `Echo Scene` = `Echo.tscn`.
- **Enemy.tscn**: root `Node2D`, script `Enemy.cs` (Max Health 2, Move Speed 70, Invincibility Duration 0.3).
- **Room01.tscn**: the tutorial room, laid out to match the OOP blueprint. Root `Node2D` named
  Room01 at (0,0), script `Room.cs`, with children: `DialogueManager` (Node, `DialogueManager.cs`);
  `DividerUpper` (1008,0) size (32,156) and `DividerLower` (1008,231) size (32,417), both
  `Obstacle.cs`, leaving a gap that `SecurityDoor` (1008,156) size (32,75) fills - it is the only
  way through to the exit zone at (1054,260) size (79,110); `PressurePlate` (978,554), `Switch.cs`
  with Is Pressure Plate ON and Linked Door Path = `../SecurityDoor`; five `Inspectable.cs` nodes
  the player examines with E - `ReportLog` (242,52) size (227,95), `Drawers` (273,168) size (182,64),
  `TimeMachine` (546,40) size (235,106), `Cabinet` (553,469) size (182,81) and `BrokenMachinery`
  (197,474) size (190,58); the `Pendant` (614,287), `Pendant.cs`; `FloorMarker.cs` nodes that label
  the zones and debris (`SpawnMarker`, `GlassMarker`, `RubbleMarker`, `PanelMarker`, `DebrisMarker`,
  `AnomalyMarker`, `EchoZoneMarker`, and `PhotoMarker`, which only appears once `saw_burnt_photo` is
  set); an `Enemy` instance at (700,545); a `Player` instance (last child, so it draws on top) at
  the wake-up spot (118,306).
- **Room02.tscn**: the second level. Same shape as Room01 (a node + `Room.cs` + children), but
  with **two** pressure plates and **two** doors in series: `DividerUpper` (420,0) and
  `DividerLower` (420,408) leave a gap that `DoorA` (420,240) size (32,168) fills;
  `Divider2Upper` (800,0) and `Divider2Lower` (800,408) leave a gap that `DoorB` (800,240)
  size (32,168) fills; `PlateA` (200,520) opens DoorA, `PlateB` (620,120) opens DoorB; an
  `Enemy` instance at (620,520). It sets `LevelNumber` = 2 and leaves `NextLevelPath` empty,
  which is what makes it the last level.

## 3. The demo loop (what happens in Room01)
Controls: **WASD/Arrows** move, **E** interact, **J** attack, **T** record/stop a capture window, **R** project the Echo, **Enter/Space** dialogue, **1/2** choices, **F3** hide the debug HUD, **F11** toggle full screen, **Esc** open the pause menu.
1. Aionia wakes at the west side of the room, inside the blue "START / WAKE-UP" marker. The short
   intro plays (game paused), then gameplay starts.
2. Walk to the **report log** and press **E**. Turn the pages with **1**; the final page reveals the
   burnt photo, which sets `saw_burnt_photo` and makes the photo marker appear beside the log.
3. Examine the rest of the scenery if you like - the destroyed time machine, the drawers, the cabinet
   and the pile of broken machinery each have their own line and their own flag.
4. Walk to the glowing **pendant** in the middle of the room and press **E**. It goes into her
   inventory, sets `touched_pendant` (which unlocks Fragment I) and plays the memory flash with Felix.
5. Capture starts OFF. Press **T** where you want the Echo to start recording, walk south-east to
   the red pressure switch and stand on it, then press **T** again to close the window.
6. Press **R**. An **Echo** (cyan ghost) is projected **in place**: it appears at the first position
   you recorded and replays the window forward from this moment - it walks back to the switch and
   stays there. Nothing else changes: no clock rewind, no puzzle or enemy reset, and you are not moved.
7. The switch turns green under the Echo's weight, so the **security door** in the east divider opens.
   Walk through it while your past self holds the plate.
8. Fight the enemy with **J** if needed, then step into the green exit zone -> **ROOM CLEAR**,
   closing dialogue, memory fragment II.
9. To test the Interact path instead: set `Is Pressure Plate` OFF on the switch, press **E** next to
   it while the window is open, then **R**; the Echo repeats the Interact and latches the lever.
10. Dying works the same way: it costs a life, projects whatever window you had as an Echo, and wakes
    you back at the start with full health. It no longer resets the room.
11. **Level 2 (Room02).** Reaching the exit sets `room01_complete`, the closing dialogue plays, and
    the game loads `Room02.tscn`. Your lives carry over, and the flags/memory fragments you earned
    are kept. Room02 has two plates and two doors in a row, so one Echo is no longer enough:
   - Stand on `PlateA`, record a short window with **T**...**T**, then project it with **R**. The Echo holds plate A, so `DoorA` opens.
   - Walk through `DoorA` to `PlateB`, record a short window on it and project it with **R**. A second Echo now holds plate B, so `DoorB` opens too.
   - With both Echoes holding their plates, walk through to the exit.
   Room02 has no `NextLevelPath`, so finishing it shows "ALL LEVELS CLEAR" and stays put.

## 4. Summary - how it fits together
**OOP map (for your defense)**
| Concept | Where |
|---|---|
| Inheritance | `Entity` -> `Player`, `Enemy`, `Echo` |
| Polymorphism | `Update`, `TakeDamage`, `Die`, `Attack`, `Interact`, `IsHostileTo` overridden per subclass; `Entity.Interact` calls `IInteractable.Interact` on a Switch or Door without knowing which |
| Interfaces | `IDamageable`, `IInteractable` (Switch, Door), `IUpdatable` (Entity, Switch) |
| Encapsulation | Private fields only; `[Export]` properties clamp values (e.g. `MaxHealth >= 1`); Echo's frames and the recorder are private |
| Composition | `Room` HAS-A lists of entities/obstacles/switches/doors/echoes; `Player` HAS-A `PlayerInput` + `EchoRecorder`; `Enemy` HAS-A `EnemyAI` |
| Singleton | `GameStateManager.Instance` (narrative/player state) and `DisplayManager.Instance` (window, e.g. F11 full screen), both registered as Autoloads |
| Signals | `HealthChanged`, `Died`, `PlayerDied`, `ActivationChanged`, `DoorStateChanged`, `LoopStarted`, `FlagChanged`, dialogue signals... |
| Level progression | `Room.NextLevelPath` (per-room, set in the Inspector) + `GameStateManager.GoToNextLevel()` |

**Echo system.** `PlayerInput` produces one `PlayerAction` per physics tick. `Player` executes
it and passes it to `EchoRecorder.Tick`, which stores an `ActionFrame` only when the action
changes (or on Interact/Attack) - tens of frames instead of ~1,800 for a 30 s loop.
`Player.ProjectEcho()` closes the capture window, creates an `Echo` from the frames, and hands it
to `Room.AddEcho()`, which calls `Echo.StartReplay()`. That anchors the replay to the *current*
clock value, so the Echo is projected **in place**: it appears at the window's first recorded
position and replays forward from this moment. Nothing about the run is reset - the clock keeps
running, puzzles and enemies keep their state, and the player is not moved. The Room owns a
single `LoopClock` (fixed 1/60 s steps) that both the recording and all replays read, so they
stay in sync. The Echo snaps to each frame's position, re-performs only Interact/Attack, and
interpolates in between. No randomness or live input is involved, so it is deterministic.
A finished Echo stays where it ended, so it can hold a pressure plate.

**Capture windows & projection.** Capture does not start by itself. The player presses **T** to
open a window at their current position and **T** again to close it, so the Echo replays exactly
the stretch they chose instead of a whole loop. A second press after closing starts a *new* window
and discards the old frames. Because a window can open after the room began, its first frame
carries a non-zero timestamp; when the window is projected the Echo *rebases* it, so frame 0 fires
the instant **R** is pressed and the rest keep their original spacing - `Echo.Update` expresses the
shared clock in the recording's own timeline (`LoopClock - _replayStartClock + frames[0].Timestamp`).
Projecting closes an open window and *consumes* it, so pressing **R** twice never spawns duplicates
(closing a window that is already frozen still returns the same frames without a second Idle frame).

An Echo is placed at its first frame's position the moment it is projected, so `Switch` counts it
right away: recording while standing on a plate (record, walk off, press **R**) is a legitimate
and intended solution. The puzzle is about *when* you capture, not about replaying a full loop.

The old design reset the whole run to place an Echo - clock to 0, puzzles and enemies reset, the
player teleported back to the start. This rework removed that: an Echo is projected where it was
recorded and the run simply keeps going. The only thing that still resets the world is the
game-over restart (`Player.RestartRun`, which calls `Room.StartNewLoop`).

**Level progression.** Each Room declares its own `LevelNumber` and `NextLevelPath` in the
Inspector, so a level describes itself instead of being listed in one central place. When the
player reaches the exit, `Room.CompleteRoom()` sets the completion flag and starts the closing
dialogue; the hand-over happens on the `DialogueEnded` signal, so the scene change never cuts the
text short. `GameStateManager` is an Autoload, so it survives `ChangeSceneToFile`: flags, memory
fragments and `CarriedLives` all persist into the next level. `Player._Ready()` picks the lives
back up from `CarriedLives`, which is why finishing a level is not a free refill. A room with an
empty `NextLevelPath` is the last one - it stays completed instead of changing scene.

**Design notes / honest limitations**
- Collision is a rectangle check in `Room.IsBlocked` instead of Godot physics (simpler to explain).
- The Echo "frame-based replay" feeds `PlayerAction`s through the same code path as the player;
  it does not literally call `Input.ParseInputEvent`, because that would also drive the live player.
- Combat outcomes in a replay depend on where enemies are in that loop, so an Echo's attack may hit or miss differently.
- Placeholder UI (HUD, dialogue box) is built in code so it works with zero assets; swap for real UI by listening to the dialogue signals.
