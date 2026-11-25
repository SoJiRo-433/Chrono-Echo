using Godot;
using System.Collections.Generic;

/// <summary>Level 8 follows the supplied research-facility map and its five PDF puzzle beats.</summary>
public partial class Room08Facility : Room
{
    private const float MapScale = 2f;
    private const string MapPath = "res://Assets/level8_time_research_map.png";
    private Switch _craneLever, _farPad, _crystalOne, _crystalTwo, _crystalThree;
    private Door _archiveDoor, _balconyDoor, _sectorDoor;
    private FacilityKeypad _keypad;
    private FloorMarker _archiveFile;
    private float _lastArchiveJump, _crateClock, _resonanceHold;
    private bool _resonanceSolved;
    private readonly Queue<(float time, Vector2 pos)> _studentPath = new();
    private float _rewindCooldown;

    public override void _Ready()
    {
        BuildMap();
        RoomSize = WorldSize = new Vector2(1280, 720);
        PlayerStartPosition = P(28, 24);
        ExitPosition = P(610, 322); ExitSize = S(18, 34);
        LevelNumber = 8; NextLevelPath = "res://Scenes/Rooms/Room09SectorAlpha.tscn"; MaxEchoes = 2;
        MaxCaptureSeconds = 15; EchoLifetimeSeconds = 90; LoopReplay = false;
        IntroDialogueId = "level8_intro"; ClearDialogueId = "level8_clear";
        CompletionFlag = "level8_complete"; ExitLockedHint = "THE RESONANCE RECORDING AND TRUST CHECK ARE INCOMPLETE";
        base._Ready();
        Dialogue?.SetObjective("Explore the classroom hallway.");
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        if (GameStateManager.Instance?.IsPaused == true) return;
        if (Player != null && Player.IsAlive)
        {
            _studentPath.Enqueue((LoopClock, Player.WorldPosition));
            while (_studentPath.Count > 0 && LoopClock - _studentPath.Peek().time > 4f) _studentPath.Dequeue();
            _rewindCooldown = Mathf.Max(0, _rewindCooldown - (float)delta);
            Vector2 student = P(300 + Mathf.Sin(LoopClock * 0.55f) * 125f, 82 + Mathf.Sin(LoopClock * 0.37f) * 8f);
            if (_rewindCooldown <= 0 && Player.WorldPosition.DistanceTo(student) < 20f)
            {
                Vector2 rewind = Player.WorldPosition;
                foreach (var point in _studentPath) if (point.time >= LoopClock - 3f) { rewind = point.pos; break; }
                Player.WorldPosition = rewind;
                _rewindCooldown = 2f;
            }
        }
        _crateClock += (float)delta;
        if (LoopClock - _lastArchiveJump >= 7f)
        {
            _lastArchiveJump = LoopClock;
            if (_archiveFile != null) _archiveFile.Position = P(336 + (int)(LoopClock / 7f) % 3 * 45, 153 + (int)(LoopClock / 7f) % 2 * 18);
        }
        if (GameStateManager.Instance?.GetFlag("level8_keypad_open") == true) _archiveDoor?.Open();
        if (_crateClock >= 6f)
        {
            _crateClock = 0;
            if (Player != null && Player.IsAlive && new Rect2(P(248, 228), S(86, 42)).Intersects(Player.Bounds))
            { Player.TakeScratch(); Player.WorldPosition = P(24, 258); }
        }
        if (!_resonanceSolved && EchoOn(_crystalOne) && EchoOn(_crystalTwo) && PlayerOn(_crystalThree))
        {
            _resonanceHold += (float)delta;
            if (_resonanceHold >= 0.8f)
            {
                _resonanceSolved = true;
                GameStateManager.Instance?.SetFlag("F9_resonance", true);
                GameStateManager.Instance?.SetFlag("level8_resonance_solved", true);
                Dialogue?.StartDialogue("level8_resonance");
                Dialogue?.SetObjective("Complete the security trust check.");
            }
        }
        else if (!_resonanceSolved) _resonanceHold = 0;

        if (EchoOn(_craneLever) && EchoOn(_farPad)) _balconyDoor?.Open();
        if (AllTrustResponses()) _sectorDoor?.Open();
    }

