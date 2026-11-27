using System.Collections.Generic;
using Godot;

/// <summary>Level 3: the Atrium. The level's art is its own map
/// (<c>Assets/level3_atrium_map.png</c>), 640x360, drawn at twice its resolution: the observation
/// deck across the top, the broken stairway coming down the west, the glitched terminal under it,
/// the glass skybridge across the middle with the lower walkway directly beneath it, and the entry
/// hall at the bottom left with the corridor running east out of it. One unit in this file is one
/// pixel of the map, so every rectangle below can be read against the sheet.
///
/// Every wall, and every part of the map that is not a room, is collision only
/// (<see cref="Obstacle.DrawBox"/> clear) - the map is the drawing. What the map cannot draw is the
/// machinery, so the overlays are the six blue tiles, the six skybridge panels, the three stair
/// steps and the jump line between them, the plates and their dotted stand spots, the three doors
/// and the captions.
///
/// The five beats are the ones the level has always had, out of the same primitives the other
/// levels use:
///
///   1. entry hall      - FluctuatingTile blue tiles (deadly) and a Door with CloseDelaySeconds = 5
///   2. glass skybridge - FluctuatingTile platforms (GapWhenAbsent), a checkpoint on the lower
///                        walkway, and the timed "atrium_bridge" conversation
///   3. terminal        - two consoles that have to be live at the same moment AND show the same
///                        four-symbol sequence
///   4. stairway        - TimeStainStep, which only an Echo can make solid, plus one missing step
///                        that has to be jumped
///   5. observation deck- three marked spots, the third held while the player stands on the second
///
/// Everything a tile does on its own it does on its own; this class owns only the rules that span
/// several nodes, plus the falls and the exit door.</summary>
public partial class RoomAtrium : Room
{
    #region Exports
    [Export] public string TerminalDialogueId { get; set; } = "atrium_terminal";
    [Export] public string TerminalRevealDialogueId { get; set; } = "atrium_terminal_reveal";

    /// <summary>The one part of the Atrium that wants a *repeating* projection: the stairway, whose
    /// stains last 3 s, so the climb has to be walked again and again for the player to follow it up.
    /// Set from the map in BuildMap.</summary>
    public Rect2 StairwayLoopZone { get; private set; } = new Rect2(320f, 152f, 112f, 136f);
    #endregion

    #region Private state
    // Half the width of her body: how much of her has to be over a hole before the floor takes
    // her. CheckFalls explains why the bridge measures her feet rather than her whole body.
    private const float FallFootprintRadius = 7f;

    private const string MapPath = "res://Assets/level3_atrium_map.png";

    /// <summary>Map pixels to world units, so the whole level is drawn at twice the map's
    /// resolution: whole pixels stay square, and the walkways are wide enough to walk.</summary>
    private const float MapScale = 2f;

    private Door _entryGate;
    private Door _stairwayGate;
    private Door _exitDoor;
    private Switch _entrancePlate;
    private Switch _consoleA;
    private Switch _consoleB;
    private Switch _windowSpotA;
    private Switch _windowSpotB;
    private Switch _windowSpotC;
    private FloorMarker _bridgeTrigger;
    private FloorMarker _terminalTrigger;
    private FloorMarker _jumpGap;
    private FloorMarker _checkpointMarker;
    private FloorMarker _corridorTrigger;

    private readonly List<TimeStainStep> _stairSteps = new();
    private readonly List<FluctuatingTile> _bridgeTiles = new();

    private Vector2 _bridgeFallRespawn;
    private Vector2 _stairwayFallRespawn;
    private Rect2 _stairwayTalkZone;

    private bool _windowPlayed;
    private bool _bridgePlayed;
    private bool _corridorObjectiveSet;
    private bool _terminalIntroPlayed;
    private bool _terminalRevealPlayed;
    private bool _stairwayTalkPlayed;
    private bool _checkpointReached;
    private float _jumpGapGraceRemaining;
    #endregion

    /// <summary>Spot 1 is the one the reflections need first, and it is only ever stood on once, so
    /// the plate records a flag instead of having to be held like the other two.</summary>
    public const string WindowSpotOneFlag = "atrium_spot_one";

