using System.Collections.Generic;
using Godot;

/// <summary>Level 3: the Atrium. Five beats, all built out of the same primitives the other levels
/// use, so nothing here is a special case:
///
///   1. entry hall      - FluctuatingTile blue tiles (deadly) and a Door with CloseDelaySeconds = 5
///   2. glass skybridge - FluctuatingTile platforms (GapWhenAbsent), an east-connector checkpoint,
///                        and the timed "atrium_bridge" conversation
///   3. terminal        - two consoles that have to be live at the same moment AND show the same
///                        four-symbol sequence
///   4. stairway        - TimeStainStep, which only an Echo can make solid, plus one missing step
///                        that has to be jumped
///   5. observation deck- three marked spots, the third held while the player stands on the second
///
/// Everything a tile does on its own it does on its own; this class owns only the rules that span
/// several nodes, plus the falls and the exit door.</summary>
public partial class RoomAtrium : Room
{
    #region Exports
    [Export] public NodePath ConsoleAPath { get; set; } = new NodePath();
    [Export] public NodePath ConsoleBPath { get; set; } = new NodePath();
    [Export] public NodePath StairwayGatePath { get; set; } = new NodePath();
    [Export] public NodePath WindowSpotAPath { get; set; } = new NodePath();
    [Export] public NodePath WindowSpotBPath { get; set; } = new NodePath();
    [Export] public NodePath WindowSpotCPath { get; set; } = new NodePath();
    [Export] public NodePath ExitDoorPath { get; set; } = new NodePath();
    [Export] public NodePath BridgeTriggerPath { get; set; } = new NodePath();

    // Scene 3's conversation starts when she walks up to the terminal, the same way the bridge's
    // does: a floor zone rather than an interactable, so E next to a console cycles that console
    // instead of being swallowed by the terminal behind it.
    [Export] public NodePath TerminalTriggerPath { get; set; } = new NodePath();

    // The missing step of the stairway: floor that is not there at all. Only a jump crosses it.
    [Export] public NodePath JumpGapPath { get; set; } = new NodePath();

    // The foot of the stairway. Stepping in here is what starts Scene 4's conversation, so the
    // family-rumour choice plays on the way up rather than after the climb is already solved.
    [Export] public Rect2 StairwayTalkZone { get; set; } = new Rect2(89f, 350f, 84f, 73f);
    [Export] public NodePath CheckpointMarkerPath { get; set; } = new NodePath();

    // A fall costs a scratch and a walk back rather than a life, so there is one of these for each
    // place the floor can give way.
    // Deliberately just past the entry gate, so a fall on the walkway's gap or the bridge puts the
    // player back before the gap rather than teleporting them over it.
    [Export] public Vector2 BridgeFallRespawnPoint { get; set; } = new Vector2(680f, 630f);
    [Export] public Vector2 StairwayFallRespawnPoint { get; set; } = new Vector2(130f, 450f);

    [Export] public string TerminalDialogueId { get; set; } = "atrium_terminal";
    [Export] public string TerminalRevealDialogueId { get; set; } = "atrium_terminal_reveal";

    // The one part of the Atrium that wants a *repeating* projection: the stairway, whose stains
    // last 3 s, so the climb has to be walked again and again for the player to follow it up.
    [Export] public Rect2 StairwayLoopZone { get; set; } = new Rect2(70f, 210f, 145f, 270f);
    #endregion

    #region Private state
    // Half the width of her body: how much of her has to be over a hole before the floor takes
    // her. CheckFalls explains why the bridge measures her feet rather than her whole body.
    private const float FallFootprintRadius = 7f;

    private Door _stairwayGate;
    private Door _exitDoor;
    private Switch _consoleA;
    private Switch _consoleB;
    private Switch _windowSpotA;
    private Switch _windowSpotB;
    private Switch _windowSpotC;
    private FloorMarker _bridgeTrigger;
    private FloorMarker _terminalTrigger;
    private FloorMarker _jumpGap;
    private FloorMarker _checkpointMarker;

    private readonly List<TimeStainStep> _stairSteps = new();
    private readonly List<FluctuatingTile> _bridgeTiles = new();

