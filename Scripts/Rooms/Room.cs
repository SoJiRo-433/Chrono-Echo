using System.Collections.Generic;
using Godot;

public partial class Room : Node2D
{
	/// <summary>The single world-size multiplier for all six levels. Timers remain in seconds.</summary>
	public const float SCALE_FACTOR = 1.5f;

	/// <summary>How far a scripted camera pan zooms in while it holds.</summary>
	private const float PanZoom = 1.35f;

	// Screen-fixed HUD layout, in the 1152x648 viewport's own units.
	private static readonly Vector2 HeartsOrigin = new(14f, 12f);
	#region Signals
	[Signal] public delegate void LoopStartedEventHandler(int loopNumber);
	[Signal] public delegate void RoomCompletedEventHandler();
	[Signal] public delegate void PuzzleStateChangedEventHandler(string switchName, bool isActivated);
	#endregion

	#region Private state (composition: the lists)
	private readonly List<Entity> _entities = new();
	private readonly List<Echo> _echoes = new();
	private readonly List<Obstacle> _obstacles = new();
	private readonly List<Switch> _switches = new();
	private readonly List<Door> _doors = new();
	private readonly List<Inspectable> _inspectables = new();
	private readonly List<IInteractable> _interactables = new();

	private Vector2 _roomSize = new Vector2(1152f, 648f);
	private Vector2 _playerStart = new Vector2(80f, 324f);
	private Vector2 _exitPosition = new Vector2(980f, 274f);
	private Vector2 _exitSize = new Vector2(120f, 100f);
	private int _maxEchoes = 3;
	private float _maxCaptureSeconds = 5f;
	private bool _loopReplay;
	private float _echoLifetimeSeconds = 20f;
	private string _introDialogueId = "intro";
	private string _clearDialogueId = "room01_clear";
	private string _completionFlag = "room01_complete";
	private int _levelNumber = 1;
	private string _nextLevelPath = "";
	private string _exitLockedHint = "";
	private string _demoEchoDialogueId = "";

	private float _loopClock;
	private int _loopNumber = 1;
	private bool _isComplete;
	private bool _transitionStarted;
	private bool _exitLocked;
	private DialogueManager _dialogue;
	private Camera2D _camera;
	private Vector2 _cameraPanTarget;
	private float _cameraPanSecondsLeft;
	private Pendant _pendant;
	private Label _bannerLabel;          // always on: GAME OVER / ROOM CLEAR
	private Control _hearts;             // always on: one heart per point of health left
	private TextureRect[] _heartIcons;
	private Label _objectiveLabel;       // always on once an Objective has been read
	private Label _echoTimerLabel;       // always on while any Echo is still alive
	private Label _recordingTimerLabel;  // shows the 5-second recording bar while capturing
	#endregion

	#region Public properties
	[Export] public Vector2 RoomSize { get => _roomSize; set { _roomSize = value; QueueRedraw(); } }
	/// <summary>Logical room bounds used by the camera and collision checks before SCALE_FACTOR.</summary>
	[Export] public Vector2 WorldSize { get => _roomSize; set { _roomSize = value; QueueRedraw(); } }
	[Export] public Vector2 PlayerStartPosition { get => _playerStart; set => _playerStart = value; }
	[Export] public Vector2 ExitPosition { get => _exitPosition; set { _exitPosition = value; QueueRedraw(); } }
	[Export] public Vector2 ExitSize { get => _exitSize; set { _exitSize = value; QueueRedraw(); } }
	[Export] public int MaxEchoes { get => _maxEchoes; set => _maxEchoes = Mathf.Max(1, value); }

	// How long a single capture window may run before it closes itself. A puzzle never needs more
	// than a few seconds of the past, and the cap keeps the Echo's frame list small.
	[Export] public float MaxCaptureSeconds { get => _maxCaptureSeconds; set => _maxCaptureSeconds = Mathf.Max(0.5f, value); }

	// Default LoopReplay for every Echo this room spawns: false keeps an Echo standing where its
	// recording ended (Room01/Room02, so it can hold a plate); true makes it repeat (the Atrium).
	[Export] public bool LoopReplay { get => _loopReplay; set => _loopReplay = value; }

