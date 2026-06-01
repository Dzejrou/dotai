using Godot;

using System;

[GlobalClass]
public partial class RestingController : Node
{
    private const string SitAnimationPrefix = "sit-down";
    private const string SitIdleAnimationPrefix = "sit-idle";
    private const string EatAnimationPrefix = "eat";
    private const string DrinkAnimationPrefix = "drink";
    private const string StandUpAnimationPrefix = "stand-up";

    private enum RestingPhase
    {
        None,
        SittingDown,
        Active,
        SitIdle,
        StandingUp,
    }

    private sealed class TrackState
    {
        public bool Active;
        public float RemainingSeconds;
        public float TickIntervalSeconds;
        public float TimeUntilNextTick;
        public int AmountPerTick;
        public string DisplayName;
    }

    private Player _player;
    private OmniSprite _omniSprite;
    private RestingPhase _phase = RestingPhase.None;
    private ConsumableKind _lastStartedKind = ConsumableKind.None;
    private readonly TrackState _food = new();
    private readonly TrackState _drink = new();
    private string _currentAnimationName;
    private bool _animationFinishedConnected;

    public bool IsResting => _phase != RestingPhase.None;

    public void Initialize(Player player)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _omniSprite = _player.OmniSprite;
        EnsureAnimationFinishedConnected();
    }

    public override void _ExitTree()
    {
        DisconnectAnimationFinished();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_phase == RestingPhase.None)
            return;

        TickTrack(_food, (float)delta, ConsumableKind.Food);
        TickTrack(_drink, (float)delta, ConsumableKind.Drink);

        if (_phase == RestingPhase.Active && !_food.Active && !_drink.Active)
            _phase = RestingPhase.SitIdle;

        UpdateAnimation();
    }

    public bool TryStartFromDefinition(InventoryItemDefinition definition)
    {
        if (definition == null || definition.ConsumableKind == ConsumableKind.None)
            return false;

        if (_player == null || _player.IsDead)
            return false;

        var track = ResolveTrack(definition.ConsumableKind);
        if (track == null)
            return false;

        track.Active = true;
        track.RemainingSeconds = Math.Max(0.1f, definition.ConsumableDurationSeconds);
        track.TickIntervalSeconds = Math.Max(0.1f, definition.ConsumableTickIntervalSeconds);
        track.TimeUntilNextTick = track.TickIntervalSeconds;
        track.AmountPerTick = Math.Max(1, definition.ConsumableAmountPerTick);
        track.DisplayName = string.IsNullOrEmpty(definition.DisplayName) ? definition.Id : definition.DisplayName;

        _lastStartedKind = definition.ConsumableKind;

        if (_phase == RestingPhase.None)
            _phase = RestingPhase.SittingDown;
        else if (_phase == RestingPhase.SitIdle)
            _phase = RestingPhase.Active;

        UpdateAnimation();
        return true;
    }

    public void CancelAll()
    {
        if (_phase == RestingPhase.None)
            return;

        _food.Active = false;
        _drink.Active = false;
        _phase = RestingPhase.None;
        _lastStartedKind = ConsumableKind.None;
        _currentAnimationName = null;
    }

    public void CancelFromDamage()
    {
        if (_phase == RestingPhase.None || _phase == RestingPhase.StandingUp)
            return;

        _food.Active = false;
        _drink.Active = false;
        _lastStartedKind = ConsumableKind.None;

        _phase = RestingPhase.StandingUp;
        _currentAnimationName = null;
        if (!PlayPrefix(StandUpAnimationPrefix))
        {
            _phase = RestingPhase.None;
            _currentAnimationName = null;
        }
    }

    public bool ShouldSuppressDefaultAnimation()
    {
        return IsResting && !string.IsNullOrEmpty(_currentAnimationName);
    }

    private string ResolveActiveAnimationPrefix()
    {
        // Prefer the most recently started track when both are still ticking; otherwise
        // fall back to whichever track is still active. Returns the eat prefix as a
        // last-resort default so the switch arm always has a string to play.
        var prefer = _lastStartedKind;
        if (prefer == ConsumableKind.Drink && _drink.Active)
            return DrinkAnimationPrefix;
        if (prefer == ConsumableKind.Food && _food.Active)
            return EatAnimationPrefix;
        if (_drink.Active)
            return DrinkAnimationPrefix;
        return EatAnimationPrefix;
    }

    private TrackState ResolveTrack(ConsumableKind kind)
    {
        return kind switch
        {
            ConsumableKind.Food => _food,
            ConsumableKind.Drink => _drink,
            _ => null,
        };
    }

    private void TickTrack(TrackState track, float delta, ConsumableKind kind)
    {
        if (!track.Active)
            return;

        track.TimeUntilNextTick -= delta;
        while (track.TimeUntilNextTick <= 0.0f && track.RemainingSeconds > 0.0f && track.Active)
        {
            ApplyTick(track, kind);
            track.TimeUntilNextTick += track.TickIntervalSeconds;
            if (track.TickIntervalSeconds <= 0.0f)
                break;
        }

        track.RemainingSeconds -= delta;
        if (track.RemainingSeconds <= 0.0f)
        {
            track.Active = false;
            track.RemainingSeconds = 0.0f;
        }
    }

    private void ApplyTick(TrackState track, ConsumableKind kind)
    {
        if (_player == null || _player.IsDead)
        {
            track.Active = false;
            return;
        }

        switch (kind)
        {
            case ConsumableKind.Food:
                var hpRestored = _player.RestoreHealthFromConsumable(track.AmountPerTick);
                if (hpRestored > 0)
                    CombatLog.Healing($"Player restores {hpRestored} HP from {track.DisplayName}.");
                break;
            case ConsumableKind.Drink:
                var manaRestored = _player.RestoreManaFromConsumable(track.AmountPerTick);
                if (manaRestored > 0)
                    CombatLog.Healing($"Player restores {manaRestored} mana from {track.DisplayName}.");
                break;
        }
    }

    private void UpdateAnimation()
    {
        if (_omniSprite == null || !GodotObject.IsInstanceValid(_omniSprite) || _player == null)
            return;

        switch (_phase)
        {
            case RestingPhase.SittingDown:
                if (!PlayPrefix(SitAnimationPrefix))
                {
                    // No sit animation wired up yet — skip straight to the active loop.
                    _phase = (_food.Active || _drink.Active) ? RestingPhase.Active : RestingPhase.SitIdle;
                    UpdateAnimation();
                }
                break;
            case RestingPhase.Active:
                PlayPrefix(ResolveActiveAnimationPrefix());
                break;
            case RestingPhase.SitIdle:
                PlayPrefix(SitIdleAnimationPrefix);
                break;
            case RestingPhase.StandingUp:
                PlayPrefix(StandUpAnimationPrefix);
                break;
        }
    }

    private bool PlayPrefix(string prefix)
    {
        if (_player == null)
            return false;

        var animationName = _player.ResolveDirectionalAnimationName(prefix);
        if (string.IsNullOrEmpty(animationName))
        {
            _currentAnimationName = null;
            return false;
        }

        if (_currentAnimationName == animationName && _omniSprite.IsAnimationPlaying)
            return true;

        if (_omniSprite.TryPlay(animationName))
        {
            _currentAnimationName = animationName;
            return true;
        }

        _currentAnimationName = null;
        return false;
    }

    private void EnsureAnimationFinishedConnected()
    {
        if (_animationFinishedConnected || _omniSprite == null)
            return;

        _omniSprite.AnimationFinished += OnOmniSpriteAnimationFinished;
        _animationFinishedConnected = true;
    }

    private void DisconnectAnimationFinished()
    {
        if (!_animationFinishedConnected || _omniSprite == null || !GodotObject.IsInstanceValid(_omniSprite))
        {
            _animationFinishedConnected = false;
            return;
        }

        _omniSprite.AnimationFinished -= OnOmniSpriteAnimationFinished;
        _animationFinishedConnected = false;
    }

    private void OnOmniSpriteAnimationFinished()
    {
        switch (_phase)
        {
            case RestingPhase.SittingDown:
                _phase = (_food.Active || _drink.Active) ? RestingPhase.Active : RestingPhase.SitIdle;
                _currentAnimationName = null;
                UpdateAnimation();
                break;
            case RestingPhase.StandingUp:
                _phase = RestingPhase.None;
                _currentAnimationName = null;
                break;
        }
    }
}
