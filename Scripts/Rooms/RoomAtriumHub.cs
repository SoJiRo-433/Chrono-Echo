using System.Collections.Generic;
using Godot;

/// <summary>Level 7 returns to the Atrium as a safe hub. It reuses the Atrium map art and its
/// room layout, but the old hazards are gone. The visit is about the three conversations, Hesiod's
/// camera roll, meeting Felix on the deck, and opening the service hatch with an Echo.</summary>
public partial class RoomAtriumHub : Room
{
    private const float MapScale = 2f;
    private const string MapPath = "res://Assets/level3_atrium_map.png";
    private const float ShutterHoldSeconds = 15f;
    private const float HatchHoldSeconds = 6f;

    private Switch _photoShutter;
    private PhotoRollPuzzle _cameraRoll;
    private Switch _consoleOne;
    private Switch _consoleTwo;
    private Door _serviceHatch;
    private FloorMarker _staticBand;
    private FloorMarker _felixSpotTrigger;
    private Inspectable _hesiod;
    private Inspectable _adellaine;
    private Inspectable _yoruka;
    private Inspectable _bedroll;
    private readonly List<string> _firstConversationOrder = new();
    private float _shutterElapsed;
    private float _hatchElapsed;
    private float _staticClock;
    private bool _returnPhase;
    private bool _felixDialogueQueued;
    private bool _nightWatchQueued;
    private bool _nightWatchFinished;

    public override void _Ready()
    {
        BuildMap();
        RoomSize = new Vector2(1280f, 720f);
        WorldSize = RoomSize;
        PlayerStartPosition = P(28f, 320f); // the doorway back from the Level 6 corridor
        ExitPosition = P(110f, 138f);        // the north-wall service hatch
        ExitSize = S(40f, 6f);
        LevelNumber = 7;
        NextLevelPath = "res://Scenes/Rooms/Room08Facility.tscn";
        MaxEchoes = 3;
        MaxCaptureSeconds = 15f;
        EchoLifetimeSeconds = 45f;
        LoopReplay = false;
        IntroDialogueId = "level7_intro";
        ClearDialogueId = "level7_clear";
        CompletionFlag = "level7_complete";
        ExitLockedHint = "THE SERVICE HATCH IS STILL SEALED";

        base._Ready();
        if (Dialogue != null) Dialogue.DialogueEnded += OnDialogueEnded;
        _cameraRoll.Hide();
        _bedroll.Hide();
        Dialogue?.SetObjective("Talk to Hesiod.");
    }

    public override void _ExitTree()
    {
        if (Dialogue != null) Dialogue.DialogueEnded -= OnDialogueEnded;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        if (GameStateManager.Instance?.IsPaused == true) return;

        float step = (float)delta;
        _staticClock += step;

        if (EchoOn(_photoShutter))
        {
            _shutterElapsed += step;
            if (_shutterElapsed >= ShutterHoldSeconds && !GameStateManager.Instance.GetFlag("level7_shutter_held"))
            {
                GameStateManager.Instance.SetFlag("level7_shutter_held", true);
                Dialogue?.SetObjective("Read Hesiod's camera roll.");
            }
        }
        else if (GameStateManager.Instance?.GetFlag("level7_shutter_held") != true)
            _shutterElapsed = 0f;

        if (!_nightWatchFinished && EchoOn(_consoleOne) && PlayerOn(_consoleTwo) &&
            _consoleOne?.IsActivated == true && _consoleTwo?.IsActivated == true)
        {
            _hatchElapsed += step;
            if (_hatchElapsed >= HatchHoldSeconds)
            {
                _nightWatchFinished = true;
                GameStateManager.Instance?.SetFlag("level7_hatch_open", true);
                _serviceHatch?.Open();
                Dialogue?.SetObjective("Enter the service lift.");
            }
        }
        else if (!_nightWatchFinished) _hatchElapsed = 0f;

        if (_felixSpotTrigger != null && Player != null &&
            _felixSpotTrigger.Bounds.Intersects(Player.Bounds) &&
            GameStateManager.Instance?.GetFlag("level7_night_walk") == true &&
            GameStateManager.Instance?.GetFlag("level7_felix_met") != true)
        {
            GameStateManager.Instance?.SetFlag("level7_felix_met", true);
            Dialogue?.StartDialogue("level7_felix_deck");
        }
    }

    public override bool IsBlocked(Rect2 bounds)
    {
        if (base.IsBlocked(bounds)) return true;

        // The terminal's static band pulses for one second, then stays quiet for two.
        bool staticActive = Mathf.PosMod(_staticClock, 3f) < 1f;
        return staticActive && _staticBand != null && _staticBand.Bounds.Intersects(bounds);
    }

