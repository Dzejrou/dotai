using Godot;

using System;

[GlobalClass]
public partial class Damage : Node
{
    [Export]
    public int FlatDamage { get; set; } = 0;

    [Export]
    public float PowerScale { get; set; } = 1.0f;

    [Export]
    public float MinDamageMultiplier { get; set; } = 1.0f;

    [Export]
    public float MaxDamageMultiplier { get; set; } = 1.0f;

    [Export]
    public DamageSchool School { get; set; } = DamageSchool.Physical;

    [Export]
    public bool CanCrit { get; set; } = true;

    private static readonly RandomNumberGenerator CritRng = CreateRng();
    private static readonly RandomNumberGenerator RollRng = CreateRng();

    public int Amount { get; private set; }

    public int BaseAmount { get; private set; }

    public bool IsCritical { get; private set; }

    public Node Source { get; private set; }

    public ulong SourceInstanceId { get; private set; }

    private float _sourceCritRate;
    private float _sourceCritDamage;
    private bool _sourceIsCombatCharacter;
    private bool _critResolved;

    public void InitializeRuntime(Node source)
    {
        Source = source;
        SourceInstanceId = source != null && GodotObject.IsInstanceValid(source)
            ? source.GetInstanceId()
            : 0UL;

        var sourcePower = 0.0f;
        if (source is CombatCharacter combatCharacter)
        {
            _sourceIsCombatCharacter = true;
            _sourceCritRate = combatCharacter.ResolvedCritRate;
            _sourceCritDamage = combatCharacter.ResolvedCritDamage;
            sourcePower = combatCharacter.ResolvedPower;
        }
        else
        {
            _sourceIsCombatCharacter = false;
            _sourceCritRate = 0.0f;
            _sourceCritDamage = 0.0f;
        }

        BaseAmount = Math.Max(0, ResolveBaseAmount(sourcePower));
        Amount = BaseAmount;
        IsCritical = false;
        _critResolved = false;
    }

    private int ResolveBaseAmount(float sourcePower)
    {
        var scaledDamage = sourcePower * PowerScale + FlatDamage;
        var maxMultiplier = Math.Max(MinDamageMultiplier, MaxDamageMultiplier);
        var minMultiplier = Math.Min(MinDamageMultiplier, maxMultiplier);
        var rolledMultiplier = minMultiplier >= maxMultiplier
            ? maxMultiplier
            : RollRng.RandfRange(minMultiplier, maxMultiplier);

        var finalDamage = scaledDamage * rolledMultiplier;
        return (int)Math.Round(finalDamage);
    }

    // TODO: Status-effect/DoT damage is currently hard non-critting via CanCrit = false set in
    // StatusEffect.DuplicateDamagePayload(). This blocks both normal source crit and Ice-vs-Frozen
    // forced crit. Future buffs may make this configurable per status (e.g. "DoTs can crit"),
    // at which point CanCrit should be driven by those buffs rather than forced off.
    public void ResolveCritForReceiver(Node2D receiver)
    {
        if (_critResolved)
            return;

        _critResolved = true;
        IsCritical = false;
        Amount = BaseAmount;

        if (BaseAmount <= 0 || !_sourceIsCombatCharacter || !CanCrit)
            return;

        var forced = School == DamageSchool.Ice && ReceiverHasFrozen(receiver);
        var shouldCrit = forced || (_sourceCritRate > 0.0f && CritRng.Randf() < _sourceCritRate);
        if (!shouldCrit)
            return;

        IsCritical = true;
        var multiplier = 1.0f + _sourceCritDamage;
        Amount = Math.Max(BaseAmount, (int)Math.Round(BaseAmount * multiplier));
    }

    private static bool ReceiverHasFrozen(Node2D receiver)
    {
        if (receiver == null || !GodotObject.IsInstanceValid(receiver))
            return false;

        var controller = receiver.GetNodeOrNull<StatusEffectController>("StatusEffectController");
        return controller != null && controller.HasStatus(FrozenEffect.StatusKeyName);
    }

    private static RandomNumberGenerator CreateRng()
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        return rng;
    }

    public void RegisterHit(Node2D receiver, bool setReceiverTargetToSource = true)
    {
        var receiverCombatState = CombatState.ResolveFor(receiver);
        receiverCombatState?.RegisterIncomingDamage(Source as Node2D, setReceiverTargetToSource);

        var sourceCombatState = CombatState.ResolveFor(Source);
        sourceCombatState?.RegisterOutgoingDamage(receiver);
    }

    public static Damage DuplicateFrom(Node owner)
    {
        if (owner == null || !GodotObject.IsInstanceValid(owner))
            return null;

        return owner.GetNodeOrNull<Damage>("Damage")?.Duplicate() as Damage;
    }
}