    public override float MovementScaleAt(Vector2 pos)
    {
        foreach (Vector2 crystal in new[] { P(160, 330), P(230, 305), P(290, 335) })
        {
            float radius = Mathf.PosMod(LoopClock, 3f) * 11f;
            if (Mathf.Abs(pos.DistanceTo(crystal) - radius) < 18f) return 0.45f;
        }
        return 1f;
    }

    protected override bool CanCompleteRoom() => GameStateManager.Instance?.GetFlag("F9_resonance") == true && AllTrustResponses();

    private void BuildMap()
    {
        var texture = ResourceLoader.Load<Texture2D>(MapPath);
        if (texture != null) Add(new Sprite2D { Texture = texture, Centered = false, Scale = Vector2.One * MapScale,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest }, "FacilityBackdrop", Vector2.Zero);

        // Walkable bands align to the three classrooms, archive, catwalk, balcony and chamber.
        Floor(12, 8, 476, 36); Floor(14, 46, 444, 75); Floor(100, 124, 354, 94);
        Floor(18, 226, 326, 70); Floor(430, 204, 196, 146); Floor(110, 292, 226, 64);
        _keypad = Add(new FacilityKeypad { Position = P(423, 188) }, "ArchiveKeypad", P(423, 188));
        Add(new Inspectable { Caption = "WHITEBOARD (E)", Size = S(30, 18), DrawBox = false, FirstDialogueId = "level8_whiteboard", RepeatDialogueId = "level8_whiteboard" }, "Whiteboard", P(140, 139));
        Add(new Inspectable { Caption = "MEETING MINUTES (E)", Size = S(34, 18), DrawBox = false, FirstDialogueId = "level8_minutes", RepeatDialogueId = "level8_minutes" }, "Minutes", P(205, 139));
        Add(new Inspectable { Caption = "FACULTY DESK (E)", Size = S(30, 18), DrawBox = false, FirstDialogueId = "level8_desk", RepeatDialogueId = "level8_desk" }, "FacultyDesk", P(140, 180));
        Add(new Inspectable { Caption = "PERSONAL LOCKER (E)", Size = S(30, 18), DrawBox = false, FirstDialogueId = "level8_locker", RepeatDialogueId = "level8_locker" }, "Locker", P(205, 180));
        Add(new Inspectable { Caption = "ENVELOPE TO FELIX (E)", Size = S(24, 18), DrawBox = false, FirstDialogueId = "level8_envelope", RepeatDialogueId = "level8_envelope" }, "Envelope", P(270, 184));
        _archiveFile = Add(new FloorMarker { Caption = "SHIFTING ARCHIVE FILE", Size = S(22, 12), DottedRing = true,
            Outline = new Color(0.68f, 0.48f, 1f) }, "ArchiveFile", P(336, 153));
        _archiveDoor = Add(new Door { Size = S(8, 22), RequiresSwitch = false }, "ArchiveExit", P(448, 205));

        Add(new Inspectable { Caption = "CHALKBOARD (E)", Size = S(32, 18), DrawBox = false, FirstDialogueId = "level8_chalkboard", RepeatDialogueId = "level8_chalkboard" }, "Chalkboard", P(236, 105));
        Add(new Inspectable { Caption = "YORUKA'S ECHO (E)", Size = S(28, 28), DrawBox = false, FirstDialogueId = "level8_yoruka", RepeatDialogueId = "level8_yoruka", CharacterSheetPath = CharacterSprite.FrontSpritePath(GameStateManager.Yoruka) }, "YorukaEcho", P(85, 82));
        Add(new FloorMarker { Caption = "AIONIA'S ECHO", Size = S(26, 22), DottedRing = true }, "AioniaEcho", P(375, 83));
        Add(new Inspectable { Caption = "CLASSROOM MEMORY (E)", Size = S(22, 18), DrawBox = false, FirstDialogueId = "level8_classroom", RepeatDialogueId = "level8_classroom" }, "ClassroomMemory", P(375, 83));

        _craneLever = Add(new Switch { IsPressurePlate = true, PlateRadius = 20 }, "CraneLever", P(306, 264));
        _farPad = Add(new Switch { IsPressurePlate = true, PlateRadius = 20 }, "FarPad", P(485, 260));
        Add(new FloorMarker { Caption = "CRANE BRAKE - ECHO 1", Size = S(56, 12), DottedRing = true }, "LeverMarker", P(286, 250));
        Add(new FloorMarker { Caption = "FAR PAD - ECHO 2", Size = S(48, 12), DottedRing = true }, "FarPadMarker", P(462, 247));
        _balconyDoor = Add(new Door { Size = S(10, 34), RequiresSwitch = false }, "BalconyDoor", P(423, 260));
        Add(new FloorMarker { Caption = "CRATE DROP ZONE", Size = S(84, 40), Fill = new Color(1, .2f, .15f, .16f), Outline = new Color(1, .35f, .2f, .65f) }, "CrateZone", P(248, 228));

        _crystalOne = Crystal("CrystalOne", 160, 330); _crystalTwo = Crystal("CrystalTwo", 230, 305); _crystalThree = Crystal("CrystalThree", 290, 335);
        Add(new FloorMarker { Caption = "CHRONAL RESONANCE CHAMBER", Size = S(140, 14), DrawBox = false }, "ChamberLabel", P(139, 350));

        Add(new Inspectable { Caption = "HESIOD / TRUST PAD (E)", Size = S(28, 24), DrawBox = false, RequiredFlag = "F9_resonance", FirstDialogueId = "level8_hesiod", RepeatDialogueId = "level8_hesiod", CharacterSheetPath = CharacterSprite.FrontSpritePath(GameStateManager.Hesiod) }, "HesiodTrust", P(470, 310));
        Add(new Inspectable { Caption = "ADELLAINE / TRUST PAD (E)", Size = S(28, 24), DrawBox = false, RequiredFlag = "F9_resonance", FirstDialogueId = "level8_adellaine", RepeatDialogueId = "level8_adellaine", CharacterSheetPath = CharacterSprite.FrontSpritePath(GameStateManager.Adellaine) }, "AdellaineTrust", P(515, 310));
        Add(new Inspectable { Caption = "YORUKA / TRUST PAD (E)", Size = S(28, 24), DrawBox = false, RequiredFlag = "F9_resonance", FirstDialogueId = "level8_yoruka_trust", RepeatDialogueId = "level8_yoruka_trust", CharacterSheetPath = CharacterSprite.FrontSpritePath(GameStateManager.Yoruka) }, "YorukaTrust", P(560, 310));
        _sectorDoor = Add(new Door { Size = S(18, 34), RequiresSwitch = false }, "SectorAlphaDoor", P(610, 322));
        Add(new FloorMarker { Caption = "SECTOR ALPHA", Size = S(54, 14), DrawBox = false }, "SectorLabel", P(580, 345));
    }

