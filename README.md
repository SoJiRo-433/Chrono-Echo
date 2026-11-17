# Chrono Echoes - Back End (Godot 4 .NET / C#)

Compiles clean (`dotnet build`, 0 warnings, 0 errors).

The game now runs Levels 1-6: the ruined room (Level 1), the hallway and Archives (Level 2), the
Atrium (Level 3), Medical Facility (Level 4), Observation Wing (Level 5), and Business Department
(Level 6). Levels 7-10 are listed under
[Not implemented yet](#6-not-implemented-yet).

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

| Cast in the demo | Role |
|---|---|
| **Aionia Avgerinos** | Protagonist. Chronology student, amnesiac after the Hyperion disaster. |
| **Mochizuki Yoruka** | Junior assistant, blunt and explosive; appears through Level 6. |
| **Felix Lasarewitch** | Her partner on Project Hyperion; he appears in the pendant's memory flash. |
| **Adellaine Moriarty**, **Hesiod Moriarty** | Medical student and business student; join in Levels 4 and 6. |
| **Pendant** | Her key item and the source of the Echo ability. |

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
4. Build once (hammer icon, top right), then press F5. Main scene: `main_menu.tscn`. The menu
   provides Start/Continue, New Game, Load Game, **CGs** (a gallery) and Quit.

## 2. Controls
| Key | Action |
|---|---|
| **W A S D** / arrows | move |
| **Space** | jump - short airtime, enough to clear one missing step of the Atrium's stairway |
| **E** | interact (examine an object, use a lever, step a console's code) |
| **J** | attack (nothing to hit - the game ships without enemies) |
| **T** | open / close a capture window - **does nothing until she has the pendant** |
| **R** | project the captured window as an Echo - **does nothing until she has the pendant** |
| **Enter** / **Space** | advance dialogue |
| **1 2 3** | pick a dialogue choice |
| **F3** | hide / show the debug readout |
| **F11** | full screen |
| **Esc** | pause menu |

The recording bar, the current objective and the GAME OVER / ROOM CLEAR banner are player-facing and
always on screen; only the debug readout answers to **F3**.

## 3. The levels

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

The Archives (`RoomLibrary`) is built on its own map: `Assets/level2_archives_map.png`, a 640x360 plan
of the whole level, drawn at twice its resolution so its pixels stay square and the aisles it draws
are wide enough to walk. Nothing else is painted - every wall, rank of shelving and fixture is
invisible collision over the map (`Obstacle.DrawBox`), and everything the player interacts with is an
overlay on it.

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
The level's art is the level's own map (`Assets/level2_archives_map.png`): one 640x360 plan holding the
reading room at the top left, the Archives with their ranks of shelving, the connector where the
shelves have come down, the lit terminal room on the right, and the root-webbed corridor along the
bottom to the heavy library doors. It is drawn at twice its resolution - a whole-number upscale, so
its pixels stay square, and wide enough in its aisles and doorways for the player's 28-unit body -
which makes the room 1280x720. **Nothing in the map moves, so everything the level does is an overlay
on it**: the walls, the ranks and the furniture are `Obstacle`s with their boxes off, so the map's own
rooms are what she walks inside; everything E answers to is an `Inspectable` with its box off too, so
the caption and the interaction land on the object the map drew; and what the map does not draw - the
plates, the Echo's dotted stand spot, the two doors and the captions - is drawn. `RoomLibrary.BuildMap`
is the only place the level's geometry exists, and one unit in it is one pixel of the map.
1. **The Archives.** The reading room holds the **cork board**, the **computer** and the **drawers**,
   each with its own line, and the striped **service door** back to the bridgeway - the one door in
   the level that leads nowhere, because the hallway side of it is buried. Walking down through it
   into the Archives pans the camera off Aionia and onto **Yoruka** - the doc's Scene 2 beat - and
   only then does she think about approaching her, which is where the "Approach the student"
   objective comes from. **Yoruka** is an interactable among the shelves; **E** starts `library_yoruka`,
   and when it ends she runs off - the node hides, and a hidden interactable is skipped, so she
   stops being examinable in the same moment. **She also leaves something behind**: the moment she
   runs, a hidden `Inspectable` appears on the exact spot she stood on (`RoomLibrary.DropYorukaItem`),
   so the blueprint's "leaving behind an item" is a thing you can walk over and pick up. It plays the
   line `library_item_taken` and sets a flag of the same name, and until she runs it is hidden - a
   hidden interactable is skipped, so it does not exist for the player either. The ranks themselves
   are invisible collision laid over the shelving the map draws, and the stack that came down across
   the north aisle is why the way east runs round it.
2. **Collapsed Shelves.** The fallen shelves are the block between the Archives and the Terminal: a
   `Door` filling the connector in the middle of the Archives' east wall (`CollapsedShelvesDoor`) -
   the one opening between the two rooms, so there is no way round it. Beside it the **safety latch**
   has to be held by an Echo while the player slips through, exactly like Room01's door, with the
   collapse narrated by `library_collapsed_shelves`. The latch is flanked by **two rubble piles** -
   one on each side of it, with the gap between them wide enough to stand in and the route past them
   left open - and a dotted blue ring marks it as the Echo's stand spot. The
   latch is on the *near* side of the block on purpose: the Echo has to reach it before the door
   opens, not after.
3. **Terminal.** Past the fallen shelves Yoruka is waiting in the lit room (`library_yoruka_followup`),
   and the surviving **terminal** - the mainframe the map draws burning in the dark - plays
   `library_terminal`, pointing the two of them at the medical wing.
4. **Root-webbed Corridor.** The drop under the terminal room puts her in the corridor, and that
   starts the walk-and-talk (`library_to_atrium`) with its three conversation beats. The corridor's
   fittings - the bench, the conduit, the workbench, the spares - are along its north wall, and its
   floor is the one the map webs with roots. When the conversation is over the heavy library doors
   open, and the doorway beyond them loads the Atrium.

### RoomAtrium - the Atrium (Level 3, scenes 1-5)
The level's art is its own map (`Assets/level3_atrium_map.png`): one 640x360 plan holding the
observation deck across the top, the broken stairway coming down the west side, the glitched terminal
under it, the glass skybridge across the middle with the lower walkway directly beneath it, and the
entry hall at the bottom left with the corridor running east out of it. It is drawn at twice its
resolution, which makes the room 1280x720, and - as in the Archives - nothing is painted twice: the
walls, the stair tube and every part of the map that is not a room are invisible collision over the
art, and what the map cannot draw is laid on top of it: the six blue tiles, the six skybridge panels,
the three stair steps and the jump line between them, the plates and their dotted stand spots, the
three doors and the captions. `RoomAtrium.BuildMap` is the only place the level's geometry exists, and
one unit in it is one pixel of the map. Every beat, flag and dialogue id is the level's own, unchanged
- the rooms are simply the map's rooms now.
1. **Entry Hall.** The hall is bounded by continuous walls that tie into the surrounding room
   barriers; its timed east gate is the only route into the connector, so the gate puzzle cannot be
   bypassed around a wall corner. Six blue `FluctuatingTile`s (deadly on contact) phase in and out on the room's
   shared `LoopClock`, two rows of three across the hall, so the only way to the gate is *through*
   them - timed to when each one is gone, with the lanes between the columns as the places to
   wait. The gate is a **timed door** (`CloseDelaySeconds = 5`) set into a wall across the corridor
   at the hall's east end, and its plate sits at the far end of the hall, so the Echo has to be what
   holds it down while Aionia crosses.
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

## 7. Design notes / honest limitations
- Collision is a rectangle check in `Room.IsBlocked` instead of Godot physics (simpler to explain).
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
  blueprint's own legend wording; the Level 3 walls were adjusted so the timing cannot be walked
  around instead of through, and the Level 2 collapse fills the only opening between the Archives and
  the terminal room for the same reason - there is no way round it, only through it.
- **The stairway's falls are the puzzle, not a fail state.** An unstained `TimeStainStep` drops her
  back to the foot of the stairs for one point of damage, and the recording keeps running through
  the fall - so a failed attempt is still a useful one: it stains every step it managed to touch,
  and the next recording starts higher up. That is the only way the first recording can be made at
  all, and it reads exactly like the mechanic it is built out of.
- A fall in the Atrium costs a scratch and a walk back, not a life: the puzzle is the timing, and
  losing the run to it would be noise.
- Placeholder UI (HUD, dialogue box, gallery, ending screen) is built in code so it works with zero
  assets; swap for real UI by listening to the dialogue signals.
