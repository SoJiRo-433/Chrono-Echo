using System.Collections.Generic;
using Godot;

/// <summary>Level 6: lobby with the revolving doors, executive office, audit archive, Atrium
/// overlook and the return beat.
///
/// Every room is walled the way the sheet draws it: the lobby's only opening is the two revolving
/// doors in its partition (the panel's gap is the way through, and a panel hit knocks her back),
/// and the office, the archive and the overlook chain off it through their own connectors. On the
/// overlook a three-by-three patch of time-decayed tiles sits under the PAD, so the Echo holding
/// the pad is what freezes the ground she has to cross.</summary>
public partial class RoomBusiness : Room
{
    private readonly List<TimeDecayTile> _decayTiles = new();
    private Switch _pad;
    private Inspectable _hesiod;
    private bool _returnDialogueStarted;
    private bool _hesiodConversationStarted;
    private bool _officeObjectivePlayed;
    private Inspectable _hesiodAtExit, _yorukaAtExit, _adellaineAtExit;

    // The patch the PAD freezes: three tiles across, three down, with the pad inside its east
    // column. The radius has to reach the far corner or the freeze leaves a tile of the patch alive.
    private const float PadFreezeRadius = 95f;

    public override void _Ready()
    {
        BuildMap();
        RoomSize = new Vector2(667, 747);
        PlayerStartPosition = new Vector2(123, 634);
        ExitPosition = new Vector2(192, 108); ExitSize = new Vector2(72, 50);
        LevelNumber = 6; NextLevelPath = ""; MaxCaptureSeconds = 15f; EchoLifetimeSeconds = 15f; LoopReplay = false;
        ExitLockedHint = "STILL SHUT - TALK TO HESIOD, YORUKA AND ADELLAINE AT THE BORDER";
        GameStateManager.Instance?.SetFlag("felix_left", true);
        base._Ready();
        DialogueManager d = Dialogue;
        if (d != null) d.DialogueEnded += OnDialogueEnded;
    }
    public override void _ExitTree() { if (Dialogue != null) Dialogue.DialogueEnded -= OnDialogueEnded; }

