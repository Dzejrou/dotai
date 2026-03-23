using Godot;

[GlobalClass]
public partial class FactionState : Node
{
    [Export]
    public string FactionKey { get; set; } = "enemies";

    public Faction Current { get; private set; }

    public override void _Ready()
    {
        if (Current == null)
            Current = ResolveConfiguredFaction();
        else
            FactionKey = Current.Key;
    }

    public void SetFaction(Faction faction)
    {
        Current = faction ?? ResolveConfiguredFaction();
        FactionKey = Current.Key;
    }

    public bool IsHostileTo(Node node)
    {
        return Current != null &&
               node is IFactionMember factionMember &&
               factionMember.Faction != null &&
               Current.IsHostileTo(factionMember.Faction);
    }

    public bool IsFriendlyTo(Node node)
    {
        return Current != null &&
               node is IFactionMember factionMember &&
               factionMember.Faction != null &&
               Current.IsFriendlyTo(factionMember.Faction);
    }

    public static FactionState ResolveFor(Node node)
    {
        if (node == null || !GodotObject.IsInstanceValid(node))
            return null;

        if (node is FactionState factionState)
            return factionState;

        return node.GetNodeOrNull<FactionState>("FactionState");
    }

    private Faction ResolveConfiguredFaction()
    {
        return Factions.Get(FactionKey) ?? Factions.Enemies;
    }
}
