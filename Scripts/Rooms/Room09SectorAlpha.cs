using Godot;
using System.Collections.Generic;

/// <summary>Level 9: Sector Alpha. The map runs from the chess lock through the office and
/// divergence terminal, then down to the damaged Hyperion prototype and inner core.</summary>
public partial class Room09SectorAlpha : Room
{
	private const float MapScale = 2f;
	private const string MapPath = "res://Assets/level9_sector_alpha_map.png";
	private const int BoardX = 20, BoardY = 174, Cell = 20;
	private readonly List<ChessPiece> _pieces = new();
	private readonly List<FloorMarker> _pieceMarkers = new();
	private readonly HashSet<Vector2I> _attacked = new();
	private readonly FloorMarker[,] _boardTiles = new FloorMarker[8, 8];
	private readonly List<FloorMarker> _pillars = new();
	private readonly List<FloorMarker> _floatingTiles = new();
	private readonly List<Switch> _gears = new();
	private readonly bool[] _gearRewound = new bool[5];
	private readonly float[] _pillarSpeeds = { 0.72f, 0.93f, 1.14f };
	private readonly Vector2[] _pillarBases = { P(335, 16), P(425, 16), P(515, 16) };
	private Switch _officeLever, _terminalPause;
	private Inspectable _drawer;
	private Door _chessDoor, _innerDoor;
	private float _chessTick, _freezeRemaining, _hitCooldown;
	private bool _pillarsDisabled, _terminalRead, _felixTalked;
	private Vector2 _lastSafePosition;
	private bool _gearDialogueQueued;

	private sealed class ChessPiece
	{
		public string Type;
		public Vector2I Cell;
		public int Step;
		public ChessPiece(string type, Vector2I cell) { Type = type; Cell = cell; }
	}

