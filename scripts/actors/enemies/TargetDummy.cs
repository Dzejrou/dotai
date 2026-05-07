using Godot;

using System;

[GlobalClass]
public partial class TargetDummy : CombatCharacter, IAttackable, ITargetable
{
    private static readonly Color InactiveModulate = new(0.55f, 0.55f, 0.55f, 0.45f);
    private const string DefaultVisualDirection = "south";

    [Export]
    public float RespawnDelaySeconds { get; set; } = 30.0f;

    [Export(PropertyHint.Enum, "east,south-east,south,south-west,west,north-west,north,north-east")]
    public string VisualDirection { get; set; } = DefaultVisualDirection;

    [Export] public Texture2D EastTexture { get; set; }
    [Export] public Texture2D SouthEastTexture { get; set; }
    [Export] public Texture2D SouthTexture { get; set; }
    [Export] public Texture2D SouthWestTexture { get; set; }
    [Export] public Texture2D WestTexture { get; set; }
    [Export] public Texture2D NorthWestTexture { get; set; }
    [Export] public Texture2D NorthTexture { get; set; }
    [Export] public Texture2D NorthEastTexture { get; set; }

    public bool CanBeTargeted => !IsDead;

    private Timer _respawnTimer;
    private ActorHUD _actorHud;
    private OmniSprite _omniSprite;
    private CollisionShape2D _collisionShape;
    private Vector2 _spawnPosition;
    private bool _respawnTimerConnected;
    private bool _statusEffectsBound;

    public override void _EnterTree()
    {
        base._EnterTree();
        EnsureTreeLifetimeConnections();
    }

    public override void _Ready()
    {
        InitializeCombatCharacter();

        _respawnTimer = GetNodeOrNull<Timer>("RespawnTimer");
        _actorHud = GetNodeOrNull<ActorHUD>("ActorHUD");
        _omniSprite = GetNodeOrNull<OmniSprite>("OmniSprite");
        _collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        _spawnPosition = GlobalPosition;

        var statusEffectController = GetNodeOrNull<StatusEffectController>("StatusEffectController");
        SetStatusEffectController(statusEffectController);

        if (_actorHud == null)
            GD.PushError($"{GetPath()}: missing required ActorHUD child.");
        else
            _actorHud.Bind(this);

        if (_omniSprite == null)
            GD.PushError($"{GetPath()}: missing required OmniSprite child.");

        if (_collisionShape == null)
            GD.PushError($"{GetPath()}: missing required CollisionShape2D child.");

        if (statusEffectController == null)
            GD.PushError($"{GetPath()}: missing required StatusEffectController child.");

        AddToGroup(CombatGroups.Actors);
        ResetCombatState();
        OnHealthStateChanged();
        RefreshVisualState();
        ConfigureRespawnTimer();
        EnsureTreeLifetimeConnections();
    }

    public override void _PhysicsProcess(double delta)
    {
        Combat?.Update(delta);
    }

    public override void _ExitTree()
    {
        DisconnectTreeLifetimeConnections();
        base._ExitTree();
    }

    public void ApplyDamage(Damage damageInfo)
    {
        if (IsDead)
            return;

        if (!TryApplyDamageToHealth(damageInfo, setReceiverTargetToSource: false, out var damage))
            return;

        FloatingText.ShowBad(damage.ToString(), this);

        if (HealthStateNode.IsDead)
            StartDeath();
    }

    public override void ApplyHealing(Healing healing)
    {
        var amount = healing?.Amount ?? 0;
        if (amount <= 0 || IsDead)
            return;

        var recovered = HealthStateNode.ApplyHealing(amount);
        if (recovered <= 0)
            return;

        FloatingText.ShowGood($"+{recovered}", this);
    }

    public void ResetSpawnPositionToCurrentPosition()
    {
        _spawnPosition = GlobalPosition;
    }

    private void StartDeath()
    {
        if (IsDead && _respawnTimer != null && _respawnTimer.TimeLeft > 0.0)
            return;

        HealthStateNode.SetDead(true);
        ResetCombatState();
        StatusEffectControllerNode?.ClearAllEffects();
        SetCollisionEnabled(false);
        RefreshVisualState();

        if (_respawnTimer != null)
        {
            _respawnTimer.Stop();
            _respawnTimer.WaitTime = Math.Max(0.01f, RespawnDelaySeconds);
            _respawnTimer.Start();
        }
    }