    private void BuildMap()
    {
        // --- the lobby: the partition and its two revolving doors are the only way through ---
        Floor("Lobby gate", new Vector2(42, 443), new Vector2(302, 211), new Color(0.82f, 0.83f, 0.85f));
        Wall("Lobby west wall", new Vector2(36, 443), new Vector2(6, 211));
        Wall("Lobby south wall", new Vector2(42, 648), new Vector2(302, 6));
        Wall("Lobby east wall", new Vector2(344, 443), new Vector2(6, 211));
        Wall("Lobby north wall west", new Vector2(42, 437), new Vector2(230, 6));
        Wall("Lobby north wall east", new Vector2(314, 437), new Vector2(30, 6));
        // The partition is solid except for the two doorways, each as tall as its door is wide,
        // so the only way through is a revolving door caught in its gap.
        Wall("Lobby partition north", new Vector2(192, 443), new Vector2(6, 66));
        Wall("Lobby partition middle", new Vector2(192, 563), new Vector2(6, 4));
        Wall("Lobby partition south", new Vector2(192, 619), new Vector2(6, 35));

        Add(new RevolvingDoor(), "RevolvingDoorOne", new Vector2(193, 536));
        Add(new RevolvingDoor(), "RevolvingDoorTwo", new Vector2(193, 593));
        Add(new FloorMarker { Caption = "START - BUSINESS DEPARTMENT", Size = new Vector2(190, 24) }, "StartMarker", new Vector2(63, 694));

        // --- the north connector up to the executive office ---
        Floor("North connector", new Vector2(272, 364), new Vector2(42, 80), new Color(0.73f, 0.75f, 0.79f));
        Wall("North connector west", new Vector2(266, 364), new Vector2(6, 80));
        Wall("North connector east", new Vector2(314, 364), new Vector2(6, 80));

        // --- the executive office ---
        Floor("Executive office", new Vector2(62, 222), new Vector2(252, 142), new Color(0.86f, 0.85f, 0.82f));
        Wall("Office west wall", new Vector2(56, 222), new Vector2(6, 142));
        Wall("Office north wall", new Vector2(56, 216), new Vector2(264, 6));
        Wall("Office east wall north", new Vector2(314, 222), new Vector2(6, 34));
        Wall("Office east wall south", new Vector2(314, 322), new Vector2(6, 42));
        Wall("Office south wall", new Vector2(56, 364), new Vector2(210, 6));

        Add(new Inspectable { Caption = "JK", Size = new Vector2(34, 28), FirstDialogueId = "level6_jacket" }, "HesiodJacket", new Vector2(72, 236));
        Add(new Inspectable { Caption = "DK", Size = new Vector2(38, 30), FirstDialogueId = "level6_desk" }, "Desk", new Vector2(133, 300));
        Add(new Inspectable { Caption = "PO", Size = new Vector2(34, 28), FirstDialogueId = "level6_poster" }, "Poster", new Vector2(172, 226));
        Add(new Inspectable { Caption = "YORUKA", Size = new Vector2(42, 38), FirstDialogueId = "level6_yoruka_poster" }, "Yoruka", new Vector2(218, 261));

        // --- the archive connector and the audit archive ---
        Floor("Archive connector", new Vector2(312, 262), new Vector2(90, 54), new Color(0.73f, 0.75f, 0.79f));
        Wall("Archive connector north", new Vector2(312, 256), new Vector2(90, 6));
        Wall("Archive connector south", new Vector2(312, 316), new Vector2(90, 6));

        Floor("Audit archive", new Vector2(402, 222), new Vector2(232, 142), new Color(0.77f, 0.79f, 0.82f));
        Wall("Archive west wall north", new Vector2(396, 222), new Vector2(6, 34));
        Wall("Archive west wall south", new Vector2(396, 322), new Vector2(6, 42));
        Wall("Archive north wall west", new Vector2(396, 216), new Vector2(146, 6));
        Wall("Archive north wall east", new Vector2(584, 216), new Vector2(50, 6));
        Wall("Archive east wall", new Vector2(634, 222), new Vector2(6, 142));
        Wall("Archive south wall", new Vector2(396, 364), new Vector2(238, 6));

        Add(new Inspectable { Caption = "AUDIT FILES", Size = new Vector2(54, 34), FirstDialogueId = "level6_archive" }, "ArchiveInspect", new Vector2(440, 296));
        Add(new AuditFolderBrowser { FlagToSet = "F6_ledger" }, "AuditBrowser", new Vector2(560, 300));

        // --- the balcony connector up to the Atrium overlook ---
        Floor("Balcony connector", new Vector2(542, 170), new Vector2(42, 52), new Color(0.73f, 0.75f, 0.79f));
        Wall("Balcony connector west", new Vector2(536, 170), new Vector2(6, 52));
        Wall("Balcony connector east", new Vector2(584, 170), new Vector2(6, 52));

        // --- the overlook, its corridor back to the Atrium, and the decayed patch ---
        Floor("Atrium overlook", new Vector2(272, 82), new Vector2(362, 92), new Color(0.66f, 0.79f, 0.91f));
        Wall("Overlook north wall", new Vector2(266, 76), new Vector2(368, 6));
        Wall("Overlook west wall north", new Vector2(266, 82), new Vector2(6, 26));
        Wall("Overlook west wall south", new Vector2(266, 158), new Vector2(6, 16));
        Wall("Overlook east wall", new Vector2(634, 82), new Vector2(6, 92));
        Wall("Overlook south wall west", new Vector2(272, 174), new Vector2(270, 6));
        Wall("Overlook south wall east", new Vector2(584, 174), new Vector2(50, 6));

        Floor("Side exit", new Vector2(192, 108), new Vector2(72, 50), new Color(0.5f, 0.55f, 0.62f));
        Wall("Side exit north wall", new Vector2(186, 102), new Vector2(84, 6));
        Wall("Side exit south wall", new Vector2(186, 158), new Vector2(84, 6));
        Wall("Side exit west wall", new Vector2(186, 108), new Vector2(6, 50));
        Add(new FloorMarker { Caption = "BACK TO THE ATRIUM", Size = new Vector2(170, 20), Outline = new Color(0.3f, 0.5f, 1f) }, "ExitMarker", new Vector2(196, 88));

        _pad = Add(new Switch { IsPressurePlate = true, PlateRadius = 25f }, "OverlookPad", new Vector2(586, 128));
        Add(new FloorMarker { Caption = "PAD - hold with Echo", Size = new Vector2(48, 48), DottedRing = true, Outline = new Color(0.25f, 0.5f, 1f) }, "PadMarker", new Vector2(562, 104));
        for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
                _decayTiles.Add(Add(new TimeDecayTile { Size = new Vector2(39, 28) }, $"DecayTile{row * 3 + col + 1}", new Vector2(505 + col * 39, 88 + row * 28)));

        _hesiod = Add(new Inspectable { Caption = "HESIOD", Size = new Vector2(44, 42), FirstDialogueId = "level6_hesiod_overlook" }, "Hesiod", new Vector2(330, 112));
        _hesiod.Hide();
        // The three at the border stand in a row with a gap each, so E picks the one she is facing
        // rather than whichever of them happens to be nearest.
        _hesiodAtExit = Add(new Inspectable { Caption = "HESIOD", Size = new Vector2(24, 24), FlagToSet = "level6_talk_hesiod" }, "HesiodAtExit", new Vector2(200, 116));
        _yorukaAtExit = Add(new Inspectable { Caption = "YORUKA", Size = new Vector2(24, 24), FlagToSet = "level6_talk_yoruka" }, "YorukaAtExit", new Vector2(240, 116));
        _adellaineAtExit = Add(new Inspectable { Caption = "ADELLAINE", Size = new Vector2(24, 24), FlagToSet = "level6_talk_adellaine" }, "AdellaineAtExit", new Vector2(280, 116));
        _hesiodAtExit.Hide(); _yorukaAtExit.Hide(); _adellaineAtExit.Hide();
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        if (_pad != null)
        {
            foreach (TimeDecayTile tile in _decayTiles)
                tile.Freeze(_pad.IsActivated && tile.Position.DistanceTo(_pad.Position) <= PadFreezeRadius);
        }
        if (Player != null)
        {
            if (!_officeObjectivePlayed && Player.WorldPosition.X > 62 && Player.WorldPosition.X < 314 && Player.WorldPosition.Y > 222 && Player.WorldPosition.Y < 364)
            { _officeObjectivePlayed = true; Dialogue?.StartDialogue("level6_office_objective"); }
            foreach (TimeDecayTile tile in _decayTiles)
            {
                if (!tile.IsDust || !tile.Bounds.Intersects(Player.Bounds)) continue;
                Player.TakeScratch(); Player.WorldPosition = new Vector2(586, 128); return;
            }
            if (_hesiod.Visible && Player.WorldPosition.DistanceTo(_hesiod.Position) < 58f && !_hesiodConversationStarted)
            { _hesiodConversationStarted = true; Dialogue?.StartDialogue("level6_hesiod_overlook"); }
            if (ExitBounds.Intersects(Player.Bounds) && !_returnDialogueStarted)
            { _returnDialogueStarted = true; Dialogue?.StartDialogue("level6_back_to_atrium"); }
        }
        if (GameStateManager.Instance?.GetFlag("F6_ledger") == true)
        {
            GameStateManager.Instance.SetFlag("level6_ledger_read", true);
            _hesiod?.Show();
        }
    }

