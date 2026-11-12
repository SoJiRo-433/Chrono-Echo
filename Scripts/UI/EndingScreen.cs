using Godot;

/// <summary>Placeholder ending screen. Nothing sends the player here yet - the levels that would
/// trigger an ending are not built - but the scene itself is complete: it asks the singleton
/// which ending was reached and shows that ending's title and blurb, then offers the way back to
/// the menu. Point a room's NextLevelPath at res://Scenes/EndingScreen.tscn to use it.</summary>
public partial class EndingScreen : Control
{
    private const string MainMenuScene = "res://main_menu.tscn";

    public override void _Ready()
    {
        Theme = UiHelper.GetTheme();
        ProcessMode = ProcessModeEnum.Always;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        GetTree().Paused = false;

        string endingId = GameStateManager.Instance?.ResolveEnding() ?? "neutral_good";
        (string title, string blurb) = Describe(endingId);

        var ground = new ColorRect { Color = new Color(0.03f, 0.04f, 0.07f, 1f), MouseFilter = MouseFilterEnum.Stop };
        AddChild(ground);
        ground.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var center = new CenterContainer();
        AddChild(center);
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var box = new VBoxContainer { CustomMinimumSize = new Vector2(760f, 0f) };
        box.AddThemeConstantOverride("separation", 18);
        center.AddChild(box);

        var heading = new Label { Text = title, HorizontalAlignment = HorizontalAlignment.Center };
        heading.AddThemeFontSizeOverride("font_size", 40);
        heading.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.45f));
        box.AddChild(heading);

        var id = new Label { Text = "ending id: " + endingId, HorizontalAlignment = HorizontalAlignment.Center };
        id.AddThemeFontSizeOverride("font_size", 13);
        id.AddThemeColorOverride("font_color", new Color(0.6f, 0.64f, 0.7f));
        box.AddChild(id);

        var body = new Label
        {
            Text = blurb,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(760f, 0f)
        };
        box.AddChild(body);

        var back = new Button { Text = "Back to the menu", CustomMinimumSize = new Vector2(320f, 56f) };
        back.Pressed += () => GetTree().ChangeSceneToFile(MainMenuScene);

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddChild(back);
        box.AddChild(row);
        back.GrabFocus();
    }

    /// <summary>The ids GameStateManager.ResolveEnding can return, with the doc's own wording.</summary>
    private static (string Title, string Blurb) Describe(string endingId)
    {
        switch (endingId)
        {
            case "true_ending":
                return ("True Ending",
                    "You and Felix are safe, and the timelines are set right. The realities are changed so that the two of you come through it - and you forget it all for good.");
            case "neutral_good":
                return ("Neutral Good Ending",
                    "The timeline is fixed and you are safe. Whatever else was lost stays lost.");
            case "sacrificial":
                return ("Sacrificial Ending",
                    "You go through with the plan and erase yourself from every timeline. Felix is distraught; he is the only one who remembers you.");
            case "no_more_time":
                return ("No More Time Ending",
                    "You never worked out what really happened. The ruins keep their secret and you stay lost inside them.");
            case "yoruka_good":
                return ("Yoruka - Good Ending",
                    "Yoruka learns to stand without anyone's approval, you remember, and the timeline is fixed.");
            case "yoruka_bad":
                return ("Yoruka - Bad Ending",
                    "The timeline is never fixed, and what Yoruka wanted from you turns into something with teeth.");
            case "yoruka_alternate":
                return ("Yoruka - Alternate Ending",
                    "Yoruka takes Felix instead, and the timeline is fixed anyway.");
            case "adellaine_good":
                return ("Adellaine - Good Ending",
                    "The timeline is fixed, and the two of you build a quiet life away from the Institute. She becomes a veterinarian.");
            case "adellaine_bad":
                return ("Adellaine - Bad Ending",
                    "The timeline is never fixed. She stops trusting you, destroys the fragments, and leaves you alone with them.");
            case "hesiod_good":
                return ("Hesiod - Good Ending",
                    "The timeline is fixed. You and Hesiod take care of the company - and of each other.");
            case "hesiod_bad":
                return ("Hesiod - Bad Ending",
                    "The timeline is never fixed. He uses the fragments, and the price of fixing it turns out to be you.");
            case "felix_bad_together":
                return ("Felix - Bad Ending, Together",
                    "He could not let you go, and neither of you leaves the collapse.");
            case "felix_bad":
                return ("Felix - Bad Ending",
                    "The timeline is fixed, and his delusion is fed instead of broken.");
            case "felix_good":
                return ("Felix - Good Ending",
                    "The timeline is fixed, and you talk him out of the rest of it.");
            default:
                return ("Ending", "The story stops here for now.");
        }
    }
}
