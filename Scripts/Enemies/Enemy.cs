using Godot;

public partial class Enemy : Entity
{
    #region Private state
    private EnemyAI _ai;
    private Vector2 _spawnPosition;
    private Vector2 _patrolA;
    private Vector2 _patrolB;
    private float _detectionRange = 170f;
    private float _patrolDistance = 110f;
    private float _attackCooldown = 1.0f;
    #endregion

    #region Public properties
    [Export] public float DetectionRange { get => _detectionRange; set => _detectionRange = Mathf.Max(1f, value); }
    [Export] public float PatrolDistance { get => _patrolDistance; set => _patrolDistance = Mathf.Max(0f, value); }
    [Export] public float AttackCooldown { get => _attackCooldown; set => _attackCooldown = Mathf.Max(0.1f, value); }

    public Vector2 PatrolPointA => _patrolA;
    public Vector2 PatrolPointB => _patrolB;
    public EnemyState AIState => _ai != null ? _ai.State : EnemyState.Patrol;

    protected override Color BodyColor => AIState switch
    {
        EnemyState.Patrol => new Color(0.65f, 0.2f, 0.2f),
        EnemyState.Chase => new Color(0.95f, 0.55f, 0.1f),
        EnemyState.Attack => new Color(1f, 0.05f, 0.05f),
        _ => new Color(0.3f, 0.3f, 0.3f)
    };
    #endregion

    #region Lifecycle
    public override void _Ready()
    {
        base._Ready();
        _ai = new EnemyAI(this);
        _spawnPosition = WorldPosition;
        _patrolA = _spawnPosition - new Vector2(_patrolDistance, 0f);
        _patrolB = _spawnPosition + new Vector2(_patrolDistance, 0f);
    }

    public void ResetForNewLoop()
    {
        Revive();
        WorldPosition = _spawnPosition;
        _ai.Reset();
    }
    #endregion

    #region Polymorphic overrides
    public override void Update(float delta)
    {
        base.Update(delta);
        if (IsAlive) _ai.Tick(delta);
    }

    public override void TakeDamage(int amount)
    {
        base.TakeDamage(amount);
        if (IsAlive) _ai.Alert();   // getting hit makes a patrolling enemy chase
    }

    public override void Die()
    {
        base.Die();
        _ai.NotifyDeath();
        Hide();
    }

    public override void Interact() { }

    protected override bool IsHostileTo(Entity other) => other is Player;
    #endregion

    #region Movement used by the AI
    public void MoveToward(Vector2 target, float delta, float speedScale)
    {
        Vector2 toTarget = target - WorldPosition;
        if (toTarget.Length() < 1f) return;

        Vector2 direction = toTarget.Normalized();
        FacingDirection = direction;
        TryMove(direction * MoveSpeed * speedScale * delta);
    }
    #endregion
}
