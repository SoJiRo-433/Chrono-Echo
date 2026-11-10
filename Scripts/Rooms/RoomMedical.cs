using System.Collections.Generic;
using Godot;

/// <summary>Level 4: medical quarantine corridor, triage room, diagnostic terminal and side route.
///
/// The corridor is the only way north and it is sealed on both sides for its whole length,
/// with the keypad and the pressure plate in the alcoves the blueprint draws. Past the
/// shutter the triage room leads east into the connector and down to the hall, whose east
/// end is the sealed steel gate; the side route past the hall is the way to the Level 5
/// exit. Nothing here can be walked around.</summary>
public partial class RoomMedical : Room
{
    private Switch _plate;
    private Door _shutter;
    private Switch _generator;
    private StasisField _stasis;
    private SequenceTerminal _terminal;
    private Inspectable _case;
    private Inspectable _adellaine;
    private Inspectable _adellaineHall, _yorukaHall;
    private readonly List<Inspectable> _examines = new();
    private bool _triageStarted, _waitUpPlayed;

    public override void _Ready()
    {
        BuildMap();
        RoomSize = new Vector2(667, 747);
        PlayerStartPosition = new Vector2(148, 620);
        ExitPosition = new Vector2(463, 527);
        ExitSize = new Vector2(42, 74);
        LevelNumber = 4;
        NextLevelPath = "res://Scenes/Rooms/Room05Observation.tscn";
        MaxCaptureSeconds = 9f;
        EchoLifetimeSeconds = 15f;
        LoopReplay = false;
        ExitLockedHint = "STILL SHUT - READ THE SCAN, TAKE THE SEALED CASE, READ THE GATE SIGN";
        base._Ready();
    }

