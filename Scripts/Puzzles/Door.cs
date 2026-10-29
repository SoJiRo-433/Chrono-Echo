using Godot;

public partial class Door : Node2D, IInteractable
{
    #region Signals
    [Signal] public delegate void DoorStateChangedEventHandler(bool isOpen);
    #endregion

    #region Private state
    private bool _isOpen;
    private Vector2 _size = new Vector2(32f, 128f);
    private bool _requiresSwitch = true;
    private string _openedFlag = "";
    #endregion

    #region Public properties
    [Export] public Vector2 Size { get => _size; set { _size = value; QueueRedraw(); } }
    [Export] public bool RequiresSwitch { get => _requiresSwitch; set => _requiresSwitch = value; }

    [Export] public string OpenedFlag { get => _openedFlag; set => _openedFlag = value ?? ""; }

    public bool IsOpen => _isOpen;
    public Rect2 Bounds => new Rect2(Position, _size);

    public float DistanceTo(Vector2 from) => Bounds.DistanceToPoint(from);
    #endregion

    #region State changes
    public void Open()
    {
        if (_isOpen) return;
        _isOpen = true;
        QueueRedraw();
        EmitSignal(SignalName.DoorStateChanged, true);
        if (_openedFlag != "") GameStateManager.Instance?.SetFlag(_openedFlag, true);
    }

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;
        QueueRedraw();
        EmitSignal(SignalName.DoorStateChanged, false);
    }

    public void OnSwitchChanged(bool isActivated)
    {
        if (isActivated) Open(); else Close();
    }
    #endregion

    #region IInteractable
    public void Interact(Entity interactor)
    {
        if (_requiresSwitch)
        {
            GD.Print("The door is sealed. Something else must open it.");
            return;
        }
        if (_isOpen) Close(); else Open();
    }
    #endregion

    #region Drawing
    public override void _Draw()
    {
        if (_isOpen)
            DrawRect(new Rect2(Vector2.Zero, _size), new Color(0.3f, 0.8f, 0.3f, 0.8f), false, 2f);   // open: green outline
        else
            DrawRect(new Rect2(Vector2.Zero, _size), new Color(0.55f, 0.3f, 0.1f));                    // closed: solid brown
    }
    #endregion
}