    protected override bool CanCompleteRoom() => _nightWatchFinished &&
        (GameStateManager.Instance?.GetFlag("level7_hatch_open") ?? false);

    private void BuildMap()
    {
        Texture2D map = ResourceLoader.Load<Texture2D>(MapPath);
        if (map != null)
            Add(new Sprite2D { Texture = map, Centered = false, Scale = Vector2.One * MapScale,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest }, "AtriumBackdrop", Vector2.Zero);
        else GD.PushWarning("RoomAtriumHub: Atrium map texture is missing.");

        // Safe walkable regions follow the Level 3 plan; the lower walkway joins the entry hall,
        // the bridge, the terminal, the spiral stairs and the observation deck.
        Floor("EntryHall", 8, 292, 182, 60);
        Floor("EastCorridor", 190, 314, 330, 38);
        Floor("LowerWalkway", 88, 256, 432, 30);
        Floor("GlassSkybridge", 88, 216, 462, 34);
        Floor("Terminal", 80, 144, 150, 66);
        Floor("Stairs", 154, 76, 62, 68);
        Floor("ObservationDeck", 104, 16, 496, 54);
        Floor("Storeroom", 520, 292, 112, 60);

        // Edge rails and room dividers. Openings line up with the routes shown on the map.
        Wall("EntryWestTop", 2, 292, 6, 20); Wall("EntryWestBottom", 2, 336, 6, 16);
        Wall("EntrySouth", 8, 352, 512, 6); Wall("EntryNorth", 8, 286, 182, 6);
        Wall("EntryEastTop", 184, 292, 6, 22);
        Wall("CorridorNorthWest", 190, 286, 278, 6); Wall("CorridorNorthEast", 520, 286, 6, 6);
        Wall("CorridorEast", 520, 292, 6, 22); Wall("StoreroomSouth", 520, 352, 112, 6);
        Wall("StoreroomEast", 632, 292, 6, 60); Wall("StoreroomNorth", 520, 286, 112, 6);
        Wall("WalkwayWest", 82, 256, 6, 30); Wall("WalkwayEast", 520, 256, 6, 30);
        Wall("BridgeNorth", 230, 210, 320, 6); Wall("BridgeWest", 82, 216, 6, 40);
        Wall("BridgeEast", 550, 216, 6, 40);
        Wall("BridgeSouthWest", 88, 250, 212, 6); Wall("BridgeSouthEast", 380, 250, 176, 6);
        Wall("TerminalWest", 74, 144, 6, 66); Wall("TerminalEast", 230, 144, 6, 66);
        Wall("TerminalNorthWest", 80, 138, 30, 6); Wall("TerminalNorthMiddle", 150, 138, 10, 6);
        Wall("TerminalNorthEast", 160, 138, 70, 6);
        Wall("TerminalSouthWest", 80, 210, 50, 6); Wall("TerminalSouthEast", 190, 210, 40, 6);
        Wall("StairsWest", 154, 76, 6, 62); Wall("StairsEast", 216, 76, 6, 62);
        Wall("DeckNorth", 98, 10, 508, 6); Wall("DeckWest", 98, 16, 6, 54);
        Wall("DeckSouthWest", 104, 70, 56, 6); Wall("DeckSouthEast", 216, 70, 384, 6);

        Add(new FloorMarker { Caption = "BACK TO THE ATRIUM", Size = S(120, 16), DrawBox = false },
            "StartCaption", P(12, 338));
        Add(new FloorMarker { Caption = "STOREROOM - REST", Size = S(84, 16), DrawBox = false },
            "StoreroomCaption", P(528, 338));

        _hesiod = Add(new Inspectable { Caption = "HESIOD / CAMERA", Size = S(42, 24), DrawBox = false,
            FirstDialogueId = "level7_hesiod", RepeatDialogueId = "level7_hesiod",
            CharacterSheetPath = CharacterSprite.FrontSpritePath(GameStateManager.Hesiod) }, "Hesiod", P(360, 224));
        _photoShutter = Add(new Switch { IsPressurePlate = true, PlateRadius = 18f }, "CameraShutter", P(426, 225));
        Add(new FloorMarker { Caption = "HOLD - 15 SEC", Size = S(52, 14), DottedRing = true,
            Outline = new Color(0.25f, 0.55f, 1f) }, "ShutterMarker", P(400, 216));
        _cameraRoll = Add(new PhotoRollPuzzle { ShutterFlag = "level7_shutter_held", FlagToSet = "F7_camera_roll",
            SolvedDialogueId = "level7_camera_solved", Caption = "CAMERA ROLL (E)" }, "CameraRoll", P(472, 225));

        _adellaine = Add(new Inspectable { Caption = "ADELLAINE", Size = S(40, 30), DrawBox = false,
            FirstDialogueId = "level7_adellaine", RepeatDialogueId = "level7_adellaine",
            CharacterSheetPath = CharacterSprite.FrontSpritePath(GameStateManager.Adellaine) }, "Adellaine", P(112, 166));
        Add(new FloorMarker { Caption = "MEDICAL KIT", Size = S(34, 12), DrawBox = false }, "MedicalKit", P(110, 184));
        _yoruka = Add(new Inspectable { Caption = "YORUKA", Size = S(40, 30), DrawBox = false,
            FirstDialogueId = "level7_yoruka", RepeatDialogueId = "level7_yoruka",
            CharacterSheetPath = CharacterSprite.FrontSpritePath(GameStateManager.Yoruka) }, "Yoruka", P(56, 318));
        Add(new FloorMarker { Caption = "CRATE", Size = S(28, 16), DrawBox = false }, "YorukaCrate", P(40, 334));

        _bedroll = Add(new Inspectable { Caption = "BEDROLL (E)", Size = S(42, 20),
            FirstDialogueId = "level7_rest", RepeatDialogueId = "level7_rest" }, "Bedroll", P(554, 312));

        // "Felix stands on the reflection spot and uses the supplied static, front-facing art.
        Add(new CharacterSprite { SheetPath = CharacterSprite.FrontSpritePath(GameStateManager.Felix), FrameCount = 1, Look = CharacterSprite.Facing.South }, "Felix", P(318, 34));
        _felixSpotTrigger = Add(new FloorMarker { Size = S(24, 24), DrawBox = false },
            "FelixSpotOne", P(188, 32));
        for (int i = 0; i < 3; i++)
            Add(new FloorMarker { Size = S(24, 24), DottedRing = true, Outline = new Color(0.45f, 0.7f, 1f) },
                $"ReflectionSpot{i + 1}", P(188 + i * 130, 32));

        _staticBand = Add(new FloorMarker { Size = S(24, 38), Caption = "STATIC", Fill = new Color(0.25f, 0.65f, 1f, 0.2f),
            Outline = new Color(0.5f, 0.8f, 1f, 0.7f), Dashed = true }, "StaticBand", P(142, 164));
        _consoleOne = Add(new Switch { IsPressurePlate = true, PlateRadius = 18f }, "ConsoleC1", P(110, 186));
        _consoleTwo = Add(new Switch { IsPressurePlate = true, PlateRadius = 18f }, "ConsoleC2", P(170, 186));
        Add(new FloorMarker { Caption = "C1", Size = S(18, 12), DrawBox = false }, "C1Label", P(100, 170));
        Add(new FloorMarker { Caption = "C2", Size = S(18, 12), DrawBox = false }, "C2Label", P(160, 170));
        _serviceHatch = Add(new Door { Size = S(40, 6), OpenedFlag = "level7_hatch_open" },
            "ServiceHatch", P(110, 138));
        Add(new FloorMarker { Caption = "SERVICE LIFT", Size = S(72, 14), DrawBox = false }, "HatchCaption", P(100, 126));
    }

