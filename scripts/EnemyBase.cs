using Godot;

using System;

public abstract partial class EnemyBase : ActorBase, IAggressiveCombatActorAIHost
{
    private const float PursuitStuckProgressThreshold = 1.0f;
    private const float PursuitStuckTimeout = 0.6f;
    private const float PursuitStuckWaypointDistance = 8.0f;

    [Export]
    public NodePath InitialTargetPath { get; set; } = new NodePath("../Player");

    [Export]
    public float AggroAcquisitionRange { get; set; } = 150.0f;

    [Export]
    public float AggroLossRange { get; set; } = 220.0f;

    [Export]
    public bool EvadeOnAggroLoss { get; set; } = true;

    [Export]
    public bool IgnoreDamageWhileEvading { get; set; } = true;
    private bool _hasPursuitProgressPosition;
    private Vector2 _lastPursuitProgressPosition;
    private float _pursuitStuckTimer;
    private Node2D _trackedPursuitTarget;
    private bool _suppressTargetAcquisitionUntilHome;

    protected void InitializeEnemy(AnimatedSprite2D animatedSprite, CollisionShape2D collisionShape, string enemyName)
    {
        InitializeEnemy(animatedSprite, collisionShape, null, enemyName);
    }

    protected void InitializeEnemy(
        AnimatedSprite2D animatedSprite,
        CollisionShape2D collisionShape,
        NavigationAgent2D navigationAgent,
        string enemyName)
    {
        InitializeActor(animatedSprite, collisionShape, navigationAgent);
        AddToGroup(CombatGroups.Enemies);

        var resolvedTarget = CurrentTarget;
        if (resolvedTarget == null)
        {
            if (!InitialTargetPath.IsEmpty && HasNode(InitialTargetPath))
                resolvedTarget = GetNode<Node2D>(InitialTargetPath);
            else
                resolvedTarget = GetParent()?.GetNodeOrNull<Node2D>("Player");
        }

        if (resolvedTarget != null && CanAcquireTarget(resolvedTarget))
        {
            SetTarget(resolvedTarget);
        }
        else if (resolvedTarget != null && !CanAcquireTarget(resolvedTarget))
            GD.PrintErr($"{enemyName} did not acquire initial target (not in aggro range).");
    }

    protected override void AcquireTarget()
    {
        TryAcquireAggressiveTarget();
    }

    public bool TryAcquireAggressiveTarget()
    {
        if (_suppressTargetAcquisitionUntilHome)
            return false;

        var candidate = TargetingHelper.FindClosestHostileTarget(
            this,
            Faction,
            node => node is Node2D targetNode && CanAcquireTarget(targetNode));

        if (candidate != null && CanAcquireTarget(candidate))
        {
            SetTarget(candidate);
            ResetPursuitStuckTracking();
            return true;
        }

        return false;
    }

    protected bool CanAcquireTarget(Node2D target)
    {
        return target is IAttackable &&
               target is ITargetable targetable &&
               targetable.CanBeTargeted &&
               IsHostileTarget(target) &&
               IsTargetWithinAcquisitionRange(target);
    }

    protected override bool ShouldLoseCurrentTarget(Node2D target)
    {
        var shouldLoseTarget = !IsTargetWithinLossRange(target);
        if (shouldLoseTarget && EvadeOnAggroLoss)
            BeginEvadeReset(false);

        return shouldLoseTarget;
    }

    private bool IsTargetWithinAcquisitionRange(Node2D target)
    {
        return IsTargetWithinRange(target, Math.Max(0.0f, AggroAcquisitionRange));
    }

    protected bool IsTargetWithinLossRange(Node2D target)
    {
        return IsTargetWithinRange(target, Math.Max(AggroLossRange, AggroAcquisitionRange));
    }

    private bool IsTargetWithinRange(Node2D target, float range)
    {
        if (target == null)
            return false;

        return GlobalPosition.DistanceTo(target.GlobalPosition) <= range;
    }

    private bool IsHostileTarget(Node target)
    {
        return Faction != null && Faction.IsHostileTo(Factions.ResolveForNode(target));
    }

