using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class Player : CombatCharacter, IAttackable, ITargetable, ISpellCaster
{
    [Signal]
    public delegate void PlayerDiedEventHandler();

    [Signal]
    public delegate void HealthChangedEventHandler(int health, int maxHealth);

    [Signal]
    public delegate void ManaChangedEventHandler(int mana, int maxMana);

    [Signal]
    public delegate void InteractionAvailabilityChangedEventHandler(bool available, string label);

    [Export]
    public float Speed { get; set; } = 140.0f;

    [Export]
    public float AttackRange { get; set; } = 28.0f;

    [Export]
    public float AttackCooldown { get; set; } = 0.5f;

    [Export]
    public float AttackArcDegrees { get; set; } = 70.0f;

    [Export]
    public int MaxAttackDamage { get; set; } = 5;

    [Export]
    public int MinAttackDamage { get; set; } = 2;

    [Export]
    public float HealthRegenerationInterval { get; set; } = 5.0f;

    [Export]
    public int HealthRegenerationAmount { get; set; } = 1;

    [Export]
    public float HealthRegenerationDelayAfterDamage { get; set; } = 5.0f;

    [Export]
    public float SoftTargetRange { get; set; } = 180.0f;

    [Export]
    public float TabTargetRange { get; set; } = 220.0f;

    [Export]
    public float InteractionRange { get; set; } = 108.0f;

    private bool _isDead;
    private readonly RandomNumberGenerator _random = new();
    private readonly HashSet<Node> _hitThisAttack = new();
    private readonly Dictionary<StringName, Spell> _spellsByAction = new();
    private IPlacementSpell _pendingPlacementSpell;
    private float _attackCooldownTimer;
    private bool _isAttacking;
    private float _healthRegenTimer;
    private float _healthRegenDelayTimer;
    private IInteractable _activeInteractable;
    private Node2D _activeInteractableNode;
    private string _activeInteractableLabel = string.Empty;
    private ActorHUD _activeTargetHud;
    private readonly HashSet<ActorHUD> _visibleTargetHuds = new();
    private readonly HashSet<ActorHUD> _nextVisibleTargetHuds = new();

    public bool CanBeTargeted => !_isDead;
    public PlayerTargetingState Targeting { get; } = new();
    public Node2D SpellOrigin => this;
    public string SpellDirectionName => LastDirection;
    public Vector2 SpellDirection => GetSpellDirection();
    public Node2D SpellTarget => Targeting.ActiveTarget;
    public Spell ArmedPlacementSpell => _pendingPlacementSpell as Spell;
    public bool CanCastSpells => !_isDead;
    public bool HasInteractionTarget => _activeInteractable != null;
    public string CurrentInteractionLabel => _activeInteractableLabel;

    public override void _Ready()
    {
        SetAnimatedSprite(GetNode<AnimatedSprite2D>("AnimatedSprite2D"));
        InitializeCombatCharacter(requireManaState: true);
        LoadEquippedSpells();
        SetAnimationSafe(GetIdleAnimationName());
        AnimatedSprite.AnimationFinished += OnAnimationFinished;
        AddToGroup(CombatGroups.Actors);

        EmitHealthChanged();
        NotifyManaChanged();
        UpdateInteractionState();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_isDead)
            return;

        Combat.Update(delta);
        if (!InCombat && ManaState.Tick(delta) > 0)
            NotifyManaChanged();

        HandleHealthRegenerationDelay((float)delta);
        if (InCombat)
            _healthRegenTimer = Math.Max(HealthRegenerationInterval, 0.0f);
        else
            HandleHealthRegeneration((float)delta);
        if (Input.IsActionJustPressed("tab_target"))
            CycleTabTarget(1);
        if (Input.IsActionJustPressed("tab_target_reverse"))
            CycleTabTarget(-1);
        if (Input.IsActionJustPressed("clear_tab_target"))
            ClearTabTarget();
        UpdateInteractionState();
        if (Input.IsActionJustPressed("interact_action"))
            TryInteract();
        TryCastEquippedSpells();
        var direction = GetInputDirection();

        if (_isAttacking)
        {
            UpdateTargetingState();
            Velocity = Vector2.Zero;
            ApplyAttackDamage();
            return;
        }

        if (_attackCooldownTimer > 0.0f)
            _attackCooldownTimer -= (float)delta;

        if (Input.IsActionPressed("attack") && _attackCooldownTimer <= 0.0f)
        {
            if (direction != Vector2.Zero)
                SetFacingDirection(direction);

            UpdateTargetingState();
            StartAttack();
            return;
        }

        if (direction == Vector2.Zero)
        {
            UpdateTargetingState();
            Velocity = Vector2.Zero;
            SetAnimationSafe(GetIdleAnimationName());
            return;
        }

        direction = direction.Normalized();
        SetFacingDirection(direction);
        var isSprinting = Input.IsActionPressed("sprint");
        var moveSpeed = isSprinting ? Speed * 2.0f : Speed;
        Velocity = direction * moveSpeed;
        MoveAndSlide();
        UpdateTargetingState();

        SetAnimationSafe(GetWalkAnimationName());
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_isDead || _pendingPlacementSpell == null || @event is not InputEventMouseButton mouseButton || !mouseButton.Pressed)
            return;

        if (mouseButton.ButtonIndex == MouseButton.Right)
        {
            ClearPendingPlacementSpell();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (mouseButton.ButtonIndex != MouseButton.Left)
            return;

        _pendingPlacementSpell.TryPlace(this, GetGlobalMousePosition());
        if (!_pendingPlacementSpell.IsAwaitingPlacement)
            _pendingPlacementSpell = null;

        GetViewport().SetInputAsHandled();
    }

    private void StartAttack()
    {
        if (_isAttacking || _attackCooldownTimer > 0.0f)
            return;

        _isAttacking = true;
        _attackCooldownTimer = AttackCooldown;
        _hitThisAttack.Clear();

        var attackAnimation = ResolveDirectionalAnimationName("attack");
        if (attackAnimation == null)
        {
            ApplyAttackDamage();
            _isAttacking = false;
            return;
        }

        AnimatedSprite.Play(attackAnimation, customSpeed: 6.0f);
        ApplyAttackDamage();
    }

    private void OnAnimationFinished()
    {
        if (AnimatedSprite.Animation.ToString().StartsWith("attack_", StringComparison.Ordinal))
            _isAttacking = false;
    }

    public void ApplyDamage(DamageInfo damageInfo)
    {
        if (_isDead)
            return;

        var damage = HealthStateNode.ApplyDamage(damageInfo.Amount);
        damageInfo.RegisterHit(this, setReceiverTargetToSource: true);

        ShowFloatingDamageNumber(damage);
        EmitHealthChanged();
        _healthRegenDelayTimer = Math.Max(HealthRegenerationDelayAfterDamage, 0.0f);

        if (HealthStateNode.IsDead)
        {
            _isDead = true;
            ResetCombatState();
            Targeting.ClearAllTargets();
            ClearPendingPlacementSpell();
            UpdateTargetHudVisibility();
            EmitSignal(SignalName.PlayerDied);
            QueueFree();
        }
    }

    public override void ApplyHealing(int amount)
    {
        if (_isDead || amount <= 0)
            return;

        var recovered = HealthStateNode.ApplyHealing(amount);
        if (recovered <= 0)
            return;

        ShowFloatingHealingNumber(recovered);
        EmitHealthChanged();
        _healthRegenTimer = Math.Max(HealthRegenerationInterval, 0.0f);
    }

    private void UpdateTargetingState()
    {
        ValidateTabTarget();
        UpdateSoftTarget();
        UpdateTargetHudVisibility();
    }

    private void UpdateInteractionState()
    {
        var currentIsValid = IsValidInteractionTarget(_activeInteractableNode, _activeInteractable);
        var nextInteractable = currentIsValid ? _activeInteractable : FindClosestInteractable();
        var nextInteractableNode = nextInteractable as Node2D;
        var nextLabel = ResolveInteractionLabel(nextInteractable);

        if (ReferenceEquals(nextInteractable, _activeInteractable) &&
            ReferenceEquals(nextInteractableNode, _activeInteractableNode) &&
            nextLabel == _activeInteractableLabel)
        {
            return;
        }

        _activeInteractable = nextInteractable;
        _activeInteractableNode = nextInteractableNode;
        _activeInteractableLabel = nextLabel;
        EmitSignal(SignalName.InteractionAvailabilityChanged, nextInteractable != null, nextLabel);
    }

    private IInteractable FindClosestInteractable()
    {
        var interactionRange = Math.Max(0.0f, InteractionRange);
        if (interactionRange <= 0.0f || !IsInsideTree() || GetTree() == null)
            return null;

        IInteractable closestInteractable = null;
        var closestDistance = float.MaxValue;

        foreach (var node in GetTree().GetNodesInGroup(InteractionGroups.Interactables))
        {
            if (!IsValidInteractableCandidate(node, out var targetNode, out var interactable))
                continue;

            var distance = GlobalPosition.DistanceTo(targetNode.GlobalPosition);
            if (distance > interactionRange || distance >= closestDistance)
                continue;

            closestDistance = distance;
            closestInteractable = interactable;
        }

        return closestInteractable;
    }

    private bool IsValidInteractableCandidate(Node node, out Node2D targetNode, out IInteractable interactable)
    {
        targetNode = null;
        interactable = null;

        if (!IsInstanceValid(node) || node is not Node2D node2D || !node2D.IsInsideTree())
            return false;

        if (node is not IInteractable interactableNode || !interactableNode.CanInteract(this))
            return false;

        targetNode = node2D;
        interactable = interactableNode;
        return true;
    }

    private bool IsValidInteractionTarget(Node2D targetNode, IInteractable interactable)
    {
        if (targetNode == null || interactable == null)
            return false;

        if (!IsInstanceValid(targetNode) || !targetNode.IsInsideTree())
            return false;

        if (!interactable.CanInteract(this))
            return false;

        return GlobalPosition.DistanceTo(targetNode.GlobalPosition) <= Math.Max(0.0f, InteractionRange);
    }

    private string ResolveInteractionLabel(IInteractable interactable)
    {
        if (interactable == null)
            return string.Empty;

        var label = interactable.GetInteractionLabel(this);
        return string.IsNullOrWhiteSpace(label) ? "Interact" : label.Trim();
    }

    public bool TryInteract()
    {
        if (!IsValidInteractionTarget(_activeInteractableNode, _activeInteractable))
        {
            UpdateInteractionState();
            if (!IsValidInteractionTarget(_activeInteractableNode, _activeInteractable))
                return false;
        }

        _activeInteractable.Interact(this);
        UpdateInteractionState();
        return true;
    }

    private void UpdateSoftTarget()
    {
        var softTargetRange = Math.Max(0.0f, SoftTargetRange);
        if (softTargetRange <= 0.0f)
        {
            Targeting.ClearSoftTarget();
            return;
        }

        var facingDirection = DirectionHelper.GetDirectionVector(LastDirection);
        if (facingDirection == Vector2.Zero)
            facingDirection = Vector2.Down;

        Node2D bestTarget = null;
        var bestScore = float.NegativeInfinity;

        foreach (var node in TargetingHelper.EnumerateCandidateTargets(this))
        {
            if (!IsValidPlayerTargetCandidate(node, out var targetNode))
                continue;

            var toTarget = targetNode.GlobalPosition - GlobalPosition;
            var distance = toTarget.Length();
            if (distance <= 0.0f || distance > softTargetRange)
                continue;

            var alignment = facingDirection.Dot(toTarget.Normalized());
            var distanceScore = 1.0f - (distance / softTargetRange);
            var alignmentScore = (alignment + 1.0f) * 0.5f;
            var score = (alignmentScore * 0.65f) + (distanceScore * 0.35f);

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestTarget = targetNode;
        }

        if (bestTarget != null)
            Targeting.SetSoftTarget(bestTarget);
        else
            Targeting.ClearSoftTarget();
    }

    private bool IsValidPlayerTargetCandidate(Node node, out Node2D targetNode)
    {
        targetNode = null;

        if (!IsInstanceValid(node) || node is not Node2D node2D || !node2D.IsInsideTree())
            return false;

        if (node is not IAttackable || node is not ITargetable targetable || !targetable.CanBeTargeted)
            return false;

        if (!TargetingHelper.CanBeExplicitlyTargetedByFaction(Faction, node2D))
            return false;

        targetNode = node2D;
        return true;
    }

    private void ClearTabTarget()
    {
        Targeting.ClearTabTarget();
        UpdateTargetHudVisibility();
    }

    private void CycleTabTarget(int direction)
    {
        var candidates = GetTabTargetCandidates();
        if (candidates.Count == 0)
        {
            ClearTabTarget();
            return;
        }

        var currentTabTarget = Targeting.TabTarget;
        var currentIndex = candidates.IndexOf(currentTabTarget);
        if (currentIndex < 0)
        {
            var initialIndex = direction < 0 ? candidates.Count - 1 : 0;
            Targeting.SetTabTarget(candidates[initialIndex]);
            UpdateTargetHudVisibility();
            return;
        }

        if (candidates.Count == 1)
        {
            ClearTabTarget();
            return;
        }

        var step = direction < 0 ? -1 : 1;
        var nextIndex = (currentIndex + step + candidates.Count) % candidates.Count;
        Targeting.SetTabTarget(candidates[nextIndex]);
        UpdateTargetHudVisibility();
    }

    private void ValidateTabTarget()
    {
        if (IsValidTabTarget(Targeting.TabTarget))
            return;

        Targeting.ClearTabTarget();
    }

    private bool IsValidTabTarget(Node2D target)
    {
        if (target == null || !IsInstanceValid(target) || !target.IsInsideTree())
            return false;

        if (target is not IAttackable || target is not ITargetable targetable || !targetable.CanBeTargeted)
            return false;

        if (!TargetingHelper.CanBeExplicitlyTargetedByFaction(Faction, target))
            return false;

        return GlobalPosition.DistanceTo(target.GlobalPosition) <= Math.Max(0.0f, TabTargetRange);
    }

    private List<Node2D> GetTabTargetCandidates()
    {
        var candidates = new List<Node2D>();
        var tabTargetRange = Math.Max(0.0f, TabTargetRange);
        if (tabTargetRange <= 0.0f)
            return candidates;

        var facingDirection = DirectionHelper.GetDirectionVector(LastDirection);
        if (facingDirection == Vector2.Zero)
            facingDirection = Vector2.Down;

        foreach (var node in TargetingHelper.EnumerateCandidateTargets(this))
        {
            if (!IsValidPlayerTargetCandidate(node, out var targetNode))
                continue;

            var distance = GlobalPosition.DistanceTo(targetNode.GlobalPosition);
            if (distance > tabTargetRange)
                continue;

            candidates.Add(targetNode);
        }

        candidates.Sort((left, right) =>
        {
            var leftScore = GetTabTargetOrderingScore(left, facingDirection, tabTargetRange);
            var rightScore = GetTabTargetOrderingScore(right, facingDirection, tabTargetRange);
            var scoreComparison = rightScore.CompareTo(leftScore);
            if (scoreComparison != 0)
                return scoreComparison;

            var leftDistance = GlobalPosition.DistanceTo(left.GlobalPosition);
            var rightDistance = GlobalPosition.DistanceTo(right.GlobalPosition);
            var distanceComparison = leftDistance.CompareTo(rightDistance);
            if (distanceComparison != 0)
                return distanceComparison;

            return left.GetInstanceId().CompareTo(right.GetInstanceId());
        });

        return candidates;
    }

    private float GetTabTargetOrderingScore(Node2D target, Vector2 facingDirection, float tabTargetRange)
    {
        var toTarget = target.GlobalPosition - GlobalPosition;
        if (toTarget == Vector2.Zero)
            return 1.0f;

        var alignment = facingDirection.Dot(toTarget.Normalized());
        var distanceScore = 1.0f - Mathf.Clamp(toTarget.Length() / tabTargetRange, 0.0f, 1.0f);
        var alignmentScore = (alignment + 1.0f) * 0.5f;
        return (alignmentScore * 0.7f) + (distanceScore * 0.3f);
    }

    private void UpdateTargetHudVisibility()
    {
        _nextVisibleTargetHuds.Clear();
        TryAddTargetHud(Targeting.ActiveTarget, _nextVisibleTargetHuds);

        foreach (var hud in _visibleTargetHuds)
        {
            if (hud == null || !GodotObject.IsInstanceValid(hud) || _nextVisibleTargetHuds.Contains(hud))
                continue;

            hud.SetUnitFrameVisible(false);
        }

        foreach (var hud in _nextVisibleTargetHuds)
            hud.SetUnitFrameVisible(true);

        _visibleTargetHuds.Clear();
        foreach (var hud in _nextVisibleTargetHuds)
            _visibleTargetHuds.Add(hud);

        UpdateTargetBracket();
    }

    private void UpdateTargetBracket()
    {
        var activeTarget = Targeting.ActiveTarget;
        var nextTargetHud = ResolveActorHud(activeTarget);

        if (_activeTargetHud != null &&
            GodotObject.IsInstanceValid(_activeTargetHud) &&
            _activeTargetHud != nextTargetHud)
        {
            _activeTargetHud.SetTargetBracketVisible(false);
        }

        _activeTargetHud = nextTargetHud;
        if (_activeTargetHud != null)
            _activeTargetHud.SetTargetBracketVisible(true);
    }

    private static void TryAddTargetHud(Node2D actor, HashSet<ActorHUD> targetHuds)
    {
        var hud = ResolveActorHud(actor);
        if (hud != null)
            targetHuds.Add(hud);
    }

    private static ActorHUD ResolveActorHud(Node2D actor)
    {
        if (actor == null || !GodotObject.IsInstanceValid(actor) || !actor.IsInsideTree())
            return null;

        return actor.GetNodeOrNull<ActorHUD>("ActorHUD");
    }

    private void ApplyAttackDamage()
    {
        if (_isDead)
            return;

        var facingVector = DirectionHelper.GetDirectionVector(LastDirection);
        var minimumDot = Mathf.Cos(Mathf.DegToRad(AttackArcDegrees / 2.0f));

        foreach (var node in TargetingHelper.EnumerateCandidateTargets(this))
        {
            if (_hitThisAttack.Contains(node) || node is not IAttackable attackable || !IsValidPlayerTargetCandidate(node, out var enemyNode))
                continue;

            var toEnemy = enemyNode.GlobalPosition - GlobalPosition;
            if (toEnemy.Length() > AttackRange)
                continue;

            if (toEnemy == Vector2.Zero)
            {
                ApplyDamageToEnemy(node, attackable);
                continue;
            }

            if (facingVector.Dot(toEnemy.Normalized()) < minimumDot)
                continue;

            ApplyDamageToEnemy(node, attackable);
        }
    }

    private void ApplyDamageToEnemy(Node node, IAttackable enemy)
    {
        if (enemy == null || !_hitThisAttack.Add(node))
            return;

        var targetFactionState = FactionState.ResolveFor(node);
        if (targetFactionState == null || !targetFactionState.CanBeDamagedBy(FactionState))
            return;

        var maxDamage = Math.Max(MinAttackDamage, MaxAttackDamage);
        var damage = _random.RandiRange(Math.Min(MinAttackDamage, maxDamage), maxDamage);
        enemy.ApplyDamage(new DamageInfo(damage, this));
    }

    private void HandleHealthRegeneration(float delta)
    {
        if (CurrentHealth >= MaxHealableHealth)
        {
            _healthRegenTimer = Math.Max(HealthRegenerationInterval, 0.0f);
            return;
        }

        _healthRegenTimer -= delta;
        if (_healthRegenTimer > 0.0f)
            return;

        if (CurrentHealth < MaxHealableHealth)
        {
            var missingHealth = MaxHealableHealth - CurrentHealth;
            var recovered = Math.Clamp(HealthRegenerationAmount, 1, missingHealth);
            ShowFloatingHealingNumber(recovered);
            HealthStateNode.ApplyHealing(recovered);
            EmitHealthChanged();
        }

        var interval = Math.Max(HealthRegenerationInterval, 0.0f);
        if (interval == 0.0f)
            _healthRegenTimer = 0.0f;
        else
            _healthRegenTimer = interval;
    }

    private void HandleHealthRegenerationDelay(float delta)
    {
        if (_healthRegenDelayTimer <= 0.0f)
            return;

        _healthRegenDelayTimer -= delta;
        _healthRegenDelayTimer = Math.Max(0.0f, _healthRegenDelayTimer);
        _healthRegenTimer = Math.Max(HealthRegenerationInterval, 0.0f);
    }

    private void ShowFloatingDamageNumber(int amount)
    {
        FloatingNumberHelper.ShowFloatingNumber(this, amount.ToString(), new Color(1.0f, 0.0f, 0.0f, 1.0f));
    }

    private void ShowFloatingHealingNumber(int amount)
    {
        if (amount <= 0)
            return;

        FloatingNumberHelper.ShowFloatingNumber(this, $"+{amount}", new Color(0.0f, 1.0f, 0.0f, 1.0f));
    }

    private void EmitHealthChanged()
    {
        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealableHealth);
    }

    public void NotifyManaChanged()
    {
        EmitSignal(SignalName.ManaChanged, CurrentMana, MaxManaValue);
    }

    private Vector2 GetSpellDirection()
    {
        var inputDirection = GetInputDirection();
        if (inputDirection != Vector2.Zero)
            return inputDirection.Normalized();

        return DirectionHelper.GetDirectionVector(LastDirection);
    }

    private static Vector2 GetInputDirection()
    {
        var direction = Vector2.Zero;
        if (Input.IsActionPressed("move_left"))
            direction.X -= 1.0f;
        if (Input.IsActionPressed("move_right"))
            direction.X += 1.0f;
        if (Input.IsActionPressed("move_up"))
            direction.Y -= 1.0f;
        if (Input.IsActionPressed("move_down"))
            direction.Y += 1.0f;

        return direction;
    }

    private void TryCastEquippedSpells()
    {
        foreach (var pair in _spellsByAction)
        {
            if (!Input.IsActionJustPressed(pair.Key))
                continue;

            if (pair.Value is IPlacementSpell placementSpell)
            {
                if (ReferenceEquals(_pendingPlacementSpell, placementSpell))
                {
                    var tabTarget = Targeting.TabTarget;
                    if (IsValidTabTarget(tabTarget))
                    {
                        placementSpell.TryPlace(this, tabTarget.GlobalPosition);
                        if (!placementSpell.IsAwaitingPlacement)
                            _pendingPlacementSpell = null;
                    }
                    else
                    {
                        ClearPendingPlacementSpell();
                    }

                    return;
                }

                ClearPendingPlacementSpell();
                if (placementSpell.TryBeginPlacement(this))
                    _pendingPlacementSpell = placementSpell;

                return;
            }

            ClearPendingPlacementSpell();
            pair.Value.TryCast(this);
            return;
        }
    }

    private void ClearPendingPlacementSpell()
    {
        _pendingPlacementSpell?.CancelPlacement();
        _pendingPlacementSpell = null;
    }

    private void LoadEquippedSpells()
    {
        _spellsByAction.Clear();

        var spellsNode = GetNode<Node>("Spells");
        foreach (var child in spellsNode.GetChildren())
        {
            if (child is not Spell spell)
            {
                GD.PushError($"{GetPath()}: Spells container child {child.Name} must inherit Spell.");
                continue;
            }

            if (spell.CastAction == default)
            {
                GD.PushError($"{spell.GetPath()}: Spell is missing CastAction.");
                continue;
            }

            _spellsByAction[spell.CastAction] = spell;
        }
    }

}