    private void OnDialogueEnded(string dialogueId)
    {
        if (dialogueId == "level7_hesiod" || dialogueId == "level7_adellaine" || dialogueId == "level7_yoruka")
        {
            string key = dialogueId switch
            {
                "level7_hesiod" => "hesiod",
                "level7_adellaine" => "adellaine",
                _ => "yoruka"
            };
            if (!_returnPhase)
            {
                string firstFlag = "level7_first_" + key;
                if (!GameStateManager.Instance.GetFlag(firstFlag))
                {
                    GameStateManager.Instance.SetFlag(firstFlag, true);
                    _firstConversationOrder.Add(key);
                }
                UpdateFirstTalkObjective();
            }
            else
            {
                GameStateManager.Instance?.SetFlag("level7_return_" + key, true);
                if (AllReturned()) QueueNightWatch();
            }
            if (dialogueId == "level7_hesiod") _cameraRoll?.Show();
        }
        else if (dialogueId == "level7_camera_solved")
        {
            Dialogue?.SetObjective("Rest in the storeroom.");
            RefreshBedrollAvailability();
        }
        else if (dialogueId == "level7_rest")
        {
            GameStateManager.Instance?.SetFlag("level7_night_walk", true);
            _bedroll?.Hide();
            Dialogue?.SetObjective("Go for a walk on the Observation Deck.");
        }
        else if (dialogueId == "level7_felix_deck")
        {
            if (!_felixDialogueQueued)
            {
                _felixDialogueQueued = true;
                Dialogue?.StartDialogue("level7_piano_roll");
            }
        }
        else if (dialogueId == "level7_piano_roll")
        {
            GameStateManager.Instance?.SetFlag("F8_piano_roll", true);
            _returnPhase = true;
            Dialogue?.SetObjective("Go back to the others.");
        }
        else if (dialogueId.StartsWith("level7_nightwatch_"))
        {
            _nightWatchFinished = true;
            GameStateManager.Instance?.SetFlag("level7_nightwatch_done", true);
            Dialogue?.SetObjective("Open the way to the Time Research Facility.");
        }
    }

