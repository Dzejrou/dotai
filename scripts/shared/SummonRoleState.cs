using Godot;

using System;

public sealed class SummonRoleState
{
    private ISummoner _summoner;
    private Node2D _commandedTarget;

    public ISummoner Summoner => _summoner;
    public Node2D SummonerNode => _summoner?.SummonerNode;
    public bool IsSummoned => _summoner != null;

    public void SetSummoner(ISummoner summoner, Action<Faction> inheritFaction = null)
    {
        if (!ReferenceEquals(_summoner, summoner))
            _commandedTarget = null;

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

    public void SetCommandedTarget(Node2D target)
    {
        _commandedTarget = target;
    }

    public Node2D GetCommandedTarget(Func<Node2D, bool> validator)
    {
        if (!validator(_commandedTarget))
        {
            _commandedTarget = null;
            return null;
        }

        return _commandedTarget;
    }

    public void ClearCommandedTarget()
    {
        _commandedTarget = null;
    }
}
