using System.Collections.Generic;
using Godot;

/// <summary>Level 5: glass-tile lock, central pane, laser security room and the three-section
/// maintenance conduit.
///
/// The level's art is its own map (<c>Assets/level5_observation_map.png</c>), 640x360, drawn at
/// twice its own resolution - the same whole-number upscale the Atrium and the Medical Facility
/// use. One unit in this file is one pixel of the map, so every rectangle below can be read
/// against the sheet.
///
/// The map's enclosures are real here: the entry glass hall opens into the pane through a gate in
/// the gallery divider, the pane's west door leads into the security room, and the conduit leaves
/// that room's south end and snakes to the Level 6 exit in three lanes joined by two shafts - so
/// the steam, the fans and the beams all have to be crossed rather than walked around. Each lane
/// carries one valve at its end, one vent and one fan.</summary>
public partial class RoomObservation : Room
{
    /// <summary>Map pixels to world units. See the class comment.</summary>
    private const float MapScale = 2f;

    private readonly List<CrackingGlassTile> _glass = new();
    private readonly List<ValveWheel> _valves = new();
    private Door _galleryGate;
    private Switch _consoleA, _consoleB, _consoleC;
    private Inspectable _felixAtWindow;
    private Inspectable _felixAtDoor;
    private bool _felixMoved;
    private LaserGrid _lasers;
    private FloorMarker _conduitTrigger;
    private float _aActivatedAt = -1f;
    private float _bActivatedAt = -1f, _cActivatedAt = -1f;
    private bool _bWasActive, _cWasActive;
    private bool _windowDialoguePlayed;
    private bool _footageDialoguePlayed;
    private bool _conduitIntroPlayed;

    public override void _Ready()
    {
        BuildMap();
        RoomSize = S(640f, 360f);
        WorldSize = S(640f, 360f);
        PlayerStartPosition = P(462f, 319f);
        ExitPosition = P(242f, 326f); ExitSize = S(20f, 20f);
        LevelNumber = 5; NextLevelPath = "res://Scenes/Rooms/Room06Business.tscn";
        MaxCaptureSeconds = 8f; EchoLifetimeSeconds = 15f; LoopReplay = false;
        ExitLockedHint = "STILL SHUT - PRESS THE SECURITY CONSOLE, THEN SHUT ALL THREE STEAM VALVES";
        base._Ready();
        if (Dialogue != null) Dialogue.DialogueEnded += OnDialogueEnded;
    }

    public override void _ExitTree() { if (Dialogue != null) Dialogue.DialogueEnded -= OnDialogueEnded; }