    #region Lifecycle
    public override void _Ready()
    {
        BuildMap();

        // The map is the world: 640x360 pixels, drawn at MapScale.
        RoomSize = S(640f, 360f);
        // The start is in the UPPER lane of the entry hall, not down the middle of it. The hall's
        // two ranks of blue tiles leave only eight map pixels of standing room in a lane, and the
        // middle of the hall is not a lane at all: a body there overlaps both ranks, so the only
        // window in which the player is clear of a tile is the half-second when one rank of each
        // side happens to be gone - far too short to cross. In the upper lane the first rank's
        // tiles are gone for two whole seconds out of every four, which is what the crossing needs.
        PlayerStartPosition = P(26f, 307f);
        ExitPosition = P(606f, 20f);
        ExitSize = S(28f, 50f);
        LevelNumber = 3;
        NextLevelPath = "res://Scenes/Rooms/Room04Medical.tscn";
        MaxEchoes = 3;
        MaxCaptureSeconds = 5f;
        LoopReplay = true;              // narrowed per projection by OnEchoProjected
        EchoLifetimeSeconds = 25f;
        IntroDialogueId = "atrium_intro";
        ClearDialogueId = "";
        CompletionFlag = "atrium_complete";

        base._Ready();

        // The Atrium's leap has a little more airtime than the base movement tutorial value so
        // the recorded jump has room for a frame of input latency at the broken stair.
        if (Player != null) Player.JumpAirtimeSeconds = 1.5f;

        DialogueManager dialogue = Dialogue;
        if (dialogue != null) dialogue.DialogueEnded += OnAtriumDialogueEnded;
    }

    public override void _ExitTree()
    {
        DialogueManager dialogue = Dialogue;
        if (dialogue != null) dialogue.DialogueEnded -= OnAtriumDialogueEnded;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        if (GameStateManager.Instance != null && GameStateManager.Instance.IsPaused) return;

        CheckTerminalIntro();
        CheckGlitchedTerminal();
        CheckTerminalReveal();
        CheckBridgeCollapse();
        CheckAtriumCheckpoint();
        CheckStairwayTalk();
        CheckFalls((float)delta);
        CheckObservationWindow();
        CheckCorridorObjective();
    }

    /// <summary>Narrows the room's exported `LoopReplay` down to the stairway. The doc's Level 3
    /// instruction is that an Echo repeats your recording, and on the stairs it must - the stains
    /// only last 3 s. Anywhere else a repeating window is actively wrong, because the rewind walks
    /// the projection off the plate it is supposed to be holding: on a plate the Atrium wants what
    /// every earlier level taught, an Echo that plays its window once and then stands where the
    /// recording ended.</summary>
    protected override void OnEchoProjected(Echo echo)
    {
        echo.LoopReplay = StairwayLoopZone.HasPoint(echo.WorldPosition);
    }

    /// <summary>R-restart has to clear what this room remembered for itself: the one-shot beats
    /// would otherwise never fire again, and the stairway would keep the last climb's stains.
    /// The consoles go back to their starting codes with the switches, which the base class does.</summary>
    protected override void ResetPuzzles()
    {
        base.ResetPuzzles();

        _bridgePlayed = false;
        _windowPlayed = false;
        _corridorObjectiveSet = false;
        _terminalIntroPlayed = false;
        _terminalRevealPlayed = false;
        _stairwayTalkPlayed = false;
        _checkpointReached = false;
        _jumpGapGraceRemaining = 0f;

        foreach (TimeStainStep step in _stairSteps) step.ResetState();
        GameStateManager.Instance?.SetFlag(WindowSpotOneFlag, false);   // spot 1 must be marked again
        GameStateManager.Instance?.SetFlag("atrium_checkpoint", false);
    }
    #endregion

    #region The map (map pixels, read off the sheet)
    private void BuildMap()
    {
        Backdrop();
        BuildEntryHall();
        BuildLowerWalkway();
        BuildSkybridge();
        BuildTerminal();
        BuildStairway();
        BuildObservationDeck();
    }

    /// <summary>The painting under everything: the first child, so every overlay lands on top.</summary>
    private void Backdrop()
    {
        var map = ResourceLoader.Load<Texture2D>(MapPath);
        if (map == null)
        {
            GD.PushWarning("RoomAtrium: " + MapPath + " is missing - the level has no art.");
            return;
        }

        Add(new Sprite2D
        {
            Texture = map,
            Centered = false,
            Scale = Vector2.One * MapScale,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        }, "Backdrop", Vector2.Zero);
    }

