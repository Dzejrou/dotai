using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class BlizzardArea : AreaOfEffect
{
    private static readonly StringName DefaultAnimationName = "default";

    private readonly RandomNumberGenerator _random = new();
    private AnimatedSprite2D _sprite;

    [Export]
    public float ImmobilizeChance { get; set; } = 0.33f;

    public override void _Ready()
    {
        _random.Randomize();
        base._Ready();
    }

    protected override void OnAreaReady()
    {
        _sprite ??= GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        _sprite?.Play(DefaultAnimationName);
    }

    protected override void OnRuntimeInitialized()
    {
        if (_sprite != null)
        {
            _sprite.Visible = true;
            _sprite.Play(DefaultAnimationName);
        }
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