    private void BuildMap()
    {
        Backdrop();

        // --- the entry glass hall: the cracked floor under the A/B/C consoles ---
        Floor("Entry glass hall", new Vector2(280, 194), new Vector2(350, 137));
        Wall("Hall west wall", new Vector2(274, 194), new Vector2(6, 137));
        Wall("Hall east wall", new Vector2(630, 194), new Vector2(6, 137));
        Wall("Hall south wall", new Vector2(280, 331), new Vector2(350, 6));

        // The gallery divider. The sheet draws its door/gate in this wall, above the middle of the
        // glass and between the two dashed columns, so the divider is solid either side of that
        // gate and the gate itself is the only way from the entry hall into the observation gallery.
        // It is the door Scene 1 is about: the A/B/C rhythm is what opens it.
        Wall("Gallery divider west", new Vector2(280, 186), new Vector2(156, 8));
        Wall("Gallery divider east", new Vector2(480, 186), new Vector2(150, 8));
        _galleryGate = Add(new Door { Size = S(44f, 8f), RequiresSwitch = true, ApproachDialogueId = "level5_gate_sealed" }, "GalleryGate", P(436f, 186f));

        for (int row = 0; row < 4; row++)
            for (int col = 0; col < 7; col++)
                _glass.Add(Add(new CrackingGlassTile { Size = S(48f, 33f) }, $"Glass_{row}_{col}", P(288f + 49f * col, 214f + 34f * row)));
        _consoleA = Add(new Switch { StaysOn = false, RequiredHoldSeconds = 0f, RequireHeldInteraction = true }, "ConsoleA", P(320f, 200f));
        _consoleB = Add(new Switch { StaysOn = true }, "ConsoleB", P(540f, 200f));
        _consoleC = Add(new Switch { StaysOn = true }, "ConsoleC", P(578f, 200f));

        // --- the central observation pane ---
        Floor("Observation pane", new Vector2(280, 10), new Vector2(350, 177));
        Wall("Pane north wall", new Vector2(274, 4), new Vector2(362, 6));
        Wall("Pane east wall", new Vector2(630, 10), new Vector2(6, 177));
        Wall("Pane west wall north", new Vector2(274, 10), new Vector2(6, 80));
        Wall("Pane west wall south", new Vector2(274, 125), new Vector2(6, 69));

        _felixAtWindow = Add(new Inspectable { Caption = "FELIX", Size = S(22f, 22f), DrawBox = false, FirstDialogueId = "level5_felix_window",
            CharacterSheetPath = CharacterSprite.FrontSpritePath(GameStateManager.Felix) }, "Felix", P(455f, 80f));
        _felixAtWindow.Hide();

        // Scene 3's tail, and the sheet's second F: once the footage has been read he is waiting at
        // the security room's door rather than at the window. He stands clear of the doorway itself,
        // because the corridor he would otherwise be standing in is the only way between the gallery
        // and this room - and an Inspectable is not solid unless a room says so, so he never blocks.
        _felixAtDoor = Add(new Inspectable { Caption = "FELIX", Size = S(20f, 20f), DrawBox = false, FirstDialogueId = "level5_felix_door",
            CharacterSheetPath = CharacterSprite.FrontSpritePath(GameStateManager.Felix) }, "FelixAtDoor", P(240f, 100f));
        _felixAtDoor.Hide();

        // --- the door between the pane and the security room (matches the drawn walkway x=198..280, y=95..125) ---
        Floor("Security door corridor", new Vector2(198, 95), new Vector2(82, 30));
        Wall("Door corridor north", new Vector2(196, 89), new Vector2(84, 6));
        Wall("Door corridor south", new Vector2(196, 125), new Vector2(84, 6));

        // --- the security room: one way in, the beams are the way through (x=14..196, y=8..226) ---
        Floor("Security room", new Vector2(14, 8), new Vector2(182, 218));
        Wall("Security north wall", new Vector2(8, 2), new Vector2(194, 6));
        Wall("Security west wall", new Vector2(8, 8), new Vector2(6, 224));
        Wall("Security east wall north", new Vector2(196, 8), new Vector2(6, 87));
        Wall("Security east wall south", new Vector2(196, 125), new Vector2(6, 107));
        // The seam between the security room and the conduit: solid either side of the floor hatch
        // opening (x=148..176) she drops through into the first conduit lane.
        Wall("Security south wall west", new Vector2(8, 226), new Vector2(140, 6));
        Wall("Security south wall east", new Vector2(176, 226), new Vector2(26, 6));

        _lasers = Add(new LaserGrid { Bounds = new Rect2(P(20f, 52f), S(172f, 152f)), Spacing = 58f }, "LaserGrid", Vector2.Zero);
        // The console only asks for the footage; the fragment itself is handed over at the end of
        // the conversation it starts, so the unlock lands with the beat and not before it.
        Add(new Switch { StaysOn = true, FlagToSet = "level5_console_pressed" }, "SecurityConsole", P(80f, 32f));

        // --- the maintenance conduit: three lanes, two shafts, one valve per lane ---
        BuildConduit();
        Add(new FloorMarker { Caption = "START - OBSERVATION WING", Size = S(90f, 12f), DrawBox = false }, "StartMarker", P(462f, 320f));
        Add(new FloorMarker { Caption = "EXIT - BUSINESS", Size = S(85f, 13f), DrawBox = false, Outline = new Color(0.3f, 0.5f, 1f) }, "ExitMarker", P(200f, 336f));
    }

