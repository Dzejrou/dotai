using Godot;

using System;

public sealed class LeashBehavior : IActorBehavior, IActorDamageInterceptor
{
    private static readonly Color EvadeColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);

    private readonly float _lossRange;
    private readonly bool _evadeOnAggroLoss;
    private readonly bool _ignoreDamageWhileReturning;
    private readonly Func<ActorBase, Vector2> _returnDestinationGetter;
    private readonly Func<ActorBase, bool> _hasRecovered;
    private readonly CombatUnitState _returnState;
    private readonly float _speedMultiplier;
    private bool _isReturningHome;

    public LeashBehavior(
        float lossRange,
        bool evadeOnAggroLoss,
        bool ignoreDamageWhileReturning,
        Func<ActorBase, Vector2> returnDestinationGetter,
        Func<ActorBase, bool> hasRecovered,
        CombatUnitState returnState = CombatUnitState.ReturningHome,
        float speedMultiplier = 1.0f)
    {
        _lossRange = Math.Max(0.0f, lossRange);
        _evadeOnAggroLoss = evadeOnAggroLoss;
        _ignoreDamageWhileReturning = ignoreDamageWhileReturning;
        _returnDestinationGetter = returnDestinationGetter ?? throw new ArgumentNullException(nameof(returnDestinationGetter));
        _hasRecovered = hasRecovered ?? throw new ArgumentNullException(nameof(hasRecovered));
        _returnState = returnState;
        _speedMultiplier = Math.Max(0.0f, speedMultiplier);
    }

    public bool IsReturningHome => _isReturningHome;

    public void BeginReturnHome(ActorBase actor, bool showEvadeText)
    {
        if (showEvadeText)
            actor.ShowFloatingDamageNumber("EVADE", EvadeColor);

        _isReturningHome = true;
        actor.ClearTarget();
    }

    public bool TryCreateIntent(ActorBase actor, double delta, out ActorIntent intent)
    {
        intent = ActorIntent.None;

        if (!_isReturningHome && actor.CurrentTarget != null && !IsTargetWithinLossRange(actor, actor.CurrentTarget))
        {
            if (_evadeOnAggroLoss)
            {
                BeginReturnHome(actor, false);
            }
            else
            {
                intent = ActorIntent.ClearTarget();
                return true;
            }
        }

        if (!_isReturningHome)
            return false;

        if (_hasRecovered(actor))
        {
            _isReturningHome = false;
            intent = ActorIntent.Hold(CombatUnitState.Idle);
            return true;
        }

        intent = ActorIntent.MoveTo(_returnDestinationGetter(actor), _returnState, _speedMultiplier);
        return true;
    }

    public bool TryHandleIncomingDamage(ActorBase actor, DamageInfo damageInfo, out IncomingDamageDecision decision)
    {
        decision = default;

        if (_isReturningHome && _ignoreDamageWhileReturning)
        {
            decision = IncomingDamageDecision.Deny("EVADE", EvadeColor);
            return true;
        }

        if (damageInfo.Source is not Node2D sourceNode)
            return false;

        if (!actor.IsHostileTo(sourceNode))
            return false;

        if (sourceNode is not ITargetable targetable || !targetable.CanBeTargeted)
            return false;

        if (IsTargetWithinLossRange(actor, sourceNode))
        {
            _isReturningHome = false;
            decision = IncomingDamageDecision.AllowWithRetarget(sourceNode);
            return true;
        }

        decision = IncomingDamageDecision.Deny("EVADE", EvadeColor);
        return true;
    }

    private bool IsTargetWithinLossRange(ActorBase actor, Node2D target)
    {
        if (target == null)
            return false;

        return actor.GlobalPosition.DistanceTo(target.GlobalPosition) <= _lossRange;
    }
}