    private void OnRespawnTimerTimeout()
    {
        Respawn();
    }

    private void Respawn()
    {
        GlobalPosition = _spawnPosition;
        HealthStateNode.SetCurrent(MaxHealableHealth);
        ResetCombatState();
        SetCollisionEnabled(true);
        RefreshVisualState();
    }

    protected override void OnHealthStateChanged()
    {
        UpdateHud();
    }

    private void UpdateHud()
    {
        if (_actorHud == null)
            return;

        _actorHud.SetHealth(CurrentHealth, MaxHealableHealth);
        _actorHud.SetFaction(Faction);
    }

    private void ConfigureRespawnTimer()
    {
        if (_respawnTimer == null)
            return;

        _respawnTimer.OneShot = true;
        _respawnTimer.WaitTime = Math.Max(0.01f, RespawnDelaySeconds);
    }

    private void BindStatusEffects(StatusEffectController statusEffectController)
    {
        statusEffectController.Connect(
            StatusEffectController.SignalName.StatusVisualStateChanged,
            new Callable(this, nameof(OnStatusVisualStateChanged)));

        statusEffectController.Connect(
            StatusEffectController.SignalName.StatusFloatingTextRequested,
            new Callable(this, nameof(OnStatusFloatingTextRequested)));

        foreach (var effect in statusEffectController.GetActiveStatusEffects())
            OnStatusVisualStateChanged(effect.StatusKey, effect, true);
    }

    private void EnsureTreeLifetimeConnections()
    {
        EnsureRespawnTimerConnected();
        EnsureStatusEffectsBound();
    }

    private void DisconnectTreeLifetimeConnections()
    {
        DisconnectRespawnTimer();
        UnbindStatusEffects();
    }

    private void EnsureRespawnTimerConnected()
    {
        if (_respawnTimerConnected || _respawnTimer == null)
            return;

        _respawnTimer.Timeout += OnRespawnTimerTimeout;
        _respawnTimerConnected = true;
    }

    private void DisconnectRespawnTimer()
    {
        if (!_respawnTimerConnected || _respawnTimer == null)
            return;

        _respawnTimer.Timeout -= OnRespawnTimerTimeout;
        _respawnTimerConnected = false;
    }

    private void EnsureStatusEffectsBound()
    {
        if (_statusEffectsBound || StatusEffectControllerNode == null)
            return;

        BindStatusEffects(StatusEffectControllerNode);
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

        _omniSprite?.ReflectStatusEffect(effect, active);
    }

    private void OnStatusFloatingTextRequested(string text, Color color)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        FloatingText.ShowCustom(text, this, color);
    }

    private void RefreshVisualState()
    {
        if (_omniSprite == null)
            return;

        _omniSprite.SetStaticTexture(ResolveVisualTexture());
        _omniSprite.SetBaseModulate(IsDead ? InactiveModulate : Colors.White);
    }

    private void SetCollisionEnabled(bool enabled)
    {
        if (_collisionShape == null)
            return;

        _collisionShape.SetDeferred("disabled", !enabled);
    }

    private Texture2D ResolveVisualTexture()
    {
        var requestedDirection = string.IsNullOrWhiteSpace(VisualDirection) ? DefaultVisualDirection : VisualDirection;
        var fallbackDirection = DirectionHelper.GetCardinalFallbackDirectionName(requestedDirection);

        return ResolveTextureForDirection(requestedDirection) ??
               ResolveTextureForDirection(fallbackDirection) ??
               SouthTexture ??
               EastTexture ??
               WestTexture ??
               NorthTexture;
    }

    private Texture2D ResolveTextureForDirection(string direction)
    {
        return direction switch
        {
            "east" => EastTexture,
            "south-east" => SouthEastTexture,
            "south" => SouthTexture,
            "south-west" => SouthWestTexture,
            "west" => WestTexture,
            "north-west" => NorthWestTexture,
            "north" => NorthTexture,
            "north-east" => NorthEastTexture,
            _ => SouthTexture,
        };
    }
}
