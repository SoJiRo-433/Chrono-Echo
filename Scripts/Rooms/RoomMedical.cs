using System.Collections.Generic;
using Godot;

/// <summary>Level 4: medical quarantine corridor, records room, diagnostic terminal and side route.
///
/// The level's art is its own map (<c>Assets/level4_medical_map.png</c>), 640x360, drawn at twice
/// its own resolution - the same whole-number upscale the Atrium uses, so its pixels stay square
/// and there is room for the player's 28-unit body in the aisles and doorways. One unit in this
/// file is one pixel of the map, so every rectangle below can be read against the sheet.
///
/// Nothing in the map moves, so everything the level does is an overlay on top of it: the walls,
/// the shelving and the furniture are <see cref="Obstacle"/>s with their boxes turned off, so the
/// map's own rooms are what she is walking inside; everything she answers to is an
/// <see cref="Inspectable"/> whose caption is the only thing drawn; what the map does not draw is
/// drawn: the shutter, the plates, the scanner and the captions.
///
/// The corridor is the only way north and it is sealed on both sides for its whole length, with
/// the keypad in the alcove the map draws to its west and the pressure plate in the notch to its
/// east. Past the shutter the records room leads east into the connector and down to the hall,
/// whose east end is the sealed steel gate; the side route past the hall is the way to the Level 5
/// exit. Nothing here can be walked around.</summary>
public partial class RoomMedical : Room
{
    /// <summary>Map pixels to world units. See the class comment.</summary>
    private const float MapScale = 2f;

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
        RoomSize = S(640f, 360f);
        WorldSize = S(640f, 360f);
        PlayerStartPosition = P(95f, 335f);
        ExitPosition = P(506f, 325f);
        ExitSize = S(28f, 20f);
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
        Backdrop();

        // --- the quarantine corridor: the map draws it down the west side, with the keypad
        // alcove off it to the west and the plate notch to its east ---
        Floor("Quarantine corridor", new Vector2(60, 118), new Vector2(72, 234));
        Wall("Corridor west wall upper", new Vector2(54, 118), new Vector2(6, 82));
        Wall("Corridor west wall lower", new Vector2(54, 240), new Vector2(6, 112));
        Wall("Corridor east wall upper", new Vector2(132, 118), new Vector2(6, 162));
        Wall("Corridor east wall lower", new Vector2(132, 320), new Vector2(6, 32));
        Wall("Corridor south wall", new Vector2(54, 352), new Vector2(84, 6));

        Floor("Keypad alcove", new Vector2(16, 200), new Vector2(44, 40));
        Wall("Keypad alcove north", new Vector2(10, 194), new Vector2(50, 6));
        Wall("Keypad alcove south", new Vector2(10, 240), new Vector2(50, 6));
        Wall("Keypad alcove west", new Vector2(10, 194), new Vector2(6, 52));

        Floor("Plate notch", new Vector2(132, 280), new Vector2(34, 40));
        Wall("Plate notch north", new Vector2(132, 274), new Vector2(34, 6));
        Wall("Plate notch south", new Vector2(132, 320), new Vector2(34, 6));
        Wall("Plate notch east", new Vector2(160, 274), new Vector2(6, 52));

        // The caption carries the sign Yoruka reads out ("4, ?, 7, 3") - otherwise she is talking
        // about a sign that is not there.
        Add(new KeypadPanel { Code = "4173", FlagToSet = "medical_keypad_open", Caption = "KEYPAD (E) - 4 ? 7 3" }, "Keypad", P(38f, 218f));
        _shutter = Add(new Door { Size = S(72f, 6f), RequiresSwitch = true, NeedsAllLinkedSwitches = true, OpenedFlag = "medical_shutter_open" }, "QuarantineShutter", P(58f, 112f));
        _plate = Add(new Switch { IsPressurePlate = true, PlateRadius = 25f }, "PressurePlate", P(148f, 300f));
        _plate.LinkDoor(_shutter);
        Add(new ScannerSweep
        {
            Corridor = new Rect2(P(60f, 118f), S(72f, 234f)),
            ShadowCenters = new[] { P(80f, 166f), P(115f, 220f), P(85f, 286f), P(115f, 330f) },
            RespawnPoint = P(95f, 335f)
        }, "ScannerSweep", Vector2.Zero);

