using System.Collections.Generic;
using Godot;

public partial class Player : Entity
{
    #region Signals
    [Signal] public delegate void LivesChangedEventHandler(int lives);
    [Signal] public delegate void PlayerDiedEventHandler();
    [Signal] public delegate void PlayerInteractedEventHandler(bool foundTarget);
    [Signal] public delegate void LoopStartedEventHandler();
    [Signal] public delegate void LoopEndedEventHandler(int frameCount);
    [Signal] public delegate void GameOverEventHandler();
    #endregion

    #region Private state
    private int _startingLives = 3;
    private int _lives;
    private bool _isGameOver;
    private readonly List<string> _inventory = new();
    private readonly PlayerInput _input = new();            // composition
    private readonly EchoRecorder _recorder = new();        // composition
    #endregion

    #region Public properties
    [Export] public int StartingLives { get => _startingLives; set => _startingLives = Mathf.Max(1, value); }

    [Export] public PackedScene EchoScene { get; set; }

    public int Lives => _lives;
    public bool IsGameOver => _isGameOver;
    public bool IsRecording => _recorder.IsRecording;
    public IReadOnlyList<string> Inventory => _inventory;

    public int CapturedFrameCount => _recorder.FrameCount;

    protected override Color BodyColor => new Color(0.25f, 0.55f, 1f);
    #endregion

    #region Godot lifecycle
    public override void _Ready()
    {
        base._Ready();
        _lives = _startingLives;

        // A new level instantiates a brand new Player node, so the lives are picked back up
        // from the singleton: reaching the next level must not hand out a free refill.
        if (GameStateManager.Instance != null && GameStateManager.Instance.HasCarriedLives)
            _lives = GameStateManager.Instance.CarriedLives;

        PlayerInput.RegisterInputActions();
        GameStateManager.Instance?.RegisterPlayer(this);
        SyncLivesToState();
    }

    public override void EnterRoom(Room room)
    {
        base.EnterRoom(room);
        WorldPosition = room.PlayerStartPosition;
        BeginTimeLoop();
    }
    #endregion

    #region Update (polymorphism: overrides Entity.Update)
    public override void Update(float delta)
    {
        base.Update(delta);   // i-frame timers, blinking, redraw

        if (_isGameOver)
        {
            if (_input.IsProjectEchoPressed()) RestartRun();   // after game over, R starts a new run
            return;
        }
        if (!IsAlive || CurrentRoom == null) return;

        // Read the capture key before the action, so a window opened this tick still records
        // this tick as its first frame.
        if (_input.IsCaptureTogglePressed()) ToggleCapture();

        PlayerAction intended = _input.ReadAction();
        Vector2 positionBeforeMove = WorldPosition;

        // Do the action first so we know whether a move was blocked by a wall.
        PlayerAction effective = ExecuteAction(intended, delta);

        // Record AFTER executing, but with the position from BEFORE the move, so that the
        // frame means "at time T the player stood HERE and then did THIS".
        if (_recorder.IsRecording)
            _recorder.Tick(CurrentRoom.LoopClock, effective, positionBeforeMove);

        // R projects the captured window as an Echo, in place. The run keeps going: no clock
        // rewind, no puzzle/enemy reset, and the player is not moved.
        if (_input.IsProjectEchoPressed()) ProjectEcho();
    }

    private PlayerAction ExecuteAction(PlayerAction action, float delta)
    {
        switch (action)
        {
            case PlayerAction.Interact:
                Interact();
                return action;

            case PlayerAction.Attack:
                Attack();
                return action;

            case PlayerAction.Idle:
                return action;

            default:   // the four movement actions
                Vector2 direction = PlayerInput.DirectionOf(action);
                FacingDirection = direction;
                bool moved = TryMove(direction * MoveSpeed * delta);
                return moved ? action : PlayerAction.Idle;
        }
    }
    #endregion

