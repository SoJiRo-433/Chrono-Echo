using Godot;

/// <summary>Level 2, scenes 2-5: the Archives through to the heavy library doors.
///
/// The level's art is the map itself (<c>Assets/level2_archives_map.png</c>), which is 640x360 and is
/// the room's whole floor plan: the reading room at the top left, the Archives with their ranks of
/// shelving, the connector where the shelves have come down, the lit terminal room on the right, and
/// the root-webbed corridor along the bottom to the heavy library doors.
///
/// The map is drawn at twice its own resolution - a whole-number upscale, so its pixels stay square,
/// and room enough for the player's 28-unit body in the aisles and doorways the map draws. One unit
/// in this file is one pixel of the map, so every rectangle below can be read against the sheet.
///
/// Nothing in the map moves, so everything the level *does* is an overlay on top of it:
///  - the walls, the shelving and the furniture are <see cref="Obstacle"/>s with their boxes turned
///    off, so the map's own rooms are what she is walking inside;
///  - everything E answers to is an <c>Inspectable</c> with its box turned off too, so the map shows
///    the object while its caption and its interaction still land on it;
///  - what the map does not draw is drawn: the plates, the dotted stand spot for the Echo, the two
///    doors, and the captions themselves.
///
/// The base Room already owns the HUD, the dialogue plumbing and the level hand-over, so this
/// subclass adds only what this level is: the scripted pan onto Yoruka, her outburst and the item
/// she leaves behind, the collapsed shelves - a Door held open by a Switch, exactly like Room01's
/// security door - and the walk-and-talk that opens the heavy doors.</summary>
public partial class RoomLibrary : Room
{
    private const string MapPath = "res://Assets/level2_archives_map.png";

    /// <summary>Map pixels to world units. See the class comment.</summary>
    private const float MapScale = 2f;

    #region Private state
    private Inspectable _yoruka;
    private Inspectable _yorukaWaiting;
    private Inspectable _yorukaItem;
    private Door _shelvesBlock;
    private Door _libraryDoors;
    private FloorMarker _corridorTrigger;
    private FloorMarker _panTrigger;
    private bool _yorukaSentOff;
    private bool _walkAndTalkPlayed;
    private bool _yorukaPanPlayed;
    #endregion

    #region Lifecycle
    public override void _Ready()
    {
        BuildMap();

        // The map is the world: 640x360 pixels, drawn at MapScale.
        RoomSize = S(640f, 360f);
        PlayerStartPosition = P(76f, 48f);
        ExitPosition = P(112f, 296f);
        ExitSize = S(40f, 54f);
        LevelNumber = 2;
        NextLevelPath = "res://Scenes/Rooms/RoomAtrium.tscn";
        MaxEchoes = 2;
        MaxCaptureSeconds = 5f;          // the window Level 2 has always asked for
        EchoLifetimeSeconds = 20f;       // long enough to hold the latch and walk through it
        LoopReplay = false;
        IntroDialogueId = "";            // the pan trigger is what introduces this level
        ClearDialogueId = "";
        CompletionFlag = "library_complete";

        base._Ready();

        if (_yorukaWaiting != null) _yorukaWaiting.Hide();   // she is on the far side of the shelves
        if (_yorukaItem != null) _yorukaItem.Hide();         // nothing is on the floor until she drops it

        if (Dialogue != null) Dialogue.DialogueEnded += OnLibraryDialogueEnded;
    }

    public override void _ExitTree()
    {
        if (Dialogue != null) Dialogue.DialogueEnded -= OnLibraryDialogueEnded;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        if (GameStateManager.Instance != null && GameStateManager.Instance.IsPaused) return;

        CheckYorukaPan();
        RevealYorukaWhenPassed();
        CheckCorridorEntry();
    }
    #endregion

    #region The map (map pixels, read straight off the sheet)
    private void BuildMap()
    {
        Backdrop();
        BuildReadingRoom();
        BuildArchives();
        BuildCollapsedShelves();
        BuildTerminalRoom();
        BuildRootWebbedCorridor();
    }