    private Switch Crystal(string name, float x, float y)
    {
        Add(new FloorMarker { Caption = "TUNE", Size = S(30, 30), DottedRing = true, Outline = new Color(.3f, .72f, 1f) }, name + "Marker", P(x - 15, y - 15));
        return Add(new Switch { IsPressurePlate = true, PlateRadius = 17 }, name, P(x, y));
    }
    private bool EchoOn(Switch pad)
    {
        if (pad == null) return false;
        foreach (Entity e in Entities) if (e is Echo && e.IsAlive && e.Visible && e.WorldPosition.DistanceTo(pad.Position) <= pad.PlateRadius) return true;
        return false;
    }
    private bool PlayerOn(Switch pad) => pad != null && Player != null && Player.IsAlive && Player.WorldPosition.DistanceTo(pad.Position) <= pad.PlateRadius;
    private bool AllTrustResponses() => GameStateManager.Instance?.GetFlag("level8_hesiod_trusted") == true && GameStateManager.Instance?.GetFlag("level8_adellaine_trusted") == true && GameStateManager.Instance?.GetFlag("level8_yoruka_trusted") == true;
    private T Add<T>(T node, string name, Vector2 pos) where T : Node2D { node.Name = name; node.Position = pos; AddChild(node); return node; }
    private void Floor(float x, float y, float w, float h) => Add(new FloorMarker { Size = S(w,h), DrawBox = false }, "Walkable" + GetChildCount(), P(x,y));
    private static Vector2 P(float x, float y) => new(x * MapScale, y * MapScale);
    private static Vector2 S(float x, float y) => new(x * MapScale, y * MapScale);
}
