using Godot;

public struct DamageInfo
{
    public int Amount { get; }

    public Node Source { get; }

    public DamageInfo(int amount, Node source = null)
    {
        Amount = amount;
        Source = source;
    }

    public DamageInfo(int amount, Node2D source)
        : this(amount, (Node)source)
    {
    }

    public void RegisterHit(Node2D receiver, bool setReceiverTargetToSource = true)
    {
        var receiverCombatState = CombatState.ResolveFor(receiver);
        receiverCombatState?.RegisterIncomingDamage(Source as Node2D, setReceiverTargetToSource);

        var sourceCombatState = CombatState.ResolveFor(Source);
        sourceCombatState?.RegisterOutgoingDamage(receiver);
    }
}
