using Godot;

using System;

public sealed class SingleOwnedSummonBehavior : IActorBehavior, IActorTickBehavior
{
    private readonly PackedScene _summonScene;
    private readonly float _spawnOffset;
    private readonly float _triggerRange;
    private readonly float _resummonDelaySeconds;
    private readonly Func<ActorBase, ISummoner> _summonerGetter;
    private Node2D _activeSummon;
    private float _resummonCooldownTimer;

    public SingleOwnedSummonBehavior(
        PackedScene summonScene,
        float spawnOffset,
        float triggerRange,
        float resummonDelaySeconds,
        Func<ActorBase, ISummoner> summonerGetter)
    {
        _summonScene = summonScene;
        _spawnOffset = Math.Max(0.0f, spawnOffset);
        _triggerRange = Math.Max(0.0f, triggerRange);
        _resummonDelaySeconds = Math.Max(0.0f, resummonDelaySeconds);
        _summonerGetter = summonerGetter ?? throw new ArgumentNullException(nameof(summonerGetter));
    }

    public bool TryCreateIntent(ActorBase actor, double delta, out ActorIntent intent)
    {
        intent = ActorIntent.None;
        return false;
    }

    public void Update(ActorBase actor, double delta)
    {
        if (actor.IsDead)
            return;

        if (_resummonCooldownTimer > 0.0f)
            _resummonCooldownTimer -= (float)delta;

        if (IsActiveSummon(_activeSummon))
            return;

        if (_activeSummon != null)
        {
            _activeSummon = null;
            _resummonCooldownTimer = Math.Max(_resummonCooldownTimer, _resummonDelaySeconds);
        }

        if (_resummonCooldownTimer > 0.0f ||
            actor.CurrentState == CombatUnitState.Attacking ||
            actor.Target == null ||
            actor.Target is not ITargetable targetable ||
            !targetable.CanBeTargeted ||
            actor.GlobalPosition.DistanceTo(actor.Target.GlobalPosition) > _triggerRange)
        {
            return;
        }

        var parent = actor.GetParent();
        var summoner = _summonerGetter(actor);
        if (parent == null || summoner == null || _summonScene == null)
            return;

        var summonedNode = _summonScene.Instantiate<Node2D>();
        if (summonedNode == null)
            return;

        var summonDirection = DirectionHelper.GetDirectionVector(actor.LastDirection);
        if (summonDirection == Vector2.Zero && actor.Target.GlobalPosition != actor.GlobalPosition)
            summonDirection = (actor.Target.GlobalPosition - actor.GlobalPosition).Normalized();
        if (summonDirection == Vector2.Zero)
            summonDirection = Vector2.Right;

        summonedNode.GlobalPosition = actor.GlobalPosition + summonDirection.Normalized() * _spawnOffset;
        if (!SummonState.TryAssignToNode(summonedNode, summoner) &&
            summonedNode is ISummonedUnit summonedUnit)
        {
            summonedUnit.SetSummoner(summoner);
        }
        parent.AddChild(summonedNode);
        _activeSummon = summonedNode;
    }

    private static bool IsActiveSummon(Node2D summonedNode)
    {
        if (summonedNode == null || !GodotObject.IsInstanceValid(summonedNode) || !summonedNode.IsInsideTree())
            return false;

        if (summonedNode is not ITargetable targetable || !targetable.CanBeTargeted)
            return false;

        var summonState = SummonState.ResolveFor(summonedNode);
        if (summonState != null)
            return summonState.HasValidSummoner();

        return summonedNode is not ISummonedUnit summonedUnit || summonedUnit.HasValidSummoner();
    }
}
