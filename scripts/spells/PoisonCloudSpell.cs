using Godot;

using System;

[GlobalClass]
public partial class PoisonCloudSpell : Spell
{
    [Export]
    public PackedScene AreaScene { get; set; }

    [Export]
    public int ManaCost { get; set; } = 8;

    [Export]
    public float CloudRadius { get; set; } = 48.0f;

    [Export]
    public float CloudLifetime { get; set; } = 14.0f;

    [Export]
    public float PoisonDuration { get; set; } = 10.0f;

    [Export]
    public float PoisonTickInterval { get; set; } = 2.0f;

    [Export]
    public int PoisonDamagePerTick { get; set; } = 5;

    public override int DisplayManaCost => Math.Max(0, ManaCost);

    public override bool CanCast(ISpellCaster caster)
    {
        if (!base.CanCast(caster) || AreaScene == null)
            return false;

        var manaState = caster.ManaState;
        var sourceFaction = caster.Faction;
        var target = caster.SpellTarget;
        return manaState != null &&
               sourceFaction != null &&
               target != null &&
               GodotObject.IsInstanceValid(target) &&
               target.IsInsideTree() &&
               manaState.Current >= Math.Max(0, ManaCost);
    }

    public override bool TryCast(ISpellCaster caster)
    {
        if (!CanCast(caster))
            return false;

        var target = caster.SpellTarget;
        if (target == null || !GodotObject.IsInstanceValid(target) || !target.IsInsideTree())
            return false;

        var poisonCloud = AreaScene.Instantiate<PoisonCloudArea>();
        if (poisonCloud == null)
            return false;

        var parent = caster.SpellOrigin.GetParent();
        if (parent == null)
        {
            poisonCloud.QueueFree();
            return false;
        }

        if (!TrySpendCastMana(caster, ManaCost))
        {
            poisonCloud.QueueFree();
            return false;
        }

        parent.AddChild(poisonCloud);
        poisonCloud.GlobalPosition = target.GlobalPosition;
        poisonCloud.CloudRadius = CloudRadius;
        poisonCloud.CloudLifetime = CloudLifetime;
        poisonCloud.PoisonDuration = PoisonDuration;
        poisonCloud.PoisonTickInterval = PoisonTickInterval;
        poisonCloud.PoisonDamagePerTick = PoisonDamagePerTick;
        poisonCloud.Initialize(caster.SpellOrigin, caster.Faction);

        return true;
    }
}