    private bool _windowPlayed;
    private bool _bridgePlayed;
    private bool _terminalIntroPlayed;
    private bool _terminalRevealPlayed;
    private bool _stairwayTalkPlayed;
    private bool _checkpointReached;
    private float _jumpGapGraceRemaining;
    #endregion

    /// <summary>Spot 1 is the one the reflections need first, and it is only ever stood on once, so
    /// the plate records a flag instead of having to be held like the other two.</summary>
    public const string WindowSpotOneFlag = "atrium_spot_one";

    #region Lifecycle
    public override void _Ready()
    {
        if (string.IsNullOrEmpty(NextLevelPath)) NextLevelPath = "res://Scenes/Rooms/Room04Medical.tscn";
        base._Ready();

        // The Atrium's leap has a little more airtime than the base movement tutorial value so
        // the recorded jump has room for a frame of input latency at the broken stair.
        if (Player != null) Player.JumpAirtimeSeconds = 1.5f;

        _consoleA = GetNodeOrNull<Switch>(ConsoleAPath);
        _consoleB = GetNodeOrNull<Switch>(ConsoleBPath);
        _stairwayGate = GetNodeOrNull<Door>(StairwayGatePath);
        _windowSpotA = GetNodeOrNull<Switch>(WindowSpotAPath);
        _windowSpotB = GetNodeOrNull<Switch>(WindowSpotBPath);
        _windowSpotC = GetNodeOrNull<Switch>(WindowSpotCPath);
        _exitDoor = GetNodeOrNull<Door>(ExitDoorPath);
        _bridgeTrigger = GetNodeOrNull<FloorMarker>(BridgeTriggerPath);
        _terminalTrigger = GetNodeOrNull<FloorMarker>(TerminalTriggerPath);
        _jumpGap = GetNodeOrNull<FloorMarker>(JumpGapPath);
        _checkpointMarker = GetNodeOrNull<FloorMarker>(CheckpointMarkerPath);

        foreach (Node child in GetChildren())
        {
            if (child is TimeStainStep step) _stairSteps.Add(step);
            else if (child is FluctuatingTile tile && tile.GapWhenAbsent) _bridgeTiles.Add(tile);
        }

        DialogueManager dialogue = Dialogue;
        if (dialogue != null) dialogue.DialogueEnded += OnAtriumDialogueEnded;
    }

    public override void _ExitTree()
    {
        DialogueManager dialogue = Dialogue;
        if (dialogue != null) dialogue.DialogueEnded -= OnAtriumDialogueEnded;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        if (GameStateManager.Instance != null && GameStateManager.Instance.IsPaused) return;

        CheckTerminalIntro();
        CheckGlitchedTerminal();
        CheckTerminalReveal();
        CheckBridgeCollapse();
        CheckAtriumCheckpoint();
        CheckStairwayTalk();
        CheckFalls((float)delta);
        CheckObservationWindow();
    }

    /// <summary>Narrows the room's exported `LoopReplay` down to the stairway. The doc's Level 3
    /// instruction is that an Echo repeats your recording, and on the stairs it must - the stains
    /// only last 3 s. Anywhere else a repeating window is actively wrong, because the rewind walks
    /// the projection off the plate it is supposed to be holding: on a plate the Atrium wants what
    /// every earlier level taught, an Echo that plays its window once and then stands where the
    /// recording ended.</summary>
    protected override void OnEchoProjected(Echo echo)
    {
        echo.LoopReplay = StairwayLoopZone.HasPoint(echo.WorldPosition);
    }

    /// <summary>R-restart has to clear what this room remembered for itself: the one-shot beats
    /// would otherwise never fire again, and the stairway would keep the last climb's stains.
    /// The consoles go back to their starting codes with the switches, which the base class does.</summary>
    protected override void ResetPuzzles()
    {
        base.ResetPuzzles();

        _bridgePlayed = false;
        _windowPlayed = false;
        _terminalIntroPlayed = false;
        _terminalRevealPlayed = false;
        _stairwayTalkPlayed = false;
        _checkpointReached = false;
        _jumpGapGraceRemaining = 0f;

        foreach (TimeStainStep step in _stairSteps) step.ResetState();
        GameStateManager.Instance?.SetFlag(WindowSpotOneFlag, false);   // spot 1 must be marked again
        GameStateManager.Instance?.SetFlag("atrium_checkpoint", false);
    }
    #endregion

