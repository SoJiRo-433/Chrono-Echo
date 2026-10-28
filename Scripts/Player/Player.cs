using System.Collections.Generic;
using Godot;

/// <summary>
/// The player character. INHERITS Entity (health, damage, movement, drawing).
///
/// OOP roles:
///  - INHERITANCE:   Player : Entity.
///  - POLYMORPHISM:  overrides Update(), TakeDamage(), Die(), Interact(); IsHostileTo().
///  - ENCAPSULATION: lives, inventory, recorder and input are private; exposed via properties.
///  - COMPOSITION:   Player HAS-A PlayerInput and HAS-A EchoRecorder.
///
/// TIME-LOOP LIFECYCLE
///   BeginTimeLoop()  -> a new loop opens. Capture does NOT start by itself: the player
///                       presses the capture key, so they choose when the Echo starts.
///   StartCapture() / StopCapture() -> open and close that window. Closing it appends an
///                       Idle frame, so the Echo stops replaying exactly where you stopped.
///   ResetTimeLoop()  -> recorder stops, an Echo is spawned from the captured window, the
///                       Room restarts (puzzles reset, echoes replay from t=0), player
///                       returns to the start and BeginTimeLoop() runs again.
///   Die() -> Respawn() -> costs a life, then runs ResetTimeLoop() (the failed attempt
///                       becomes an Echo too).
/// </summary>
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

    /// <summary>Scene used to spawn Echoes. If left empty, a plain Echo node is created in code.</summary>
    [Export] public PackedScene EchoScene { get; set; }

    public int Lives => _lives;
    public bool IsGameOver => _isGameOver;
    public bool IsRecording => _recorder.IsRecording;
    public IReadOnlyList<string> Inventory => _inventory;

    /// <summary>How many frames the current - or already frozen - capture window holds.</summary>
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

    /// <summary>The Room calls this once the player is inside it: place at start and start recording.</summary>
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
            if (_input.IsResetPressed()) RestartRun();   // after game over, R starts a new run
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

        if (_input.IsResetPressed()) ResetTimeLoop(CurrentRoom);
    }

    /// <summary>
    /// Performs one action. Returns the action that REALLY happened: a move that was blocked
    /// by a wall is reported as Idle, so the recording matches reality.
    /// </summary>
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

    /// <summary>Death: announce it, then resolve it AFTER the current tick (see ResolveDeath).</summary>
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

    /// <summary>
    /// Respawn after death: costs one life. With lives left, the failed attempt becomes an
    /// Echo and the player restarts at the room's start; with none left it is game over.
    /// </summary>
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

        ResetTimeLoop(CurrentRoom);   // restores position + health as part of the reset
    }

    /// <summary>Full restart after game over: fresh lives, no echoes, flags cleared.</summary>
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
    /// <summary>
    /// Opens a new loop. Capture no longer starts on its own: the player presses the capture
    /// key, so the Echo replays the window they chose instead of the whole loop.
    /// </summary>
    public void BeginTimeLoop()
    {
        EmitSignal(SignalName.LoopStarted);
    }

    /// <summary>Opens a fresh capture window at the player's current position.</summary>
    public void StartCapture()
    {
        _recorder.StartRecording();   // pressing the key again starts a new window
    }

    /// <summary>Closes the window where the player is standing right now.</summary>
    public void StopCapture()
    {
        if (!_recorder.IsRecording) return;
        _recorder.StopRecording(CurrentRoom != null ? CurrentRoom.LoopClock : 0f, WorldPosition);
    }

    /// <summary>The capture key: the first press opens a window, the next one closes it.</summary>
    public void ToggleCapture()
    {
        if (_recorder.IsRecording) StopCapture();
        else StartCapture();
    }

    /// <summary>
    /// Ends the current loop: stop recording -> spawn an Echo that replays it -> restart the
    /// room -> put the player back at the start -> begin recording a new loop.
    /// </summary>
    public void ResetTimeLoop(Room room)
    {
        if (room == null) return;

        // The loop ends one tick after the last recorded tick.
        float endTime = room.LoopClock + room.StepSeconds;
        IReadOnlyList<ActionFrame> frames = _recorder.StopRecording(endTime, WorldPosition);
        EmitSignal(SignalName.LoopEnded, frames.Count);

        if (frames.Count > 0)
        {
            Echo echo = EchoScene != null ? EchoScene.Instantiate<Echo>() : new Echo();
            echo.Initialize(frames);
            room.AddEcho(echo);
        }

        room.StartNewLoop();     // puzzles reset, enemies reset, ALL echoes restart at t=0
        ReturnToStart(room);
        BeginTimeLoop();
    }

    /// <summary>
    /// Mirrors the current lives into the singleton so that the next level (which builds a
    /// new Player node) starts with the same number of lives.
    /// </summary>
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
