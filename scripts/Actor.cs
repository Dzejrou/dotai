using Godot;

using System;
using System.Collections.Generic;

public abstract partial class Actor : CharacterBody2D, IFactionMember, IHealable
{
    private const string DefaultCorpseScenePath = "res://scenes/world/corpse.tscn";
    private const string BehaviorNodeTargetingPath = "Behaviors/Tier10_Targeting";
    private const string BehaviorNodeLeashPath = "Behaviors/Tier20_Leash";
    private const string BehaviorNodeCombatPath = "Behaviors/Tier50_Combat";
    private const string BehaviorNodeReturnHomePath = "Behaviors/Tier80_ReturnHome";
    private const string BehaviorNodeRecoveryPath = "Behaviors/Tier90_Recovery";
    private const float NavigationTargetUpdateThreshold = 8.0f;
    private const float DefaultPathDesiredDistance = 6.0f;
    private const float DefaultTargetDesiredDistance = 8.0f;
    private const float ShortRangeDirectMovementDistance = 24.0f;

    [Export]
    public StringName DeathAnimation { get; set; } = "falling-back-death";

    [Export]
    public float HomeReturnTolerance { get; set; } = 4.0f;

    public AnimatedSprite2D AnimatedSprite { get; private set; }
    public CollisionShape2D CollisionShape { get; private set; }
    public NavigationAgent2D NavigationAgent { get; private set; }
    public CombatState Combat => _combat;
    public Node2D Target => _combat.Target;
    public bool InCombat => _combat.InCombat;
    public bool IsUsingNavigationPath { get; private set; }
    public Vector2 LastNavigationPathPosition { get; private set; }
    public float MovementSpeed { get; private set; } = 1.0f;
    public string LastDirection { get; private set; } = "south";
    public Vector2 HomePosition { get; private set; }
    public int CurrentHealth => _health.Current;
    public bool IsDead => _health.IsDead;
    public Faction Faction => _faction.Current;
    public int ResolvedMaxHealth
    {
        get
        {
            var desiredMaxHealth = Math.Max(1, MaxHealthValue);
            if (_health.Max != desiredMaxHealth)
                _health.SetMax(desiredMaxHealth);
            return _health.Max;
        }
    }
    public int MaxHealableHealth => ResolvedMaxHealth;
    public bool CanReceiveHealing => !IsDead && CurrentHealth < ResolvedMaxHealth;
    public ICombatActionController PrimaryActionController { get; private set; }

    [Export]
    public CombatUnitState CurrentState { get; private set; } = CombatUnitState.Idle;

