using Godot;

using System;

[GlobalClass]
public partial class ReturnHomeBehavior : Node, IActorBehavior, IActorDamageInterceptor
{
    private const string ScenePath = "Behaviors/Tier80_ReturnHome/ReturnHomeBehavior";

    [Export]
    public CombatUnitState MoveState { get; set; } = CombatUnitState.ReturningHome;

    [Export]
    public float SpeedMultiplier { get; set; } = 1.0f;

    [Export]
    public float ReturnHomeTriggerDistance { get; set; } = 48.0f;

    private readonly Func<Actor, Vector2> _destinationGetter;
    private readonly Func<Actor, bool> _isAtDestination;

    public ReturnHomeBehavior() { }

    public ReturnHomeBehavior(
        Func<Actor, Vector2> destinationGetter,
        Func<Actor, bool> isAtDestination,
        CombatUnitState moveState = CombatUnitState.ReturningHome,
        float speedMultiplier = 1.0f,
        float returnHomeTriggerDistance = 48.0f)
    {
        _destinationGetter = destinationGetter ?? throw new ArgumentNullException(nameof(destinationGetter));
        _isAtDestination = isAtDestination ?? throw new ArgumentNullException(nameof(isAtDestination));
        MoveState = moveState;
        SpeedMultiplier = Math.Max(0.0f, speedMultiplier);
        ReturnHomeTriggerDistance = Math.Max(0.0f, returnHomeTriggerDistance);
    }

    public void BeginReturnHome(Actor actor)
    {
        if (actor == null)
            return;

        // Leashing must abandon any in-flight cast so it cannot fire after the
        // actor turns to walk home.
        actor.PrimaryActionController?.Cancel(actor);
        actor.ClearTarget();
        actor.Combat.ExitCombat();
        actor.SetState(MoveState);
    }

    public bool TryCreateIntent(Actor actor, double delta, out ActorIntent intent)
    {
        intent = ActorIntent.None;

        // Encounter-owned actors never leash/return home; the encounter governs when
        // they leave combat.
        if (actor == null || actor.InCombat || actor.IsEncounterControlled)
            return false;

        var isReturningHome = actor.CurrentState == MoveState;
        if (IsAtDestination(actor))
        {
            if (!isReturningHome)
                return false;

            intent = ActorIntent.Hold(CombatUnitState.Idle);
            return true;
        }

        if (!isReturningHome && !ShouldStartReturningHome(actor))
            return false;

        intent = ActorIntent.MoveTo(GetDestination(actor), MoveState, Math.Max(0.0f, SpeedMultiplier));
        return true;
    }

    public bool TryHandleIncomingDamage(Actor actor, Damage damageInfo, out IncomingDamageDecision decision)
    {
        decision = default;

        if (actor == null ||
            actor.IsEncounterControlled ||
            damageInfo.Source is not Node2D sourceNode ||
            !actor.IsHostileTo(sourceNode) ||
            sourceNode is not ITargetable targetable ||
            !targetable.CanBeTargeted ||
            !actor.CanReachTarget(sourceNode))
        {
            return false;
        }

        decision = actor.InCombat
            ? IncomingDamageDecision.Allow()
            : IncomingDamageDecision.AllowWithRetarget(sourceNode);
        return true;
    }

    public static ReturnHomeBehavior ResolveFor(Actor actor)
    {
        return actor?.GetNodeOrNull<ReturnHomeBehavior>(ScenePath);
    }

    private bool IsAtDestination(Actor actor)
    {
        return _isAtDestination?.Invoke(actor) ?? actor.IsAtHome();
    }

    private Vector2 GetDestination(Actor actor)
    {
        return _destinationGetter?.Invoke(actor) ?? actor.HomePosition;
    }

    private bool ShouldStartReturningHome(Actor actor)
    {
        return actor.GlobalPosition.DistanceTo(GetDestination(actor)) > Math.Max(0.0f, ReturnHomeTriggerDistance);
    }
}
