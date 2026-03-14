using Godot;

using System;

public abstract partial class EnemyBase : CombatUnitBase
{
    [Export]
    public NodePath InitialTargetPath { get; set; } = new NodePath("../Player");

    [Export]
    public StringName DeathAnimation { get; set; } = "falling-back-death";

    [Export]
    public bool DisableCollisionOnDeath { get; set; } = true;

    [Export]
    public float AggroAcquisitionRange { get; set; } = 150.0f;

    [Export]
    public float AggroLossRange { get; set; } = 220.0f;

    [Export]
    public float HomeReturnTolerance { get; set; } = 4.0f;

    protected Vector2 HomePosition { get; private set; }

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
        InitializeCombatUnit(animatedSprite, collisionShape, navigationAgent);
        AddToGroup(CombatGroups.Enemies);
        HomePosition = GlobalPosition;

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
        var candidate = TargetingHelper.FindClosestTarget(
            this,
            CombatGroups.Allies,
            node => node is Node2D && node is IAttackable && node is ITargetable targetable && targetable.CanBeTargeted);

        if (candidate != null && CanAcquireTarget(candidate))
            SetTarget(candidate);
    }

    protected bool CanAcquireTarget(Node2D target)
    {
        return target is IAttackable && target is ITargetable targetable && targetable.CanBeTargeted &&
               IsTargetWithinAcquisitionRange(target);
    }

    protected override bool ShouldLoseCurrentTarget(Node2D target)
    {
        return !IsTargetWithinLossRange(target);
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

    protected bool TryReactToDamageSource(DamageInfo damageInfo)
    {
        if (damageInfo.Source is not Node2D sourceNode)
            return true;

        if (!sourceNode.IsInGroup(CombatGroups.Allies))
            return true;

        if (sourceNode is not ITargetable targetable || !targetable.CanBeTargeted)
            return true;

        if (IsTargetWithinLossRange(sourceNode))
        {
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

    protected bool IsAtHome()
    {
        return GlobalPosition.DistanceTo(HomePosition) <= Math.Max(0.0f, HomeReturnTolerance);
    }

    protected override bool HandleNoTarget(double delta)
    {
        if (IsAtHome())
            return false;

        return TryMoveTowardDestination(HomePosition, 1.0f, CombatUnitState.ReturningHome, delta);
    }

    protected bool TryFinalizeDeathAnimation() => TryFinalizeDeathAnimation(DeathAnimation);

    protected bool TryPlayDeathAnimation() => TryPlayDeathAnimation(DeathAnimation, DisableCollisionOnDeath);
}