    /// <summary>Scene 1. The entry hall is the bottom left of the map with the corridor running east
    /// out of it. Six blue tiles stand across it in two rows, so the only way to the gate is through
    /// them - timed to when each one is gone, with the lanes between the columns as the places to
    /// wait. The gate is timed: its plate sits at the far end of the hall, so the Echo is what holds
    /// it down while Aionia crosses.</summary>
    private void BuildEntryHall()
    {
        // The hall's floor and the corridor's floor are not the same shape: the map paints the
        // hall down to y 298 and the corridor down to y 314, with a step between them at x 176.
        Floor("Entry hall", new Vector2(8, 292), new Vector2(182, 60), new Color(0.30f, 0.31f, 0.35f));
        Floor("Corridor", new Vector2(190, 314), new Vector2(330, 38), new Color(0.30f, 0.31f, 0.35f));
        Wall("Entry west wall", new Vector2(2, 292), new Vector2(6, 60));
        Wall("Entry south wall", new Vector2(8, 352), new Vector2(512, 6));
        Wall("Corridor east wall", new Vector2(520, 292), new Vector2(6, 60));

        // The map paints the corridor's floor as the band from y 326 down; north of that, east of
        // the gate, there is nothing painted at all: the dark outside the building. So that whole
        // side is walled off, and the corridor is the painted band and nothing else. The wall runs
        // from the gate to the far end of the corridor, where the way up is - so the corridor has
        // one way on and no side passage to walk past. It starts just past the gate, because the
        // hall's floor runs higher than the corridor's and the player steps down between the last
        // tile and the gate - where the gate itself tells her she has to.
        Wall("Corridor north wall", new Vector2(190, 286), new Vector2(286, 40));

        // The gate is set into a wall across the corridor, so the hall really is the only way on.
        Wall("Gate wall", new Vector2(184, 292), new Vector2(6, 16));
        _entryGate = Add(new Door
        {
            Size = S(6f, 44f),
            OpenedFlag = "atrium_gate_opened",
            CloseDelaySeconds = 5f
        }, "Gate", P(184f, 308f));

        // The plate sits in the same lane as the start, so the player presses it on her way east.
        // It used to sit on the middle line, fifteen map pixels lower: from the lane she now
        // starts in that plate could never be reached at all, and the gate with it.
        _entrancePlate = Add(new Switch { IsPressurePlate = true, PlateRadius = 26f }, "EntrancePlate", P(48f, 307f));
        _entrancePlate.LinkDoor(_entryGate);

        // Just past the gate: the point where the objective changes from the entry hall to the
        // way up. Nothing is drawn here - it is a place, not a sign.
        _corridorTrigger = Add(new FloorMarker
        {
            Size = S(60f, 24f),
            DrawBox = false
        }, "CorridorTrigger", P(192f, 326f));

        // Two rows of three, the rows offset so a lane through one is covered by the next. The
        // eight map pixels between the rows are narrower than the player, so the gap is not a
        // lane of its own: she has to cross in one row or the other, and the only lane that can be
        // crossed is the upper one. The field sits west of where it used to, which leaves her
        // room between the last column and the step down onto the corridor's lower floor.
        float[] columns = { 60f, 98f, 136f };
        float[] phases = { 0f, 0.5f, 1f, 1.5f, 2f, 2.5f };
        for (int i = 0; i < 3; i++)
            BlueTile($"BlueTile{i + 1}", new Vector2(columns[i], 296f), phases[i]);
        for (int i = 0; i < 3; i++)
            BlueTile($"BlueTile{i + 4}", new Vector2(columns[i], 326f), phases[i + 3]);

        // The start sign sits in the gap between the two rows, right under where she starts.
        Caption("StartMarker", "START - THE ATRIUM", new Vector2(10, 318), new Vector2(120, 14));
    }

    private void BlueTile(string name, Vector2 position, float phase) =>
        Add(new FluctuatingTile
        {
            Size = S(18f, 22f),
            PeriodSeconds = 4f,
            // A tile is there for a second and a half of every four. Two seconds left the
            // window for starting the crossing a quarter of a second wide, and left the lower
            // row's first column with half a second - which is not enough to walk across even
            // one tile, so stepping down into the lower row was a way to get stuck for good.
            SolidSeconds = 1.5f,
            PhaseSeconds = phase,
            Deadly = true,
            ContactDamage = 3
        }, name, P(position.X, position.Y));

