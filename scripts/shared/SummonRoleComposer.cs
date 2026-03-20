using System;

public sealed class SummonRoleComposer
{
    private readonly SummonRoleState _summonRole;
    private readonly Action<IActorBehavior[]> _configureBehaviors;
    private readonly Func<IActorBehavior[]> _defaultBehaviorFactory;
    private readonly Func<SummonBehaviorPreset> _summonPresetFactory;
    private readonly Action<bool> _onRoleModeChanged;

    public SummonRoleComposer(
        SummonRoleState summonRole,
        Action<IActorBehavior[]> configureBehaviors,
        Func<IActorBehavior[]> defaultBehaviorFactory,
        Func<SummonBehaviorPreset> summonPresetFactory,
        Action<bool> onRoleModeChanged = null)
    {
        _summonRole = summonRole ?? throw new ArgumentNullException(nameof(summonRole));
        _configureBehaviors = configureBehaviors ?? throw new ArgumentNullException(nameof(configureBehaviors));
        _defaultBehaviorFactory = defaultBehaviorFactory ?? throw new ArgumentNullException(nameof(defaultBehaviorFactory));
        _summonPresetFactory = summonPresetFactory ?? throw new ArgumentNullException(nameof(summonPresetFactory));
        _onRoleModeChanged = onRoleModeChanged;
    }

    public FollowSummonerBehavior Refresh()
    {
        var isSummoned = _summonRole.IsSummoned;
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
