using Godot;

/// <summary>A floor tile that crumbles after three seconds under any actor and reforms four seconds later.</summary>
public partial class TimeDecayTile : Node2D
{
    [Export] public Vector2 Size { get; set; } = new Vector2(40, 40);
    [Export] public float StandSeconds { get; set; } = 3f;
    [Export] public float ReformSeconds { get; set; } = 4f;
    private Room _room; private float _occupiedAt = -1f; private float _dustUntil = -1f;
    private bool _wasOccupied;
    private bool _frozen;
    public Rect2 Bounds => new Rect2(Position, Size);
    public bool IsDust => !_frozen && _room != null && _room.LoopClock < _dustUntil;
    public override void _Ready() { _room = GetParent() as Room; }
    public override void _PhysicsProcess(double delta)
    {
        if (_room == null || GameStateManager.Instance?.IsPaused == true) return;
        bool occupied = false;
        foreach (Entity entity in _room.Entities) if (entity.IsAlive && Bounds.Intersects(entity.Bounds)) { occupied = true; break; }
        if (occupied && !_wasOccupied) _occupiedAt = _room.LoopClock;
        if (occupied && _frozen) _occupiedAt = _room.LoopClock;
        if (occupied && !_frozen && !_wasOccupied && _occupiedAt < 0f) _occupiedAt = _room.LoopClock;
        if (occupied && _occupiedAt >= 0f && _room.LoopClock - _occupiedAt >= StandSeconds)
        { _dustUntil = _room.LoopClock + ReformSeconds; _occupiedAt = -1f; }
        if (!occupied) _occupiedAt = -1f;
        _wasOccupied = occupied;
        QueueRedraw();
    }
    public void Freeze(bool frozen) { _frozen = frozen; QueueRedraw(); }
    public void ResetState() { _occupiedAt = _dustUntil = -1f; _wasOccupied = false; QueueRedraw(); }
    public override void _Draw()
    {
        Color fill = IsDust ? new Color(0.45f, 0.18f, 0.55f, 0.65f) : _frozen ? new Color(0.35f, 0.72f, 1f, 0.9f) : new Color(0.75f, 0.78f, 0.83f, 0.9f);
        DrawRect(new Rect2(Vector2.Zero, Size), fill); DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.85f, 0.88f, 0.95f, 0.8f), false, 1f);
    }
}
