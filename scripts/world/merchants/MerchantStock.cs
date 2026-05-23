using Godot;

using System.Collections.Generic;

[GlobalClass]
public partial class MerchantStock : Node
{
    [Signal]
    public delegate void StockChangedEventHandler();

    private static readonly RandomNumberGenerator AppearanceRng = CreateAppearanceRng();

    [Export]
    public MerchantDefinition Definition { get; set; }

    // Optional override. If null, the stock falls back to the bound InventoryController's
    // GearGenerationRules at TryPurchase/TryRefresh time.
    [Export]
    public GearGenerationRules GearGenerationRules { get; set; }

    private readonly List<MerchantOffer> _offers = new();
    private bool _stockBuilt;

    public IReadOnlyList<MerchantOffer> Offers => _offers;

    public string DisplayName => Definition?.DisplayName ?? "Merchant";

    public int RefreshCost => Definition?.RefreshCost ?? 0;

    public override void _Ready()
    {
        EnsureStockBuilt();
    }

    public void EnsureStockBuilt()
    {
        if (_stockBuilt)
            return;

        RebuildStock();
    }

    public void RebuildStock()
    {
        _offers.Clear();

        if (Definition == null)
        {
            _stockBuilt = true;
            EmitSignal(SignalName.StockChanged);
            return;
        }

        foreach (var rule in Definition.StaticOffers)
        {
            var offer = BuildOffer(rule, MerchantOfferOrigin.Static);
            if (offer != null)
                _offers.Add(offer);
        }

        foreach (var rule in Definition.DynamicOffers)
        {
            if (rule == null)
                continue;
            if (!RollAppearance(rule.AppearanceChance))
                continue;

            var offer = BuildOffer(rule, MerchantOfferOrigin.Dynamic);
            if (offer != null)
                _offers.Add(offer);
        }

        _stockBuilt = true;
        EmitSignal(SignalName.StockChanged);
    }

    public bool TryPurchase(int offerIndex, InventoryController inventory)
    {
        if (inventory == null || !GodotObject.IsInstanceValid(inventory))
            return false;
        if (offerIndex < 0 || offerIndex >= _offers.Count)
            return false;

        var offer = _offers[offerIndex];
        if (offer == null || offer.Purchased)
            return false;

        if (!CanInventoryAccept(offer, inventory))
            return false;

        if (inventory.Gold < offer.Price)
            return false;

        if (!inventory.TrySpendGold(offer.Price))
            return false;

        var added = AddOfferToInventory(offer, inventory);
        if (!added)
        {
            // Capacity check passed but add failed (e.g. slot vacated between check and add).
            // Refund defensively so the player isn't out of gold for nothing.
            inventory.AddGold(offer.Price);
            return false;
        }

        offer.Purchased = true;
        EmitSignal(SignalName.StockChanged);
        return true;
    }

    public bool TryRefresh(InventoryController inventory)
    {
        if (inventory == null || !GodotObject.IsInstanceValid(inventory))
            return false;

        var cost = RefreshCost;
        if (cost > 0)
        {
            if (inventory.Gold < cost)
                return false;
            if (!inventory.TrySpendGold(cost))
                return false;
        }

        RebuildStock();
        return true;
    }

    public bool CanInventoryAccept(MerchantOffer offer, InventoryController inventory)
    {
        if (offer == null || inventory == null || !GodotObject.IsInstanceValid(inventory))
            return false;

        return offer.Kind switch
        {
            MerchantOfferKind.StackItem => offer.StackItem != null &&
                inventory.CanAddItem(offer.StackItem, offer.StackQuantity),
            MerchantOfferKind.GeneratedGear => offer.Gear != null &&
                inventory.CanAddGear(offer.Gear),
            _ => false,
        };
    }

    private bool AddOfferToInventory(MerchantOffer offer, InventoryController inventory)
    {
        switch (offer.Kind)
        {
            case MerchantOfferKind.StackItem:
                var remaining = inventory.AddItem(offer.StackItem, offer.StackQuantity);
                return remaining == 0;

            case MerchantOfferKind.GeneratedGear:
                return inventory.AddGear(offer.Gear);
        }

        return false;
    }

    private MerchantOffer BuildOffer(MerchantOfferRule rule, MerchantOfferOrigin origin)
    {
        if (rule == null)
            return null;

        switch (rule.Kind)
        {
            case MerchantOfferKind.StackItem:
                if (rule.StackItem == null)
                {
                    GD.PushWarning($"{nameof(MerchantStock)}: stack offer rule is missing an item; skipping.");
                    return null;
                }

                return new MerchantOffer
                {
                    Kind = MerchantOfferKind.StackItem,
                    Origin = origin,
                    Price = rule.Price,
                    StackItem = rule.StackItem,
                    StackQuantity = rule.StackQuantity,
                };

            case MerchantOfferKind.GeneratedGear:
                var rules = ResolveGearRules();
                if (rules == null)
                {
                    GD.PushWarning($"{nameof(MerchantStock)}: gear offer requires GearGenerationRules but none is available.");
                    return null;
                }

                var gear = GearGenerator.Generate(rule.GearSlot, rule.GearQuality, rules);
                if (gear == null)
                    return null;

                return new MerchantOffer
                {
                    Kind = MerchantOfferKind.GeneratedGear,
                    Origin = origin,
                    Price = rule.Price,
                    Gear = gear,
                };
        }

        return null;
    }

    private static bool RollAppearance(float chance)
    {
        if (chance >= 1.0f)
            return true;
        if (chance <= 0.0f)
            return false;
        return AppearanceRng.Randf() < chance;
    }

    private static RandomNumberGenerator CreateAppearanceRng()
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        return rng;
    }

    private GearGenerationRules ResolveGearRules()
    {
        if (GearGenerationRules != null)
            return GearGenerationRules;

        // Walk up to find a World, then ask its inventory controller for the rules resource.
        var current = (Node)this;
        while (current != null)
        {
            if (current is World world)
            {
                var inventory = world.ResolveInventoryController();
                if (inventory != null && inventory.GearGenerationRules != null)
                    return inventory.GearGenerationRules;
                break;
            }
            current = current.GetParent();
        }

        return null;
    }
}
