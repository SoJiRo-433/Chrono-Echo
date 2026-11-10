using Godot;

/// <summary>A floor tile that phases in and out on the room's shared LoopClock. There are two
/// kinds and they are opposites:
///
///  - a DEADLY tile (the Atrium's blue ones) is an obstacle. While it is "here" it blocks the way
///    and hurts whoever is standing in it; while it is "gone" it is a ghost outline you can walk
///    through. Its presence is the hazard.
///  - a GAP-WHEN-ABSENT tile (the skybridge panels, the walkway's hole) is the floor itself. It
///    never blocks anyone: its *absence* is the hazard, and standing over one while it is gone is
///    a fall, which the room checks.
///
/// It reads the LoopClock rather than a private timer, so an Echo's replay passes through exactly
/// the same phases the player did - the same guarantee the rest of the Echo system relies on.</summary>
public partial class FluctuatingTile : Obstacle
{
    #region Private state
    private float _periodSeconds = 4f;
    private float _solidSeconds = 2f;
    private float _phaseSeconds;
    private bool _deadly;
    private string _holdOpenFlag = "";

    private Room _room;
    private bool _isPresent;
    private bool _phaseKnown;
    #endregion

    #region Public properties
    [Export] public float PeriodSeconds { get => _periodSeconds; set => _periodSeconds = Mathf.Max(0.1f, value); }
    [Export] public float SolidSeconds { get => _solidSeconds; set => _solidSeconds = Mathf.Max(0.05f, value); }

    /// <summary>Shifts this tile's cycle, so a row of tiles can ripple instead of blinking together.</summary>
    [Export] public float PhaseSeconds { get => _phaseSeconds; set => _phaseSeconds = value; }

    /// <summary>Whether the tile hurts whoever is inside it while it is present (the blue tiles).</summary>
    [Export] public bool Deadly { get => _deadly; set => _deadly = value; }

    [Export] public int ContactDamage { get; set; } = 3;

    /// <summary>Once this flag is set the tile stops phasing and stays solid for good.</summary>
    [Export] public string HoldOpenFlag { get => _holdOpenFlag; set => _holdOpenFlag = value ?? ""; }

    /// <summary>A switch whose press holds this tile solid for as long as it is held - the
    /// bridge's counterweight. Takes priority over HoldOpenFlag when both are set.</summary>
    [Export] public NodePath HoldOpenSwitchPath { get; set; } = new NodePath();

    private Switch _holdSwitch;

    /// <summary>True when the tile's ABSENCE is the hazard (a bridge platform over a drop) rather
    /// than its presence (a blue tile from a version of the room with no floor). RoomAtrium checks
    /// falls on the tiles that opt in here.</summary>
    [Export] public bool GapWhenAbsent { get; set; }

    public bool IsPresent => _isPresent;
    #endregion

    #region Lifecycle
    public override void _Ready()
    {
        _room = GetParent() as Room;
        if (!HoldOpenSwitchPath.IsEmpty) _holdSwitch = GetNodeOrNull<Switch>(HoldOpenSwitchPath);
        QueueRedraw();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (GameStateManager.Instance != null && GameStateManager.Instance.IsPaused) return;

        bool held = _holdSwitch != null
            ? _holdSwitch.IsActivated
            : _holdOpenFlag != "" && (GameStateManager.Instance?.GetFlag(_holdOpenFlag) ?? false);
        bool present = held || IsPresentAt(Clock);

        // The first tick has to apply the phase even when it matches the default, or a tile that
        // starts in its "gone" half would keep the Obstacle default and block forever.
        if (!_phaseKnown || present != _isPresent)
        {
            _phaseKnown = true;
            _isPresent = present;
            // Only a tile whose *presence* is the hazard is an obstacle. A gap-when-absent tile is
            // the floor itself and must never block: on the bridge, blocking whenever a panel was
            // there would make it a wall while present and a hole while gone, so nobody could ever
            // set foot on it - the crossing has to be floor that comes and goes.
            IsSolid = present && !GapWhenAbsent;   // the Obstacle setter redraws for us
        }

        if (_isPresent && _deadly) DamageOccupants();
    }
    #endregion

    #region Phase logic
    private float Clock => _room != null ? _room.LoopClock : 0f;

    /// <summary>Solid for SolidSeconds out of every PeriodSeconds, offset by PhaseSeconds.</summary>
    private bool IsPresentAt(float clock)
    {
        float phase = Mathf.PosMod(clock - _phaseSeconds, _periodSeconds);
        return phase < _solidSeconds;
    }

    private void DamageOccupants()
    {
        if (_room == null) return;

        Rect2 box = Bounds;
        foreach (Entity entity in _room.Entities)
        {
            if (!entity.IsAlive) continue;
            if (entity is Player || entity is Echo)
                if (box.Intersects(entity.Bounds) && entity is Player player)
                {
                    player.TakeScratch();
                    player.WorldPosition = _room.PlayerStartPosition;
                }
        }
    }
    #endregion

    #region Drawing
    public override void _Draw()
    {
        Rect2 box = new Rect2(Vector2.Zero, Size);

        if (!_isPresent)
        {
            // Gone: the hole in the floor, and a hint of where the tile would be.
            DrawRect(box, new Color(0.04f, 0.09f, 0.16f, 0.55f));
            DrawRect(box, new Color(0.4f, 0.75f, 1f, 0.35f), false, 1f);
            return;
        }

        Color fill = _deadly
            ? new Color(0.18f, 0.45f, 0.85f, 0.9f)      // the blue tile from a room with no floor
            : new Color(0.75f, 0.78f, 0.8f, 0.85f);     // a normal marble slab
        DrawRect(box, fill);
        DrawRect(box, new Color(0.85f, 0.9f, 1f, 0.9f), false, 2f);
    }
    #endregion
}
