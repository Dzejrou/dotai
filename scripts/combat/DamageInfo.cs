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
}
