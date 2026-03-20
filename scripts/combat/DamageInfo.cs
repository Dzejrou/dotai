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
        if (receiver is ICombatStateOwner receiverCombatOwner)
            receiverCombatOwner.Combat.RegisterIncomingDamage(Source as Node2D, setReceiverTargetToSource);

        if (Source is ICombatStateOwner sourceCombatOwner)
            sourceCombatOwner.Combat.RegisterOutgoingDamage(receiver);
    }
}
