using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class Player : CombatCharacter, IAttackable, ITargetable, ISpellCaster
{
    private enum PendingSpellPhase
    {
        Casting,
        Channeling,
    }

    private sealed class PendingPlayerCast
    {
        public Spell Spell { get; init; }
        public SpellCastRequest Request { get; init; }
        public float DurationSeconds { get; init; }
        public float ElapsedSeconds { get; set; }
        public PendingSpellPhase Phase { get; init; }
        public SpellCastResult ChannelResult { get; init; }
    }

    private const string CastingAnimationBaseName = "casting";
    private const string CastAnimationBaseName = "cast";

    [Signal]
    public delegate void PlayerDiedEventHandler();

    [Signal]
    public delegate void InteractionAvailabilityChangedEventHandler(bool available);

    [Signal]
    public delegate void SpellLoadoutChangedEventHandler();

    [Signal]
    public delegate void ExperienceGainedEventHandler(int amount, int totalExperience);

    [Signal]
    public delegate void LevelChangedEventHandler(int newLevel);

    [Signal]
    public delegate void ExperienceChangedEventHandler(int currentExperience, int requiredExperience, int level);

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

    [Export(PropertyHint.Range, "0,256,1")]
    public float LootMagnetRadius { get; set; } = 80.0f;

    private const int DefaultExperiencePerLevelFallback = 100;

    [Export]
    public ExperienceTable ExperienceTable { get; set; }

    [Export]
    public int MaxLevel { get; set; } = 60;

    [Export]
    public int MaxLevelExperiencePerGold { get; set; } = 10;

    private int _currentExperience;

    public int CurrentExperience => _currentExperience;

    private float _spellCastPushbackPercent = 0.10f;
    private float _spellCastPushbackInternalCooldownSeconds = 0.5f;
    private bool _isDead;
    private readonly Dictionary<StringName, Spell> _spellsByAction = new();
    private PendingPlayerCast _pendingCast;
    private IPlacementSpell _pendingPlacementSpell;
    private float _healthRegenTimer;
    private float _healthRegenDelayTimer;
    private float _spellCastPushbackCooldownRemaining;
    private IInteractable _activeInteractable;
    private Node2D _activeInteractableNode;
    private ActorHUD _actorHud;
    private Area2D _lootMagnetArea;
    private CollisionShape2D _lootMagnetCollisionShape;
    private ActorHUD _activeTargetHud;
    private readonly HashSet<ActorHUD> _visibleTargetHuds = new();
    private readonly HashSet<ActorHUD> _nextVisibleTargetHuds = new();
    private readonly GameConfigStore _gameConfigStore = new();
    private CastBar _castBar;
    private string _activeCompletionAnimationName;
    private bool _animationFinishedConnected;
    private readonly List<SpellBook> _spellBooks = new();
    private readonly List<SpellBook> _extraSpellBooks = new();
    public CombatUnitState CurrentState { get; private set; } = CombatUnitState.Idle;

    public bool CanBeTargeted => !_isDead;
    public PlayerTargetingState Targeting { get; } = new();
    public Node2D SpellOrigin => this;
    public Spell ArmedPlacementSpell => _pendingPlacementSpell as Spell;
    public SpellBook SpellBookNode { get; private set; }
    public SpellBook TestSpellBookNode { get; private set; }
    public IReadOnlyList<SpellBook> ExtraSpellBookNodes => _extraSpellBooks;
    public SpellLoadout SpellLoadoutNode { get; private set; }
    public bool CanCastSpells => !_isDead;
    public bool HasInteractionTarget => _activeInteractable != null;
    public Node2D CurrentInteractionTarget => _activeInteractableNode;
    public InventoryController InventoryController => (GetParent() as World)?.ResolveInventoryController();
    [Export]
    public float SpellCastPushbackPercent
    {
        get => _spellCastPushbackPercent;
        set => _spellCastPushbackPercent = Math.Max(0.0f, value);
    }

    [Export]
    public float SpellCastPushbackInternalCooldownSeconds
    {
        get => _spellCastPushbackInternalCooldownSeconds;
        set => _spellCastPushbackInternalCooldownSeconds = Math.Max(0.0f, value);
    }

    public void ShowFloatingText(string text, Color color)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        FloatingText.ShowCustom(text, this, color);
    }

    public void BindCastBar(CastBar castBar)
    {
        _castBar = castBar;
        RefreshCastBar();
    }

    private bool _equipmentChangedBound;

    public override void _Ready()
    {
        _gameConfigStore.LoadGameSettings();
        SetOmniSprite(GetNode<OmniSprite>("OmniSprite"));
        EnsureAnimationFinishedConnected();
        InitializeCombatCharacter(requireManaState: true);
        BindEquipmentController();
        _actorHud = GetNodeOrNull<ActorHUD>("ActorHUD");
        _lootMagnetArea = GetNodeOrNull<Area2D>("LootMagnetArea");
        _lootMagnetCollisionShape = GetNodeOrNull<CollisionShape2D>("LootMagnetArea/CollisionShape2D");
        if (_actorHud == null)
            GD.PushError($"{GetPath()}: missing required ActorHUD child.");
        else
        {
            _actorHud.Bind(this);
            _actorHud.SetUnitFrameVisible(true);
        }
        ConfigureLootMagnetArea();

        BindStatusEffects();
        InitializeSpellInventory();
        LoadEquippedSpells();
        SetAnimationSafe(GetIdleAnimationName());
        AddToGroup(CombatGroups.Actors);

        OnHealthStateChanged();
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

        if (_lootMagnetArea != null &&
            GodotObject.IsInstanceValid(_lootMagnetArea) &&
            _lootMagnetArea.IsConnected(Area2D.SignalName.AreaEntered, new Callable(this, nameof(OnLootMagnetAreaEntered))))
        {
            _lootMagnetArea.Disconnect(Area2D.SignalName.AreaEntered, new Callable(this, nameof(OnLootMagnetAreaEntered)));
        }

        UnbindEquipmentController();
        CancelPendingCast();
        DisconnectAnimationFinished();
        UnbindStatusEffects();
        base._ExitTree();
    }

    private void BindEquipmentController()
    {
        if (_equipmentChangedBound || EquipmentControllerNode == null)
            return;

        EquipmentControllerNode.Connect(
            EquipmentController.SignalName.Changed,
            new Callable(this, nameof(OnEquipmentChanged)));
        _equipmentChangedBound = true;
    }

    private void UnbindEquipmentController()
    {
        if (!_equipmentChangedBound ||
            EquipmentControllerNode == null ||
            !GodotObject.IsInstanceValid(EquipmentControllerNode))
        {
            _equipmentChangedBound = false;
            return;
        }

        var callable = new Callable(this, nameof(OnEquipmentChanged));
        if (EquipmentControllerNode.IsConnected(EquipmentController.SignalName.Changed, callable))
            EquipmentControllerNode.Disconnect(EquipmentController.SignalName.Changed, callable);

        _equipmentChangedBound = false;
    }

    private void OnEquipmentChanged()
    {
        // SetMax clamps current values; equipping never refills HP/mana.
        HealthStateNode?.SetMax(ResolvedMaxHealth);
        if (ManaStateNode != null)
        {
            ManaStateNode.SetMax(ResolvedMaxMana);
            NotifyManaChanged();
        }

        RefreshActorHud();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_isDead)
            return;

        Combat.Update(delta);
        if (!InCombat && ManaState.Tick(delta, ResolvedMP5) > 0)
            NotifyManaChanged();

        TickSpellCastPushbackCooldown((float)delta);
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

        if (UpdatePendingCast((float)delta, direction))
            return;

        SetState(CombatUnitState.Idle);

        if (direction == Vector2.Zero)
        {
            UpdateTargetingState();
            Velocity = Vector2.Zero;
            UpdatePassiveTargetFacing();
            if (TryHoldCompletionAnimation())
                return;

            SetAnimationSafe(GetIdleAnimationName());
            return;
        }

        direction = direction.Normalized();
        SetFacingDirection(direction);
        ClearCompletionAnimation();
        if (!CanMove)
        {
            Velocity = Vector2.Zero;
            UpdateTargetingState();
            if (TryHoldCompletionAnimation())
                return;

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

        TryFinalizePlacementSpellCast(_pendingPlacementSpell, CreatePlacementCastRequest(GetGlobalMousePosition()));
        GetViewport().SetInputAsHandled();
    }

    public void ApplyDamage(Damage damageInfo)
    {
        if (_isDead)
            return;

        if (!TryApplyDamageToHealth(damageInfo, setReceiverTargetToSource: true, out var damage))
            return;

        ShowFloatingDamageNumber(damage, damageInfo.IsCritical);
        _healthRegenDelayTimer = Math.Max(HealthRegenerationDelayAfterDamage, 0.0f);
        TryApplySpellCastPushback(damage);

        if (HealthStateNode.IsDead)
        {
            _isDead = true;
            ResetCombatState();
            Targeting.ClearAllTargets();
            CancelPendingCast();
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
        _healthRegenTimer = Math.Max(HealthRegenerationInterval, 0.0f);
    }

    public int AddGold(int amount)
    {
        if (_isDead || amount <= 0)
            return 0;

        var inventory = InventoryController;
        if (inventory == null || !GodotObject.IsInstanceValid(inventory))
            return 0;

        var added = inventory.AddGold(amount);
        if (added > 0)
            FloatingText.ShowCustom($"+{added} gold", this, new Color(1.0f, 0.88f, 0.32f, 1.0f));

        return added;
    }

    public int GetRequiredExperienceForCurrentLevel()
    {
        return GetRequiredExperienceForLevel(Level);
    }

    private int GetRequiredExperienceForLevel(int level)
    {
        var fallback = Math.Max(1, DefaultExperiencePerLevelFallback);
        var maxLevel = Math.Max(1, MaxLevel);
        if (level >= maxLevel)
            return fallback;

        return ExperienceTable?.GetRequiredExperienceForLevel(level, fallback) ?? fallback;
    }

    public void AddExperience(int amount)
    {
        if (_isDead || amount <= 0)
            return;

        var maxLevel = Math.Max(1, MaxLevel);
        var xpPerGold = Math.Max(1, MaxLevelExperiencePerGold);

        if (Level >= maxLevel)
        {
            AddGold(amount / xpPerGold);
            return;
        }

        FloatingText.ShowCustom($"+{amount} XP", this, new Color(0.3f, 1.0f, 0.5f, 1.0f));
        _currentExperience += amount;

        var required = GetRequiredExperienceForCurrentLevel();
        while (_currentExperience >= required && Level < maxLevel)
        {
            _currentExperience -= required;
            Level++;
            FloatingText.ShowCustom($"LEVEL {Level}", this, new Color(1.0f, 0.95f, 0.2f, 1.0f));
            required = GetRequiredExperienceForCurrentLevel();
        }

        if (Level >= maxLevel && _currentExperience > 0)
        {
            var leftoverGold = _currentExperience / xpPerGold;
            _currentExperience = 0;
            if (leftoverGold > 0)
                AddGold(leftoverGold);
        }

        EmitSignal(SignalName.ExperienceGained, amount, _currentExperience);
        EmitSignal(SignalName.ExperienceChanged, _currentExperience, GetRequiredExperienceForCurrentLevel(), Level);
    }

    public PlayerSaveData CreateSaveSnapshot()
    {
        return new PlayerSaveData
        {
            Level = Level,
            CurrentExperience = _currentExperience,
            CurrentHealth = CurrentHealth,
            CurrentMana = CurrentMana,
        };
    }

    public void ApplyLoadedLevelAndExperience(int level, int currentExperience)
    {
        var maxLevel = Math.Max(1, MaxLevel);
        Level = Math.Clamp(level, 1, maxLevel);

        var required = GetRequiredExperienceForCurrentLevel();
        _currentExperience = Level >= maxLevel
            ? 0
            : Math.Clamp(currentExperience, 0, Math.Max(0, required - 1));

        EmitSignal(SignalName.ExperienceChanged, _currentExperience, required, Level);
    }

    protected override void OnLevelChanged(int newLevel)
    {
        EmitSignal(SignalName.LevelChanged, newLevel);
    }

    public void ApplyLoadedHealthAndMana(int currentHealth, int currentMana)
    {
        HealthStateNode?.SetCurrent(currentHealth);
        if (ManaStateNode != null)
        {
            ManaStateNode.SetCurrent(currentMana);
            NotifyManaChanged();
        }
    }

    public bool TryAdjustLevelForTesting(int delta)
    {
        if (_isDead || delta == 0)
            return false;

        var maxLevel = Math.Max(1, MaxLevel);
        var previousLevel = Level;
        Level = Math.Clamp(Level + delta, 1, maxLevel);
        if (Level == previousLevel)
            return false;

        var required = GetRequiredExperienceForCurrentLevel();
        _currentExperience = Level >= maxLevel
            ? 0
            : Math.Clamp(_currentExperience, 0, required - 1);

        EmitSignal(SignalName.ExperienceChanged, _currentExperience, required, Level);
        return true;
    }

    public Stats DebugStats => StatsNode;
    public HealthState DebugHealthState => HealthStateNode;
    public ManaState DebugManaState => ManaStateNode;

    public bool DebugSetLevel(int level)
    {
        var maxLevel = Math.Max(1, MaxLevel);
        var clamped = Math.Clamp(level, 1, maxLevel);
        return TryAdjustLevelForTesting(clamped - Level);
    }

    public void DebugSetCurrentExperience(int amount)
    {
        var maxLevel = Math.Max(1, MaxLevel);
        var required = GetRequiredExperienceForCurrentLevel();
        var upperBound = Level >= maxLevel ? 0 : Math.Max(0, required - 1);
        _currentExperience = Math.Clamp(amount, 0, upperBound);
        EmitSignal(SignalName.ExperienceChanged, _currentExperience, required, Level);
    }

    public void DebugSetCurrentHealth(int value)
    {
        HealthStateNode?.SetCurrent(value);
    }

    public void DebugSetCurrentMana(int value)
    {
        if (ManaStateNode == null)
            return;

        ManaStateNode.SetCurrent(value);
        NotifyManaChanged();
    }

    public void DebugResyncMaxHealthFromStats()
    {
        if (StatsNode == null || HealthStateNode == null)
            return;

        HealthStateNode.SetMax(StatsNode.ResolvedMaxHealth);
    }

    public void DebugResyncMaxManaFromStats()
    {
        if (StatsNode == null || ManaStateNode == null)
            return;

        ManaStateNode.SetMax(StatsNode.ResolvedMaxMana);
        NotifyManaChanged();
    }

    public int RestoreManaFromDrop(int amount)
    {
        if (_isDead || amount <= 0 || ManaState == null)
            return 0;

        var restored = ManaState.Restore(amount);
        NotifyManaChanged();

        if (restored > 0)
            FloatingText.ShowCustom($"+{restored} mana", this, new Color(0.45f, 0.78f, 1.0f, 1.0f));

        return restored;
    }

    public int RestoreHealthFromDrop(int amount)
    {
        if (_isDead || amount <= 0 || HealthStateNode == null)
            return 0;

        var currentHealthBefore = CurrentHealth;
        var healing = new Healing();
        healing.InitializeRuntime(this, amount);
        ApplyHealing(healing);
        return Math.Max(0, CurrentHealth - currentHealthBefore);
    }

    public bool TrySaveSpellLoadoutConfiguration(out string message)
    {
        if (SpellLoadoutNode == null || !GodotObject.IsInstanceValid(SpellLoadoutNode))
        {
            message = $"{GetPath()}: player spell loadout is unavailable.";
            return false;
        }

        // TODO: Revisit whether spell loadout changes should autosave after binding updates once options-menu settings also share config.json.
        return _gameConfigStore.TrySaveSpellLoadout(SpellLoadoutNode, out message);
    }

    public IReadOnlyList<Spell> GetBindableSpells(bool includeTestSpells)
    {
        var spells = new List<Spell>();
        AddSpellTemplates(spells, SpellBookNode);
        if (includeTestSpells)
        {
            foreach (var extraSpellBook in _extraSpellBooks)
                AddSpellTemplates(spells, extraSpellBook);
        }

        return spells;
    }

    public bool TryResolveSpellById(StringName spellId, out Spell spell)
    {
        spell = null;
        if (spellId.IsEmpty)
            return false;

        var equippedSpell = SpellLoadoutNode?.GetEquippedSpellById(spellId);
        if (equippedSpell != null && GodotObject.IsInstanceValid(equippedSpell))
        {
            spell = equippedSpell;
            return true;
        }

        foreach (var spellBook in _spellBooks)
        {
            if (spellBook == null || !GodotObject.IsInstanceValid(spellBook))
                continue;

            var spellTemplate = spellBook.GetSpellTemplateById(spellId);
            if (spellTemplate == null)
                continue;

            spell = spellTemplate;
            return true;
        }

        return false;
    }

    public bool TryCancelSpellInputFromEscape()
    {
        if (_pendingCast != null)
        {
            CancelPendingCast(showCanceledFeedback: true);
            GetViewport()?.SetInputAsHandled();
            return true;
        }

        if (_pendingPlacementSpell != null)
        {
            ClearPendingPlacementSpell();
            GetViewport()?.SetInputAsHandled();
            return true;
        }

        return false;
    }

    private void ConfigureLootMagnetArea()
    {
        if (_lootMagnetArea == null)
        {
            GD.PushError($"{GetPath()}: missing required LootMagnetArea child.");
            return;
        }

        if (_lootMagnetCollisionShape == null)
        {
            GD.PushError($"{GetPath()}: missing required LootMagnetArea/CollisionShape2D child.");
            return;
        }

        if (_lootMagnetCollisionShape.Shape is not CircleShape2D circleShape)
        {
            circleShape = new CircleShape2D();
            _lootMagnetCollisionShape.Shape = circleShape;
        }

        circleShape.Radius = Math.Max(0.0f, LootMagnetRadius);
        _lootMagnetArea.Monitoring = true;
        _lootMagnetArea.Monitorable = false;
        _lootMagnetArea.Connect(Area2D.SignalName.AreaEntered, new Callable(this, nameof(OnLootMagnetAreaEntered)));
    }

    private void OnLootMagnetAreaEntered(Area2D area)
    {
        if (_isDead || area is not Drop drop)
            return;

        drop.BeginAttraction(this);
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

        foreach (var effect in statusEffectController.GetActiveStatusEffects())
            OnStatusVisualStateChanged(effect.StatusKey, effect, true);
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

    private void OnStatusVisualStateChanged(StringName statusKey, StatusEffect effect, bool active)
    {
        if (statusKey == PoisonedEffect.StatusKeyName)
            _actorHud?.SetPoisoned(active);

        OmniSprite?.ReflectStatusEffect(effect, active);
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

        if (ReferenceEquals(nextInteractable, _activeInteractable) &&
            ReferenceEquals(nextInteractableNode, _activeInteractableNode))
        {
            return;
        }

        _activeInteractable = nextInteractable;
        _activeInteractableNode = nextInteractableNode;
        EmitSignal(SignalName.InteractionAvailabilityChanged, nextInteractable != null);
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

    public bool TryGetInteractionPromptPosition(out Vector2 promptPosition)
    {
        if (!IsValidInteractionTarget(_activeInteractableNode, _activeInteractable))
        {
            promptPosition = Vector2.Zero;
            return false;
        }

        var promptOffset = _activeInteractableNode is IInteractionPromptAnchor promptAnchor
            ? promptAnchor.InteractionPromptOffset
            : new Vector2(0.0f, -56.0f);
        promptPosition = _activeInteractableNode.GlobalPosition + promptOffset;
        return true;
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

    private void ShowFloatingDamageNumber(int amount, bool isCritical)
    {
        FloatingText.ShowDamage(amount, isCritical, this);
    }

    private void ShowFloatingHealingNumber(int amount)
    {
        if (amount <= 0)
            return;

        FloatingText.ShowGood($"+{amount}", this);
    }

    private void RefreshActorHud()
    {
        if (_actorHud == null)
            return;

        _actorHud.SetHealth(CurrentHealth, MaxHealableHealth);
        _actorHud.SetFaction(Faction);
    }

    protected override void OnHealthStateChanged()
    {
        RefreshActorHud();
    }

    public void NotifyManaChanged()
    {
        RefreshActorHud();
    }

    private void InitializeSpellInventory()
    {
        SpellBookNode = GetNodeOrNull<SpellBook>("SpellBook");
        TestSpellBookNode = GetNodeOrNull<SpellBook>("TestSpellBook");
        SpellLoadoutNode = GetNodeOrNull<SpellLoadout>("SpellLoadout");
        _spellBooks.Clear();
        _extraSpellBooks.Clear();

        if (SpellBookNode == null)
        {
            GD.PushError($"{GetPath()}: missing required SpellBook child.");
            return;
        }

        _spellBooks.Add(SpellBookNode);
        CollectExtraSpellBooks();

        if (SpellLoadoutNode == null)
        {
            GD.PushError($"{GetPath()}: missing required SpellLoadout child.");
            return;
        }

        _gameConfigStore.InitializeSpellLoadout(SpellBookNode, _spellBooks, SpellLoadoutNode);
        SpellLoadoutNode.Connect(SpellLoadout.SignalName.LoadoutChanged, new Callable(this, nameof(OnSpellLoadoutChanged)));
    }

    private static void AddSpellTemplates(List<Spell> spells, SpellBook spellBook)
    {
        if (spells == null || spellBook == null || !GodotObject.IsInstanceValid(spellBook))
            return;

        foreach (var spellTemplate in spellBook.GetSpellTemplates())
            spells.Add(spellTemplate);
    }

    private void CollectExtraSpellBooks()
    {
        foreach (var child in GetChildren())
        {
            if (child is not SpellBook spellBook || ReferenceEquals(spellBook, SpellBookNode))
                continue;

            _extraSpellBooks.Add(spellBook);
            _spellBooks.Add(spellBook);
        }
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

    private void TickSpellCastPushbackCooldown(float delta)
    {
        if (_spellCastPushbackCooldownRemaining <= 0.0f)
            return;

        _spellCastPushbackCooldownRemaining = Math.Max(
            0.0f,
            _spellCastPushbackCooldownRemaining - Math.Max(0.0f, delta));
    }

    private void TryApplySpellCastPushback(int damage)
    {
        if (damage <= 0 ||
            _pendingCast == null ||
            SpellCastPushbackPercent <= 0.0f ||
            _spellCastPushbackCooldownRemaining > 0.0f)
        {
            return;
        }

        var pushbackSeconds = Math.Max(0.0f, _pendingCast.DurationSeconds * SpellCastPushbackPercent);
        if (pushbackSeconds <= 0.0f)
            return;

        if (_pendingCast.Phase == PendingSpellPhase.Channeling)
            _pendingCast.ElapsedSeconds = Math.Min(_pendingCast.DurationSeconds, _pendingCast.ElapsedSeconds + pushbackSeconds);
        else
            _pendingCast.ElapsedSeconds = Math.Max(0.0f, _pendingCast.ElapsedSeconds - pushbackSeconds);

        _spellCastPushbackCooldownRemaining = SpellCastPushbackInternalCooldownSeconds;
        RefreshCastBar();

        if (_castBar != null && GodotObject.IsInstanceValid(_castBar))
            _castBar.ShowPushback(pushbackSeconds);

        if (_pendingCast != null && _pendingCast.ElapsedSeconds >= _pendingCast.DurationSeconds)
            CompletePendingCast();
    }

    private bool UpdatePendingCast(float delta, Vector2 movementInput)
    {
        if (_pendingCast == null)
            return false;

        if (movementInput != Vector2.Zero)
        {
            CancelPendingCast();
            return false;
        }

        UpdateTargetingState();
        Velocity = Vector2.Zero;
        SetState(_pendingCast.Phase == PendingSpellPhase.Channeling
            ? CombatUnitState.Channeling
            : CombatUnitState.Casting);
        UpdatePendingCastFacing();
        _pendingCast.ElapsedSeconds = Math.Min(
            _pendingCast.DurationSeconds,
            _pendingCast.ElapsedSeconds + Math.Max(0.0f, delta));
        RefreshCastBar();
        PlayCastingAnimationIfAvailable();

        if (_pendingCast.ElapsedSeconds < _pendingCast.DurationSeconds)
            return true;

        CompletePendingCast();
        return false;
    }

    private void UpdatePassiveTargetFacing()
    {
        var tabTarget = Targeting.TabTarget;
        if (!IsValidTabTarget(tabTarget) || SpellOrigin == null || !GodotObject.IsInstanceValid(SpellOrigin))
            return;

        var toTarget = tabTarget.GlobalPosition - SpellOrigin.GlobalPosition;
        if (toTarget == Vector2.Zero)
            return;

        SetFacingDirection(toTarget);
    }

    private void UpdatePendingCastFacing()
    {
        if (_pendingCast?.Spell == null || !GodotObject.IsInstanceValid(_pendingCast.Spell))
            return;

        var followLiveTargetNode = _pendingCast.Phase == PendingSpellPhase.Casting;
        FaceSpellRequest(_pendingCast.Spell, _pendingCast.Request ?? SpellCastRequest.Empty, followLiveTargetNode);
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
                if (ReferenceEquals(_pendingCast?.Spell, spell))
                    return;

                if (_pendingCast != null)
                    CancelPendingCast();

                if (ReferenceEquals(_pendingPlacementSpell, placementSpell))
                {
                    var tabTarget = Targeting.TabTarget;
                    if (IsValidTabTarget(tabTarget))
                    {
                        TryFinalizePlacementSpellCast(placementSpell, CreatePlacementCastRequest(tabTarget.GlobalPosition, tabTarget));
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
            TryStartSpellCast(spell, CreateSpellCastRequest());
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

    private bool TryFinalizePlacementSpellCast(IPlacementSpell placementSpell, SpellCastRequest request)
    {
        if (placementSpell is not Spell spell)
            return false;

        ClearPendingPlacementSpell();
        return TryStartSpellCast(spell, request);
    }

    private bool TryStartSpellCast(Spell spell, SpellCastRequest request)
    {
        if (spell == null || !GodotObject.IsInstanceValid(spell))
            return false;

        if (ReferenceEquals(_pendingCast?.Spell, spell))
            return false;

        ClearCompletionAnimation();
        CancelPendingCast();

        var lockedRequest = request?.Clone() ?? SpellCastRequest.Empty;
        if (!spell.CanCast(this, lockedRequest))
            return false;

        FaceSpellRequest(spell, lockedRequest);

        var castDuration = ApplyHasteToDuration(spell.CastTimeDuration);
        if (castDuration <= 0.0f)
            return StartSpellEffect(spell, lockedRequest);

        _pendingCast = new PendingPlayerCast
        {
            Spell = spell,
            Request = lockedRequest,
            DurationSeconds = castDuration,
            ElapsedSeconds = 0.0f,
            Phase = PendingSpellPhase.Casting,
        };

        SetState(CombatUnitState.Casting);
        RefreshCastBar();
        PlayCastingAnimationIfAvailable();
        return true;
    }

    private void CompletePendingCast()
    {
        var completedCast = _pendingCast;
        _pendingCast = null;
        RefreshCastBar();

        if (completedCast?.Spell == null || !GodotObject.IsInstanceValid(completedCast.Spell))
        {
            SetState(CombatUnitState.Idle);
            return;
        }

        if (completedCast.Phase == PendingSpellPhase.Channeling)
        {
            CleanupChannelOwnedNodes(completedCast.ChannelResult);
            SetState(CombatUnitState.Idle);
            return;
        }

        if (completedCast.Spell.IsChanneled)
        {
            if (!StartSpellEffect(completedCast.Spell, completedCast.Request ?? SpellCastRequest.Empty))
                ShowCastCanceled();

            return;
        }

        FaceSpellRequest(completedCast.Spell, completedCast.Request ?? SpellCastRequest.Empty);
        PlayCastCompletionAnimationIfAvailable();
        if (!completedCast.Spell.TryCast(this, completedCast.Request ?? SpellCastRequest.Empty))
            ShowCastCanceled();

        SetState(CombatUnitState.Idle);
    }

    private void CancelPendingCast(bool showCanceledFeedback = false)
    {
        var shouldShowCanceledFeedback = showCanceledFeedback &&
                                         _pendingCast != null &&
                                         _castBar != null &&
                                         GodotObject.IsInstanceValid(_castBar);
        CleanupChannelOwnedNodes(_pendingCast?.Phase == PendingSpellPhase.Channeling ? _pendingCast.ChannelResult : null);
        _pendingCast = null;
        SetState(CombatUnitState.Idle);
        if (shouldShowCanceledFeedback)
            _castBar.ShowCanceled();
        else
            RefreshCastBar();
    }

    private void RefreshCastBar()
    {
        if (_castBar == null || !GodotObject.IsInstanceValid(_castBar))
            return;

        if (_pendingCast?.Spell == null || !GodotObject.IsInstanceValid(_pendingCast.Spell))
        {
            _castBar.HideCast();
            return;
        }

        _castBar.ShowCast(
            _pendingCast.Spell.DisplayLabel,
            _pendingCast.DurationSeconds,
            _pendingCast.Phase == PendingSpellPhase.Channeling);
        _castBar.UpdateCast(_pendingCast.ElapsedSeconds);
    }

    private void ShowCastCanceled(string label = "CANCELED")
    {
        if (_castBar == null || !GodotObject.IsInstanceValid(_castBar))
            return;

        _castBar.ShowCanceled(label);
    }

    private void PlayCastingAnimationIfAvailable()
    {
        var animationName = ResolveDirectionalAnimationName(CastingAnimationBaseName);
        if (!string.IsNullOrEmpty(animationName))
            SetAnimationSafe(animationName);
    }

    private void PlayCastCompletionAnimationIfAvailable()
    {
        ClearCompletionAnimation();
        var animationName = ResolveDirectionalAnimationName(CastAnimationBaseName);
        if (string.IsNullOrEmpty(animationName) || OmniSprite == null)
            return;

        if (OmniSprite.TryPlay(animationName))
            _activeCompletionAnimationName = animationName;
    }

    private bool TryHoldCompletionAnimation()
    {
        if (string.IsNullOrEmpty(_activeCompletionAnimationName))
            return false;

        if (OmniSprite == null ||
            !GodotObject.IsInstanceValid(OmniSprite) ||
            OmniSprite.CurrentAnimation != _activeCompletionAnimationName ||
            !OmniSprite.IsAnimationPlaying)
        {
            _activeCompletionAnimationName = null;
            return false;
        }

        return true;
    }

    private void ClearCompletionAnimation()
    {
        _activeCompletionAnimationName = null;
    }

    private void EnsureAnimationFinishedConnected()
    {
        if (_animationFinishedConnected || OmniSprite == null)
            return;

        OmniSprite.AnimationFinished += OnOmniSpriteAnimationFinished;
        _animationFinishedConnected = true;
    }

    private void DisconnectAnimationFinished()
    {
        if (!_animationFinishedConnected || OmniSprite == null)
            return;

        OmniSprite.AnimationFinished -= OnOmniSpriteAnimationFinished;
        _animationFinishedConnected = false;
    }

    private void OnOmniSpriteAnimationFinished()
    {
        ClearCompletionAnimation();
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

    private bool StartSpellEffect(Spell spell, SpellCastRequest request)
    {
        if (spell == null || !GodotObject.IsInstanceValid(spell))
            return false;

        var lockedRequest = request?.Clone() ?? SpellCastRequest.Empty;
        FaceSpellRequest(spell, lockedRequest);
        var shouldOwnRuntimeNodes = spell.IsChanneled;
        lockedRequest.OwnRuntimeNodesForChannel = shouldOwnRuntimeNodes;

        var didCast = spell.TryCast(this, lockedRequest, out var castResult);
        if (!didCast)
        {
            SetState(CombatUnitState.Idle);
            return false;
        }

        if (!spell.IsChanneled)
        {
            SetState(CombatUnitState.Idle);
            return true;
        }

        _pendingCast = new PendingPlayerCast
        {
            Spell = spell,
            Request = lockedRequest,
            DurationSeconds = ApplyHasteToDuration(spell.ChannelDuration),
            ElapsedSeconds = 0.0f,
            Phase = PendingSpellPhase.Channeling,
            ChannelResult = castResult,
        };

        SetState(CombatUnitState.Channeling);
        RefreshCastBar();
        PlayCastingAnimationIfAvailable();
        return true;
    }

    // TODO: Add continuous live-facing for future single-target channeled spells when those are supported.
    private void FaceSpellRequest(Spell spell, SpellCastRequest request, bool followLiveTargetNode = false)
    {
        if (!TryResolveSpellFacingDirection(spell, request, followLiveTargetNode, out var facingDirection))
            return;

        SetFacingDirection(facingDirection);
    }

    private bool TryResolveSpellFacingDirection(
        Spell spell,
        SpellCastRequest request,
        bool followLiveTargetNode,
        out Vector2 facingDirection)
    {
        facingDirection = Vector2.Zero;
        if (spell == null ||
            !spell.ShouldFaceCastRequest ||
            request == null ||
            SpellOrigin == null ||
            !GodotObject.IsInstanceValid(SpellOrigin))
        {
            return false;
        }

        if (followLiveTargetNode && request.TryResolveTargetNode(out var targetNode))
        {
            var toLiveTarget = targetNode.GlobalPosition - SpellOrigin.GlobalPosition;
            if (toLiveTarget == Vector2.Zero)
                return false;

            facingDirection = toLiveTarget.Normalized();
            return true;
        }

        if (request.TryResolveTargetPosition(out var targetPosition))
        {
            var toTarget = targetPosition - SpellOrigin.GlobalPosition;
            if (toTarget == Vector2.Zero)
                return false;

            facingDirection = toTarget.Normalized();
            return true;
        }

        if (!request.Direction.HasValue || request.Direction.Value == Vector2.Zero)
            return false;

        facingDirection = request.Direction.Value.Normalized();
        return true;
    }

    private void CleanupChannelOwnedNodes(SpellCastResult result)
    {
        if (result?.ChannelOwnedNodes == null)
            return;

        foreach (var node in result.ChannelOwnedNodes)
        {
            if (node != null && GodotObject.IsInstanceValid(node))
                node.QueueFree();
        }
    }

    private void SetState(CombatUnitState state)
    {
        CurrentState = state;
    }

}
