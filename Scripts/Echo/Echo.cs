using System.Collections.Generic;
using Godot;

public partial class Echo : Entity
{
    #region Private state
    private List<ActionFrame> _frames = new();
    private int _nextFrameIndex;
    private bool _isReplaying;

    private float _replayStartClock;
    #endregion

    #region Public properties
    public bool IsReplaying => _isReplaying;
    public int FrameCount => _frames.Count;
    protected override Color BodyColor => new Color(0.4f, 0.95f, 1f, 0.55f);
    #endregion

    #region Setup
    public void Initialize(IReadOnlyList<ActionFrame> frames)
    {
        _frames = new List<ActionFrame>(frames);   // private copy
    }

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
    public override void TakeDamage(int amount) { }

    public override void Die() { }

    public override void Interact()
    {
        TryInteractWithNearby();
    }

    protected override bool IsHostileTo(Entity other) => other is Enemy;
    #endregion
}
