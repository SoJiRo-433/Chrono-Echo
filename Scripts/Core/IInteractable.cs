using Godot;

public interface IInteractable
{
    float DistanceTo(Vector2 from);

    void Interact(Entity interactor);
}

/// <summary>An interactable that responds continuously while its user holds E.</summary>
public interface IHeldInteractable
{
    void HoldInteract(Entity interactor, float delta);
}
