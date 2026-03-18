public interface IActorBehavior
{
    bool TryCreateIntent(ActorBase actor, double delta, out ActorIntent intent);
}
