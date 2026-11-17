using Godot;

/// <summary>The two pieces of the interface that are drawn from art rather than from plain controls,
/// in one place, so no caller has to know a file path, a texture region or a scaling rule.
///
/// Only the hearts and the dialogue box use the sheets in <c>res://Assets/UI/</c>. The rest of the
/// interface - the objective line, the banners, the world labels, the menus - is built from ordinary
/// controls.
///
/// - the <b>hearts</b> are a small pixel-art sheet: three separate 39x40 drawings, drawn at their own
///   size. A spent heart is a tint of the same drawing, not a second drawing.
/// - the <b>dialogue frame</b> is a hand-drawn fine-line box carrying a speaker tab that hangs over
///   its own top edge, so it is drawn whole at the sheet's proportions and never nine-patched: a
///   nine-patch stretches the middle of every edge, which would smear the tab across the top border.
///   The <b>portrait plate</b> is drawn whole for the same reason - its cut corners are the point of
///   it - and is only ever shown when there is a portrait to put in it.
/// </summary>
public static class UiSkin
{
    #region Tints
    /// <summary>Hearts already spent.</summary>
    public static readonly Color SpentHeart = new(0.24f, 0.26f, 0.32f, 0.9f);
    #endregion

    #region Sheets
    private static Texture2D _hearts;
    private static Texture2D _dialogueFrame;
    private static Texture2D _portraitFrame;

    public static Texture2D Hearts => _hearts ??= GD.Load<Texture2D>("res://Assets/UI/ui_hearts.png");
    public static Texture2D DialogueFrame => _dialogueFrame ??= GD.Load<Texture2D>("res://Assets/UI/ui_dialogue_panel.png");
    public static Texture2D PortraitFrame => _portraitFrame ??= GD.Load<Texture2D>("res://Assets/UI/ui_portrait_frame.png");
    #endregion

    #region Hearts
    /// <summary>How many hearts the sheet draws. Entity.MaxHealth is three, so a life is one row.</summary>
    public const int HeartCount = 3;
    public const float HeartStride = 49f;
    public static readonly Vector2 HeartSize = new(39f, 40f);

    // The sheet holds three separate 39x40 drawings, so each heart is addressed by its own cell
    // rather than by repeating the first one.
    private static readonly Rect2[] HeartCells =
    {
        new(7f, 1f, 39f, 40f),
        new(56f, 1f, 39f, 40f),
        new(104f, 1f, 39f, 40f)
    };

    public static Texture2D Heart(int index) => new AtlasTexture
    {
        Atlas = Hearts,
        Region = HeartCells[Mathf.Clamp(index, 0, HeartCount - 1)],
        FilterClip = true
    };
    #endregion

    #region Dialogue frame (drawn whole)
    public static readonly Vector2 DialogueSheetSize = new(1408f, 433f);

    /// <summary>The body's black interior on the sheet, starting below the speaker tab - which the
    /// sheet draws hanging over the body's own top-left corner.</summary>
    public static readonly Rect2 DialogueTextRegion = new(58f, 142f, 1292f, 234f);

    /// <summary>The speaker tab on the sheet: its name goes in here, not above the box.</summary>
    public static readonly Rect2 DialogueTabRegion = new(96f, 22f, 419f, 109f);

    public static TextureRect DialoguePlate(Vector2 size)
    {
        var plate = new TextureRect
        {
            Texture = DialogueFrame,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        // Set last: until ExpandMode is IgnoreSize above, the texture's own size is this control's
        // minimum, and any size assigned before that point is clamped straight back up to it.
        plate.Size = size;
        return plate;
    }

    /// <summary>Maps a rectangle measured on the sheet into a frame drawn at <paramref name="frameSize"/>,
    /// in the frame's own coordinates - so the result can position children of the frame directly.
    /// The frame is always drawn at the sheet's proportions.</summary>
    public static Rect2 DialogueLocal(Rect2 sheetRegion, Vector2 frameSize)
    {
        float scale = frameSize.X / DialogueSheetSize.X;
        return new Rect2(sheetRegion.Position * scale, sheetRegion.Size * scale);
    }
    #endregion

    #region Portrait frame (drawn whole)
    // The sheet leaves a wide margin of empty space around the drawing, so the plate is cropped to
    // the drawing itself and the window inside the frame is expressed as a share of that crop.
    private static readonly Rect2 PortraitRegion = new(193f, 30f, 732f, 706f);
    private static readonly Rect2 PortraitWindowOnSheet = new(42f, 43f, 646f, 602f);

    /// <summary>Width over height of the frame as it is drawn.</summary>
    public const float PortraitAspect = 732f / 706f;

    public static TextureRect PortraitPlate(Vector2 size)
    {
        var plate = new TextureRect
        {
            Texture = new AtlasTexture { Atlas = PortraitFrame, Region = PortraitRegion, FilterClip = true },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        // Set last: until ExpandMode is IgnoreSize above, the texture's own size is this control's
        // minimum, and any size assigned before that point is clamped straight back up to it.
        plate.Size = size;
        return plate;
    }

    /// <summary>Where a picture sits inside a frame of <paramref name="frameSize"/>, in the frame's
    /// own coordinates.</summary>
    public static Rect2 PortraitWindow(Vector2 frameSize)
    {
        Vector2 scale = new(frameSize.X / PortraitRegion.Size.X, frameSize.Y / PortraitRegion.Size.Y);
        return new Rect2(PortraitWindowOnSheet.Position * scale, PortraitWindowOnSheet.Size * scale);
    }
    #endregion
}
