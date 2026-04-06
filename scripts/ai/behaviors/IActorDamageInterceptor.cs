public interface IActorDamageInterceptor
{
    bool TryHandleIncomingDamage(Actor actor, Damage damageInfo, out IncomingDamageDecision decision);
}
