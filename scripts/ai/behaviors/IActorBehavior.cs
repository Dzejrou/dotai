public interface IActorBehavior
{
    bool TryCreateIntent(Actor actor, double delta, out ActorIntent intent);
}
