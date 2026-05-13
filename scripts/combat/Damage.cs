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

    public int Amount { get; private set; }

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
        Amount = Math.Max(0, amount);
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