    private void BuildMap()
    {
        // --- the quarantine corridor: walled for its whole length, keypad niche west,
        // plate niche east, and the shutter as the only way north ---
        Floor("Quarantine corridor", new Vector2(102, 188), new Vector2(92, 416), new Color(0.82f, 0.83f, 0.85f));
        Wall("Corridor west wall upper", new Vector2(94, 32), new Vector2(8, 268));
        Wall("Corridor west wall lower", new Vector2(94, 360), new Vector2(8, 387));
        Wall("Corridor east wall upper", new Vector2(194, 188), new Vector2(8, 318));
        Wall("Corridor east wall lower", new Vector2(194, 554), new Vector2(8, 193));

        Floor("Keypad niche", new Vector2(56, 294), new Vector2(44, 72), new Color(0.78f, 0.79f, 0.82f));
        Wall("Keypad niche north", new Vector2(56, 288), new Vector2(44, 8));
        Wall("Keypad niche south", new Vector2(56, 364), new Vector2(44, 8));
        Wall("Keypad niche west", new Vector2(50, 294), new Vector2(8, 74));

        Floor("Plate niche", new Vector2(194, 500), new Vector2(50, 60), new Color(0.78f, 0.79f, 0.82f));
        Wall("Plate niche north", new Vector2(194, 494), new Vector2(50, 8));
        Wall("Plate niche south", new Vector2(194, 558), new Vector2(50, 8));
        Wall("Plate niche east", new Vector2(240, 500), new Vector2(8, 64));

        Add(new KeypadPanel { Code = "4173", FlagToSet = "medical_keypad_open" }, "Keypad", new Vector2(72, 330));
        _shutter = Add(new Door { Size = new Vector2(92, 12), RequiresSwitch = true, NeedsAllLinkedSwitches = true, OpenedFlag = "medical_shutter_open" }, "QuarantineShutter", new Vector2(102, 183));
        _plate = Add(new Switch { IsPressurePlate = true, PlateRadius = 25f }, "PressurePlate", new Vector2(218, 528));
        _plate.LinkDoor(_shutter);
        Add(new ScannerSweep
        {
            Corridor = new Rect2(102, 188, 92, 416),
            ShadowCenters = new[] { new Vector2(124, 252), new Vector2(168, 322), new Vector2(124, 420), new Vector2(164, 472) },
            RespawnPoint = new Vector2(148, 620)
        }, "ScannerSweep", Vector2.Zero);

        // --- the triage room, and the connector east that is the only way on ---
        Floor("Triage room", new Vector2(102, 32), new Vector2(321, 157), new Color(0.87f, 0.88f, 0.89f));
        Wall("Triage north wall", new Vector2(94, 26), new Vector2(337, 8));
        Wall("Triage east wall", new Vector2(423, 32), new Vector2(8, 157));
        Wall("Triage south wall", new Vector2(202, 189), new Vector2(140, 8));
        Floor("Triage connector", new Vector2(342, 189), new Vector2(81, 213), new Color(0.84f, 0.85f, 0.87f));
        Wall("Connector west wall", new Vector2(336, 189), new Vector2(8, 273));
        Wall("Connector east wall", new Vector2(423, 189), new Vector2(8, 213));

        _generator = Add(new Switch { RequiredHoldSeconds = 0f, HoldActivationSeconds = 15f, RequireHeldInteraction = true, FlagToSet = "medical_generator_active" }, "Generator", new Vector2(139, 38));
        // Everything in this room keeps at least one clear body-length from everything else, so E
        // always lands on the thing the player is standing in front of. The terminal used to sit
        // against the notes and the whiteboard, which meant the nearest-interactable pick never
        // chose it - and reading the scan is one of the three things this level is completed on.
        _stasis = Add(new StasisField { Radius = 34f, GeneratorPath = new NodePath("../Generator") }, "StasisField", new Vector2(360, 141));
        _case = Add(new Inspectable { Caption = "SEALED CASE", Size = new Vector2(54, 42), FlagToSet = "medical_case_taken", RequiredFlag = "medical_generator_active" }, "SealedCase", new Vector2(333, 120));
        Add(new CoolantPuddleRow { Start = new Vector2(404, 51), Spacing = 26f }, "CoolantPuddles", Vector2.Zero);
        // The terminal draws its own caption now, so the room does not need one on top of it.
        _terminal = Add(new SequenceTerminal { FlagToSet = "F4_scan", ContinueDialogueId = "level4_diagnostic_terminal" }, "DiagnosticTerminal", new Vector2(352, 58));
        _terminal.Hide();

        // The four things worth examining: notes and whiteboard on the north wall either side of the
        // terminal's column, the cot and the supply cabinet out on the floor below them.
        _examines.Add(Add(new Inspectable { Caption = "STASIS COT", Size = new Vector2(46, 30), FirstDialogueId = "level4_cot", FlagToSet = "medical_exam_cot" }, "StasisCot", new Vector2(196, 110)));
        _examines.Add(Add(new Inspectable { Caption = "SUPPLY CABINET", Size = new Vector2(46, 30), FirstDialogueId = "level4_cabinet", FlagToSet = "medical_exam_cabinet" }, "SupplyCabinet", new Vector2(296, 110)));
        _examines.Add(Add(new Inspectable { Caption = "NOTES", Size = new Vector2(42, 30), FirstDialogueId = "level4_notes", FlagToSet = "medical_exam_notes" }, "AdellaineNotes", new Vector2(196, 44)));
        _examines.Add(Add(new Inspectable { Caption = "WHITEBOARD", Size = new Vector2(46, 30), FirstDialogueId = "level4_whiteboard", FlagToSet = "medical_exam_whiteboard" }, "Whiteboard", new Vector2(256, 44)));
        _adellaine = Add(new Inspectable { Caption = "ADELLAINE", Size = new Vector2(40, 40), FirstDialogueId = "level4_triage_encounter" }, "Adellaine", new Vector2(110, 148));
        _adellaine.Hide();
        Add(new Inspectable { Caption = "YORUKA", Size = new Vector2(40, 40), FirstDialogueId = "level4_yoruka" }, "Yoruka", new Vector2(170, 148));
        Add(new ScanMinigame { ContinueDialogueId = "level4_triage_scan_result" }, "ScanMinigame", new Vector2(250, 166));

        // --- the hall, the sealed gate at its east end, and the side route south ---
        Floor("Business hall", new Vector2(342, 402), new Vector2(281, 60), new Color(0.76f, 0.78f, 0.81f));
        Wall("Hall north wall", new Vector2(431, 396), new Vector2(192, 8));
        Wall("Hall east gate", new Vector2(623, 402), new Vector2(8, 60));
        Wall("Hall south west", new Vector2(344, 462), new Vector2(139, 8));
        Wall("Hall south east", new Vector2(523, 462), new Vector2(100, 8));
        Floor("Side route", new Vector2(483, 463), new Vector2(40, 80), new Color(0.82f, 0.83f, 0.85f));
        Wall("Side route west", new Vector2(473, 470), new Vector2(8, 77));
        Wall("Side route east", new Vector2(523, 470), new Vector2(8, 77));

        Add(new Inspectable { Caption = "BUSINESS GATE", Size = new Vector2(70, 24), FirstDialogueId = "level4_business_gate", FlagToSet = "medical_route_open" }, "BusinessGate", new Vector2(424, 403));
        _yorukaHall = Add(new Inspectable { Caption = "YORUKA", Size = new Vector2(38, 38), FirstDialogueId = "level4_yoruka", FlagToSet = "medical_yoruka_talked" }, "YorukaHall", new Vector2(404, 426));
        _adellaineHall = Add(new Inspectable { Caption = "ADELLAINE", Size = new Vector2(38, 38), FirstDialogueId = "level4_adellaine_walk", FlagToSet = "medical_adellaine_talked", RequiredFlag = "adellaine_joined" }, "AdellaineHall", new Vector2(444, 426));
        _adellaineHall.Hide();
        Add(new FloorMarker { Caption = "START - QUARANTINE CORRIDOR", Size = new Vector2(180, 26) }, "StartMarker", new Vector2(106, 630));
        Add(new FloorMarker { Caption = "EXIT - OBSERVATION WING", Size = new Vector2(170, 26), Outline = new Color(0.3f, 0.5f, 1f) }, "ExitMarker", new Vector2(468, 610));
    }

