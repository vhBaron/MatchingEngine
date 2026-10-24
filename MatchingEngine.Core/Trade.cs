namespace MatchingEngine;

// Representa uma execução gerada pelo encontro entre uma ordem BUY e uma SELL.
// O preço utilizado é definido pelo matching com base na resting order.
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