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

    private float _loopClock;
    private int _loopNumber = 1;
    private bool _isComplete;
    private bool _transitionStarted;
    private DialogueManager _dialogue;
    private ColorRect _hudPanel;
    private Label _hudLabel;
    private Label _bannerLabel;
    private Label _controlsLabel;
    #endregion

    #region Public properties
    [Export] public Vector2 RoomSize { get => _roomSize; set { _roomSize = value; QueueRedraw(); } }
    [Export] public Vector2 PlayerStartPosition { get => _playerStart; set => _playerStart = value; }
    [Export] public Vector2 ExitPosition { get => _exitPosition; set { _exitPosition = value; QueueRedraw(); } }
    [Export] public Vector2 ExitSize { get => _exitSize; set { _exitSize = value; QueueRedraw(); } }
    [Export] public int MaxEchoes { get => _maxEchoes; set => _maxEchoes = Mathf.Max(1, value); }
    [Export] public string IntroDialogueId { get => _introDialogueId; set => _introDialogueId = value ?? ""; }
    [Export] public string ClearDialogueId { get => _clearDialogueId; set => _clearDialogueId = value ?? ""; }
    [Export] public string CompletionFlag { get => _completionFlag; set => _completionFlag = value ?? ""; }
    [Export] public bool ShowDebugHud { get => _showDebugHud; set => _showDebugHud = value; }

    [Export] public int LevelNumber { get => _levelNumber; set => _levelNumber = Mathf.Max(1, value); }

    [Export] public string NextLevelPath { get => _nextLevelPath; set => _nextLevelPath = value ?? ""; }

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
        if (_showDebugHud) BuildDebugHud();
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
                case Pendant pendant: _interactables.Add(pendant); break;
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
        if (_hudPanel == null) return;
        _hudLabel.Text = BuildHudText();
        RefreshBanner();
    }

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
        foreach (Enemy enemy in _enemies) enemy.ResetForNewLoop();
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
        echo.StartReplay();     // anchor the window to the clock; it replays forward from here
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
    public bool IsBlocked(Rect2 bounds)
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
        if (_isComplete) Callable.From(BeginLevelTransition).CallDeferred();
    }

    private void BeginLevelTransition()
    {
        if (_transitionStarted || IsFinalLevel) return;
        if (GameStateManager.Instance == null) return;

        _transitionStarted = true;
        GameStateManager.Instance.GoToNextLevel(_nextLevelPath);
    }
    #endregion

    #region Debug HUD (placeholder UI)
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
        _hudLabel.AddThemeFontSizeOverride("font_size", 13);
        _hudLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.87f, 0.93f));
        _hudPanel.AddChild(_hudLabel);

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

        _controlsLabel = new Label
        {
            Position = new Vector2(14f, 618f),
            Size = new Vector2(1124f, 22f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _controlsLabel.Text = "WASD move   ·   T record/stop   ·   E interact   ·   J attack   ·   R project echo   ·   Enter dialogue   ·   1/2 choose   ·   F3 hide HUD   ·   F11 fullscreen";
        _controlsLabel.AddThemeFontSizeOverride("font_size", 12);
        _controlsLabel.AddThemeColorOverride("font_color", new Color(0.62f, 0.66f, 0.72f));
        _controlsLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.8f));
        _controlsLabel.AddThemeConstantOverride("outline_size", 3);

        layer.AddChild(_hudPanel);
        layer.AddChild(_bannerLabel);
        layer.AddChild(_controlsLabel);
        AddChild(layer);
    }

    private void RefreshBanner()
    {
        string text = "";
        if (Player != null && Player.IsGameOver) text = "GAME OVER - press R to restart";
        else if (_isComplete) text = IsFinalLevel ? "ALL LEVELS CLEAR" : "ROOM CLEAR";

        _bannerLabel.Text = text;
        _bannerLabel.Visible = text != "" && _hudPanel.Visible;
    }

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

    private string DescribeCapture()
    {
        if (Player.IsRecording) return "Capture ON - T to stop";
        if (Player.CapturedFrameCount > 0) return "Captured " + Player.CapturedFrameCount + " frames - R to project";
        return "Capture off - press T to record";
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
