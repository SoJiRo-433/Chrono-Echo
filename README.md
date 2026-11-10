# Chrono Echoes - Back End (Godot 4 .NET / C#)

<<<<<<< Updated upstream
Compiles clean (`dotnet build`, 0 warnings, 0 errors), but it has not been run inside the Godot
editor yet. If you hit a runtime error, paste it back and it can be fixed quickly.
=======
Compiles clean (`dotnet build`, 0 warnings, 0 errors).

The game now runs Levels 1-6: the ruined room (Level 1), the hallway and Archives (Level 2), the
Atrium (Level 3), Medical Facility (Level 4), Observation Wing (Level 5), and Business Department
(Level 6). Levels 7-10 are listed under
[Not implemented yet](#6-not-implemented-yet).
>>>>>>> Stashed changes

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
<<<<<<< Updated upstream
| **Felix Lasarewitch** | Her partner on Project Hyperion; he appears in the pendant's memory flash. |
| **Pendant** | Her key item and the source of the Echo ability; it also speaks to her. |
=======
| **Mochizuki Yoruka** | Junior assistant, blunt and explosive; appears through Level 6. |
| **Felix Lasarewitch** | Her partner on Project Hyperion; he appears in the pendant's memory flash. |
| **Adellaine Moriarty**, **Hesiod Moriarty** | Medical student and business student; join in Levels 4 and 6. |
| **Pendant** | Her key item and the source of the Echo ability. |
>>>>>>> Stashed changes

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

<<<<<<< Updated upstream
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
=======
| # | Scene | Design doc | Script |
|---|---|---|---|
| 1 | `Scenes/Rooms/Room01.tscn` | Level 1, scenes 1-8 (tutorial) | `Room.cs` |
| 2 | `Scenes/Rooms/Room02.tscn` | Level 2, scene 1 (Crossing the Hallway) | `Room.cs` |
| 2 | `Scenes/Rooms/RoomLibrary.tscn` | Level 2, scenes 2-5 (Archives -> Collapsed Shelves -> Terminal -> Root-webbed Corridor) | `RoomLibrary.cs` extends `Room` |
| 3 | `Scenes/Rooms/RoomAtrium.tscn` | Level 3, scenes 1-5 (Entry Hall -> Glass Skybridge -> Glitched Terminal -> Broken Stairway -> Observation Deck) | `RoomAtrium.cs` extends `Room` |
| 4 | `Scenes/Rooms/Room04Medical.tscn` | Level 4, scenes 1-5 (Heavy Door -> Triage -> Diagnostic Terminal -> Business Hall) | `RoomMedical.cs` extends `Room` |
| 5 | `Scenes/Rooms/Room05Observation.tscn` | Level 5, scenes 1-4 (Glass Lock -> Observation Pane -> Security -> Conduit) | `RoomObservation.cs` extends `Room` |
| 6 | `Scenes/Rooms/Room06Business.tscn` | Level 6, scenes 1-5 (Lobby -> Office -> Audit Archive -> Overlook -> Atrium return) | `RoomBusiness.cs` extends `Room` |

Levels 2 and 3 are laid out from the two level blueprints: their scene names, their puzzle rules and
their tuning values (record window 5 s, gate timer 5 s, bridge panels 2.5 s on a 4 s cycle,
time-stain 3 s, jump airtime 0.6 s) follow those sheets, with the small skybridge easing adjustment
noted below; the Atrium's four-symbol console codes
and the stairway's missing step come off the Level 3 sheet's scenes 3 and 4.

`Room01 -> Room02 -> RoomLibrary -> RoomAtrium -> Room04Medical -> Room05Observation -> Room06Business`.
Each room declares its `LevelNumber` and `NextLevelPath`; Level 6 leaves the path empty and shows
**LEVEL 6 CLEAR - to be continued**.

### Room01 - the ruined room (Level 1)
Waking up, the report log and the burnt photo, the scenery, the **pendant**, the first Echo, and the
pressure-switch puzzle behind the security door.
1. Aionia wakes at the west side inside the blue "START / WAKE-UP" marker; the intro plays.
2. Examine the report log with **E** (turn the pages with **1**) - the last page sets
   `saw_burnt_photo` and makes the photo marker appear.
3. Pick up the glowing **pendant**. That sets `touched_pendant`, unlocks Fragment I, plays the
   memory flash with Felix - **and is what turns the Echo ability on**.
4. When the flash ends, a **scripted demo Echo** is projected in front of her: an ordinary `Echo`
   built from ordinary `ActionFrame`s that repeats the "touches its head" beat from the narration.
5. Approaching the security door (or stepping on its plate) plays the scene-7 line
   *"I can use the pendant to get out of here."*
6. The door is the Room01 puzzle: record a window that ends standing on the plate, project it, and
   walk through while the Echo holds the weight.
7. Reaching the exit sets `room01_complete` (Fragment II) and loads `Room02.tscn`.

### Room02 - the bridgeway (Level 2, scene 1)
Two pressure plates and **one** door, and that door wants both plates weighed down at the same
moment (`Door.NeedsAllLinkedSwitches`). Aionia can only stand on one of them, so an Echo is the
only way to open it - and because she still has to walk through afterwards, the door latches open
(`Door.LatchOpen`) instead of demanding she stay on her plate. `room02_intro` sets the objective
("use your Echo to find a way to open the door"); the closing `room02_clear` is the bridge into the
Archives.

Per the blueprint both plates sit **side by side in one strip** at the top-left, walled above and
below and left open at its right end, with `START: TUTORIAL EXIT` at the strip's left and the
divider door beyond it - she walks in, works the two plates, then drops out of the strip's open end
to the door. The `HOLD BOTH PLATES AT ONCE` marker sits over the pair, and a dotted blue ring
(`FloorMarker.DottedRing`) marks PlateA as the spot the Echo is meant to hold: the blueprints' "Echo
stand spot" mark, which Level 3's deck uses for the same purpose.

### RoomLibrary - the Archives to the heavy doors (Level 2, scenes 2-5)
1. **The Archives.** Walking in pans the camera off Aionia and onto **Yoruka** - the doc's Scene 2
   beat - and only then does she think about approaching her, which is where the "Approach the
   student" objective comes from. **Yoruka** is an interactable among the shelves; **E** starts `library_yoruka`,
   and when it ends she runs off - the node hides, and a hidden interactable is skipped, so she
   stops being examinable in the same moment. The cork board, computer, bookshelves and drawers each
   have their own line. **She also leaves something behind**: the moment she runs, a hidden
   `Inspectable` appears on the exact spot she stood on (`RoomLibrary.DropYorukaItem`), so the
   blueprint's "leaving behind an item" is a thing you can walk over and pick up. It plays the line
   `library_item_taken` and sets a flag of the same name, and until she runs it is hidden - a hidden
   interactable is skipped, so it does not exist for the player either.
2. **Collapsed Shelves.** The fallen shelves are the block between the collapsed-shelves room and
   the Terminal: a `Door` filling the divider (`CollapsedShelvesDoor`, at x = 650). Inside the room
   the **safety latch** has to be held by an Echo while the player slips through, exactly like
   Room01's door, with the collapse narrated by `library_collapsed_shelves`. The latch is flanked by
   **two rubble piles** - rubble on both sides of it, as the blueprint draws - with the gap between
   them wide enough to stand in and the route past them left open, and a dotted blue ring marks it
   as the Echo's stand spot. The latch is on the *near* side of the block on purpose: the Echo has
   to reach it before the door opens, not after.
3. **Terminal.** Past the shelves Yoruka is waiting (`library_yoruka_followup`), and the surviving
   terminal plays `library_terminal`, pointing the two of them at the medical wing.
4. **Root-webbed Corridor.** Dropping into the corridor starts the walk-and-talk
   (`library_to_atrium`) with its three conversation beats. When it is over the heavy library doors
   open, and the doorway beyond them loads the Atrium.

### RoomAtrium - the Atrium (Level 3, scenes 1-5)
The room uses the portrait layout in the Level 3 blueprint: Entry Hall at the bottom, the lower
walkway and glass bridge across the middle, the terminal and stairway on the west side, and the
Observation Deck across the top. Its logical `WorldSize` is 932x1043 before the global 1.5 scale.
1. **Entry Hall.** The hall is bounded by continuous walls that tie into the surrounding room
   barriers; its timed east gate is the only route into the connector, so the gate puzzle cannot be
   bypassed around a wall corner. Six blue `FluctuatingTile`s (deadly on contact) phase in and out on the room's
   shared `LoopClock`, two rows of three across the hall, so the only way to the gate is *through*
   them - timed to when each one is gone, with the 44-pixel gaps between them as the places to
   wait. The gate on the right wall is a **timed door** (`CloseDelaySeconds = 5`), and its plate
   sits at the west end, so the Echo has to be what holds it down while Aionia crosses.
2. **Lower Walkway, then the Glass Skybridge.** The six bridge panels are
   `FluctuatingTile`s of the *gap-when-absent* kind - floor that is there for 2.5 seconds of every 4
   and gone the rest, never a wall. You walk straight onto a panel while it is there, and a panel
   drops you only once the ground under your feet is the one that has gone - a landing in the middle
   of a solid panel is safe even when the panel beside it is missing, and so is the lower walkway
   underneath, whose north strip her body reaches up into the panels above. A leap carries her over
   a panel that has gone, the same way it carries her over the stairway's missing step: mid-leap
   nobody is standing on the floor, so she only has to find a solid panel on the way down.
   The counterweight plate is replaced by a checkpoint on the lower
   walkway between the bridge and the entry route. After reaching it, falls anywhere in the Atrium
   return Aionia there with one scratch. Solid corridor walls bound the east connector. The panels' phases ripple so the
   crossing chains in the direction the level sends her
   - in from the walkway at the bridge's east end, west towards the Terminal - about a panel a
   second, so the panel under her keeps arriving just ahead of her. Stepping onto the bridge
   starts `atrium_bridge`, whose choice is **timed** - run out the clock and she keeps moving.
     The skybridge panels are solid for 2.5 seconds of each unchanged 4-second cycle, giving newer
     players a half-second extra crossing cushion after room scaling; their count and phase offsets
     are unchanged.
3. **Glitched Terminal.** Both consoles carry a **four-symbol sequence** (`Switch.SequencePool`,
   drawn on the console) and they only count as matched when they are weighed down *at the same
   moment* **and** showing the *same* sequence - so it is an Echo on one of them and Aionia on the
   other, after **E** has stepped each console's code round to match. Walking into the consoles
   plays `atrium_terminal`, which offers the other way: let Yoruka decode it, which sets
   `yoruka_solved_terminal` and opens the gate outright. Whichever route was taken, the same reveal
   follows - `atrium_terminal_reveal` plays "SUBJECT A.A. ANCHOR STATUS: STABLE." - so that line has
   one copy in the content and lands either way.
4. **Broken Stairway.** The stairway is three `TimeStainStep`s that are holes until an Echo touches
   them (3 seconds of stain, with an 8-second window on the post-jump landing), and one **missing step** between the first and the second that
   no stain can ever cover: it has to be **jumped** (**Space + W/Up** while moving north). The Atrium
   gives the jump 1.5 s of airtime and a short input grace window so a new player has room to move
   across the gap; the base jump remains 0.6 s elsewhere.
   So the climb is recorded once, fall and all - every attempt stains a little further
   up the stairs - and then she follows her own replay up it, timed to the stains and leaping the
   gap where the recording leapt it. The stairway gate latches open behind her, so a fall at the
   foot of the stairs never shuts her out. Setting foot in the stair tube is also what plays the
   blueprint's third-landing conversation (`atrium_stairway`) - Yoruka's family rumour and the
   choice that follows it.
5. **Observation Deck.** Three marked spots, each with the blueprints' dotted ring on it; the first
   is stood on to mark itself (`atrium_spot_one`), and the third has to be **held** while Aionia
   stands on the second. That plays `atrium_window` - the CG the blueprint asks for, "Aionia and
   Felix", shown behind the dialogue from the memory's own nodes - sets `atrium_window_seen`
   (Fragment F3) - and per the blueprint the heavy door on the deck's east wall then **opens after
   F3**, which is where Level 4 begins.

A fall off the bridge or the stairway costs a scratch and puts the player back at the start of that
stretch - past the entry gate for the bridge, at the foot of the stairs for the stairway - not a
life: the puzzle is the timing.

### RoomMedical - Medical Facility (Level 4)
The quarantine corridor uses a four-digit keypad (4173), a pressure plate and a deterministic scanner
rail. Shadow zones hide Aionia from the beam. The triage room adds the cursor scan, four inspectables,
alternating coolant puddles, a held generator and a stasis field. The diagnostic terminal has five
sequence rounds and awards F4. The corridor is walled for its whole length, with the keypad and the pressure plate in the alcoves
the sheet draws, so the shutter is the only way north and the triage connector is the only way on.
The south hall carries the party to the Level 5 side route. Its capture
cap is 9 seconds and Echo lifetime is 15 seconds.

### RoomObservation - Observation Wing (Level 5)
The entry hall uses stepped glass tiles and A/B/C consoles, and the rhythm they want is what opens
the sheet's gate in the gallery divider - the only way from the entry hall into the observation
gallery. The security room has a three-beam laser grid, and once its footage has been read Felix is waiting at
its door, where the sheet draws its second F; the maintenance conduit repeats its steam, fan-gap and
held-valve challenge in three sections.
The conduit is the sheet's serpentine: three lanes joined by two shafts, each lane holding its
steam vent, its fan and the valve wheel at the end the player has to run to, and stepping into its
mouth plays Scene 4's objective and the six-second hold instruction. The footage sets F5. The smallest capture cap used for the recorded valve route is 8 seconds; Echo
lifetime is 15 seconds.

### RoomBusiness - Business and Finance (Level 6)
The lobby is walled with the two clocked revolving doors as its only way through, followed by a
hazard-free office and a code-built audit-folder browser for F6. On the overlook a three-by-three
patch of time-decayed tiles sits under the PAD, and the Echo that holds the pad is what freezes the
ground Aionia has to cross. Hesiod joins there. The final talk points and blue-star border play
the Atrium return beat; Level 6 is the current end of content.

## 4. Systems

### The Echo
`PlayerInput` produces one `PlayerAction` per physics tick. A fresh E press remains `Interact`; a
continued hold is `InteractHeld`. Both actions are recorded, so held consoles and valve wheels replay
continuously through `Echo.InteractHeld`. Existing press-to-use switches keep their original behavior.
`Player` executes the action and passes it to
`EchoRecorder.Tick`, which stores an `ActionFrame` only when the action changes (or on
Interact/InteractHeld/Attack) - tens of frames instead of ~1,800 for a 30 s loop. `Player.ProjectEcho()` closes
the capture window, creates an `Echo` from the frames, and hands it to `Room.AddEcho()`, which
anchors the replay to the clock so the Echo is projected **in place**: it appears at the window's
first recorded position and replays forward from that moment. Nothing about the run is reset. The
Room owns a single `LoopClock` (fixed 1/60 s steps) that the recording, every replay and periodic
hazard read, so they stay in sync. An Echo snaps to each frame's position, re-performs recorded
interactions and attacks, and interpolates in between - deterministic, no randomness, no
live input.

### Camera and world size
`Room.SCALE_FACTOR` is the single 1.5 world multiplier. Every room root scales its floor plan and
world actors together; player movement compensates for that transform, so world distance grows while
the physical movement speed and puzzle periods remain unchanged. Each room exports `WorldSize` (with
`RoomSize` retained for existing scene compatibility). A smoothed `Camera2D` follows the player and
clamps to those bounds; small worlds are centered over a filled backdrop. HUD, objective, dialogue,
pause, CG and recording controls are `CanvasLayer` UI and remain screen-fixed.

**The pendant gates it.** Capture and projection are abilities of the pendant, not of Aionia:
`Player.HasPendant` reads the `touched_pendant` flag, and until it is set **T** and **R** are
ignored silently. Because `Player.RestartRun` clears the flags, the pendant is put back on the floor
at the same moment (`Pendant` listens for `FlagsReset`), so the flags and the node always agree.
Where it goes back matters: only Room01 has a pendant lying around to pick up again
(`Room.HasPendantOnTheFloor`), so only there does a restart take the ability away - on any later
level the flag is kept, because a restart that cleared it would delete the Echo for good with no way
to earn it back.

**How long can a window be?** `Room.MaxCaptureSeconds` (default 5) - the capture closes itself when
the cap is reached, and the HUD's recording bar drains against `Player.CaptureSecondsLeft`.

**The jump is recorded like anything else.** `PlayerAction.Jump` is one of the recorded actions, so
a leap lands in the frame list (it is stored even if the action did not change, like Interact and
Attack) and the Echo re-performs it - `Entity.Jump` for `Entity.JumpAirtimeSeconds`, 0.6 s by
default. `Entity.IsAirborne` is what the Atrium's fall checks read: mid-leap nobody is standing on
the floor, so the one step of the stairway that is missing for good can be crossed. Position does
not change - this is a top-down room, so the airtime is a state, not an arc - and `Room.AddEcho`
copies the player's airtime onto every Echo it spawns, so a recorded leap clears exactly the step
it cleared while it was being recorded.

**`LoopReplay`** is the difference between the two kinds of Echo the puzzles need. `false` (Room01,
Room02, the Archives, and every plate in the Atrium): the replay plays once and then stands still
where the recording ended, which is what lets an Echo hold a plate down for as long as it lives.
`true` (the Atrium's stairway): the window repeats for as long as the Echo exists, which is what
keeps re-lighting the steps faster than their 3-second stains can fade. It is `[Export]`ed on both
`Echo` and `Room`; the room sets the default for every Echo it spawns, and `Room.OnEchoProjected`
is the room's last word on a projection - `RoomAtrium` uses it to narrow the repeat to the stairway
(`StairwayLoopZone`), because a window that repeats walks itself off the plate it was supposed to be
holding. An Atrium plate is therefore solved exactly the way Room01 taught it: record a window that
*ends* standing on the plate, project it, and walk away.

**How long does a projection last?** It does not last forever. `Room.EchoLifetimeSeconds` is
stamped onto every Echo the same way `LoopReplay` is - 20 s in Room01 and the Archives, 25 s in
Room02 and the Atrium, `0` for no limit at all. The Echo counts itself down and shows it: a ring
around the projection drains clockwise and turns warm colour under a quarter left, and the HUD
carries a line reading `ECHO   n.n s left` beside the recording bar. When the clock reaches zero
the projection fades out and the room reclaims it (`Room.RemoveEcho`) - which is the point of the
timer, because the plate it was standing on comes up and the door it was propping open closes
behind you. Keep the value comfortably longer than the walk its puzzle asks the player to make;
shorten it and a level can become unsolvable rather than tense.

### Dialogue
Everything is data: `DialogueData` holds nodes, lines and choices; `DialogueManager` runs them and
emits signals. Presentation is built in code in `DialogueManager.RefreshOverlay`, with no art
required:
- the **speaker's name is its own label** above the box, tinted by `SpeakerColor()` - Aionia, Yoruka,
  Felix, Adellaine and Hesiod each get a distinct colour, and Narrator / Instructions / Objective get
  caption styles instead of a person's voice;
- a **CG plate** can sit full-screen behind the box. Two things put a picture there: the pendant's
  flash (the memory the game opens with) and any node that declares one of its own with
  `DialogueNode.SetPlate` - the Atrium's window shows its CG while she is remembering and lifts it
  for the beats back in the room, because a CG is a beat of a conversation, not a mode;
- an optional **portrait** is loaded from `res://Assets/Portraits/<Speaker_With_Underscores>.png`
  when that file exists, and skipped silently when it does not;
- an **Objective** line also becomes the HUD's persistent "Current objective" label until a later
  objective replaces it;
- a node can carry a **flag** of its own (`SetFlagOnEnter`) and a **choice clock**
  (`SetChoiceTimeout`); running out takes the last option, so the "do nothing" answer belongs last.

`DialogueChoice` knows four things beyond its text: a flag to set, a flag that hides it once taken
(so a conversation that loops back offers only the branches not yet chosen), a flag that must
already be set for it to appear, and an **affection** change.

### Affection, routes and endings
`GameStateManager` stores affection as integer tenths per character (`yoruka`, `adellaine`, `hesiod`,
`felix`), while dialogue choices use readable decimal deltas. Thus +1.2 is saved as 12 and -0.5 as
-5; whole-number choices are multiplied by ten too. Saves include an affection scale marker, and
older integer saves are migrated on load. Every Yoruka choice in the game moves hers - kind answers up, dismissive ones down - and
the bridge choice moves hers too. The counters show up as a line in the debug HUD and are saved.
`GameStateManager.ResolveEnding()` reads the flags and the counters and returns one of the doc's
ending ids; `Scenes/EndingScreen.tscn` is the placeholder screen that displays it. The ending route
content after Level 6 is still unbuilt.

### Saving
`SaveManager` owns the files; `GameStateManager` hands it the state (`BuildSaveState()`): the
narrative flags and the affection counters. Loading restores both and re-emits every change, and a
New Game clears them. The scene path is stored by `SaveManager` itself.

### CG gallery
`MainMenu`'s **CGs** button opens `CgGallery`, which lists the CGs in its catalogue - the pendant
flash, the headache, and the Atrium's window, **"Aionia and Felix"** (`atrium_window_seen`, the CG
the Level 3 blueprint asks for) - and draws the ones still locked as **???**. An entry is unlocked
by a narrative flag, and the gallery reads those flags from the most recent save so it works from
the menu before a game is loaded. No CG art ships with the repo, so all three entries point at the
one memory image the game has; they stay separate drawings to the player because they unlock
separately.

### Restarting
**R** after a game over is the only restart the game has: `Player.RestartRun` clears the lives, the
inventory and every narrative flag (`GameStateManager.ResetFlags`), drops the Echoes
(`Room.ClearEchoes`) and starts a new loop, which calls `Room.ResetPuzzles`. The base class closes
every door and resets every switch; `RoomLibrary` and `RoomAtrium` override that hook to put back
what only they know about - Yoruka in the Archives with the dropped item gone again, the console
codes back to their starting sequences, the stairway's stains and Atrium checkpoint wiped,
and the one-shot beats (the bridge collapse, the terminal's intro and
its reveal, the window) armed again. So nothing is left half-solved behind the player, and the
pendant returns to the floor on the same flag reset (`Pendant.ResetPickup`) - which is what lets the
Echo ability be earned again instead of being gone for good. That last part only holds in Room01,
where the pendant actually is on the floor: on every later level the restart keeps the ability
rather than taking it away with nothing left to earn it back from.

## 5. OOP map (for your defense)
| Concept | Where |
|---|---|
| Inheritance | `Entity` -> `Player`, `Echo`; `Room` -> `RoomLibrary`, `RoomAtrium`, `RoomMedical`, `RoomObservation`, `RoomBusiness`; `Obstacle` -> `FluctuatingTile`, `TimeStainStep`; `Switch` -> `ValveWheel` |
| Polymorphism | `Update`, `TakeDamage`, `Die`, `Attack`, `Interact`, `Jump`, held interaction and room progression are specialized by their subclasses; room subclasses add beats and reset state through `Room._PhysicsProcess` / `Room.ResetPuzzles` |
| Interfaces | `IDamageable`, `IInteractable` (Switch, Door, Inspectable, Pendant, keypad and browser), `IHeldInteractable` (Switch), `IUpdatable` (Entity, Switch) |
| Puzzle composition | `RoomMedical` coordinates `ScannerSweep`, `KeypadPanel`, `CoolantPuddleRow`, `StasisField` and `SequenceTerminal`; `RoomObservation` coordinates `CrackingGlassTile`, `LaserGrid`, `SteamVent`, `GapFan` and `ValveWheel`; `RoomBusiness` coordinates `RevolvingDoor`, `TimeDecayTile` and `AuditFolderBrowser` |
| Encapsulation | Private fields only; `[Export]` properties clamp values (e.g. `MaxHealth >= 1`, `MaxCaptureSeconds >= 0.5`); Echo's frames and the recorder are private |
| Composition | `Room` HAS-A lists of entities/obstacles/switches/doors/echoes; `Player` HAS-A `PlayerInput` + `EchoRecorder` |
| Singleton | `GameStateManager.Instance` (narrative state, flags, affection, endings), `DisplayManager.Instance` (window, F11), both Autoloads |
| Signals | `HealthChanged`, `Died`, `PlayerDied`, `ActivationChanged`, `DoorStateChanged`, `LoopStarted`, `FlagChanged`, `AffectionChanged`, dialogue signals... |
| Level progression | `Room.LevelNumber` / `Room.NextLevelPath` (per-room, set in the Inspector) + `GameStateManager.GoToNextLevel()`; a level ends when the player reaches its exit zone, which every room gates behind its own last puzzle |

## 6. Not implemented yet
Levels 4-6 are built from `Chrome6.pdf` and the supplied blueprints. The remaining level list is:

| # | Level | Status |
|---|---|---|
| 7 | The Atrium | not implemented |
| 8 | (blank in the doc) | not implemented |
| 9 | (blank in the doc) | not implemented |
| 10 | (blank in the doc) | not implemented |

Also not implemented:
- The later romance and ending routes, including any scenes after Level 6.
- Acting on an ending: `ResolveEnding()` and the ending screen exist, but no level leads to them.
- Character art: no sprites, portraits or CGs ship with the repo; the code-drawn visuals and the
  code-built UI are still the placeholder layer, and the CG gallery draws "???" cards for its
  entries because there is only the one memory image to show.
>>>>>>> Stashed changes

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
<<<<<<< Updated upstream
- The Echo "frame-based replay" feeds `PlayerAction`s through the same code path as the player;
  it does not literally call `Input.ParseInputEvent`, because that would also drive the live player.
- Combat outcomes in a replay depend on where enemies are in that loop, so an Echo's attack may hit or miss differently.
- Placeholder UI (HUD, dialogue box) is built in code so it works with zero assets; swap for real UI by listening to the dialogue signals.
=======
  A non-solid `Obstacle` is scenery and never blocks, which is what lets one class cover both walls
  and the Atrium's phasing tiles.
- The Echo "frame-based replay" feeds `PlayerAction`s through the same code path as the player; it
  does not literally call `Input.ParseInputEvent`, because that would also drive the live player.
- The game ships without enemies, so **J** has nothing to hit: `Entity.Attack` still runs and an
  Echo still replays the swing, there are just no targets. `Entity.IsHostileTo` is the hook that
  decides who can be hurt, and nothing overrides it now - `Scripts/Enemies/Enemy.cs`,
  `Scripts/Enemies/EnemyAI.cs` and `Scenes/Enemy.tscn` have all been deleted, along with the
  `Room` list that used to reset them each loop.
- **Actors draw above the floor plan.** Levels 4-6 build their map in code when the room is ready, which puts every
  floor rectangle and wall *after* the player in the draw order; at equal `z_index` those opaque fills painted over
  her, so she could not be seen on those levels at all. Anything that walks now takes an explicit layer above the
  map (`Entity.ActorZIndex`, 12).
- The keypad, the scan ring, the diagnostic terminal and the audit ledger cabinet draw themselves now. They used to
  be invisible interactables, which left the quarantine corridor reading as a wall with no way past it.
- **Everything the player presses E on answers to its own box, not to a point.** Each device reports its distance as
  a rectangle, so standing in front of a thing targets that thing, and the crowded rooms - the Level 4 triage room
  and the Level 6 border - keep a clear gap between neighbours. The diagnostic terminal used to sit against the
  notes and the whiteboard, where the nearest-interactable pick never chose it at all: Fragment F4 was unreachable
  and the level could not be finished.
- **The green exit box is a condition check on Levels 4-6, not a doorway**, so it only fires when the level's own
  flags are all set. Standing in it while they are not now prints what is still outstanding on the banner
  (`Room.ExitLockedHint`) instead of doing nothing at all. Levels 1-3 gate their exits with a real door and need no
  hint.
- The world is scaled by `Room.SCALE_FACTOR`; the camera follows and clamps to each room's `WorldSize`.
- **A phasing tile is one of two opposites.** The Atrium's blue tiles are *obstacles*: while one is
  "here" it blocks the way and costs one scratch with a return to the room start, and while it is
  gone it is a ghost outline you can walk through. The bridge panels and the walkway's hole are the
  other kind - *floor*: they never block, and their absence is the hazard, because standing over
  one while it is gone is a fall. Both are `FluctuatingTile`s of the same class, told apart by
  `GapWhenAbsent`, and both are present for 2 s of every 4 s offset by `PhaseSeconds`, so a row
  ripples rather than blinking in unison. Because they read the room's shared `LoopClock`, an
  Echo's replay crosses them on exactly the rhythm the player did. The blue tiles' "no walk" is the
  blueprint's own legend wording; the Level 2 collapsed-shelves room and Level 3's walls were both
  adjusted so the timing cannot be walked around instead of through.
- **The stairway's falls are the puzzle, not a fail state.** An unstained `TimeStainStep` drops her
  back to the foot of the stairs for one point of damage, and the recording keeps running through
  the fall - so a failed attempt is still a useful one: it stains every step it managed to touch,
  and the next recording starts higher up. That is the only way the first recording can be made at
  all, and it reads exactly like the mechanic it is built out of.
- A fall in the Atrium costs a scratch and a walk back, not a life: the puzzle is the timing, and
  losing the run to it would be noise.
- Placeholder UI (HUD, dialogue box, gallery, ending screen) is built in code so it works with zero
  assets; swap for real UI by listening to the dialogue signals.
>>>>>>> Stashed changes
