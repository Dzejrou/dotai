using Godot;

using System;

[GlobalClass]
public partial class LeashBehavior : Node, IActorBehavior, IActorDamageInterceptor
{
    private static readonly Color EvadeColor = new(1.0f, 1.0f, 1.0f, 1.0f);
    private const string ScenePath = "Behaviors/Tier20_Leash/LeashBehavior";

    [Export]
    public float LossRange { get; set; } = 220.0f;

    [Export]
    public bool EvadeOnAggroLoss { get; set; } = true;

    [Export]
    public bool IgnoreDamageWhileReturning { get; set; } = true;

    [Export]
    public CombatUnitState ReturnState { get; set; } = CombatUnitState.ReturningHome;

    [Export]
    public float SpeedMultiplier { get; set; } = 1.0f;

    private readonly Func<ActorBase, Vector2> _returnDestinationGetter;
    private readonly Func<ActorBase, bool> _hasRecovered;
    private bool _isReturningHome;

    public LeashBehavior() { }

    public LeashBehavior(
        float lossRange,
        bool evadeOnAggroLoss,
        bool ignoreDamageWhileReturning,
        Func<ActorBase, Vector2> returnDestinationGetter,
        Func<ActorBase, bool> hasRecovered,
        CombatUnitState returnState = CombatUnitState.ReturningHome,
        float speedMultiplier = 1.0f)
    {
        LossRange = Math.Max(0.0f, lossRange);
        EvadeOnAggroLoss = evadeOnAggroLoss;
        IgnoreDamageWhileReturning = ignoreDamageWhileReturning;
        _returnDestinationGetter = returnDestinationGetter ?? throw new ArgumentNullException(nameof(returnDestinationGetter));
        _hasRecovered = hasRecovered ?? throw new ArgumentNullException(nameof(hasRecovered));
        ReturnState = returnState;
        SpeedMultiplier = Math.Max(0.0f, speedMultiplier);
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
            if (EvadeOnAggroLoss)
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

        if (HasRecovered(actor))
        {
            _isReturningHome = false;
            intent = ActorIntent.Hold(CombatUnitState.Idle);
            return true;
        }

        intent = ActorIntent.MoveTo(GetReturnDestination(actor), ReturnState, Math.Max(0.0f, SpeedMultiplier));
        return true;
    }

    public bool TryHandleIncomingDamage(ActorBase actor, DamageInfo damageInfo, out IncomingDamageDecision decision)
    {
        decision = default;

        if (_isReturningHome && IgnoreDamageWhileReturning)
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

    public static LeashBehavior ResolveFor(ActorBase actor)
    {
        return actor?.GetNodeOrNull<LeashBehavior>(ScenePath);
    }

    private bool HasRecovered(ActorBase actor)
    {
        return _hasRecovered?.Invoke(actor) ?? actor.IsAtHome();
    }

    private Vector2 GetReturnDestination(ActorBase actor)
    {
        return _returnDestinationGetter?.Invoke(actor) ?? actor.HomePosition;
    }

    private bool IsTargetWithinLossRange(ActorBase actor, Node2D target)
    {
        if (target == null)
            return false;

        return actor.GlobalPosition.DistanceTo(target.GlobalPosition) <= Math.Max(0.0f, LossRange);
    }
}