    /// <summary>The painting under everything. It is the first child, so every overlay - the plates,
    /// the doors, the captions - lands on top of it.</summary>
    private void Backdrop()
    {
        var map = ResourceLoader.Load<Texture2D>(MapPath);
        if (map == null)
        {
            GD.PushWarning("RoomLibrary: " + MapPath + " is missing - the level has no art.");
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

    /// <summary>The room she walks into out of the hallway: the Archives' front desk. The cork
    /// board, the computer and the drawers are all here, and the sealed service door back to the
    /// bridgeway is the room's east wall.</summary>
    private void BuildReadingRoom()
    {
        // Walls: shapes only - the map is what is drawn. The gap between the two south pieces is the
        // doorway down into the Archives.
        Wall("Reading north wall", new Vector2(6, 0), new Vector2(184, 6));
        Wall("Reading west wall", new Vector2(0, 6), new Vector2(6, 62));
        Wall("Reading east wall", new Vector2(190, 6), new Vector2(6, 56));
        Wall("Reading south wall west", new Vector2(6, 62), new Vector2(95, 6));
        Wall("Reading south wall east", new Vector2(152, 62), new Vector2(38, 6));

        Talkable("CorkBoard", "CORK BOARD (E)", new Vector2(16, 12), new Vector2(44, 30), "library_cork_board", "library_cork_board_seen");
        Talkable("Computer", "COMPUTER (E)", new Vector2(70, 10), new Vector2(34, 26), "library_computer", "library_computer_seen");
        Talkable("Drawers", "DRAWERS (E)", new Vector2(112, 14), new Vector2(30, 24), "library_drawers", "library_drawers_seen");
        Talkable("ServiceDoor", "SERVICE DOOR (E)", new Vector2(150, 8), new Vector2(20, 50), "library_sealed_door");

        Caption("StartMarker", "START - THE ARCHIVES", new Vector2(8, 44), new Vector2(150, 14));
    }

    /// <summary>The Archives proper: three ranks of shelving with the aisles between them, the rank
    /// along the north wall, the spine at the east end, and Yoruka somewhere in the middle of it.</summary>
    private void BuildArchives()
    {
        Wall("Archive west wall", new Vector2(0, 68), new Vector2(6, 194));
        Wall("Archive north wall east", new Vector2(170, 62), new Vector2(210, 6));
        Wall("Archive east wall north", new Vector2(380, 68), new Vector2(6, 74));     // the connector to
        Wall("Archive east wall south", new Vector2(380, 182), new Vector2(6, 80));    // the terminal is
        Wall("Archive south wall", new Vector2(0, 262), new Vector2(386, 6));          // the gap between them

        // The ranks, and the stack that came down across the north aisle. All shapes: the map draws
        // the shelving, and the spill of books around the collapsed case.
        Shelf("ShelfNorthEast", new Vector2(272, 68), new Vector2(84, 12));
        Shelf("ShelfRowOne", new Vector2(128, 120), new Vector2(172, 12));
        Shelf("ShelfRowTwo", new Vector2(118, 168), new Vector2(182, 12));
        Shelf("ShelfRowThree", new Vector2(128, 216), new Vector2(122, 12));
        Shelf("ShelfSpine", new Vector2(306, 204), new Vector2(16, 58));
        Shelf("CollapsedStack", new Vector2(216, 80), new Vector2(46, 40));
        Shelf("ToppledShelf", new Vector2(170, 228), new Vector2(66, 18));

        Talkable("Bookshelves", "BOOKSHELVES (E)", new Vector2(130, 168), new Vector2(80, 12), "library_bookshelves", "library_bookshelves_seen");

        // Scene 2's pan trigger: the first step into the room proper, and the camera goes looking
        // for Yoruka before Aionia has said anything.
        _panTrigger = Add(new FloorMarker
        {
            Size = S(51f, 32f),
            Fill = new Color(0.55f, 0.45f, 0.85f, 0.04f), Outline = new Color(0.60f, 0.50f, 0.90f, 0.15f)
        }, "YorukaPanTrigger", P(101f, 72f));

        // She stands in the first aisle the way on runs through - 150,134 is the aisle between the
        // first two ranks, a few steps from where the player comes down out of the reading room - so
        // the pan finds her without a search, and she is not a wall across the aisle either.
        _yoruka = Talkable("Yoruka", "YORUKA (E)", new Vector2(150, 134), new Vector2(24, 34),
                           "library_yoruka", solid: false, characterSheet: CharacterSprite.FrontSpritePath(GameStateManager.Yoruka));

        // Scene 2 ends with her storming off - "leaving behind an item" (sheet). It is anchored to
        // her node rather than to a hard-coded spot, so moving her moves the thing she leaves.
        _yorukaItem = Talkable("YorukaItem", "DROPPED ITEM (E)", new Vector2(150, 134), new Vector2(24, 18),
                               "library_item_taken", flagToSet: "library_item_taken", repeat: true, solid: false);
    }

    /// <summary>Scene 3. The fallen shelves have come down in the connector between the Archives and
    /// the terminal, so that connector is the block: one Door filling it, held open by the latch
    /// beside it. The latch sits on the near side on purpose - the Echo has to reach it before the
    /// door opens, not after - flanked by the two rubble piles the sheet draws, with enough floor
    /// between them to stand in and the route around them left open.</summary>
    private void BuildCollapsedShelves()
    {
        _shelvesBlock = Add(new Door
        {
            Size = S(49f, 40f),
            OpenedFlag = "library_shelves_opened",
            ApproachDialogueId = "library_collapsed_shelves",
            ApproachFlag = "library_shelves_seen"
        }, "CollapsedShelvesDoor", P(386f, 142f));

        Wall("Connector north wall", new Vector2(380, 136), new Vector2(55, 6));
        Wall("Connector south wall", new Vector2(380, 182), new Vector2(55, 6));
        Wall("RubbleLatchNorth", new Vector2(348, 120), new Vector2(30, 30));
        Wall("RubbleLatchSouth", new Vector2(348, 186), new Vector2(30, 30));

        // 13 map pixels of radius: the same reach as Room01's and Room02's plates, rather than the
        // whole connector painted yellow.
        Switch latch = Add(new Switch { IsPressurePlate = true, PlateRadius = 13f * MapScale },
                           "ShelvesLatch", P(363f, 168f));
        latch.LinkDoor(_shelvesBlock);

        Add(new FloorMarker
        {
            Size = S(28f, 28f),
            Outline = new Color(0.55f, 0.80f, 1f, 0.85f), DottedRing = true
        }, "ShelvesLatchSpot", P(349f, 154f));
    }

    /// <summary>Scene 4: the lit room, with the sheet's mainframe across its north wall and Yoruka
    /// waiting in it - she is the one who points the two of them at the medical wing.</summary>
    private void BuildTerminalRoom()
    {
        Wall("Terminal north wall", new Vector2(429, 72), new Vector2(211, 6));
        Wall("Terminal west wall north", new Vector2(429, 78), new Vector2(6, 64));
        Wall("Terminal west wall south", new Vector2(429, 182), new Vector2(6, 68));
        Wall("Terminal east wall", new Vector2(634, 72), new Vector2(6, 178));
        // The drop into the corridor, at the x the map draws its connector (500..540), and clear of
        // the rack and the spares on the room's east side.
        Wall("Terminal south wall west", new Vector2(429, 250), new Vector2(71, 6));
        Wall("Terminal south wall east", new Vector2(540, 250), new Vector2(100, 6));
        Wall("Drop west wall", new Vector2(494, 250), new Vector2(6, 42));
        Wall("Drop east wall", new Vector2(540, 250), new Vector2(6, 42));

        Talkable("TerminalLog", "TERMINAL (E)", new Vector2(487, 88), new Vector2(73, 40),
                 "library_terminal", repeat: true);
        Talkable("ServerRack", "SERVER RACK (E)", new Vector2(552, 150), new Vector2(50, 60), "library_server_rack");
        Talkable("TerminalSpares", "SPARES (E)", new Vector2(558, 214), new Vector2(44, 28), "library_spares");

        _yorukaWaiting = Talkable("YorukaWaiting", "YORUKA (E)", new Vector2(570, 112), new Vector2(24, 34),
                                  "library_yoruka_followup", repeat: true, characterSheet: CharacterSprite.FrontSpritePath(GameStateManager.Yoruka));
    }

    /// <summary>Scene 5: the corridor the sheet webs with roots. It runs west from the drop under
    /// the terminal room to the heavy library doors, which is the whole of the scene - the walk-and-
    /// talk plays on the way down, and the doors open when it is over.</summary>
    private void BuildRootWebbedCorridor()
    {
        Wall("Corridor north wall west", new Vector2(104, 286), new Vector2(396, 6));   // the drop from the
        Wall("Corridor north wall east", new Vector2(540, 286), new Vector2(100, 6));   // terminal is between
        Wall("Corridor south wall", new Vector2(104, 354), new Vector2(536, 6));
        Wall("Corridor west wall", new Vector2(104, 292), new Vector2(6, 62));
        Wall("Corridor east wall", new Vector2(634, 292), new Vector2(6, 62));

        _libraryDoors = Add(new Door { Size = S(14f, 62f), OpenedFlag = "library_doors_opened" },
                            "LibraryDoors", P(200f, 292f));

        // The corridor's equipment, all of it against the north wall so the floor it is worked from
        // stays clear.
        Talkable("CorridorBench", "BENCH (E)", new Vector2(224, 292), new Vector2(52, 22), "library_bench");
        Talkable("CorridorConduit", "CONDUIT (E)", new Vector2(330, 292), new Vector2(46, 28), "library_conduit");
        Talkable("CorridorWorkbench", "WORKBENCH (E)", new Vector2(450, 292), new Vector2(42, 24), "library_workbench");
        Talkable("CorridorSpares", "SPARES (E)", new Vector2(590, 292), new Vector2(34, 26), "library_spares");

        _corridorTrigger = Add(new FloorMarker
        {
            Size = S(40f, 62f),
            Fill = new Color(0.55f, 0.45f, 0.85f, 0.05f), Outline = new Color(0.60f, 0.50f, 0.90f, 0.25f)
        }, "CorridorTrigger", P(500f, 292f));

        Caption("DoorMarker", "HEAVY LIBRARY DOORS", new Vector2(146, 268), new Vector2(130, 16));
        Caption("ExitMarker", "EXIT - THE ATRIUM", new Vector2(110, 330), new Vector2(120, 16),
                new Color(0.3f, 0.5f, 1f));
    }
    #endregion

    #region Story beats
    private void OnLibraryDialogueEnded(string dialogueId)
    {
        if (dialogueId == "library_yoruka") SendYorukaOff();
        else if (dialogueId == "library_to_atrium") _libraryDoors?.Open();   // scene 5: the doors, at last
    }

    /// <summary>Scene 2 opens with the camera leaving Aionia and finding Yoruka among the shelves,
    /// and only then does she think about approaching her - which is also where the objective
    /// ("Approach the student") comes from.</summary>
    private void CheckYorukaPan()
    {
        if (_yorukaPanPlayed || _panTrigger == null) return;

        Player player = Player;
        if (player == null || !player.IsAlive) return;
        if (!_panTrigger.Bounds.Intersects(player.Bounds)) return;

        _yorukaPanPlayed = true;
        PanCameraTo(_yoruka != null ? _yoruka.Position : P(250f, 168f), 1.8f);
        Dialogue?.StartDialogue("library_intro");
    }

    /// <summary>Scene 2 ends with her storming off. The node goes away, and because a hidden
    /// interactable is skipped it stops being examinable at the same moment - and the item she
    /// drops appears in the same spot in the same breath.</summary>
    private void SendYorukaOff()
    {
        if (_yorukaSentOff) return;
        _yorukaSentOff = true;
        _yoruka?.Hide();
        DropYorukaItem();
    }

    private void DropYorukaItem()
    {
        if (_yorukaItem == null) return;
        if (_yoruka != null) _yorukaItem.Position = _yoruka.Position;
        _yorukaItem.Show();
    }

    /// <summary>Scene 4: once the fallen shelves are open and the player is through them, Yoruka is
    /// waiting in the terminal room rather than among the shelves she ran from.</summary>
    private void RevealYorukaWhenPassed()
    {
        if (_yorukaWaiting == null || _yorukaWaiting.Visible) return;
        if (_shelvesBlock == null || !_shelvesBlock.IsOpen) return;
        if (Player == null || Player.WorldPosition.X < _shelvesBlock.Position.X + 40f) return;

        _yorukaWaiting.Show();
    }

    /// <summary>Scene 5: dropping into the root-webbed corridor starts the walk-and-talk with its
    /// three conversation beats. The heavy library doors open when it is over.</summary>
    private void CheckCorridorEntry()
    {
        if (_walkAndTalkPlayed || _corridorTrigger == null) return;

        Player player = Player;
        if (player == null || !player.IsAlive) return;
        if (!_corridorTrigger.Bounds.Intersects(player.Bounds)) return;

        _walkAndTalkPlayed = true;
        Dialogue?.StartDialogue("library_to_atrium");
    }

    /// <summary>An R-restart has to put this level back the way it started: Yoruka in the Archives,
    /// nothing on the floor, the conversation beats unplayed and the far-side Yoruka hidden again.
    /// The flags that go with them are cleared by the reset itself.</summary>
    protected override void ResetPuzzles()
    {
        base.ResetPuzzles();

        _yorukaSentOff = false;
        _walkAndTalkPlayed = false;
        _yorukaPanPlayed = false;
        _yoruka?.Show();
        _yorukaItem?.Hide();
        _yorukaWaiting?.Hide();
    }
    #endregion

    #region Overlay helpers
    /// <summary>A point of the map - one map pixel is one unit here, so this is where the level's
    /// geometry is read against the sheet.</summary>
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

    /// <summary>A wall, a rank of shelving or a piece of furniture as the map drew it: a shape that
    /// blocks, with nothing of its own drawn - the map is the drawing. The ranks and the rubble
    /// piles are told from the walls only by what they say to the flood fill, which is the point:
    /// the map decides where the room is.</summary>
    private void Wall(string name, Vector2 position, Vector2 size) =>
        Add(new Obstacle { Size = S(size.X, size.Y), DrawBox = false }, name, P(position.X, position.Y));

    private void Shelf(string name, Vector2 position, Vector2 size) => Wall(name, position, size);

    /// <summary>Something E answers to whose object the map already draws: the box is off, the
    /// caption and the interaction stay.</summary>
    private Inspectable Talkable(string name, string caption, Vector2 position, Vector2 size,
                                 string dialogueId, string flagToSet = "", bool repeat = false,
                                 bool solid = true, string characterSheet = "")
    {
        var node = new Inspectable
        {
            Caption = caption,
            Size = S(size.X, size.Y),
            Solid = solid,
            DrawBox = false,
            FirstDialogueId = dialogueId,
            RepeatDialogueId = repeat ? dialogueId : "",
            FlagToSet = flagToSet,
            CharacterSheetPath = characterSheet
        };
        return Add(node, name, P(position.X, position.Y));
    }

    /// <summary>A marker with the caption only: the map already draws the place.</summary>
    private void Caption(string name, string caption, Vector2 position, Vector2 size, Color? outline = null)
    {
        var marker = new FloorMarker { Caption = caption, Size = S(size.X, size.Y), DrawBox = false };
        if (outline.HasValue) marker.Outline = outline.Value;
        Add(marker, name, P(position.X, position.Y));
    }
    #endregion
}