    /// <summary>The lower walkway: the band under the skybridge, reached from the corridor through
    /// the two gaps in the wall between them. It is where the checkpoint sits, and where a fall from
    /// the bridge puts her back once she has reached it.</summary>
    private void BuildLowerWalkway()
    {
        Floor("Lower walkway", new Vector2(88, 256), new Vector2(432, 30), new Color(0.16f, 0.18f, 0.22f));
        Wall("Walkway west wall", new Vector2(82, 256), new Vector2(6, 30));
        Wall("Walkway east wall", new Vector2(520, 256), new Vector2(6, 30));
        // This piece runs from the corridor's own west wall, not from the walkway's edge: the band
        // west of the walkway would otherwise be open from the corridor into the dark outside.
        // It stops at the gate; from there to the far end of the corridor the corridor's own north
        // wall takes over, and it is open only at the very end - the way up to the skybridge.
        Wall("Walkway south wall west", new Vector2(2, 286), new Vector2(188, 6));

        // The way up, at the end of the corridor: the doorway drawn round the opening, because the
        // map shows a lit threshold and nothing more, and a dark band below it reads as a wall.
        Add(new FloorMarker
        {
            Size = S(44f, 28f),
            Caption = "UP - THE BRIDGE",
            Fill = new Color(0.45f, 0.6f, 0.85f, 0.05f),
            Outline = new Color(0.55f, 0.7f, 0.95f, 0.7f),
            Dashed = true
        }, "WalkwayOpening", P(476f, 286f));

        _checkpointMarker = Add(new FloorMarker
        {
            Size = S(60f, 30f),
            Caption = "CHECKPOINT",
            Fill = new Color(0.35f, 0.8f, 0.65f, 0.18f),
            Outline = new Color(0.45f, 1f, 0.7f, 0.9f),
            Dashed = true
        }, "LowerWalkwayCheckpoint", P(290f, 256f));

        _bridgeFallRespawn = P(212f, 322f);   // just past the entry gate, so a fall is a walk back
    }

    /// <summary>Scene 2. Six panels of floor that are there for 2.5 s of every 4 and gone the rest,
    /// phased west to east so the crossing ripples the way the level sends her - in from the
    /// walkway, west towards the terminal. Stepping onto the bridge starts the timed conversation.</summary>
    private void BuildSkybridge()
    {
        // Six panels of floor that are there for part of every 12.3 seconds and gone the rest,
        // phased so that the wave of floor travels WEST, the way the level sends her - in from
        // the walkway, west towards the terminal.
        //
        // The wave has to travel no faster than she can walk, or it outruns her and leaves her
        // standing over a hole. She walks 40 map pixels a second and a panel is 77 wide, so she
        // needs 1.93 seconds to cross one: the wave may therefore only advance 1.95 seconds per
        // panel. With the old one-second steps it moved at seventy-seven pixels a second, nearly
        // twice her speed, and the west half of the bridge could not be crossed at all - the
        // panel she was standing on always went first.
        const float WaveStepSeconds = 1.95f;
        const float BridgePeriodSeconds = 12.3f;
        const float BridgeSolidSeconds = 9.75f;
        for (int i = 0; i < 6; i++)
        {
            float phase = Mathf.PosMod(1f + (3 - i) * WaveStepSeconds, BridgePeriodSeconds);
            _bridgeTiles.Add(Add(new FluctuatingTile
            {
                Size = S(77f, 34f),
                PeriodSeconds = BridgePeriodSeconds,
                SolidSeconds = BridgeSolidSeconds,
                PhaseSeconds = phase,
                GapWhenAbsent = true
            }, $"SkyPanel{i + 1}", P(88f + i * 77f, 216f)));
        }

        // The north wall runs to the map's east edge, and the south wall to the bridge's own east
        // wall: without that, the two corners of the bridge open onto the dark outside the rooms.
        Wall("Bridge north wall", new Vector2(230, 210), new Vector2(410, 6));
        Wall("Bridge west wall", new Vector2(82, 216), new Vector2(6, 40));
        Wall("Bridge east wall", new Vector2(550, 216), new Vector2(6, 40));
        Wall("Bridge south wall west", new Vector2(88, 250), new Vector2(212, 6));     // the way onto the
        Wall("Bridge south wall east", new Vector2(380, 250), new Vector2(176, 6));    // bridge is between

        _bridgeTrigger = Add(new FloorMarker
        {
            Size = S(462f, 34f),
            Fill = new Color(0.45f, 0.6f, 0.85f, 0.05f),
            Outline = new Color(0.55f, 0.7f, 0.95f, 0.2f)
        }, "BridgeTrigger", P(88f, 216f));

        // The map paints the bridge's south rail as one unbroken line, so on its own it says
        // nothing about where the glass can be stepped onto. The level draws its own doorway
        // over the opening in the wall, the way it draws the jump gap and the checkpoint.
        Add(new FloorMarker
        {
            Size = S(80f, 16f),
            Caption = "ONTO THE SKYBRIDGE",
            Fill = new Color(0.45f, 0.6f, 0.85f, 0.06f),
            Outline = new Color(0.55f, 0.7f, 0.95f, 0.75f),
            Dashed = true
        }, "BridgeOpening", P(300f, 248f));
    }