    private readonly List<IActorBehavior> _behaviors = new();
    private readonly List<IActorTickBehavior> _tickBehaviors = new();
    private readonly List<IActorDamageInterceptor> _damageInterceptors = new();
    private bool _hasNavigationDestination;
    private Vector2 _lastNavigationDestination;
    private ActorHUD _actorHud;
    private HealthState _health;
    private CombatState _combat;
    private FactionState _faction;
    private bool _subscribedToNavigationDebug;
    private static PackedScene _corpseScene;

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
            ApplyNavigationDebugState(NavigationDebugSettings.Enabled);
            SubscribeToNavigationDebug();
        }

        AddToGroup(CombatGroups.Actors);
        HomePosition = GlobalPosition;
        _combat = GetNode<CombatState>("CombatState");
        _combat.ClearTarget();
        _combat.ExitCombat();
        _health = GetNode<HealthState>("HealthState");
        _health.Initialize(Math.Max(1, MaxHealthValue));
        _faction = GetNode<FactionState>("FactionState");
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

        _combat.Update(delta);

        PrimaryActionController?.Update(this, delta);
        foreach (var tickBehavior in _tickBehaviors)
            tickBehavior.Update(this, delta);

        if (!IsStructurallyValidTarget(Target))
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

        UnsubscribeFromNavigationDebug();
        OnActorExitTree();
    }

    public void SetTarget(Node2D target)
    {
        _combat.SetTarget(target);
    }

    public void ClearTarget()
    {
        _combat.ClearTarget();
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

        SetState(Target != null ? CombatUnitState.PursuingTarget : CombatUnitState.Idle);
    }

    public bool IsAtHome()
    {
        return GlobalPosition.DistanceTo(HomePosition) <= Math.Max(0.0f, HomeReturnTolerance);
    }

    public bool IsHostileTo(Node target)
    {
        return _faction.IsHostileTo(target);
    }

    public bool IsFriendlyTo(Node target)
    {
        return _faction.IsFriendlyTo(target);
    }

    public void ApplyHealing(int amount)
    {
        if (amount <= 0 || IsDead)
            return;

        var healedAmount = _health.ApplyHealing(amount);
        if (healedAmount <= 0)
            return;

        RefreshHealthLabel();
        ShowFloatingHealingNumber(healedAmount);
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

    private void AppendBehaviorNodes(Node root)
    {
        if (root == null)
            return;

        foreach (var child in root.GetChildren())
        {
            if (child is Node childNode)
                AppendBehaviorNodesRecursive(childNode);
        }
    }

    private void AppendBehaviorNodesRecursive(Node node)
    {
        AppendBehavior(node as IActorBehavior);

        foreach (var child in node.GetChildren())
        {
            if (child is Node childNode)
                AppendBehaviorNodesRecursive(childNode);
        }
    }

    private void AppendBehavior(IActorBehavior behavior)
    {
        if (behavior == null)
            return;

        _behaviors.Add(behavior);
        if (behavior is IActorTickBehavior tickBehavior)
            _tickBehaviors.Add(tickBehavior);
        if (behavior is IActorDamageInterceptor damageInterceptor)
            _damageInterceptors.Add(damageInterceptor);
    }

    protected void ConfigureBehaviors(params IActorBehavior[] behaviors)
    {
        _behaviors.Clear();
        _tickBehaviors.Clear();
        _damageInterceptors.Clear();

        AppendBehaviorNodes(GetNodeOrNull<Node>(BehaviorNodeTargetingPath));
        AppendBehaviorNodes(GetNodeOrNull<Node>(BehaviorNodeLeashPath));

        foreach (var behavior in behaviors)
            AppendBehavior(behavior);

        AppendBehaviorNodes(GetNodeOrNull<Node>(BehaviorNodeCombatPath));
        AppendBehaviorNodes(GetNodeOrNull<Node>(BehaviorNodeRecoveryPath));
        AppendBehaviorNodes(GetNodeOrNull<Node>(BehaviorNodeReturnHomePath));
    }

    protected void SetPrimaryActionController(ICombatActionController actionController)
    {
        PrimaryActionController = actionController;
    }

    protected void SetMovementSpeed(float speed)
    {
        MovementSpeed = Math.Max(0.0f, speed);
    }

    protected void SetIsDead(bool value)
    {
        _health.SetDead(value);
        if (value)
        {
            CleanupNavigationForInactiveState();
            _combat.ClearTarget();
            _combat.ExitCombat();
        }
    }

    protected void ResetPrimaryActionController()
    {
        PrimaryActionController?.Cancel(this);
    }

    protected void PrepareForRemoval()
    {
        CleanupNavigationForInactiveState();
    }

    protected void SpawnCorpseAndFree()
    {
        PrepareForRemoval();
        Velocity = Vector2.Zero;
        ResetPrimaryActionController();
        SpawnCorpse();
        QueueFree();
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

        damage = _health.ApplyDamage(damageInfo.Amount);
        RefreshHealthLabel();
        damageInfo.RegisterHit(this, setReceiverTargetToSource: false);

        died = _health.IsDead;
        if (died)
            SetIsDead(true);

        return true;
    }

    private void ApplyNavigationDebugState(bool enabled)
    {
        if (NavigationAgent == null)
            return;

        NavigationAgent.DebugEnabled = enabled;
    }

    private void SubscribeToNavigationDebug()
    {
        if (_subscribedToNavigationDebug || NavigationAgent == null)
            return;

        NavigationDebugSettings.Changed += ApplyNavigationDebugState;
        _subscribedToNavigationDebug = true;
    }

    private void UnsubscribeFromNavigationDebug()
    {
        if (!_subscribedToNavigationDebug)
            return;

        NavigationDebugSettings.Changed -= ApplyNavigationDebugState;
        _subscribedToNavigationDebug = false;
    }

    private void CleanupNavigationForInactiveState()
    {
        ResetNavigationPathState();
        Velocity = Vector2.Zero;

        if (NavigationAgent == null)
            return;

        if (NavigationAgent.IsInsideTree())
            NavigationAgent.TargetPosition = GlobalPosition;

        NavigationAgent.SetPhysicsProcess(false);
        ApplyNavigationDebugState(false);
        UnsubscribeFromNavigationDebug();
    }

    protected virtual void OnActorExitTree() { }

    private void OnAnimatedSpriteAnimationFinished()
    {
        if (AnimatedSprite == null)
            return;

        var animationName = AnimatedSprite.Animation;
        PrimaryActionController?.HandleAnimationFinished(this, animationName);
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

    private void ApplyIntentTargetChange(ActorIntent intent)
    {
        if (!intent.ChangeTarget)
            return;

        if (intent.Target == null)
            ClearTarget();
        else
            SetTarget(intent.Target);
    }

    private void ExecuteIntent(ActorIntent intent, double delta)
    {
        if (intent.FacingDirection.HasValue && intent.FacingDirection.Value != Vector2.Zero)
            SetFacingDirection(intent.FacingDirection.Value);

        if (intent.RemoveNow)
        {
            PrepareForRemoval();
            QueueFree();
            return;
        }

        if (intent.TeleportDestination.HasValue)
        {
            TeleportTo(intent.TeleportDestination.Value);
        }

        if (intent.UsePrimaryAction && PrimaryActionController != null && Target != null)
        {
            Velocity = Vector2.Zero;
            PrimaryActionController.StartAction(this, Target);
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
        var movementToDestination = desiredDestination - GlobalPosition;
        if (movementToDestination == Vector2.Zero)
        {
            ResetNavigationPathState();
            return Vector2.Zero;
        }

        if (movementToDestination.Length() <= ShortRangeDirectMovementDistance)
        {
            ResetNavigationPathState();
            if (NavigationAgent != null && NavigationAgent.IsInsideTree())
                NavigationAgent.TargetPosition = GlobalPosition;
            return movementToDestination;
        }

        var agentInsideTree = NavigationAgent != null && NavigationAgent.IsInsideTree();
        if (!agentInsideTree)
        {
            ResetNavigationPathState();
            return movementToDestination;
        }

        if (!NavigationAgent.GetNavigationMap().IsValid)
        {
            ResetNavigationPathState();
            return movementToDestination;
        }

        if (ShouldRefreshNavigationTarget(desiredDestination))
            RefreshNavigationTarget(desiredDestination);

        var nextPathPosition = NavigationAgent.GetNextPathPosition();
        IsUsingNavigationPath = true;
        LastNavigationPathPosition = nextPathPosition;

        var movementToPath = nextPathPosition - GlobalPosition;
        if (movementToPath == Vector2.Zero)
            movementToPath = movementToDestination;

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

    private void SpawnCorpse()
    {
        if (AnimatedSprite?.SpriteFrames == null)
            return;

        var parent = GetParent();
        var corpseScene = ResolveCorpseScene();
        if (parent == null || corpseScene == null)
            return;

        var corpse = corpseScene.Instantiate<Corpse>();
        if (corpse == null)
            return;

        parent.AddChild(corpse);
        corpse.Initialize(
            AnimatedSprite.SpriteFrames,
            DeathAnimation,
            LastDirection,
            GlobalPosition,
            AnimatedSprite.Position,
            AnimatedSprite.Scale,
            AnimatedSprite.FlipH,
            AnimatedSprite.FlipV,
            ZIndex);
    }

    private static PackedScene ResolveCorpseScene()
    {
        _corpseScene ??= GD.Load<PackedScene>(DefaultCorpseScenePath);
        return _corpseScene;
    }

}
