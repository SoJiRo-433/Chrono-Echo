using Godot;

/// <summary>Quarter-turn glass door with a clocked gap and knockback when a panel reaches Aionia.</summary>
public partial class RevolvingDoor : Node2D
{
    [Export] public float Radius { get; set; } = 27f;
    [Export] public float TurnSeconds { get; set; } = 4f;
    private Room _room; private float _lastHit = -10f;
    public bool GapOpen => _room != null && Mathf.PosMod(_room.LoopClock, TurnSeconds) < 0.7f;
    public override void _Ready() { _room = GetParent() as Room; }
    public override void _PhysicsProcess(double delta)
    {
        if (GapOpen || _room?.Player == null || _room.LoopClock - _lastHit < 0.8f) return;
        Vector2 offset = _room.Player.WorldPosition - Position;
        if (offset.Length() > Radius + 12f || Mathf.Abs(offset.X) < 10f) return;
        _lastHit = _room.LoopClock; _room.Player.TakeScratch(); _room.Player.WorldPosition += new Vector2(Mathf.Sign(offset.X) * -36f, 0);
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
