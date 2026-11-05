using System.Collections.Generic;
using Godot;

public partial class DialogueManager : Node
{
	#region Signals
	[Signal] public delegate void DialogueStartedEventHandler(string dialogueId);
	[Signal] public delegate void LineChangedEventHandler(string speaker, string text);
	[Signal] public delegate void ChoicesPresentedEventHandler(Godot.Collections.Array<string> choices);
	[Signal] public delegate void DialogueEndedEventHandler(string dialogueId);
	[Signal] public delegate void MemoryFragmentUnlockedEventHandler(string fragmentId, string title, string text);
	#endregion

	#region Private state
	private readonly Dictionary<string, DialogueData> _library = new();
	private readonly List<MemoryFragment> _fragments = new();
	private readonly List<MemoryFragment> _unlockedFragments = new();
	private readonly HashSet<string> _unlockedIds = new();
	private readonly List<DialogueChoice> _visibleChoices = new();

	private DialogueData _currentData;
	private DialogueNode _currentNode;
	private int _lineIndex;
	private bool _isActive;
	private bool _awaitingChoice;
	private float _choiceTimeout;
	private float _choiceTimer;

	// placeholder UI
	private CanvasLayer _layer;
	private TextureRect _memoryImage;
	private TextureRect _portrait;
	private ColorRect _panel;
	private Label _speakerLabel;
	private Label _dialogueLabel;
	private Label _toastLabel;

	private string _currentObjective = "";
	private string _platePath = "";
	#endregion

	private const string ObjectiveSpeaker = "Objective";
	private const string PortraitFolder = "res://Assets/Portraits/";

	// The one CG image the repo ships. The pendant's flash is drawn with it, and any node that
	// declares a plate of its own (the Atrium's window) draws it too - the gallery keeps the CGs
	// apart by flag even while they share a placeholder picture.
	private const string MemoryCgPath = "res://Assets/blue_light_memory.jpg";

	#region Public properties
	[Export] public bool ShowDebugOverlay { get; set; } = true;
	public bool IsActive => _isActive;
	public IReadOnlyList<MemoryFragment> UnlockedFragments => _unlockedFragments;

	/// <summary>The last Objective line shown, kept on screen by the HUD until the next one
	/// replaces it. Empty until an Objective has been read in this run.</summary>
	public string CurrentObjective => _currentObjective;

	public int FragmentCount => _fragments.Count;
	#endregion

	#region Lifecycle
	public override void _Ready()
	{
		BuildDemoContent();
		if (ShowDebugOverlay) BuildOverlay();

		if (GameStateManager.Instance != null)
		{
			GameStateManager.Instance.FlagChanged += OnFlagChanged;
			GameStateManager.Instance.FlagsReset += OnFlagsReset;
		}
	}

	public override void _ExitTree()
	{
		if (GameStateManager.Instance != null)
		{
			GameStateManager.Instance.FlagChanged -= OnFlagChanged;
			GameStateManager.Instance.FlagsReset -= OnFlagsReset;
		}
	}

	public override void _Process(double delta)
	{
		if (!_isActive || !_awaitingChoice || _choiceTimeout <= 0f) return;

		// A timed choice: when the clock runs out she takes the option that was left last.
		_choiceTimer -= (float)delta;
		if (_choiceTimer <= 0f)
		{
			SelectChoice(_visibleChoices.Count - 1);
			return;
		}
		RefreshOverlay();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!_isActive || _currentNode == null) return;
		if (!(@event is InputEventKey key) || !key.Pressed || key.IsEcho()) return;

		bool advance = key.Keycode == Key.Enter || key.Keycode == Key.KpEnter || key.Keycode == Key.Space;
		bool choose = key.Keycode >= Key.Key1 && key.Keycode <= Key.Key9;
		if (!advance && !choose) return;

		// Claim the event BEFORE acting. Advancing the last line of a closing conversation
		// hands over to the next level, and the scene change frees this node - so touching
		// the viewport afterwards dereferences a node that no longer exists.
		GetViewport()?.SetInputAsHandled();

