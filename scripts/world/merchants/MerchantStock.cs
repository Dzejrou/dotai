using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class MerchantStock : Node
{
    [Signal]
    public delegate void StockChangedEventHandler();

    private static readonly RandomNumberGenerator OfferRng = CreateOfferRng();

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

    // Buys the offer at offerIndex, paying through buyWallet. The wallet abstracts the Buy currency
    // (Gold for ordinary merchants, a future currency such as Dungeon Points elsewhere); item
    // delivery still goes through the InventoryController. Capacity is preflighted before any spend,
    // and a delivery failure after payment refunds through the same wallet.
    public bool TryPurchase(int offerIndex, InventoryController inventory, ICurrencyWallet buyWallet)
    {
        if (inventory == null || !GodotObject.IsInstanceValid(inventory))
            return false;
        if (buyWallet == null)
            return false;
        if (offerIndex < 0 || offerIndex >= _offers.Count)
            return false;

        var offer = _offers[offerIndex];
        if (offer == null || offer.Purchased)
            return false;

        if (!CanInventoryAccept(offer, inventory))
            return false;

        if (!buyWallet.CanAfford(offer.Price))
            return false;

        if (!buyWallet.TrySpend(offer.Price))
            return false;

        var added = AddOfferToInventory(offer, inventory);
        if (!added)
        {
            // Capacity check passed but add failed (e.g. slot vacated between check and add).
            // Refund defensively so the player isn't out of currency for nothing.
            buyWallet.Refund(offer.Price);
            return false;
        }

        // Limited offers sell out until the next refresh rebuilds stock; unlimited offers stay
        // buyable and are never marked purchased.
        if (offer.StockMode == MerchantOfferStockMode.Limited)
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
                    StockMode = rule.StockMode,
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

                var slot = ResolveOfferSlot(rule);
                var quality = ResolveOfferQuality(rule);
                var gear = GearGenerator.Generate(slot, quality, rules);
                if (gear == null)
                    return null;

                return new MerchantOffer
                {
                    Kind = MerchantOfferKind.GeneratedGear,
                    Origin = origin,
                    StockMode = rule.StockMode,
                    Price = rule.Price,
                    Gear = gear,
                    RevealedSubstatCount = rule.RevealedSubstatCount,
                };
        }

        return null;
    }

    // Resolves the equipment slot for a generated-gear rule. Centralized so future
    // weighted distributions can plug in without touching BuildOffer.
    private static EquipmentSlot ResolveOfferSlot(MerchantOfferRule rule)
    {
        return rule.SlotMode switch
        {
            MerchantOfferSlotMode.RandomSlot => PickUniformSlot(),
            _ => rule.GearSlot,
        };
    }

    // Resolves the gear quality for a generated-gear rule. MinimumQuality treats Trash
    // as Common since Trash gear is not worth surfacing in merchant stock.
    private static ItemQuality ResolveOfferQuality(MerchantOfferRule rule)
    {
        return rule.QualityMode switch
        {
            MerchantOfferQualityMode.RandomQuality =>
                PickUniformQuality(ItemQuality.Common, ItemQuality.Legendary),
            MerchantOfferQualityMode.MinimumQuality =>
                PickUniformQuality(
                    rule.Quality == ItemQuality.Trash ? ItemQuality.Common : rule.Quality,
                    ItemQuality.Legendary),
            _ => rule.Quality,
        };
    }

    private static EquipmentSlot PickUniformSlot()
    {
        var values = Enum.GetValues<EquipmentSlot>();
        return values[OfferRng.RandiRange(0, values.Length - 1)];
    }

    private static ItemQuality PickUniformQuality(ItemQuality min, ItemQuality max)
    {
        var lo = (int)min;
        var hi = (int)max;
        if (hi < lo)
            (lo, hi) = (hi, lo);
        return (ItemQuality)OfferRng.RandiRange(lo, hi);
    }

    private static bool RollAppearance(float chance)
    {
        if (chance >= 1.0f)
            return true;
        if (chance <= 0.0f)
            return false;
        return OfferRng.Randf() < chance;
    }

    private static RandomNumberGenerator CreateOfferRng()
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
