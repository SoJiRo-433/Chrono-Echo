using Godot;

public partial class Switch : Node2D, IInteractable, IHeldInteractable, IUpdatable
{
    #region Signals
    [Signal] public delegate void ActivationChangedEventHandler(bool isActivated);
    #endregion

    #region Private state
    private bool _isActivated;
    private bool _isPressurePlate;
    private bool _staysOn = true;
    private float _plateRadius = 28f;
    private Door _linkedDoor;
    private Room _room;
    private string _flagToSet = "";
    private string[] _sequencePool = System.Array.Empty<string>();
    private int _sequenceIndex;
    private int _sequenceStartIndex;
    private float _requiredHoldSeconds;
    private float _holdDurationSeconds;
    private float _holdElapsed;
    private float _activeRemaining;
    private float _lastHoldClock = -1f;
    private bool _requireHeldInteraction;
    private static Font _consoleFont;
    #endregion

    #region Public properties
    [Export] public NodePath LinkedDoorPath { get; set; } = new NodePath();

    // Recorded the first time this switch turns on. Rooms use it for things a door cannot say:
    // the Atrium's counterweight that keeps the next stretch of bridge solid, for instance.
    [Export] public string FlagToSet { get => _flagToSet; set => _flagToSet = value ?? ""; }

    [Export] public bool IsPressurePlate { get => _isPressurePlate; set { _isPressurePlate = value; QueueRedraw(); } }
    [Export] public bool StaysOn { get => _staysOn; set => _staysOn = value; }
    [Export] public float PlateRadius { get => _plateRadius; set { _plateRadius = Mathf.Max(4f, value); QueueRedraw(); } }

    // How much room answers to E on this switch. A plate reacts to weight rather than to hands, so
    // this only ever decides which device the player meant - and a box beats a point for that.
    [Export] public Vector2 InteractionSize { get; set; } = new Vector2(26f, 26f);
    [Export] public float RequiredHoldSeconds { get => _requiredHoldSeconds; set => _requiredHoldSeconds = Mathf.Max(0f, value); }
    [Export] public float HoldActivationSeconds { get => _holdDurationSeconds; set => _holdDurationSeconds = Mathf.Max(0f, value); }
    [Export] public bool RequireHeldInteraction { get => _requireHeldInteraction; set => _requireHeldInteraction = value; }

    // A console is a switch that also displays something: E steps it to its next sequence, and the
    // room can then ask whether two consoles are showing the same one. An empty pool is a plain
    // switch, which is every other switch in the game.
    [Export] public string[] SequencePool
    {
        get => _sequencePool;
        set { _sequencePool = value ?? System.Array.Empty<string>(); QueueRedraw(); }
    }

    /// <summary>Which sequence in the pool this console starts on. A restart puts it back here.</summary>
    [Export] public int SequenceIndex { get => _sequenceIndex; set { _sequenceIndex = value; QueueRedraw(); } }

    public bool IsConsole => _sequencePool.Length > 0;

    /// <summary>The sequence this console is showing right now; empty when it is not a console.
    /// Clamped on read, so the order the two exports arrive in does not matter.</summary>
    public string CurrentSequence =>
        _sequencePool.Length == 0 ? "" : _sequencePool[Mathf.PosMod(_sequenceIndex, _sequencePool.Length)];

    public bool IsActivated => _isActivated;
    public Door LinkedDoor => _linkedDoor;

    public Rect2 InteractionBounds => new Rect2(Position - InteractionSize * 0.5f, InteractionSize);

    public float DistanceTo(Vector2 from) => InteractionBounds.DistanceToPoint(from);
    #endregion

    #region Lifecycle
    public override void _Ready()
    {
        _room = GetParent() as Room;
        _sequenceStartIndex = _sequenceIndex;   // the code a reset returns this console to
        if (!LinkedDoorPath.IsEmpty) LinkDoor(GetNodeOrNull<Door>(LinkedDoorPath));
        QueueRedraw();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (GameStateManager.Instance != null && GameStateManager.Instance.IsPaused) return;
        Update((float)delta);
    }

    public void LinkDoor(Door door)
    {
        if (_linkedDoor != null) ActivationChanged -= _linkedDoor.OnSwitchChanged;
        _linkedDoor = door;

        if (_linkedDoor != null)
        {
            _linkedDoor.RegisterSwitch(this);   // so a door can require several plates at once
            ActivationChanged += _linkedDoor.OnSwitchChanged;
        }
    }
    #endregion

