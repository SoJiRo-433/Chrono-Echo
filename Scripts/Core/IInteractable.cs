using Godot;

/// <summary>
/// INTERFACE: anything an Entity (Player or Echo) can use with the Interact action.
/// OOP role: Switch and Door both implement this, so Entity.Interact() never needs to
/// know which concrete puzzle piece it is talking to.
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// How far 'from' is from reaching this, in pixels - 0 means "standing against it".
    /// Box-shaped props measure to their nearest edge, so standing beside a wide desk counts
    /// as reaching it even though the desk's centre is further away.
    /// </summary>
    float DistanceTo(Vector2 from);

    /// <summary>Called when an entity interacts with this object.</summary>
    void Interact(Entity interactor);
}
