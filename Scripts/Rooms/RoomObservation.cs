using System.Collections.Generic;
using Godot;

/// <summary>Level 5: glass-tile lock, central pane, laser security room and the three-section
/// maintenance conduit.
///
/// The blueprint's enclosures are real here: the entry hall opens into the pane through a doorway
/// in the gallery divider, the pane's west door leads into the security room, and the conduit
/// leaves that room's south end and snakes to the Level 6 exit in three lanes joined by two
/// shafts - so the steam, the fans and the beams all have to be crossed rather than walked
/// around. Each lane carries one valve at its end, one vent and one fan.</summary>
public partial class RoomObservation : Room
{
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
        RoomSize = new Vector2(667, 747);
        PlayerStartPosition = new Vector2(493, 620);
        ExitPosition = new Vector2(280, 630); ExitSize = new Vector2(56, 70);
        LevelNumber = 5; NextLevelPath = "res://Scenes/Rooms/Room06Business.tscn";
        MaxCaptureSeconds = 8f; EchoLifetimeSeconds = 15f; LoopReplay = false;
        ExitLockedHint = "STILL SHUT - PRESS THE SECURITY CONSOLE, THEN SHUT ALL THREE STEAM VALVES";
        base._Ready();
        if (Dialogue != null) Dialogue.DialogueEnded += OnDialogueEnded;
    }

    public override void _ExitTree() { if (Dialogue != null) Dialogue.DialogueEnded -= OnDialogueEnded; }

    private void BuildMap()
    {
        // --- the entry hall: glass tiles under the A/B/C consoles ---
        Floor("Entry glass hall", new Vector2(342, 474), new Vector2(292, 160), new Color(0.7f, 0.82f, 0.92f));
        Wall("Hall west wall", new Vector2(334, 474), new Vector2(8, 160));
        Wall("Hall east wall", new Vector2(634, 474), new Vector2(8, 160));

        // The gallery divider. The sheet draws its door/gate in this wall, above the middle of the
        // glass and between the two dashed columns, so the divider is solid either side of that
        // gate and the gate itself is the only way from the entry hall into the observation gallery.
        // It is the door Scene 1 is about: the A/B/C rhythm is what opens it.
        Wall("Gallery divider west", new Vector2(254, 470), new Vector2(183, 6));
        Wall("Gallery divider east", new Vector2(481, 470), new Vector2(153, 6));
        _galleryGate = Add(new Door { Size = new Vector2(44, 12), RequiresSwitch = true, ApproachDialogueId = "level5_gate_sealed" }, "GalleryGate", new Vector2(437, 464));

        for (int row = 0; row < 5; row++)
            for (int col = 0; col < 7; col++)
                _glass.Add(Add(new CrackingGlassTile { Size = new Vector2(38, 36) }, $"Glass_{row}_{col}", new Vector2(350 + 39 * col, 496 + 37 * row)));
        _consoleA = Add(new Switch { StaysOn = false, RequiredHoldSeconds = 0f, RequireHeldInteraction = true }, "ConsoleA", new Vector2(377, 478));
        _consoleB = Add(new Switch { StaysOn = true }, "ConsoleB", new Vector2(561, 478));
        _consoleC = Add(new Switch { StaysOn = true }, "ConsoleC", new Vector2(596, 478));

        // --- the central observation pane ---
        Floor("Observation pane", new Vector2(342, 252), new Vector2(292, 222), new Color(0.84f, 0.87f, 0.9f));
        Wall("Pane north wall", new Vector2(336, 246), new Vector2(306, 6));
        Wall("Pane east wall", new Vector2(634, 252), new Vector2(8, 222));
        Wall("Pane west wall north", new Vector2(336, 252), new Vector2(6, 110));
        Wall("Pane west wall south", new Vector2(336, 402), new Vector2(6, 72));

        _felixAtWindow = Add(new Inspectable { Caption = "FELIX", Size = new Vector2(44, 44), FirstDialogueId = "level5_felix_window" }, "Felix", new Vector2(488, 341));
        _felixAtWindow.Hide();

        // Scene 3's tail, and the sheet's second F: once the footage has been read he is waiting at
        // the security room's door rather than at the window. He stands clear of the doorway itself,
        // because the corridor he would otherwise be standing in is the only way between the gallery
        // and this room - and an Inspectable is not solid unless a room says so, so he never blocks.
        _felixAtDoor = Add(new Inspectable { Caption = "FELIX", Size = new Vector2(40, 40), FirstDialogueId = "level5_felix_door" }, "FelixAtDoor", new Vector2(232, 382));
        _felixAtDoor.Hide();

        // --- the door between the pane and the security room ---
        Floor("Security door corridor", new Vector2(260, 362), new Vector2(76, 40), new Color(0.8f, 0.82f, 0.85f));
        Wall("Door corridor north", new Vector2(260, 356), new Vector2(76, 6));
        Wall("Door corridor south", new Vector2(260, 402), new Vector2(76, 6));

        // --- the security room: one way in, the beams are the way through ---
        Floor("Security room", new Vector2(102, 92), new Vector2(152, 391), new Color(0.73f, 0.76f, 0.8f));
        Wall("Security north wall", new Vector2(96, 86), new Vector2(164, 6));
        Wall("Security west wall", new Vector2(96, 92), new Vector2(6, 391));
        Wall("Security east wall north", new Vector2(254, 92), new Vector2(6, 270));
        Wall("Security east wall south", new Vector2(254, 402), new Vector2(6, 81));
        Wall("Security south wall west", new Vector2(96, 483), new Vector2(14, 6));
        Wall("Security south wall east", new Vector2(170, 483), new Vector2(90, 6));

        _lasers = Add(new LaserGrid { Bounds = new Rect2(102, 132, 152, 320), Spacing = 68f }, "LaserGrid", Vector2.Zero);
        // The console only asks for the footage; the fragment itself is handed over at the end of
        // the conversation it starts, so the unlock lands with the beat and not before it.
        Add(new Switch { StaysOn = true, FlagToSet = "level5_console_pressed" }, "SecurityConsole", new Vector2(178, 108));

        // --- the maintenance conduit: three lanes, two shafts, one valve per lane ---
        BuildConduit();
        Add(new FloorMarker { Caption = "START - OBSERVATION WING", Size = new Vector2(180, 24) }, "StartMarker", new Vector2(423, 643));
        Add(new FloorMarker { Caption = "EXIT - BUSINESS DEPARTMENT", Size = new Vector2(190, 24), Outline = new Color(0.3f, 0.5f, 1f) }, "ExitMarker", new Vector2(300, 700));
    }

    /// <summary>The sheet's serpentine: lane one runs east, drops down the east shaft, lane two
    /// runs west, drops down the west shaft, lane three runs east to the exit. Each lane has its
    /// vent, its fan and its valve, and the valve sits at the end the player has to run to.</summary>
    private void BuildConduit()
    {
        var shell = new Color(0.28f, 0.3f, 0.34f);

        Floor("Conduit lane one", new Vector2(100, 500), new Vector2(240, 40), shell);
        Floor("Conduit east shaft", new Vector2(300, 540), new Vector2(40, 20), shell);
        Floor("Conduit lane two", new Vector2(100, 560), new Vector2(240, 40), shell);
        Floor("Conduit west shaft", new Vector2(100, 600), new Vector2(40, 20), shell);
        Floor("Conduit lane three", new Vector2(100, 620), new Vector2(210, 40), shell);
        Floor("Level 6 exit", new Vector2(280, 630), new Vector2(56, 70), new Color(0.5f, 0.55f, 0.62f));

        // outer shell
        Wall("Conduit north west", new Vector2(94, 494), new Vector2(16, 6));
        Wall("Conduit north east", new Vector2(170, 494), new Vector2(175, 6));
        Wall("Conduit west wall", new Vector2(94, 500), new Vector2(6, 166));
        Wall("Conduit east wall", new Vector2(340, 500), new Vector2(6, 247));
        Wall("Conduit south west", new Vector2(100, 660), new Vector2(180, 6));
        Wall("Conduit south east", new Vector2(336, 660), new Vector2(9, 6));
        Wall("Exit south wall", new Vector2(280, 700), new Vector2(66, 6));

        // the two dividers, each leaving its shaft open
        Wall("Conduit divider one", new Vector2(100, 540), new Vector2(200, 6));
        Wall("Conduit divider two", new Vector2(140, 600), new Vector2(200, 6));

        // a lane one valve sits by the entry, lane two and three at the far ends
        AddValveSection(1, new Vector2(120, 520), new Vector2(235, 520), new Vector2(288, 520));
        AddValveSection(2, new Vector2(320, 580), new Vector2(215, 580), new Vector2(128, 580));
        AddValveSection(3, new Vector2(118, 640), new Vector2(210, 640), new Vector2(276, 640));

        _conduitTrigger = Add(new FloorMarker { Caption = "", Size = new Vector2(70, 60), Fill = new Color(0.4f, 0.85f, 0.9f, 0.04f), Outline = new Color(0.5f, 0.9f, 0.95f, 0.15f) }, "ConduitTrigger", new Vector2(105, 495));
    }

    /// <summary>One lane's trio: the valve wheel (held for six seconds, suppressing the lane for
    /// fifteen), the steam vent it governs and the fan at the junction.</summary>
    private void AddValveSection(int number, Vector2 valve, Vector2 vent, Vector2 fan)
    {
        ValveWheel wheel = Add(new ValveWheel { Name = $"Valve{number}", PlateRadius = 20f, FlagToSet = $"steam_{number}_shut" }, $"Valve{number}", valve);
        _valves.Add(wheel);
        Add(new SteamVent { ValvePath = new NodePath($"../Valve{number}") }, $"SteamVent{number}", vent);
        Add(new GapFan(), $"Fan{number}", fan);
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
    private void Wall(string name, Vector2 pos, Vector2 size) => Add(new Obstacle { Size = size }, name, pos);
    private void Floor(string name, Vector2 pos, Vector2 size, Color color) => Add(new FloorMarker { Size = size, Caption = "", Fill = color, Outline = new Color(color, 0.8f) }, name, pos);
}
