# Levels 4-6 and World Scaling Plan

## Phase 0 recon

Sources reviewed: `README.md`, `DESIGN.md`, `Chrome6.pdf` pages 16-29, the attached Level 4-6 blueprints, project scripts and current room scenes. `whole_map_for_now.png` is not in the workspace; level chaining will follow the written request and the connection labels visible on the supplied blueprints.

### Existing architecture and planned classes

| Treatment | Classes |
|---|---|
| Reuse as-is | `Entity`, `Obstacle`, `IInteractable`, `IDamageable`, `IUpdatable`, `ActionFrame`, `Echo`, `EchoRecorder`, `FloorMarker`, `Pendant`, `Inspectable`, `TimeStainStep`, `FluctuatingTile`, `PauseMenu`, `SaveManager` |
| Extend | `Room` (camera, scale constant, world-size bounds, shared clock and generic interactable dispatch), `RoomAtrium` (Level 4 destination and scaled Atrium world), `RoomLibrary` (scaled layout), `PlayerAction`, `PlayerInput`, `Player`, `EchoRecorder`, `Echo`, `Entity` (held interaction dispatch), `Switch` (held interaction and timed activation), `DialogueData` / `DialogueChoice` (fractional and multiple affection deltas), `DialogueManager` (Levels 4-6 content and memory fragments), `GameStateManager` (tenths-based affection through save/load) |
| Add | `RoomMedical`, `RoomObservation`, `RoomBusiness`; `IHeldInteractable`; `ScannerSweep`; `KeypadPanel`; `ScanMinigame`; `SequenceTerminal`; `CoolantPuddleRow`; `StasisField`; `CrackingGlassTile`; `LaserGrid`; `SteamVent`; `GapFan`; `ValveWheel`; `RevolvingDoor`; `TimeDecayTile`; `AuditFolderBrowser` |

Single-node rules belong in these small reusable puzzle/UI nodes. Room subclasses coordinate multi-node progression, shared clock checks, scene order, reset behavior, and flags, following `RoomAtrium`.

### Existing sizing, input and coordinate conventions

- Existing room scenes and the `Room` default declare `RoomSize = (1152, 648)`. `Room._Draw` draws that background and uses it as the bounds check in `IsBlocked`.
- Repository-wide search found literal viewport assumptions in `Room`'s HUD (objective/banner widths 1152; recording bar, labels and debug readout positioned against 648), `DialogueManager`'s overlay panel and toast positions/sizes (x 20..1132, y 210..630), and README text describing the Atrium's 1152x648 band layout. `RoomAtrium.tscn` has 1152x648 room bounds and three band-divider obstacles. Existing per-scene placements live in `Room01.tscn`, `Room02.tscn`, `RoomLibrary.tscn`, and `RoomAtrium.tscn`.
- `RoomAtrium` also exports `BridgeFallRespawnPoint`, `StairwayFallRespawnPoint`, and `StairwayLoopZone`; all are world coordinates and must scale with its layout. `RoomLibrary`'s collapsed-shelves door is at x=650 and must scale with its geometry. FloorMarker positions and sizes are scene-authored and included in the scaling.
- `PauseMenu`, `EndingScreen`, and the CG plate are CanvasLayer/Control based. `DialogueManager` creates a CanvasLayer; `Room.BuildHud` creates a CanvasLayer. They remain screen-fixed when the room world is scaled and the camera moves. Existing menu controls use full-rect anchors.
- `PlayerInput.ReadAction()` maps `E` through `Input.IsActionJustPressed("interact")`; E is currently a press, never a held action. `PlayerAction.Interact` is stored as a one-shot frame; `Echo` calls `Interact()` on that frame only. Holding E cannot currently be captured or replayed. Plan: add `InteractHeld` as an ongoing recorded action while E is down (except a just-pressed tick remains the legacy `Interact` press), and have switches accrue required hold duration from shared room physics time for both player and Echo. This keeps existing press-to-use controls.
- `SCALE_FACTOR = 1.5` will live once in `Room` and be applied to the room root's world transform. Exported world dimensions and all scene-authored positions/sizes remain in the existing logical units; the root transform scales floors, collision bounds, markers, doors, switches, NPCs, spawn/respawn locations and rectangular zones together. The camera clamps in logical room coordinates and uses the viewport's visible world extents. CanvasLayers do not inherit the world camera transform.

