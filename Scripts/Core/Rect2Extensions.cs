using Godot;

/// <summary>
/// Geometry helper shared by everything that answers IInteractable.
///
/// Why it exists: Godot's Rect2 has no "how far is this point from you" query, and measuring
/// to the CENTRE of a wide prop is wrong - the report log is 227 px across, so its centre is
/// out of reach even while the player is standing right against its edge.
/// </summary>
public static class Rect2Extensions
{
    /// <summary>Distance from 'point' to the closest edge of the rectangle. 0 when inside it.</summary>
    public static float DistanceToPoint(this Rect2 rect, Vector2 point)
    {
        float dx = Mathf.Max(Mathf.Max(rect.Position.X - point.X, 0f), point.X - (rect.Position.X + rect.Size.X));
        float dy = Mathf.Max(Mathf.Max(rect.Position.Y - point.Y, 0f), point.Y - (rect.Position.Y + rect.Size.Y));
        return Mathf.Sqrt(dx * dx + dy * dy);
    }
}
