using System.Collections.Generic;
using System.Text;
using Godot;

public partial class Room : Node2D
{
    #region Signals
    [Signal] public delegate void LoopStartedEventHandler(int loopNumber);
    [Signal] public delegate void RoomCompletedEventHandler();
    [Signal] public delegate void PuzzleStateChangedEventHandler(string switchName, bool isActivated);
    #endregion

    #region Private state (composition: the lists)
    private readonly List<Entity> _entities = new();
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
    private float _maxCaptureSeconds = 5f;
    private bool _loopReplay;
    private float _echoLifetimeSeconds = 20f;
    private string _introDialogueId = "intro";
    private string _clearDialogueId = "room01_clear";
    private string _completionFlag = "room01_complete";
    private bool _showDebugHud = true;
    private int _levelNumber = 1;
    private string _nextLevelPath = "";
    private string _demoEchoDialogueId = "";

    private float _loopClock;
    private int _loopNumber = 1;
    private bool _isComplete;
    private bool _transitionStarted;
    private DialogueManager _dialogue;
    private Pendant _pendant;
    private ColorRect _hudPanel;         // F3-toggled debug readout
    private Label _hudLabel;
    private Label _controlsLabel;
    private Label _bannerLabel;          // always on: GAME OVER / ROOM CLEAR
    private Label _recordLabel;          // always on while a capture window is open
    private ColorRect _recordBarBg;
    private ColorRect _recordBarFill;
    private Label _objectiveLabel;       // always on once an Objective has been read
    private Label _echoTimerLabel;       // always on while any Echo is still alive
    #endregion

    #region Public properties
    [Export] public Vector2 RoomSize { get => _roomSize; set { _roomSize = value; QueueRedraw(); } }
    [Export] public Vector2 PlayerStartPosition { get => _playerStart; set => _playerStart = value; }
    [Export] public Vector2 ExitPosition { get => _exitPosition; set { _exitPosition = value; QueueRedraw(); } }
    [Export] public Vector2 ExitSize { get => _exitSize; set { _exitSize = value; QueueRedraw(); } }
    [Export] public int MaxEchoes { get => _maxEchoes; set => _maxEchoes = Mathf.Max(1, value); }

    // How long a single capture window may run before it closes itself. A puzzle never needs more
    // than a few seconds of the past, and the cap keeps the Echo's frame list small.
    [Export] public float MaxCaptureSeconds { get => _maxCaptureSeconds; set => _maxCaptureSeconds = Mathf.Max(0.5f, value); }

    // Default LoopReplay for every Echo this room spawns: false keeps an Echo standing where its
    // recording ended (Room01/Room02, so it can hold a plate); true makes it repeat (the Atrium).
    [Export] public bool LoopReplay { get => _loopReplay; set => _loopReplay = value; }

    // How long each projected Echo lasts before it decays and the room reclaims it. 0 = no limit.
    // Keep it comfortably longer than the walk its puzzle asks for: once the Echo is gone, the
    // plate it was holding comes up and the door it was propping open closes behind the player.
    [Export] public float EchoLifetimeSeconds { get => _echoLifetimeSeconds; set => _echoLifetimeSeconds = Mathf.Max(0f, value); }
    [Export] public string IntroDialogueId { get => _introDialogueId; set => _introDialogueId = value ?? ""; }
    [Export] public string ClearDialogueId { get => _clearDialogueId; set => _clearDialogueId = value ?? ""; }
    [Export] public string CompletionFlag { get => _completionFlag; set => _completionFlag = value ?? ""; }
    [Export] public bool ShowDebugHud { get => _showDebugHud; set => _showDebugHud = value; }

    [Export] public int LevelNumber { get => _levelNumber; set => _levelNumber = Mathf.Max(1, value); }

    [Export] public string NextLevelPath { get => _nextLevelPath; set => _nextLevelPath = value ?? ""; }

    // When this conversation finishes, the room projects a scripted demo Echo in front of the
    // player (Scene 6's "a blue light appeared in front of you"). Empty = no demo.
    [Export] public string DemoEchoDialogueId { get => _demoEchoDialogueId; set => _demoEchoDialogueId = value ?? ""; }

    public IReadOnlyList<Entity> Entities => _entities;
    public IReadOnlyList<Echo> Echoes => _echoes;
    public IReadOnlyList<Obstacle> Obstacles => _obstacles;
    public IReadOnlyList<Switch> Switches => _switches;
    public IReadOnlyList<Door> Doors => _doors;
    public IReadOnlyList<Inspectable> Inspectables => _inspectables;
    public Player Player { get; private set; }