### Coordinate plan

Levels 1 and 2 use the original 1152x648 room dimensions; Level 3 follows the supplied portrait Atrium plan at 932x1043 logical units. Their root transforms apply the 1.5 world scale. All authored room-world geometry is multiplied around the room origin `(0,0)`. Player starts/exits, collision geometry, plates, doors, entities, markers, `RoomAtrium` respawn points and `StairwayLoopZone`, and the RoomLibrary door at x=650 therefore move with their room. The Atrium layout follows the blueprint's entry hall at the bottom, lower walkway and bridge across the middle, terminal and stairway on the west, and observation deck at the top.

For each new blueprint, use its black frame as the world rectangle. The frame is x=41..708 and y=41..788 in the supplied images, giving logical world size 667x747 and scaled world size about 1000.5x1120.5. Convert a blueprint point `(x,y)` to logical `(x-41,y-41)`; convert a rectangle's width/height directly from blueprint pixels; the Room root applies 1.5 to both. This proportional map preserves relative placements and exactly follows marker ordering.

| Level | Scene order and normalized blueprint layout (image coordinates; convert by subtracting frame origin, then room scale applies) |
|---|---|
| 3 Atrium | Portrait world 932x1043: Entry Hall `(59,800,408,198)` at bottom; gate opens to an east connector; Lower Walkway under a six-panel horizontal Glass Skybridge `(59,565,618,100)`; Terminal `(59,423,214,143)` west of the bridge; Broken Stairway `(89,215,84,208)` north of the terminal; Observation Deck `(89,47,615,168)` at the top with three spots and the Level 4 exit on its east edge. Entry hazards remain six blue tiles in two rows. |
| 4 Medical | Rebuilt on `Assets/level4_medical_map.png` (640x360, drawn at 2x like the Atrium). Quarantine corridor `x=60..132, y=118..352` south-to-north; keypad alcove west `x=16..60, y=200..240`; scanner runs eastward across the corridor, shadows in the gurneys at `(80,166),(115,220),(85,286),(115,330)`; plate in the notch east `x=132..166, y=280..320`; shutter on the hazard tape `x=58..130, y=112..118`. Records room `x=30..480, y=12..118`: stasis field on the blue disc `(398,44)`, five-puddle row down `x=452, y=40..96`, terminal `(420,72)`, sealed case `(445,102)`, generator `(115,22)`, cot `(65,24)`, notes `(165,13)`, whiteboard `(75,92)`, cabinet `(135,104)`. Connector `x=414..465, y=118..210`; hall `x=325..620, y=205..286` with the sealed gate on the hazard tape `(586,240)`; side route `x=500..540, y=286..352` down to the Level 5 exit. |
| 5 Observation | Rebuilt on `Assets/level5_observation_map.png` (640x360, drawn at 2x like the Atrium). Entry glass hall `x=280..630, y=194..331`, start `(462,319)`, cracked floor `x=288..630, y=214..350`, north consoles A/B/C at `(320,200),(540,200),(578,200)`; the gallery divider `y=186..194` with its gate at `x=436..480`. Central pane `x=280..630, y=10..187`, Felix at the window `(455,80)`. The pane's west door corridor `x=262..280, y=90..125` leads into the security room `x=10..272, y=8..272`, Felix at the door `(250,107)`, console `(80,32)`, three beams sliding west across `x=16..266, y=56..206`. The conduit drops from the room's south opening `x=40..110`: lane one `y=278..294`, east shaft `x=242..268`, lane two `y=310..326`, west shaft `x=14..40`, lane three `y=342..358` to the Level 6 exit `x=242..268`. |
| 6 Business | Lobby `(83,484,302,211)`, start `(164,675)`, Felix talk point `(118,544)`, revolving doors centered `(234,577)` and `(234,634)`; connector north to Executive Office `(103,263,252,142)`, jacket `(113,277)`, desk `(174,344)`, poster `(213,267)`, Yoruka/talk point `(259,302)`; connector east to Audit Archive `(443,263,232,142)`, folder shelves x=463 at y=284/320/356, ledger `(623,343)`; connector north to overlook `(313,123,362,92)`, PAD `(628,169)`, Hesiod `(334,169)`, talk point `(356,169)`. Exit/border back to Atrium at `(234,169)`. |

