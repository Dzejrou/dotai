using Godot;

using System;

[GlobalClass]
public partial class Damage : Node
{
    [Export]
    public int MinimumDamage { get; set; } = 1;

    [Export]
    public int MaximumDamage { get; set; } = 1;

    [Export]
    public DamageSchool School { get; set; } = DamageSchool.Physical;

    private static readonly RandomNumberGenerator CritRng = CreateCritRng();

    public int Amount { get; private set; }

    public int BaseAmount { get; private set; }

    public bool IsCritical { get; private set; }

    public Node Source { get; private set; }

    public ulong SourceInstanceId { get; private set; }

    public int ResolveAmount(RandomNumberGenerator randomNumberGenerator = null)
    {
        var maximumDamage = Math.Max(MinimumDamage, MaximumDamage);
        var minimumDamage = Math.Min(MinimumDamage, maximumDamage);
        if (randomNumberGenerator != null)
            return Math.Max(1, randomNumberGenerator.RandiRange(minimumDamage, maximumDamage));

        return Math.Max(1, maximumDamage);
    }

    public void InitializeRuntime(Node source, int amount)
    {
        Source = source;
        SourceInstanceId = source != null && GodotObject.IsInstanceValid(source)
            ? source.GetInstanceId()
            : 0UL;
        BaseAmount = Math.Max(0, amount);
        ResolveCrit();
    }

    private void ResolveCrit()
    {
        IsCritical = false;
        Amount = BaseAmount;

        if (BaseAmount <= 0 || Source is not CombatCharacter combatCharacter)
            return;

        var critRate = combatCharacter.ResolvedCritRate;
        if (critRate <= 0.0f)
            return;

        if (CritRng.Randf() >= critRate)
            return;

        IsCritical = true;
        var multiplier = 1.0f + combatCharacter.ResolvedCritDamage;
        Amount = Math.Max(BaseAmount, (int)Math.Round(BaseAmount * multiplier));
    }

    private static RandomNumberGenerator CreateCritRng()
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

        if (owner.GetNodeOrNull<Damage>("Damage")?.Duplicate() is not Damage damage)
            return null;

        damage.ApplyResolvedSchool(owner);
        return damage;
    }

    public void ApplyResolvedSchool(Node owner)
    {
        var ownerSchool = DamageSchoolTag.Resolve(owner);
        if (ownerSchool.HasValue)
            School = ownerSchool.Value;
    }
}
