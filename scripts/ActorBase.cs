using Godot;

using System;
using System.Collections.Generic;

public abstract partial class ActorBase : CharacterBody2D, IFactionMember
{
    private const float NavigationTargetUpdateThreshold = 8.0f;
    private const float DefaultPathDesiredDistance = 6.0f;
    private const float DefaultTargetDesiredDistance = 8.0f;

    [Export]
    public StringName DeathAnimation { get; set; } = "falling-back-death";

    [Export]
    public bool DisableCollisionOnDeath { get; set; } = true;

    [Export]
    public float HomeReturnTolerance { get; set; } = 4.0f;

    public AnimatedSprite2D AnimatedSprite { get; private set; }
    public CollisionShape2D CollisionShape { get; private set; }
    public NavigationAgent2D NavigationAgent { get; private set; }
    public Node2D CurrentTarget { get; private set; }
    public bool IsUsingNavigationPath { get; private set; }
    public Vector2 LastNavigationPathPosition { get; private set; }
    public float MovementSpeed { get; private set; } = 1.0f;
    public string LastDirection { get; private set; } = "south";
    public Vector2 HomePosition { get; private set; }
    public int CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }
    public int ResolvedMaxHealth => Math.Max(1, MaxHealthValue);
    public ICombatActionController PrimaryActionController { get; private set; }

    [Export]
    public CombatUnitState CurrentState { get; private set; } = CombatUnitState.Idle;

    public abstract Faction Faction { get; }

    private readonly List<IActorBehavior> _behaviors = new();
    private readonly List<IActorTickBehavior> _tickBehaviors = new();
    private readonly List<IActorDamageInterceptor> _damageInterceptors = new();
    private bool _hasNavigationDestination;
    private Vector2 _lastNavigationDestination;
    private ActorHUD _actorHud;

    protected abstract int MaxHealthValue { get; }

    protected void InitializeActor(
        AnimatedSprite2D animatedSprite,
        CollisionShape2D collisionShape,
        NavigationAgent2D navigationAgent = null)
    {
        AnimatedSprite = animatedSprite;
        CollisionShape = collisionShape;
        NavigationAgent = navigationAgent;

        if (NavigationAgent != null)
        {
            NavigationAgent.PathDesiredDistance = DefaultPathDesiredDistance;
            NavigationAgent.TargetDesiredDistance = DefaultTargetDesiredDistance;
        }

        CurrentHealth = ResolvedMaxHealth;
        IsDead = false;
        HomePosition = GlobalPosition;
        _actorHud = GetNodeOrNull<ActorHUD>("ActorHUD");
        if (_actorHud == null)
            GD.PushError($"{GetPath()}: missing required ActorHUD child.");
        else
            _actorHud.Bind(this);
        RefreshHealthLabel();

        if (AnimatedSprite != null)
            AnimatedSprite.AnimationFinished += OnAnimatedSpriteAnimationFinished;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDead)
        {
            Velocity = Vector2.Zero;
            return;
        }

        PrimaryActionController?.Update(this, delta);
        foreach (var tickBehavior in _tickBehaviors)
            tickBehavior.Update(this, delta);

        if (!IsStructurallyValidTarget(CurrentTarget))
            ClearTarget();

        if (CurrentState == CombatUnitState.Attacking)
        {
            Velocity = Vector2.Zero;
            return;
        }

        if (TryResolveBehaviorIntent(delta, out var winningIntent))
        {
            ExecuteIntent(winningIntent, delta);
            return;
        }

        StopAndIdle();
    }

    public override void _ExitTree()
    {
        if (AnimatedSprite != null)
            AnimatedSprite.AnimationFinished -= OnAnimatedSpriteAnimationFinished;

        OnActorExitTree();
    }

    public void SetTarget(Node2D target)
    {
        CurrentTarget = target;
    }

    public void ClearTarget()
    {
        CurrentTarget = null;
    }

    public void SetState(CombatUnitState state)
    {
        CurrentState = state;
    }

    public void SetFacingDirection(Vector2 direction)
    {
        if (direction != Vector2.Zero)
            LastDirection = DirectionHelper.GetDirectionName(direction);
    }

    public void TeleportTo(Vector2 position)
    {
        GlobalPosition = position;
        Velocity = Vector2.Zero;
        ResetNavigationPathState();
    }

    public void FinishAttackState()
    {
        if (CurrentState != CombatUnitState.Attacking)
            return;

        SetState(CurrentTarget != null ? CombatUnitState.PursuingTarget : CombatUnitState.Idle);
    }

    public bool IsAtHome()
    {
        return GlobalPosition.DistanceTo(HomePosition) <= Math.Max(0.0f, HomeReturnTolerance);
    }

    public bool IsHostileTo(Node target)
    {
        return Faction != null && Faction.IsHostileTo(Factions.ResolveForNode(target));
    }

    public void ApplyHealing(int amount)
    {
        if (amount <= 0 || IsDead)
            return;

        var clampedHealing = Math.Min(amount, ResolvedMaxHealth - CurrentHealth);
        if (clampedHealing <= 0)
            return;

        SetCurrentHealth(CurrentHealth + clampedHealing);
        ShowFloatingHealingNumber(clampedHealing);
    }

    public void PlayIdleIfAvailable()
    {
        if (AnimatedSprite?.SpriteFrames == null)
            return;

        var idleAnimation = $"breathing-idle_{LastDirection}";
        if (!AnimatedSprite.SpriteFrames.HasAnimation(idleAnimation))
            return;

        if (!AnimatedSprite.IsPlaying() || AnimatedSprite.Animation != idleAnimation)
            AnimatedSprite.Play(idleAnimation);
    }

    public bool TryMoveTowardDestination(Vector2 destinationPosition, float speedMultiplier, CombatUnitState movingState, double delta)
    {
        var movement = ResolveMovementDirection(destinationPosition, delta);
        if (movement == Vector2.Zero)
            return false;

        SetState(movingState);

        var normalizedMovement = movement.Normalized();
        SetFacingDirection(normalizedMovement);
        var walkAnimation = $"walk_{LastDirection}";
        if (AnimatedSprite?.SpriteFrames != null &&
            AnimatedSprite.SpriteFrames.HasAnimation(walkAnimation) &&
            (!AnimatedSprite.IsPlaying() || AnimatedSprite.Animation != walkAnimation))
        {
            AnimatedSprite.Play(walkAnimation);
        }

        Velocity = normalizedMovement * MovementSpeed * Math.Max(0.0f, speedMultiplier);
        return true;
    }

    public void ShowFloatingDamageNumber(string text, Color color)
    {
        _actorHud?.ShowFloatingText(text, color);
    }

    public static bool IsStructurallyValidTarget(Node2D target)
    {
        return target != null && GodotObject.IsInstanceValid(target) && target.IsInsideTree();
    }

    protected void ConfigureBehaviors(params IActorBehavior[] behaviors)
    {
        _behaviors.Clear();
        _tickBehaviors.Clear();
        _damageInterceptors.Clear();

        foreach (var behavior in behaviors)
        {
            if (behavior == null)
                continue;

            _behaviors.Add(behavior);
            if (behavior is IActorTickBehavior tickBehavior)
                _tickBehaviors.Add(tickBehavior);
            if (behavior is IActorDamageInterceptor damageInterceptor)
                _damageInterceptors.Add(damageInterceptor);
        }
    }

    protected void SetPrimaryActionController(ICombatActionController actionController)
    {
        PrimaryActionController = actionController;
    }

    protected void SetMovementSpeed(float speed)
    {
        MovementSpeed = Math.Max(0.0f, speed);
    }

    protected void ApplyFactionCombatGroup()
    {
        Factions.ApplyCombatGroup(this, Faction);
    }

    protected void SetCurrentHealth(int value)
    {
        CurrentHealth = Math.Clamp(value, 0, ResolvedMaxHealth);
        RefreshHealthLabel();
    }

    protected void SetIsDead(bool value)
    {
        IsDead = value;
    }

    protected void MarkDead()
    {
        CurrentState = CombatUnitState.Dead;
    }

    protected void ResetPrimaryActionController()
    {
        PrimaryActionController?.Cancel(this);
    }

    protected void RefreshHealthLabel()
    {
        if (_actorHud == null)
            return;

        _actorHud.SetHealth(CurrentHealth, ResolvedMaxHealth);
        _actorHud.SetFaction(Faction);
    }

    protected void ShowFloatingHealingNumber(int amount)
    {
        if (amount <= 0 || _actorHud == null)
            return;

        _actorHud.ShowFloatingText($"+{amount}", new Color(0.0f, 1.0f, 0.0f, 1.0f));
    }

    protected bool TryApplyIncomingDamage(DamageInfo damageInfo, out int damage, out bool died)
    {
        damage = 0;
        died = false;

        if (IsDead)
            return false;

        foreach (var damageInterceptor in _damageInterceptors)
        {
            if (!damageInterceptor.TryHandleIncomingDamage(this, damageInfo, out var decision))
                continue;

            if (!string.IsNullOrEmpty(decision.FloatingText))
                ShowFloatingDamageNumber(decision.FloatingText, decision.FloatingTextColor);

            if (decision.RetargetTo != null && IsStructurallyValidTarget(decision.RetargetTo))
                SetTarget(decision.RetargetTo);

            if (!decision.AllowDamage)
                return false;

            break;
        }

        damage = Math.Max(1, damageInfo.Amount);
        SetCurrentHealth(CurrentHealth - damage);
        died = CurrentHealth <= 0;
        if (died)
            SetIsDead(true);

        return true;
    }

    protected bool TryFinalizeDeathAnimation()
    {
        if (AnimatedSprite?.SpriteFrames == null)
            return false;

        var animationName = AnimatedSprite.Animation.ToString();
        if (!animationName.StartsWith(DeathAnimation.ToString(), StringComparison.Ordinal))
            return false;

        var finalFrame = Math.Max(0, AnimatedSprite.SpriteFrames.GetFrameCount(animationName) - 1);
        AnimatedSprite.Stop();
        AnimatedSprite.SetFrame(finalFrame);
        SetPhysicsProcess(false);
        return true;
    }

    protected bool TryPlayDeathAnimation(bool queueFreeOnMissingAnimation = false)
    {
        if (DisableCollisionOnDeath && CollisionShape != null)
            CollisionShape.CallDeferred("set", "disabled", true);

        var animationName = $"{DeathAnimation}_{LastDirection}";
        if (AnimatedSprite?.SpriteFrames != null &&
            AnimatedSprite.SpriteFrames.HasAnimation(animationName) &&
            AnimatedSprite.SpriteFrames.GetFrameCount(animationName) > 0)
        {
            AnimatedSprite.Play(animationName);
            return true;
        }

        if (queueFreeOnMissingAnimation)
            QueueFree();
        else
            SetPhysicsProcess(false);

        return false;
    }

    protected virtual void OnActorExitTree() { }

    protected virtual void OnDeathAnimationFinalized() { }

    private void OnAnimatedSpriteAnimationFinished()
    {
        if (AnimatedSprite == null)
            return;

        var animationName = AnimatedSprite.Animation;
        if (PrimaryActionController?.HandleAnimationFinished(this, animationName) == true)
            return;

        if (TryFinalizeDeathAnimation())
            OnDeathAnimationFinalized();
    }

    private bool TryResolveBehaviorIntent(double delta, out ActorIntent winningIntent)
    {
        winningIntent = ActorIntent.None;

        foreach (var behavior in _behaviors)
        {
            if (!behavior.TryCreateIntent(this, delta, out var candidateIntent))
                continue;

            if (candidateIntent.ChangeTarget)
            {
                if (candidateIntent.Target == null)
                    ClearTarget();
                else
                    SetTarget(candidateIntent.Target);
            }

            if (!candidateIntent.HasExecutionDirective)
                continue;

            winningIntent = candidateIntent;
            return true;
        }

        return false;
    }

    private void ExecuteIntent(ActorIntent intent, double delta)
    {
        if (intent.UsePrimaryAction && PrimaryActionController != null && CurrentTarget != null)
        {
            Velocity = Vector2.Zero;
            PrimaryActionController.StartAction(this, CurrentTarget);
            return;
        }

        if (intent.StopMovement)
        {
            SetState(intent.State);
            ResetNavigationPathState();
            Velocity = Vector2.Zero;
            if (CurrentState != CombatUnitState.Attacking)
                PlayIdleIfAvailable();
            return;
        }

        if (intent.Destination.HasValue)
        {
            if (TryMoveTowardDestination(intent.Destination.Value, intent.SpeedMultiplier, intent.State, delta))
            {
                MoveAndSlide();
                return;
            }
        }

        StopAndIdle();
    }

    private void StopAndIdle()
    {
        ResetNavigationPathState();
        SetState(CombatUnitState.Idle);
        Velocity = Vector2.Zero;
        PlayIdleIfAvailable();
    }

    private Vector2 ResolveMovementDirection(Vector2 desiredDestination, double delta)
    {
        if (desiredDestination == GlobalPosition)
        {
            ResetNavigationPathState();
            return Vector2.Zero;
        }

        var agentInsideTree = NavigationAgent != null && NavigationAgent.IsInsideTree();
        if (!agentInsideTree)
        {
            ResetNavigationPathState();
            return desiredDestination - GlobalPosition;
        }

        if (!NavigationAgent.GetNavigationMap().IsValid)
        {
            ResetNavigationPathState();
            return desiredDestination - GlobalPosition;
        }

        if (ShouldRefreshNavigationTarget(desiredDestination))
            RefreshNavigationTarget(desiredDestination);

        var nextPathPosition = NavigationAgent.GetNextPathPosition();
        IsUsingNavigationPath = true;
        LastNavigationPathPosition = nextPathPosition;

        var movementToPath = nextPathPosition - GlobalPosition;
        if (movementToPath == Vector2.Zero)
            movementToPath = desiredDestination - GlobalPosition;

        return movementToPath;
    }

    private bool ShouldRefreshNavigationTarget(Vector2 desiredDestination)
    {
        if (!_hasNavigationDestination)
            return true;

        return _lastNavigationDestination.DistanceTo(desiredDestination) > NavigationTargetUpdateThreshold;
    }

    private void RefreshNavigationTarget(Vector2 desiredDestination)
    {
        if (NavigationAgent == null)
            return;

        NavigationAgent.TargetPosition = desiredDestination;
        _hasNavigationDestination = true;
        _lastNavigationDestination = desiredDestination;
    }

    private void ResetNavigationPathState()
    {
        _hasNavigationDestination = false;
        IsUsingNavigationPath = false;
        LastNavigationPathPosition = Vector2.Zero;
    }

}