The coordinate list names every gameplay object represented by the blueprint; extra corridor floor and boundary rectangles will be derived from the same room and connector rectangles. No image assets are added; visuals are drawn by puzzle nodes in the existing placeholder style.

### Timing and scaling checks to perform during implementation

- Existing authored periods remain 2s/4s for Atrium phases, 5s timed gate, 3s stain, and 0.6s default jump airtime. The Atrium now overrides airtime to 0.9s to make its recorded jump reliable. Scale changes distances by 1.5 while world movement speed remains unchanged, so walk durations are recalculated from `Entity.MoveSpeed` (default 120 px/s), including the A/B/C paths, Level 4 nine-second recording, held-console routes, steam valve routes, and PAD crossing.
- Level 4 rooms use 15s Echo lifetime. Capture caps are set per room to the shortest cap that covers the longest required recorded action sequence, no less than 9s on Level 4. Echo lifetime is increased only where measured post-scale traversal plus required interaction time exceeds 15s.
- Level 3 skybridge panels use a 9.75-second solid window on a 12.3-second cycle. The wave advances 1.95 seconds per panel; the extended overlap makes each successive panel light before Aionia reaches it, while the short absent window preserves the moving-gap hazard. Panel count and layout are unchanged.
- Follow-up access adjustment: the lower-walkway pressure plate and extra timed hole were removed; the lower walkway is safe and the checkpoint sits there. After reaching it, falls from the bridge or stairway return Aionia to the checkpoint. The east corridor connector has solid side walls. Atrium jump airtime is 1.5 s.
- Follow-up access adjustment: Level 4's room walls now run past the corners they meet, so the space between the rooms is sealed off - it is not part of the building. The start has its own short corridor floor, as the blueprint's "start corridor south" calls for. The Level 5 exit is the blueprint's blue star at logical (483,527), centred in the side route rather than hanging below it.
- The post-jump landing step now holds its Echo stain for 8 s (the other two stair steps remain at 3 s) to provide enough time to get across after the jump.
- Fractional affection is stored as signed tenths in the existing integer counters and JSON saves: PDF values such as +1.2 and -0.5 are stored as +12 and -5 units. All existing integer choices are multiplied by ten at the same time; display/debug and route threshold comparisons convert from tenths. This avoids changing save value types and preserves precision.

## Phase 1-5 implementation sequence

1. Implement world scaling and a smoothed, clamped `Camera2D` owned by `Room`; make small rooms center inside the viewport. Keep HUD/dialogue/pause/CG/record bar in CanvasLayers.
2. Add deterministic held-interact capture/replay and reusable puzzle primitives, all periodic hazards reading the shared `LoopClock`.
3. Built the three room scripts/scenes from the blueprint transforms and connected `RoomAtrium -> RoomMedical -> RoomObservation -> RoomBusiness`, leaving Level 6's `NextLevelPath` empty.
4. Added PDF-derived dialogue registrations, choice branches, flags, objectives, and F4-F6 fragment rewards in `DialogueManager.BuildDemoContent()`; updated README and this plan with chosen capture windows.
5. `dotnet build` succeeds with no warnings or errors. Scene resource paths and exported scene values were inspected textually; Godot editor validation and playtesting remain to be done.

## Level 2 rebuild - the Archives sheet

### Phase 0 recon

The supplied Level 2 map is 640x360 and is the level's art as well as its plan. It is drawn at twice
its own resolution (`RoomLibrary.MapScale`) - a whole-number upscale, so its pixels stay square, and
wide enough in the aisles and doorways it draws for the player's 28-unit body - which makes the room
1280x720. Nothing else is painted: one unit in `RoomLibrary.BuildMap` is one pixel of the map, and
every rectangle below is quoted in those pixels.