    /// <summary>Scene 3. The terminal room at the bottom of the stairway, with the two consoles the
    /// sheet draws as its two lit screens. Walking in introduces the puzzle; the stairway gate above
    /// opens when the codes match or when Yoruka decodes it.</summary>
    private void BuildTerminal()
    {
        Floor("Terminal", new Vector2(80, 144), new Vector2(150, 66), new Color(0.34f, 0.35f, 0.38f));
        Wall("Terminal west wall", new Vector2(74, 144), new Vector2(6, 66));
        Wall("Terminal east wall", new Vector2(230, 144), new Vector2(6, 66));
        Wall("Terminal north wall west", new Vector2(80, 138), new Vector2(80, 6));    // the stairway is
        Wall("Terminal north wall east", new Vector2(216, 138), new Vector2(14, 6));   // between these
        Wall("Terminal south wall west", new Vector2(80, 210), new Vector2(50, 6));    // the way onto the
        Wall("Terminal south wall east", new Vector2(190, 210), new Vector2(40, 6));   // bridge between

        _consoleA = Add(new Switch
        {
            IsPressurePlate = true,
            PlateRadius = 24f,
            SequencePool = new[] { "#@*+", "@*+#", "*+#@", "+#@*" },
            SequenceIndex = 0
        }, "ConsoleA", P(110f, 186f));

        _consoleB = Add(new Switch
        {
            IsPressurePlate = true,
            PlateRadius = 24f,
            SequencePool = new[] { "#@*+", "@*+#", "*+#@", "+#@*" },
            SequenceIndex = 1
        }, "ConsoleB", P(170f, 186f));

        _terminalTrigger = Add(new FloorMarker
        {
            Size = S(150f, 54f),
            Fill = new Color(0.4f, 0.85f, 0.9f, 0.05f),
            Outline = new Color(0.5f, 0.9f, 0.95f, 0.2f)
        }, "TerminalTrigger", P(80f, 150f));
    }

    /// <summary>Scene 4. The stairway: three steps that are holes until an Echo stains them, and one
    /// missing step between the first and the second that no stain can cover - it has to be jumped.
    /// Stepping into the tube is what plays the family-rumour conversation.</summary>
    private void BuildStairway()
    {
        Wall("Stair west wall", new Vector2(154, 76), new Vector2(6, 68));
        Wall("Stair east wall", new Vector2(216, 76), new Vector2(6, 68));
        Caption("StairMarker", "BROKEN STAIRWAY", new Vector2(160, 62), new Vector2(90, 12));

        _stairSteps.Add(Add(new TimeStainStep { Size = S(56f, 20f), StainSeconds = 3f },
                            "Step1", P(160f, 118f)));

        _jumpGap = Add(new FloorMarker
        {
            Size = S(56f, 3f),
            Caption = "JUMP (Space + W/Up)",
            Fill = new Color(0.03f, 0.02f, 0.06f, 0.75f),
            Outline = new Color(0.95f, 0.85f, 0.4f, 0.8f),
            Dashed = true
        }, "JumpGap", P(160f, 112f));

        // Each step the Echo touches turns solid for three seconds.
        _stairSteps.Add(Add(new TimeStainStep { Size = S(56f, 20f), StainSeconds = 3f },
                            "Step2", P(160f, 92f)));

        _stairSteps.Add(Add(new TimeStainStep { Size = S(56f, 16f), StainSeconds = 3f },
                            "Step3", P(160f, 76f)));

        _stairwayGate = Add(new Door
        {
            Size = S(56f, 6f),
            OpenedFlag = "atrium_stairway_opened",
            LatchOpen = true
        }, "StairwayGate", P(160f, 138f));

        StairwayLoopZone = new Rect2(P(160f, 76f), S(56f, 68f));
        _stairwayTalkZone = new Rect2(P(160f, 144f), S(56f, 24f));
        _stairwayFallRespawn = P(188f, 156f);   // the foot of the stairs
    }

