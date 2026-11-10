using Godot;

/// <summary>Three deterministic sliding beams. A hit delegates the room reset/respawn rule.</summary>
public partial class LaserGrid : Node2D
{
    [Export] public Rect2 Bounds { get; set; } = new Rect2(0, 0, 150, 390);
    [Export] public float[] Periods { get; set; } = new[] { 7f, 9f, 11f };
    [Export] public float Spacing { get; set; } = 44f;
    private Room _room;
    private float _lastHit = -10f;
    public override void _Ready() { _room = GetParent() as Room; }
    public override void _Process(double delta) => QueueRedraw();
    public override void _PhysicsProcess(double delta)
    {
        if (_room?.Player == null || _room.LoopClock - _lastHit < 1f) return;
        Vector2 p = _room.Player.WorldPosition;
        if (!Bounds.HasPoint(p)) return;
        for (int i = 0; i < 3; i++)
        {
            float x = BeamPosition(i);
            if (Mathf.Abs(p.X - x) > 8f || p.Y < Bounds.Position.Y || p.Y > Bounds.End.Y) continue;
            _lastHit = _room.LoopClock; _room.HandlePuzzleHit("laser"); break;
        }
    }
    public override void _Draw()
    {
        if (_room == null) return;
        for (int i = 0; i < 3; i++)
        {
            float x = BeamPosition(i);
            DrawLine(new Vector2(x, Bounds.Position.Y), new Vector2(x, Bounds.End.Y), new Color(1f, 0.18f, 0.2f, 0.9f), 7f);
        }
        DrawRect(Bounds, new Color(1f, 0.2f, 0.2f, 0.5f), false, 2f);
    }

    /// <summary>Where beam <paramref name="index"/> is right now. The blueprint's arrows point
    /// west, so the rails carry them east to west, each at its own speed.</summary>
    private float BeamPosition(int index)
    {
        float phase = Mathf.PosMod(_room?.LoopClock ?? 0f, Periods[index]) / Periods[index];
        float x = Bounds.End.X - phase * Bounds.Size.X + Spacing * index;
        return Bounds.Position.X + Mathf.PosMod(x - Bounds.Position.X, Bounds.Size.X);
    }
}
