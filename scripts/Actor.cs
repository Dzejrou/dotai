using Godot;

using System;
using System.Collections.Generic;

public abstract partial class Actor : CombatCharacter
{
    private const string DefaultCorpseScenePath = "res://scenes/world/corpse.tscn";
    private const string BehaviorNodePhasePath = "Behaviors/Tier05_Phase";
    private const string BehaviorNodeTargetingPath = "Behaviors/Tier10_Targeting";
    private const string BehaviorNodeCombatPath = "Behaviors/Tier50_Combat";
    private const string BehaviorNodeReturnHomePath = "Behaviors/Tier80_ReturnHome";
    private const string BehaviorNodeRecoveryPath = "Behaviors/Tier90_Recovery";
    private const string PrimaryActionControllerPath = "PrimaryActionController";
    private const float NavigationTargetUpdateThreshold = 8.0f;
    private const float DefaultPathDesiredDistance = 6.0f;
    private const float DefaultTargetDesiredDistance = 8.0f;
    private const float ShortRangeDirectMovementDistance = 24.0f;

    [Export]
    public StringName DeathAnimation { get; set; } = "death";

    [Export]
    public float HomeReturnTolerance { get; set; } = 4.0f;

    [Export(PropertyHint.Range, "0,64,0.5")]
    public float DropSpreadDistanceMin { get; set; } = 6.0f;

    [Export(PropertyHint.Range, "0,64,0.5")]
    public float DropSpreadDistanceMax { get; set; } = 12.0f;

    public NavigationAgent2D NavigationAgent { get; private set; }
    public Node2D Target => Combat.Target;
    public bool IsUsingNavigationPath { get; private set; }
    public Vector2 LastNavigationPathPosition { get; private set; }
    public float MovementSpeed { get; private set; } = 1.0f;
    public Vector2 HomePosition { get; private set; }
    public ICombatActionController PrimaryActionController { get; private set; }

    [Export]
    public CombatUnitState CurrentState { get; private set; } = CombatUnitState.Idle;

    private readonly List<IActorBehavior> _behaviors = new();
    private readonly List<IActorTickBehavior> _tickBehaviors = new();
    private readonly List<IActorDamageInterceptor> _damageInterceptors = new();
    private bool _hasNavigationDestination;
    private Vector2 _lastNavigationDestination;
    private ActorHUD _actorHud;
    private bool _subscribedToNavigationDebug;
    private bool _animationFinishedConnected;
    private bool _statusEffectsBound;
    private static PackedScene _corpseScene;
    private static readonly RandomNumberGenerator LootRandom = CreateLootRandom();

    [Export]
    public LootTable LootTable { get; set; }

    [Export]
    public bool RollGlobalGearLoot { get; set; } = true;

    [Export]
    public ActorRank Rank { get; set; } = ActorRank.Normal;

    private ActorLevelScalingRules _cachedLevelScalingRules;

    protected void InitializeActor(
        OmniSprite omniSprite,
        NavigationAgent2D navigationAgent = null)
    {
        SetOmniSprite(omniSprite);
        NavigationAgent = navigationAgent;

        if (NavigationAgent != null)
        {
            NavigationAgent.PathDesiredDistance = DefaultPathDesiredDistance;
            NavigationAgent.TargetDesiredDistance = DefaultTargetDesiredDistance;
        }

        AddToGroup(CombatGroups.Actors);
        HomePosition = GlobalPosition;
        InitializeCombatCharacter();
        ResetCombatState();
        var statusEffectController = GetNodeOrNull<StatusEffectController>("StatusEffectController");
        SetStatusEffectController(statusEffectController);
        if (statusEffectController == null)
            GD.PushError($"{GetPath()}: missing required StatusEffectController child.");
        var scenePrimaryActionController = GetNodeOrNull<Node>(PrimaryActionControllerPath);
        if (scenePrimaryActionController != null)
        {
            if (scenePrimaryActionController is not ICombatActionController actionController)
                GD.PushError($"{GetPath()}: PrimaryActionController must implement ICombatActionController.");
            else
                PrimaryActionController = actionController;
        }
        _actorHud = GetNodeOrNull<ActorHUD>("ActorHUD");
        if (_actorHud == null)
            GD.PushError($"{GetPath()}: missing required ActorHUD child.");
        else
            _actorHud.Bind(this);
        OnHealthStateChanged();
        EnsureTreeLifetimeConnections();
    }