What the sheet shows, room by room: a small room at the top left with debris and one hazard-striped
panel; the Archives filling the west, a wide brick room with ranks of shelving, loose furniture
against its west wall and a stack that has come down in the middle; the archives' east wall has one
opening, a connector to the room on the right; that room is floored in grey tile, with a wide lit
panel across its north wall, a large machine and a crate below it; the bottom of the sheet is one
long brick corridor running its whole width, hung with cabling and roots and lined with benches,
machines and crates along its north wall.

### Layout

| Sheet rectangle | What it is | Notes |
|---|---|---|
| `(6,6,184,56)` | Reading room | the level's start; door in its south wall at `x=101..152` |
| `(16,12,44,30)` `(70,10,34,26)` `(112,14,30,24)` | Cork board, computer, drawers | the Archives' front desk, each with its own line |
| `(150,8,20,50)` | Service door | the striped panel on the sheet: sealed, and leads nowhere |
| `(6,68,374,194)` | The Archives | ranks of shelving at `y=120/168/216`, the rank along `y=68`, the vertical spine at `(306,204,16,58)` |
| `(216,80,46,40)` | Collapsed stack | blocks; the way east runs round it |
| `(170,228,66,18)` | Toppled shelf | blocks too: it is a shelf lying across the aisle, not a rug |
| `(150,134,24,34)` | Yoruka | in the first aisle the way on runs through, a few steps from the doorway; the pan lands on her, and she does not block the aisle |
| `(380,142,6,40)` | Archive east opening | the only way out of the Archives |
| `(386,142,49,40)` | Collapsed shelves door | the block filling the connector; `library_collapsed_shelves` on approach |
| `(348,120,30,30)` `(348,186,30,30)` | Rubble piles | flanking `(363,168)`, the latch the Echo holds; dotted ring on the stand spot |
| `(435,78,199,172)` | Terminal room | grey tile; opening to the corridor at `x=545..585` in its south wall |
| `(487,88,73,40)` | The terminal | invisible over the mainframe the map draws; plays `library_terminal` |
| `(552,150,50,60)` `(558,214,44,28)` | Server rack, spares | |
| `(600,112,24,34)` | Yoruka waiting | shown once the fallen shelves are open and the player is through them |
| `(500,250,40,42)` | Drop into the corridor | the sheet's vertical connector, at the x the map draws it - clear of the rack and the spares, so the way down is the way it looks |
| `(110,292,524,62)` | Root-webbed corridor | the walk-and-talk trigger at its east end |
| `(200,292,14,62)` | Heavy library doors | open when `library_to_atrium` ends |
| `(224,292,52,22)` `(330,292,46,28)` `(450,292,42,24)` `(590,292,34,26)` | Bench, conduit, workbench, spares | along the north wall, so the walking floor stays clear |
| `(112,296,40,54)` | Exit | beyond the heavy doors, to the Atrium |

### Classes

| Treatment | Classes |
|---|---|
| Reuse | `Room`, `Door`, `Switch` (the latch), `Inspectable`, `FloorMarker`, `Obstacle`, `DialogueManager`, and a `Sprite2D` for the map itself |
| Extend | `Obstacle` gains `DrawBox`: the map draws the walls, the shelving and the furniture, so the shapes that block them must not also draw. `FloorMarker` and `Inspectable` already carried that switch |
| Add | nothing at all. The map draws every shelf, the terminal, the roots and the furniture, so the level needs no drawing nodes - only collision and the overlays the map does not have (the plates, the rings and the captions) |

`RoomLibrary` keeps every beat it had and every dialogue id it used. What changed is where the level
lives: its rooms are the map's rooms, so the collapsed shelves are the connector between the Archives
and the terminal room - the one opening between them, so the block cannot be walked around - and the
scene file holds nothing but the room script, the dialogue manager and the player.

The map has to be imported before the room can load it (`<exe> --headless --path <project> --import`),
which is also what `dotnet build` plus a first run would do in the editor.

### Validation

`_check_library.gd` runs headless (`--headless --fixed-fps 60 --script res://_check_library.gd`) and
stages the level the way it is played - everything shut, then the fallen shelves open, then the
library doors - flood-filling the floor from the player's start at each stage on a 6-unit grid with
the player's own 28x28 body. It asserts that the latch is reachable while the terminal room is not,
that the corridor opens with the shelves and the exit stays shut until the heavy doors do, that every
interactable has somewhere to stand, and - driven with synthetic input - that walking down into the
Archives starts the scene 2 pan and zooms the camera.