    #region Combat and interaction overrides (polymorphism)
    public override void TakeDamage(int amount)
    {
        if (_isGameOver) return;
        base.TakeDamage(amount);
    }

    public override void Die()
    {
        base.Die();   // IsAlive = false, Died signal
        EmitSignal(SignalName.PlayerDied);
        GameStateManager.Instance?.NotifyPlayerDied();

        // Deferred on purpose: Die() is usually called from inside an enemy's Update().
        // Changing the Room (spawning an Echo) in the middle of that tick could break
        // loops that are still running. CallDeferred waits until the tick is finished.
        Callable.From(ResolveDeath).CallDeferred();
    }

    public override void Interact()
    {
        bool used = TryInteractWithNearby();
        EmitSignal(SignalName.PlayerInteracted, used);
    }

    protected override bool IsHostileTo(Entity other) => other is Enemy;
    #endregion

    #region Lives and respawn
    private void ResolveDeath()
    {
        Respawn();
    }

    public void Respawn()
    {
        _lives--;
        EmitSignal(SignalName.LivesChanged, _lives);
        SyncLivesToState();

        if (_lives <= 0)
        {
            _isGameOver = true;
            _recorder.StopRecording(CurrentRoom != null ? CurrentRoom.LoopClock : 0f, WorldPosition);
            EmitSignal(SignalName.GameOver);
            GameStateManager.Instance?.TriggerGameOver();
            return;
        }

        ProjectEcho();                              // the failed attempt leaves an Echo behind
        if (CurrentRoom != null) ReturnToStart(CurrentRoom);   // full health + back to the start
    }

    public void RestartRun()
    {
        Room room = CurrentRoom;
        if (room == null) return;

        _lives = _startingLives;
        _isGameOver = false;
        _inventory.Clear();
        GameStateManager.Instance?.ResetFlags();
        SyncLivesToState();
        room.ClearEchoes();
        room.StartNewLoop();
        ReturnToStart(room);
        BeginTimeLoop();
        EmitSignal(SignalName.LivesChanged, _lives);
    }
    #endregion

    #region Time loop
    public void BeginTimeLoop()
    {
        EmitSignal(SignalName.LoopStarted);
    }

    public void StartCapture()
    {
        _recorder.StartRecording();   // pressing the key again starts a new window
    }

    public void StopCapture()
    {
        if (!_recorder.IsRecording) return;
        _recorder.StopRecording(CurrentRoom != null ? CurrentRoom.LoopClock : 0f, WorldPosition);
    }

    public void ToggleCapture()
    {
        if (_recorder.IsRecording) StopCapture();
        else StartCapture();
    }

    public void ProjectEcho()
    {
        Room room = CurrentRoom;
        if (room == null) return;

        IReadOnlyList<ActionFrame> frames = _recorder.StopRecording(room.LoopClock, WorldPosition);
        _recorder.Clear();                        // the window is consumed once it is projected
        EmitSignal(SignalName.LoopEnded, frames.Count);

        if (frames.Count == 0) return;

        Echo echo = EchoScene != null ? EchoScene.Instantiate<Echo>() : new Echo();
        echo.Initialize(frames);
        room.AddEcho(echo);                        // AddEcho anchors the replay and starts it now
    }

    private void SyncLivesToState()
    {
        if (GameStateManager.Instance != null) GameStateManager.Instance.CarriedLives = _lives;
    }

    private void ReturnToStart(Room room)
    {
        Revive(1.0f);            // full health + one second of spawn protection
        WorldPosition = room.PlayerStartPosition;
        FacingDirection = Vector2.Right;
    }
    #endregion

    #region Inventory (simple)
    public void AddItem(string item) { if (!string.IsNullOrEmpty(item)) _inventory.Add(item); }
    public bool HasItem(string item) => _inventory.Contains(item);
    public bool RemoveItem(string item) => _inventory.Remove(item);
    #endregion
}
