using Godot;

public abstract partial class CombatCharacter : AnimatedCharacter, IFactionMember, IHealable
{
    protected HealthState HealthStateNode { get; private set; }
    protected CombatState CombatStateNode { get; private set; }
    protected FactionState FactionStateNode { get; private set; }
    protected ManaState ManaStateNode { get; private set; }
    protected StatusEffectController StatusEffectControllerNode { get; private set; }

    public CombatState Combat => CombatStateNode;
    public bool InCombat => CombatStateNode?.InCombat ?? false;
    public Faction Faction => FactionStateNode?.Current;
    public FactionState FactionState => FactionStateNode;
    public ManaState ManaState => ManaStateNode;
    public int CurrentHealth => HealthStateNode?.Current ?? 0;
    public int MaxHealthValue => HealthStateNode?.Max ?? 0;
    public int MaxHealableHealth => MaxHealthValue;
    public int CurrentMana => ManaStateNode?.Current ?? 0;
    public int MaxManaValue => ManaStateNode?.Max ?? 0;
    public bool IsDead => HealthStateNode?.IsDead ?? false;
    public bool CanReceiveHealing => !IsDead && CurrentHealth < MaxHealableHealth;
    public virtual bool CanMove => StatusEffectControllerNode?.CanMove() ?? true;
    public virtual float MovementSpeedMultiplier => StatusEffectControllerNode?.GetMovementSpeedMultiplier() ?? 1.0f;
    public virtual float AttackSpeedMultiplier => StatusEffectControllerNode?.GetAttackSpeedMultiplier() ?? 1.0f;
    public virtual float CastSpeedMultiplier => StatusEffectControllerNode?.GetCastSpeedMultiplier() ?? 1.0f;

    protected void InitializeCombatCharacter(bool requireManaState = false)
    {
        CombatStateNode = GetNode<CombatState>("CombatState");
        HealthStateNode = GetNode<HealthState>("HealthState");
        HealthStateNode.Initialize();
        FactionStateNode = GetNode<FactionState>("FactionState");
        ManaStateNode = requireManaState
            ? GetNode<ManaState>("ManaState")
            : GetNodeOrNull<ManaState>("ManaState");
        ManaStateNode?.Initialize();
    }

    protected void ResetCombatState()
    {
        CombatStateNode?.ClearTarget();
        CombatStateNode?.ExitCombat();
    }

    protected void SetStatusEffectController(StatusEffectController statusEffectController)
    {
        StatusEffectControllerNode = statusEffectController;
    }

    public abstract void ApplyHealing(int amount);
}
