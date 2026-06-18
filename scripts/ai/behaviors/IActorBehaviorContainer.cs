// Marker for a behavior node that owns and forwards to its own child behaviors.
// Actor's recursive behavior discovery collects the container itself but does not
// descend into its children, so wrapped behaviors are never collected (and executed)
// twice - once directly and once through the container. A container is responsible
// for resolving its children and forwarding intent/tick to them.
public interface IActorBehaviorContainer
{
}
