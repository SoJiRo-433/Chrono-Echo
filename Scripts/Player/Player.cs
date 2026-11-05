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
    private float _captureStartClock;                       // LoopClock value when the window opened
    #endregion

    #region Public properties
    [Export] public int StartingLives { get => _startingLives; set => _startingLives = Mathf.Max(1, value); }

    [Export] public PackedScene EchoScene { get; set; }

    public int Lives => _lives;
    public bool IsGameOver => _isGameOver;
    public bool IsRecording => _recorder.IsRecording;
    public IReadOnlyList<string> Inventory => _inventory;

    // The Echo is an ability of the pendant, not of Aionia: until she picks it up the capture
    // and projection keys do nothing at all (silently - no hint, no rejection line). The flag
    // is the single source of truth, so a full flag reset takes the ability away again - and
    // the Pendant node reappears to match (see Pendant.ListenForReset).
    public bool HasPendant => GameStateManager.Instance?.GetFlag("touched_pendant") ?? false;

    public int CapturedFrameCount => _recorder.FrameCount;

    /// <summary>Seconds left in the open capture window; 0 when nothing is being recorded.
    /// The HUD bar drains against this.</summary>
    public float CaptureSecondsLeft =>
        !_recorder.IsRecording || CurrentRoom == null
            ? 0f
            : Mathf.Max(0f, CurrentRoom.MaxCaptureSeconds - (CurrentRoom.LoopClock - _captureStartClock));

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
        // this tick as its first frame. Without the pendant the key is ignored outright.
        if (HasPendant && _input.IsCaptureTogglePressed()) ToggleCapture();
        AutoStopCaptureAtLimit();

        PlayerAction intended = _input.ReadAction();
        Vector2 positionBeforeMove = WorldPosition;

        // Do the action first so we know whether a move was blocked by a wall.
        PlayerAction effective = ExecuteAction(intended, delta);

        // Record AFTER executing, but with the position from BEFORE the move, so that the
        // frame means "at time T the player stood HERE and then did THIS".
        if (_recorder.IsRecording)
            _recorder.Tick(CurrentRoom.LoopClock, effective, positionBeforeMove);

        // R projects the captured window as an Echo, in place. The run keeps going: no clock
        // rewind, no puzzle reset, and the player is not moved.
        if (HasPendant && _input.IsProjectEchoPressed()) ProjectEcho();
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

            case PlayerAction.Jump:
                Jump();
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

        // Deferred on purpose: Die() can be called from inside another entity's Update() - the
        // Atrium's blue tiles damage whoever is standing on them. Changing the Room (spawning an
        // Echo) in the middle of that tick could break loops that are still running. CallDeferred
        // waits until the tick is finished.
        Callable.From(ResolveDeath).CallDeferred();
    }

    public override void Interact()
    {
        bool used = TryInteractWithNearby();
        EmitSignal(SignalName.PlayerInteracted, used);
    }
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

        // The Echo is an ability of the pendant, so the flag reset takes it away - and in Room01
        // the pendant goes back on the floor at the same moment, ready to be picked up again.
        // Anywhere else there is no pendant to find, and a game-over restart would delete the
        // ability for good: a level that cannot hand the pendant back keeps it instead.
        if (!room.HasPendantOnTheFloor) GameStateManager.Instance?.SetFlag("touched_pendant", true);

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
        _captureStartClock = CurrentRoom != null ? CurrentRoom.LoopClock : 0f;
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

    /// <summary>The capture window is capped per room ([Export] MaxCaptureSeconds), so the recorded
    /// past can never grow past what the puzzle needs. The window closes itself once the cap is hit.</summary>
    private void AutoStopCaptureAtLimit()
    {
        if (!_recorder.IsRecording || CurrentRoom == null) return;
        if (CurrentRoom.LoopClock - _captureStartClock < CurrentRoom.MaxCaptureSeconds) return;
        StopCapture();
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
