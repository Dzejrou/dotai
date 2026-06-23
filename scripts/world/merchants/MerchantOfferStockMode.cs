// How an offer's stock behaves after a purchase. Limited offers sell out until the next
// refresh/restock rebuilds stock; Unlimited offers stay repeatedly purchasable and are never
// marked sold (intended for staples such as the future Dungeon shop's food and drink).
public enum MerchantOfferStockMode
{
    Limited = 0,
    Unlimited = 1,
}
