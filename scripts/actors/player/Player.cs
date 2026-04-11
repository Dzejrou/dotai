using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class Player : CombatCharacter, IAttackable, ITargetable, ISpellCaster
{
    [Signal]
    public delegate void PlayerDiedEventHandler();

    [Signal]
    public delegate void InteractionAvailabilityChangedEventHandler(bool available, string label);

    [Signal]
    public delegate void SpellLoadoutChangedEventHandler();

    [Export]
    public float Speed { get; set; } = 140.0f;

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
    private readonly Dictionary<StringName, Spell> _spellsByAction = new();
    private IPlacementSpell _pendingPlacementSpell;
    private float _healthRegenTimer;
    private float _healthRegenDelayTimer;
    private IInteractable _activeInteractable;
    private Node2D _activeInteractableNode;
    private string _activeInteractableLabel = string.Empty;
    private ActorHUD _actorHud;
    private ActorHUD _activeTargetHud;
    private readonly HashSet<ActorHUD> _visibleTargetHuds = new();
    private readonly HashSet<ActorHUD> _nextVisibleTargetHuds = new();

    public bool CanBeTargeted => !_isDead;
    public PlayerTargetingState Targeting { get; } = new();
    public Node2D SpellOrigin => this;
    public Spell ArmedPlacementSpell => _pendingPlacementSpell as Spell;
    public SpellBook SpellBookNode { get; private set; }
    public SpellLoadout SpellLoadoutNode { get; private set; }
    public bool CanCastSpells => !_isDead;
    public bool HasInteractionTarget => _activeInteractable != null;
    public string CurrentInteractionLabel => _activeInteractableLabel;

    public void ShowFloatingText(string text, Color color)
    {
        _actorHud?.ShowFloatingText(text, color);
    }

    public override void _Ready()
    {
        SetAnimatedSprite(GetNode<AnimatedSprite2D>("AnimatedSprite2D"));
        InitializeCombatCharacter(requireManaState: true);
        _actorHud = GetNodeOrNull<ActorHUD>("ActorHUD");
        if (_actorHud == null)
            GD.PushError($"{GetPath()}: missing required ActorHUD child.");
        else
        {
            _actorHud.Bind(this);
            _actorHud.SetUnitFrameVisible(true);
        }

        BindStatusEffects();
        InitializeSpellInventory();
        LoadEquippedSpells();
        SetAnimationSafe(GetIdleAnimationName());
        AddToGroup(CombatGroups.Actors);

        RefreshActorHud();
        NotifyManaChanged();
        UpdateInteractionState();
    }

    public override void _ExitTree()
    {
        if (SpellLoadoutNode != null &&
            GodotObject.IsInstanceValid(SpellLoadoutNode) &&
            SpellLoadoutNode.IsConnected(SpellLoadout.SignalName.LoadoutChanged, new Callable(this, nameof(OnSpellLoadoutChanged))))
        {
            SpellLoadoutNode.Disconnect(SpellLoadout.SignalName.LoadoutChanged, new Callable(this, nameof(OnSpellLoadoutChanged)));
        }

        UnbindStatusEffects();
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
        UpdatePendingPlacementPreview();
        TryCastEquippedSpells();
        var direction = GetInputDirection();

        if (direction == Vector2.Zero)
        {
            UpdateTargetingState();
            Velocity = Vector2.Zero;
            SetAnimationSafe(GetIdleAnimationName());
            return;
        }

        direction = direction.Normalized();
        SetFacingDirection(direction);
        if (!CanMove)
        {
            Velocity = Vector2.Zero;
            UpdateTargetingState();
            SetAnimationSafe(GetIdleAnimationName());
            return;
        }

        var isSprinting = Input.IsActionPressed("sprint");
        var moveSpeed = isSprinting ? Speed * 2.0f : Speed;
        Velocity = direction * moveSpeed * Math.Max(0.0f, MovementSpeedMultiplier);
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

        _pendingPlacementSpell.TryPlace(this, CreatePlacementCastRequest(GetGlobalMousePosition()));
        if (!_pendingPlacementSpell.IsAwaitingPlacement)
            _pendingPlacementSpell = null;

        GetViewport().SetInputAsHandled();
    }

    public void ApplyDamage(Damage damageInfo)
    {
        if (_isDead)
            return;

        var damage = HealthStateNode.ApplyDamage(damageInfo.Amount);
        damageInfo.RegisterHit(this, setReceiverTargetToSource: true);

        ShowFloatingDamageNumber(damage);
        RefreshActorHud();
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

    public override void ApplyHealing(Healing healing)
    {
        var amount = healing?.Amount ?? 0;
        if (_isDead || amount <= 0)
            return;

        var recovered = HealthStateNode.ApplyHealing(amount);
        if (recovered <= 0)
            return;

        ShowFloatingHealingNumber(recovered);
        RefreshActorHud();
        _healthRegenTimer = Math.Max(HealthRegenerationInterval, 0.0f);
    }

    private void BindStatusEffects()
    {
        var statusEffectController = GetNodeOrNull<StatusEffectController>("StatusEffectController");
        SetStatusEffectController(statusEffectController);
        if (statusEffectController == null)
        {
            GD.PushError($"{GetPath()}: missing required StatusEffectController child.");
            return;
        }

        statusEffectController.Connect(
            StatusEffectController.SignalName.StatusVisualStateChanged,
            new Callable(this, nameof(OnStatusVisualStateChanged)));

        statusEffectController.Connect(
            StatusEffectController.SignalName.StatusFloatingTextRequested,
            new Callable(this, nameof(OnStatusFloatingTextRequested)));

        OnStatusVisualStateChanged(PoisonedEffect.StatusKeyName, statusEffectController.HasStatus(PoisonedEffect.StatusKeyName));
        OnStatusVisualStateChanged(SlowedEffect.StatusKeyName, statusEffectController.HasStatus(SlowedEffect.StatusKeyName));
    }

    private void UnbindStatusEffects()
    {
        if (StatusEffectControllerNode == null || !GodotObject.IsInstanceValid(StatusEffectControllerNode))
            return;

        var callable = new Callable(this, nameof(OnStatusVisualStateChanged));
        if (StatusEffectControllerNode.IsConnected(StatusEffectController.SignalName.StatusVisualStateChanged, callable))
            StatusEffectControllerNode.Disconnect(StatusEffectController.SignalName.StatusVisualStateChanged, callable);

        var textCallable = new Callable(this, nameof(OnStatusFloatingTextRequested));
        if (StatusEffectControllerNode.IsConnected(StatusEffectController.SignalName.StatusFloatingTextRequested, textCallable))
            StatusEffectControllerNode.Disconnect(StatusEffectController.SignalName.StatusFloatingTextRequested, textCallable);
    }

    private void OnStatusVisualStateChanged(StringName statusKey, bool active)
    {
        if (statusKey == PoisonedEffect.StatusKeyName)
        {
            _actorHud?.SetPoisoned(active);
            return;
        }

        if (statusKey != SlowedEffect.StatusKeyName)
            return;

        if (active)
            SetSpriteTint(SlowedSpriteTintColor);
        else
            ResetSpriteTint();
    }

    private void OnStatusFloatingTextRequested(string text, Color color)
    {
        ShowFloatingText(text, color);
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
            var healing = new Healing();
            healing.InitializeRuntime(this, recovered);
            ApplyHealing(healing);
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
        ShowFloatingText(amount.ToString(), new Color(1.0f, 0.0f, 0.0f, 1.0f));
    }

    private void ShowFloatingHealingNumber(int amount)
    {
        if (amount <= 0)
            return;

        ShowFloatingText($"+{amount}", new Color(0.0f, 1.0f, 0.0f, 1.0f));
    }

    private void RefreshActorHud()
    {
        if (_actorHud == null)
            return;

        _actorHud.SetHealth(CurrentHealth, MaxHealableHealth);
        _actorHud.SetFaction(Faction);
    }

    public void NotifyManaChanged()
    {
        RefreshActorHud();
    }

    private void InitializeSpellInventory()
    {
        SpellBookNode = GetNodeOrNull<SpellBook>("SpellBook");
        SpellLoadoutNode = GetNodeOrNull<SpellLoadout>("SpellLoadout");

        if (SpellBookNode == null)
            GD.PushError($"{GetPath()}: missing required SpellBook child.");

        if (SpellLoadoutNode == null)
        {
            GD.PushError($"{GetPath()}: missing required SpellLoadout child.");
            return;
        }

        SpellLoadoutNode.ApplyDefaultAssignments(SpellBookNode);
        SpellLoadoutNode.Connect(SpellLoadout.SignalName.LoadoutChanged, new Callable(this, nameof(OnSpellLoadoutChanged)));
    }

    private void OnSpellLoadoutChanged()
    {
        LoadEquippedSpells();
        if (_pendingPlacementSpell == null)
        {
            EmitSignal(SignalName.SpellLoadoutChanged);
            return;
        }

        foreach (var equippedSpell in _spellsByAction.Values)
        {
            if (ReferenceEquals(equippedSpell, _pendingPlacementSpell))
            {
                EmitSignal(SignalName.SpellLoadoutChanged);
                return;
            }
        }

        ClearPendingPlacementSpell();
        EmitSignal(SignalName.SpellLoadoutChanged);
    }

    private void UpdatePendingPlacementPreview()
    {
        if (_pendingPlacementSpell == null)
            return;

        _pendingPlacementSpell.UpdatePlacementPreview(CreatePlacementCastRequest(GetGlobalMousePosition()));
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
        foreach (var slotAction in SpellLoadout.SlotActions)
        {
            if (!Input.IsActionJustPressed(slotAction))
                continue;

            if (!_spellsByAction.TryGetValue(slotAction, out var spell) || spell == null)
                return;

            if (spell is IPlacementSpell placementSpell)
            {
                if (ReferenceEquals(_pendingPlacementSpell, placementSpell))
                {
                    var tabTarget = Targeting.TabTarget;
                    if (IsValidTabTarget(tabTarget))
                    {
                        placementSpell.TryPlace(this, CreatePlacementCastRequest(tabTarget.GlobalPosition, tabTarget));
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
                if (placementSpell.TryBeginPlacement(this, CreatePlacementCastRequest(GetGlobalMousePosition())))
                    _pendingPlacementSpell = placementSpell;

                return;
            }

            ClearPendingPlacementSpell();
            spell.TryCast(this, CreateSpellCastRequest());
            return;
        }
    }

    private SpellCastRequest CreateSpellCastRequest(Node2D target = null)
    {
        var spellTarget = ResolveSpellTarget(target);
        var request = new SpellCastRequest
        {
            Direction = GetSpellDirection(),
        };

        if (spellTarget != null)
        {
            request.TargetNode = spellTarget;
            request.TargetPosition = spellTarget.GlobalPosition;
        }

        return request;
    }

    private SpellCastRequest CreatePlacementCastRequest(Vector2 targetPosition, Node2D target = null)
    {
        var request = CreateSpellCastRequest(target);
        request.TargetPosition = targetPosition;
        return request;
    }

    private Node2D ResolveSpellTarget(Node2D preferredTarget = null)
    {
        if (IsValidSpellTarget(preferredTarget))
            return preferredTarget;

        return IsValidSpellTarget(Targeting.ActiveTarget) ? Targeting.ActiveTarget : null;
    }

    private static bool IsValidSpellTarget(Node2D target)
    {
        return target != null &&
               GodotObject.IsInstanceValid(target) &&
               target.IsInsideTree();
    }

    private void ClearPendingPlacementSpell()
    {
        _pendingPlacementSpell?.CancelPlacement();
        _pendingPlacementSpell = null;
    }

    private void LoadEquippedSpells()
    {
        _spellsByAction.Clear();

        if (SpellLoadoutNode == null || !GodotObject.IsInstanceValid(SpellLoadoutNode))
            return;

        foreach (var slotAction in SpellLoadout.SlotActions)
        {
            var spell = SpellLoadoutNode.GetEquippedSpell(slotAction);
            if (spell == null)
                continue;

            if (spell.CastAction == default)
            {
                GD.PushError($"{spell.GetPath()}: Spell is missing CastAction.");
                continue;
            }

            _spellsByAction[spell.CastAction] = spell;
        }
    }

}