    public override float MovementScaleAt(Vector2 worldPosition) => _stasis != null && _stasis.Contains(worldPosition) ? 0.2f : 1f;

    protected override bool CanCompleteRoom() => GameStateManager.Instance?.GetFlag("medical_route_open") == true
        && GameStateManager.Instance?.GetFlag("F4_scan") == true
        && GameStateManager.Instance?.GetFlag("medical_case_taken") == true;

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        var state = GameStateManager.Instance;
        if (_shutter != null && _plate != null)
        {
            bool unlocked = state?.GetFlag("medical_keypad_open") == true;
            if (unlocked && _plate.IsActivated) _shutter.Open(); else _shutter.Close();
        }
        if (!_triageStarted && _shutter?.IsOpen == true && Player.WorldPosition.Y < 188)
        {
            _triageStarted = true; _adellaine?.Show(); Dialogue?.StartDialogue("level4_triage_encounter");
        }
        int examined = 0; foreach (Inspectable item in _examines) if (item.HasBeenExamined) examined++;
        if (examined >= 3 && _terminal != null) _terminal.Show();
        if (state?.GetFlag("adellaine_joined") == true) _adellaineHall?.Show();
        if (!_waitUpPlayed && _yorukaHall?.HasBeenExamined == true && _adellaineHall?.HasBeenExamined == true
            && Dialogue?.IsActive != true && Dialogue?.StartDialogue("level4_wait_up") == true)
            _waitUpPlayed = true;
    }

    protected override void ResetPuzzles()
    {
        base.ResetPuzzles(); _triageStarted = false; _waitUpPlayed = false;
        foreach (Inspectable item in _examines) item.Show();
        _terminal?.Hide();
    }

    private T Add<T>(T node, string name, Vector2 position) where T : Node2D
    { node.Name = name; node.Position = position; AddChild(node); return node; }
    private void Wall(string name, Vector2 position, Vector2 size) => Add(new Obstacle { Size = size }, name, position);
    private void Floor(string name, Vector2 position, Vector2 size, Color color) => Add(new FloorMarker { Size = size, Caption = "", Fill = color, Outline = new Color(color, 0.8f) }, name, position);
}
