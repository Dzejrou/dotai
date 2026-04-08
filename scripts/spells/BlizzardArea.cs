using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class BlizzardArea : AreaOfEffect
{
    private readonly RandomNumberGenerator _random = new();

    [Export]
    public float ImmobilizeChance { get; set; } = 0.33f;

    public override void _Ready()
    {
        _random.Randomize();
        base._Ready();
    }

    protected override IEnumerable<StatusEffect> CreateStatusEffectsForTarget(Node2D target)
    {
        var templateName = _random.Randf() < Math.Clamp(ImmobilizeChance, 0.0f, 1.0f)
            ? "ImmobilizedEffect"
            : "SlowedEffect";

        if (DuplicateStatusTemplate(templateName) is StatusEffect effect)
            yield return effect;
    }
}
