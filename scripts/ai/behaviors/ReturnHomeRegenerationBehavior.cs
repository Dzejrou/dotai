using System;

public sealed class ReturnHomeRegenerationBehavior : IActorBehavior, IActorTickBehavior
{
    private readonly float _fractionPerSecond;
    private float _timer;

    public ReturnHomeRegenerationBehavior(float fractionPerSecond)
    {
        _fractionPerSecond = Math.Max(0.0f, fractionPerSecond);
    }

    public bool TryCreateIntent(Actor actor, double delta, out ActorIntent intent)
    {
        intent = ActorIntent.None;
        return false;
    }

    public void Update(Actor actor, double delta)
    {
        if (_fractionPerSecond <= 0.0f ||
            actor.IsDead ||
            actor.InCombat ||
            actor.CurrentHealth >= actor.ResolvedMaxHealth ||
            actor.CurrentState != CombatUnitState.ReturningHome)
        {
            _timer = 0.0f;
            return;
        }

        _timer += Math.Max(0.0f, (float)delta);
        var tickCount = (int)MathF.Floor(_timer);
        if (tickCount <= 0)
            return;

        _timer -= tickCount;
        var healPerTick = Math.Max(1, (int)MathF.Round(actor.ResolvedMaxHealth * _fractionPerSecond));
        actor.ApplyHealing(Math.Min(actor.ResolvedMaxHealth - actor.CurrentHealth, tickCount * healPerTick));
    }
}