    /// <summary>Scene 5. Three marked spots on the deck: the first marks itself, the third has to be
    /// held while Aionia stands on the second. When they line up the window shows its fragment, and
    /// the heavy door on the deck's east wall unlocks - which is where Level 4 begins.</summary>
    private void BuildObservationDeck()
    {
        Floor("Observation deck", new Vector2(104, 16), new Vector2(496, 54), new Color(0.42f, 0.45f, 0.50f));
        Wall("Deck north wall", new Vector2(98, 10), new Vector2(508, 6));
        Wall("Deck west wall", new Vector2(98, 16), new Vector2(6, 54));
        Wall("Deck south wall west", new Vector2(104, 70), new Vector2(56, 6));    // the stairway comes
        Wall("Deck south wall east", new Vector2(216, 70), new Vector2(384, 6));   // up between these

        _windowSpotA = WindowSpot("WindowSpotA", 200f, true);
        _windowSpotB = WindowSpot("WindowSpotB", 330f, false);
        _windowSpotC = WindowSpot("WindowSpotC", 460f, false);
        SpotRing("SpotMarker1", 200f);
        SpotRing("SpotMarker2", 330f);
        SpotRing("SpotMarker3", 460f);

        // The way on: a wall across the deck's east end with the heavy door set into it, and the
        // Level 4 threshold beyond.
        Wall("Exit wall top", new Vector2(600, 16), new Vector2(6, 8));
        _exitDoor = Add(new Door { Size = S(6f, 27f), OpenedFlag = "atrium_exit_opened" },
                        "ExitDoor", P(600f, 24f));
        Wall("Exit wall bottom", new Vector2(600, 51), new Vector2(6, 19));
        Floor("Exit threshold", new Vector2(606, 16), new Vector2(28, 54), new Color(0.5f, 0.55f, 0.62f));
        Wall("Exit threshold north", new Vector2(606, 10), new Vector2(28, 6));
        Wall("Exit threshold south", new Vector2(606, 70), new Vector2(28, 6));
        Wall("Exit threshold east", new Vector2(634, 10), new Vector2(6, 66));
        Caption("ExitMarker", "EXIT - LEVEL 4", new Vector2(608, 2), new Vector2(90, 14));
    }

    private Switch WindowSpot(string name, float x, bool marksItself) =>
        Add(new Switch
        {
            IsPressurePlate = true,
            PlateRadius = 20f,
            FlagToSet = marksItself ? WindowSpotOneFlag : ""
        }, name, P(x, 44f));

    private void SpotRing(string name, float x) =>
        Add(new FloorMarker
        {
            Size = S(24f, 24f),
            Outline = new Color(0.55f, 0.8f, 1f, 0.85f),
            DottedRing = true
        }, name, P(x - 12f, 32f));
    #endregion

    #region Scene 2 - the bridge comes apart
    /// <summary>The first time she sets foot on the skybridge it starts collapsing, and the timed
    /// choice plays: the bridge does not wait for her to decide.</summary>
    private void CheckBridgeCollapse()
    {
        if (_bridgePlayed || _bridgeTrigger == null) return;

        Player player = Player;
        if (player == null || !player.IsAlive) return;
        if (!_bridgeTrigger.Bounds.Intersects(player.Bounds)) return;

        _bridgePlayed = true;
        Dialogue?.StartDialogue("atrium_bridge");
    }
    #endregion

    #region Scene 3 - the glitched terminal
    /// <summary>Walking up to the consoles introduces the puzzle: she can let Yoruka decode it or
    /// set the codes herself. It is a zone rather than an interactable so that E, right here, is
    /// free for the consoles that are the puzzle.</summary>
    private void CheckTerminalIntro()
    {
        if (_terminalIntroPlayed || _terminalTrigger == null) return;

        Player player = Player;
        if (player == null || !player.IsAlive) return;
        if (!_terminalTrigger.Bounds.Intersects(player.Bounds)) return;

        _terminalIntroPlayed = true;
        Dialogue?.StartDialogue(TerminalDialogueId);
    }