	public override void _Ready()
	{
		BuildMap();
		RoomSize = WorldSize = new Vector2(1280, 720);
		PlayerStartPosition = P(34, 255);
		ExitPosition = P(246, 336); ExitSize = S(30, 14);
		LevelNumber = 9; NextLevelPath = "res://Scenes/Rooms/Room10Rooftop.tscn"; MaxEchoes = 1;
		MaxCaptureSeconds = 15; EchoLifetimeSeconds = 15; LoopReplay = false;
		IntroDialogueId = "level9_intro"; ClearDialogueId = "level9_clear";
		CompletionFlag = "level9_complete"; ExitLockedHint = "THE TERMINAL, PROTOTYPE, AND INNER CORE CONFRONTATION ARE INCOMPLETE";
		base._Ready();
		_lastSafePosition = PlayerStartPosition;
		Dialogue?.SetObjective("Cross the chessboard to the opposing king.");
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		if (GameStateManager.Instance?.IsPaused == true) return;
		float dt = (float)delta;
		_hitCooldown = Mathf.Max(0f, _hitCooldown - dt);
		if (_freezeRemaining > 0)
		{
			_freezeRemaining = Mathf.Max(0f, _freezeRemaining - dt);
			if (Player != null && Player.IsAlive) Player.WorldPosition = _lastSafePosition;
		}
		else if (Player != null && Player.IsAlive) _lastSafePosition = Player.WorldPosition;

		_chessTick += dt;
		if (_chessTick >= 3f)
		{
			_chessTick -= 3f;
			if (EchoOnBoard() == null) MoveChessPieces();
			RefreshAttackLines();
		}
		if (Player != null && Player.IsAlive && _hitCooldown <= 0 && IsPlayerOnAttackTile())
		{
			Player.TakeScratch(); Player.WorldPosition = PlayerStartPosition;
			_lastSafePosition = PlayerStartPosition; _hitCooldown = 1.2f;
		}
		if (Player != null && Player.IsAlive)
		{
			Vector2I boardCell = BoardCell(Player.WorldPosition);
			if (boardCell.X >= 6 && boardCell.Y <= 1)
			{
				GameStateManager.Instance?.SetFlag("level9_chess_cleared", true);
				_chessDoor?.Open();
				if (GameStateManager.Instance?.GetFlag("level9_chess_objective_set") != true)
				{
					GameStateManager.Instance?.SetFlag("level9_chess_objective_set", true);
					Dialogue?.SetObjective("Explore the shared office and investigate the surroundings to uncover the hidden drawer.");
				}
			}
		}

		// Office lever now temporarily disables the yellow pillars (timeline columns)
		_pillarsDisabled = EchoOn(_officeLever);
		GameStateManager.Instance?.SetFlag("level9_pillars_disabled", _pillarsDisabled);

		// Check if all office interactions have been found to reveal the drawer
		bool whiteboard = GameStateManager.Instance?.GetFlag("level9_office_whiteboard") == true;
		bool mugs = GameStateManager.Instance?.GetFlag("level9_office_mugs") == true;
		bool chair = GameStateManager.Instance?.GetFlag("level9_office_chair") == true;
		bool drawerRevealed = whiteboard && mugs && chair;
		GameStateManager.Instance?.SetFlag("level9_drawer_revealed", drawerRevealed);
		if (_drawer != null) _drawer.Visible = drawerRevealed;
		if (GameStateManager.Instance?.GetFlag("level9_drawer_found") == true &&
			GameStateManager.Instance?.GetFlag("level9_drawer_objective_updated") != true)
		{
			GameStateManager.Instance?.SetFlag("level9_drawer_objective_updated", true);
			Dialogue?.SetObjective("Hold an Echo on the terminal pause switch, then inspect all three variables.");
		}

		bool columnsPaused = EchoOn(_terminalPause);
		GameStateManager.Instance?.SetFlag("level9_columns_paused", columnsPaused);
		for (int i = 0; i < _pillars.Count; i++)
		{
			var p = _pillars[i];
			// Keep each column within the beam lane painted on the map instead of sweeping
			// across unrelated rooms. Echo pause freezes it at its current aligned position.
			// Office lever also disables pillars temporarily.
			if (!_pillarsDisabled && !columnsPaused)
			{
				float x = _pillarBases[i].X + Mathf.Sin(LoopClock * _pillarSpeeds[i]) * 18f;
				p.Position = new Vector2(x, _pillarBases[i].Y);
				if (_freezeRemaining <= 0 && Player != null &&
					new Rect2(p.Position, S(14, 150)).Intersects(Player.Bounds))
				{ _freezeRemaining = 2f; _lastSafePosition = Player.WorldPosition - new Vector2(24f, 0f); }
			}
			else
			{
				// Pillars disabled - hide them off-screen
				p.Position = new Vector2(_pillarBases[i].X, -100);
			}
		}

		if (!_terminalRead && GameStateManager.Instance?.GetFlag("level9_variable_1") == true &&
			GameStateManager.Instance?.GetFlag("level9_variable_2") == true &&
			GameStateManager.Instance?.GetFlag("level9_variable_3") == true && Dialogue?.IsActive != true)
		{
			_terminalRead = true;
			GameStateManager.Instance?.SetFlag("F10_divergence", true);
			// If we have both resonance (F9) and divergence (F10), the truth about Felix is remembered
			if (GameStateManager.Instance?.GetFlag("F9_resonance") == true)
				GameStateManager.Instance?.SetFlag("remembered_felix", true);
			Dialogue?.StartDialogue("level9_terminal_read");
			Dialogue?.SetObjective("Rewind the five prototype gears with Echoes.");
		}
		bool felixTalked = GameStateManager.Instance?.GetFlag("level9_felix_choice") == true;
		if (felixTalked && !_felixTalked) Dialogue?.SetObjective("Reach the exit beyond the inner core.");
		_felixTalked = felixTalked;

		// Each gear completes immediately when interacted with.
		for (int i = 0; i < _gears.Count; i++)
		{
			string wound = $"level9_gear_{i + 1}_wound";
			string rewound = $"level9_gear_{i + 1}_rewound";
			if (GameStateManager.Instance?.GetFlag(wound) == true && !_gearRewound[i])
			{
				_gearRewound[i] = true;
				GameStateManager.Instance?.SetFlag(rewound, true);
			}
		}
		if (_terminalRead && AllGearsRewound() && !_gearDialogueQueued)
		{
			_gearDialogueQueued = true;
			GameStateManager.Instance?.SetFlag("level9_prototype_restored", true);
			_innerDoor?.Open();
			Dialogue?.StartDialogue("level9_machine_restored");
		}
		if (_terminalRead && AllGearsRewound()) _innerDoor?.Open();

		Vector2 chamberEntry = P(386, 268);
		for (int i = 0; i < _floatingTiles.Count; i++)
		{
			float phase = Mathf.PosMod(LoopClock + i * 0.8f, 4f);
			bool solid = phase < 2f;
			_floatingTiles[i].Fill = solid ? new Color(.34f, .55f, .66f, .28f) : new Color(.34f, .55f, .66f, .05f);
			_floatingTiles[i].Outline = solid ? new Color(.65f, .82f, .9f, .78f) : new Color(.65f, .82f, .9f, .22f);
			if (!solid && Player != null && Player.IsAlive && _floatingTiles[i].Bounds.Intersects(Player.Bounds) && _hitCooldown <= 0)
			{
				Player.TakeScratch(); Player.WorldPosition = chamberEntry; _lastSafePosition = chamberEntry; _hitCooldown = 1.2f;
			}
		}
	}

