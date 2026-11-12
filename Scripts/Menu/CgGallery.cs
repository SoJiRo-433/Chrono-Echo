using System.Collections.Generic;
using Godot;
using GDict = Godot.Collections.Dictionary;

/// <summary>One unlockable CG. It is gated by a narrative flag rather than by anything in the
/// image, so the gallery never has to load a file it has not unlocked.</summary>
public sealed class CgEntry
{
    public string Id { get; }
    public string Title { get; }
    public string RequiredFlag { get; }
    public string ImagePath { get; }

    public CgEntry(string id, string title, string requiredFlag, string imagePath)
    {
        Id = id;
        Title = title;
        RequiredFlag = requiredFlag;
        ImagePath = imagePath;
    }
}

/// <summary>The "CGs" screen from the main menu: every CG the player has unlocked, with the ones
/// still locked drawn as "???". Unlocks are read from the most recent save's flags, so the screen
/// works from the menu before any game has been loaded.</summary>
public partial class CgGallery : Control
{
    private const int Columns = 3;
    private const string LockedText = "???";
    private const float CardWidth = 300f;
    private const float CardHeight = 170f;

    // The CGs the demo actually shows. Two come out of the pendant's memory flash - the flash
    // itself, and the headache that follows it - and the third is the Atrium's observation-window
    // memory, which the Level 3 blueprint calls for ("CG: Aionia and Felix").
    // No CG art ships with the repo, so all three point at the one memory image; the gallery
    // still keeps them apart by flag, and draws "???" until each is unlocked.
    public static readonly CgEntry[] Catalog =
    {
        new CgEntry("pendant_flash", "The Pendant", "touched_pendant", "res://Assets/blue_light_memory.jpg"),
        new CgEntry("headache", "The Headache", "saw_headache", "res://Assets/blue_light_memory.jpg"),
        new CgEntry("atrium_window", "Aionia and Felix", "atrium_window_seen", "res://Assets/blue_light_memory.jpg")
    };

    public static CgGallery Open(Node parent)
    {
        var gallery = new CgGallery();
        parent.AddChild(gallery);
        return gallery;
    }

    public override void _Ready()
    {
        Theme = UiHelper.GetTheme();
        ProcessMode = ProcessModeEnum.Always;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.84f), MouseFilter = MouseFilterEnum.Stop };
        AddChild(dim);
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 28);
        AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 16);
        margin.AddChild(box);

        box.AddChild(new Label { Text = "CG Gallery", HorizontalAlignment = HorizontalAlignment.Center });

        HashSet<string> unlocked = ReadUnlockedFlags();

        var grid = new GridContainer { Columns = Columns };
        grid.AddThemeConstantOverride("h_separation", 18);
        grid.AddThemeConstantOverride("v_separation", 18);
        box.AddChild(grid);

        foreach (CgEntry entry in Catalog)
            grid.AddChild(BuildCard(entry, unlocked.Contains(entry.RequiredFlag)));

        var back = new Button { Text = "Back", CustomMinimumSize = new Vector2(220, 52) };
        back.Pressed += QueueFree;

        var backRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        backRow.AddChild(back);
        box.AddChild(backRow);
        back.GrabFocus();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            QueueFree();
        }
    }

    private static Control BuildCard(CgEntry entry, bool unlocked)
    {
        var card = new VBoxContainer { CustomMinimumSize = new Vector2(CardWidth, 0f) };
        card.AddThemeConstantOverride("separation", 6);

        // A plain Control (not a container) so the picture and the placeholder can both sit on
        // the same rectangle instead of stacking.
        var frame = new Control { CustomMinimumSize = new Vector2(CardWidth, CardHeight) };

        if (unlocked && ResourceLoader.Exists(entry.ImagePath))
        {
            var image = new TextureRect
            {
                Texture = GD.Load<Texture2D>(entry.ImagePath),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = MouseFilterEnum.Ignore
            };
            frame.AddChild(image);
            image.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }
        else
        {
            var ground = new ColorRect
            {
                Color = new Color(0.08f, 0.09f, 0.12f, 0.95f),
                MouseFilter = MouseFilterEnum.Ignore
            };
            frame.AddChild(ground);
            ground.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

            var question = new Label
            {
                Text = LockedText,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore
            };
            frame.AddChild(question);
            question.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }

        card.AddChild(frame);
        card.AddChild(new Label
        {
            Text = unlocked ? entry.Title : LockedText,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        });

        return card;
    }

    /// <summary>Unlocks come from the most recent save when there is one - so the gallery shows
    /// what the player has earned without loading a game - and from the live singleton otherwise.</summary>
    private static HashSet<string> ReadUnlockedFlags()
    {
        var unlocked = new HashSet<string>();

        if (SaveManager.LastSlot >= 0)
        {
            GDict data = SaveManager.ReadSlot(SaveManager.LastSlot);
            if (data != null && data.ContainsKey("state"))
            {
                GDict state = data["state"].AsGodotDictionary();
                if (state.ContainsKey("flags"))
                {
                    GDict flags = state["flags"].AsGodotDictionary();
                    foreach (Variant key in flags.Keys)
                        if (flags[key].AsBool()) unlocked.Add(key.AsString());
                    return unlocked;
                }
            }
        }

        GameStateManager live = GameStateManager.Instance;
        if (live != null)
            foreach (KeyValuePair<string, bool> pair in live.Flags)
                if (pair.Value) unlocked.Add(pair.Key);

        return unlocked;
    }
}