        // --- the records room, and the connector east that is the only way on ---
        // Every wall runs past the corner it meets, so the rooms are sealed. The space between
        // the rooms is not part of the level: the map leaves it dark, and a gap of a few
        // pixels at a corner was enough for the player to walk out of the building.
        Floor("Records room", new Vector2(30, 12), new Vector2(450, 106));
        Wall("Records north wall", new Vector2(24, 6), new Vector2(462, 6));
        Wall("Records west wall", new Vector2(24, 6), new Vector2(6, 112));
        Wall("Records east wall", new Vector2(480, 6), new Vector2(6, 112));
        Wall("Records south wall west", new Vector2(30, 112), new Vector2(28, 6));
        Wall("Records south wall east", new Vector2(130, 112), new Vector2(284, 6));
        Wall("Records south wall far east", new Vector2(465, 112), new Vector2(15, 6));

        Floor("Triage connector", new Vector2(414, 118), new Vector2(51, 92));
        Wall("Connector west wall", new Vector2(408, 118), new Vector2(6, 92));
        Wall("Connector east wall", new Vector2(465, 118), new Vector2(6, 92));

        // The generator, the stasis field it feeds, the case the field guards, the puddles and the
        // terminal all sit where the map puts them: the field is the blue disc on the room's east
        // side, the puddles are the row of small objects beside it.
        _generator = Add(new Switch { RequiredHoldSeconds = 0f, HoldActivationSeconds = 15f, RequireHeldInteraction = true, FlagToSet = "medical_generator_active" }, "Generator", P(115f, 22f));
        // Everything in this room keeps at least one clear body-length from everything else, so E
        // always lands on the thing the player is standing in front of. The terminal used to sit
        // against the notes and the whiteboard, which meant the nearest-interactable pick never
        // chose it - and reading the scan is one of the three things this level is completed on.
        _stasis = Add(new StasisField { Radius = 40f, GeneratorPath = new NodePath("../Generator") }, "StasisField", P(398f, 44f));
        _case = Add(new Inspectable { Caption = "SEALED CASE", Size = S(26f, 20f), DrawBox = false, FlagToSet = "medical_case_taken", RequiredFlag = "medical_generator_active" }, "SealedCase", P(445f, 102f));
        Add(new CoolantPuddleRow { Start = P(452f, 40f), Spacing = 28f }, "CoolantPuddles", Vector2.Zero);
        // The terminal draws its own caption now, so the room does not need one on top of it.
        _terminal = Add(new SequenceTerminal { FlagToSet = "F4_scan", ContinueDialogueId = "level4_diagnostic_terminal" }, "DiagnosticTerminal", P(420f, 72f));
        _terminal.Hide();

        // The four things worth examining: the cot on the bed at the north-west corner, the notes
        // on the panel along the north wall, the whiteboard on the panel below it, and the supply
        // cabinet out on the floor among the desks.
        _examines.Add(Add(new Inspectable { Caption = "STASIS COT", Size = S(23f, 15f), DrawBox = false, FirstDialogueId = "level4_cot", FlagToSet = "medical_exam_cot" }, "StasisCot", P(65f, 24f)));
        _examines.Add(Add(new Inspectable { Caption = "SUPPLY CABINET", Size = S(23f, 15f), DrawBox = false, FirstDialogueId = "level4_cabinet", FlagToSet = "medical_exam_cabinet" }, "SupplyCabinet", P(135f, 104f)));
        _examines.Add(Add(new Inspectable { Caption = "NOTES", Size = S(21f, 15f), DrawBox = false, FirstDialogueId = "level4_notes", FlagToSet = "medical_exam_notes" }, "AdellaineNotes", P(165f, 13f)));
        _examines.Add(Add(new Inspectable { Caption = "WHITEBOARD", Size = S(23f, 15f), DrawBox = false, FirstDialogueId = "level4_whiteboard", FlagToSet = "medical_exam_whiteboard" }, "Whiteboard", P(75f, 92f)));
        _adellaine = Add(new Inspectable { Caption = "ADELLAINE", Size = S(20f, 20f), DrawBox = false, FirstDialogueId = "level4_triage_encounter", CharacterSheetPath = CharacterSprite.FrontSpritePath(GameStateManager.Adellaine) }, "Adellaine", P(45f, 92f));
        _adellaine.Hide();
        Add(new Inspectable { Caption = "YORUKA", Size = S(20f, 20f), DrawBox = false, FirstDialogueId = "level4_yoruka",
            CharacterSheetPath = CharacterSprite.FrontSpritePath(GameStateManager.Yoruka) }, "Yoruka", P(88f, 92f));
        Add(new ScanMinigame { ContinueDialogueId = "level4_triage_scan_result" }, "ScanMinigame", P(125f, 108f));

