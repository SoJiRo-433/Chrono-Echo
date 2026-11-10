using System.Collections.Generic;
using System.Text;
using Godot;

/// <summary>
/// A playable room. The CONTAINER that ties every system together.
///
/// OOP roles:
///  - COMPOSITION: a Room HAS-A list of entities, obstacles, switches, doors (and echoes).
///    It discovers them from its child nodes in _Ready().
///  - Owns the shared LOOP CLOCK that keeps the recording and every Echo in sync.
///  - Answers questions for others: IsBlocked(), FindNearestInteractable().
///
/// The Room node must sit at position (0,0): all coordinates are room coordinates.
/// </summary>
public partial class Room : Node2D
{
    /// <summary>The single world-size multiplier for all six levels. Timers remain in seconds.</summary>
    public const float SCALE_FACTOR = 1.5f;

    /// <summary>How far a scripted camera pan zooms in while it holds.</summary>
    private const float PanZoom = 1.35f;
    #region Signals
    [Signal] public delegate void LoopStartedEventHandler(int loopNumber);
    [Signal] public delegate void RoomCompletedEventHandler();
    [Signal] public delegate void PuzzleStateChangedEventHandler(string switchName, bool isActivated);
    #endregion

    #region Private state (composition: the lists)
    private readonly List<Entity> _entities = new();
    private readonly List<Enemy> _enemies = new();
    private readonly List<Echo> _echoes = new();
    private readonly List<Obstacle> _obstacles = new();
    private readonly List<Switch> _switches = new();
    private readonly List<Door> _doors = new();
    private readonly List<Inspectable> _inspectables = new();
    private readonly List<IInteractable> _interactables = new();

    private Vector2 _roomSize = new Vector2(1152f, 648f);
    private Vector2 _playerStart = new Vector2(80f, 324f);
    private Vector2 _exitPosition = new Vector2(980f, 274f);
    private Vector2 _exitSize = new Vector2(120f, 100f);
    private int _maxEchoes = 3;
    private string _introDialogueId = "intro";
    private string _clearDialogueId = "room01_clear";
    private string _completionFlag = "room01_complete";
    private bool _showDebugHud = true;
    private int _levelNumber = 1;
    private string _nextLevelPath = "";
<<<<<<< Updated upstream
=======
    private string _exitLockedHint = "";
    private string _demoEchoDialogueId = "";
>>>>>>> Stashed changes

    private float _loopClock;
    private int _loopNumber = 1;
    private bool _isComplete;
    private bool _transitionStarted;
    private bool _exitLocked;
    private DialogueManager _dialogue;
<<<<<<< Updated upstream
    private ColorRect _hudPanel;
=======
    private Camera2D _camera;
    private Vector2 _cameraPanTarget;
    private float _cameraPanSecondsLeft;
    private Pendant _pendant;
    private ColorRect _hudPanel;         // F3-toggled debug readout
>>>>>>> Stashed changes
    private Label _hudLabel;
    private Label _bannerLabel;
    private Label _controlsLabel;
    #endregion

    #region Public properties
    [Export] public Vector2 RoomSize { get => _roomSize; set { _roomSize = value; QueueRedraw(); } }
    /// <summary>Logical room bounds used by the camera and collision checks before SCALE_FACTOR.</summary>
    [Export] public Vector2 WorldSize { get => _roomSize; set { _roomSize = value; QueueRedraw(); } }
    [Export] public Vector2 PlayerStartPosition { get => _playerStart; set => _playerStart = value; }
    [Export] public Vector2 ExitPosition { get => _exitPosition; set { _exitPosition = value; QueueRedraw(); } }
    [Export] public Vector2 ExitSize { get => _exitSize; set { _exitSize = value; QueueRedraw(); } }
    [Export] public int MaxEchoes { get => _maxEchoes; set => _maxEchoes = Mathf.Max(1, value); }
    [Export] public string IntroDialogueId { get => _introDialogueId; set => _introDialogueId = value ?? ""; }
    [Export] public string ClearDialogueId { get => _clearDialogueId; set => _clearDialogueId = value ?? ""; }
    [Export] public string CompletionFlag { get => _completionFlag; set => _completionFlag = value ?? ""; }
    [Export] public bool ShowDebugHud { get => _showDebugHud; set => _showDebugHud = value; }

