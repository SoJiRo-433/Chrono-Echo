using Godot;

/// <summary>A character standing in the world, drawn from a four-frame strip: side-left, front,
/// side-right, back. The strip is sliced with plain arithmetic - the frames are packed into one
/// uniform cell grid - so a sheet needs nothing but a path and a frame count.
///
/// The node's origin sits at the character's feet, so a character can be dropped onto the floor
/// at the same point the interactable's box does without the art floating above the ground.
///
/// The frames face the player: which way a character looks is the one thing that makes a
/// still drawing read as a person standing in the room rather than a picture stuck to it.</summary>
public partial class CharacterSprite : Node2D
{
	// The four frames, in the order the strip packs them.
	    public static string FrontSpritePath(string characterId) => characterId?.ToLowerInvariant() switch
    {
        "adellaine" => "res://Assets/Characters/adellaine_front.png",
        "felix" => "res://Assets/Characters/felix_front.png",
        "hesiod" => "res://Assets/Characters/hesiod_front.png",
        "yoruka" => "res://Assets/Characters/yoruka_front.png",
        _ => ""
    };

    private const int LeftFrame = 0;
	private const int FrontFrame = 1;
	private const int RightFrame = 2;
	private const int BackFrame = 3;

	/// <summary>Which way a character looks. TowardPlayer turns them as she walks around them;
	/// the fixed directions are for the scenes that stage a character deliberately - Felix at
	/// the window in Level 7 is meant to stand with his back to Aionia.</summary>
	public enum Facing
	{
		TowardPlayer,
		North,
		South,
		East,
		West
	}

	[Export] public string SheetPath { get; set; }
	[Export] public int FrameCount { get; set; } = 1;

	/// <summary>Multiplies the art's own pixel size. The strips are drawn at one art pixel per
	/// world unit, which puts a character a little taller than the player - who is a 28-unit
	/// circle - so they read as people and not as furniture.</summary>
	[Export] public float PixelScale { get; set; } = 1f;

	[Export] public Facing Look { get; set; } = Facing.TowardPlayer;

	/// <summary>Nudges the art off the node's origin, for the rare case where a character has to
	/// stand slightly off the point the room placed them at.</summary>
	[Export] public Vector2 Offset { get; set; } = Vector2.Zero;

	private Sprite2D _sprite;
	private AtlasTexture _atlas;
	private int _frame = FrontFrame;

	/// <summary>The art's size in world units, before PixelScale. A room can use this to place a
	/// caption under a character without guessing.</summary>
	public float ArtWidth { get; private set; }
	public float ArtHeight { get; private set; }

	public override void _Ready()
	{
		// The floor of a code-built level is added after the actors, so without an explicit layer
		// the map paints over anyone standing on it (see Entity's ActorZIndex).
		ZIndex = 12;

		if (SheetPath == "") return;
		Texture2D sheet = ResourceLoader.Load<Texture2D>(SheetPath);
		if (sheet == null)
		{
			GD.PushError("CharacterSprite: no sheet at '" + SheetPath + "'.");
			return;
		}

		int count = Mathf.Max(1, FrameCount);
		ArtWidth = sheet.GetWidth() / (float)count;
		ArtHeight = sheet.GetHeight();

		_atlas = new AtlasTexture { Atlas = sheet, Region = new Rect2(0, 0, ArtWidth, ArtHeight) };
		_sprite = new Sprite2D
		{
			Texture = _atlas,
			Centered = false,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			Scale = Vector2.One * PixelScale,
			// The sprite's own origin is its top-left, so it is shifted to put the character's
			// feet on this node's origin and their middle on its x.
			Position = new Vector2(-ArtWidth * PixelScale * 0.5f, -ArtHeight * PixelScale) + Offset
		};
		AddChild(_sprite);
		ApplyFrame(FrameCount <= 1 ? 0 : FrontFrame);
	}

	public override void _Process(double delta)
	{
		if (_atlas == null || FrameCount <= 1) return;
		if (Look != Facing.TowardPlayer)
		{
			ApplyFrame(FrameFor(Look));
			return;
		}
		FaceThePlayerIfItCan();
	}

	/// <summary>Turns the character to look at the player. The horizontal axis wins a tie, because
	/// a character that is level with the player is easier to read from the side.</summary>
	private void FaceThePlayerIfItCan()
	{
		Player player = GameStateManager.Instance?.Player;
		if (player == null || !player.IsAlive) return;

		Vector2 toPlayer = player.GlobalPosition - GlobalPosition;
		if (toPlayer.LengthSquared() < 1f) return;

		int frame;
		if (Mathf.Abs(toPlayer.X) > Mathf.Abs(toPlayer.Y))
			frame = toPlayer.X < 0f ? LeftFrame : RightFrame;
		else
			frame = toPlayer.Y > 0f ? FrontFrame : BackFrame;
		ApplyFrame(frame);
	}

	private static int FrameFor(Facing look) => look switch
	{
		Facing.North => BackFrame,
		Facing.South => FrontFrame,
		Facing.East => RightFrame,
		Facing.West => LeftFrame,
		_ => FrontFrame
	};

	private void ApplyFrame(int frame)
	{
		if (_atlas == null || frame == _frame && _atlas.Region.Position.X == frame * ArtWidth) return;
		_frame = frame;
		_atlas.Region = new Rect2(frame * ArtWidth, 0, ArtWidth, ArtHeight);
	}
}
