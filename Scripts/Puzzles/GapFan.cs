using Godot;

/// <summary>A passable fan junction with a deterministic brief gap every two seconds.</summary>
public partial class GapFan : Node2D
{
    [Export] public float Radius { get; set; } = 26f;
    [Export] public float PeriodSeconds { get; set; } = 2f;
    [Export] public float GapSeconds { get; set; } = 0.45f;
    private Room _room; private float _lastHit = -10f;
    public bool IsGap => _room != null && Mathf.PosMod(_room.LoopClock, PeriodSeconds) < GapSeconds;
    public override void _Ready() { _room = GetParent() as Room; }
    public override void _PhysicsProcess(double delta)
    {
        if (IsGap || _room?.Player == null || _room.LoopClock - _lastHit < 0.8f) return;
        if (_room.Player.WorldPosition.DistanceTo(Position) > Radius) return;
        _lastHit = _room.LoopClock; _room.Player.TakeScratch(); _room.Player.WorldPosition += Vector2.Left * 30f;
    }
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        Color color = IsGap ? new Color(0.35f, 0.9f, 0.5f) : new Color(0.78f, 0.8f, 0.86f);
        DrawArc(Vector2.Zero, Radius, 0, Mathf.Tau, 32, color, 3f);
        float angle = (_room?.LoopClock ?? 0f) * 5f;
        for (int i = 0; i < 3; i++) { float a = angle + i * Mathf.Tau / 3f; DrawLine(Vector2.Zero, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Radius, color, 4f); }
    }
}