		if (advance) Advance();
		else SelectChoice((int)key.Keycode - (int)Key.Key1);
	}
	#endregion

	#region Public API
	public void RegisterDialogue(DialogueData data) { _library[data.Id] = data; }

	public void RegisterFragment(MemoryFragment fragment) { _fragments.Add(fragment); }

	public bool StartDialogue(string dialogueId)
	{
		if (_isActive || !_library.TryGetValue(dialogueId, out DialogueData data)) return false;

		_currentData = data;
		_isActive = true;
		GameStateManager.Instance?.SetPaused(true);   // freeze gameplay
		EmitSignal(SignalName.DialogueStarted, dialogueId);
		EnterNode(data.StartNodeId);
		return true;
	}

	public void Advance()
	{
		if (!_isActive || _awaitingChoice) return;

		if (_lineIndex < _currentNode.Lines.Count - 1)
		{
			_lineIndex++;
			ShowCurrentLine();
			return;
		}

		if (!string.IsNullOrEmpty(_currentNode.NextNodeId)) EnterNode(_currentNode.NextNodeId);
		else EndDialogue();
	}

	public void SelectChoice(int index)
	{
		if (!_isActive || !_awaitingChoice || index < 0 || index >= _visibleChoices.Count) return;

		DialogueChoice choice = _visibleChoices[index];
		_awaitingChoice = false;

		// Apply the outcome to the single source of truth.
		if (!string.IsNullOrEmpty(choice.FlagToSet))
			GameStateManager.Instance?.SetFlag(choice.FlagToSet, choice.FlagValue);

		// Choices move the relationship counters as well as the flags.
		if (!string.IsNullOrEmpty(choice.AffectionCharacter))
			GameStateManager.Instance?.AddAffection(choice.AffectionCharacter, choice.AffectionDelta);

		if (!string.IsNullOrEmpty(choice.NextNodeId)) EnterNode(choice.NextNodeId);
		else EndDialogue();
	}
	#endregion

	#region Internal flow
	private void EnterNode(string nodeId)
	{
		DialogueNode node = _currentData.GetNode(nodeId);
		if (node == null || node.Lines.Count == 0) { EndDialogue(); return; }

		_currentNode = node;
		_lineIndex = 0;

		if (!string.IsNullOrEmpty(node.FlagOnEnter))
			GameStateManager.Instance?.SetFlag(node.FlagOnEnter, node.FlagOnEnterValue);

		ShowCurrentLine();
	}

	private void ShowCurrentLine()
	{
		EmitSignal(SignalName.LineChanged, _currentNode.Speaker, _currentNode.Lines[_lineIndex]);

		// An Objective line is also the HUD's "Current objective": it replaces the previous one
		// and stays on screen long after this dialogue box has closed.
		if (_currentNode.Speaker == ObjectiveSpeaker) _currentObjective = _currentNode.Lines[_lineIndex];

		_awaitingChoice = false;
		_visibleChoices.Clear();
		_choiceTimeout = 0f;
		_choiceTimer = 0f;

		// Choices appear together with the LAST line of the node.
		if (_lineIndex == _currentNode.Lines.Count - 1)
		{
			foreach (DialogueChoice choice in _currentNode.Choices)
				if (choice.IsAvailable()) _visibleChoices.Add(choice);

			if (_visibleChoices.Count > 0)
			{
				_awaitingChoice = true;
				_choiceTimeout = _currentNode.ChoiceTimeoutSeconds;
				_choiceTimer = _choiceTimeout;
				var texts = new Godot.Collections.Array<string>();
				foreach (DialogueChoice choice in _visibleChoices) texts.Add(choice.Text);
				EmitSignal(SignalName.ChoicesPresented, texts);
			}
		}
		RefreshOverlay();
	}

	private void EndDialogue()
	{
		string finishedId = _currentData?.Id ?? "";
		_isActive = false;
		_awaitingChoice = false;
		_currentData = null;
		_currentNode = null;
		GameStateManager.Instance?.SetPaused(false);   // resume gameplay
		RefreshOverlay();
		EmitSignal(SignalName.DialogueEnded, finishedId);
		CheckMemoryFragments();
	}
	#endregion

	#region Memory fragments
	private void OnFlagChanged(string flagName, bool value) { CheckMemoryFragments(); }

	private void OnFlagsReset()
	{
		_unlockedIds.Clear();
		_unlockedFragments.Clear();
	}

	private void CheckMemoryFragments()
	{
		GameStateManager state = GameStateManager.Instance;
		if (state == null) return;

		foreach (MemoryFragment fragment in _fragments)
		{
			if (_unlockedIds.Contains(fragment.Id) || !state.GetFlag(fragment.RequiredFlag)) continue;

			_unlockedIds.Add(fragment.Id);               // mark first: prevents re-entry via FlagChanged
			_unlockedFragments.Add(fragment);
			state.SetFlag("memory_" + fragment.Id, true);
			EmitSignal(SignalName.MemoryFragmentUnlocked, fragment.Id, fragment.Title, fragment.Text);
			ShowToast("MEMORY UNLOCKED - " + fragment.Title + "\n" + fragment.Text);
		}
	}
	#endregion

	#region Placeholder overlay (no external assets)
	private void BuildOverlay()
	{
		_layer = new CanvasLayer { Layer = 10 };

		_memoryImage = new TextureRect
		{
			Texture = GD.Load<Texture2D>(MemoryCgPath),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = false
		};
		_memoryImage.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_layer.AddChild(_memoryImage);

		// Portrait slot: filled from res://Assets/Portraits/<speaker>.png when that file exists,
		// and simply left hidden when it does not - so the game runs with no portrait art at all.
		_portrait = new TextureRect
		{
			Position = new Vector2(20f, 210f),
			Size = new Vector2(200f, 230f),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspect,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = false
		};

		_panel = new ColorRect { Color = new Color(0f, 0f, 0f, 0.78f), Position = new Vector2(20f, 450f), Size = new Vector2(1112f, 180f), Visible = false };

		// The speaker's name is its own label sitting above the box (not a "Name:" prefix on the
		// line), tinted with that speaker's colour.
		_speakerLabel = new Label
		{
			Position = new Vector2(24f, -32f),
			Size = new Vector2(1064f, 30f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_speakerLabel.AddThemeFontSizeOverride("font_size", 21);
		_speakerLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		_speakerLabel.AddThemeConstantOverride("outline_size", 5);
		_panel.AddChild(_speakerLabel);

		_dialogueLabel = new Label { Position = new Vector2(16f, 26f), Size = new Vector2(1080f, 146f), AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_dialogueLabel.AddThemeFontSizeOverride("font_size", 20);
		_panel.AddChild(_dialogueLabel);

		_toastLabel = new Label { Position = new Vector2(20f, 380f), Size = new Vector2(1100f, 60f), AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false };
		_toastLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.4f));
		_toastLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		_toastLabel.AddThemeConstantOverride("outline_size", 4);

		_layer.AddChild(_portrait);
		_layer.AddChild(_panel);
		_layer.AddChild(_toastLabel);
		AddChild(_layer);
	}

	private void RefreshOverlay()
	{
		RefreshPlate();

		if (_panel == null) return;
		_panel.Visible = _isActive;
		if (!_isActive || _currentNode == null)
		{
			if (_portrait != null) _portrait.Visible = false;
			return;
		}

		string speaker = _currentNode.Speaker;
		Color colour = SpeakerColor(speaker);

		_speakerLabel.Text = speaker;
		_speakerLabel.AddThemeColorOverride("font_color", colour);
		RefreshPortrait(speaker);

		string text = _currentNode.Lines[_lineIndex];
		if (_awaitingChoice)
		{
			text += "\n";
			for (int i = 0; i < _visibleChoices.Count; i++)
				text += "\n  [" + (i + 1) + "]  " + _visibleChoices[i].Text;

			if (_choiceTimeout > 0f)
				text += "\n\n  " + Mathf.Max(0f, _choiceTimer).ToString("0.0") + "s";
		}
		else
		{
			text += "\n\n  [Enter] continue";
		}
		_dialogueLabel.Text = text;
	}

	/// <summary>The CG plate behind the box. Two things can put a picture there: the pendant's
	/// flash, which is the memory the whole game opens with, and any node that declares one of its
	/// own - the Atrium's window shows its CG while she is remembering and drops it for the beats
	/// that are back in the room.</summary>
	private void RefreshPlate()
	{
		if (_memoryImage == null) return;

		bool flash = _isActive && _currentData?.Id == "pendant_flash"
		             && _currentNode?.Id != "headache";   // the headache is the pain, not the memory

		string declared = _isActive ? _currentNode?.PlateImagePath ?? "" : "";
		string path = declared != "" ? declared : flash ? MemoryCgPath : "";

		if (path != _platePath)
		{
			_platePath = path;
			_memoryImage.Texture = path == "" || !ResourceLoader.Exists(path) ? null : GD.Load<Texture2D>(path);
		}

		_memoryImage.Visible = path != "";
	}

	/// <summary>Fills the portrait slot from res://Assets/Portraits/&lt;speaker&gt;.png when that
	/// file exists, and hides the slot otherwise. Missing art is not an error.</summary>
	private void RefreshPortrait(string speaker)
	{
		if (_portrait == null) return;

		string path = PortraitFolder + speaker.Replace(' ', '_') + ".png";
		Texture2D texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;

		_portrait.Texture = texture;
		_portrait.Visible = texture != null;
	}

	/// <summary>One colour per speaker. The five named characters are eye/hair-coded; the
	/// Narrator, Instructions and Objective voices read as caption styles, not as people.
	/// (The doc's profile pages do not specify colours, so these are chosen to be distinct.)</summary>
	private static Color SpeakerColor(string speaker)
	{
		switch (speaker)
		{
			case "Aionia Avgerinos": return new Color(0.63f, 0.87f, 1f);     // cold blue of the pendant
			case "Mochizuki Yoruka": return new Color(1f, 0.66f, 0.82f);     // jirai-kei pink
			case "Felix Lasarewitch": return new Color(1f, 0.85f, 0.45f);    // the institute's golden boy
			case "Adellaine Moriarty": return new Color(0.66f, 1f, 0.78f);   // medicine green
			case "Hesiod Moriarty": return new Color(0.85f, 0.78f, 1f);      // photography violet
			case ObjectiveSpeaker: return new Color(1f, 0.86f, 0.45f);
			case "Instructions": return new Color(0.87f, 0.93f, 0.62f);
			case "Narrator": return new Color(0.72f, 0.76f, 0.84f);
			default: return new Color(0.9f, 0.9f, 0.9f);
		}
	}

	private void ShowToast(string message)
	{
		GD.Print(message);
		if (_toastLabel == null) return;
		_toastLabel.Text = message;
		_toastLabel.Visible = true;
		GetTree().CreateTimer(6.0).Timeout += () => { if (IsInstanceValid(_toastLabel)) _toastLabel.Visible = false; };
	}
	#endregion

	#region Story content (laid out like the OOP blueprint)
	// The narrative from the design doc, wired into the blueprint's floor plan.
	// Protagonist: Aionia Avgerinos. Setting: the ruined Aetherion Institute.
	// The room itself is built in Scenes/Rooms/Room01.tscn - this is only the words.
	private void BuildDemoContent()
	{
		// --- Scene 1: waking up at the west side of the room ---
		RegisterDialogue(new DialogueData("intro")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("W-what happened? Where am I?")
				.SetNext("wake"))
			.AddNode(new DialogueNode("wake", "Narrator")
				.AddLine("The space around you looks like it has been abandoned. You find scattered papers, broken machines, and broken glasses.")
				.AddLine("You should look around and find out what happened.")
				.SetNext("move"))
			.AddNode(new DialogueNode("move", "Instructions")
				.AddLine("Use W,A,S,D to move.")));

		// --- Scene 2-3: the report log, page by page, ending on the burnt photo ---
		RegisterDialogue(new DialogueData("report_log")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("Project Hyperion, to traverse time and space…Interesting")
				.AddChoice(new DialogueChoice("Turn the page", "photo", "saw_burnt_photo")))
			.AddNode(new DialogueNode("photo", "Narrator")
				.AddLine("In the picture, it shows a figure of you and another person facing away, looking towards a machine in the same room that you’re standing in right now.")
				.SetNext("photo_reaction"))
			.AddNode(new DialogueNode("photo_reaction", "Aionia Avgerinos")
				.AddLine("That looks like me, and someone else…But why do I not remember who that is?")
				.AddLine("I should keep this.")
				.AddLine("I should check around, maybe that will help me find clues")));

		RegisterDialogue(new DialogueData("report_log_again")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("I should check around, maybe that will help me find clues")));

		// --- Scene 3: everything else worth examining ---
		RegisterDialogue(new DialogueData("drawers")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("Folders, stack of papers, and nothing worth to note..")));

		RegisterDialogue(new DialogueData("time_machine")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("Probably a machine to churn out data.")));

		RegisterDialogue(new DialogueData("cabinet")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("Just some mechanical parts.")));

		RegisterDialogue(new DialogueData("broken_machinery")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("Just some mechanical parts.")));

		// Scene 3 interactables straight from the doc: the boxes and the loose photos.
		RegisterDialogue(new DialogueData("boxes")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("Just some mechanical parts.")));

		RegisterDialogue(new DialogueData("photos")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("Pictures of the room, the students working on it, and mostly just documentation. You can’t seem to identify who these students are though.")));

		// --- Scene 4-5: the pendant, and the memory it drags up with it ---
		RegisterDialogue(new DialogueData("pendant_flash")
			.AddNode(new DialogueNode("pickup", "Narrator")
				.AddLine("After looking around the room, you notice a shining, blue light, on the floor. You picked it up.")
				.SetNext("pickup_question"))
			.AddNode(new DialogueNode("pickup_question", "Aionia Avgerinos")
				.AddLine("What is this?")
				.SetNext("buttons"))
			.AddNode(new DialogueNode("buttons", "Narrator")
				.AddLine("You tried pushing the buttons of the pendant.")
				.AddLine("It somehow reminded you of a memory from a long time ago. You remember talking to a blonde person, and he seems very important to you.")
				.SetNext("felix_offer"))
			.AddNode(new DialogueNode("felix_offer", "Felix Lasarewitch")
				.AddLine("Hey, I want to give something to you.")
				.SetNext("aionia_question"))
			.AddNode(new DialogueNode("aionia_question", "Aionia Avgerinos")
				.AddLine("And this is?")
				.SetNext("felix_explain"))
			.AddNode(new DialogueNode("felix_explain", "Felix Lasarewitch")
				.AddLine("Since our study—project, rather—is time. I figured you need a proper physical failsafe.")
				.AddLine("Consider it a specialized remote controller for the Hyperion core mainframe. If the spatial trajectory fluctuates while we're testing, use this override frequency to anchor your position and recalibrate the system.")
				.AddLine("Make sure you keep it close, alright? No matter where the data takes us... I need you to be safe.")
				.SetNext("headache"))
			.AddNode(new DialogueNode("headache", "Aionia Avgerinos")
				.AddLine("Argh! What just happened?!")
				.AddLine("Why did I suddenly remember someone?")
				.SetFlagOnEnter("saw_headache")
				.SetNext("echo_reveal"))
			.AddNode(new DialogueNode("echo_reveal", "Narrator")
				.AddLine("While trying to relive the memory, a blue light appeared in front of you.")
				.AddLine("It is a figure of you—a projection of yourself. It touched its head in pain, mimicking your action earlier.")
				.SetNext("echo_first_reaction"))
			.AddNode(new DialogueNode("echo_first_reaction", "Aionia Avgerinos")
				.AddLine("Wha…It copies my movements?")
				.AddLine("I need to test this out.")
				.SetNext("echo_instruction"))
			.AddNode(new DialogueNode("echo_instruction", "Narrator")
				.AddLine("Press T to start recording movement. Press T again to stop. Press R to project the Echo.")
				.SetNext("echo_reaction"))
			.AddNode(new DialogueNode("echo_reaction", "Aionia Avgerinos")
				.AddLine("Hah…Amazing")
				.AddLine("I can use this to get out of here.")));

		// Scene 7 opener: the line above used to be duplicated into the flash, so the beat where
		// she connects the Echo to the sealed door lives here instead - it fires the first time
		// the player walks up to the security door or its pressure plate (see Door.ApproachDialogueId).
		RegisterDialogue(new DialogueData("room01_plate")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("The security door is sealed, and this plate needs weight to stay down.")
				.AddLine("I can use the pendant to get out of here.")));

				// --- Scene 6-7: the Echo is what gets you through the security door ---
		RegisterDialogue(new DialogueData("room01_clear")
			.AddNode(new DialogueNode("start", "Narrator")
				.AddLine("You’ve made it out, with only the burnt photo on your hand and the pendant someone you knew gave to you.")
				.AddLine("You should check outside.")));

		// Level 2: the library bridgeway, archives and Yoruka encounter.
		RegisterDialogue(new DialogueData("room02_intro")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("I need to cross this hallway, but the doors are closed.")
				.AddLine("There should be a way to open the doors.")
				.AddLine("!!!!")
				.AddLine("Those plates can be a way to open those doors.")
				.SetNext("objective"))
			.AddNode(new DialogueNode("objective", "Objective")
				.AddLine("Use your Echo to find a way to open the door.")));

		// Level 2 Scene 1 ends here: the hallway is the last stretch before the Archives.
		RegisterDialogue(new DialogueData("room02_clear")
			.AddNode(new DialogueNode("start", "Narrator")
				.AddLine("The plates hold the last door open long enough for you to slip through.")
				.AddLine("Beyond it the corridor widens, and the smell of dust gives way to old paper.")
				.SetNext("objective"))
			.AddNode(new DialogueNode("objective", "Objective")
				.AddLine("Head for the Archives and look for anything that survived.")));

		// The Archives open on the line that used to sit in Room02's closing beat.
		RegisterDialogue(new DialogueData("library_intro")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("Another student? Glad that I am not alone here, I should approach her.")));

		RegisterDialogue(new DialogueData("library_yoruka")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("Uhm, excuse me?")
				.SetNext("question"))
			.AddNode(new DialogueNode("question", "Mochizuki Yoruka")
				.AddLine("!!")
				.AddLine("Senior!! Why are you here? Shouldn’t you be fixing this mess right now?")
				.AddChoice(new DialogueChoice("Fixing what? I don’t even know what is happening here.", "option_a", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 1))
				.AddChoice(new DialogueChoice("Do I know you? My head is completely blank right now.", "option_b", "asked_identity", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 1))
				.AddChoice(new DialogueChoice("You look terrified. Are you alright?", "option_c", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 2)))
			.AddNode(new DialogueNode("option_a", "Mochizuki Yoruka")
				.AddLine("I-I-I WHAT?! The entire temporal matrix in this sector is collapsing, how can you be so calm right now!")
				.SetNext("aionia_remembers"))
			.AddNode(new DialogueNode("aionia_remembers", "Aionia Avgerinos")
				.AddLine("I…don’t remember.")
				.SetNext("outburst"))
			.AddNode(new DialogueNode("option_b", "Mochizuki Yoruka")
				.AddLine("Don't joke around with me! This isn't the time for your games. If you're trying to run away from your responsibility because everything’s fried, it won't work—everyone's depending on you!")
				.SetNext("outburst"))
			.AddNode(new DialogueNode("option_c", "Mochizuki Yoruka")
				.AddLine("I-I am not terrified! I'm just... highly alert! Unlike some people who act completely unfazed while reality itself tears apart at the seams!")
				.SetNext("aionia_panicking"))
			.AddNode(new DialogueNode("aionia_panicking", "Aionia Avgerinos")
				.AddLine("Panicking doesn't seem very efficient.")
				.SetNext("outburst"))
			.AddNode(new DialogueNode("outburst", "Mochizuki Yoruka")
				.AddLine("Unbelievable! I hate you! I literally don't have time to deal with your amnesiac act right now! If you want to get yourself permanently erased by a localized time-loop and left as a forgotten corpse, go right ahead!")
				.AddLine("Just stay out of my sight!")
				.SetNext("runs"))
			.AddNode(new DialogueNode("runs", "Narrator")
				.AddLine("The student runs away, leaving behind an item.")
				.AddLine("Follow her and maybe ask her more questions. Kindly this time.")
				.AddLine("Objective: Run after the twin-pigtails girl and ask her for more information about yourself.")));

		RegisterDialogue(new DialogueData("library_yoruka_followup")
			.AddNode(new DialogueNode("start", "Mochizuki Yoruka")
				.AddLine("!!")
				.AddLine("Listen, I’m sorry. I was just, just confused and all.")
				.AddLine("I didn’t expect you to be here…")
				.AddLine("How did you even manage to get here?")
				.AddChoice(new DialogueChoice("I had this. Shows pendant.", "pendant_option", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 2))
				.AddChoice(new DialogueChoice("It’s a long story.", "story_option", affectionCharacter: GameStateManager.Yoruka, affectionDelta: -1)))
			.AddNode(new DialogueNode("pendant_option", "Mochizuki Yoruka")
				.AddLine("Okay, I don’t know what that is for—but can you help me with this?")
				.SetNext("after_options"))
			.AddNode(new DialogueNode("story_option", "Mochizuki Yoruka")
				.AddLine("Yeah right…")
				.SetNext("after_options"))
			.AddNode(new DialogueNode("after_options", "Mochizuki Yoruka")
				.AddLine("Anyway, let’s just fix this thing.")
				.AddLine("This terminal log helps us where to go next.")
				.AddLine("I am trying to find out what actually happened in this institute.")
				.AddLine("I figure out maybe something or someone is messing with it.")
				.AddLine("Let’s talk later.")
				.SetNext("objective"))
			.AddNode(new DialogueNode("objective", "Objective")
				.AddLine("Interact with the surviving terminal interface to extract the data log.")));

		// What she picks up where Yoruka was standing: the item the blueprint's scene 2 says she
		// leaves behind. One line, and the flag the room watches for comes from the node itself.
		RegisterDialogue(new DialogueData("library_item_taken")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("A student card, dropped in the dust. 'Mochizuki Yoruka - junior assistant.' So that's her name.")));

		RegisterDialogue(new DialogueData("library_cork_board")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("Pictures of the reading hall, students working on research, and structural documentation. I can’t seem to identify who these people are, though.")));
		RegisterDialogue(new DialogueData("library_computer")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("They probably encode some book borrowers here.")));
		RegisterDialogue(new DialogueData("library_bookshelves")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("Books sorted by subject. Can’t see why we need to look here.")));
		RegisterDialogue(new DialogueData("library_drawers")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("Scattered folders and stacks of dusty papers. Nothing of real importance here.")));
		RegisterDialogue(new DialogueData("library_collapsed_shelves")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("She moved fast... The main route is completely blocked by these fallen shelves. I'll need to bypass it.")
				.SetNext("objective"))
			.AddNode(new DialogueNode("objective", "Objective")
				.AddLine("Clear the collapsed path using the Echo to catch up to Yoruka.")));
		RegisterDialogue(new DialogueData("library_terminal")
			.AddNode(new DialogueNode("start", "Mochizuki Yoruka")
				.AddLine("Yay!! It’s fixed.")
				.AddLine("Let’s see here…")
				.AddLine("The next terminal log should be in the medical wing. But before we can go there, we need to go through the Atrium…")
				.AddLine("…What a headache…")
				.AddChoice(new DialogueChoice("It’s a long story.", "walk")))
			.AddNode(new DialogueNode("walk", "Aionia Avgerinos")
				.AddLine("We’ll talk on the way, is that good?")
				.SetNext("sure"))
			.AddNode(new DialogueNode("sure", "Mochizuki Yoruka")
				.AddLine("…Sure!")));

		// --- Scene 5: the walk to the Atrium (plays on leaving the Library) ---
		RegisterDialogue(new DialogueData("library_to_atrium")
			.AddNode(new DialogueNode("start", "Narrator")
				.AddLine("The heavy library doors slide open as You and Yoruka begin walking down the quiet, root-webbed corridor toward the Atrium.")
				.AddLine("You told her about what happened to you, how you woke up in a strange place—not remembering events that came before.")
				.AddLine("You showed her your pendant, to which she also doesn’t have a clue about.")
				.SetNext("legend"))
			.AddNode(new DialogueNode("legend", "Mochizuki Yoruka")
				.AddLine("I still can't believe you completely blanked on me. When you were heading Project Hyperion, you were practically a campus legend. Everyone wanted to be in your orbit... especially us junior assistants trying to learn actual chronology from the best.")
				.AddChoice(new DialogueChoice("Did I…treat you poorly?", "treated", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 1)))
			.AddNode(new DialogueNode("treated", "Mochizuki Yoruka")
				.AddLine("N-No! Well—maybe a little! You never gave me dramatic responses no matter how much I freaked out. But... you never ignored me either.")
				.AddLine("When everyone else said I was too loud or hyper-fixated, you actually listened to my theories.")
				.AddChoice(new DialogueChoice("I'm sorry I forgot you, Yoruka. I'll make sure to remember everything.", "promise", "promised_yoruka", true, null, "promised_yoruka", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 3))
				.AddChoice(new DialogueChoice("What was Project Hyperion?", "hyperion_q", "asked_hyperion", true, null, "asked_hyperion")))
			.AddNode(new DialogueNode("promise", "Mochizuki Yoruka")
				.AddLine("W—What?! Don't just drop heavy, emotional things on me out of nowhere with that deadpan face! It's completely unfair! ...It’s not like I was sitting around waiting for you to validate me or anything, but... if you actually remember, you better keep your promise, okay?! Don't you dare forget me again!")
				// "Choose another option from the bottom": the branch not yet taken is offered here
				.AddChoice(new DialogueChoice("What was Project Hyperion?", "hyperion_q", "asked_hyperion", true, null, "asked_hyperion")))
			.AddNode(new DialogueNode("hyperion_q", "Mochizuki Yoruka")
				.AddLine("Eek—are you joking?")
				.AddLine("You were the lead researcher of the entire project…You and Felix-san are the one all of us junior assistants looked up to as our idols while trying to learn real chronology!")
				.AddLine("I worked so hard just to understand your equations...Hearing you ask me about it like it's nothing makes me feel like you've completely erased me from your memory.")
				.AddLine("Please... don't act like I never mattered to you...")
				.AddChoice(new DialogueChoice("Who is Felix?", "felix_q")))
			.AddNode(new DialogueNode("felix_q", "Narrator")
				.AddLine("You noticed Yoruka flinched slightly, surprised that you forgot about this Felix-person.")
				.SetNext("felix_w"))
			.AddNode(new DialogueNode("felix_w", "Mochizuki Yoruka")
				.AddLine("W-well…")
				.AddLine("Felix Lasarewitch, the golden-boy heir of the institute, the senior researcher you spent every waking hour locked in the lab with, and the most terrifyingly influential person on this entire campus!")
				.AddLine("Everyone, every single student and faculty member, walks on eggshells around him because he practically owned the board of directors!")
				.AddChoice(new DialogueChoice("I... still don't remember him.", "felix_after", affectionCharacter: GameStateManager.Yoruka, affectionDelta: -1)))
			// End of the Felix branch: the apology is still on the table if she has not offered it yet.
			.AddNode(new DialogueNode("felix_after", "Mochizuki Yoruka")
				.AddLine("That’s a shame, we could encounter him around here, since he may be also figuring out how to fix things here.")
				.AddLine("Let’s just carry on to the Atrium then, shall we go, senior?")
				.AddChoice(new DialogueChoice("I'm sorry I forgot you, Yoruka. I'll make sure to remember everything.", "promise", "promised_yoruka", true, null, "promised_yoruka", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 3))));

		// ======================================================================
		// Level 3 - The Atrium. Dialogue staged verbatim from the doc; the
		// RoomAtrium scene is not built yet, so these fire once that level exists.
		// ======================================================================
		RegisterDialogue(new DialogueData("atrium_intro")
			.AddNode(new DialogueNode("start", "Narrator")
				.AddLine("The floor looks like it is changing appearances. Brand new marble floor then broken the next. Be careful where you step on.")
				.SetNext("yoruka1"))
			.AddNode(new DialogueNode("yoruka1", "Mochizuki Yoruka")
				.AddLine("Seems like these tiles are fluctuating through time.")
				.AddLine("Don't step on the blue tiles. They're from a version of this room where the floor never got built.")
				.SetNext("aionia1"))
			.AddNode(new DialogueNode("aionia1", "Aionia Avgerinos")
				.AddLine("That sounds like a very specific problem.")
				.SetNext("yoruka2"))
			.AddNode(new DialogueNode("yoruka2", "Mochizuki Yoruka")
				.AddLine("It is a specific problem! Ugh, why do I have to explain this to you?")
				.SetNext("instr"))
			.AddNode(new DialogueNode("instr", "Instructions")
				.AddLine("Press T to record, then R to project your Echo. It repeats your recording.")
				.SetNext("yoruka3"))
			.AddNode(new DialogueNode("yoruka3", "Mochizuki Yoruka")
				.AddLine("Just so we're clear, I'm not standing on the plate. The pendant copies you, remember?")
				.SetNext("aionia2"))
			.AddNode(new DialogueNode("aionia2", "Aionia Avgerinos")
				.AddLine("I wasn't going to ask.")
				.SetNext("yoruka4"))
			.AddNode(new DialogueNode("yoruka4", "Mochizuki Yoruka")
				.AddLine("...You were thinking about it. Your face did that thing.")
				.SetNext("objective"))
			.AddNode(new DialogueNode("objective", "Objective")
				.AddLine("Get across the entry hall.")));

		RegisterDialogue(new DialogueData("atrium_bridge")
			.AddNode(new DialogueNode("start", "Narrator")
				.AddLine("The bridge breathes in and out. There is a rhythm to it. Learn it.")
				.AddLine("Yoruka's panel collapses.")
				.SetNext("panic"))
			.AddNode(new DialogueNode("panic", "Mochizuki Yoruka")
				.AddLine("S-Senior!!")
				.SetChoiceTimeout(8f)   // the bridge is collapsing: not deciding is its own answer
				.AddChoice(new DialogueChoice("Grab her hand.", "a_hand", "bridge_rescued_yoruka", true, null, null, GameStateManager.Yoruka, 2))
				.AddChoice(new DialogueChoice("Tell her to jump onto your Echo's platform.", "b_jump", "bridge_rescued_yoruka", true, null, null, GameStateManager.Yoruka, 1))
				.AddChoice(new DialogueChoice("Keep moving. She'll catch up.", "c_go", "bridge_abandoned_yoruka", true, null, null, GameStateManager.Yoruka, -1)))
			.AddNode(new DialogueNode("a_hand", "Aionia Avgerinos")
				.AddLine("I've got you.")
				.SetNext("a_react"))
			.AddNode(new DialogueNode("a_react", "Mochizuki Yoruka")
				.AddLine("...You didn't hesitate. Not even a little.")
				.SetNext("objective"))
			.AddNode(new DialogueNode("b_jump", "Aionia Avgerinos")
				.AddLine("Jump. Left. My Echo's holding it.")
				.SetNext("b_react"))
			.AddNode(new DialogueNode("b_react", "Mochizuki Yoruka")
				.AddLine("Are you out of your mind?! ...Fine! Fine, fine!")
				.AddLine("That actually worked. Don't look so smug.")
				.SetNext("objective"))
			.AddNode(new DialogueNode("c_go", "Aionia Avgerinos")
				.AddLine("You can make it. Keep going.")
				.SetNext("c_react"))
			.AddNode(new DialogueNode("c_react", "Mochizuki Yoruka")
				.AddLine("Don't say that like it's easy!")
				.SetNext("c_fall"))
			.AddNode(new DialogueNode("c_fall", "Narrator")
				.AddLine("Yoruka falls below the abyss, but you're sure she'll catch up.")
				.SetNext("objective"))
			.AddNode(new DialogueNode("objective", "Objective")
				.AddLine("Cross the bridge before the panels phase out.")));

		// Scene 3's opening. Both answers are one conversation-ending beat each: the terminal only
		// opens once the puzzle is actually solved (Yoruka's flag, or the two consoles matched),
		// and the reveal is its own conversation so the blueprint's line has one copy, not two.
		RegisterDialogue(new DialogueData("atrium_terminal")
			.AddNode(new DialogueNode("start", "Narrator")
				.AddLine("In front of you is a terminal. You can solve this puzzle by using an Echo or let Yoruka solve it by herself.")
				.AddLine("*Either way, the terminal opens a path to the stairway.")
				.AddChoice(new DialogueChoice("Let Yoruka decode it.", "let_yoruka", "yoruka_solved_terminal", hiddenIfFlag: "yoruka_solved_terminal", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 2))
				.AddChoice(new DialogueChoice("Decode it by yourself.", "self", "atrium_terminal_self", hiddenIfFlag: "atrium_terminal_self", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 1)))
			.AddNode(new DialogueNode("let_yoruka", "Mochizuki Yoruka")
				.AddLine("Heh! The pattern isn't random. It's a Fibonacci offset with a time delay. Of course they hid a lock in my favorite kind of problem.")
				.SetNext("enjoy"))
			.AddNode(new DialogueNode("enjoy", "Aionia Avgerinos")
				.AddLine("You enjoy this.")
				.SetNext("do_i"))
			.AddNode(new DialogueNode("do_i", "Mochizuki Yoruka")
				.AddLine("I do!"))
			.AddNode(new DialogueNode("self", "Objective")
				.AddLine("Match the sequence on both consoles.")));

		// What the terminal says once it is open - played by RoomAtrium for either route, so the
		// blueprint's reveal lands whether Yoruka decoded it or the player matched the codes.
		RegisterDialogue(new DialogueData("atrium_terminal_reveal")
			.AddNode(new DialogueNode("unlock", "Narrator")
				.AddLine("The terminal unlocks. Text flickers across the screen for one second: SUBJECT A.A. ANCHOR STATUS: STABLE.")
				.SetNext("name_q"))
			.AddNode(new DialogueNode("name_q", "Mochizuki Yoruka")
				.AddLine("...Senior, why is your name on a system that's supposed to be broken?")
				.SetNext("warm"))
			.AddNode(new DialogueNode("warm", "Narrator")
				.AddLine("The pendant is warm in your palm. You don't remember putting your hand on it.")));

		RegisterDialogue(new DialogueData("atrium_stairway")
			.AddNode(new DialogueNode("start", "Mochizuki Yoruka")
				.AddLine("I'll just wait up here. Because I can climb. Like a normal person.")
				.SetNext("climb"))
			.AddNode(new DialogueNode("climb", "Narrator")
				.AddLine("You climb. She follows. Somewhere around the third landing, her voice drops.")
				.SetNext("family"))
			.AddNode(new DialogueNode("family", "Mochizuki Yoruka")
				.AddLine("...You know people said I only got into this place because of my family.")
				.AddChoice(new DialogueChoice("Do you believe that?", "believe", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 2))
				.AddChoice(new DialogueChoice("Say nothing and keep climbing.", "silent", affectionCharacter: GameStateManager.Yoruka, affectionDelta: -1)))
			.AddNode(new DialogueNode("believe", "Mochizuki Yoruka")
				.AddLine("Of course not! ...But sometimes, at night, I wonder.")
				.AddChoice(new DialogueChoice("Then why are you still here?", "why_still", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 2)))
			.AddNode(new DialogueNode("why_still", "Mochizuki Yoruka")
				.AddLine("Because I'm going to prove them wrong. Every single one of them.")
				.SetNext("objective"))
			.AddNode(new DialogueNode("silent", "Mochizuki Yoruka")
				.AddLine("...Typical. You never answered that kind of thing before either.")
				.SetNext("objective"))
			.AddNode(new DialogueNode("objective", "Objective")
				.AddLine("Use your Echo to climb the broken stairs.")));

		// The window's memory carries the CG the blueprint asks for: "CG: Aionia and Felix". The
		// plate is declared on the memory's own nodes, so it lifts for the beats back in the room.
		RegisterDialogue(new DialogueData("atrium_window")
			.AddNode(new DialogueNode("start", "Narrator")
				.AddLine("In the glass, a version of you is standing next to someone. The glass won't show his face.")
				.SetPlate(MemoryCgPath)
				.SetNext("felix1"))
			.AddNode(new DialogueNode("felix1", "Felix Lasarewitch")
				.AddLine("You've been staring at that window for an hour.")
				.AddLine("If something ever happens to me, don't go looking.")
				.SetPlate(MemoryCgPath)
				.SetNext("a_again"))
			.AddNode(new DialogueNode("a_again", "Aionia Avgerinos")
				.AddLine("Say that again.")
				.SetPlate(MemoryCgPath)
				.SetNext("f_joke"))
			.AddNode(new DialogueNode("f_joke", "Felix Lasarewitch")
				.AddLine("It was a joke.")
				.SetPlate(MemoryCgPath)
				.SetNext("a_wasnt"))
			.AddNode(new DialogueNode("a_wasnt", "Aionia Avgerinos")
				.AddLine("It wasn't.")
				.SetPlate(MemoryCgPath)
				.SetNext("felt_afraid"))
			.AddNode(new DialogueNode("felt_afraid", "Aionia Avgerinos")
				.AddLine("Why did he sound so... afraid?")
				.SetPlate(MemoryCgPath)
				.SetNext("f3"))
			.AddNode(new DialogueNode("f3", "Narrator")
				.AddLine("FRAGMENT F3 ACQUIRED: The Window.")
				.SetNext("objective"))
			.AddNode(new DialogueNode("objective", "Objective")
				.AddLine("Align the reflections in the window.")));

		// --- Memory fragments: the pieces of Aionia's past she recovers ---
		RegisterFragment(new MemoryFragment("pendant", "Fragment I - The Pendant",
			"Cold blue light, and a promise to keep you safe. Felix. The name arrives before the face does.", "touched_pendant"));
		RegisterFragment(new MemoryFragment("hyperion", "Fragment II - Project Hyperion",
			"A machine built to traverse time and space. Every timeline in existence was spiralling toward collapse.", "room01_complete"));
		RegisterFragment(new MemoryFragment("name", "Fragment III - A Name",
			"Aionia Avgerinos. It is your own name, and it fits back into place like a key.", "asked_identity"));
		RegisterFragment(new MemoryFragment("corridor", "Fragment IV - The Fractured Campus",
			"The overgrown, shattered corridors of the Aetherion Institute. Every parallel timeline has collided into one unstable present.", "library_complete"));
		RegisterFragment(new MemoryFragment("window", "Fragment F3 - The Window",
			"Standing at the glass, next to someone whose face the glass won't show. A sunset that was never one sunset.", "atrium_window_seen"));
	}
	#endregion
}
