using Godot;

// Gold-backed currency wallet over an InventoryController. Buy transactions for ordinary
// merchants pay through this so the shared commerce logic stays currency-agnostic. Sell and
// Buyback also operate on Gold but use the InventoryController directly; a GoldWallet may be
// passed to them too, but the future Dungeon Points buy wallet must never reach those paths.
public sealed class GoldWallet : ICurrencyWallet
{
    private readonly InventoryController _inventory;

    public GoldWallet(InventoryController inventory)
    {
        _inventory = inventory;
    }

    public string Label => "Gold";

    public string Suffix => "g";

    public int Balance => HasInventory ? _inventory.Gold : 0;

    public bool CanAfford(int amount)
    {
        if (amount <= 0)
            return true;

        return Balance >= amount;
    }

    public bool TrySpend(int amount)
    {
        return HasInventory && _inventory.TrySpendGold(amount);
    }

    public void Refund(int amount)
    {
        if (HasInventory)
            _inventory.AddGold(amount);
    }

    private bool HasInventory => _inventory != null && GodotObject.IsInstanceValid(_inventory);
}
