using Godot;

/// <summary>Overlay shown when the player runs out of lives. Offers three choices:
/// restart the current level, view credits, or return to the main menu.</summary>
public partial class GameOverScreen : Control
{
	private const string MainMenuScene = "res://main_menu.tscn";
	private const string CreditsScene  = "res://Scenes/EndingScreen.tscn";

	private Player _player;
	private Room _room;

	public void Setup(Player player, Room room)
	{
		_player = player;
		_room   = room;
	}

	public override void _Ready()
	{
		Theme = UiHelper.GetTheme();
		ProcessMode = ProcessModeEnum.Always;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;   // block clicks reaching the game

		// --- dark overlay ---
		var ground = new ColorRect { Color = new Color(0.03f, 0.02f, 0.05f, 0.82f), MouseFilter = MouseFilterEnum.Stop };
		AddChild(ground);
		ground.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		// --- centred column ---
		var center = new CenterContainer();
		AddChild(center);
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		var box = new VBoxContainer { CustomMinimumSize = new Vector2(500f, 0f) };
		box.AddThemeConstantOverride("separation", 20);
		center.AddChild(box);

		// heading
		var heading = new Label
		{
			Text = "GAME OVER",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		heading.AddThemeFontSizeOverride("font_size", 44);
		heading.AddThemeColorOverride("font_color", new Color(1f, 0.35f, 0.3f));
		box.AddChild(heading);

		// subtitle
		var subtitle = new Label
		{
			Text = "You have run out of lives.",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		subtitle.AddThemeFontSizeOverride("font_size", 18);
		subtitle.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.8f));
		box.AddChild(subtitle);

		// spacer
		box.AddChild(new Control { CustomMinimumSize = new Vector2(0f, 10f) });

		// --- buttons ---
		var btnRestart = MakeButton("Back to level start");
		btnRestart.Pressed += OnRestartPressed;

		var btnCredits = MakeButton("Credits");
		btnCredits.Pressed += OnCreditsPressed;

		var btnMenu = MakeButton("Main Menu");
		btnMenu.Pressed += OnMenuPressed;

		var buttonColumn = new VBoxContainer { CustomMinimumSize = new Vector2(360f, 0f) };
		buttonColumn.AddThemeConstantOverride("separation", 12);
		buttonColumn.AddChild(btnRestart);
		buttonColumn.AddChild(btnCredits);
		buttonColumn.AddChild(btnMenu);

		var buttonCenter = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		buttonCenter.AddChild(buttonColumn);
		box.AddChild(buttonCenter);

		btnRestart.GrabFocus();
	}

	private static Button MakeButton(string text)
	{
		return new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(360f, 52f)
		};
	}

	private void OnRestartPressed()
	{
		QueueFree();
		_player?.RestartRun();
	}

	private void OnCreditsPressed()
	{
		GetTree()?.ChangeSceneToFile(CreditsScene);
	}

	private void OnMenuPressed()
	{
		GetTree()?.ChangeSceneToFile(MainMenuScene);
	}
}
