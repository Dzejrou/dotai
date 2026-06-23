// Abstraction over the currency a Buy transaction is paid in. Lets the shared commerce logic
// (offer presentation and MerchantStock.TryPurchase) spend, refund and report a balance without
// knowing whether the source pays in Gold (ordinary merchants) or a future currency such as
// Dungeon Points. Sell and Buyback are intentionally not routed through this seam; they stay
// Gold-based regardless of the Buy wallet.
public interface ICurrencyWallet
{
    // Human-readable currency name, e.g. "Gold".
    string Label { get; }

    // Short price suffix shown next to amounts, e.g. "g".
    string Suffix { get; }

    // Current spendable balance. Returns 0 when the backing source is unavailable.
    int Balance { get; }

    // True when the balance can cover amount. A non-positive amount is always affordable.
    bool CanAfford(int amount);

    // Spends amount, returning whether the spend happened. Must not partially spend.
    bool TrySpend(int amount);

    // Credits amount back, used to roll back a spend when delivery fails after payment.
    void Refund(int amount);
}
