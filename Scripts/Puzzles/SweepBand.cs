using Godot;

/// <summary>A band of glitch static that sweeps back and forth across a strip of floor. Anyone it
/// touches cannot move while it is over them - the room's collision check treats the band as
/// solid - so crossing it is a timing problem: the player waits for the band to be at the far end
/// and walks across behind it.
///
/// The band reads the room's shared LoopClock, so it crosses a given point at the same moment
/// every cycle, and an Echo walking through it is caught on exactly the same beat.</summary>
public partial class SweepBand : Node2D
{
    #region Private state
    private Room _room;
    #endregion

    #region Exports
    /// <summary>The strip of floor the band slides along.</summary>
    [Export] public Rect2 Bounds { get; set; } = new Rect2(0, 0, 200, 40);

    /// <summary>Seconds for one pass from one end to the other. The band goes back and forth, so
    /// a full cycle is twice this.</summary>
    [Export] public float PeriodSeconds { get; set; } = 3f;

    [Export] public float BandWidth { get; set; } = 22f;
    #endregion

    #region Lifecycle
    public override void _Ready() { _room = GetParent() as Room; }
    public override void _Process(double delta) => QueueRedraw();
    #endregion

    /// <summary>Where the band is right now. It slides from one end of the strip to the other and
    /// back again, so the quiet gap is when it is furthest from where the player is standing.</summary>
    public Rect2 BandRect()
    {
        float cycle = Mathf.Max(0.5f, PeriodSeconds) * 2f;
        float phase = Mathf.PosMod(Clock, cycle) / cycle;
        float travel = phase <= 0.5f ? phase * 2f : (1f - phase) * 2f;

        float span = Mathf.Max(0f, Bounds.Size.X - BandWidth);
        float x = Bounds.Position.X + travel * span;
        return new Rect2(new Vector2(x, Bounds.Position.Y), new Vector2(BandWidth, Bounds.Size.Y));
    }

    public bool Overlaps(Rect2 box) => BandRect().Intersects(box);

    private float Clock => _room?.LoopClock ?? 0f;

    #region Drawing
    public override void _Draw()
    {
        Rect2 band = BandRect();
        DrawRect(band, new Color(0.75f, 0.45f, 1f, 0.3f));
        DrawRect(band, new Color(0.9f, 0.7f, 1f, 0.75f), false, 2f);
        // The rails the band slides along, so the strip reads as a path and not as a floating bar.
        DrawLine(new Vector2(Bounds.Position.X, Bounds.End.Y), new Vector2(Bounds.End.X, Bounds.End.Y),
                 new Color(0.6f, 0.4f, 0.8f, 0.5f), 2f);
    }
    #endregion
}
