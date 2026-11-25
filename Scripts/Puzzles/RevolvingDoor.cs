using Godot;

/// <summary>Quarter-turn glass door with a clocked gap and knockback when a panel reaches Aionia.</summary>
public partial class RevolvingDoor : Node2D
{
    [Export] public float Radius { get; set; } = 27f;
    [Export] public float TurnSeconds { get; set; } = 4f;
    private Room _room; private float _lastHit = -10f;
    /// <summary>How long the door stands open each turn. It has to cover the walk to it and the
    /// walk through: the bar blocks the whole opening, the player needs about 1.03 s to cross it,
    /// and she is shoved back off the wheel while it is shut. A shorter window closed the bar on
    /// her half way through and left her stuck in the doorway.</summary>
    private const float OpenSeconds = 2f;

    /// <summary>Headless validation: force the door gap to stay open without waiting for the clock.</summary>
    [Export] public bool ForceOpenForTest { get; set; }

    public bool GapOpen => _room != null && (ForceOpenForTest || Mathf.PosMod(_room.LoopClock, TurnSeconds) < OpenSeconds);
    public override void _Ready() { _room = GetParent() as Room; }
    public override void _PhysicsProcess(double delta)
    {
        if (GapOpen || _room?.Player == null || _room.LoopClock - _lastHit < 0.8f) return;
        Vector2 offset = _room.Player.WorldPosition - Position;
        if (offset.Length() > Radius + 12f || Mathf.Abs(offset.X) < 10f) return;
        // A spinning panel pushes her away from the hub, not into it.
        _lastHit = _room.LoopClock; _room.Player.TakeScratch(); _room.Player.WorldPosition += new Vector2(Mathf.Sign(offset.X) * 36f, 0);
    }
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        float angle = (Mathf.Floor((_room?.LoopClock ?? 0f) / TurnSeconds) % 4f) * Mathf.Pi / 2f;
        DrawCircle(Vector2.Zero, Radius, new Color(0.75f, 0.9f, 1f, 0.12f));
        for (int i = 0; i < 4; i++) { float a = angle + Mathf.Tau * i / 4f; DrawLine(Vector2.Zero, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Radius, new Color(0.6f, 0.8f, 1f, 0.8f), 4f); }
        DrawArc(Vector2.Zero, Radius, 0, Mathf.Tau, 36, GapOpen ? new Color(0.45f, 1f, 0.55f) : new Color(0.7f, 0.82f, 1f), 2f);
    }
}