    public DialogueManager Dialogue => _dialogue;

    public float LoopClock => _loopClock;
    public int LoopNumber => _loopNumber;
    public bool IsComplete => _isComplete;

    public bool IsFinalLevel => string.IsNullOrEmpty(_nextLevelPath);

    /// <summary>True when a flag reset puts the pendant back in the world here, because this room
    /// has one lying on the floor. Only Room01 does; every later level is past it, so a restart
    /// there must not take the Echo ability away with the flags (see Player.RestartRun).</summary>
    public bool HasPendantOnTheFloor => _pendant != null;

    public float StepSeconds => 1f / Engine.PhysicsTicksPerSecond;

    public Rect2 ExitBounds => new Rect2(_exitPosition, _exitSize);
    #endregion

    #region Lifecycle
    public override void _Ready()
    {
        // Start at -1 tick so the first physics step brings the clock to exactly 0.
        _loopClock = -StepSeconds;

        RegisterChildren();
        GameStateManager.Instance?.RegisterRoom(this);
        GameStateManager.Instance?.RegisterLevel(_levelNumber);

        foreach (Entity entity in _entities.ToArray()) entity.EnterRoom(this);   // Player is placed at the start

        _dialogue = GetNodeOrNull<DialogueManager>("DialogueManager");
        if (_dialogue != null) _dialogue.DialogueEnded += OnDialogueEnded;       // level hand-over waits for it
        BuildHud();
        QueueRedraw();

        if (_introDialogueId != "") _dialogue?.StartDialogue(_introDialogueId);
    }

