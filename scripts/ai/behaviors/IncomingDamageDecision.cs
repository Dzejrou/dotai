using Godot;

public readonly struct IncomingDamageDecision
{
    public bool AllowDamage { get; init; }
    public Node2D RetargetTo { get; init; }
    public string FloatingText { get; init; }
    public Color FloatingTextColor { get; init; }

    // When set, the prevented hit is reported through the shared absorbed-hit
    // feedback path (ABSORB floating text + combat-log entry) instead of a damage
    // number, so fully prevented damage is never silent.
    public bool ReportAbsorbed { get; init; }
    public int AbsorbedAmount { get; init; }

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

    // Prevents the hit and reports it as absorbed/prevented through the shared
    // combat-feedback path rather than a normal damage number.
    public static IncomingDamageDecision Absorb(int amount)
    {
        return new IncomingDamageDecision
        {
            AllowDamage = false,
            ReportAbsorbed = true,
            AbsorbedAmount = amount < 0 ? 0 : amount,
        };
    }
}
