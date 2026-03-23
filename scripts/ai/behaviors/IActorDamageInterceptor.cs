public interface IActorDamageInterceptor
{
    bool TryHandleIncomingDamage(Actor actor, DamageInfo damageInfo, out IncomingDamageDecision decision);
}
