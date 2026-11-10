using Godot;

/// <summary>Five coolant puddles electrified in alternating groups on the shared loop clock.</summary>
public partial class CoolantPuddleRow : Node2D
{
    [Export] public Vector2 Start { get; set; }
    [Export] public float Spacing { get; set; } = 28f;
    [Export] public float HalfCycleSeconds { get; set; } = 3f;
    [Export] public float KnockbackDistance { get; set; } = 28f;
    private Room _room;
    private float _lastHit = -10f;
    public override void _Ready() { _room = GetParent() as Room; }
    public override void _PhysicsProcess(double delta)
    {
        if (_room?.Player == null || GameStateManager.Instance?.IsPaused == true) return;
        if (_room.LoopClock - _lastHit < 0.5f) return;
        for (int i = 0; i < 5; i++)
        {
            bool lit = (Mathf.FloorToInt(_room.LoopClock / HalfCycleSeconds) % 2 == 0) == (i % 2 == 0);
            Vector2 point = Start + Vector2.Down * (Spacing * i);
            if (!lit || point.DistanceTo(_room.Player.WorldPosition) > 20f) continue;
            _lastHit = _room.LoopClock;
            _room.Player.TakeScratch();
            Vector2 away = (_room.Player.WorldPosition - point).Normalized();
            if (away == Vector2.Zero) away = Vector2.Left;
            _room.Player.WorldPosition += away * KnockbackDistance;
            break;
        }
        QueueRedraw();
    }
    public override void _Draw()
    {
        for (int i = 0; i < 5; i++)
        {
            bool lit = _room != null && ((Mathf.FloorToInt(_room.LoopClock / HalfCycleSeconds) % 2 == 0) == (i % 2 == 0));
            Vector2 point = Start + Vector2.Down * (Spacing * i);
            DrawCircle(point, 10f, lit ? new Color(0.7f, 0.95f, 1f, 0.85f) : new Color(0.18f, 0.3f, 0.42f, 0.55f));
        }
    }
}
