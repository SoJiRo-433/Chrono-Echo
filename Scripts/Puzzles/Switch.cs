using Godot;

/// <summary>
/// A puzzle switch that controls a linked Door. Implements IInteractable AND IUpdatable.
///
/// Two modes (set IsPressurePlate in the Inspector):
///  - Lever (default): the player/Echo presses E nearby to activate it. StaysOn=true means
///    it latches on until it is reset (which now only happens on a full restart).
///  - Pressure plate: active ONLY while a living Player or Echo stands on it. This is the
///    classic time-loop trick: an Echo that finished its replay standing on the plate keeps
///    the door open while the live player walks through.
///
/// Decoupling: the Switch never calls the Door directly while running - it emits
/// ActivationChanged and the Door listens (wired once in _Ready).
/// </summary>
public partial class Switch : Node2D, IInteractable, IUpdatable
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
    #endregion

    #region Public properties
    /// <summary>Path (set in the Inspector) to the Door this switch controls.</summary>
    [Export] public NodePath LinkedDoorPath { get; set; } = new NodePath();
    [Export] public bool IsPressurePlate { get => _isPressurePlate; set { _isPressurePlate = value; QueueRedraw(); } }
    [Export] public bool StaysOn { get => _staysOn; set => _staysOn = value; }
    [Export] public float PlateRadius { get => _plateRadius; set { _plateRadius = Mathf.Max(4f, value); QueueRedraw(); } }

    public bool IsActivated => _isActivated;
    public Door LinkedDoor => _linkedDoor;

    /// <summary>A switch is a small circle, so its own position is the interaction point.</summary>
    public float DistanceTo(Vector2 from) => from.DistanceTo(Position);
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
        if (!_isPressurePlate || _room == null) return;

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
        if (_isPressurePlate) return;   // plates react to weight, not to hands
        SetActivated(_staysOn ? true : !_isActivated);
    }
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

    /// <summary>Called by the Room on a full restart (after a game over).</summary>
    public void ResetState()
    {
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
