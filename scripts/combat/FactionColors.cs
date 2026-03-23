using Godot;

public static class FactionColors
{
    private static readonly Color AllyColor = new Color(0.45f, 0.95f, 0.45f, 1.0f);
    private static readonly Color EnemyColor = new Color(1.0f, 0.38f, 0.38f, 1.0f);
    private static readonly Color NeutralColor = new Color(1.0f, 0.9f, 0.35f, 1.0f);
    private static readonly Color UnknownColor = Colors.White;

    public static Color Resolve(Faction faction)
    {
        if (ReferenceEquals(faction, Factions.Allies))
            return AllyColor;

        if (ReferenceEquals(faction, Factions.Enemies))
            return EnemyColor;

        if (ReferenceEquals(faction, Factions.Neutral))
            return NeutralColor;

        return UnknownColor;
    }

    public static Color Resolve(Node node)
    {
        return Resolve(node is IFactionMember factionMember ? factionMember.Faction : null);
    }
}