    #region Scene 2 - the bridge comes apart
    /// <summary>The first time she sets foot on the skybridge it starts collapsing, and the timed
    /// choice plays: the bridge does not wait for her to decide.</summary>
    private void CheckBridgeCollapse()
    {
        if (_bridgePlayed || _bridgeTrigger == null) return;

        Player player = Player;
        if (player == null || !player.IsAlive) return;
        if (!_bridgeTrigger.Bounds.Intersects(player.Bounds)) return;

        _bridgePlayed = true;
        Dialogue?.StartDialogue("atrium_bridge");
    }
    #endregion

    #region Scene 3 - the glitched terminal
    /// <summary>Walking up to the consoles introduces the puzzle: she can let Yoruka decode it or
    /// set the codes herself. It is a zone rather than an interactable so that E, right here, is
    /// free for the consoles that are the puzzle.</summary>
    private void CheckTerminalIntro()
    {
        if (_terminalIntroPlayed || _terminalTrigger == null) return;

        Player player = Player;
        if (player == null || !player.IsAlive) return;
        if (!_terminalTrigger.Bounds.Intersects(player.Bounds)) return;

        _terminalIntroPlayed = true;
        Dialogue?.StartDialogue(TerminalDialogueId);
    }

    /// <summary>Two consoles only count when they are weighed down at the same moment AND show the
    /// same four-symbol sequence, so the player sets the codes with E and the Echo is what holds
    /// one of them down. Yoruka decoding it sets a flag instead and opens the way anyway.</summary>
    private void CheckGlitchedTerminal()
    {
        if (_stairwayGate == null || _stairwayGate.IsOpen) return;

        bool solvedByYoruka = GameStateManager.Instance?.GetFlag("yoruka_solved_terminal") ?? false;
        if (ConsolesMatched() || solvedByYoruka) _stairwayGate.Open();
    }

    /// <summary>True only when both consoles are live and both display the same sequence right now.</summary>
    private bool ConsolesMatched()
    {
        if (_consoleA == null || _consoleB == null) return false;
        if (!_consoleA.IsActivated || !_consoleB.IsActivated) return false;

        string sequence = _consoleA.CurrentSequence;
        return sequence != "" && sequence == _consoleB.CurrentSequence;
    }

    /// <summary>Whichever route opened the stairway gate, the same reveal plays afterwards. Polled
    /// rather than fired from the two places that open it, so "SUBJECT A.A. ANCHOR STATUS: STABLE."
    /// has exactly one copy in the content, and so it waits its turn behind the conversation the
    /// player is already reading.</summary>
    private void CheckTerminalReveal()
    {
        if (_terminalRevealPlayed) return;
        if (_stairwayGate == null || !_stairwayGate.IsOpen) return;
        if (Dialogue != null && Dialogue.IsActive) return;

        _terminalRevealPlayed = true;
        Dialogue?.StartDialogue(TerminalRevealDialogueId);
    }
    #endregion

    #region Scene 4 - the stairway conversation
    /// <summary>The blueprint's "dialogue choice about Yoruka's family rumours on the third landing".
    /// It fires the first time Aionia sets foot in the stair tube, so the beat and its choice land
    /// before the climb rather than never.</summary>
    private void CheckStairwayTalk()
    {
        if (_stairwayTalkPlayed) return;

        Player player = Player;
        if (player == null || !player.IsAlive) return;
        if (!StairwayTalkZone.Intersects(player.Bounds)) return;

        _stairwayTalkPlayed = true;
        Dialogue?.StartDialogue("atrium_stairway");
    }
    #endregion

