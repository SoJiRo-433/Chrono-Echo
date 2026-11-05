using System.Collections.Generic;
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
    private string _approachDialogueId = "";
    private string _approachFlag = "";
    private float _approachRadius = 140f;
    private Room _room;
    private bool _approachFired;
    private float _closeDelaySeconds;
    private float _closeTimer = -1f;
    private static Font _countdownFont;
    private bool _needsAllLinkedSwitches;
    private bool _latchOpen;
    private bool _latched;
    private readonly List<Switch> _linkedSwitches = new();
    #endregion

    #region Public properties
    [Export] public Vector2 Size { get => _size; set { _size = value; QueueRedraw(); } }
    [Export] public bool RequiresSwitch { get => _requiresSwitch; set => _requiresSwitch = value; }

    [Export] public string OpenedFlag { get => _openedFlag; set => _openedFlag = value ?? ""; }

    // A door can introduce itself: the first time the player comes close - or steps on the plate
    // linked to it - this one-shot conversation plays and records that it happened.
    [Export] public string ApproachDialogueId { get => _approachDialogueId; set => _approachDialogueId = value ?? ""; }
    [Export] public string ApproachFlag { get => _approachFlag; set => _approachFlag = value ?? ""; }
    [Export] public float ApproachRadius { get => _approachRadius; set => _approachRadius = Mathf.Max(0f, value); }

    // Seconds the door waits after its plate is released before it slams. 0 = it closes the
    // instant the weight comes off (Room01/Room02). The Atrium uses 5, so the Echo has to be
    // the thing holding the plate while the player walks through.
    [Export] public float CloseDelaySeconds { get => _closeDelaySeconds; set => _closeDelaySeconds = Mathf.Max(0f, value); }

    // A door can be wired to a whole set of plates instead of one. Level 2's bridgeway door is:
    // both plates have to be weighed down at the same moment before it opens.
    [Export] public bool NeedsAllLinkedSwitches { get => _needsAllLinkedSwitches; set => _needsAllLinkedSwitches = value; }

    // When true the door opens for good the first time its condition is met. Level 2's bridgeway
    // needs this: Aionia has to stand on a plate to open the door, so the door cannot demand she
    // stay there afterwards.
    [Export] public bool LatchOpen { get => _latchOpen; set => _latchOpen = value; }

    public bool IsCountingDown => _closeTimer > 0f;
    public float SecondsUntilClose => Mathf.Max(0f, _closeTimer);

    /// <summary>Called by each Switch as it wires itself up, so a door does not have to go looking
    /// for its plates - and so the order the nodes happen to be ready in does not matter.</summary>
    public void RegisterSwitch(Switch linked)
    {
        if (linked != null && !_linkedSwitches.Contains(linked)) _linkedSwitches.Add(linked);
    }

    public bool IsOpen => _isOpen;
    public Rect2 Bounds => new Rect2(Position, _size);

    public float DistanceTo(Vector2 from) => Bounds.DistanceToPoint(from);
    #endregion

    #region Lifecycle
    public override void _Ready() { _room = GetParent() as Room; }

    public override void _PhysicsProcess(double delta)
    {
        if (GameStateManager.Instance != null && GameStateManager.Instance.IsPaused) return;

        TickCloseDelay((float)delta);

        Player player = _room?.Player;
        if (player == null || !player.IsAlive) return;
        if (Bounds.DistanceToPoint(player.WorldPosition) <= _approachRadius) NotifyApproached();
    }

    private void TickCloseDelay(float delta)
    {
        if (_closeTimer <= 0f) return;

        _closeTimer -= delta;
        if (_closeTimer <= 0f)
        {
            _closeTimer = -1f;
            Close();
        }
        QueueRedraw();   // the countdown bar drains on the door itself
    }

    /// <summary>Fires the one-shot "first seen" line. A linked plate calls this too, so the
    /// player gets the hint from whichever end of the puzzle they reach first.</summary>
    public void NotifyApproached()
    {
        if (_approachFired || _approachDialogueId == "") return;
        _approachFired = true;

        if (_approachFlag != "") GameStateManager.Instance?.SetFlag(_approachFlag, true);
        _room?.Dialogue?.StartDialogue(_approachDialogueId);
    }
    #endregion

    #region State changes
    public void Open()
    {
        _closeTimer = -1f;   // weight is back on: call off the slam
        if (_isOpen) return;
        _isOpen = true;
        QueueRedraw();
        EmitSignal(SignalName.DoorStateChanged, true);
        if (_openedFlag != "") GameStateManager.Instance?.SetFlag(_openedFlag, true);
    }

    public void Close()
    {
        _latched = false;   // a room reset puts a latched door back to needing its plates again
        if (!_isOpen) return;
        _isOpen = false;
        QueueRedraw();
        EmitSignal(SignalName.DoorStateChanged, false);
    }

    public void OnSwitchChanged(bool isActivated)
    {
        if (_needsAllLinkedSwitches)
        {
            if (AllLinkedSwitchesActive())
            {
                Open();
                if (_latchOpen) _latched = true;
                return;
            }

            if (_latched) return;   // opened for good; letting go does not shut it again

            if (_closeDelaySeconds > 0f)
            {
                _closeTimer = _closeDelaySeconds;
                QueueRedraw();
                return;
            }

            Close();
            return;
        }

        if (isActivated)
        {
            Open();
            return;
        }

        // Losing the weight does not always slam the door: with a delay it waits, so a walking
        // Echo gets a window and the player can still be on the wrong side of it.
        if (_closeDelaySeconds > 0f)
        {
            _closeTimer = _closeDelaySeconds;
            QueueRedraw();
            return;
        }

        Close();
    }

    /// <summary>True only when every plate wired to this door is weighed down right now. A door
    /// with no plates wired to it never satisfies this.</summary>
    private bool AllLinkedSwitchesActive()
    {
        if (_linkedSwitches.Count == 0) return false;

        foreach (Switch linked in _linkedSwitches)
            if (!linked.IsActivated) return false;

        return true;
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

        if (_closeTimer <= 0f) return;

        // The countdown is drawn on the door: a bar draining downward plus the seconds left.
        float ratio = _closeDelaySeconds > 0f ? Mathf.Clamp(_closeTimer / _closeDelaySeconds, 0f, 1f) : 0f;
        DrawRect(new Rect2(0f, 0f, _size.X, _size.Y * ratio), new Color(1f, 0.55f, 0.2f, 0.45f));

        if (_countdownFont == null)
            _countdownFont = ResourceLoader.Load<Font>("res://Assets/Fonts/NotoSerifJP-VariableFont_wght.ttf");

        if (_countdownFont != null)
            DrawString(_countdownFont, new Vector2(0f, -6f), Mathf.CeilToInt(_closeTimer).ToString(),
                       HorizontalAlignment.Left, -1f, 18, Colors.Orange);
    }
    #endregion
}
