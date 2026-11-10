using System.Collections.Generic;
using Godot;

/// <summary>A deterministic two-step glass tile. Either Aionia or an Echo advances the crack.
///
/// A step counts only when an actor's CENTRE is over this tile. The actor's body is 28x28 and the
/// tiles are 38x36 with a 1 px gutter between them, so an overlap test against the whole body would
/// crack every tile an actor brushed: two of them at once down a row, and four where a row and a
/// column boundary are straddled together.</summary>
public partial class CrackingGlassTile : Node2D
{
    [Export] public Vector2 Size { get; set; } = new Vector2(44, 44);
    private readonly Dictionary<ulong, int> _steps = new();
    private readonly HashSet<ulong> _standing = new();
    public bool IsShattered { get; private set; }
    public Rect2 Bounds => new Rect2(Position, Size);
    public override void _PhysicsProcess(double delta)
    {
        if (GameStateManager.Instance?.IsPaused == true) return;
        Room room = GetParent() as Room; if (room == null) return;
        var current = new HashSet<ulong>();
        foreach (Entity entity in room.Entities)
        {
            if (!entity.IsAlive || !Bounds.HasPoint(entity.WorldPosition)) continue;
            ulong id = entity.GetInstanceId(); current.Add(id);
            if (_standing.Contains(id)) continue;
            int count = _steps.TryGetValue(id, out int previous) ? previous + 1 : 1;
            _steps[id] = count;
            if (count >= 2) IsShattered = true;
            QueueRedraw();
        }
        _standing.Clear(); foreach (ulong id in current) _standing.Add(id);
    }
    public void ResetState() { _steps.Clear(); _standing.Clear(); IsShattered = false; QueueRedraw(); }
    public override void _Draw()
    {
        if (IsShattered) { DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.1f, 0.12f, 0.17f, 0.8f)); return; }
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.73f, 0.88f, 1f, 0.32f));
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.62f, 0.82f, 1f, 0.8f), false, 2f);
        foreach (int steps in _steps.Values) if (steps > 0) { DrawLine(Vector2.Zero, Size, new Color(1f, 1f, 1f, 0.9f), 2f); DrawLine(new Vector2(Size.X, 0), new Vector2(0, Size.Y), new Color(1f, 1f, 1f, 0.8f), 1f); break; }
    }
}
