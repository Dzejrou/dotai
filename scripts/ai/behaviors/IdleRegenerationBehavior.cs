using System;

public sealed class IdleRegenerationBehavior : IActorBehavior, IActorTickBehavior
{
    private readonly float _fractionPerSecond;
    private readonly float _intervalSeconds;
    private float _timer;

    public IdleRegenerationBehavior(float fractionPerSecond, float intervalSeconds)
    {
        _fractionPerSecond = Math.Max(0.0f, fractionPerSecond);
        _intervalSeconds = Math.Max(0.01f, intervalSeconds);
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
            actor.CurrentHealth >= actor.ResolvedMaxHealth ||
            actor.CurrentState != CombatUnitState.Idle ||
            actor.Target != null)
        {
            _timer = 0.0f;
            return;
        }

        _timer += Math.Max(0.0f, (float)delta);
        var tickCount = (int)MathF.Floor(_timer / _intervalSeconds);
        if (tickCount <= 0)
            return;

        _timer -= tickCount * _intervalSeconds;
        var healPerTick = Math.Max(1, (int)MathF.Round(actor.ResolvedMaxHealth * _fractionPerSecond));
        actor.ApplyHealing(Math.Min(actor.ResolvedMaxHealth - actor.CurrentHealth, tickCount * healPerTick));
    }
}
