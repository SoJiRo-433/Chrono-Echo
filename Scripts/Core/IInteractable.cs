using Godot;

public interface IInteractable
{
    float DistanceTo(Vector2 from);

    void Interact(Entity interactor);
}
