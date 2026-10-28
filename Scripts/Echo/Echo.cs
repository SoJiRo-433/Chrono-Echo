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
/// HOW THE REPLAY WORKS (and why it is deterministic)
///  1. The Room owns ONE shared loop clock (Room.LoopClock) that advances in fixed 1/60 s
///     physics steps. The recording was made against that same clock, and the replay reads
///     that same clock, so "time" means exactly the same thing in both.
///  2. Each tick the Echo fires every frame whose Timestamp has been reached. A frame
///     snaps the Echo to the recorded position and, if the action is Interact or Attack,
///     re-performs it (only world-affecting actions are replayed - movement is NOT
///     simulated again).
///  3. Between two frames, the Echo's position is linearly interpolated. Because the
///     recorder stored a frame at every change of direction, a straight line between two
///     frames is exactly the path the player walked.
///  4. A capture window can start AFTER the loop began, because the player chooses the
///     moment. Until the window's first timestamp arrives the Echo waits at its anchor
///     position - the spot it was captured - since it has no earlier frame to glide from.
///  No randomness, no real-time clock, no live input: same frames in = same Echo out.
///  Several Echoes can exist at once - each has its own frame list and they all read the
///  same clock, so they stay in sync ("stacked" echoes).
/// </summary>
public partial class Echo : Entity
{
    #region Private state
    private List<ActionFrame> _frames = new();
    private int _nextFrameIndex;
    private bool _isReplaying;
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

    /// <summary>Rewinds to t=0. The Room calls this at the start of every loop.</summary>
    public void StartReplay()
    {
        _nextFrameIndex = 0;
        _isReplaying = _frames.Count > 0;
        if (_isReplaying) WorldPosition = _frames[0].Position;
    }
    #endregion

    #region Update (polymorphism)
    public override void Update(float delta)
    {
        base.Update(delta);
        if (!_isReplaying || CurrentRoom == null) return;

        float now = CurrentRoom.LoopClock;
        if (now < 0f) return;   // the loop was just reset; wait for tick 0

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

        // 3) Nothing has fired yet, because the window opens later than this moment: hold at
        //    the anchor position. There is no "previous" frame to glide from.
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
