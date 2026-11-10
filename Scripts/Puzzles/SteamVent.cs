using Godot;

/// <summary>A 2x2 plume that puffs for two seconds on a five-second LoopClock cycle.</summary>
public partial class SteamVent : Node2D
{
    [Export] public Vector2 Size { get; set; } = new Vector2(56, 56);
    [Export] public float PeriodSeconds { get; set; } = 5f;
    [Export] public float PuffSeconds { get; set; } = 2f;
    [Export] public NodePath ValvePath { get; set; } = new NodePath();
    private Room _room; private Switch _valve; private float _lastHit = -10f;
    public bool IsPuffing => _room != null && _valve?.IsActivated != true && Mathf.PosMod(_room.LoopClock, PeriodSeconds) < PuffSeconds;
    public override void _Ready() { _room = GetParent() as Room; _valve = GetNodeOrNull<Switch>(ValvePath); }
    public override void _PhysicsProcess(double delta)
    {
        if (!IsPuffing || _room?.Player == null || _room.LoopClock - _lastHit < 1f) return;
        Rect2 plume = new Rect2(Position - Size * 0.5f, Size);
        if (!plume.Intersects(_room.Player.Bounds)) return;
        _lastHit = _room.LoopClock; _room.Player.TakeScratch();
        _room.Player.WorldPosition += Vector2.Left * 32f;
    }
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        if (!IsPuffing) return;
        DrawRect(new Rect2(Position - Size * 0.5f, Size), new Color(0.9f, 0.96f, 1f, 0.3f));
        DrawRect(new Rect2(Position - Size * 0.5f, Size), new Color(0.85f, 0.95f, 1f, 0.8f), false, 2f);
    }
}
