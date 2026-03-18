public interface IActorDamageInterceptor
{
    bool TryHandleIncomingDamage(ActorBase actor, DamageInfo damageInfo, out IncomingDamageDecision decision);
}
