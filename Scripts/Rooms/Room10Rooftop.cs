using Godot;

/// <summary>The final rooftop: walk to Aionia's last Echo, hear the companions, then choose the
/// fate of the timeline at the central console.</summary>
public partial class Room10Rooftop : Room
{
    private const float MapScale = 2f;
    private const string MapPath = "res://Assets/level10_rooftop_map.png";
    private Inspectable _lastEcho, _console;
    private Inspectable[] _companions;
    private string _lockedRoute = "";
    private int _fragmentCount;
    private bool _endingTransitionStarted;

    public override void _Ready()
    {
        BuildMap();
        RoomSize = WorldSize = new Vector2(1280, 720);
        PlayerStartPosition = P(320, 338);
        ExitPosition = P(320, 180); ExitSize = S(48, 28);
        LevelNumber = 10; NextLevelPath = "res://Scenes/EndingScreen.tscn";
        MaxEchoes = 1; MaxCaptureSeconds = 15; EchoLifetimeSeconds = 15; LoopReplay = false;
        IntroDialogueId = "level10_intro";
        CompletionFlag = "level10_complete"; ExitLockedHint = "REACH THE FINAL CHOICE AT THE CONSOLE";
        base._Ready();

        _fragmentCount = CountUnlockedFragments();
        _lastEcho.FirstDialogueId = _fragmentCount >= 8 ? "level10_echo_many" :
            _fragmentCount < 6 ? "level10_echo_few" : "level10_echo_standard";
        _lastEcho.RepeatDialogueId = "level10_echo_repeat";
        _lockedRoute = DetermineRoute();
        AssignCompanionDialogues();
        if (GameStateManager.Instance?.CompletedRunBefore == true && _lockedRoute == GameStateManager.Felix && _fragmentCount >= 8)
        {
            GameStateManager.Instance.SetFlag("true_ending_unlocked", true);
            _console.FirstDialogueId = "level10_final_choice_replay";
            _console.RepeatDialogueId = "level10_final_choice_replay";
        }
        Dialogue?.SetObjective("Reach the last Echo at the center of the rooftop.");
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        if (GameStateManager.Instance?.IsPaused == true) return;
        if (!_endingTransitionStarted && CanCompleteRoom())
        {
            _endingTransitionStarted = true;
            CompleteRoom();
            return;
        }
        bool echoSpoken = GameStateManager.Instance?.GetFlag("level10_echo_spoken") == true;
        if (_fragmentCount >= 6 && echoSpoken)
            foreach (Inspectable companion in _companions) companion.Visible = true;

        if (AllCompanionsAnswered() && GameStateManager.Instance?.GetFlag("level10_companions_done") != true)
        {
            GameStateManager.Instance?.SetFlag("level10_companions_done", true);
            Dialogue?.SetObjective("Choose ERASE, REPAIR, or leave time untouched at the console.");
        }
        _console.Visible = _fragmentCount >= 6 && GameStateManager.Instance?.GetFlag("level10_companions_done") == true;

        if (_fragmentCount < 6 && echoSpoken && GameStateManager.Instance?.GetFlag("final_choice_do_nothing") != true)
        {
            GameStateManager.Instance?.SetFlag("final_choice_do_nothing", true);
        }
    }

    protected override bool CanCompleteRoom() =>
        (GameStateManager.Instance?.GetFlag("final_choice_true_ending") == true ||
         GameStateManager.Instance?.GetFlag("final_choice_erase") == true ||
         GameStateManager.Instance?.GetFlag("final_choice_repair") == true ||
         GameStateManager.Instance?.GetFlag("final_choice_do_nothing") == true) && Dialogue?.IsActive != true;

