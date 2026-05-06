using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class IceSpikesArea : AreaOfEffect
{
    private static readonly StringName DefaultAnimationName = "default";

    private readonly RandomNumberGenerator _random = new();
    private OmniSprite _omniSprite;

    [Export]
    public float ImmobilizeChance { get; set; } = 0.33f;

    public override void _Ready()
    {
        _random.Randomize();
        base._Ready();
    }

    protected override void OnAreaReady()
    {
        _omniSprite ??= GetNodeOrNull<OmniSprite>("OmniSprite");
        _omniSprite?.TryPlay(DefaultAnimationName);
    }

    protected override void OnRuntimeInitialized()
    {
        if (_omniSprite != null)
        {
            _omniSprite.Visible = true;
            _omniSprite.TryPlay(DefaultAnimationName);
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