    /// <summary>1-based level number, shown on the HUD and reported to the GameStateManager.</summary>
    [Export] public int LevelNumber { get => _levelNumber; set => _levelNumber = Mathf.Max(1, value); }

    /// <summary>
    /// Scene loaded once this level is finished (the player reached the exit). Leave it
    /// empty on the last level: the room then simply stays completed.
    /// </summary>
    [Export] public string NextLevelPath { get => _nextLevelPath; set => _nextLevelPath = value ?? ""; }

<<<<<<< Updated upstream
=======
    // A room whose exit is gated by what the level still wants done puts the reason here. Standing in
    // the green box then says what is outstanding instead of doing nothing at all.
    [Export] public string ExitLockedHint { get => _exitLockedHint; set => _exitLockedHint = value ?? ""; }

    // When this conversation finishes, the room projects a scripted demo Echo in front of the
    // player (Scene 6's "a blue light appeared in front of you"). Empty = no demo.
    [Export] public string DemoEchoDialogueId { get => _demoEchoDialogueId; set => _demoEchoDialogueId = value ?? ""; }

>>>>>>> Stashed changes
    public IReadOnlyList<Entity> Entities => _entities;
    public IReadOnlyList<Echo> Echoes => _echoes;
    public IReadOnlyList<Obstacle> Obstacles => _obstacles;
    public IReadOnlyList<Switch> Switches => _switches;
    public IReadOnlyList<Door> Doors => _doors;
    public IReadOnlyList<Inspectable> Inspectables => _inspectables;
    public Player Player { get; private set; }

    /// <summary>The room's DialogueManager, so scenery can start a conversation by id.</summary>
    public DialogueManager Dialogue => _dialogue;

    /// <summary>Seconds since this loop started. Negative for one tick right after a reset.</summary>
    public float LoopClock => _loopClock;
    public int LoopNumber => _loopNumber;
    public bool IsComplete => _isComplete;

    /// <summary>True when this room has no NextLevelPath, i.e. nothing follows it.</summary>
    public bool IsFinalLevel => string.IsNullOrEmpty(_nextLevelPath);

    /// <summary>Length of one physics tick in seconds (1/60).</summary>
    public float StepSeconds => 1f / Engine.PhysicsTicksPerSecond;

    public Rect2 ExitBounds => new Rect2(_exitPosition, _exitSize);
    public virtual float MovementScaleAt(Vector2 worldPosition) => 1f;

    /// <summary>Takes the camera off the player for a moment and parks it on a world point - the
    /// Library's scripted pan to Yoruka. It hands the camera back to the follow when it expires.
    /// A small zoom comes with it, because a room this small clamps the camera so hard that a
    /// pan alone would not read.</summary>
    public void PanCameraTo(Vector2 worldPosition, float seconds)
    {
        _cameraPanTarget = worldPosition;
        _cameraPanSecondsLeft = Mathf.Max(0.05f, seconds);
        if (_camera != null) _camera.Zoom = Vector2.One * PanZoom;
    }

    /// <summary>A reusable reset-and-respawn hook for hazards whose design resets a whole room.</summary>
    public void HandlePuzzleHit(string hazard)
    {
        ResetPuzzles();
        if (Player == null) return;
        Player.TakeScratch();
        Player.WorldPosition = _playerStart;
    }
    #endregion

    #region Lifecycle
    public override void _Ready()
    {
        // Start at -1 tick so the first physics step brings the clock to exactly 0.
        _loopClock = -StepSeconds;

        RegisterChildren();
        BuildRoomCamera();
        GameStateManager.Instance?.RegisterRoom(this);
        GameStateManager.Instance?.RegisterLevel(_levelNumber);

        foreach (Entity entity in _entities.ToArray()) entity.EnterRoom(this);   // Player starts recording here

        _dialogue = GetNodeOrNull<DialogueManager>("DialogueManager");
        if (_dialogue != null) _dialogue.DialogueEnded += OnDialogueEnded;       // level hand-over waits for it
        if (_showDebugHud) BuildDebugHud();
        QueueRedraw();

        if (_introDialogueId != "") _dialogue?.StartDialogue(_introDialogueId);
    }