	protected override bool CanCompleteRoom() => _terminalRead && AllGearsRewound() && _felixTalked;

	private void BuildMap()
	{
		Texture2D map = ResourceLoader.Load<Texture2D>(MapPath);
		if (map != null) Add(new Sprite2D { Texture = map, Centered = false, Scale = Vector2.One * MapScale,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest }, "SectorAlphaBackdrop", Vector2.Zero);

		// The chess lock occupies the lower-left 8x8 grid on the supplied map.
		for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
		{
			_boardTiles[x, y] = Add(new FloorMarker { Size = S(Cell, Cell), Fill = new Color(1f, .08f, .08f, 0),
				Outline = new Color(1f, .15f, .12f, 0), DrawBox = true }, $"ChessTile_{x}_{y}", P(BoardX + x * Cell, BoardY + y * Cell));
		}
		_pieces.Add(new ChessPiece("Rook", new Vector2I(1, 1)));
		_pieces.Add(new ChessPiece("Bishop", new Vector2I(5, 2)));
		_pieces.Add(new ChessPiece("Knight", new Vector2I(6, 5)));
		_pieces.Add(new ChessPiece("King", new Vector2I(4, 6)));
		foreach (ChessPiece piece in _pieces)
			_pieceMarkers.Add(Add(new FloorMarker { Size = S(12, 12), Fill = new Color(.62f, .62f, .64f, .95f),
				Outline = new Color(.9f, .88f, .72f, .9f) }, $"Chess{piece.Type}", PiecePosition(piece.Cell)));
		Add(new Inspectable { Caption = "FELIX / INTERCOM (E)", Size = S(30, 18), DrawBox = false,
			FirstDialogueId = "level9_intercom", RepeatDialogueId = "level9_intercom" }, "Intercom", P(34, 243));
		Add(new FloorMarker { Caption = "OPPOSING KING / EXIT", Size = S(42, 14), DrawBox = false }, "KingExitLabel", P(156, 177));
		_chessDoor = Add(new Door { Size = S(24, 8), RequiresSwitch = true }, "ChessExit", P(142, 164));
		RefreshAttackLines();

		// Shared office - investigate surroundings to uncover hidden drawer
		Add(new Inspectable { Caption = "SHARED WHITEBOARD (E)", Size = S(48, 18), DrawBox = false,
			FirstDialogueId = "level9_whiteboard", RepeatDialogueId = "level9_whiteboard",
			FlagToSet = "level9_office_whiteboard" }, "SharedWhiteboard", P(82, 67));
		Add(new Inspectable { Caption = "TWO MUGS (E)", Size = S(30, 18), DrawBox = false,
			FirstDialogueId = "level9_mugs", RepeatDialogueId = "level9_mugs",
			FlagToSet = "level9_office_mugs" }, "Mugs", P(170, 75));
		Add(new Inspectable { Caption = "EMPTY CHAIR (E)", Size = S(32, 24), DrawBox = false,
			FirstDialogueId = "level9_chair", RepeatDialogueId = "level9_chair",
			FlagToSet = "level9_office_chair" }, "EmptyChair", P(215, 125));

		// Office lever - now disables yellow pillars temporarily
		_officeLever = Add(new Switch { IsPressurePlate = true, PlateRadius = 16f }, "OfficePillarLever", P(38, 149));
		Add(new FloorMarker { Caption = "DISABLE PILLARS / ECHO", Size = S(50, 12), DottedRing = true }, "PillarLeverLabel", P(20, 137));

		_drawer = Add(new Inspectable { Caption = "HIDDEN DRAWER (E)", Size = S(36, 20), DrawBox = false,
			RequiredFlag = "level9_drawer_revealed", FirstDialogueId = "level9_drawer", RepeatDialogueId = "level9_drawer" }, "HiddenDrawer", P(260, 141));
		_drawer.Visible = false;

		// Divergence terminal: three moving light columns sweep the approach until an Echo holds
		// the east-wall pause switch. The three variable controls expose the simulation results.
		for (int i = 0; i < 3; i++)
		{
			_pillars.Add(Add(new FloorMarker { Size = S(12, 150), Fill = new Color(.86f, .91f, .73f, .32f),
				Outline = new Color(.94f, 1f, .83f, .82f) }, $"TimelineColumn{i + 1}", P(335 + i * 85, 90)));
		}
		_terminalPause = Add(new Switch { IsPressurePlate = true, PlateRadius = 17 }, "TerminalPause", P(576, 88));
		Add(new FloorMarker { Caption = "ECHO PAUSE", Size = S(42, 12), DottedRing = true }, "PauseLabel", P(555, 71));
		Add(new Inspectable { Caption = "VARIABLE 1 (E)", Size = S(24, 20), DrawBox = false, RequiredFlag = "level9_columns_paused",
			FlagToSet = "level9_variable_1", FirstDialogueId = "level9_variable_one", RepeatDialogueId = "level9_variable_one" }, "VariableOne", P(555, 55));
		Add(new Inspectable { Caption = "VARIABLE 2 (E)", Size = S(24, 20), DrawBox = false, RequiredFlag = "level9_columns_paused",
			FlagToSet = "level9_variable_2", FirstDialogueId = "level9_variable_two", RepeatDialogueId = "level9_variable_two" }, "VariableTwo", P(555, 102));
		Add(new Inspectable { Caption = "OVERRIDE LOG (E)", Size = S(30, 22), DrawBox = false, RequiredFlag = "level9_columns_paused",
			FlagToSet = "level9_variable_3", FirstDialogueId = "level9_variable_three", RepeatDialogueId = "level9_variable_three" }, "VariableThree", P(580, 136));

		// Prototype chamber: the five blue gear pads are separate Echo replay tasks.
		Vector2[] gearPoints = { P(455, 202), P(505, 194), P(548, 220), P(528, 276), P(455, 275) };
		for (int i = 0; i < gearPoints.Length; i++)
		{
			int index = i + 1;
			_gears.Add(Add(new Switch { IsPressurePlate = true, PlateRadius = 24f }, $"GearEchoPad{index}", gearPoints[i]));
			Add(new GearToggle { Caption = $"WIND GEAR {index} (E)", Size = S(24, 20), DrawBox = false,
			RequiredFlag = "F10_divergence", FlagToSet = $"level9_gear_{index}_wound", DialogueId = "level9_gear_wound" },
				$"GearControl{index}", gearPoints[i] + new Vector2(0, 18));
			Add(new FloorMarker { Caption = $"GEAR {index}", Size = S(20, 12), DottedRing = true,
				Outline = new Color(.33f, .72f, 1f) }, $"GearMarker{index}", gearPoints[i] - new Vector2(20, 12));
		}
		// Enclose all drawn Sector Alpha rooms and connectors so players cannot leave the map.
		Wall("UpperNorthWall", 14, 6, 594, 6);
		Wall("OfficeWestWall", 14, 12, 6, 144);
		Wall("TerminalEastWall", 602, 12, 6, 144);
		Wall("UpperSouthWall1", 14, 156, 128, 18);
		Wall("UpperSouthWall2", 166, 156, 314, 18);
		Wall("UpperSouthWall3", 510, 156, 98, 18);

		Wall("ChessWestWall", 14, 174, 6, 160);
		Wall("ChessEastWall", 180, 174, 6, 160);
		Wall("ChessSouthWall", 14, 334, 172, 6);

		Wall("ProtoEastWall", 578, 174, 6, 170);
		Wall("ProtoSouthWall", 396, 344, 188, 6);
		Wall("ProtoWestNorth", 396, 174, 6, 76);
		Wall("ProtoWestSouth", 396, 286, 6, 58);

		Wall("CoreCorrNorth", 320, 244, 76, 6);
		Wall("CoreCorrSouth", 320, 286, 76, 6);

		Wall("CoreNorthWall", 204, 222, 122, 6);
		Wall("CoreWestWall", 204, 228, 6, 108);
		Wall("CoreEastNorth", 320, 228, 6, 22);
		Wall("CoreEastSouth", 320, 286, 6, 50);
		Wall("CoreSouthWest", 204, 336, 42, 6);
		Wall("CoreSouthEast", 276, 336, 50, 6);
		Wall("ExitAlcoveWest", 240, 342, 6, 10);
		Wall("ExitAlcoveEast", 276, 342, 6, 10);
		Wall("ExitAlcoveSouth", 240, 350, 42, 6);

		Add(new FloorMarker { Caption = "HYPERION PROTOTYPE", Size = S(64, 16), DrawBox = false }, "PrototypeLabel", P(474, 245));
		Vector2[] tilePoints = { P(420, 224), P(532, 208), P(536, 278), P(423, 300), P(495, 324) };
		for (int i = 0; i < tilePoints.Length; i++)
			_floatingTiles.Add(Add(new FloorMarker { Size = S(24, 18), Fill = new Color(.34f, .55f, .66f, .28f),
				Outline = new Color(.65f, .82f, .9f, .78f) }, $"FloatingTile{i + 1}", tilePoints[i]));
		_innerDoor = Add(new Door { Size = S(10, 36), RequiresSwitch = true }, "InnerCoreDoor", P(316, 250));
		Add(new Inspectable { Caption = "FELIX (E)", Size = S(36, 30), DrawBox = false,
			RequiredFlag = "level9_prototype_restored", FirstDialogueId = "level9_felix", RepeatDialogueId = "level9_felix",
			CharacterSheetPath = CharacterSprite.FrontSpritePath(GameStateManager.Felix) }, "Felix", P(247, 268));
		Add(new FloorMarker { Caption = "INNER CORE", Size = S(46, 13), DrawBox = false }, "InnerCoreLabel", P(240, 322));
	}

