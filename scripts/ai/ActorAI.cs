using Godot;

public abstract class ActorAI
{
    protected ActorBase Actor { get; private set; }

    public void Initialize(ActorBase actor)
    {
        if (ReferenceEquals(Actor, actor))
            return;

        Shutdown();
        Actor = actor;
        OnInitialize();
    }

    public virtual void Update(double delta) { }

    public virtual bool TryAcquireTarget() => false;

    public virtual bool TryGetDesiredMovementTarget(Vector2 targetPosition, double delta, out Vector2 desiredMovementTarget)
    {
        desiredMovementTarget = Vector2.Zero;
        return false;
    }

    public void Shutdown()
    {
        if (Actor == null)
            return;

        OnShutdown();
        Actor = null;
    }

    protected virtual void OnInitialize() { }

    protected virtual void OnShutdown() { }
}
