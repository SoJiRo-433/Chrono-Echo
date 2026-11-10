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
| 4 Medical | Start corridor south `(189,660)`, quarantine corridor `x=143..234, y=229..645`, south-to-north; keypad alcove west `(103,343,40,51)`; scanner runs eastward across corridor at `y=423`; shadows/gurneys around `(165,294),(209,364),(165,463),(205,513)`; plate `(239,535)`; shutter `(143,224,91,12)`. Triage room `(143,73,321,157)`: five-puddle row at x=445, y=92/113/135/157/179; four inspectables along north row x=169/220/250/289; G at `(180,79)`, terminal D `(289,79)`, station/case/circle on east side. Hall `(383,443,281,60)` leads to the side route and Level 5 exit blue star `(524,568)`. NPCs/talk point per A/Y letters and purple diamonds in scenes 2 and 5. |
| 5 Observation | Entry/glass hall `(383,515,292,160)`, start `(534,662)` and north consoles A/B/C at `(418,517),(602,517),(632,517)`; left and right columns follow dashed blueprint routes. Central pane `(383,293,292,222)` with Felix/talk point at `(529,382)`. Security room `(143,133,152,391)`, C console `(195,142)`, lasers span x=174,219,264 and slide west. Three conduit sections use the single-tile turns shown in lower-left blueprint: vents/fans/valves at the red squares, circles and V markers respectively, leaving Level 6 exit at `(342,681)`. |
| 6 Business | Lobby `(83,484,302,211)`, start `(164,675)`, Felix talk point `(118,544)`, revolving doors centered `(234,577)` and `(234,634)`; connector north to Executive Office `(103,263,252,142)`, jacket `(113,277)`, desk `(174,344)`, poster `(213,267)`, Yoruka/talk point `(259,302)`; connector east to Audit Archive `(443,263,232,142)`, folder shelves x=463 at y=284/320/356, ledger `(623,343)`; connector north to overlook `(313,123,362,92)`, PAD `(628,169)`, Hesiod `(334,169)`, talk point `(356,169)`. Exit/border back to Atrium at `(234,169)`. |

The coordinate list names every gameplay object represented by the blueprint; extra corridor floor and boundary rectangles will be derived from the same room and connector rectangles. No image assets are added; visuals are drawn by puzzle nodes in the existing placeholder style.

### Timing and scaling checks to perform during implementation

- Existing authored periods remain 2s/4s for Atrium phases, 5s timed gate, 3s stain, and 0.6s default jump airtime. The Atrium now overrides airtime to 0.9s to make its recorded jump reliable. Scale changes distances by 1.5 while world movement speed remains unchanged, so walk durations are recalculated from `Entity.MoveSpeed` (default 120 px/s), including the A/B/C paths, Level 4 nine-second recording, held-console routes, steam valve routes, and PAD crossing.
- Level 4 rooms use 15s Echo lifetime. Capture caps are set per room to the shortest cap that covers the longest required recorded action sequence, no less than 9s on Level 4. Echo lifetime is increased only where measured post-scale traversal plus required interaction time exceeds 15s.
- Follow-up ease adjustment: Level 3 skybridge panels use a 2.5-second solid window on the unchanged 4-second cycle, adding 0.5 seconds of crossing cushion. Panel count and phase offsets remain unchanged.
- Follow-up access adjustment: the lower-walkway pressure plate and extra timed hole were removed; the lower walkway is safe and the checkpoint sits there. After reaching it, falls from the bridge or stairway return Aionia to the checkpoint. The east corridor connector has solid side walls. Atrium jump airtime is 1.5 s.
- The post-jump landing step now holds its Echo stain for 8 s (the other two stair steps remain at 3 s) to provide enough time to get across after the jump.
- Fractional affection is stored as signed tenths in the existing integer counters and JSON saves: PDF values such as +1.2 and -0.5 are stored as +12 and -5 units. All existing integer choices are multiplied by ten at the same time; display/debug and route threshold comparisons convert from tenths. This avoids changing save value types and preserves precision.

## Phase 1-5 implementation sequence

1. Implement world scaling and a smoothed, clamped `Camera2D` owned by `Room`; make small rooms center inside the viewport. Keep HUD/dialogue/pause/CG/record bar in CanvasLayers.
2. Add deterministic held-interact capture/replay and reusable puzzle primitives, all periodic hazards reading the shared `LoopClock`.
3. Built the three room scripts/scenes from the blueprint transforms and connected `RoomAtrium -> RoomMedical -> RoomObservation -> RoomBusiness`, leaving Level 6's `NextLevelPath` empty.
4. Added PDF-derived dialogue registrations, choice branches, flags, objectives, and F4-F6 fragment rewards in `DialogueManager.BuildDemoContent()`; updated README and this plan with chosen capture windows.
5. `dotnet build` succeeds with no warnings or errors. Scene resource paths and exported scene values were inspected textually; Godot editor validation and playtesting remain to be done.
