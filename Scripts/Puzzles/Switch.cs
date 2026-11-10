using Godot;

<<<<<<< Updated upstream
/// <summary>
/// A puzzle switch that controls a linked Door. Implements IInteractable AND IUpdatable.
///
/// Two modes (set IsPressurePlate in the Inspector):
///  - Lever (default): the player/Echo presses E nearby to activate it. StaysOn=true means
///    it latches on until the loop resets.
///  - Pressure plate: active ONLY while a living Player or Echo stands on it. This is the
///    classic time-loop trick: an Echo that finished its replay standing on the plate keeps
///    the door open while the live player walks through.
///
/// Decoupling: the Switch never calls the Door directly while running - it emits
/// ActivationChanged and the Door listens (wired once in _Ready).
/// </summary>
public partial class Switch : Node2D, IInteractable, IUpdatable
=======
public partial class Switch : Node2D, IInteractable, IHeldInteractable, IUpdatable
>>>>>>> Stashed changes
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
<<<<<<< Updated upstream
=======
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
>>>>>>> Stashed changes
    #endregion

    #region Public properties
    /// <summary>Path (set in the Inspector) to the Door this switch controls.</summary>
    [Export] public NodePath LinkedDoorPath { get; set; } = new NodePath();
    [Export] public bool IsPressurePlate { get => _isPressurePlate; set { _isPressurePlate = value; QueueRedraw(); } }
    [Export] public bool StaysOn { get => _staysOn; set => _staysOn = value; }
    [Export] public float PlateRadius { get => _plateRadius; set { _plateRadius = Mathf.Max(4f, value); QueueRedraw(); } }

<<<<<<< Updated upstream
    public bool IsActivated => _isActivated;
    public Door LinkedDoor => _linkedDoor;

    /// <summary>A switch is a small circle, so its own position is the interaction point.</summary>
    public float DistanceTo(Vector2 from) => from.DistanceTo(Position);
=======
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
>>>>>>> Stashed changes
    #endregion

    #region Lifecycle
    public override void _Ready()
    {
        _room = GetParent() as Room;
        if (!LinkedDoorPath.IsEmpty) LinkDoor(GetNodeOrNull<Door>(LinkedDoorPath));
        QueueRedraw();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (GameStateManager.Instance != null && GameStateManager.Instance.IsPaused) return;
        Update((float)delta);
    }

    /// <summary>Connects this switch to a door (also usable from code).</summary>
    public void LinkDoor(Door door)
    {
        if (_linkedDoor != null) ActivationChanged -= _linkedDoor.OnSwitchChanged;
        _linkedDoor = door;
        if (_linkedDoor != null) ActivationChanged += _linkedDoor.OnSwitchChanged;
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
            bool canPress = entity is Player || entity is Echo;
            if (canPress && entity.IsAlive && entity.WorldPosition.DistanceTo(Position) <= _plateRadius)
            {
                occupied = true;
                break;
            }
        }
        SetActivated(occupied);
    }
    #endregion

    #region IInteractable (lever logic)
    public void Interact(Entity interactor)
    {
<<<<<<< Updated upstream
        if (_isPressurePlate) return;   // plates react to weight, not to hands
        SetActivated(_staysOn ? true : !_isActivated);
    }
=======
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
>>>>>>> Stashed changes
    #endregion

    #region State
    /// <summary>The only place the state changes - so the signal is never forgotten.</summary>
    private void SetActivated(bool value)
    {
        if (_isActivated == value) return;
        _isActivated = value;
        QueueRedraw();
        EmitSignal(SignalName.ActivationChanged, _isActivated);
    }

    /// <summary>Called by the Room when a new loop starts.</summary>
    public void ResetState()
    {
<<<<<<< Updated upstream
=======
        _sequenceIndex = _sequenceStartIndex;   // a restart puts the consoles back to their codes
        _holdElapsed = 0f;
        _lastHoldClock = -1f;
        _activeRemaining = 0f;
>>>>>>> Stashed changes
        SetActivated(false);
    }
    #endregion

    #region Drawing
    public override void _Draw()
    {
        Color fill = _isActivated ? new Color(0.3f, 0.9f, 0.3f) : new Color(0.85f, 0.75f, 0.2f);
        float radius = _isPressurePlate ? _plateRadius : 12f;
        DrawCircle(Vector2.Zero, radius, fill);
        DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 32, new Color(0.2f, 0.2f, 0.2f), 2f);
    }
    #endregion
}