`_shot_library.gd` renders four screenshots of the level - the whole map, the Archives, the terminal
room and the corridor - with the room's own camera held still, for eyeballing the map against the
sheet. It must run without `--headless`, which has no renderer.

## Level 3 rebuild - the Atrium map

### Phase 0 recon

The supplied Atrium map is 640x360 and is the level's art as well as its plan, so the same treatment
applies: drawn at twice its own resolution (`RoomAtrium.MapScale`) to 1280x720, one unit in
`RoomAtrium.BuildMap` to one pixel of the map, every rectangle below quoted in those pixels.

Read off the map, room by room: the observation deck is the lit band across the top; the broken
stairway comes down the west side as a narrow lit tube with the broken treads drawn in it; the
glitched terminal is the room at the bottom of that tube, its two consoles drawn as the two lit
screens in its south wall; the glass skybridge is the band across the middle, drawn as a row of dark
panels with their blue glow; the lower walkway is the band directly under it; and the entry hall is
the lit room at the bottom left, with the corridor running east out of it.

### Layout

| Map rectangle | What it is | Notes |
|---|---|---|
| `(104,16,496,54)` | Observation deck | the three window spots at `x=200/330/460`, `y=44`; the deck's south wall opens at `x=160..216` onto the stairway |
| `(600,16,6,54)` | Deck east wall | the heavy door set into it at `y=24..51`; the Level 4 threshold beyond |
| `(160,76,56,68)` | Broken stairway | `TimeStainStep`s at `y=118/92/76`, the jump line at `y=112`, and the stairway gate at `y=138` |
| `(80,144,150,66)` | Glitched terminal | consoles at `(110,186)` and `(170,186)`; north wall opens onto the stairway, south wall onto the bridge |
| `(88,216,462,34)` | Glass skybridge | six `FluctuatingTile` panels, 77 px each, 2.5 s solid of every 4 s, phases `0,3,2,1,0,3` |
| `(88,256,432,30)` | Lower walkway | the checkpoint at `(290,256)`; the wall above it opens at `x=300..380` onto the bridge, the wall below at `x=264..368` and `464..520` onto the corridor |
| `(8,292,512,60)` | Corridor and entry hall | the gate wall at `x=184`; six blue tiles in two rows of three across the hall |
| `(26,322)` | Start | the entry hall's west end |

### Classes

| Treatment | Classes |
|---|---|
| Reuse | `Room`, `Door`, `Switch`, `FluctuatingTile` (both kinds), `TimeStainStep`, `FloorMarker`, `Obstacle`, and a `Sprite2D` for the map |
| Extend | `FluctuatingTile`'s drawing: a gap-when-absent tile is now a translucent pane rather than an opaque slab, because on this level the bridge's glass is drawn by the map and a fill would paint it out |
| Add | nothing: as with the Archives, the map draws the rooms, so the level needs collision and overlays only |

`RoomAtrium` keeps every beat, flag and dialogue id it had. The logic - the consoles matching, the
reveal, the falls, the window, the exit - is the same code as before the rebuild; what changed is
that it reads nodes built from the map instead of scene-authored ones, and that the world is the
map's 1280x720 rather than the old portrait 932x1043.

### Validation

`_check_atrium.gd` runs headless and stages the level the way it is played - everything shut, then
the entry gate open, then the stairway gate, then the exit door - flood-filling the floor at each
stage. It freezes the room and takes the phasing tiles out of the collision test first: the phases
would otherwise make every answer depend on the tick, and what it is checking is the walls. It asserts
that each room is sealed until its own key opens it, that every interactable has somewhere to stand,
and that the dark outside the rooms cannot be walked into.

`_shot_atrium.gd` renders five screenshots (whole, entry, bridge, stairs, deck) with the camera held
still; it reads the level's opening conversation through first, or the panel would cover every shot.
Both need the map imported first (`<exe> --headless --path <project> --import`).
