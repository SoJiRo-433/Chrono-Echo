using Godot;

public enum EnemyState
{
    Patrol,
    Chase,
    Attack,
    Dead
}

public sealed class EnemyAI
{
    #region Private state
    private readonly Enemy _owner;
    private EnemyState _state = EnemyState.Patrol;
    private float _attackTimer;
    private bool _goingToB = true;
    #endregion

    public EnemyState State => _state;

    public EnemyAI(Enemy owner)
    {
        _owner = owner;
    }

    #region Control from the owner
    public void Reset()
    {
        _state = EnemyState.Patrol;
        _attackTimer = 0f;
        _goingToB = true;
    }

    public void Alert()
    {
        if (_state == EnemyState.Patrol) _state = EnemyState.Chase;
    }

    public void NotifyDeath() { _state = EnemyState.Dead; }
    #endregion

    #region Per-tick logic
    public void Tick(float delta)
    {
        if (_state == EnemyState.Dead) return;
        if (!_owner.IsAlive) { _state = EnemyState.Dead; return; }

        _attackTimer = Mathf.Max(0f, _attackTimer - delta);

        // Enemies only care about the living Player (never about Echoes).
        Player target = GameStateManager.Instance?.Player;
        bool hasTarget = target != null && target.IsAlive;
        float distance = hasTarget ? _owner.WorldPosition.DistanceTo(target.WorldPosition) : float.MaxValue;

        switch (_state)
        {
            case EnemyState.Patrol:
                DoPatrol(delta);
                if (distance <= _owner.DetectionRange) _state = EnemyState.Chase;
                break;

            case EnemyState.Chase:
                if (!hasTarget || distance > _owner.DetectionRange * 1.5f) { _state = EnemyState.Patrol; break; }
                if (distance <= _owner.AttackRange) { _state = EnemyState.Attack; break; }
                _owner.MoveToward(target.WorldPosition, delta, 1f);
                break;

            case EnemyState.Attack:
                if (!hasTarget || distance > _owner.AttackRange * 1.2f) { _state = EnemyState.Chase; break; }
                if (_attackTimer <= 0f)
                {
                    _owner.Attack();                       // Entity.Attack damages hostile targets in range
                    _attackTimer = _owner.AttackCooldown;  // wait before the next swing
                }
                break;
        }
    }

    private void DoPatrol(float delta)
    {
        Vector2 goal = _goingToB ? _owner.PatrolPointB : _owner.PatrolPointA;
        if (_owner.WorldPosition.DistanceTo(goal) < 4f) { _goingToB = !_goingToB; return; }
        _owner.MoveToward(goal, delta, 0.5f);
    }
    #endregion
}
