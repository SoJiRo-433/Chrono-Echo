/// <summary>
/// INTERFACE: anything that has health and can be hurt.
/// OOP role: lets combat code treat Player, Enemy (and any future destructible)
/// the same way without knowing the concrete class (polymorphism via interface).
/// </summary>
public interface IDamageable
{
    int Health { get; }
    int MaxHealth { get; }
    bool IsAlive { get; }

    /// <summary>Apply damage. Implementations decide about invincibility, death, etc.</summary>
    void TakeDamage(int amount);
}