        // --- the hall, the sealed gate at its east end, and the side route south ---
        Floor("Business hall", new Vector2(325, 205), new Vector2(295, 81));
        Wall("Hall west wall", new Vector2(319, 205), new Vector2(6, 81));
        Wall("Hall north wall west", new Vector2(325, 199), new Vector2(89, 6));
        Wall("Hall north wall east", new Vector2(465, 199), new Vector2(155, 6));
        Wall("Hall east gate", new Vector2(620, 199), new Vector2(6, 93));
        Wall("Hall south west", new Vector2(325, 286), new Vector2(175, 6));
        Wall("Hall south east", new Vector2(540, 286), new Vector2(80, 6));
        Floor("Side route", new Vector2(500, 286), new Vector2(40, 66));
        Wall("Side route west", new Vector2(494, 286), new Vector2(6, 66));
        Wall("Side route east", new Vector2(540, 286), new Vector2(6, 66));
        Wall("Side route south", new Vector2(494, 352), new Vector2(52, 6));

        // The business gate is the hazard tape the map paints across the hall's east end.
        Add(new Inspectable { Caption = "BUSINESS GATE", Size = S(35f, 12f), DrawBox = false, FirstDialogueId = "level4_business_gate", FlagToSet = "medical_route_open" }, "BusinessGate", P(586f, 240f));
        _yorukaHall = Add(new Inspectable { Caption = "YORUKA", Size = S(19f, 19f), DrawBox = false, FirstDialogueId = "level4_yoruka", FlagToSet = "medical_yoruka_talked",
            CharacterSheetPath = CharacterSprite.FrontSpritePath(GameStateManager.Yoruka) }, "YorukaHall", P(355f, 240f));
        _adellaineHall = Add(new Inspectable { Caption = "ADELLAINE", Size = S(19f, 19f), DrawBox = false, FirstDialogueId = "level4_adellaine_walk", FlagToSet = "medical_adellaine_talked", RequiredFlag = "adellaine_joined", CharacterSheetPath = CharacterSprite.FrontSpritePath(GameStateManager.Adellaine) }, "AdellaineHall", P(390f, 240f));
        _adellaineHall.Hide();
        Add(new FloorMarker { Caption = "START - QUARANTINE", Size = S(72f, 14f), DrawBox = false }, "StartMarker", P(60f, 336f));
        Add(new FloorMarker { Caption = "EXIT - LEVEL 5", Size = S(85f, 13f), DrawBox = false, Outline = new Color(0.3f, 0.5f, 1f) }, "ExitMarker", P(486f, 344f));
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
        if (!_triageStarted && _shutter?.IsOpen == true && Player.WorldPosition.Y < 236)
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

    /// <summary>The painting under everything. It is the first child, so every overlay - the
    /// shutter, the plates, the captions - lands on top of it.</summary>
    private void Backdrop()
    {
        var map = ResourceLoader.Load<Texture2D>("res://Assets/level4_medical_map.png");
        if (map == null)
        {
            GD.PushWarning("RoomMedical: the map image is missing - the level has no art.");
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