    public override bool IsBlocked(Rect2 bounds)
    {
        if (base.IsBlocked(bounds)) return true;
        foreach (Node node in GetChildren())
        {
            if (node is not RevolvingDoor door || door.GapOpen) continue;
            Rect2 panel = new Rect2(door.Position - new Vector2(door.Radius, 7f), new Vector2(door.Radius * 2f, 14f));
            if (panel.Intersects(bounds)) return true;
        }
        return false;
    }

    protected override bool CanCompleteRoom() => GameStateManager.Instance?.GetFlag("level6_return_ready") == true
        && GameStateManager.Instance?.GetFlag("level6_talk_hesiod") == true
        && GameStateManager.Instance?.GetFlag("level6_talk_yoruka") == true
        && GameStateManager.Instance?.GetFlag("level6_talk_adellaine") == true;
    protected override void ResetPuzzles()
    {
        base.ResetPuzzles(); _returnDialogueStarted = false; _hesiodConversationStarted = false; _officeObjectivePlayed = false;
        foreach (TimeDecayTile tile in _decayTiles) tile.ResetState();
    }
    private void OnDialogueEnded(string dialogueId)
    {
        if (dialogueId == "level6_hesiod_overlook")
        {
            GameStateManager.Instance?.SetFlag("hesiod_joined", true);
            _hesiod?.Hide();   // he moves to the border with the other two, rather than standing twice
            _hesiodAtExit?.Show(); _yorukaAtExit?.Show(); _adellaineAtExit?.Show();
        }
        if (dialogueId == "level6_lobby_gate") GameStateManager.Instance?.SetFlag("felix_left", true);
        if (dialogueId == "level6_back_to_atrium") GameStateManager.Instance?.SetFlag("level6_return_ready", true);
    }
    private T Add<T>(T node, string name, Vector2 position) where T : Node2D { node.Name = name; node.Position = position; AddChild(node); return node; }
    private void Wall(string name, Vector2 pos, Vector2 size) => Add(new Obstacle { Size = size }, name, pos);
    private void Floor(string name, Vector2 pos, Vector2 size, Color color) => Add(new FloorMarker { Size = size, Caption = "", Fill = color, Outline = new Color(color, 0.8f) }, name, pos);
}
