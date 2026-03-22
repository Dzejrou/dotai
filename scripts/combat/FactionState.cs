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

        ApplyParentCombatGroup();
    }

    public void SetFaction(Faction faction)
    {
        Current = faction ?? ResolveConfiguredFaction();
        FactionKey = Current.Key;
        ApplyParentCombatGroup();
    }

    public bool IsHostileTo(Node node)
    {
        return Current != null && Current.IsHostileTo(Factions.ResolveForNode(node));
    }

    public bool IsFriendlyTo(Node node)
    {
        var otherFaction = Factions.ResolveForNode(node);
        return Current != null &&
               otherFaction != null &&
               !Current.IsHostileTo(otherFaction) &&
               !otherFaction.IsHostileTo(Current);
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

    private void ApplyParentCombatGroup()
    {
        var parentNode = GetParent();
        if (parentNode == null || !parentNode.IsInsideTree())
            return;

        Factions.ApplyCombatGroup(parentNode, Current);
    }
}