    /// <summary>The sheet's serpentine aligned to the drawn conduit walkways (y=232..346):
    /// lane one runs east, drops down the east shaft, lane two runs west, drops down the west
    /// shaft, lane three runs east to the exit.</summary>
    private void BuildConduit()
    {
        Floor("Conduit lane one", new Vector2(50, 232), new Vector2(212, 22));
        Floor("Conduit east shaft", new Vector2(240, 254), new Vector2(22, 28));
        Floor("Conduit lane two", new Vector2(24, 282), new Vector2(238, 22));
        Floor("Conduit west shaft", new Vector2(24, 304), new Vector2(26, 20));
        Floor("Conduit lane three", new Vector2(24, 324), new Vector2(238, 22));
        Floor("Level 6 exit", new Vector2(242, 326), new Vector2(20, 20));

        // Outer shell around the drawn conduit walkways.
        Wall("Conduit lane1 north east", new Vector2(202, 226), new Vector2(66, 6));
        Wall("Conduit lane1 west wall", new Vector2(44, 232), new Vector2(6, 22));
        Wall("Conduit lane2 north west", new Vector2(18, 276), new Vector2(32, 6));
        Wall("Conduit west wall", new Vector2(18, 282), new Vector2(6, 68));
        Wall("Conduit east wall", new Vector2(262, 232), new Vector2(6, 118));
        Wall("Conduit south wall", new Vector2(18, 346), new Vector2(250, 6));

        // The two solid dividers between the drawn lanes: divider one stops at x240 to leave the
        // east shaft open (x240..262), divider two starts at x50 to leave the west shaft open (x24..50).
        Wall("Conduit divider one", new Vector2(44, 254), new Vector2(196, 28));
        Wall("Conduit divider two", new Vector2(50, 304), new Vector2(218, 20));

        // A valve, vent and fan aligned to each drawn conduit lane.
        AddValveSection(1, new Vector2(66, 243), new Vector2(198, 243), new Vector2(250, 243));
        AddValveSection(2, new Vector2(248, 268), new Vector2(110, 293), new Vector2(40, 293));
        AddValveSection(3, new Vector2(38, 314), new Vector2(188, 335), new Vector2(225, 335));

        _conduitTrigger = Add(new FloorMarker { Caption = "", Size = S(60f, 26f), Fill = new Color(0.4f, 0.85f, 0.9f, 0.04f), Outline = new Color(0.5f, 0.9f, 0.95f, 0.15f) }, "ConduitTrigger", P(132f, 228f));
    }

    /// <summary>One lane's trio: the valve wheel (held for six seconds, suppressing the lane for
    /// fifteen), the steam vent it governs and the fan at the junction.</summary>
    private void AddValveSection(int number, Vector2 valve, Vector2 vent, Vector2 fan)
    {
        ValveWheel wheel = Add(new ValveWheel { Name = $"Valve{number}", PlateRadius = 20f, FlagToSet = $"steam_{number}_shut", PersistActivation = true }, $"Valve{number}", P(valve.X, valve.Y));
        _valves.Add(wheel);
        Add(new SteamVent { ValvePath = new NodePath($"../Valve{number}") }, $"SteamVent{number}", P(vent.X, vent.Y));
        Add(new GapFan { FlagToSet = $"fan_{number}_stopped" }, $"Fan{number}", P(fan.X, fan.Y));
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        // The rhythm counts only in order, and B and C latch on when they are pressed. A console
        // pressed out of turn would sit there lit and never give the sequence the rising edge it is
        // waiting for, so the lock could never be finished at all. It lets go again instead, and she
        // can walk back and retry the order.
        if (_consoleB?.IsActivated == true && _aActivatedAt < 0f) _consoleB.ResetState();
        if (_consoleC?.IsActivated == true && _bActivatedAt < 0f) _consoleC.ResetState();
        if (_consoleA?.IsActivated == true && _aActivatedAt < 0f) _aActivatedAt = LoopClock;
        if (_consoleB?.IsActivated == true && !_bWasActive && _aActivatedAt >= 0f) _bActivatedAt = LoopClock;
        if (_consoleC?.IsActivated == true && !_cWasActive && _bActivatedAt >= 0f) _cActivatedAt = LoopClock;
        _bWasActive = _consoleB?.IsActivated == true; _cWasActive = _consoleC?.IsActivated == true;
        // A missed six-second window is a failed attempt, not a softlock. Clear the latched
        // consoles and timing state together so the player can replay the Echo sequence.
        if (_aActivatedAt >= 0f && LoopClock - _aActivatedAt > 6f && GameStateManager.Instance?.GetFlag("observation_lock_open") != true)
        {
            ResetConsoleSequence();
        }
        bool sequence = _aActivatedAt >= 0f && _bActivatedAt >= _aActivatedAt && _cActivatedAt >= _bActivatedAt
            && _cActivatedAt - _aActivatedAt <= 6f && _consoleA?.IsActivated == true && _consoleB?.IsActivated == true && _consoleC?.IsActivated == true;
        if (sequence) GameStateManager.Instance?.SetFlag("observation_lock_open", true);

        // The door Scene 1 is about. The rhythm is the only thing that answers it, so the gate is
        // opened from here rather than from a plate or a console link.
        if (GameStateManager.Instance?.GetFlag("observation_lock_open") == true) _galleryGate?.Open();
        if (GameStateManager.Instance?.GetFlag("observation_lock_open") == true && !_windowDialoguePlayed)
        { _windowDialoguePlayed = true; _felixAtWindow?.Show(); }

        // The footage read, he moves from the window to the door: "review the footage for Fragment
        // F5, then talk to Felix at the door".
        if (GameStateManager.Instance?.GetFlag("F5_footage") == true && !_felixMoved)
        { _felixMoved = true; _felixAtWindow?.Hide(); _felixAtDoor?.Show(); }
        if (GameStateManager.Instance?.GetFlag("level5_console_pressed") == true && !_footageDialoguePlayed)
        { _footageDialoguePlayed = true; Dialogue?.StartDialogue("level5_security_footage"); }
        CheckConduitIntro();
        CheckGlassFall();
    }