    #region Scenes 2 and 4 - the falls
    /// <summary>The skybridge platforms and the stairway steps are both "a floor that is only
    /// sometimes there". Standing on one while it is gone is a fall: a scratch and a walk back to
    /// the start of that stretch, not a life - the puzzle is the timing.
    ///
    /// What counts as "standing on" differs between the two on purpose. A stair step drops her as
    /// soon as her body touches an unstained one, which is what makes the climb need the Echo. A
    /// bridge panel only drops her when the ground she is actually standing on has gone - her own
    /// footprint, not the shoulders that overhang the next panel. Measuring the whole body there
    /// made two safe places count as falls: landing in the middle of a solid panel with an absent
    /// neighbour, and walking the lower walkway under the bridge, whose north strip her body
    /// reaches up into the panels above it.
    ///
    /// The bridge also honours the leap, the same rule the stairway uses: mid-leap she is not
    /// standing on anything, so a jump carries her over a panel that has gone and she only has to
    /// find a solid one on the way down.</summary>
    private void CheckFalls(float delta)
    {
        Player player = Player;
        if (player == null || !player.IsAlive) return;

        Rect2 box = player.Bounds;
        Rect2 footprint = new Rect2(
            player.WorldPosition - new Vector2(FallFootprintRadius, FallFootprintRadius),
            new Vector2(FallFootprintRadius * 2f, FallFootprintRadius * 2f));

        if (!player.IsAirborne)
        {
            foreach (TimeStainStep step in _stairSteps)
                if (!step.IsStained && step.Bounds.Intersects(box)) { Fall(player, StairwayFallRespawnPoint); return; }

            if (_jumpGap != null && _jumpGap.Bounds.Intersects(box))
            {
                // Room physics runs before Player physics. Give Space one brief window to be
                // processed by Player.Jump before declaring contact with the gap a fall.
                if (_jumpGapGraceRemaining <= 0f) _jumpGapGraceRemaining = 0.15f;
                else
                {
                    _jumpGapGraceRemaining = Mathf.Max(0f, _jumpGapGraceRemaining - delta);
                    if (_jumpGapGraceRemaining <= 0f)
                    {
                        Fall(player, StairwayFallRespawnPoint);
                        return;
                    }
                }
            }
            else _jumpGapGraceRemaining = 0f;

            foreach (FluctuatingTile tile in _bridgeTiles)
                if (!tile.IsPresent && tile.Bounds.Intersects(footprint)) { Fall(player, BridgeFallRespawnPoint); return; }
        }
        else _jumpGapGraceRemaining = 0f;
    }

    private void Fall(Player player, Vector2 respawn)
    {
        player.TakeScratch();
        player.WorldPosition = _checkpointReached
            ? _checkpointMarker.Position + _checkpointMarker.Size * 0.5f
            : respawn;
    }

    private void CheckAtriumCheckpoint()
    {
        if (_checkpointReached || Player == null || _checkpointMarker == null) return;
        if (!_checkpointMarker.Bounds.Intersects(Player.Bounds)) return;
        _checkpointReached = true;
        GameStateManager.Instance?.SetFlag("atrium_checkpoint", true);
    }
    #endregion

    #region Scene 5 - the observation deck
    /// <summary>The three spots have to be stood on so the reflections line up: the first one marks
    /// itself, the third has to be held while the player stands on the second. When they line up
    /// the window shows its fragment, and the heavy door on the deck's east wall unlocks - which is
    /// where Level 4 begins.</summary>
    private void CheckObservationWindow()
    {
        if (_windowPlayed) return;
        if (_windowSpotA == null || _windowSpotB == null || _windowSpotC == null) return;
        if (!(GameStateManager.Instance?.GetFlag(WindowSpotOneFlag) ?? false)) return;   // spot 1 first
        if (!_windowSpotB.IsActivated || !_windowSpotC.IsActivated) return;

        _windowPlayed = true;
        GameStateManager.Instance?.SetFlag("atrium_window_seen", true);   // unlocks Fragment F3
        Dialogue?.StartDialogue("atrium_window");
    }

    private void OnAtriumDialogueEnded(string dialogueId)
    {
        // "OPENS AFTER F3": the way onward only unlocks once the window has been read.
        if (dialogueId == "atrium_window") _exitDoor?.Open();
    }
    #endregion
}
