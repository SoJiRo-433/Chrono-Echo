/// <summary>
/// INTERFACE: anything that is ticked once per physics frame by the game loop.
/// OOP role: Entity (and Switch, for pressure plates) share one "Update(delta)"
/// contract. Godot's _PhysicsProcess simply forwards to Update().
/// </summary>
public interface IUpdatable
{
    void Update(float delta);
}
