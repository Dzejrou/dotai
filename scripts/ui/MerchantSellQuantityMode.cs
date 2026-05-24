using System;

public enum MerchantSellQuantityMode
{
    One = 0,
    Five = 1,
    Ten = 2,
    Fifty = 3,
    All = 4,
}

public static class MerchantSellQuantityModeExtensions
{
    public static MerchantSellQuantityMode Next(this MerchantSellQuantityMode mode) => mode switch
    {
        MerchantSellQuantityMode.One => MerchantSellQuantityMode.Five,
        MerchantSellQuantityMode.Five => MerchantSellQuantityMode.Ten,
        MerchantSellQuantityMode.Ten => MerchantSellQuantityMode.Fifty,
        MerchantSellQuantityMode.Fifty => MerchantSellQuantityMode.All,
        MerchantSellQuantityMode.All => MerchantSellQuantityMode.One,
        _ => MerchantSellQuantityMode.One,
    };

    public static string GetButtonLabel(this MerchantSellQuantityMode mode) => mode switch
    {
        MerchantSellQuantityMode.One => "Sell: 1",
        MerchantSellQuantityMode.Five => "Sell: 5",
        MerchantSellQuantityMode.Ten => "Sell: 10",
        MerchantSellQuantityMode.Fifty => "Sell: 50",
        MerchantSellQuantityMode.All => "Sell: All",
        _ => "Sell: 1",
    };

    // Resolves how many units to sell from a stack with `availableQuantity` units left.
    // `All` consumes the full stack; the numeric modes cap at their named limit.
    public static int ResolveSellQuantity(this MerchantSellQuantityMode mode, int availableQuantity)
    {
        if (availableQuantity <= 0)
            return 0;

        return mode switch
        {
            MerchantSellQuantityMode.One => Math.Min(1, availableQuantity),
            MerchantSellQuantityMode.Five => Math.Min(5, availableQuantity),
            MerchantSellQuantityMode.Ten => Math.Min(10, availableQuantity),
            MerchantSellQuantityMode.Fifty => Math.Min(50, availableQuantity),
            MerchantSellQuantityMode.All => availableQuantity,
            _ => Math.Min(1, availableQuantity),
        };
    }
}