    /// <summary>Two consoles only count when they are weighed down at the same moment AND show the
    /// same four-symbol sequence, so the player sets the codes with E and the Echo is what holds
    /// one of them down. Yoruka decoding it sets a flag instead and opens the way anyway.</summary>
    private void CheckGlitchedTerminal()
    {
        if (_stairwayGate == null || _stairwayGate.IsOpen) return;

        bool solvedByYoruka = GameStateManager.Instance?.GetFlag("yoruka_solved_terminal") ?? false;
        if (ConsolesMatched() || solvedByYoruka) _stairwayGate.Open();
    }

    /// <summary>True only when both consoles are live and both display the same sequence right now.</summary>
    private bool ConsolesMatched()
    {
        if (_consoleA == null || _consoleB == null) return false;
        if (!_consoleA.IsActivated || !_consoleB.IsActivated) return false;

        string sequence = _consoleA.CurrentSequence;
        return sequence != "" && sequence == _consoleB.CurrentSequence;
    }

    /// <summary>Whichever route opened the stairway gate, the same reveal plays afterwards. Polled
    /// rather than fired from the two places that open it, so "SUBJECT A.A. ANCHOR STATUS: STABLE."
    /// has exactly one copy in the content, and so it waits its turn behind the conversation the
    /// player is already reading.</summary>
    private void CheckTerminalReveal()
    {
        if (_terminalRevealPlayed) return;
        if (_stairwayGate == null || !_stairwayGate.IsOpen) return;
        if (Dialogue != null && Dialogue.IsActive) return;

        _terminalRevealPlayed = true;
        Dialogue?.StartDialogue(TerminalRevealDialogueId);
    }
    #endregion

    #region Scene 4 - the stairway conversation
    /// <summary>The blueprint's "dialogue choice about Yoruka's family rumours on the third landing".
    /// It fires the first time Aionia sets foot in the stair tube, so the beat and its choice land
    /// before the climb rather than never.</summary>
    private void CheckStairwayTalk()
    {
        if (_stairwayTalkPlayed) return;

        Player player = Player;
        if (player == null || !player.IsAlive) return;
        if (!_stairwayTalkZone.Intersects(player.Bounds)) return;

        _stairwayTalkPlayed = true;
        Dialogue?.StartDialogue("atrium_stairway");
    }
    #endregion

    #region Scenes 2 and 4 - the falls
    /// <summary>The skybridge platforms and the stairway steps are both "a floor that is only
    /// sometimes there". Standing on one while it is gone is a fall: a scratch and a walk back to
    /// the start of that stretch, not a life - the puzzle is the timing.
    ///
    /// What counts as "standing on" differs between the two on purpose. A stair step drops her as
    /// soon as her body touches an unstained one, which is what makes the climb need the Echo. A
    /// bridge panel only drops her when the ground she is actually standing on has gone - her own
    /// footprint, not the shoulders that overhang the next panel. Measuring the whole body there
    /// made two safe places count as falls: landing in the middle of a solid panel with an absent
    /// neighbour, and walking the lower walkway under the bridge, whose north strip her body
    /// reaches up into the panels above it.
    ///
    /// The bridge also honours the leap, the same rule the stairway uses: mid-leap she is not
    /// standing on anything, so a jump carries her over a panel that has gone and she only has to
    /// find a solid one on the way down.</summary>
    private void CheckFalls(float delta)
    {
        Player player = Player;
        if (player == null || !player.IsAlive) return;

        Rect2 box = player.Bounds;
        Rect2 footprint = new Rect2(
            player.WorldPosition - new Vector2(FallFootprintRadius, FallFootprintRadius),
            new Vector2(FallFootprintRadius * 2f, FallFootprintRadius * 2f));

        if (!player.IsAirborne)
        {
            foreach (TimeStainStep step in _stairSteps)
                if (!step.IsStained && step.Bounds.Intersects(box)) { Fall(player, _stairwayFallRespawn); return; }

            if (_jumpGap != null && _jumpGap.Bounds.Intersects(box))
            {
                // Room physics runs before Player physics. Give Space one brief window to be
                // processed by Player.Jump before declaring contact with the gap a fall.
                if (_jumpGapGraceRemaining <= 0f) _jumpGapGraceRemaining = 0.15f;
                else
                {
                    _jumpGapGraceRemaining = Mathf.Max(0f, _jumpGapGraceRemaining - delta);
                    if (_jumpGapGraceRemaining <= 0f)
                    {
                        Fall(player, _stairwayFallRespawn);
                        return;
                    }
                }
            }
            else _jumpGapGraceRemaining = 0f;

            foreach (FluctuatingTile tile in _bridgeTiles)
                if (!tile.IsPresent && tile.Bounds.Intersects(footprint)) { Fall(player, _bridgeFallRespawn); return; }
        }
        else _jumpGapGraceRemaining = 0f;
    }

