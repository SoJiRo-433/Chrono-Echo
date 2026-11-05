using System.Collections.Generic;
using Godot;

public partial class Echo : Entity
{
	#region Private state
	private List<ActionFrame> _frames = new();
	private int _nextFrameIndex;
	private bool _isReplaying;
	private bool _loopReplay;
	private float _lifetimeSeconds = 20f;
	private float _lifeRemaining;
	private bool _expired;

	private float _replayStartClock;
	#endregion

	#region Public properties
	public bool IsReplaying => _isReplaying;
	public int FrameCount => _frames.Count;

	/// <summary>True: the window repeats for as long as the Echo exists (Level 3's "it repeats
	/// your recording on a loop"). False: it plays once and then stands still, which is what lets
	/// an Echo hold a pressure plate in Room01/Room02. The room sets this when it spawns the Echo.</summary>
	[Export] public bool LoopReplay { get => _loopReplay; set => _loopReplay = value; }

	/// <summary>How long the projection lasts before it decays and the room reclaims it. 0 = it
	/// lasts until the run ends. The room sets this when it spawns the Echo, the same way it sets
	/// LoopReplay, so a level can tune how long its puzzle has to be solved in.</summary>
	[Export] public float LifetimeSeconds { get => _lifetimeSeconds; set => _lifetimeSeconds = Mathf.Max(0f, value); }

	public bool IsExpired => _expired;

	/// <summary>Seconds before this projection decays; infinity when it has no lifetime.</summary>
	public float SecondsLeft => _lifetimeSeconds > 0f ? Mathf.Max(0f, _lifeRemaining) : float.PositiveInfinity;

	public float LifetimeRatio =>
		_lifetimeSeconds > 0f ? Mathf.Clamp(_lifeRemaining / _lifetimeSeconds, 0f, 1f) : 1f;
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
		_lifeRemaining = _lifetimeSeconds;   // the projection starts its own clock here
		if (_isReplaying) WorldPosition = _frames[0].Position;
	}
	#endregion

	#region Update (polymorphism)
	public override void Update(float delta)
	{
		base.Update(delta);

		// The projection is not permanent: the micro-loop decays and the room takes the Echo back.
		if (!TickLifetime(delta)) return;

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

		// 2) Replay finished. A one-shot Echo stands still at its final position, which is what
		//    lets it keep holding a plate; a looping Echo rewinds to its first frame and repeats.
		if (_nextFrameIndex >= _frames.Count)
		{
			if (!_loopReplay)
			{
				_isReplaying = false;
				return;
			}

			// Rewind: the clock the replay started at is "now", so frames[0] is due again.
			_replayStartClock = CurrentRoom.LoopClock;
			_nextFrameIndex = 0;
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
			case PlayerAction.Jump: Jump(); break;   // the leap is replayed, not the arc
			default: break;   // movement/idle: position comes from the frames, not from input
		}
	}
	#endregion

	#region Lifetime
	/// <summary>Counts the projection down. Returns false once it is gone, so Update stops there.</summary>
	private bool TickLifetime(float delta)
	{
		if (_expired) return false;
		if (_lifetimeSeconds <= 0f) return true;

		_lifeRemaining = Mathf.Max(0f, _lifeRemaining - delta);
		if (_lifeRemaining > 0f) return true;

		Expire();
		return false;
	}

	private void Expire()
	{
		if (_expired) return;
		_expired = true;
		_isReplaying = false;
		Hide();

		// Deferred for the same reason Player.Die defers its respawn: a Switch or one of the
		// Atrium's tiles may be half-way through walking the room's entity list this very tick,
		// and taking an Echo out of that list mid-loop would invalidate the iteration.
		Room room = CurrentRoom;
		if (room != null) Callable.From(() => room.RemoveEcho(this)).CallDeferred();
	}
	#endregion

	#region Drawing
	public override void _Draw()
	{
		base._Draw();

		// The Echo carries its own countdown: a ring that drains clockwise and turns warm
		// as the projection runs out.
		if (_expired || _lifetimeSeconds <= 0f) return;

		float ratio = LifetimeRatio;
		Color colour = ratio < 0.25f ? new Color(1f, 0.6f, 0.4f) : new Color(0.55f, 0.95f, 1f);
		float radius = BodyRadius + 7f;

		DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 32, new Color(colour, 0.22f), 3f);
		DrawArc(Vector2.Zero, radius, -Mathf.Pi / 2f, -Mathf.Pi / 2f + Mathf.Tau * ratio, 32, colour, 3f);
	}
	#endregion

	#region Polymorphic overrides
	public override void TakeDamage(int amount) { }

	public override void Die() { }

	public override void Interact()
	{
		TryInteractWithNearby();
	}
	#endregion
}