    private void UpdateFirstTalkObjective()
    {
        int count = (GameStateManager.Instance?.GetFlag("level7_first_hesiod") == true ? 1 : 0) +
                    (GameStateManager.Instance?.GetFlag("level7_first_adellaine") == true ? 1 : 0) +
                    (GameStateManager.Instance?.GetFlag("level7_first_yoruka") == true ? 1 : 0);
        Dialogue?.SetObjective(count >= 3 ? "Solve Hesiod's camera roll, then rest." : $"Talk to your friends ({count}/3).");
        RefreshBedrollAvailability();
    }

    private void RefreshBedrollAvailability()
    {
        bool ready = GameStateManager.Instance?.GetFlag("F7_camera_roll") == true &&
                     GameStateManager.Instance?.GetFlag("level7_first_hesiod") == true &&
                     GameStateManager.Instance?.GetFlag("level7_first_adellaine") == true &&
                     GameStateManager.Instance?.GetFlag("level7_first_yoruka") == true &&
                     GameStateManager.Instance?.GetFlag("level7_night_walk") != true;
        if (_bedroll != null) _bedroll.Visible = ready;
    }

    private bool AllReturned() => GameStateManager.Instance?.GetFlag("level7_return_hesiod") == true &&
        GameStateManager.Instance?.GetFlag("level7_return_adellaine") == true &&
        GameStateManager.Instance?.GetFlag("level7_return_yoruka") == true;

    private void QueueNightWatch()
    {
        if (_nightWatchQueued) return;
        _nightWatchQueued = true;
        string winner = PickNightWatch();
        Dialogue?.SetObjective("Wait for the night watch.");
        CallDeferred(nameof(StartNightWatch), winner);
    }

    private void StartNightWatch(string winner) => Dialogue?.StartDialogue("level7_nightwatch_" + winner);

    private string PickNightWatch()
    {
        GameStateManager state = GameStateManager.Instance;
        if (state == null) return "group";
        string[] friends = { "hesiod", "adellaine", "yoruka" };
        float friendMax = Mathf.Max(state.GetAffection(GameStateManager.Hesiod),
            Mathf.Max(state.GetAffection(GameStateManager.Adellaine), state.GetAffection(GameStateManager.Yoruka)));
        float felix = state.GetAffection(GameStateManager.Felix);
        if (felix > friendMax && friendMax < GameStateManager.RomanceThreshold) return "felix";
        if (friendMax < GameStateManager.RomanceThreshold) return "group";
        foreach (string friend in _firstConversationOrder)
            if (Mathf.IsEqualApprox(state.GetAffection(friend), friendMax)) return friend;
        foreach (string friend in friends)
            if (Mathf.IsEqualApprox(state.GetAffection(friend), friendMax)) return friend;
        return "group";
    }

    private bool EchoOn(Switch plate)
    {
        if (plate == null) return false;
        foreach (Entity entity in Entities)
            if (entity is Echo && entity.IsAlive && entity.Visible &&
                entity.WorldPosition.DistanceTo(plate.Position) <= plate.PlateRadius) return true;
        return false;
    }

    private bool PlayerOn(Switch plate) => plate != null && Player != null && Player.IsAlive &&
        Player.WorldPosition.DistanceTo(plate.Position) <= plate.PlateRadius;

    private T Add<T>(T node, string name, Vector2 position) where T : Node2D
    {
        node.Name = name; node.Position = position; AddChild(node); return node;
    }

    private void Floor(string name, float x, float y, float width, float height) => Add(new FloorMarker
    {
        Size = S(width, height), DrawBox = false, Fill = new Color(0.3f, 0.3f, 0.35f, 0f),
        Outline = new Color(0.3f, 0.3f, 0.35f, 0f)
    }, name, P(x, y));

    private void Wall(string name, float x, float y, float width, float height) => Add(new Obstacle
    {
        Size = S(width, height), DrawBox = false
    }, name, P(x, y));

    private static Vector2 P(float x, float y) => new(x * MapScale, y * MapScale);
    private static Vector2 S(float width, float height) => new(width * MapScale, height * MapScale);
}