    /// <summary>Sorts the child nodes into the Room's typed lists.</summary>
    private void RegisterChildren()
    {
        foreach (Node child in GetChildren())
        {
            switch (child)
            {
                case Player player: Player = player; _entities.Add(player); break;
                case Enemy enemy: _enemies.Add(enemy); _entities.Add(enemy); break;
                case Echo echo: _echoes.Add(echo); _entities.Add(echo); break;
                case Obstacle obstacle: _obstacles.Add(obstacle); break;
                case Switch sw:
                    _switches.Add(sw);
                    _interactables.Add(sw);
                    sw.ActivationChanged += isActive => EmitSignal(SignalName.PuzzleStateChanged, sw.Name.ToString(), isActive);
                    break;
                case Door door: _doors.Add(door); _interactables.Add(door); break;
                case Inspectable inspectable: _inspectables.Add(inspectable); _interactables.Add(inspectable); break;
<<<<<<< Updated upstream
                case Pendant pendant: _interactables.Add(pendant); break;
=======
                case Pendant pendant: _pendant = pendant; _interactables.Add(pendant); break;
                case Node2D node when node is IInteractable interactable: _interactables.Add(interactable); break;
>>>>>>> Stashed changes
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (GameStateManager.Instance != null && GameStateManager.Instance.IsPaused) return;

        // The Room is the PARENT, so Godot runs this BEFORE any child's _PhysicsProcess:
        // every entity sees the same clock value within one tick.
        _loopClock += (float)delta;

        if (!_isComplete && CanCompleteRoom() && Player != null && Player.IsAlive && ExitBounds.Intersects(Player.Bounds))
            CompleteRoom();
    }

    /// <summary>True while she stands in an exit that is still gated, so the banner can say why it
    /// did nothing. Worked out here rather than in the physics step because the banner has to keep
    /// up even while a conversation has the game paused - Level 6's border starts one on the way in.</summary>
    private void RefreshExitLocked()
    {
        _exitLocked = !_isComplete && Player != null && Player.IsAlive
                      && ExitBounds.Intersects(Player.Bounds) && !CanCompleteRoom();
    }

    protected virtual bool CanCompleteRoom() => true;

    public override void _Process(double delta)
    {
<<<<<<< Updated upstream
        if (_hudPanel == null) return;
        _hudLabel.Text = BuildHudText();
=======
        UpdateRoomCamera((float)delta);
        RefreshExitLocked();
        if (_hudLabel != null) _hudLabel.Text = BuildHudText();
>>>>>>> Stashed changes
        RefreshBanner();
    }

<<<<<<< Updated upstream
    /// <summary>F3 hides the overlay, so a screenshot is not covered in debug text.</summary>
=======
    /// <summary>Creates the world camera independently of the CanvasLayer HUD and dialogue UI.</summary>
    private void BuildRoomCamera()
    {
        _camera = new Camera2D
        {
            Name = "RoomCamera",
            Enabled = true,
            PositionSmoothingEnabled = true,
            PositionSmoothingSpeed = 5f
        };
        AddChild(_camera);
        if (Player != null) _camera.Position = Player.WorldPosition;
        _camera.MakeCurrent();
    }

    private void UpdateRoomCamera(float delta)
    {
        if (_camera == null || Player == null) return;

        // A scripted pan keeps the camera for a moment and then gives it back to the follow.
        if (_cameraPanSecondsLeft > 0f)
        {
            _cameraPanSecondsLeft = Mathf.Max(0f, _cameraPanSecondsLeft - delta);
            _camera.Position = _camera.Position.Lerp(ClampCamera(_cameraPanTarget), Mathf.Min(1f, delta * 5f));
            return;
        }

        _camera.Position = ClampCamera(Player.WorldPosition);
        if (!_camera.Zoom.IsEqualApprox(Vector2.One))
            _camera.Zoom = _camera.Zoom.Lerp(Vector2.One, Mathf.Min(1f, delta * 4f));
    }

    /// <summary>Keeps the camera inside the room, whatever it is looking at.</summary>
    private Vector2 ClampCamera(Vector2 target)
    {
        Vector2 viewport = GetViewportRect().Size / SCALE_FACTOR;
        Vector2 half = viewport * 0.5f;
        target.X = _roomSize.X <= viewport.X ? _roomSize.X * 0.5f : Mathf.Clamp(target.X, half.X, _roomSize.X - half.X);
        target.Y = _roomSize.Y <= viewport.Y ? _roomSize.Y * 0.5f : Mathf.Clamp(target.Y, half.Y, _roomSize.Y - half.Y);
        return target;
    }

    public override void _EnterTree()
    {
        Scale = Vector2.One * SCALE_FACTOR;
    }

>>>>>>> Stashed changes
    public override void _UnhandledInput(InputEvent @event)
    {
        if (_hudPanel == null) return;
        if (!(@event is InputEventKey key) || !key.Pressed || key.IsEcho() || key.Keycode != Key.F3) return;

        bool show = !_hudPanel.Visible;
        _hudPanel.Visible = show;
        _controlsLabel.Visible = show;
        RefreshBanner();
        GetViewport()?.SetInputAsHandled();
    }

    public override void _Draw()
    {
        Vector2 visibleWorld = GetViewportRect().Size / SCALE_FACTOR;
        Vector2 bgSize = new Vector2(Mathf.Max(_roomSize.X, visibleWorld.X), Mathf.Max(_roomSize.Y, visibleWorld.Y));
        Vector2 bgPosition = (_roomSize - bgSize) * 0.5f;
        DrawRect(new Rect2(bgPosition, bgSize), new Color(0.09f, 0.09f, 0.13f));
        DrawRect(ExitBounds, new Color(0.2f, 0.8f, 0.3f, 0.35f));
        DrawRect(ExitBounds, new Color(0.3f, 1f, 0.4f), false, 2f);
    }
    #endregion

    #region Time loop control
    /// <summary>
    /// Starts a new loop: rewinds the clock, resets puzzles and enemies, and makes every
    /// Echo replay from the beginning. Called by Player.ResetTimeLoop().
    /// </summary>
    public void StartNewLoop()
    {
        _loopNumber++;
        _loopClock = -StepSeconds;
        _isComplete = false;

        ResetPuzzles();
        foreach (Enemy enemy in _enemies) enemy.ResetForNewLoop();
        foreach (Echo echo in _echoes) echo.StartReplay();

        EmitSignal(SignalName.LoopStarted, _loopNumber);
    }

    /// <summary>Adds a new Echo. If there are too many, the oldest one is removed.</summary>
    public void AddEcho(Echo echo)
    {
        if (_echoes.Count >= _maxEchoes)
        {
            Echo oldest = _echoes[0];
            _echoes.RemoveAt(0);
            _entities.Remove(oldest);
            oldest.QueueFree();
        }

        AddChild(echo);
        echo.EnterRoom(this);
        _echoes.Add(echo);
        _entities.Add(echo);
    }

    public void ClearEchoes()
    {
        foreach (Echo echo in _echoes)
        {
            _entities.Remove(echo);
            echo.QueueFree();
        }
        _echoes.Clear();
    }

    private void ResetPuzzles()
    {
        foreach (Switch sw in _switches) sw.ResetState();
        foreach (Door door in _doors) door.Close();
    }
    #endregion

    #region Queries used by entities and puzzles
<<<<<<< Updated upstream
    /// <summary>True if 'bounds' leaves the room or touches an obstacle or a CLOSED door.</summary>
    public bool IsBlocked(Rect2 bounds)
=======
    public virtual bool IsBlocked(Rect2 bounds)
>>>>>>> Stashed changes
    {
        if (!new Rect2(Vector2.Zero, _roomSize).Encloses(bounds)) return true;

        foreach (Obstacle obstacle in _obstacles)
            if (bounds.Intersects(obstacle.Bounds)) return true;

        foreach (Door door in _doors)
            if (!door.IsOpen && bounds.Intersects(door.Bounds)) return true;

        // Furniture the player can examine is solid too, so the room reads like the blueprint.
        foreach (Inspectable inspectable in _inspectables)
            if (inspectable.Solid && bounds.Intersects(inspectable.Bounds)) return true;

        return false;
    }

    /// <summary>Nearest switch/door within 'radius' of 'from', or null.</summary>
    public IInteractable FindNearestInteractable(Vector2 from, float radius)
    {
        IInteractable best = null;
        float bestDistance = radius;
        foreach (IInteractable candidate in _interactables)
        {
            float distance = candidate.DistanceTo(from);
            if (distance <= bestDistance) { best = candidate; bestDistance = distance; }
        }
        return best;
    }
    #endregion

    #region Completion
    /// <summary>
    /// The player reached the exit. In Room01 that means the pressure plate was touched and
    /// the door it opens was walked through; the room then hands over to the next level.
    /// </summary>
    private void CompleteRoom()
    {
        _isComplete = true;
        bool firstTime = !(GameStateManager.Instance?.GetFlag(_completionFlag) ?? false);
        GameStateManager.Instance?.SetFlag(_completionFlag, true);
        EmitSignal(SignalName.RoomCompleted);

        // The closing conversation pauses the game; leave only once it has been read.
        bool dialogueStarted = firstTime && _clearDialogueId != "" && _dialogue != null
                               && _dialogue.StartDialogue(_clearDialogueId);

        if (!dialogueStarted) BeginLevelTransition();
    }

    /// <summary>
    /// The closing dialogue finished: leave the level. Deferred on purpose, the same way
    /// Player.Die() defers its respawn - this signal arrives from inside the DialogueManager's
    /// own EndDialogue(), so changing the scene right here would free the DialogueManager
    /// while it is still part way through its call stack.
    /// </summary>
    private void OnDialogueEnded(string dialogueId)
    {
        if (_isComplete) Callable.From(BeginLevelTransition).CallDeferred();
    }

    /// <summary>Loads the next level, if there is one. Safe to call more than once.</summary>
    private void BeginLevelTransition()
    {
        if (_transitionStarted || IsFinalLevel) return;
        if (GameStateManager.Instance == null) return;

        _transitionStarted = true;
        GameStateManager.Instance.GoToNextLevel(_nextLevelPath);
    }
    #endregion

    #region Debug HUD (placeholder UI)
    /// <summary>Compact overlay: one status panel, a centred banner, and a controls footer.</summary>
    private void BuildDebugHud()
    {
        CanvasLayer layer = new CanvasLayer { Layer = 5 };

        _hudPanel = new ColorRect
        {
            Color = new Color(0.04f, 0.05f, 0.08f, 0.62f),
            Position = new Vector2(14f, 12f),
            Size = new Vector2(360f, 104f)
        };

        _hudLabel = new Label
        {
            Position = new Vector2(12f, 9f),
            Size = new Vector2(336f, 88f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
<<<<<<< Updated upstream
        _hudLabel.AddThemeFontSizeOverride("font_size", 13);
        _hudLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.87f, 0.93f));
        _hudPanel.AddChild(_hudLabel);
=======
        _recordBarBg.AddChild(_recordBarFill);

        _recordLabel = new Label
        {
            Position = new Vector2(324f, 590f),
            Size = new Vector2(460f, 24f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        _recordLabel.AddThemeFontSizeOverride("font_size", 13);
        _recordLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.97f, 1f));
        _recordLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _recordLabel.AddThemeConstantOverride("outline_size", 4);

        // --- echo timer: the soonest projection to decay, so the room's clock stays legible ---
        _echoTimerLabel = new Label
        {
            Position = new Vector2(14f, 564f),
            Size = new Vector2(520f, 24f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        _echoTimerLabel.AddThemeFontSizeOverride("font_size", 13);
        _echoTimerLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.95f, 1f));
        _echoTimerLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _echoTimerLabel.AddThemeConstantOverride("outline_size", 4);

        // --- objective: replaces "Name: line" once an Objective line has been read ---
        _objectiveLabel = new Label
        {
            Position = new Vector2(0f, 6f),
            Size = new Vector2(GetViewportRect().Size.X, 26f),
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        _objectiveLabel.AddThemeFontSizeOverride("font_size", 15);
        _objectiveLabel.AddThemeColorOverride("font_color", new Color(1f, 0.86f, 0.45f));
        _objectiveLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _objectiveLabel.AddThemeConstantOverride("outline_size", 4);
>>>>>>> Stashed changes

        _bannerLabel = new Label
        {
            Position = new Vector2(0f, 70f),
            Size = new Vector2(GetViewportRect().Size.X, 36f),
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        _bannerLabel.AddThemeFontSizeOverride("font_size", 24);
        _bannerLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.35f));
        _bannerLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _bannerLabel.AddThemeConstantOverride("outline_size", 5);

        _controlsLabel = new Label
        {
            Position = new Vector2(14f, 618f),
            Size = new Vector2(1124f, 22f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _controlsLabel.Text = "WASD move   ·   T capture   ·   E interact   ·   J attack   ·   R reset loop   ·   Enter dialogue   ·   1/2 choose   ·   F3 hide HUD";
        _controlsLabel.AddThemeFontSizeOverride("font_size", 12);
        _controlsLabel.AddThemeColorOverride("font_color", new Color(0.62f, 0.66f, 0.72f));
        _controlsLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.8f));
        _controlsLabel.AddThemeConstantOverride("outline_size", 3);

        layer.AddChild(_hudPanel);
        layer.AddChild(_bannerLabel);
        layer.AddChild(_controlsLabel);
        AddChild(layer);
    }

    /// <summary>Keeps the banner to the one thing that matters right now, or hides it.</summary>
    private void RefreshBanner()
    {
        string text = "";
        if (Player != null && Player.IsGameOver) text = "GAME OVER - press R to restart";
        else if (_isComplete) text = IsFinalLevel && _levelNumber == 6 ? "LEVEL 6 CLEAR - to be continued" : IsFinalLevel ? "ALL LEVELS CLEAR" : "ROOM CLEAR";
        else if (_exitLocked && _exitLockedHint != "") text = _exitLockedHint;

        _bannerLabel.Text = text;
        _bannerLabel.Visible = text != "" && _hudPanel.Visible;
    }

    /// <summary>Five short lines. The flag list is trimmed, so it can never cover the screen.</summary>
    private string BuildHudText()
    {
        if (Player == null) return "";

        var sb = new StringBuilder();
        sb.AppendLine("AIONIA AVGERINOS  ·  LEVEL " + _levelNumber + (IsFinalLevel ? " (FINAL)" : ""));
        sb.AppendLine("HP " + Player.Health + "/" + Player.MaxHealth +
                      "  ·  Lives " + Player.Lives +
                      "  ·  Loop " + _loopNumber +
                      "  ·  Echoes " + _echoes.Count + "/" + _maxEchoes);
        sb.AppendLine(DescribeCapture() + "  ·  " + Mathf.Max(0f, _loopClock).ToString("0.0") + "s");
        sb.AppendLine("Plates " + CountActiveSwitches() + "/" + _switches.Count +
                      "  ·  Doors " + CountOpenDoors() + "/" + _doors.Count +
                      "  ·  Memories " + UnlockedFragmentCount + "/" + TotalFragmentCount);
        sb.AppendLine("Flags: " + ShortenList(ActiveFlags(), 42));
        return sb.ToString();
    }

    /// <summary>What the capture window is doing right now, in as few words as possible.</summary>
    private string DescribeCapture()
    {
        if (Player.IsRecording) return "Capture ON";
        if (Player.CapturedFrameCount > 0) return "Capture held, " + Player.CapturedFrameCount + " frames";
        return "Capture off - press T";
    }

    private int CountActiveSwitches()
    {
        int count = 0;
        foreach (Switch sw in _switches) if (sw.IsActivated) count++;
        return count;
    }

    private int CountOpenDoors()
    {
        int count = 0;
        foreach (Door door in _doors) if (door.IsOpen) count++;
        return count;
    }

    private int UnlockedFragmentCount => _dialogue != null ? _dialogue.UnlockedFragments.Count : 0;
    private int TotalFragmentCount => _dialogue != null ? _dialogue.FragmentCount : 0;

    /// <summary>Every story flag that is currently true.</summary>
    private List<string> ActiveFlags()
    {
        var flags = new List<string>();
        if (GameStateManager.Instance == null) return flags;

        foreach (KeyValuePair<string, bool> pair in GameStateManager.Instance.Flags)
            if (pair.Value) flags.Add(pair.Key);
        return flags;
    }

    /// <summary>
    /// Joins as many entries as fit in 'budget' characters, then reports how many were left
    /// out ("a, b +7"). Keeps the flag line to one line however much story is unlocked.
    /// </summary>
    private static string ShortenList(List<string> items, int budget)
    {
        if (items.Count == 0) return "-";

        var sb = new StringBuilder();
        int shown = 0;
        for (int i = 0; i < items.Count; i++)
        {
            string piece = (i == 0 ? "" : ", ") + items[i];
            int hidden = items.Count - i - 1;
            string tail = hidden > 0 ? " +" + hidden : "";
            if (sb.Length + piece.Length + tail.Length > budget) break;

            sb.Append(piece);
            shown++;
        }

        if (shown == 0) return "+" + items.Count;
        if (shown < items.Count) sb.Append(" +").Append(items.Count - shown);
        return sb.ToString();
    }
    #endregion
}
