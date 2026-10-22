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
}