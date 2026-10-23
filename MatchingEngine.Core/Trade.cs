
namespace MatchingEngine;
public class Trade
{
    public long BuyOrderId { get; }
    public long SellOrderId { get; }

    public decimal Price { get; }
    public int Quantity { get; }

    public DateTime Timestamp { get; }

    public Trade(
        long buyOrderId,
        long sellOrderId,
        decimal price,
        int quantity)
    {
        BuyOrderId = buyOrderId;
        SellOrderId = sellOrderId;
        Price = price;
        Quantity = quantity;
        Timestamp = DateTime.UtcNow;
    }
}