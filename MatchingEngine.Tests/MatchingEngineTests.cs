using MatchingEngine;
using Xunit;

namespace MatchingEngine.Tests;

public class MatchingEngineTest
{
    [Fact]
    public async Task Concurrent_Orders_Should_Be_Processed_By_Sequence()
    {
        var book = new OrderBook();
        var engine = new MatchingEngine(book);

        const int numberOfOrders = 10;

        using var barrier = new Barrier(numberOfOrders + 1);

        var tasks = new List<Task>();

        for (int i = 1; i <= numberOfOrders; i++)
        {
            long id = i;

            tasks.Add(Task.Run(() =>
            {
                var order = new Order
                {
                    Id = id,
                    Side = Side.Buy,
                    Type = OrderType.LMT,
                    Price = 100m,
                    Quantity = 10
                };

                barrier.SignalAndWait();

                engine.SubmitOrder(order);
            }));
        }

        barrier.SignalAndWait();

        await Task.WhenAll(tasks);

        await engine.StopAsync();

        var bids = book.Bids.ToArray();

        Assert.Equal(numberOfOrders, bids.Length);

        for (int i = 0; i < numberOfOrders; i++)
        {
            Assert.Equal((long)(i + 1), bids[i].Sequence);
        }
    }


    [Fact]
    public async Task BuyLimitOrder_ShouldMatchBestAsks_InPricePriority()
    {
        // Arrange
        var book = new OrderBook();
        var engine = new MatchingEngine(book);

        book.AddOrder(new Order
        {
            Id = 1,
            Side = Side.Buy,
            Type = OrderType.LMT,
            Price = 99,
            Quantity = 10
        });

        book.AddOrder(new Order
        {
            Id = 2,
            Side = Side.Buy,
            Type = OrderType.LMT,
            Price = 98,
            Quantity = 20
        });

        book.AddOrder(new Order
        {
            Id = 3,
            Side = Side.Sell,
            Type = OrderType.LMT,
            Price = 101,
            Quantity = 10
        });

        book.AddOrder(new Order
        {
            Id = 4,
            Side = Side.Sell,
            Type = OrderType.LMT,
            Price = 102,
            Quantity = 20
        });

        book.AddOrder(new Order
        {
            Id = 5,
            Side = Side.Sell,
            Type = OrderType.LMT,
            Price = 103,
            Quantity = 30
        });


        var incomingOrder = new Order
        {
            Id = 6,
            Side = Side.Buy,
            Type = OrderType.LMT,
            Price = 102,
            Quantity = 25
        };


        // Act
        List<Trade> trades = engine.ProcessOrder(incomingOrder);


        // Assert - Trades
        Assert.Equal(2, trades.Count);

        // Primeiro deve consumir o ASK de 101
        Assert.Equal(3, trades[0].SellOrderId);
        Assert.Equal(10, trades[0].Quantity);
        Assert.Equal(101m, trades[0].Price);

        // Depois consome parcialmente o ASK de 102
        Assert.Equal(4, trades[1].SellOrderId);
        Assert.Equal(15, trades[1].Quantity);
        Assert.Equal(102m, trades[1].Price);


        // Incoming foi completamente executada
        Assert.Equal(0, incomingOrder.Quantity);


        // ASK de 101 deve ter desaparecido
        Assert.DoesNotContain(
            book.Asks,
            order => order.Id == 3
        );

        // ASK de 102 deve continuar com 5
        Order remainingAsk = Assert.Single(
            book.Asks.Where(order => order.Id == 4)
        );

        Assert.Equal(5, remainingAsk.Quantity);
        Assert.Equal(102m, remainingAsk.Price);


        // ASK de 103 não deve ter sido tocado
        Order untouchedAsk = Assert.Single(
            book.Asks.Where(order => order.Id == 5)
        );

        Assert.Equal(30, untouchedAsk.Quantity);


        // Bids antigos não devem ter sido alterados
        Assert.Equal(2, book.Bids.Count());

        Assert.Contains(book.Bids, order =>
            order.Id == 1 &&
            order.Quantity == 10 &&
            order.Price == 99);

        Assert.Contains(book.Bids, order =>
            order.Id == 2 &&
            order.Quantity == 20 &&
            order.Price == 98);


        await engine.StopAsync();
        }


    [Fact]
    public async Task BuyMarketOrder_ShouldConsumeBestAsks_UntilFilled()
    {
        // Arrange
        var book = new OrderBook();
        var engine = new MatchingEngine(book);

        book.AddOrder(new Order
        {
            Id = 1,
            Side = Side.Sell,
            Type = OrderType.LMT,
            Price = 101,
            Quantity = 10
        });

        book.AddOrder(new Order
        {
            Id = 2,
            Side = Side.Sell,
            Type = OrderType.LMT,
            Price = 102,
            Quantity = 20
        });

        book.AddOrder(new Order
        {
            Id = 3,
            Side = Side.Sell,
            Type = OrderType.LMT,
            Price = 103,
            Quantity = 30
        });

        var incoming = new Order
        {
            Id = 4,
            Side = Side.Buy,
            Type = OrderType.MKT,
            Price = null,
            Quantity = 45
        };

        // Act
        var trades = engine.ProcessOrder(incoming);

        // Assert
        Assert.Equal(3, trades.Count);

        Assert.Equal(101m, trades[0].Price);
        Assert.Equal(10, trades[0].Quantity);

        Assert.Equal(102m, trades[1].Price);
        Assert.Equal(20, trades[1].Quantity);

        Assert.Equal(103m, trades[2].Price);
        Assert.Equal(15, trades[2].Quantity);

        Assert.Equal(0, incoming.Quantity);

        var remainingAsk = Assert.Single(book.Asks);

        Assert.Equal(103m, remainingAsk.Price);
        Assert.Equal(15, remainingAsk.Quantity);

        await engine.StopAsync();
    }
}
