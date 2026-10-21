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

        Assert.Equal(105m, book.Bids[0].Price);
        Assert.Equal(102m, book.Bids[1].Price);
        Assert.Equal(100m, book.Bids[2].Price);
    }
}