	// How long each projected Echo lasts before it decays and the room reclaims it. 0 = no limit.
	// Keep it comfortably longer than the walk its puzzle asks for: once the Echo is gone, the
	// plate it was holding comes up and the door it was propping open closes behind the player.
	[Export] public float EchoLifetimeSeconds { get => _echoLifetimeSeconds; set => _echoLifetimeSeconds = Mathf.Max(0f, value); }
	[Export] public string IntroDialogueId { get => _introDialogueId; set => _introDialogueId = value ?? ""; }
	[Export] public string ClearDialogueId { get => _clearDialogueId; set => _clearDialogueId = value ?? ""; }
	[Export] public string CompletionFlag { get => _completionFlag; set => _completionFlag = value ?? ""; }
	[Export] public int LevelNumber { get => _levelNumber; set => _levelNumber = Mathf.Max(1, value); }

	[Export] public string NextLevelPath { get => _nextLevelPath; set => _nextLevelPath = value ?? ""; }

	// A room whose exit is gated by what the level still wants done puts the reason here. Standing in
	// the green box then says what is outstanding instead of doing nothing at all.
	[Export] public string ExitLockedHint { get => _exitLockedHint; set => _exitLockedHint = value ?? ""; }

	// When this conversation finishes, the room projects a scripted demo Echo in front of the
	// player (Scene 6's "a blue light appeared in front of you"). Empty = no demo.
	[Export] public string DemoEchoDialogueId { get => _demoEchoDialogueId; set => _demoEchoDialogueId = value ?? ""; }

	public IReadOnlyList<Entity> Entities => _entities;
	public IReadOnlyList<Echo> Echoes => _echoes;
	public IReadOnlyList<Obstacle> Obstacles => _obstacles;
	public IReadOnlyList<Switch> Switches => _switches;
	public IReadOnlyList<Door> Doors => _doors;
	public IReadOnlyList<Inspectable> Inspectables => _inspectables;
	public Player Player { get; private set; }

	public DialogueManager Dialogue => _dialogue;

	public float LoopClock => _loopClock;
	public int LoopNumber => _loopNumber;
	public bool IsComplete => _isComplete;

	public bool IsFinalLevel => string.IsNullOrEmpty(_nextLevelPath);

	/// <summary>True when a flag reset puts the pendant back in the world here, because this room
	/// has one lying on the floor. Only Room01 does; every later level is past it, so a restart
	/// there must not take the Echo ability away with the flags (see Player.RestartRun).</summary>
	public bool HasPendantOnTheFloor => _pendant != null;

	public float StepSeconds => 1f / Engine.PhysicsTicksPerSecond;

	public Rect2 ExitBounds => new Rect2(_exitPosition, _exitSize);
	public virtual float MovementScaleAt(Vector2 worldPosition) => 1f;

	/// <summary>Takes the camera off the player for a moment and parks it on a world point - the
	/// Library's scripted pan to Yoruka. It hands the camera back to the follow when it expires.
	/// A small zoom comes with it, because a room this small clamps the camera so hard that a
	/// pan alone would not read.</summary>
	public void PanCameraTo(Vector2 worldPosition, float seconds)
	{
		_cameraPanTarget = worldPosition;
		_cameraPanSecondsLeft = Mathf.Max(0.05f, seconds);
		if (_camera != null) _camera.Zoom = Vector2.One * PanZoom;
	}

	/// <summary>A reusable reset-and-respawn hook for hazards whose design resets a whole room.</summary>
	public void HandlePuzzleHit(string hazard)
	{
		ResetPuzzles();
		if (Player == null) return;
		Player.TakeScratch();
		Player.WorldPosition = _playerStart;
	}
	#endregion

	#region Lifecycle
	public override void _Ready()
	{
		// Start at -1 tick so the first physics step brings the clock to exactly 0.
		_loopClock = -StepSeconds;

		RegisterChildren();
		BuildRoomCamera();
		GameStateManager.Instance?.RegisterRoom(this);
		GameStateManager.Instance?.RegisterLevel(_levelNumber);

		foreach (Entity entity in _entities.ToArray()) entity.EnterRoom(this);   // Player is placed at the start

		_dialogue = GetNodeOrNull<DialogueManager>("DialogueManager");
		if (_dialogue != null) _dialogue.DialogueEnded += OnDialogueEnded;       // level hand-over waits for it
		BuildHud();
		QueueRedraw();

		if (_introDialogueId != "" && !SaveManager.IsLoadingSave) _dialogue?.StartDialogue(_introDialogueId);
	}

