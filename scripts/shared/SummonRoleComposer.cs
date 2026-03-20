using System;

public sealed class SummonRoleComposer
{
    private readonly SummonState _summonState;
    private readonly Action<IActorBehavior[]> _configureBehaviors;
    private readonly Func<IActorBehavior[]> _defaultBehaviorFactory;
    private readonly Func<SummonBehaviorPreset> _summonPresetFactory;
    private readonly Action<bool> _onRoleModeChanged;

    public SummonRoleComposer(
        SummonState summonState,
        Action<IActorBehavior[]> configureBehaviors,
        Func<IActorBehavior[]> defaultBehaviorFactory,
        Func<SummonBehaviorPreset> summonPresetFactory,
        Action<bool> onRoleModeChanged = null)
    {
        _summonState = summonState ?? throw new ArgumentNullException(nameof(summonState));
        _configureBehaviors = configureBehaviors ?? throw new ArgumentNullException(nameof(configureBehaviors));
        _defaultBehaviorFactory = defaultBehaviorFactory ?? throw new ArgumentNullException(nameof(defaultBehaviorFactory));
        _summonPresetFactory = summonPresetFactory ?? throw new ArgumentNullException(nameof(summonPresetFactory));
        _onRoleModeChanged = onRoleModeChanged;
    }

    public FollowSummonerBehavior Refresh()
    {
        var isSummoned = _summonState.IsSummoned;
        _onRoleModeChanged?.Invoke(isSummoned);

        if (!isSummoned)
        {
            _configureBehaviors(_defaultBehaviorFactory() ?? Array.Empty<IActorBehavior>());
            return null;
        }

        var summonPreset = _summonPresetFactory();
        _configureBehaviors(summonPreset.Behaviors);
        return summonPreset.FollowSummonerBehavior;
    }
}
