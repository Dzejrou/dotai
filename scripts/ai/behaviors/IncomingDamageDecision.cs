using Godot;

public readonly struct IncomingDamageDecision
{
    public bool AllowDamage { get; init; }
    public Node2D RetargetTo { get; init; }
    public string FloatingText { get; init; }
    public Color FloatingTextColor { get; init; }

    public static IncomingDamageDecision Allow()
    {
        return new IncomingDamageDecision
        {
            AllowDamage = true,
        };
    }

    public static IncomingDamageDecision AllowWithRetarget(Node2D target)
    {
        return new IncomingDamageDecision
        {
            AllowDamage = true,
            RetargetTo = target,
        };
    }

    public static IncomingDamageDecision Deny(string floatingText, Color floatingTextColor)
    {
        return new IncomingDamageDecision
        {
            AllowDamage = false,
            FloatingText = floatingText,
            FloatingTextColor = floatingTextColor,
        };
    }
}
