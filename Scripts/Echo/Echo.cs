using System.Collections.Generic;
using Godot;

/// <summary>
/// A ghost that replays one previous loop. INHERITS Entity.
///
/// OOP roles:
///  - INHERITANCE:   Echo : Entity.
///  - POLYMORPHISM:  overrides Update(), TakeDamage(), Die(), Interact(); IsHostileTo().
///  - ENCAPSULATION: the recorded frames are private; the Echo cannot be edited from outside.
///
/// HOW THE PROJECTION WORKS (and why it is deterministic)
///  1. The Room owns ONE shared clock (Room.LoopClock) that advances in fixed 1/60 s physics
///     steps. The recording was made against that same clock, and the replay reads that same
///     clock, so "time" means exactly the same thing in both.
///  2. An Echo is PROJECTED IN PLACE: the player presses R and it is added to the room right
///     then, replaying its window FORWARD from that moment. Nothing else about the run
///     changes - the clock is not rewound, puzzles and enemies keep their state, and the
///     player is not moved. (The old design reset the whole run to place an Echo; that is
///     what this rework removed.)
///  3. To do that the Echo remembers WHEN it was projected (_replayStartClock) and expresses
///     the current clock in its own recording timeline (see Update). The window's first frame
///     therefore fires the instant the Echo appears, and the rest keep their original spacing.
///     Anchoring at 0 - the old "reset the loop" case - is just _replayStartClock == 0.
///  4. Each tick the Echo fires every frame whose Timestamp has been reached. A frame snaps
///     the Echo to the recorded position and, if the action is Interact or Attack, re-performs
///     it (only world-affecting actions are replayed - movement is NOT simulated again).
///  5. Between two frames, the Echo's position is linearly interpolated. Because the recorder
///     stored a frame at every change of direction, a straight line between two frames is
///     exactly the path the player walked.
///  No randomness, no real-time clock, no live input: same frames in = same Echo out.
///  Several Echoes can exist at once - each has its own frame list and its own projection
///  moment, and they all read the same clock, so they stay in sync ("stacked" echoes).
/// </summary>
public partial class Echo : Entity
{
    #region Private state
    private List<ActionFrame> _frames = new();
    private int _nextFrameIndex;
    private bool _isReplaying;

    /// <summary>Value of Room.LoopClock at the moment this Echo was projected.</summary>
    private float _replayStartClock;
    #endregion

    #region Public properties
    public bool IsReplaying => _isReplaying;
    public int FrameCount => _frames.Count;
    protected override Color BodyColor => new Color(0.4f, 0.95f, 1f, 0.55f);
    #endregion

    #region Setup
    /// <summary>Gives the Echo its recording. Called once, before it is added to the Room.</summary>
    public void Initialize(IReadOnlyList<ActionFrame> frames)
    {
        _frames = new List<ActionFrame>(frames);   // private copy
    }

    /// <summary>
    /// Begins the replay from the CURRENT moment. The Room calls this right after the Echo is
    /// added, which is what "projected in place" means: frame 0 fires now, the rest follow with
    /// their original spacing. (The old design rewound the clock to 0 first, so this anchoring
    /// used to be a no-op - a projection start of 0 - which is exactly the special case here.)
    /// </summary>
    public void StartReplay()
    {
        _nextFrameIndex = 0;
        _replayStartClock = CurrentRoom != null ? CurrentRoom.LoopClock : 0f;
        _isReplaying = _frames.Count > 0;
        if (_isReplaying) WorldPosition = _frames[0].Position;
    }
    #endregion

    #region Update (polymorphism)
    public override void Update(float delta)
    {
        base.Update(delta);
        if (!_isReplaying || CurrentRoom == null) return;

        // Express "now" in the recording's own timeline: the window's first frame fires the
        // moment this Echo was projected, and every later frame keeps its original spacing.
        float now = CurrentRoom.LoopClock - _replayStartClock + _frames[0].Timestamp;

        // 1) Fire every frame that is due (usually zero or one per tick).
        while (_nextFrameIndex < _frames.Count && _frames[_nextFrameIndex].Timestamp <= now)
        {
            ActionFrame frame = _frames[_nextFrameIndex];
            WorldPosition = frame.Position;        // snap: removes any drift
            Perform(frame.Action);
            _nextFrameIndex++;
        }

        // 2) Replay finished: stand still at the final position (can keep holding a plate).
        if (_nextFrameIndex >= _frames.Count)
        {
            _isReplaying = false;
            return;
        }

        // 3) Nothing has fired yet: hold at the anchor position. With a fresh projection this
        //    is momentary (frame 0 fires on the first tick), but the branch keeps the Echo safe
        //    if it is ever added with the clock deliberately behind its projection start.
        if (_nextFrameIndex == 0)
        {
            WorldPosition = _frames[0].Position;
            return;
        }

        // 4) Glide between the last fired frame and the next one.
        ActionFrame previous = _frames[_nextFrameIndex - 1];
        ActionFrame next = _frames[_nextFrameIndex];
        float span = next.Timestamp - previous.Timestamp;
        float t = span > 0f ? Mathf.Clamp((now - previous.Timestamp) / span, 0f, 1f) : 1f;
        WorldPosition = previous.Position.Lerp(next.Position, t);

        Vector2 heading = next.Position - previous.Position;
        if (heading != Vector2.Zero) FacingDirection = heading.Normalized();
    }

    /// <summary>Only world-affecting actions are re-performed.</summary>
    private void Perform(PlayerAction action)
    {
        switch (action)
        {
            case PlayerAction.Interact: Interact(); break;
            case PlayerAction.Attack: Attack(); break;
            default: break;   // movement/idle: position comes from the frames, not from input
        }
    }
    #endregion

    #region Polymorphic overrides
    /// <summary>Echoes are memories: they cannot be hurt.</summary>
    public override void TakeDamage(int amount) { }

    public override void Die() { }

    /// <summary>Same world effect as the player's Interact, but without any player-only side effects.</summary>
    public override void Interact()
    {
        TryInteractWithNearby();
    }

    protected override bool IsHostileTo(Entity other) => other is Enemy;
    #endregion
}
