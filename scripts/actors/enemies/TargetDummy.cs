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
    private Sprite2D _visualSprite;
    private CollisionShape2D _collisionShape;
    private Vector2 _spawnPosition;
    private bool _isSlowed;

    public override void _Ready()
    {
        InitializeCombatCharacter();

        _respawnTimer = GetNodeOrNull<Timer>("RespawnTimer");
        _actorHud = GetNodeOrNull<ActorHUD>("ActorHUD");
        _visualSprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        _collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        _spawnPosition = GlobalPosition;

        var statusEffectController = GetNodeOrNull<StatusEffectController>("StatusEffectController");
        SetStatusEffectController(statusEffectController);

        if (_actorHud == null)
            GD.PushError($"{GetPath()}: missing required ActorHUD child.");
        else
            _actorHud.Bind(this);

        if (_visualSprite == null)
            GD.PushError($"{GetPath()}: missing required Sprite2D child.");

        if (_collisionShape == null)
            GD.PushError($"{GetPath()}: missing required CollisionShape2D child.");

        if (statusEffectController == null)
            GD.PushError($"{GetPath()}: missing required StatusEffectController child.");
        else
            BindStatusEffects(statusEffectController);

        AddToGroup(CombatGroups.Actors);
        ResetCombatState();
        UpdateHud();
        RefreshVisualState();

        if (_respawnTimer != null)
        {
            _respawnTimer.OneShot = true;
            _respawnTimer.WaitTime = Math.Max(0.01f, RespawnDelaySeconds);
            _respawnTimer.Timeout += OnRespawnTimerTimeout;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        Combat?.Update(delta);
    }

    public override void _ExitTree()
    {
        if (_respawnTimer != null)
            _respawnTimer.Timeout -= OnRespawnTimerTimeout;

        UnbindStatusEffects();
    }

    public void ApplyDamage(Damage damageInfo)
    {
        if (IsDead)
            return;

        var damage = HealthStateNode.ApplyDamage(damageInfo.Amount);
        damageInfo.RegisterHit(this, setReceiverTargetToSource: false);
        UpdateHud();
        _actorHud?.ShowFloatingText(damage.ToString(), new Color(1.0f, 0.0f, 0.0f, 1.0f));

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

        UpdateHud();
        _actorHud?.ShowFloatingText($"+{recovered}", new Color(0.0f, 1.0f, 0.0f, 1.0f));
    }

    private void StartDeath()
    {
        if (IsDead && _respawnTimer != null && _respawnTimer.TimeLeft > 0.0)
            return;

        HealthStateNode.SetDead(true);
        ResetCombatState();
        StatusEffectControllerNode?.ClearAllEffects();
        UpdateHud();
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
        UpdateHud();
        RefreshVisualState();
    }

    private void UpdateHud()
    {
        if (_actorHud == null)
            return;

        _actorHud.SetHealth(CurrentHealth, MaxHealableHealth);
        _actorHud.SetFaction(Faction);
    }

    private void BindStatusEffects(StatusEffectController statusEffectController)
    {
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

        _isSlowed = active;
        RefreshVisualState();
    }

    private void OnStatusFloatingTextRequested(string text, Color color)
    {
        _actorHud?.ShowFloatingText(text, color);
    }

    private void RefreshVisualState()
    {
        if (_visualSprite == null)
            return;

        _visualSprite.Texture = ResolveVisualTexture();
        _visualSprite.Modulate = ResolveVisualModulate();
    }

    private Color ResolveVisualModulate()
    {
        if (IsDead)
            return InactiveModulate;

        return _isSlowed ? SlowedSpriteTintColor : Colors.White;
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
