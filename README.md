# Chrono Echoes - Back End (Godot 4 .NET / C#)

Compiles clean (`dotnet build`, 0 warnings, 0 errors), but it has not been run inside the Godot
editor yet. If you hit a runtime error, paste it back and it can be fixed quickly.

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

[dotnet]

project/assembly_name="ChronoEchoes"
```
   (Or: Project Settings > Globals > Autoload > add `Scripts/Core/GameStateManager.cs`,
   name `GameStateManager`, Enable.)
4. Build once (hammer icon, top right), then press F5. Main scene: `Scenes/Rooms/Room01.tscn`.

## 2. Scene setup (already provided as .tscn; to rebuild by hand)
All visuals are drawn in code (`_Draw`), so every scene is just a node with a script.
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
Controls: **WASD/Arrows** move, **E** interact, **J** attack, **T** capture, **R** reset loop, **Enter/Space** dialogue, **1/2** choices, **F3** hide the debug HUD.
1. Aionia wakes at the west side of the room, inside the blue "START / WAKE-UP" marker. The short
   intro plays (game paused), then gameplay starts.
2. Walk to the **report log** and press **E**. Turn the pages with **1**; the final page reveals the
   burnt photo, which sets `saw_burnt_photo` and makes the photo marker appear beside the log.
3. Examine the rest of the scenery if you like - the destroyed time machine, the drawers, the cabinet
   and the pile of broken machinery each have their own line and their own flag.
4. Walk to the glowing **pendant** in the middle of the room and press **E**. It goes into her
   inventory, sets `touched_pendant` (which unlocks Fragment I) and plays the memory flash with Felix.
5. **Loop 1:** capture is OFF. Press **T** where you want the Echo to start, walk south-east to the
   red pressure switch and stand on it, then press **T** again to close the window.
6. Press **R** (or let the enemy kill you; that costs a life). An **Echo** (cyan ghost) is spawned from the window you captured.
7. **Loop 2:** the world resets. The Echo replays your captured window and stays on the switch. The switch turns green, the **security door** in the east divider opens.
8. Walk through the open door (fight the enemy with J if needed) into the green exit zone -> **ROOM CLEAR**, closing dialogue, memory fragment II.
9. To test the Interact path instead: set `Is Pressure Plate` OFF on the switch, press **E** next to it in loop 1, then **R**; the Echo repeats the Interact.
10. **Level 2 (Room02).** Reaching the exit sets `room01_complete`, the closing dialogue plays, and
   the game loads `Room02.tscn`. Your lives carry over, and the flags/memory fragments you earned
   are kept. Room02 has two plates and two doors in a row, so one Echo is no longer enough:
   - Loop 1: walk onto `PlateA`, press **R**. The Echo now holds plate A, so `DoorA` stays open.
   - Loop 2: walk through `DoorA` to `PlateB`, stand on it, press **R**. A second Echo now holds plate B.
   - Loop 3: Echo 1 holds A and Echo 2 holds B, so both doors are open - walk through to the exit.
   Room02 has no `NextLevelPath`, so finishing it shows "ALL LEVELS CLEAR" and stays put.

## 4. Testing checklist (QA)
**Recording / Echo**
- [ ] Capture is OFF when a loop starts; the HUD reads `Capture off - press T`; Loop #1.
- [ ] T opens a window (`Capture ON`); T again freezes it and the HUD shows the frame count.
- [ ] A loop where T was never pressed spawns no Echo when you press R.
- [ ] After R: Loop # increments, Echoes 1/3, player returns to start, enemy and puzzles reset.
- [ ] The Echo replays the captured window at the same speed as the recorded run, then stands still at the final position.
- [ ] An Echo captured mid-loop waits at its anchor position until the window's first timestamp arrives, then replays (no index error).
- [ ] Capturing only the final second while standing on the plate gives an Echo that is already there when the loop restarts.
- [ ] Pressing E near the lever (plate mode OFF) inside a window -> the Echo activates it at the same moment in loop 2.
- [ ] Do 4 resets: only 3 echoes exist (oldest removed). Two echoes replay in sync.
- [ ] Repeat the identical run twice: Echoes behave identically (deterministic).
**Puzzles**
- [ ] Door is solid when closed (player cannot pass), passable when open.
- [ ] Plate is green only while Player or Echo stands on it; door closes when nobody does.
- [ ] Obstacles and room edges block movement; entities slide along walls.
- [ ] Each loop reset closes the door and deactivates the switch.
**Combat / health**
- [ ] Enemy colour: dark red patrol -> orange chase -> bright red attack.
- [ ] Enemy hit by J (needs 2 hits, ~0.3 s apart); it disappears when dead and returns next loop.
- [ ] Player loses 1 HP per enemy hit, blinks during i-frames, cannot be hit again during them.
- [ ] Dying costs 1 life, spawns an Echo of the failed run, restores HP and position.
- [ ] Lives 0 -> "GAME OVER"; R restarts with 3 lives, no echoes, flags cleared.
- [ ] Echo cannot be damaged by the enemy.
**Levels**
- [ ] Reaching the exit in Room01 plays the closing dialogue, and only then loads Room02.
- [ ] Lives carry over: finish Room01 with 1 life left -> Room02 starts with 1 life.
- [ ] Flags and memory fragments survive the level change (the HUD's Flags line still names `touched_pendant`).
- [ ] Room02 has no next level: completing it shows "ALL LEVELS CLEAR" and does not change scene.
**Narrative / interactables**
- [ ] Gameplay is frozen during dialogue and resumes after.
- [ ] The report log opens with **E**, the pages turn with **1**, and the final page sets `saw_burnt_photo` (HUD flags) and makes the photo marker appear.
- [ ] The time machine, drawers, cabinet and broken machinery each play their own line and set their own flag the first time.
- [ ] Re-examining the report log plays the short "already read" line instead of the pages.
- [ ] Picking up the pendant adds `pendant` to her inventory, sets `touched_pendant`, unlocks Fragment I, and removes the pendant from the floor.
- [ ] Reaching the exit sets `room01_complete`, plays closing dialogue once, unlocks Fragment II.
- [ ] "Ask the pendant who you are" appears only if `touched_pendant` is set.
**Architecture**
- [ ] Output panel shows no errors; only one GameStateManager exists (Remote tree).

## 5. Summary - how it fits together
**OOP map (for your defense)**
| Concept | Where |
|---|---|
| Inheritance | `Entity` -> `Player`, `Enemy`, `Echo` |
| Polymorphism | `Update`, `TakeDamage`, `Die`, `Attack`, `Interact`, `IsHostileTo` overridden per subclass; `Entity.Interact` calls `IInteractable.Interact` on a Switch or Door without knowing which |
| Interfaces | `IDamageable`, `IInteractable` (Switch, Door), `IUpdatable` (Entity, Switch) |
| Encapsulation | Private fields only; `[Export]` properties clamp values (e.g. `MaxHealth >= 1`); Echo's frames and the recorder are private |
| Composition | `Room` HAS-A lists of entities/obstacles/switches/doors/echoes; `Player` HAS-A `PlayerInput` + `EchoRecorder`; `Enemy` HAS-A `EnemyAI` |
| Singleton | `GameStateManager.Instance`, registered as Autoload |
| Signals | `HealthChanged`, `Died`, `PlayerDied`, `ActivationChanged`, `DoorStateChanged`, `LoopStarted`, `FlagChanged`, dialogue signals... |
| Level progression | `Room.NextLevelPath` (per-room, set in the Inspector) + `GameStateManager.GoToNextLevel()` |

**Echo system.** `PlayerInput` produces one `PlayerAction` per physics tick. `Player` executes
it and passes it to `EchoRecorder.Tick`, which stores an `ActionFrame` only when the action
changes (or on Interact/Attack) - tens of frames instead of ~1,800 for a 30 s loop.
`Player.ResetTimeLoop(room)` closes the capture window, creates an `Echo` from the frames, and
calls `Room.StartNewLoop()`, which resets puzzles/enemies and rewinds every Echo. The Room owns a
single `LoopClock` (fixed 1/60 s steps) that both the recording and all replays read, so they
stay in sync. The Echo snaps to each frame's position, re-performs only Interact/Attack, and
interpolates in between. No randomness or live input is involved, so it is deterministic.
A finished Echo stays where it ended, so it can hold a pressure plate.

**Capture windows.** Capture does not start by itself. The player presses **T** to open a window
at their current position, and **T** again to close it, so the Echo replays exactly the stretch
they chose instead of the whole loop. A second press after closing starts a *new* window and
discards the old frames. Because a window can open after the loop began, its first frame carries a
non-zero timestamp: until that timestamp arrives the Echo waits at its anchor position, since it
has no earlier frame to glide from - `Echo.Update` has an explicit branch for that case. Ending
the loop with **R** closes an open window; closing one that is already frozen returns the same
frames without appending a second Idle frame.

An Echo holds whatever it is standing on from the moment the loop starts, even before its own
window begins - it is already at its anchor position, so `Switch` counts it. That is deliberate,
not an oversight: opening a window while standing on a plate is a legitimate solution, and the
puzzle is meant to be about *when* you capture rather than about walking the whole route first.
Blocking it would mean teaching `Switch` to ask whether an Echo's window has started yet.

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