    #region IUpdatable (pressure plate logic)
    public void Update(float delta)
    {
        if (_room == null) return;

        if (_lastHoldClock >= 0f && _room.LoopClock - _lastHoldClock > _room.StepSeconds * 1.5f)
        {
            _lastHoldClock = -1f;
            _holdElapsed = 0f;
            if (!_isPressurePlate && _holdDurationSeconds <= 0f) SetActivated(false);
        }
        if (_activeRemaining > 0f && _isActivated && _lastHoldClock < 0f)
        {
            _activeRemaining = Mathf.Max(0f, _activeRemaining - delta);
            if (_activeRemaining <= 0f) SetActivated(false);
        }

        if (!_isPressurePlate) return;

        bool occupied = false;
        foreach (Entity entity in _room.Entities)
        {
            // A hidden entity is off the board: an Echo that has decayed releases the plate on the
            // same tick it goes, rather than a tick later when the room gets round to freeing it.
            bool canPress = entity is Player || entity is Echo;
            if (canPress && entity.IsAlive && entity.Visible &&
                entity.WorldPosition.DistanceTo(Position) <= _plateRadius)
            {
                occupied = true;
                break;
            }
        }
        SetActivated(occupied);

        // Standing on the plate counts as reaching the puzzle from its other end.
        if (occupied) _linkedDoor?.NotifyApproached();
    }
    #endregion

    #region IInteractable (lever logic)
    public void Interact(Entity interactor)
    {
        if (_requireHeldInteraction) return;
        // A console answers hands first: E steps it to its next code. What is *left* on it is
        // still weight, exactly like a plate - the player sets the code, the Echo holds it.
        if (IsConsole) { AdvanceSequence(); return; }

        if (_isPressurePlate) return;   // plates react to weight, not to hands
        SetActivated(_staysOn ? true : !_isActivated);
    }

    public void HoldInteract(Entity interactor, float delta)
    {
        if (!_requireHeldInteraction && _requiredHoldSeconds <= 0f) return;
        if (_isPressurePlate) return;
        _lastHoldClock = _room?.LoopClock ?? 0f;
        _holdElapsed += delta;
        if (_holdElapsed < _requiredHoldSeconds) return;
        if (_holdDurationSeconds > 0f) _activeRemaining = _holdDurationSeconds;
        SetActivated(true);
    }

    public void AdvanceSequence()
    {
        if (_sequencePool.Length == 0) return;
        _sequenceIndex = Mathf.PosMod(_sequenceIndex + 1, _sequencePool.Length);
        QueueRedraw();
    }
    #endregion

    #region State
    private void SetActivated(bool value)
    {
        if (_isActivated == value) return;
        _isActivated = value;
        QueueRedraw();
        EmitSignal(SignalName.ActivationChanged, _isActivated);

        if (_isActivated && _flagToSet != "") GameStateManager.Instance?.SetFlag(_flagToSet, true);
    }

    public void ResetState()
    {
        _sequenceIndex = _sequenceStartIndex;   // a restart puts the consoles back to their codes
        _holdElapsed = 0f;
        _lastHoldClock = -1f;
        _activeRemaining = 0f;
        SetActivated(false);
        QueueRedraw();
    }
    #endregion

    #region Drawing
    public override void _Draw()
    {
        Color fill = _isActivated ? new Color(0.3f, 0.9f, 0.3f) : new Color(0.85f, 0.75f, 0.2f);
        float radius = _isPressurePlate ? _plateRadius : 12f;
        DrawCircle(Vector2.Zero, radius, fill);
        DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 32, new Color(0.2f, 0.2f, 0.2f), 2f);

        if (IsConsole) DrawConsoleScreen();
    }

    /// <summary>The console's face. "Match the same sequence on both" has to be something the
    /// player can read off the room, so the four symbols are drawn above the plate - and the frame
    /// turns green while the console is weighed down.</summary>
    private void DrawConsoleScreen()
    {
        Rect2 screen = new Rect2(-34f, -46f, 68f, 26f);
        DrawRect(screen, new Color(0.05f, 0.09f, 0.12f, 0.95f));
        DrawRect(screen, _isActivated ? new Color(0.45f, 1f, 0.6f) : new Color(0.5f, 0.7f, 0.85f), false, 2f);

        if (_consoleFont == null)
            _consoleFont = ResourceLoader.Load<Font>("res://Assets/Fonts/NotoSerifJP-VariableFont_wght.ttf");
        if (_consoleFont == null) return;

        DrawString(_consoleFont, new Vector2(screen.Position.X + 6f, screen.Position.Y + 19f),
                   CurrentSequence, HorizontalAlignment.Center, screen.Size.X - 12f, 17,
                   _isActivated ? new Color(0.6f, 1f, 0.7f) : new Color(0.6f, 0.85f, 1f));
    }
    #endregion
}
