using Godot;

[GlobalClass]
public partial class GoldSackDrop : Drop
{
    private static readonly RandomNumberGenerator GoldRandom = CreateGoldRandom();

    [Export]
    public int GoldAmount { get; set; } = 5;

    protected override void ApplyTo(Player player)
    {
        player.AddGold(ResolveGrantedGoldAmount());
    }

    private int ResolveGrantedGoldAmount()
    {
        var maxAmount = Mathf.Max(0, GoldAmount);
        if (maxAmount <= 0)
            return 0;

        var minAmount = Mathf.Max(1, Mathf.CeilToInt(maxAmount * 0.5f));
        return GoldRandom.RandiRange(minAmount, maxAmount);
    }

    private static RandomNumberGenerator CreateGoldRandom()
    {
        var random = new RandomNumberGenerator();
        random.Randomize();
        return random;
    }
}
