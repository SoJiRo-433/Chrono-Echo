using Godot;
using System.Collections.Generic;

/// <summary>Level 7's camera roll: four photographs that have to be laid out in the order they
/// were taken. The clues are in the frames themselves - the time on the clock, the length of the
/// shadow, the weather. The earliest frame is still inside the camera, so the shutter has to be
/// held down before it can be read at all.
///
/// The panel is code-built, like the audit folder browser: the game ships without character art,
/// and a photograph with no picture in it is a clue, not an image.</summary>
public partial class PhotoRollPuzzle : Node2D, IInteractable
{
	#region Private state
	// The frames in the order they were taken. Index 0 is the one still in the camera.
	private static readonly string[] Clues =
	{
		"03:30 - a flashbulb. A room, a piano, and nobody standing at it.",
		"06:20 - the sun is just up. The shadow lies east of the frame.",
		"12:05 - noon. The shadow is a short stump under the frame.",
        "18:40 - the light is going. The shadow lies west, and it is raining."
	};

	// The order the frames are spread out in, so the roll cannot be solved by reading left to right.
	private static readonly int[] DisplayOrder = { 2, 0, 3, 1 };

	private CanvasLayer _layer;
	private Label _rollLabel;
	private Label _statusLabel;
	private readonly List<Button> _buttons = new();
	private int _placed;
	#endregion

	#region Exports
	/// <summary>The flag the shutter switch sets once it has been held down long enough.</summary>
	[Export] public string ShutterFlag { get; set; } = "level7_shutter_held";

	[Export] public string FlagToSet { get; set; } = "F7_camera";
	[Export] public string SolvedDialogueId { get; set; } = "level7_camera_solved";
	[Export] public Vector2 Size { get; set; } = new Vector2(44f, 34f);
	[Export] public string Caption { get; set; } = "CAMERA ROLL (E)";
	#endregion

	public Rect2 InteractionBounds => new Rect2(Position - Size * 0.5f, Size);
	public float DistanceTo(Vector2 from) => InteractionBounds.DistanceToPoint(from);

	#region Lifecycle
	public override void _Ready()
	{
		Label label = new Label
		{
			Text = Caption,
			Position = new Vector2(-Size.X * 0.5f, -Size.Y * 0.5f - 16f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		label.AddThemeFontSizeOverride("font_size", 12);
		label.AddThemeColorOverride("font_color", new Color(0.9f, 0.86f, 0.7f));
		AddChild(label);
		QueueRedraw();
	}

	public override void _Draw()
	{
		Rect2 box = new Rect2(-Size * 0.5f, Size);
		DrawRect(box, new Color(0.2f, 0.2f, 0.24f, 0.95f));
		DrawRect(box, new Color(0.86f, 0.79f, 0.62f, 0.9f), false, 2f);
		// The shutter of the camera, drawn as a lens.
		DrawCircle(Vector2.Zero, 9f, new Color(0.35f, 0.42f, 0.5f, 0.95f));
		DrawCircle(Vector2.Zero, 4f, new Color(0.7f, 0.85f, 1f, 0.9f));
	}
	#endregion

	#region IInteractable
	public void Interact(Entity interactor)
	{
		if (_layer == null) BuildUi();
		_placed = 0;
		Refresh();
		_layer.Visible = true;
		GameStateManager.Instance?.SetPaused(true);
	}
	#endregion

	#region The panel
	private void BuildUi()
	{
		_layer = new CanvasLayer { Layer = 20, Visible = false };

		var panel = new PanelContainer { Position = new Vector2(150, 90), Size = new Vector2(860, 460) };
		var stack = new VBoxContainer();
		panel.AddChild(stack);

		var title = new Label { Text = "HESIOD'S CAMERA ROLL", HorizontalAlignment = HorizontalAlignment.Center };
		title.AddThemeFontSizeOverride("font_size", 18);
		stack.AddChild(title);

		var grid = new GridContainer { Columns = 4 };
		stack.AddChild(grid);

		for (int i = 0; i < DisplayOrder.Length; i++)
		{
			int frame = DisplayOrder[i];

			var clue = new Label
			{
				Text = Clues[frame],
				CustomMinimumSize = new Vector2(200, 96),
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				VerticalAlignment = VerticalAlignment.Top
			};
			clue.AddThemeFontSizeOverride("font_size", 12);
			grid.AddChild(clue);

			var button = new Button { Text = "FRAME " + (i + 1), CustomMinimumSize = new Vector2(200, 34) };
			int chosen = frame;
			button.Pressed += () => Place(chosen);
			grid.AddChild(button);
			_buttons.Add(button);
		}

		_rollLabel = new Label { Text = "THE ROLL IS EMPTY", HorizontalAlignment = HorizontalAlignment.Center };
		_rollLabel.AddThemeFontSizeOverride("font_size", 15);
		stack.AddChild(_rollLabel);

		_statusLabel = new Label { Text = "", HorizontalAlignment = HorizontalAlignment.Center };
		_statusLabel.AddThemeFontSizeOverride("font_size", 13);
		_statusLabel.AddThemeColorOverride("font_color", new Color(1f, 0.8f, 0.5f));
		stack.AddChild(_statusLabel);

		var leave = new Button { Text = "PUT THE CAMERA AWAY", CustomMinimumSize = new Vector2(240, 32) };
		leave.Pressed += Close;
		stack.AddChild(leave);

		_layer.AddChild(panel);
		AddChild(_layer);
	}

	/// <summary>Lays one frame down. A frame that is not the next one in the roll breaks the
	/// order and the whole strip has to be laid out again - which is the puzzle: the frames have
	/// to be read before they are placed.</summary>
	private void Place(int frame)
	{
		if (frame != _placed)
		{
			_placed = 0;
			_statusLabel.Text = "The shadows disagree. The roll comes apart.";
			Refresh();
			return;
		}

		_placed++;
		if (_placed < Clues.Length)
		{
			_statusLabel.Text = "";
			Refresh();
			return;
		}

		GameStateManager.Instance?.SetFlag(FlagToSet, true);
		Close();
		(GetParent() as Room)?.Dialogue?.StartDialogue(SolvedDialogueId);
	}

	private void Refresh()
	{
		if (_rollLabel == null) return;

		if (_placed == 0) _rollLabel.Text = "THE ROLL IS EMPTY";
		else
		{
			string times = "";
			for (int i = 0; i < _placed; i++) times += (i > 0 ? "  ->  " : "") + Clues[i].Substring(0, 5);
			_rollLabel.Text = times;
		}

		bool shutterHeld = GameStateManager.Instance?.GetFlag(ShutterFlag) == true;

		for (int i = 0; i < _buttons.Count; i++)
		{
			int frame = DisplayOrder[i];
			// The earliest frame is still in the camera: its button only works once the shutter
			// has been held down for long enough to expose it.
			bool locked = frame == 0 && !shutterHeld;
			_buttons[i].Disabled = locked;
			_buttons[i].Text = locked ? "FRAME " + (i + 1) + " (IN THE CAMERA)" : "FRAME " + (i + 1);
		}
	}

	private void Close()
	{
		if (_layer != null) _layer.Visible = false;
		GameStateManager.Instance?.SetPaused(false);
	}
	#endregion
}