    private void Fall(Player player, Vector2 respawn)
    {
        player.TakeScratch();
        player.WorldPosition = _checkpointReached
            ? _checkpointMarker.Position + _checkpointMarker.Size * 0.5f
            : respawn;
    }

    private void CheckAtriumCheckpoint()
    {
        if (_checkpointReached || Player == null || _checkpointMarker == null) return;
        if (!_checkpointMarker.Bounds.Intersects(Player.Bounds)) return;
        _checkpointReached = true;
        GameStateManager.Instance?.SetFlag("atrium_checkpoint", true);
    }

    /// <summary>Scene 1 ends at the gate, and what comes after it is not obvious: the corridor
    /// runs on east to a dead end, and the way up is in its north wall. The map gives no hint of
    /// it, so the objective changes once she is through the gate and says where the way on is.
    /// </summary>
    private void CheckCorridorObjective()
    {
        if (_corridorObjectiveSet || _corridorTrigger == null || Player == null) return;
        if (!_corridorTrigger.Bounds.Intersects(Player.Bounds)) return;
        _corridorObjectiveSet = true;
        Dialogue?.SetObjective("Find the way up at the end of the corridor.");
    }
    #endregion

    #region Scene 5 - the observation deck
    /// <summary>The three spots have to be stood on so the reflections line up: the first one marks
    /// itself, the third has to be held while the player stands on the second. When they line up
    /// the window shows its fragment, and the heavy door on the deck's east wall unlocks - which is
    /// where Level 4 begins.</summary>
    private void CheckObservationWindow()
    {
        if (_windowPlayed) return;
        if (_windowSpotA == null || _windowSpotB == null || _windowSpotC == null) return;
        if (!(GameStateManager.Instance?.GetFlag(WindowSpotOneFlag) ?? false)) return;   // spot 1 first
        if (!_windowSpotB.IsActivated || !_windowSpotC.IsActivated) return;

        _windowPlayed = true;
        GameStateManager.Instance?.SetFlag("atrium_window_seen", true);   // unlocks Fragment F3
        Dialogue?.StartDialogue("atrium_window");
    }

    private void OnAtriumDialogueEnded(string dialogueId)
    {
        // "OPENS AFTER F3": the way onward only unlocks once the window has been read.
        if (dialogueId == "atrium_window") _exitDoor?.Open();
    }
    #endregion

    #region Overlay helpers
    /// <summary>A point of the map, in world units.</summary>
    private static Vector2 P(float x, float y) => new(x * MapScale, y * MapScale);

    /// <summary>A size off the map.</summary>
    private static Vector2 S(float width, float height) => new(width * MapScale, height * MapScale);

    private T Add<T>(T node, string name, Vector2 position) where T : Node2D
    {
        node.Name = name;
        node.Position = position;
        AddChild(node);
        return node;
    }

    /// <summary>A wall or a piece of the map that is not a room: a shape that blocks, with nothing
    /// of its own drawn. The map decides where the level is.</summary>
    private void Wall(string name, Vector2 position, Vector2 size) =>
        Add(new Obstacle { Size = S(size.X, size.Y), DrawBox = false }, name, P(position.X, position.Y));

    /// <summary>A floor rectangle the map already draws: the marker is the room's bounds and its
    /// caption only, so the art shows through.</summary>
    private void Floor(string name, Vector2 position, Vector2 size, Color colour) =>
        Add(new FloorMarker
        {
            Size = S(size.X, size.Y),
            Caption = "",
            Fill = new Color(colour, 0.0f),      // the map's own floor is the fill
            Outline = new Color(colour, 0.5f),
            DrawBox = false
        }, name, P(position.X, position.Y));

    private void Caption(string name, string caption, Vector2 position, Vector2 size) =>
        Add(new FloorMarker { Caption = caption, Size = S(size.X, size.Y), DrawBox = false },
            name, P(position.X, position.Y));
    #endregion
}
