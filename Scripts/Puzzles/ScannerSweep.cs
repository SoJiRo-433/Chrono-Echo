using Godot;

/// <summary>A deterministic scanner beam that moves west to east on the room LoopClock.</summary>
public partial class ScannerSweep : Node2D
{
    [Export] public Rect2 Corridor { get; set; } = new Rect2(0, 0, 90, 420);
    [Export] public Vector2[] ShadowCenters { get; set; } = System.Array.Empty<Vector2>();
    [Export] public float PeriodSeconds { get; set; } = 6f;
    [Export] public Vector2 RespawnPoint { get; set; }
    private Room _room;
    private int _lastAlarmCycle = -1;
    private Vector2 _alertPoint;
    private float _alertUntil = -1f;
    public override void _Ready() { _room = GetParent() as Room; }
    public override void _Process(double delta) => QueueRedraw();
    public override void _PhysicsProcess(double delta)
    {
        if (_room?.Player == null || GameStateManager.Instance?.IsPaused == true) return;
        float time = _room.LoopClock;
        int cycle = Mathf.FloorToInt(time / Mathf.Max(0.1f, PeriodSeconds));
        float x = Corridor.Position.X + Mathf.PosMod(time, PeriodSeconds) / PeriodSeconds * Corridor.Size.X;
        Vector2 p = _room.Player.WorldPosition;
        if (cycle == _lastAlarmCycle || !Corridor.HasPoint(p) || Mathf.Abs(p.X - x) > 9f || InShadow(p)) return;
        _lastAlarmCycle = cycle;
        _alertPoint = p;
        _alertUntil = time + 0.65f;
        _room.Player.TakeScratch();
        _room.Player.WorldPosition = RespawnPoint;
    }
    private bool InShadow(Vector2 point)
    {
        foreach (Vector2 center in ShadowCenters) if (point.DistanceTo(center) <= 24f) return true;
        return false;
    }
    public override void _Draw()
    {
        if (_room == null) return;
        float phase = Mathf.PosMod(_room.LoopClock, Mathf.Max(0.1f, PeriodSeconds)) / PeriodSeconds;
        float x = Corridor.Position.X + phase * Corridor.Size.X;
        Color flash = ((int)(_room.LoopClock * 8f) % 2 == 0) ? new Color(1f, 0.95f, 0.65f, 0.4f) : new Color(0.5f, 0.9f, 1f, 0.25f);
        DrawLine(new Vector2(x, Corridor.Position.Y), new Vector2(x, Corridor.End.Y), new Color(0.35f, 0.82f, 1f, 0.9f), 5f);
        DrawRect(new Rect2(x - 8f, Corridor.Position.Y, 16f, Corridor.Size.Y), flash);
        foreach (Vector2 shadow in ShadowCenters) DrawRect(new Rect2(shadow - new Vector2(22, 18), new Vector2(44, 36)), new Color(0.12f, 0.12f, 0.16f));
        if (_room.LoopClock < _alertUntil)
        {
            float pulse = 0.35f + 0.45f * Mathf.Abs(Mathf.Sin(_room.LoopClock * 18f));
            DrawRect(new Rect2(_alertPoint - new Vector2(18f, 18f), new Vector2(36f, 36f)), new Color(1f, 0.05f, 0.05f, pulse));
        }
    }
}
