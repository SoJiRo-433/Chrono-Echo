using Godot;

/// <summary>A shared-clock field suppressed while its generator switch is held down.</summary>
public partial class StasisField : Node2D
{
    [Export] public float Radius { get; set; } = 50f;
    [Export] public NodePath GeneratorPath { get; set; } = new NodePath();
    private Switch _generator;
    private Room _room;
    public bool IsActive => _generator == null || !_generator.IsActivated;
    public override void _Ready() { _room = GetParent() as Room; _generator = GetNodeOrNull<Switch>(GeneratorPath); }
    public override void _Process(double delta) => QueueRedraw();
    public bool Contains(Vector2 point) => IsActive && point.DistanceTo(Position) < Radius;
    public override void _Draw()
    {
        if (!IsActive) return;
        float pulse = 0.22f + 0.08f * Mathf.Sin((_room?.LoopClock ?? 0f) * 4f);
        DrawCircle(Vector2.Zero, Radius, new Color(0.25f, 0.72f, 1f, pulse));
        DrawArc(Vector2.Zero, Radius, 0, Mathf.Tau, 48, new Color(0.5f, 0.88f, 1f, 0.85f), 3f);
    }
}