    private void ResetConsoleSequence()
    {
        _consoleA?.ResetState();
        _consoleB?.ResetState();
        _consoleC?.ResetState();
        _aActivatedAt = _bActivatedAt = _cActivatedAt = -1f;
        _bWasActive = _cWasActive = false;
    }

    /// <summary>Scene 4 opens with the conduit's objective and the six-second hold, which the
    /// design doc puts in an Objective and an Instructions line. It fires when she reaches the
    /// conduit's mouth, so she reads it before the first lane rather than never.</summary>
    private void CheckConduitIntro()
    {
        if (_conduitIntroPlayed || _conduitTrigger == null) return;

        Player player = Player;
        if (player == null || !player.IsAlive) return;
        if (!_conduitTrigger.Bounds.Intersects(player.Bounds)) return;

        _conduitIntroPlayed = true;
        Dialogue?.StartDialogue("level5_conduit");
    }

    private void CheckGlassFall()
    {
        if (Player == null || Player.IsAirborne) return;
        foreach (CrackingGlassTile tile in _glass)
        {
            // The same rule the tile counts a step by: what drops her is the tile she is standing
            // on, not any shattered tile her 28 px body happens to graze on the way past it.
            if (tile.IsShattered && tile.Bounds.HasPoint(Player.WorldPosition))
            { Player.TakeScratch(); Player.WorldPosition = PlayerStartPosition; ResetPuzzles(); return; }
        }
    }

    protected override bool CanCompleteRoom() => GameStateManager.Instance?.GetFlag("F5_footage") == true
        && GameStateManager.Instance?.GetFlag("steam_1_shut") == true
        && GameStateManager.Instance?.GetFlag("steam_2_shut") == true
        && GameStateManager.Instance?.GetFlag("steam_3_shut") == true;
    protected override void ResetPuzzles()
    {
        base.ResetPuzzles(); _aActivatedAt = _bActivatedAt = _cActivatedAt = -1f; _bWasActive = _cWasActive = false; _windowDialoguePlayed = false; _footageDialoguePlayed = false; _conduitIntroPlayed = false; _felixMoved = false; _felixAtDoor?.Hide();
        foreach (CrackingGlassTile tile in _glass) tile.ResetState();
    }
    private void OnDialogueEnded(string dialogueId)
    {
    }
    private T Add<T>(T node, string name, Vector2 position) where T : Node2D { node.Name = name; node.Position = position; AddChild(node); return node; }

    /// <summary>A point of the map - one map pixel is one unit here, so this is where the level's
    /// geometry is read against the sheet.</summary>
    private static Vector2 P(float x, float y) => new(x * MapScale, y * MapScale);

    /// <summary>A size off the map.</summary>
    private static Vector2 S(float width, float height) => new(width * MapScale, height * MapScale);

    /// <summary>A wall, a shelf or a piece of furniture as the map drew it: a shape that blocks,
    /// with nothing of its own drawn - the map is the drawing.</summary>
    private void Wall(string name, Vector2 position, Vector2 size) =>
        Add(new Obstacle { Size = S(size.X, size.Y), DrawBox = false }, name, P(position.X, position.Y));

    /// <summary>The floor a room stands on. The map paints it, so this is the room's bounds and
    /// its caption only.</summary>
    private void Floor(string name, Vector2 position, Vector2 size) =>
        Add(new FloorMarker { Size = S(size.X, size.Y), Caption = "", DrawBox = false }, name, P(position.X, position.Y));

    /// <summary>The painting under everything. It is the first child, so every overlay - the gate,
    /// the consoles, the captions - lands on top of it.</summary>
    private void Backdrop()
    {
        var map = ResourceLoader.Load<Texture2D>("res://Assets/level5_observation_map.png");
        if (map == null)
        {
            GD.PushWarning("RoomObservation: the map image is missing - the level has no art.");
            return;
        }

        Add(new Sprite2D
        {
            Texture = map,
            Centered = false,
            Scale = Vector2.One * MapScale,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest   // the map is drawn in pixels
        }, "Backdrop", Vector2.Zero);
    }
}
