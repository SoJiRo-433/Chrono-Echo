using Godot;

/// <summary>Level 2, scenes 2-5: the Archives through to the heavy library doors.
///
/// The base Room already owns the HUD, the dialogue plumbing and the level hand-over, so this
/// subclass adds only what this level is: Yoruka (who storms off after her first conversation and
/// is waiting past the fallen shelves later), the walk-and-talk along the root-webbed corridor,
/// and the heavy doors that open only once that conversation is over.
///
/// The Archives -> collapsed shelves -> terminal path needs no code at all: the fallen shelves are
/// a Door held open by a Switch, exactly like Room01's security door.</summary>
public partial class RoomLibrary : Room
{
    [Export] public NodePath YorukaPath { get; set; } = new NodePath();
    [Export] public NodePath YorukaWaitingPath { get; set; } = new NodePath();
    [Export] public NodePath ShelvesBlockPath { get; set; } = new NodePath();
    [Export] public NodePath LibraryDoorsPath { get; set; } = new NodePath();
    [Export] public NodePath CorridorTriggerPath { get; set; } = new NodePath();

    // Scene 2 ends with her storming off - "leaving behind an item" (blueprint). The item is a
    // scene node so it can be picked up like any other thing she examines, but it stays hidden
    // until she actually runs, and it goes away again on a restart with the rest of the room.
    [Export] public NodePath YorukaItemPath { get; set; } = new NodePath();

    // Scene 2's scripted beat: walk in, and the camera pans out to Yoruka before she is spoken to.
    [Export] public NodePath PanTriggerPath { get; set; } = new NodePath();

    private Inspectable _yoruka;
    private Inspectable _yorukaWaiting;
    private Inspectable _yorukaItem;
    private Door _shelvesBlock;
    private Door _libraryDoors;
    private FloorMarker _corridorTrigger;
    private FloorMarker _panTrigger;
    private bool _yorukaSentOff;
    private bool _walkAndTalkPlayed;
    private bool _yorukaPanPlayed;

    #region Lifecycle
    public override void _Ready()
    {
        base._Ready();

        _yoruka = GetNodeOrNull<Inspectable>(YorukaPath);
        _yorukaWaiting = GetNodeOrNull<Inspectable>(YorukaWaitingPath);
        _yorukaItem = GetNodeOrNull<Inspectable>(YorukaItemPath);
        _shelvesBlock = GetNodeOrNull<Door>(ShelvesBlockPath);
        _libraryDoors = GetNodeOrNull<Door>(LibraryDoorsPath);
        _corridorTrigger = GetNodeOrNull<FloorMarker>(CorridorTriggerPath);
        _panTrigger = GetNodeOrNull<FloorMarker>(PanTriggerPath);

        if (_yorukaWaiting != null) _yorukaWaiting.Hide();   // she is on the far side of the shelves
        if (_yorukaItem != null) _yorukaItem.Hide();         // nothing is on the floor until she drops it

        DialogueManager dialogue = Dialogue;
        if (dialogue != null) dialogue.DialogueEnded += OnLibraryDialogueEnded;
    }

    public override void _ExitTree()
    {
        DialogueManager dialogue = Dialogue;
        if (dialogue != null) dialogue.DialogueEnded -= OnLibraryDialogueEnded;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        if (GameStateManager.Instance != null && GameStateManager.Instance.IsPaused) return;

        CheckYorukaPan();
        RevealYorukaWhenPassed();
        CheckCorridorEntry();
    }

    /// <summary>Scene 2 opens with the camera leaving Aionia and finding Yoruka among the shelves,
    /// and only then does she think about approaching her - which is also where the objective
    /// ("Approach the student") comes from.</summary>
    private void CheckYorukaPan()
    {
        if (_yorukaPanPlayed || _panTrigger == null) return;

        Player player = Player;
        if (player == null || !player.IsAlive) return;
        if (!_panTrigger.Bounds.Intersects(player.Bounds)) return;

        _yorukaPanPlayed = true;
        PanCameraTo(_yoruka != null ? _yoruka.Position : new Vector2(350f, 100f), 1.8f);
        Dialogue?.StartDialogue("library_intro");
    }

    /// <summary>An R-restart has to put this level back the way it started: Yoruka in the
    /// Archives, nothing on the floor, the conversation beats unplayed and the far-side Yoruka
    /// hidden again. The flags that go with them are cleared by the reset itself.</summary>
    protected override void ResetPuzzles()
    {
        base.ResetPuzzles();

        _yorukaSentOff = false;
        _walkAndTalkPlayed = false;
        _yorukaPanPlayed = false;
        _yoruka?.Show();
        _yorukaItem?.Hide();
        _yorukaWaiting?.Hide();
    }
    #endregion

    #region Story beats
    private void OnLibraryDialogueEnded(string dialogueId)
    {
        if (dialogueId == "library_yoruka") SendYorukaOff();
        else if (dialogueId == "library_to_atrium") _libraryDoors?.Open();   // scene 5: the doors, at last
    }

    /// <summary>Scene 2 ends with her storming off. The node goes away, and because a hidden
    /// interactable is skipped it stops being examinable at the same moment - and the item she
    /// drops appears in the same spot in the same breath.</summary>
    private void SendYorukaOff()
    {
        if (_yorukaSentOff) return;
        _yorukaSentOff = true;
        _yoruka?.Hide();
        DropYorukaItem();
    }

    /// <summary>Puts the dropped item where she was standing. It is anchored to her node rather
    /// than to a hard-coded spot, so moving her moves the thing she leaves behind.</summary>
    private void DropYorukaItem()
    {
        if (_yorukaItem == null) return;
        if (_yoruka != null) _yorukaItem.Position = _yoruka.Position;
        _yorukaItem.Show();
    }

    /// <summary>Scene 4: once the fallen shelves are open and the player is through them, Yoruka is
    /// waiting further along the corridor.</summary>
    private void RevealYorukaWhenPassed()
    {
        if (_yorukaWaiting == null || _yorukaWaiting.Visible) return;
        if (_shelvesBlock == null || !_shelvesBlock.IsOpen) return;
        if (Player == null || Player.WorldPosition.X < _shelvesBlock.Position.X + 40f) return;

        _yorukaWaiting.Show();
    }

    /// <summary>Scene 5: dropping into the root-webbed corridor starts the walk-and-talk with its
    /// three conversation beats. The heavy library doors open when it is over.</summary>
    private void CheckCorridorEntry()
    {
        if (_walkAndTalkPlayed || _corridorTrigger == null) return;

        Player player = Player;
        if (player == null || !player.IsAlive) return;
        if (!_corridorTrigger.Bounds.Intersects(player.Bounds)) return;

        _walkAndTalkPlayed = true;
        Dialogue?.StartDialogue("library_to_atrium");
    }
    #endregion
}
