using Godot;

/// <summary>A passable fan junction with a deterministic brief gap every two seconds.</summary>
public partial class GapFan : Node2D, IInteractable
{
    [Export] public float Radius { get; set; } = 26f;
    [Export] public float PeriodSeconds { get; set; } = 2f;
    // The player needs about 0.67 s to cross the fan's reach with their body included.
    // A 1.25 s opening gives enough time to see the gap and cross the narrow shaft.
    [Export] public float GapSeconds { get; set; } = 1.25f;
    [Export] public string FlagToSet { get; set; } = "";
    private Room _room; private float _lastHit = -10f; private bool _stopped;
    public bool IsGap => _stopped || (_room != null && Mathf.PosMod(_room.LoopClock, PeriodSeconds) < GapSeconds);
    public float DistanceTo(Vector2 from) => Mathf.Max(0f, from.DistanceTo(Position) - Radius);
    public override void _Ready()
    {
        _room = GetParent() as Room;
        _stopped = !string.IsNullOrEmpty(FlagToSet) && GameStateManager.Instance?.GetFlag(FlagToSet) == true;
    }
    public void Interact(Entity interactor)
    {
        _stopped = true;
        if (!string.IsNullOrEmpty(FlagToSet)) GameStateManager.Instance?.SetFlag(FlagToSet, true);
        QueueRedraw();
    }
    public override void _PhysicsProcess(double delta)
    {
        if (_stopped || IsGap || _room?.Player == null || _room.LoopClock - _lastHit < 0.8f) return;
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
