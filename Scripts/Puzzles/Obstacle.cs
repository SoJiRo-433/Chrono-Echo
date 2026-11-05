using Godot;

public partial class Obstacle : Node2D
{
    private Vector2 _size = new Vector2(64f, 64f);
    private bool _isSolid = true;

    [Export]
    public Vector2 Size
    {
        get => _size;
        set { _size = value; QueueRedraw(); }
    }

    // A non-solid obstacle is scenery: still drawn, but it never blocks movement. The Atrium's
    // fluctuating tiles and stair steps are obstacles that switch this on and off as they phase.
    [Export]
    public bool IsSolid
    {
        get => _isSolid;
        set { _isSolid = value; QueueRedraw(); }
    }

    public Rect2 Bounds => new Rect2(Position, _size);

    public override void _Draw()
    {
        Color fill = _isSolid ? new Color(0.35f, 0.35f, 0.4f) : new Color(0.35f, 0.35f, 0.4f, 0.22f);
        DrawRect(new Rect2(Vector2.Zero, _size), fill);
        DrawRect(new Rect2(Vector2.Zero, _size), new Color(0.6f, 0.6f, 0.7f), false, 2f);
    }
}
