using Godot;

using System;

[GlobalClass]
public partial class MerchantDefinition : Resource
{
    [Export]
    public string DisplayName { get; set; } = "Merchant";

    [Export]
    public int RefreshCost
    {
        get => _refreshCost;
        set => _refreshCost = Math.Max(0, value);
    }

    [Export]
    public Godot.Collections.Array<MerchantOfferRule> StaticOffers { get; set; } = new();

    [Export]
    public Godot.Collections.Array<MerchantOfferRule> DynamicOffers { get; set; } = new();

    private int _refreshCost = 25;
}
