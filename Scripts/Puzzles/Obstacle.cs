using Godot;

public partial class Obstacle : Node2D
{
    private Vector2 _size = new Vector2(64f, 64f);

    [Export]
    public Vector2 Size
    {
        get => _size;
        set { _size = value; QueueRedraw(); }
    }

    public Rect2 Bounds => new Rect2(Position, _size);

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, _size), new Color(0.35f, 0.35f, 0.4f));
        DrawRect(new Rect2(Vector2.Zero, _size), new Color(0.6f, 0.6f, 0.7f), false, 2f);
    }
}
