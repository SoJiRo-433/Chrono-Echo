using Godot;

/// <summary>One step of the Atrium's broken stairway. It never blocks movement - that is the
/// point, it is a hole in the floor. Instead an Echo walking over it "stains" it solid for a few
/// seconds, and RoomAtrium treats a step that is not stained as a fall.
///
/// Only an Echo stains a step. That is the doc's rule: she follows the path her own past self
/// makes, and she has to keep up with it.</summary>
public partial class TimeStainStep : Obstacle
{
    #region Private state
    private float _stainSeconds = 3f;
    private float _stainTimer;
    private Room _room;
    #endregion

    #region Public properties
    [Export] public float StainSeconds { get => _stainSeconds; set => _stainSeconds = Mathf.Max(0.1f, value); }

    public bool IsStained => _stainTimer > 0f;

    /// <summary>0..1, how much stain is left. Used to fade the drawing out as it expires.</summary>
    public float StainRatio => _stainSeconds > 0f ? Mathf.Clamp(_stainTimer / _stainSeconds, 0f, 1f) : 0f;
    #endregion

    #region Lifecycle
    public override void _Ready()
    {
        _room = GetParent() as Room;
        IsSolid = false;          // always walkable: stained or not, it never blocks anyone
        QueueRedraw();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (GameStateManager.Instance != null && GameStateManager.Instance.IsPaused) return;

        if (_stainTimer > 0f)
        {
            _stainTimer = Mathf.Max(0f, _stainTimer - (float)delta);
            QueueRedraw();
        }

        if (_room == null) return;

        Rect2 box = Bounds;
        foreach (Entity entity in _room.Entities)
        {
            if (!entity.IsAlive || entity is not Echo) continue;
            if (box.Intersects(entity.Bounds)) Restain();
        }
    }

    private void Restain()
    {
        if (Mathf.IsEqualApprox(_stainTimer, _stainSeconds)) return;
        _stainTimer = _stainSeconds;
        QueueRedraw();
    }

    /// <summary>A room reset puts the step back to being a hole, so a restart never leaves the
    /// last climb's stains behind.</summary>
    public void ResetState()
    {
        if (_stainTimer <= 0f) return;
        _stainTimer = 0f;
        QueueRedraw();
    }
    #endregion

    #region Drawing
    public override void _Draw()
    {
        Rect2 box = new Rect2(Vector2.Zero, Size);

        if (!IsStained)
        {
            // No stain: the stair is a gap that happens to be shaped like a step.
            DrawRect(box, new Color(0.06f, 0.05f, 0.1f, 0.75f));
            DrawRect(box, new Color(0.7f, 0.65f, 0.5f, 0.25f), false, 1f);
            return;
        }

        float fade = StainRatio;
        DrawRect(box, new Color(0.35f, 0.85f, 1f, 0.35f + 0.4f * fade));
        DrawRect(box, new Color(0.6f, 0.95f, 1f, 0.5f + 0.5f * fade), false, 2f);
    }
    #endregion
}
