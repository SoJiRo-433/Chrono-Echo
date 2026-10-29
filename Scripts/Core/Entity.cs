using System.Linq;
using Godot;

public abstract partial class Entity : Node2D, IDamageable, IUpdatable
{
    #region Signals (decoupling)
    [Signal] public delegate void HealthChangedEventHandler(int current, int max);
    [Signal] public delegate void DiedEventHandler();
    #endregion

    #region Private state (encapsulation: no public fields anywhere)
    private Vector2 _position;
    private int _health;
    private int _maxHealth = 3;
    private bool _isAlive = true;
    private float _moveSpeed = 120f;
    private float _invincibilityDuration = 0.75f;
    private float _invincibilityTimer;
    private int _attackDamage = 1;
    private float _attackRange = 44f;
    private float _attackFlashTimer;
    private Room _room;
    #endregion

    #region Public properties (controlled access)
    [Export] public int MaxHealth { get => _maxHealth; set => _maxHealth = Mathf.Max(1, value); }
    [Export] public float MoveSpeed { get => _moveSpeed; set => _moveSpeed = Mathf.Max(0f, value); }
    [Export] public float InvincibilityDuration { get => _invincibilityDuration; set => _invincibilityDuration = Mathf.Max(0f, value); }
    [Export] public int AttackDamage { get => _attackDamage; set => _attackDamage = Mathf.Max(0, value); }
    [Export] public float AttackRange { get => _attackRange; set => _attackRange = Mathf.Max(1f, value); }

    public int Health => _health;
    public bool IsAlive => _isAlive;
    public bool IsInvincible => _invincibilityTimer > 0f;
    public Room CurrentRoom => _room;
    public float BodyRadius => 14f;
    public float InteractRadius => 56f;

    public Vector2 WorldPosition
    {
        get => _position;
        set { _position = value; Position = value; }
    }

    public Vector2 FacingDirection { get; protected set; } = Vector2.Down;

    public Rect2 Bounds => BoundsAt(_position);

    protected abstract Color BodyColor { get; }
    #endregion

    #region Godot lifecycle
    public override void _Ready()
    {
        _position = Position;
        _health = _maxHealth;
        _isAlive = true;
        QueueRedraw();
    }

    public override void _PhysicsProcess(double delta)
    {
        // Dialogue (and other overlays) pause gameplay through the singleton.
        if (GameStateManager.Instance != null && GameStateManager.Instance.IsPaused) return;
        Update((float)delta);
    }

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, BodyRadius, BodyColor);
        DrawLine(Vector2.Zero, FacingDirection * BodyRadius, Colors.White, 2f);
        if (_attackFlashTimer > 0f)
            DrawArc(Vector2.Zero, _attackRange, 0f, Mathf.Tau, 32, new Color(1f, 1f, 1f, 0.6f), 2f);
    }
    #endregion

    #region Virtual behaviour (polymorphism)
    public virtual void Update(float delta)
    {
        if (_invincibilityTimer > 0f) _invincibilityTimer = Mathf.Max(0f, _invincibilityTimer - delta);
        if (_attackFlashTimer > 0f) _attackFlashTimer = Mathf.Max(0f, _attackFlashTimer - delta);

        // Blink while invincible so the player can SEE the i-frames.
        bool blinkOff = IsInvincible && ((int)(_invincibilityTimer * 16f) % 2 == 0);
        SelfModulate = blinkOff ? new Color(1f, 1f, 1f, 0.35f) : Colors.White;
        QueueRedraw();
    }

    public virtual void TakeDamage(int amount)
    {
        if (!_isAlive || amount <= 0 || IsInvincible) return;

        _health = Mathf.Max(0, _health - amount);
        _invincibilityTimer = _invincibilityDuration;   // i-frames
        EmitSignal(SignalName.HealthChanged, _health, _maxHealth);

        if (_health == 0) Die();
    }

    public virtual void Die()
    {
        _isAlive = false;
        EmitSignal(SignalName.Died);
        QueueRedraw();
    }

    public virtual void EnterRoom(Room room)
    {
        _room = room;
    }

    public virtual void Interact()
    {
        TryInteractWithNearby();
    }

    public virtual void Attack()
    {
        _attackFlashTimer = 0.12f;
        if (_room == null) return;

        // ToArray() = iterate a snapshot, so the list can safely change during the loop.
        foreach (Entity target in _room.Entities.ToArray())
        {
            if (target == this || !target.IsAlive || !IsHostileTo(target)) continue;
            if (target.WorldPosition.DistanceTo(_position) <= _attackRange + target.BodyRadius)
                target.TakeDamage(_attackDamage);
        }
    }

    protected virtual bool IsHostileTo(Entity other) => false;
    #endregion

    #region Protected helpers for subclasses
    protected bool TryInteractWithNearby()
    {
        if (_room == null) return false;
        IInteractable target = _room.FindNearestInteractable(_position, InteractRadius);
        if (target == null) return false;
        target.Interact(this);
        return true;
    }

    protected void Revive(float protectionSeconds = 0f)
    {
        _isAlive = true;
        _health = _maxHealth;
        _invincibilityTimer = protectionSeconds;
        Show();
        EmitSignal(SignalName.HealthChanged, _health, _maxHealth);
        QueueRedraw();
    }

    public void Heal(int amount)
    {
        if (!_isAlive || amount <= 0) return;
        _health = Mathf.Min(_maxHealth, _health + amount);
        EmitSignal(SignalName.HealthChanged, _health, _maxHealth);
    }

    protected bool TryMove(Vector2 offset)
    {
        if (offset == Vector2.Zero) return false;
        if (_room == null) { WorldPosition = _position + offset; return true; }

        if (CanOccupy(_position + offset)) { WorldPosition = _position + offset; return true; }

        Vector2 xOnly = new Vector2(offset.X, 0f);
        if (xOnly != Vector2.Zero && CanOccupy(_position + xOnly)) { WorldPosition = _position + xOnly; return true; }

        Vector2 yOnly = new Vector2(0f, offset.Y);
        if (yOnly != Vector2.Zero && CanOccupy(_position + yOnly)) { WorldPosition = _position + yOnly; return true; }

        return false;
    }
    #endregion

    #region Private helpers
    private Rect2 BoundsAt(Vector2 center)
    {
        float r = BodyRadius;
        return new Rect2(center - new Vector2(r, r), new Vector2(r * 2f, r * 2f));
    }

    private bool CanOccupy(Vector2 point)
    {
        if (!_room.IsBlocked(BoundsAt(point))) return true;
        return _room.IsBlocked(BoundsAt(_position));
    }
    #endregion
}
