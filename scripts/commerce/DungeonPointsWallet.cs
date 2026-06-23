using Godot;

// Dungeon Points (DP) wallet for Dungeon Shop Buy transactions. Backs the shared commerce Buy
// path with the saved Dungeon Points balance, so a spend mutates the persisted total and normal
// save/load carries it. Only Buy is routed through this wallet; Sell and Buyback stay Gold-based
// through the InventoryController, so this wallet must never reach those paths. Keeps Dungeon
// coupling out of the commerce surface itself — only this small adapter knows about Dungeon.
public sealed class DungeonPointsWallet : ICurrencyWallet
{
    private readonly Dungeon _dungeon;

    public DungeonPointsWallet(Dungeon dungeon)
    {
        _dungeon = dungeon;
    }

    public string Label => "DP";

    public string Suffix => " DP";

    public int Balance => HasDungeon ? _dungeon.Points : 0;

    public bool CanAfford(int amount)
    {
        if (amount <= 0)
            return true;

        return Balance >= amount;
    }

    public bool TrySpend(int amount)
    {
        return HasDungeon && _dungeon.TrySpendPoints(amount);
    }

    public void Refund(int amount)
    {
        if (HasDungeon)
            _dungeon.RefundPoints(amount);
    }

    private bool HasDungeon => _dungeon != null && GodotObject.IsInstanceValid(_dungeon);
}
