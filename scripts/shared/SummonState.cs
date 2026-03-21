using Godot;

using System;

[GlobalClass]
public partial class SummonState : Node
{
    private ISummoner _summoner;

    public ISummoner Summoner => _summoner;
    public Node2D SummonerNode => _summoner?.SummonerNode;
    public bool IsSummoned => _summoner != null;

    public void SetSummoner(ISummoner summoner, Action<Faction> inheritFaction = null)
    {
        _summoner = summoner;
        if (summoner is IFactionMember factionMember)
            inheritFaction?.Invoke(factionMember.Faction);
    }

    public bool HasValidSummoner()
    {
        return _summoner != null &&
               GodotObject.IsInstanceValid(_summoner.SummonerNode) &&
               _summoner.IsSummonerActive;
    }

    public bool IsOwnedBy(Node2D owner)
    {
        return owner != null && SummonerNode == owner;
    }

    public static SummonState ResolveFor(ActorBase actor)
    {
        if (actor == null || !GodotObject.IsInstanceValid(actor))
            return null;

        return actor.GetNodeOrNull<SummonState>("SummonState");
    }
}
