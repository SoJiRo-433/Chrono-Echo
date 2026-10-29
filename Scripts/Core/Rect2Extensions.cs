using Godot;

public static class Rect2Extensions
{
    public static float DistanceToPoint(this Rect2 rect, Vector2 point)
    {
        float dx = Mathf.Max(Mathf.Max(rect.Position.X - point.X, 0f), point.X - (rect.Position.X + rect.Size.X));
        float dy = Mathf.Max(Mathf.Max(rect.Position.Y - point.Y, 0f), point.Y - (rect.Position.Y + rect.Size.Y));
        return Mathf.Sqrt(dx * dx + dy * dy);
    }
}