	private void RegisterChildren()
	{
		foreach (Node child in GetChildren())
		{
			switch (child)
			{
				case Player player: Player = player; _entities.Add(player); break;
				case Echo echo: _echoes.Add(echo); _entities.Add(echo); break;
				case Obstacle obstacle: _obstacles.Add(obstacle); break;
				case Switch sw:
					_switches.Add(sw);
					_interactables.Add(sw);
					sw.ActivationChanged += isActive => EmitSignal(SignalName.PuzzleStateChanged, sw.Name.ToString(), isActive);
					break;
				case Door door: _doors.Add(door); _interactables.Add(door); break;
				case Inspectable inspectable: _inspectables.Add(inspectable); _interactables.Add(inspectable); break;
				case Pendant pendant: _pendant = pendant; _interactables.Add(pendant); break;
				case Node2D node when node is IInteractable interactable: _interactables.Add(interactable); break;
			}
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (GameStateManager.Instance != null && GameStateManager.Instance.IsPaused) return;

		// The Room is the PARENT, so Godot runs this BEFORE any child's _PhysicsProcess:
		// every entity sees the same clock value within one tick.
		_loopClock += (float)delta;

		if (!_isComplete && CanCompleteRoom() && Player != null && Player.IsAlive && ExitBounds.Intersects(Player.Bounds))
			CompleteRoom();
	}

	/// <summary>True while she stands in an exit that is still gated, so the banner can say why it
	/// did nothing. Worked out here rather than in the physics step because the banner has to keep
	/// up even while a conversation has the game paused - Level 6's border starts one on the way in.</summary>
	private void RefreshExitLocked()
	{
		_exitLocked = !_isComplete && Player != null && Player.IsAlive
					  && ExitBounds.Intersects(Player.Bounds) && !CanCompleteRoom();
	}

	protected virtual bool CanCompleteRoom() => true;

	public override void _Process(double delta)
	{
		UpdateRoomCamera((float)delta);
		RefreshExitLocked();
		RefreshBanner();
		RefreshEchoTimer();
		RefreshRecordingTimer();
		RefreshObjective();
	}

	/// <summary>Creates the world camera independently of the CanvasLayer HUD and dialogue UI.</summary>
	private void BuildRoomCamera()
	{
		_camera = new Camera2D
		{
			Name = "RoomCamera",
			Enabled = true,
			PositionSmoothingEnabled = true,
			PositionSmoothingSpeed = 5f
		};
		AddChild(_camera);
		if (Player != null) _camera.Position = Player.WorldPosition;
		_camera.MakeCurrent();
	}

	public void SnapCameraToPlayer()
	{
		if (_camera == null || Player == null) return;
		_cameraPanSecondsLeft = 0f;
		_camera.Zoom = Vector2.One;
		_camera.Position = ClampCamera(Player.WorldPosition);
	}

	private void UpdateRoomCamera(float delta)
	{
		if (_camera == null || Player == null) return;

		// A scripted pan keeps the camera for a moment and then gives it back to the follow.
		if (_cameraPanSecondsLeft > 0f)
		{
			_cameraPanSecondsLeft = Mathf.Max(0f, _cameraPanSecondsLeft - delta);
			_camera.Position = _camera.Position.Lerp(ClampCamera(_cameraPanTarget), Mathf.Min(1f, delta * 5f));
			return;
		}

		_camera.Position = ClampCamera(Player.WorldPosition);
		if (!_camera.Zoom.IsEqualApprox(Vector2.One))
			_camera.Zoom = _camera.Zoom.Lerp(Vector2.One, Mathf.Min(1f, delta * 4f));
	}

	/// <summary>Keeps the camera inside the room, whatever it is looking at.</summary>
	private Vector2 ClampCamera(Vector2 target)
	{
		Vector2 viewport = GetViewportRect().Size / SCALE_FACTOR;
		Vector2 half = viewport * 0.5f;
		target.X = _roomSize.X <= viewport.X ? _roomSize.X * 0.5f : Mathf.Clamp(target.X, half.X, _roomSize.X - half.X);
		target.Y = _roomSize.Y <= viewport.Y ? _roomSize.Y * 0.5f : Mathf.Clamp(target.Y, half.Y, _roomSize.Y - half.Y);
		return target;
	}

	public override void _EnterTree()
	{
		Scale = Vector2.One * SCALE_FACTOR;
	}

	public override void _Draw()
	{
		Vector2 visibleWorld = GetViewportRect().Size / SCALE_FACTOR;
		Vector2 bgSize = new Vector2(Mathf.Max(_roomSize.X, visibleWorld.X), Mathf.Max(_roomSize.Y, visibleWorld.Y));
		Vector2 bgPosition = (_roomSize - bgSize) * 0.5f;
		DrawRect(new Rect2(bgPosition, bgSize), new Color(0.09f, 0.09f, 0.13f));
		DrawRect(ExitBounds, new Color(0.2f, 0.8f, 0.3f, 0.35f));
		DrawRect(ExitBounds, new Color(0.3f, 1f, 0.4f), false, 2f);
	}
	#endregion

	#region Time loop control
	public void StartNewLoop()
	{
		_loopNumber++;
		_loopClock = -StepSeconds;
		_isComplete = false;

		ResetPuzzles();
		foreach (Echo echo in _echoes) echo.StartReplay();

		EmitSignal(SignalName.LoopStarted, _loopNumber);
	}

	public void AddEcho(Echo echo)
	{
		if (_echoes.Count >= _maxEchoes)
		{
			Echo oldest = _echoes[0];
			_echoes.RemoveAt(0);
			_entities.Remove(oldest);
			oldest.QueueFree();
		}

		AddChild(echo);
		echo.EnterRoom(this);
		echo.LoopReplay = _loopReplay;                 // the replay style for the echoes it spawns...
		echo.LifetimeSeconds = _echoLifetimeSeconds;   // ...and how long each projection lasts

		// ...and the same airtime the player jumps with, so a recorded leap clears the same step
		// in the replay that it cleared in the recording.
		if (Player != null) echo.JumpAirtimeSeconds = Player.JumpAirtimeSeconds;

		echo.StartReplay();     // anchor the window to the clock; it replays forward from here
		OnEchoProjected(echo);  // ...and now the room gets the last word on the projection
		_echoes.Add(echo);
		_entities.Add(echo);
	}

	/// <summary>Called once per projection, after its window has been anchored to the clock, so a
	/// room can refine it. RoomAtrium overrides this: only its stairway wants a repeating window,
	/// because a projection that repeats walks itself off a plate.</summary>
	protected virtual void OnEchoProjected(Echo echo) { }

	/// <summary>Reclaim a single Echo: it has decayed, or the room is thinning them out. Guarded so
	/// a deferred removal that arrives after the room was already cleared is a no-op.</summary>
	public void RemoveEcho(Echo echo)
	{
		if (echo == null || !GodotObject.IsInstanceValid(echo)) return;

		_echoes.Remove(echo);
		_entities.Remove(echo);
		echo.QueueFree();
	}

	public void ClearEchoes()
	{
		foreach (Echo echo in _echoes)
		{
			_entities.Remove(echo);
			echo.QueueFree();
		}
		_echoes.Clear();
	}

	/// <summary>Puts every puzzle back to its starting state. A subclass overrides this to clear
	/// the pieces of state only it knows about - its one-shot beats, its stairway stains, the item
	/// a character dropped - so an R-restart never leaves a solved room behind.</summary>
	protected virtual void ResetPuzzles()
	{
		foreach (Switch sw in _switches) sw.ResetState();
		foreach (Door door in _doors) door.Close();
	}

	public void ResetLevelFlagsForDeath()
	{
		GameStateManager.Instance?.ResetLevelFlags(_levelNumber, string.IsNullOrEmpty(_completionFlag) ? null : new[] { _completionFlag });
	}
	#endregion

	#region Queries used by entities and puzzles
	public virtual bool IsBlocked(Rect2 bounds)
	{
		if (!new Rect2(Vector2.Zero, _roomSize).Encloses(bounds)) return true;

		foreach (Obstacle obstacle in _obstacles)
			if (obstacle.IsSolid && bounds.Intersects(obstacle.Bounds)) return true;

		foreach (Door door in _doors)
			if (!door.IsOpen && bounds.Intersects(door.Bounds)) return true;

		// Furniture the player can examine is solid too, so the room reads like the blueprint.
		foreach (Inspectable inspectable in _inspectables)
			if (inspectable.Solid && bounds.Intersects(inspectable.Bounds)) return true;

		return false;
	}

	public IInteractable FindNearestInteractable(Vector2 from, float radius)
	{
		IInteractable best = null;
		float bestDistance = radius;
		foreach (IInteractable candidate in _interactables)
		{
			// A hidden interactable is off the board: Yoruka once she has stormed off, a terminal
			// the player has not reached yet. Hiding the node is all it takes to disable it.
			if (candidate is Node2D node && !node.Visible) continue;

			float distance = candidate.DistanceTo(from);
			if (distance <= bestDistance) { best = candidate; bestDistance = distance; }
		}
		return best;
	}
	#endregion

	#region Completion
	protected void CompleteRoom()
	{
		_isComplete = true;
		bool firstTime = !(GameStateManager.Instance?.GetFlag(_completionFlag) ?? false);
		GameStateManager.Instance?.SetFlag(_completionFlag, true);
		EmitSignal(SignalName.RoomCompleted);

		// The closing conversation pauses the game; leave only once it has been read.
		bool dialogueStarted = firstTime && _clearDialogueId != "" && _dialogue != null
							   && _dialogue.StartDialogue(_clearDialogueId);

		if (!dialogueStarted) BeginLevelTransition();
	}

	private void OnDialogueEnded(string dialogueId)
	{
		if (dialogueId != "" && dialogueId == _demoEchoDialogueId) SpawnDemoEcho();
		if (_isComplete) Callable.From(BeginLevelTransition).CallDeferred();
	}

	/// <summary>Scene 6: after the pendant's memory flash the room projects a translucent copy of
	/// Aionia that repeats the "touches its head" beat. It is an ordinary Echo built from ordinary
	/// ActionFrames - no special-case physics and no player input involved.</summary>
	private void SpawnDemoEcho()
	{
		if (Player == null) return;
		if (GameStateManager.Instance?.GetFlag("echo_demo_spawned") ?? false) return;
		GameStateManager.Instance?.SetFlag("echo_demo_spawned", true);

		Vector2 forward = Player.FacingDirection == Vector2.Zero ? Vector2.Right : Player.FacingDirection;
		Vector2 spot = Player.WorldPosition + forward * 76f;

		float t0 = _loopClock;
		var frames = new List<ActionFrame>
		{
			new ActionFrame(t0, spot, PlayerAction.Idle),
			new ActionFrame(t0 + 0.4f, spot, PlayerAction.Interact),   // it touches its head
			new ActionFrame(t0 + 1.0f, spot, PlayerAction.Idle),
			new ActionFrame(t0 + 1.8f, spot, PlayerAction.Idle)
		};

		Echo echo = Player.EchoScene != null ? Player.EchoScene.Instantiate<Echo>() : new Echo();
		echo.Initialize(frames);
		AddEcho(echo);
	}

	private void BeginLevelTransition()
	{
		if (_transitionStarted || IsFinalLevel) return;
		if (GameStateManager.Instance == null) return;

		_transitionStarted = true;
		GameStateManager.Instance.GoToNextLevel(_nextLevelPath);
	}
	#endregion

	#region HUD (screen-fixed; the hearts are the one piece drawn from Assets/UI)
	private void BuildHud()
	{
		CanvasLayer layer = new CanvasLayer { Name = "HudLayer", Layer = 5 };

		// --- hearts: how much of this life is left, one drawing per point of Entity.MaxHealth ---
		_hearts = new Control { Position = HeartsOrigin, MouseFilter = Control.MouseFilterEnum.Ignore };
		_heartIcons = new TextureRect[UiSkin.HeartCount];
		for (int i = 0; i < UiSkin.HeartCount; i++)
		{
			var heart = new TextureRect
			{
				Texture = UiSkin.Heart(i),
				Position = new Vector2(i * UiSkin.HeartStride, 0f),
				Size = UiSkin.HeartSize,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.Scale,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			_heartIcons[i] = heart;
			_hearts.AddChild(heart);
		}
		if (Player != null)
		{
			Player.HealthChanged += RefreshHearts;
			RefreshHearts(Player.Health, Player.MaxHealth);
		}

		// --- echo timer: the soonest projection to decay, so the room's clock stays legible ---
		_echoTimerLabel = new Label
		{
			Position = new Vector2(14f, 564f),
			Size = new Vector2(520f, 24f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = false
		};
		_echoTimerLabel.AddThemeFontSizeOverride("font_size", 13);
		_echoTimerLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.95f, 1f));
		_echoTimerLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		_echoTimerLabel.AddThemeConstantOverride("outline_size", 4);

		// --- objective: replaces "Name: line" once an Objective line has been read ---
		_objectiveLabel = new Label
		{
			Position = new Vector2(0f, 6f),
			Size = new Vector2(GetViewportRect().Size.X, 26f),
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = false
		};
		_objectiveLabel.AddThemeFontSizeOverride("font_size", 15);
		_objectiveLabel.AddThemeColorOverride("font_color", new Color(1f, 0.86f, 0.45f));
		_objectiveLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		_objectiveLabel.AddThemeConstantOverride("outline_size", 4);

		_bannerLabel = new Label
		{
			Position = new Vector2(0f, 70f),
			Size = new Vector2(GetViewportRect().Size.X, 36f),
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = false
		};
		_bannerLabel.AddThemeFontSizeOverride("font_size", 24);
		_bannerLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.35f));
		_bannerLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		_bannerLabel.AddThemeConstantOverride("outline_size", 5);