	private partial class GearToggle : Node2D, IInteractable
	{
		public Vector2 Size { get; set; } = new(24, 20);
		public string Caption { get; set; } = "";
		public bool DrawBox { get; set; }
		public string RequiredFlag { get; set; } = "";
		public string FlagToSet { get; set; } = "";
		public string DialogueId { get; set; } = "";

		public float DistanceTo(Vector2 from) => new Rect2(Position, Size).DistanceToPoint(from);

		public override void _Ready()
		{
			Texture2D indicatorTexture = UiSkin.DisInspect;
			if (indicatorTexture != null)
			{
				AddChild(new Sprite2D
				{
					Texture = indicatorTexture,
					Centered = true,
					TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
					ZIndex = 20,
					Position = new Vector2(Size.X * 0.5f, Mathf.Min(Size.Y * 0.4f, 16f))
				});
			}
		}

		public void Interact(Entity interactor)
		{
			if (GameStateManager.Instance?.GetFlag(RequiredFlag) != true) return;
			if (GameStateManager.Instance?.GetFlag(FlagToSet) == true) return;
			GameStateManager.Instance?.SetFlag(FlagToSet, true);
			string rewoundFlag = FlagToSet.Replace("_wound", "_rewound");
			GameStateManager.Instance?.SetFlag(rewoundFlag, true);
			(GetParent() as Room)?.Dialogue?.StartDialogue(DialogueId);
		}
	}

