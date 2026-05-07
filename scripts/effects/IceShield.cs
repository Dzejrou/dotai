using Godot;

using System;

[GlobalClass]
public partial class IceShield : Node2D, IDamageAbsorber
{
    private const string VisualNodePath = "AnimatedSprite2D";

    [Export]
    public int AbsorbAmount { get; set; } = 50;

    [Export]
    public float DurationSeconds { get; set; } = 30.0f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float VisualAlpha { get; set; } = 0.55f;

    public int RemainingAbsorbAmount { get; private set; }
    public float RemainingLifetimeSeconds { get; private set; }

    private AnimatedSprite2D _visual;

    public override void _Ready()
    {
        _visual = GetNodeOrNull<AnimatedSprite2D>(VisualNodePath);
        if (_visual == null)
            GD.PushError($"{GetPath()}: missing required {VisualNodePath} child.");

        RefreshShield();
    }

    public override void _Process(double delta)
    {
        if (RemainingLifetimeSeconds <= 0.0f)
            return;

        RemainingLifetimeSeconds = Math.Max(0.0f, RemainingLifetimeSeconds - (float)delta);
        if (RemainingLifetimeSeconds <= 0.0f)
            QueueFree();
    }

    public int AbsorbDamage(int incomingDamage)
    {
        var remainingDamage = Math.Max(0, incomingDamage);
        if (remainingDamage <= 0 || RemainingAbsorbAmount <= 0)
            return remainingDamage;

        var absorbedDamage = Math.Min(RemainingAbsorbAmount, remainingDamage);
        RemainingAbsorbAmount -= absorbedDamage;
        remainingDamage -= absorbedDamage;

        if (RemainingAbsorbAmount <= 0)
            QueueFree();

        return remainingDamage;
    }

    public void RefreshShield()
    {
        AbsorbAmount = Math.Max(0, AbsorbAmount);
        DurationSeconds = Math.Max(0.0f, DurationSeconds);
        VisualAlpha = Math.Clamp(VisualAlpha, 0.0f, 1.0f);

        RemainingAbsorbAmount = AbsorbAmount;
        RemainingLifetimeSeconds = DurationSeconds;

        if (_visual != null)
        {
            _visual.Modulate = new Color(1.0f, 1.0f, 1.0f, VisualAlpha);
            _visual.Play("default");
        }

        if (RemainingAbsorbAmount <= 0 || RemainingLifetimeSeconds <= 0.0f)
            QueueFree();
    }
}