    private void RegisterChildren()
    {
        foreach (Node child in GetChildren())
        {
            switch (child)
            {
                case Player player: Player = player; _entities.Add(player); break;
                case Echo echo: _echoes.Add(echo); _entities.Add(echo); break;
                case Obstacle obstacle: _obstacles.Add(obstacle); break;
                case Switch sw:
                    _switches.Add(sw);
                    _interactables.Add(sw);
                    sw.ActivationChanged += isActive => EmitSignal(SignalName.PuzzleStateChanged, sw.Name.ToString(), isActive);
                    break;
                case Door door: _doors.Add(door); _interactables.Add(door); break;
                case Inspectable inspectable: _inspectables.Add(inspectable); _interactables.Add(inspectable); break;
                case Pendant pendant: _pendant = pendant; _interactables.Add(pendant); break;
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (GameStateManager.Instance != null && GameStateManager.Instance.IsPaused) return;

        // The Room is the PARENT, so Godot runs this BEFORE any child's _PhysicsProcess:
        // every entity sees the same clock value within one tick.
        _loopClock += (float)delta;

        if (!_isComplete && Player != null && Player.IsAlive && ExitBounds.Intersects(Player.Bounds))
            CompleteRoom();
    }

    public override void _Process(double delta)
    {
        if (_hudLabel != null) _hudLabel.Text = BuildHudText();
        RefreshBanner();
        RefreshRecordBar();
        RefreshEchoTimer();
        RefreshObjective();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_hudPanel == null) return;
        if (!(@event is InputEventKey key) || !key.Pressed || key.IsEcho() || key.Keycode != Key.F3) return;

        // F3 hides the DEBUG readout only. The recording bar and the banner are player-facing
        // and stay on screen whatever F3 is set to.
        bool show = !_hudPanel.Visible;
        _hudPanel.Visible = show;
        _controlsLabel.Visible = show;
        RefreshBanner();
        GetViewport()?.SetInputAsHandled();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, _roomSize), new Color(0.09f, 0.09f, 0.13f));
        DrawRect(ExitBounds, new Color(0.2f, 0.8f, 0.3f, 0.35f));
        DrawRect(ExitBounds, new Color(0.3f, 1f, 0.4f), false, 2f);
    }
    #endregion

    #region Time loop control
    public void StartNewLoop()
    {
        _loopNumber++;
        _loopClock = -StepSeconds;
        _isComplete = false;

        ResetPuzzles();
        foreach (Echo echo in _echoes) echo.StartReplay();

        EmitSignal(SignalName.LoopStarted, _loopNumber);
    }

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
        echo.LoopReplay = _loopReplay;                 // the replay style for the echoes it spawns...
        echo.LifetimeSeconds = _echoLifetimeSeconds;   // ...and how long each projection lasts

        // ...and the same airtime the player jumps with, so a recorded leap clears the same step
        // in the replay that it cleared in the recording.
        if (Player != null) echo.JumpAirtimeSeconds = Player.JumpAirtimeSeconds;

        echo.StartReplay();     // anchor the window to the clock; it replays forward from here
        OnEchoProjected(echo);  // ...and now the room gets the last word on the projection
        _echoes.Add(echo);
        _entities.Add(echo);
    }

    /// <summary>Called once per projection, after its window has been anchored to the clock, so a
    /// room can refine it. RoomAtrium overrides this: only its stairway wants a repeating window,
    /// because a projection that repeats walks itself off a plate.</summary>
    protected virtual void OnEchoProjected(Echo echo) { }

    /// <summary>Reclaim a single Echo: it has decayed, or the room is thinning them out. Guarded so
    /// a deferred removal that arrives after the room was already cleared is a no-op.</summary>
    public void RemoveEcho(Echo echo)
    {
        if (echo == null || !GodotObject.IsInstanceValid(echo)) return;

        _echoes.Remove(echo);
        _entities.Remove(echo);
        echo.QueueFree();
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

    /// <summary>Puts every puzzle back to its starting state. A subclass overrides this to clear
    /// the pieces of state only it knows about - its one-shot beats, its stairway stains, the item
    /// a character dropped - so an R-restart never leaves a solved room behind.</summary>
    protected virtual void ResetPuzzles()
    {
        foreach (Switch sw in _switches) sw.ResetState();
        foreach (Door door in _doors) door.Close();
    }
    #endregion

    #region Queries used by entities and puzzles
    public bool IsBlocked(Rect2 bounds)
    {
        if (!new Rect2(Vector2.Zero, _roomSize).Encloses(bounds)) return true;

        foreach (Obstacle obstacle in _obstacles)
            if (obstacle.IsSolid && bounds.Intersects(obstacle.Bounds)) return true;

        foreach (Door door in _doors)
            if (!door.IsOpen && bounds.Intersects(door.Bounds)) return true;

        // Furniture the player can examine is solid too, so the room reads like the blueprint.
        foreach (Inspectable inspectable in _inspectables)
            if (inspectable.Solid && bounds.Intersects(inspectable.Bounds)) return true;

        return false;
    }

    public IInteractable FindNearestInteractable(Vector2 from, float radius)
    {
        IInteractable best = null;
        float bestDistance = radius;
        foreach (IInteractable candidate in _interactables)
        {
            // A hidden interactable is off the board: Yoruka once she has stormed off, a terminal
            // the player has not reached yet. Hiding the node is all it takes to disable it.
            if (candidate is Node2D node && !node.Visible) continue;

            float distance = candidate.DistanceTo(from);
            if (distance <= bestDistance) { best = candidate; bestDistance = distance; }
        }
        return best;
    }
    #endregion

    #region Completion
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

    private void OnDialogueEnded(string dialogueId)
    {
        if (dialogueId != "" && dialogueId == _demoEchoDialogueId) SpawnDemoEcho();
        if (_isComplete) Callable.From(BeginLevelTransition).CallDeferred();
    }

    /// <summary>Scene 6: after the pendant's memory flash the room projects a translucent copy of
    /// Aionia that repeats the "touches its head" beat. It is an ordinary Echo built from ordinary
    /// ActionFrames - no special-case physics and no player input involved.</summary>
    private void SpawnDemoEcho()
    {
        if (Player == null) return;
        if (GameStateManager.Instance?.GetFlag("echo_demo_spawned") ?? false) return;
        GameStateManager.Instance?.SetFlag("echo_demo_spawned", true);

        Vector2 forward = Player.FacingDirection == Vector2.Zero ? Vector2.Right : Player.FacingDirection;
        Vector2 spot = Player.WorldPosition + forward * 76f;

        float t0 = _loopClock;
        var frames = new List<ActionFrame>
        {
            new ActionFrame(t0, spot, PlayerAction.Idle),
            new ActionFrame(t0 + 0.4f, spot, PlayerAction.Interact),   // it touches its head
            new ActionFrame(t0 + 1.0f, spot, PlayerAction.Idle),
            new ActionFrame(t0 + 1.8f, spot, PlayerAction.Idle)
        };

        Echo echo = Player.EchoScene != null ? Player.EchoScene.Instantiate<Echo>() : new Echo();
        echo.Initialize(frames);
        AddEcho(echo);
    }

    private void BeginLevelTransition()
    {
        if (_transitionStarted || IsFinalLevel) return;
        if (GameStateManager.Instance == null) return;

        _transitionStarted = true;
        GameStateManager.Instance.GoToNextLevel(_nextLevelPath);
    }
    #endregion

    #region Placeholder HUD (built in code, no assets)
    private void BuildHud()
    {
        CanvasLayer layer = new CanvasLayer { Layer = 5 };

        // --- recording bar: drains while a capture window is open, F3 or no F3 ---
        _recordBarBg = new ColorRect
        {
            Color = new Color(0.04f, 0.05f, 0.08f, 0.8f),
            Position = new Vector2(14f, 592f),
            Size = new Vector2(300f, 20f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        _recordBarFill = new ColorRect
        {
            Color = new Color(0.35f, 0.95f, 1f, 0.9f),
            Position = new Vector2(2f, 2f),
            Size = new Vector2(296f, 16f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
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
            Size = new Vector2(1152f, 26f),
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        _objectiveLabel.AddThemeFontSizeOverride("font_size", 15);
        _objectiveLabel.AddThemeColorOverride("font_color", new Color(1f, 0.86f, 0.45f));
        _objectiveLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _objectiveLabel.AddThemeConstantOverride("outline_size", 4);

        _bannerLabel = new Label
        {
            Position = new Vector2(0f, 70f),
            Size = new Vector2(1152f, 36f),
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        _bannerLabel.AddThemeFontSizeOverride("font_size", 24);
        _bannerLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.35f));
        _bannerLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _bannerLabel.AddThemeConstantOverride("outline_size", 5);

        layer.AddChild(_recordBarBg);
        layer.AddChild(_recordLabel);
        layer.AddChild(_echoTimerLabel);
        layer.AddChild(_objectiveLabel);
        layer.AddChild(_bannerLabel);

        // --- the debug readout is the only part F3 hides ---
        if (_showDebugHud)
        {
            _hudPanel = new ColorRect
            {
                Color = new Color(0.04f, 0.05f, 0.08f, 0.62f),
                Position = new Vector2(14f, 12f),
                Size = new Vector2(360f, 128f)
            };

            _hudLabel = new Label
            {
                Position = new Vector2(12f, 9f),
                Size = new Vector2(336f, 112f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _hudLabel.AddThemeFontSizeOverride("font_size", 13);
            _hudLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.87f, 0.93f));
            _hudPanel.AddChild(_hudLabel);

            _controlsLabel = new Label
            {
                Position = new Vector2(14f, 618f),
                Size = new Vector2(1124f, 22f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _controlsLabel.Text = "WASD move   ·   Space jump   ·   T record/stop   ·   E interact   ·   J attack   ·   R project echo   ·   Enter dialogue   ·   1/2 choose   ·   F3 hide HUD   ·   F11 fullscreen";
            _controlsLabel.AddThemeFontSizeOverride("font_size", 12);
            _controlsLabel.AddThemeColorOverride("font_color", new Color(0.62f, 0.66f, 0.72f));
            _controlsLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.8f));
            _controlsLabel.AddThemeConstantOverride("outline_size", 3);

            layer.AddChild(_hudPanel);
            layer.AddChild(_controlsLabel);
        }

        AddChild(layer);
    }

    private void RefreshBanner()
    {
        if (_bannerLabel == null) return;

        string text = "";
        if (Player != null && Player.IsGameOver) text = "GAME OVER - press R to restart";
        else if (_isComplete) text = IsFinalLevel ? "ALL LEVELS CLEAR" : "ROOM CLEAR";

        _bannerLabel.Text = text;
        _bannerLabel.Visible = text != "";
    }

    private void RefreshRecordBar()
    {
        if (_recordBarBg == null) return;

        bool recording = Player != null && Player.IsRecording;
        _recordBarBg.Visible = recording;
        _recordLabel.Visible = recording;
        if (!recording) return;

        float window = Mathf.Max(0.01f, _maxCaptureSeconds);
        float left = Player.CaptureSecondsLeft;
        float ratio = Mathf.Clamp(left / window, 0f, 1f);

        _recordBarFill.Size = new Vector2(296f * ratio, 16f);
        _recordBarFill.Color = ratio < 0.25f
            ? new Color(1f, 0.5f, 0.4f, 0.95f)
            : new Color(0.35f, 0.95f, 1f, 0.9f);
        _recordLabel.Text = "REC   " + left.ToString("0.0") + "s left of " + window.ToString("0.#") + "s";
    }

    private void RefreshEchoTimer()
    {
        if (_echoTimerLabel == null) return;

        if (_echoes.Count == 0 || _echoLifetimeSeconds <= 0f)
        {
            _echoTimerLabel.Visible = false;
            return;
        }

        float soonest = SoonestEchoSeconds();
        _echoTimerLabel.Visible = true;
        _echoTimerLabel.Text = "ECHO   " + soonest.ToString("0.0") + "s left   (" +
                               _echoes.Count + "/" + _maxEchoes + ")";
        _echoTimerLabel.AddThemeColorOverride("font_color", soonest < _echoLifetimeSeconds * 0.25f
            ? new Color(1f, 0.6f, 0.4f)
            : new Color(0.55f, 0.95f, 1f));
    }

    private float SoonestEchoSeconds()
    {
        float soonest = float.MaxValue;
        foreach (Echo echo in _echoes) soonest = Mathf.Min(soonest, echo.SecondsLeft);
        return soonest;
    }

    private void RefreshObjective()
    {
        if (_objectiveLabel == null) return;

        string objective = _dialogue != null ? _dialogue.CurrentObjective : "";
        _objectiveLabel.Visible = objective != "";
        _objectiveLabel.Text = objective == "" ? "" : "OBJECTIVE:   " + objective;
    }

    private string BuildHudText()
    {
        if (Player == null) return "";

        var sb = new StringBuilder();
        sb.AppendLine("AIONIA AVGERINOS  ·  LEVEL " + _levelNumber + (IsFinalLevel ? " (FINAL)" : ""));
        sb.AppendLine("HP " + Player.Health + "/" + Player.MaxHealth +
                      "  ·  Lives " + Player.Lives +
                      "  ·  Loop " + _loopNumber +
                      "  ·  Echoes " + _echoes.Count + "/" + _maxEchoes + DescribeEchoTimers());
        sb.AppendLine(DescribeCapture() + "  ·  " + Mathf.Max(0f, _loopClock).ToString("0.0") + "s");
        sb.AppendLine("Plates " + CountActiveSwitches() + "/" + _switches.Count +
                      "  ·  Doors " + CountOpenDoors() + "/" + _doors.Count +
                      "  ·  Memories " + UnlockedFragmentCount + "/" + TotalFragmentCount);
        sb.AppendLine(DescribeAffection());
        sb.AppendLine("Flags: " + ShortenList(ActiveFlags(), 42));
        return sb.ToString();
    }

    private string DescribeCapture()
    {
        if (!Player.HasPendant) return "No pendant - Echo locked";
        if (Player.IsRecording) return "Capture ON - " + Player.CaptureSecondsLeft.ToString("0.0") + "s left";
        if (Player.CapturedFrameCount > 0) return "Captured " + Player.CapturedFrameCount + " frames - R to project";
        return "Capture off - press T to record";
    }

    // Debug readout: the romance counters the dialogue choices move.
    private static string DescribeAffection()
    {
        GameStateManager state = GameStateManager.Instance;
        if (state == null) return "Affection: -";

        return "Affection: Yoruka " + state.GetAffection(GameStateManager.Yoruka) +
               "  ·  Adellaine " + state.GetAffection(GameStateManager.Adellaine) +
               "  ·  Hesiod " + state.GetAffection(GameStateManager.Hesiod) +
               "  ·  Felix " + state.GetAffection(GameStateManager.Felix);
    }

    // Debug readout: how long the room will keep each projection.
    private string DescribeEchoTimers()
    {
        if (_echoes.Count == 0 || _echoLifetimeSeconds <= 0f) return "";
        return " (next out " + SoonestEchoSeconds().ToString("0.0") + "s)";
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

    private List<string> ActiveFlags()
    {
        var flags = new List<string>();
        if (GameStateManager.Instance == null) return flags;

        foreach (KeyValuePair<string, bool> pair in GameStateManager.Instance.Flags)
            if (pair.Value) flags.Add(pair.Key);
        return flags;
    }

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