	private void MoveChessPieces()
	{
		if (EchoOnBoard() != null) return;
		foreach (ChessPiece piece in _pieces)
		{
			Vector2I[] moves = piece.Type switch
			{
				"Rook" => new[] { new Vector2I(1, 0), new Vector2I(-1, 0), new Vector2I(0, 1), new Vector2I(0, -1) },
				"Bishop" => new[] { new Vector2I(1, 1), new Vector2I(-1, -1), new Vector2I(1, -1), new Vector2I(-1, 1) },
				"Knight" => new[] { new Vector2I(1, 2), new Vector2I(2, 1), new Vector2I(-1, 2), new Vector2I(-2, 1), new Vector2I(1, -2), new Vector2I(2, -1), new Vector2I(-1, -2), new Vector2I(-2, -1) },
				_ => new[] { new Vector2I(1, 0), new Vector2I(0, 1), new Vector2I(-1, 0), new Vector2I(0, -1) }
			};
			for (int attempt = 0; attempt < moves.Length; attempt++)
			{
				Vector2I candidate = piece.Cell + moves[(piece.Step + attempt) % moves.Length];
				if (InsideBoard(candidate) && !Occupied(candidate, piece))
				{ piece.Cell = candidate; piece.Step = (piece.Step + attempt + 1) % moves.Length; break; }
			}
		}
	}