    public override void _EnterTree()
    {
        base._EnterTree();
        EnsureTreeLifetimeConnections();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDead)
        {
            Velocity = Vector2.Zero;
            return;
        }

        Combat.Update(delta);

        PrimaryActionController?.Update(this, delta);
        foreach (var tickBehavior in _tickBehaviors)
            tickBehavior.Update(this, delta);

        if (!IsStructurallyValidTarget(Target))
            ClearTarget();

        // Suppress behavior and movement while an action-owned operation is in
        // flight (attack swing, or a timer-driven cast/release). The controller's
        // Update already ran above, so cast timers keep advancing while suppressed.
        if (CurrentState == CombatUnitState.Attacking || (PrimaryActionController?.IsBusy ?? false))
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
        DisconnectTreeLifetimeConnections();
        OnActorExitTree();
        base._ExitTree();
    }

    public void SetTarget(Node2D target)
    {
        Combat.SetTarget(target);
    }

    public void ClearTarget()
    {
        Combat.ClearTarget();
    }

    public void SetState(CombatUnitState state)
    {
        CurrentState = state;
    }

    // Returns the actor to normal AI after an action-owned operation completes.
    // Covers melee/ranged swings (Attacking) and enemy casts (Casting/Channeling).
    public void FinishAttackState()
    {
        if (CurrentState != CombatUnitState.Attacking &&
            CurrentState != CombatUnitState.Casting &&
            CurrentState != CombatUnitState.Channeling)
            return;

        SetState(Target != null ? CombatUnitState.PursuingTarget : CombatUnitState.Idle);
    }

    public bool IsAtHome()
    {
        return GlobalPosition.DistanceTo(HomePosition) <= Math.Max(0.0f, HomeReturnTolerance);
    }

    public bool IsHostileTo(Node target)
    {
        return FactionState.IsHostileTo(target);
    }

    public bool IsFriendlyTo(Node target)
    {
        return FactionState.IsFriendlyTo(target);
    }

    public override void ApplyHealing(Healing healing)
    {
        var amount = healing?.Amount ?? 0;
        if (amount <= 0 || IsDead)
            return;

        var healedAmount = HealthStateNode.ApplyHealing(amount);
        if (healedAmount <= 0)
            return;

        ShowFloatingHealingNumber(healedAmount);
        CombatLog.Heal(this, healedAmount);
    }

    public bool TryMoveTowardDestination(Vector2 destinationPosition, float speedMultiplier, CombatUnitState movingState, double delta)
    {
        var movement = ResolveMovementDirection(destinationPosition, delta);
        if (movement == Vector2.Zero)
            return false;

        SetState(movingState);

        var normalizedMovement = movement.Normalized();
        SetFacingDirection(normalizedMovement);
        SetAnimationSafe(GetWalkAnimationName());

        Velocity = normalizedMovement * MovementSpeed * Math.Max(0.0f, speedMultiplier) * Math.Max(0.0f, MovementSpeedMultiplier);
        return true;
    }

    public void ShowFloatingDamageNumber(string text, Color color)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        FloatingText.ShowCustom(text, this, color);
    }

    public static bool IsStructurallyValidTarget(Node2D target)
    {
        return target != null && GodotObject.IsInstanceValid(target) && target.IsInsideTree();
    }

    public bool CanReachTarget(Node2D target)
    {
        if (!IsStructurallyValidTarget(target))
            return false;

        return CanReachDestination(target.GlobalPosition);
    }

    public void ResetHomePositionToCurrentPosition()
    {
        HomePosition = GlobalPosition;
        ResetNavigationPathState();
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

        // Highest-priority tier: boss phase/transition control runs ahead of
        // targeting and combat so it can take over the actor during a transition.
        // Most actors have no such tier node; collection is then a no-op.
        AppendBehaviorNodes(GetNodeOrNull<Node>(BehaviorNodePhasePath));
        AppendBehaviorNodes(GetNodeOrNull<Node>(BehaviorNodeTargetingPath));

        foreach (var behavior in behaviors)
            AppendBehavior(behavior);

        AppendBehaviorNodes(GetNodeOrNull<Node>(BehaviorNodeCombatPath));
        AppendBehaviorNodes(GetNodeOrNull<Node>(BehaviorNodeReturnHomePath));
        AppendBehaviorNodes(GetNodeOrNull<Node>(BehaviorNodeRecoveryPath));
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
        HealthStateNode.SetDead(value);
        if (value)
        {
            CleanupNavigationForInactiveState();
            ResetCombatState();
            // Cancel any in-flight action so a pending cast cannot execute after
            // the caster dies (no delayed Ring of Fire after the Demon is killed).
            ResetPrimaryActionController();
        }
    }

    protected void ResetPrimaryActionController()
    {
        PrimaryActionController?.Cancel(this);
    }

    // Reset/restoration (e.g. timed-room restart) must cancel any in-flight cast
    // and drop the actor out of an action state so it cannot resume a stale cast.
    public override void RestoreCombatState(bool clearStatusEffects = true)
    {
        base.RestoreCombatState(clearStatusEffects);
        ResetPrimaryActionController();
        if (CurrentState is CombatUnitState.Attacking or CombatUnitState.Casting or CombatUnitState.Channeling)
            SetState(CombatUnitState.Idle);
    }

    protected void PrepareForRemoval()
    {
        CleanupNavigationForInactiveState();
    }

    protected override void OnHealthStateChanged()
    {
        RefreshHealthLabel();
    }

    protected override void OnLevelChanged(int newLevel)
    {
        // Spawners assign Level before the actor enters the tree; skip until HealthState exists.
        if (HealthStateNode == null)
            return;

        // SetMax clamps current health to the new max; it never grants a free heal.
        HealthStateNode.SetMax(ResolvedMaxHealth);
        RefreshHealthLabel();
    }

    protected override int ResolveScaledBaseMaxHealth(int baseMaxHealth)
    {
        var rules = ResolveLevelScalingRules();
        return rules == null ? baseMaxHealth : rules.ScaleMaxHealth(baseMaxHealth, Level, Rank);
    }

    protected override float ResolveScaledBasePower(float basePower)
    {
        var rules = ResolveLevelScalingRules();
        return rules == null ? basePower : rules.ScalePower(basePower, Level, Rank);
    }

    private ActorLevelScalingRules ResolveLevelScalingRules()
    {
        if (_cachedLevelScalingRules != null)
            return _cachedLevelScalingRules;

        _cachedLevelScalingRules = FindWorld()?.ActorLevelScalingRules;
        return _cachedLevelScalingRules;
    }

    protected void SpawnCorpseAndFree()
    {
        PrepareForRemoval();
        Velocity = Vector2.Zero;
        ResetPrimaryActionController();
        SpawnLootDrops();
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
        if (amount <= 0)
            return;

        FloatingText.ShowGood($"+{amount}", this);
    }

    protected bool TryApplyIncomingDamage(Damage damageInfo, out int damage, out bool died)
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

            if (decision.ReportAbsorbed)
                ReportAbsorbedHit(decision.AbsorbedAmount);

            if (decision.RetargetTo != null && IsStructurallyValidTarget(decision.RetargetTo))
                SetTarget(decision.RetargetTo);

            if (!decision.AllowDamage)
                return false;

            break;
        }

        if (!TryApplyDamageToHealth(damageInfo, setReceiverTargetToSource: false, out damage))
            return false;

        died = HealthStateNode.IsDead;
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

    private static ActorExperienceRewardRules _fallbackExperienceRewardRules;

    protected void TryGrantExperienceToKiller(Damage damageInfo)
    {
        if (damageInfo?.Source is not Player player || !GodotObject.IsInstanceValid(player))
            return;

        var reward = ResolveExperienceReward(player);
        if (reward > 0)
            player.AddExperience(reward);
    }

    private int ResolveExperienceReward(Player player)
    {
        var rules = FindWorld()?.ActorExperienceRewardRules ?? GetFallbackExperienceRewardRules();
        var playerLevel = Math.Max(1, player.Level);
        var enemyLevel = Math.Max(1, Level);

        var sameLevelKills = rules.GetSameLevelKills(playerLevel);
        var requiredXp = player.GetRequiredExperienceForRewardLevel(enemyLevel);
        var baseXp = requiredXp / sameLevelKills;
        var finalXp = baseXp * rules.GetRankMultiplier(Rank) * rules.GetLevelDifferenceMultiplier(enemyLevel - playerLevel);

        if (finalXp <= 0.0f)
            return 0;

        var rounded = Mathf.RoundToInt(finalXp);
        return rounded < 1 ? 1 : rounded;
    }

    private static ActorExperienceRewardRules GetFallbackExperienceRewardRules()
    {
        return _fallbackExperienceRewardRules ??= new ActorExperienceRewardRules();
    }

    protected virtual void OnActorExitTree() { }

    private void EnsureTreeLifetimeConnections()
    {
        EnsureAnimationFinishedConnected();
        EnsureNavigationDebugSubscribed();
        EnsureStatusEffectsBound();
    }

    private void DisconnectTreeLifetimeConnections()
    {
        DisconnectAnimationFinished();
        UnsubscribeFromNavigationDebug();
        UnbindStatusEffects();
    }

    private void EnsureAnimationFinishedConnected()
    {
        if (_animationFinishedConnected || OmniSprite == null)
            return;

        OmniSprite.AnimationFinished += OnAnimatedSpriteAnimationFinished;
        _animationFinishedConnected = true;
    }

    private void DisconnectAnimationFinished()
    {
        if (!_animationFinishedConnected || OmniSprite == null)
            return;

        OmniSprite.AnimationFinished -= OnAnimatedSpriteAnimationFinished;
        _animationFinishedConnected = false;
    }

    private void EnsureNavigationDebugSubscribed()
    {
        if (NavigationAgent == null)
            return;

        ApplyNavigationDebugState(NavigationDebugSettings.Enabled);
        SubscribeToNavigationDebug();
    }

    private void BindStatusEffects()
    {
        if (StatusEffectControllerNode == null)
            return;

        StatusEffectControllerNode.Connect(
            StatusEffectController.SignalName.StatusVisualStateChanged,
            new Callable(this, nameof(OnStatusVisualStateChanged)));

        StatusEffectControllerNode.Connect(
            StatusEffectController.SignalName.StatusFloatingTextRequested,
            new Callable(this, nameof(OnStatusFloatingTextRequested)));

        foreach (var effect in StatusEffectControllerNode.GetActiveStatusEffects())
            OnStatusVisualStateChanged(effect.StatusKey, effect, true);
    }

    private void EnsureStatusEffectsBound()
    {
        if (_statusEffectsBound || StatusEffectControllerNode == null)
            return;

        BindStatusEffects();
        _statusEffectsBound = true;
    }

    private void UnbindStatusEffects()
    {
        if (!_statusEffectsBound || StatusEffectControllerNode == null || !GodotObject.IsInstanceValid(StatusEffectControllerNode))
            return;

        var callable = new Callable(this, nameof(OnStatusVisualStateChanged));
        if (StatusEffectControllerNode.IsConnected(StatusEffectController.SignalName.StatusVisualStateChanged, callable))
            StatusEffectControllerNode.Disconnect(StatusEffectController.SignalName.StatusVisualStateChanged, callable);

        var textCallable = new Callable(this, nameof(OnStatusFloatingTextRequested));
        if (StatusEffectControllerNode.IsConnected(StatusEffectController.SignalName.StatusFloatingTextRequested, textCallable))
            StatusEffectControllerNode.Disconnect(StatusEffectController.SignalName.StatusFloatingTextRequested, textCallable);

        _statusEffectsBound = false;
    }

    private void OnStatusVisualStateChanged(StringName statusKey, StatusEffect effect, bool active)
    {
        if (statusKey == PoisonedEffect.StatusKeyName)
            _actorHud?.SetPoisoned(active);

        OmniSprite?.ReflectStatusEffect(effect, active);
    }

    private void OnStatusFloatingTextRequested(string text, Color color)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        FloatingText.ShowCustom(text, this, color);
    }

    private void OnAnimatedSpriteAnimationFinished()
    {
        var animationName = OmniSprite?.CurrentAnimation ?? default;
        if (animationName.IsEmpty)
            return;

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

    private void ExecuteIntent(ActorIntent intent, double delta)
    {
        if (intent.FacingDirection.HasValue && intent.FacingDirection.Value != Vector2.Zero)
            SetFacingDirection(intent.FacingDirection.Value);

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
            if (!CanMove)
            {
                SetState(intent.State);
                ResetNavigationPathState();
                Velocity = Vector2.Zero;
                PlayIdleIfAvailable();
                return;
            }

            if (TryMoveTowardDestination(intent.Destination.Value, intent.SpeedMultiplier, intent.State, delta))
            {
                MoveAndSlide();
                return;
            }
        }

        StopAndIdle();
    }

    private void SpawnLootDrops()
    {
        if (LootTable == null)
            return;

        var dropParent = GetParent();
        if (dropParent == null)
            return;

        var rolledEntries = LootTable.Roll(LootRandom);
        foreach (var entry in rolledEntries)
        {
            if (entry == null)
                continue;

            var drop = entry.CreateDropInstance();
            SpawnDrop(dropParent, drop);
        }

        if (RollGlobalGearLoot)
            SpawnGlobalGearLootDrops(dropParent, Rank);
    }

    private void SpawnGlobalGearLootDrops(Node dropParent, ActorRank rank)
    {
        CombatLog.Debug($"Global gear loot: {CombatLog.ResolveName(this)} (lvl {Level}, rank {rank}) eval start.");

        var world = FindWorld();
        var rules = world?.GlobalGearLootRules;
        var gearRules = world?.GearGenerationRules;
        var dropScene = world?.GearDropScene;

        if (rules == null)
        {
            CombatLog.Debug("Global gear loot: skipped (missing GlobalGearLootRules).");
            return;
        }
        if (gearRules == null)
        {
            CombatLog.Debug("Global gear loot: skipped (missing GearGenerationRules).");
            return;
        }
        if (dropScene == null)
        {
            CombatLog.Debug("Global gear loot: skipped (missing GearDropScene).");
            return;
        }

        var baseRollCount = Math.Max(1, rules.RollCount);
        var rankBonus = rules.GetRankRollBonus(rank);
        var rollCount = baseRollCount + rankBonus;
        CombatLog.Debug($"Global gear loot: rank {rank} rolls = {baseRollCount} base + {rankBonus} rank bonus = {rollCount} effective.");
        for (var i = 0; i < rollCount; i++)
        {
            var rolled = rules.TryRollGear(Level, LootRandom, gearRules, out var gear, out var rollResult);
            if (!rolled || gear == null)
            {
                CombatLog.Debug($"Global gear loot: roll {i + 1}/{rollCount} failed ({FormatGlobalGearLootFailureReason(rollResult.FailureReason)}).");
                continue;
            }

            CombatLog.Debug($"Global gear loot: roll {i + 1}/{rollCount} success (lvl {Level}, rank {rank}, {rollResult.Quality} {rollResult.Slot}, \"{gear.Definition.DisplayName}\").");

            if (dropScene.Instantiate() is not InventoryItemDrop gearDrop)
                continue;

            gearDrop.ItemDefinition = gear.Definition;
            gearDrop.GearInstance = gear;
            gearDrop.Quantity = 1;
            gearDrop.PickupMode = DropPickupMode.InteractOnly;
            SpawnDrop(dropParent, gearDrop);
        }
    }

    private static string FormatGlobalGearLootFailureReason(GlobalGearLootRollFailureReason reason)
    {
        return reason switch
        {
            GlobalGearLootRollFailureReason.MissingConfig => "missing config",
            GlobalGearLootRollFailureReason.ChanceMiss => "chance miss",
            GlobalGearLootRollFailureReason.NoLevelBand => "no level band",
            GlobalGearLootRollFailureReason.NoQualityWeight => "no quality weight",
            GlobalGearLootRollFailureReason.GenerationFailure => "generation failure",
            _ => "unknown",
        };
    }

    private void SpawnDrop(Node dropParent, Drop drop)
    {
        if (drop == null)
            return;

        if (dropParent is Node2D node2DParent)
        {
            var spawnStartPosition = node2DParent.ToLocal(GlobalPosition);
            var spawnTargetPosition = node2DParent.ToLocal(GlobalPosition + ResolveDropSpawnOffset());
            drop.ConfigureSpawnMotion(spawnStartPosition, spawnTargetPosition);
        }

        dropParent.CallDeferred(Node.MethodName.AddChild, drop);
    }

    private World FindWorld()
    {
        var current = GetParent();
        while (current != null)
        {
            if (current is World world)
                return world;

            current = current.GetParent();
        }

        return null;
    }

    private static RandomNumberGenerator CreateLootRandom()
    {
        var random = new RandomNumberGenerator();
        random.Randomize();
        return random;
    }

    private Vector2 ResolveDropSpawnOffset()
    {
        var angle = LootRandom.RandfRange(0.0f, Mathf.Tau);
        var minDistance = Mathf.Max(0.0f, DropSpreadDistanceMin);
        var maxDistance = Mathf.Max(minDistance, DropSpreadDistanceMax);
        var distance = LootRandom.RandfRange(minDistance, maxDistance);
        return Vector2.Right.Rotated(angle) * distance;
    }

    private void StopAndIdle()
    {
        ResetNavigationPathState();
        SetState(CombatUnitState.Idle);
        Velocity = Vector2.Zero;
        PlayIdleIfAvailable();
    }

    private bool CanReachDestination(Vector2 destination)
    {
        var movementToDestination = destination - GlobalPosition;
        if (movementToDestination == Vector2.Zero || movementToDestination.Length() <= ShortRangeDirectMovementDistance)
            return true;

        if (NavigationAgent == null || !NavigationAgent.IsInsideTree())
            return true;

        var navigationMap = NavigationAgent.GetNavigationMap();
        if (!navigationMap.IsValid)
            return true;

        var path = NavigationServer2D.MapGetPath(navigationMap, GlobalPosition, destination, true);
        if (path.Length == 0)
            return false;

        return path[path.Length - 1].DistanceTo(destination) <= DefaultTargetDesiredDistance;
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
        var animatedSprite = OmniSprite?.AnimatedSprite;
        if (animatedSprite?.SpriteFrames == null)
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
            animatedSprite.SpriteFrames,
            DeathAnimation,
            LastDirection,
            GlobalPosition,
            animatedSprite.Position,
            animatedSprite.Scale,
            animatedSprite.FlipH,
            animatedSprite.FlipV,
            ZIndex);
        (parent as World)?.RegisterCorpse(corpse);
    }

    private static PackedScene ResolveCorpseScene()
    {
        _corpseScene ??= GD.Load<PackedScene>(DefaultCorpseScenePath);
        return _corpseScene;
    }

}