    protected bool TryReactToDamageSource(DamageInfo damageInfo)
    {
        if (IsEvadingHomeReturn() && IgnoreDamageWhileEvading)
        {
            ShowFloatingDamageNumber("EVADE", new Color(1.0f, 1.0f, 1.0f, 1.0f));
            return false;
        }

        if (damageInfo.Source is not Node2D sourceNode)
            return true;

        if (!IsHostileTarget(sourceNode))
            return true;

        if (sourceNode is not ITargetable targetable || !targetable.CanBeTargeted)
            return true;

        if (IsTargetWithinLossRange(sourceNode))
        {
            _suppressTargetAcquisitionUntilHome = false;
            SetTarget(sourceNode);
            return true;
        }

        ShowFloatingDamageNumber("EVADE", new Color(1.0f, 1.0f, 1.0f, 1.0f));
        return false;
    }

    protected void ShowFloatingDamageNumber(string text, Color color)
    {
        FloatingNumberHelper.ShowFloatingNumber(this, text, color);
    }

    protected override void OnReachedHomeWithoutTarget()
    {
        _suppressTargetAcquisitionUntilHome = false;
        ResetPursuitStuckTracking();
    }

    protected override void OnActorPrePhysicsProcess(double delta)
    {
        UpdatePursuitStuckEvade((float)delta);
    }

    protected bool TryApplyEnemyDamage(DamageInfo damageInfo, out int damage, out bool died)
    {
        damage = 0;
        died = false;

        if (IsDead)
            return false;

        if (!TryReactToDamageSource(damageInfo))
            return false;

        damage = Math.Max(1, damageInfo.Amount);
        SetCurrentHealth(Math.Max(0, CurrentHealth - damage));
        died = CurrentHealth <= 0;
        if (died)
            SetIsDead(true);

        return true;
    }

    private void UpdatePursuitStuckEvade(float delta)
    {
        if (_suppressTargetAcquisitionUntilHome)
        {
            ResetPursuitStuckTracking();
            return;
        }

        if (CurrentTarget == null ||
            CurrentState != CombatUnitState.PursuingTarget ||
            !IsUsingNavigationPath ||
            Velocity == Vector2.Zero)
        {
            ResetPursuitStuckTracking();
            return;
        }

        if (GlobalPosition.DistanceTo(LastNavigationPathPosition) <= PursuitStuckWaypointDistance)
        {
            ResetPursuitStuckTracking();
            return;
        }

        if (!ReferenceEquals(_trackedPursuitTarget, CurrentTarget))
        {
            _trackedPursuitTarget = CurrentTarget;
            _hasPursuitProgressPosition = true;
            _lastPursuitProgressPosition = GlobalPosition;
            _pursuitStuckTimer = 0.0f;
            return;
        }

        if (!_hasPursuitProgressPosition)
        {
            _hasPursuitProgressPosition = true;
            _lastPursuitProgressPosition = GlobalPosition;
            _pursuitStuckTimer = 0.0f;
            return;
        }

        if (GlobalPosition.DistanceTo(_lastPursuitProgressPosition) > PursuitStuckProgressThreshold)
        {
            _lastPursuitProgressPosition = GlobalPosition;
            _pursuitStuckTimer = 0.0f;
            return;
        }

        _pursuitStuckTimer += Math.Max(0.0f, delta);
        if (_pursuitStuckTimer < PursuitStuckTimeout)
            return;

        BeginEvadeReset(true);
    }

    private void ResetPursuitStuckTracking()
    {
        _hasPursuitProgressPosition = false;
        _lastPursuitProgressPosition = Vector2.Zero;
        _pursuitStuckTimer = 0.0f;
        _trackedPursuitTarget = null;
    }

    private void BeginEvadeReset(bool showEvadeText)
    {
        if (showEvadeText)
            ShowFloatingDamageNumber("EVADE", new Color(1.0f, 1.0f, 1.0f, 1.0f));

        _suppressTargetAcquisitionUntilHome = true;
        ClearTarget();
        ResetPursuitStuckTracking();
    }

    private bool IsEvadingHomeReturn()
    {
        return _suppressTargetAcquisitionUntilHome;
    }

}