	private void RefreshAttackLines()
	{
		for (int i = 0; i < _pieces.Count; i++) _pieceMarkers[i].Position = PiecePosition(_pieces[i].Cell);
		_attacked.Clear();
		Echo echo = EchoOnBoard();
		if (echo != null)
		{
			// An Echo on the board freezes the pieces and neutralizes their attack lines,
			// leaving a clear crossing window while the projection lasts.
		}
		else
		{
			foreach (ChessPiece p in _pieces)
			{
				if (p.Type == "Knight")
				{
					foreach (int[] d in new[] { new[] { 1, 2 }, new[] { 2, 1 }, new[] { -1, 2 }, new[] { -2, 1 }, new[] { 1, -2 }, new[] { 2, -1 }, new[] { -1, -2 }, new[] { -2, -1 } })
					{ var c = p.Cell + new Vector2I(d[0], d[1]); if (InsideBoard(c)) _attacked.Add(c); }
					continue;
				}
				int[][] dirs = p.Type switch
				{
					"Rook" => new[] { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } },
					"Bishop" => new[] { new[] { 1, 1 }, new[] { -1, -1 }, new[] { 1, -1 }, new[] { -1, 1 } },
					_ => new[] { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 }, new[] { 1, 1 }, new[] { -1, -1 }, new[] { 1, -1 }, new[] { -1, 1 } }
				};
				foreach (int[] d in dirs)
				{
					Vector2I c = p.Cell + new Vector2I(d[0], d[1]);
					if (p.Type == "King") { if (InsideBoard(c)) _attacked.Add(c); continue; }
					while (InsideBoard(c)) { _attacked.Add(c); if (Occupied(c, p)) break; c += new Vector2I(d[0], d[1]); }
				}
			}
		}
		for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
		{
			bool danger = _attacked.Contains(new Vector2I(x, y));
			_boardTiles[x, y].Fill = danger ? new Color(1f, .05f, .04f, .24f) : new Color(1, .08f, .08f, 0);
			_boardTiles[x, y].Outline = danger ? new Color(1f, .12f, .08f, .78f) : new Color(1f, .15f, .12f, 0);
		}
	}

	private bool IsPlayerOnAttackTile()
	{
		Vector2I cell = BoardCell(Player.WorldPosition);
		return InsideBoard(cell) && _attacked.Contains(cell);
	}
	private Echo EchoOnBoard()
	{
		foreach (Entity e in Entities)
			if (e is Echo echo && echo.IsAlive && echo.Visible && new Rect2(P(BoardX, BoardY), S(8 * Cell, 8 * Cell)).HasPoint(echo.WorldPosition)) return echo;
		return null;
	}
	private bool EchoOn(Switch pad)
	{
		if (pad == null) return false;
		foreach (Entity e in Entities)
			if (e is Echo && e.IsAlive && e.Visible && e.WorldPosition.DistanceTo(pad.Position) <= pad.PlateRadius) return true;
		return false;
	}
	private bool AllGearsRewound()
	{
		foreach (bool rewound in _gearRewound) if (!rewound) return false;
		return true;
	}
	private bool Occupied(Vector2I cell, ChessPiece except) { foreach (var p in _pieces) if (p != except && p.Cell == cell) return true; return false; }
	private static bool InsideBoard(Vector2I c) => c.X >= 0 && c.X < 8 && c.Y >= 0 && c.Y < 8;
	private static Vector2 PiecePosition(Vector2I c) => P(BoardX + c.X * Cell + 4, BoardY + c.Y * Cell + 4);
	private static Vector2I BoardCell(Vector2 p) => new(Mathf.FloorToInt(p.X / S(Cell, Cell).X) - Mathf.FloorToInt(P(BoardX, 0).X / S(Cell, Cell).X), Mathf.FloorToInt(p.Y / S(Cell, Cell).Y) - Mathf.FloorToInt(P(0, BoardY).Y / S(Cell, Cell).Y));
	private T Add<T>(T node, string name, Vector2 position) where T : Node2D { node.Name = name; node.Position = position; AddChild(node); return node; }
	private void Wall(string name, float x, float y, float w, float h) => Add(new Obstacle { Size = S(w, h), DrawBox = false }, name, P(x, y));
	private static Vector2 P(float x, float y) => new(x * MapScale, y * MapScale);
	private static Vector2 S(float x, float y) => new(x * MapScale, y * MapScale);
}