		layer.AddChild(_hearts);
		layer.AddChild(_echoTimerLabel);

		// --- recording timer: shows the 5-second capture window while recording ---
		_recordingTimerLabel = new Label
		{
			Position = new Vector2(14f, 590f),
			Size = new Vector2(520f, 24f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = false
		};
		_recordingTimerLabel.AddThemeFontSizeOverride("font_size", 13);
		_recordingTimerLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.35f));
		_recordingTimerLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		_recordingTimerLabel.AddThemeConstantOverride("outline_size", 4);

		layer.AddChild(_recordingTimerLabel);
		layer.AddChild(_objectiveLabel);
		layer.AddChild(_bannerLabel);

		AddChild(layer);
	}

	private void RefreshBanner()
	{
		if (_bannerLabel == null) return;

		string text = "";
		if (Player != null && Player.IsGameOver) text = "GAME OVER - press R to restart";
		else if (_isComplete) text = IsFinalLevel && _levelNumber >= 6 ? $"LEVEL {_levelNumber} CLEAR - to be continued" : IsFinalLevel ? "ALL LEVELS CLEAR" : "ROOM CLEAR";
		else if (_exitLocked && _exitLockedHint != "") text = _exitLockedHint;

		_bannerLabel.Text = text;
		_bannerLabel.Visible = text != "";
	}

	/// <summary>Hearts spent are tinted, not swapped: the sheet draws one heart and a lost point
	/// should read as that same heart gone cold. MaxHealth is three, which is what the sheet
	/// carries; a higher value would want a second row rather than more hearts here.</summary>
	private void RefreshHearts(int current, int max)
	{
		if (_heartIcons == null) return;

		for (int i = 0; i < _heartIcons.Length; i++)
		{
			_heartIcons[i].Visible = i < max;
			_heartIcons[i].Modulate = i < current ? Colors.White : UiSkin.SpentHeart;
		}
	}

	private void RefreshEchoTimer()
	{
		if (_echoTimerLabel == null) return;

		if (_echoes.Count == 0 || _echoLifetimeSeconds <= 0f)
		{
			_echoTimerLabel.Visible = false;
			return;
		}

		float soonest = SoonestEchoSeconds();
		_echoTimerLabel.Visible = true;
		_echoTimerLabel.Text = "ECHO   " + soonest.ToString("0.0") + "s left   (" +
							   _echoes.Count + "/" + _maxEchoes + ")";
		_echoTimerLabel.AddThemeColorOverride("font_color", soonest < _echoLifetimeSeconds * 0.25f
			? new Color(1f, 0.6f, 0.4f)
			: new Color(0.55f, 0.95f, 1f));
	}

	private float SoonestEchoSeconds()
	{
		float soonest = float.MaxValue;
		foreach (Echo echo in _echoes) soonest = Mathf.Min(soonest, echo.SecondsLeft);
		return soonest;
	}

	private void RefreshRecordingTimer()
	{
		if (_recordingTimerLabel == null || Player == null) return;

		float left = Player.CaptureSecondsLeft;
		_recordingTimerLabel.Visible = left > 0f;

		if (left > 0f)
		{
			int barLength = (int)(left / Player.CurrentRoom.MaxCaptureSeconds * 20);
			string bar = new string('█', barLength) + new string('░', 20 - barLength);
			_recordingTimerLabel.Text = "RECORDING   " + left.ToString("0.0") + "s   [" + bar + "]";
			_recordingTimerLabel.AddThemeColorOverride("font_color", left < 1f
				? new Color(1f, 0.4f, 0.4f)
				: new Color(1f, 0.85f, 0.35f));
		}
	}

	private void RefreshObjective()
	{
		if (_objectiveLabel == null) return;

		string objective = _dialogue != null ? _dialogue.CurrentObjective : "";
		_objectiveLabel.Visible = objective != "";
		_objectiveLabel.Text = objective == "" ? "" : "OBJECTIVE:   " + objective;
	}

	#endregion
}
