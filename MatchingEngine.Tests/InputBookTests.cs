using MatchingEngine;
using Xunit;

namespace MatchingEngine.Tests;

public class OrderBookTests
{
    [Fact]
    public void Bids_Should_Be_Ordered_By_Highest_Price_First()
    {
        var book = new OrderBook();

        book.AddOrder(new Order
        {
            Id = 1,
            Side = Side.Buy,
            Type = OrderType.LMT,
            Price = 100m,
            Quantity = 10
        });

        book.AddOrder(new Order
        {
            Id = 2,
            Side = Side.Buy,
            Type = OrderType.LMT,
            Price = 105m,
            Quantity = 10
        });

        book.AddOrder(new Order
        {
            Id = 3,
            Side = Side.Buy,
            Type = OrderType.LMT,
            Price = 102m,
            Quantity = 10
        });

        var bids = book.Bids.ToArray();

        Assert.Equal(105m, bids[0].Price);
        Assert.Equal(102m, bids[1].Price);
        Assert.Equal(100m, bids[2].Price);
    }

    [Fact]
    public void Asks_Should_Be_Ordered_By_Lowest_Price_First()
    {
        var book = new OrderBook();

        book.AddOrder(new Order
        {
            Id = 1,
            Side = Side.Sell,
            Type = OrderType.LMT,
            Price = 105m,
            Quantity = 10
        });

        book.AddOrder(new Order
        {
            Id = 2,
            Side = Side.Sell,
            Type = OrderType.LMT,
            Price = 100m,
            Quantity = 10
        });

        book.AddOrder(new Order
        {
            Id = 3,
            Side = Side.Sell,
            Type = OrderType.LMT,
            Price = 102m,
            Quantity = 10
        });

        var asks = book.Asks.ToArray();

        Assert.Equal(100m, asks[0].Price);
        Assert.Equal(102m, asks[1].Price);
        Assert.Equal(105m, asks[2].Price);
    }

    
}