    private void BuildMap()
    {
        Texture2D map = ResourceLoader.Load<Texture2D>(MapPath);
        if (map != null) Add(new Sprite2D { Texture = map, Centered = false, Scale = Vector2.One * MapScale,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest }, "RooftopBackdrop", Vector2.Zero);

        // Keep the player inside the stone rooftop parapet border.
        Wall("ParapetNorth", 0, 0, 640, 8);
        Wall("ParapetSouth", 0, 352, 640, 8);
        Wall("ParapetWest", 0, 8, 8, 344);
        Wall("ParapetEast", 632, 8, 8, 344);

        // The supplied art paints these concentric chronal rings; dotted markers carry them into play.
        foreach (var ring in new[] { (d: 472f, color: new Color(.78f, .86f, 1f, .55f)),
                     (d: 340f, color: new Color(.33f, .65f, 1f, .62f)),
                     (d: 190f, color: new Color(1f, .56f, .24f, .72f)) })
            Add(new FloorMarker { Size = S(ring.d, ring.d), DottedRing = true,
                Outline = ring.color }, "ChronalRing" + GetChildCount(), P(320f - ring.d / 2f, 180f - ring.d / 2f));

        Add(new FloorMarker { Caption = "NOWHERE / ANYPLACE / ALL AT ONCE", Size = S(180, 15), DrawBox = false },
            "RooftopCaption", P(230, 22));
        _lastEcho = Add(new Inspectable { Caption = "LAST ECHO OF AIONIA (E)", Size = S(34, 28), DrawBox = false,
            FirstDialogueId = "level10_echo_standard", RepeatDialogueId = "level10_echo_repeat" }, "LastEcho", P(300, 157));

        _companions = new[]
        {
            Companion("Yoruka", "MOCHIZUKI YORUKA", P(193, 163)),
            Companion("Adellaine", "ADELLAINE MORIARTY", P(448, 163)),
            Companion("Hesiod", "HESIOD MORIARTY", P(193, 201)),
            Companion("Felix", "FELIX LASAREWITCH", P(448, 201))
        };
        foreach (Inspectable companion in _companions) companion.Visible = false;

        _console = Add(new Inspectable { Caption = "ERASE / REPAIR CONSOLE (E)", Size = S(44, 32), DrawBox = false,
            RequiredFlag = "level10_companions_done", FirstDialogueId = "level10_final_choice",
            RepeatDialogueId = "level10_final_choice" }, "FinalConsole", P(320, 183));
        _console.Visible = false;
        Add(new FloorMarker { Caption = "THE FINAL CHOICE", Size = S(90, 14), DrawBox = false }, "ChoiceLabel", P(275, 210));
    }

    private Inspectable Companion(string id, string caption, Vector2 position) => Add(new Inspectable
    {
        Caption = caption + " (E)", Size = S(34, 26), DrawBox = false,
        RequiredFlag = "level10_echo_spoken", FlagToSet = "level10_spoke_" + id.ToLowerInvariant(),
        FirstDialogueId = "level10_companion_" + id.ToLowerInvariant() + "_short",
        RepeatDialogueId = "level10_companion_repeat",
        CharacterSheetPath = CharacterSprite.FrontSpritePath(id)
    }, "Companion" + id, position);

    private void AssignCompanionDialogues()
    {
        foreach (Inspectable character in _companions)
        {
            string id = character.Name.ToString().Substring("Companion".Length).ToLowerInvariant();
            character.FirstDialogueId = id == _lockedRoute
                ? "level10_companion_" + id + "_route"
                : "level10_companion_" + id + "_short";
        }
    }

    private string DetermineRoute()
    {
        GameStateManager state = GameStateManager.Instance;
        if (state == null) return "";

        // Read the route locked by the Level 7 night watch, instead of re-deriving from affection
        string[] priority = { GameStateManager.Felix, GameStateManager.Yoruka, GameStateManager.Adellaine, GameStateManager.Hesiod };
        foreach (string id in priority)
        {
            if (state.GetFlag("level7_route_locked_" + id.ToLowerInvariant()) == true)
                return id;
        }
        return "";
    }

    private static int CountUnlockedFragments()
    {
        string[] flags = { "touched_pendant", "room01_complete", "asked_identity", "library_complete",
            "atrium_window_seen", "F4_scan", "F5_footage", "F6_ledger", "F7_camera_roll",
            "F8_piano_roll", "F9_resonance", "F10_divergence" };
        int count = 0;
        foreach (string flag in flags) if (GameStateManager.Instance?.GetFlag(flag) == true) count++;
        return count;
    }

    private bool AllCompanionsAnswered() =>
        GameStateManager.Instance?.GetFlag("level10_spoke_yoruka") == true &&
        GameStateManager.Instance?.GetFlag("level10_spoke_adellaine") == true &&
        GameStateManager.Instance?.GetFlag("level10_spoke_hesiod") == true &&
        GameStateManager.Instance?.GetFlag("level10_spoke_felix") == true;

    private T Add<T>(T node, string name, Vector2 position) where T : Node2D
    { node.Name = name; node.Position = position; AddChild(node); return node; }
    private void Wall(string name, float x, float y, float w, float h) => Add(new Obstacle { Size = S(w, h), DrawBox = false }, name, P(x, y));
    private static Vector2 P(float x, float y) => new(x * MapScale, y * MapScale);
    private static Vector2 S(float x, float y) => new(x * MapScale, y * MapScale);
}
