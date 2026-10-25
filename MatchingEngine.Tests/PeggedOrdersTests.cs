using System.Linq;
using System.Threading.Tasks;
using MatchingEngine;
using Xunit;

namespace MatchingEngine.Tests;

public class PeggedOrderTests
{
    private const string Owner = "Victor";

    private static Order LimitOrder(long id, Side side, decimal price, int quantity)
    {
        return new Order
        {
            Id = id,
            OwnerID = Owner,
            Side = side,
            Type = OrderType.LMT,
            Price = price,
            Quantity = quantity
        };
    }

    private static Order PegOrder(long id, Side side, int quantity)
    {
        return new Order
        {
            Id = id,
            OwnerID = Owner,
            Side = side,
            Type = OrderType.PEG,
            Quantity = quantity
        };
    }

    [Fact]
    public async Task PegOrder_Buy_UsesBestBidPrice()
    {
        var book = new OrderBook();
        book.AddOrder(LimitOrder(1, Side.Buy, 99m, 10));
        book.AddOrder(LimitOrder(2, Side.Buy, 100m, 10));

        var engine = new MatchingEngine(book);
        engine.SubmitOrder(new EngineMessage
        {
            Type = MessageType.PeggedOrder,
            Order = PegOrder(7, Side.Buy, 20)
        });

        // Aguarda a worker terminar antes de conferir o book.
        await engine.StopAsync();

        var peg = book.Bids.Single(order => order.Id == 7);
        Assert.Equal(100m, peg.Price);
        Assert.Equal(20, peg.Quantity);
        Assert.Equal(OrderType.PEG, peg.Type);
    }

    [Fact]
    public async Task PegOrder_Sell_UsesBestAskPrice()
    {
        var book = new OrderBook();
        book.AddOrder(LimitOrder(1, Side.Sell, 102m, 10));
        book.AddOrder(LimitOrder(2, Side.Sell, 101m, 10));

        var engine = new MatchingEngine(book);
        engine.SubmitOrder(new EngineMessage
        {
            Type = MessageType.PeggedOrder,
            Order = PegOrder(7, Side.Sell, 20)
        });
        await engine.StopAsync();

        var peg = book.Asks.Single(order => order.Id == 7);
        Assert.Equal(101m, peg.Price);
        Assert.Equal(20, peg.Quantity);
    }

    [Fact]
    public async Task PegOrder_UpdatesPrice_WhenBetterLimitArrives()
    {
        var book = new OrderBook();
        book.AddOrder(LimitOrder(1, Side.Buy, 99m, 10));
        book.PeggedOrder(PegOrder(7, Side.Buy, 20));

        var engine = new MatchingEngine(book);
        engine.SubmitOrder(new EngineMessage
        {
            Type = MessageType.NewOrder,
            Order = LimitOrder(8, Side.Buy, 100m, 10)
        });
        await engine.StopAsync();

        var bids = book.Bids.ToList();
        var peg = bids.Single(order => order.Id == 7);
        Assert.Equal(100m, peg.Price);
        Assert.Equal(20, peg.Quantity);
        // A LMT chegou ao novo preço antes da PEG.
        Assert.Equal(8, bids[0].Id);
        Assert.Equal(7, bids[1].Id);
    }

    [Fact]
    public async Task PegOrder_UpdatesPrice_WhenBestLimitIsModifiedToWorsePrice()
    {
        // Reproduz o problema da LMT 8 e da PEG 7.
        var book = new OrderBook();
        book.AddOrder(LimitOrder(1, Side.Buy, 99m, 10));
        book.AddOrder(LimitOrder(8, Side.Buy, 100m, 10));
        book.PeggedOrder(PegOrder(7, Side.Buy, 20));

        var engine = new MatchingEngine(book);
        engine.SubmitOrder(new EngineMessage
        {
            Type = MessageType.ModifyOrder,
            OrderId = 8,
            login = Owner,
            Price = 97m
        });
        await engine.StopAsync();

        var bids = book.Bids.ToList();
        var peg = bids.Single(order => order.Id == 7);
        Assert.Equal(99m, peg.Price);
        Assert.Equal(20, peg.Quantity);
        Assert.Equal(1, bids[0].Id);
        Assert.Equal(7, bids[1].Id);
        Assert.Equal(8, bids[2].Id);
    }

    [Fact]
    public async Task PegOrder_UpdatesPrice_WhenBestLimitIsCancelled()
    {
        var book = new OrderBook();
        book.AddOrder(LimitOrder(1, Side.Buy, 99m, 10));
        book.AddOrder(LimitOrder(8, Side.Buy, 100m, 10));
        book.PeggedOrder(PegOrder(7, Side.Buy, 20));

        var engine = new MatchingEngine(book);
        engine.SubmitOrder(new EngineMessage
        {
            Type = MessageType.CancelOrder,
            OrderId = 8,
            login = Owner
        });
        await engine.StopAsync();

        var bids = book.Bids.ToList();
        var peg = bids.Single(order => order.Id == 7);
        Assert.Equal(99m, peg.Price);
        Assert.Equal(2, bids.Count);
        Assert.Equal(1, bids[0].Id);
        Assert.Equal(7, bids[1].Id);
    }

    [Fact]
    public async Task PegOrder_ModifyQuantity_DoesNotExecuteAgainstAsk()
    {
        var book = new OrderBook();
        book.AddOrder(LimitOrder(1, Side.Buy, 100m, 10));
        book.AddOrder(LimitOrder(2, Side.Sell, 105m, 10));
        book.PeggedOrder(PegOrder(7, Side.Buy, 20));

        var engine = new MatchingEngine(book);
        engine.SubmitOrder(new EngineMessage
        {
            Type = MessageType.ModifyOrder,
            OrderId = 7,
            login = Owner,
            Quantity = 30
        });
        await engine.StopAsync();

        var peg = book.Bids.Single(order => order.Id == 7);
        var ask = Assert.Single(book.Asks);
        Assert.Equal(30, peg.Quantity);
        Assert.Equal(100m, peg.Price);
        Assert.Equal(OrderType.PEG, peg.Type);
        Assert.Equal(10, ask.Quantity);
    }

    [Fact]
    public async Task PegOrder_DoesNotAllowPriceModification()
    {
        var book = new OrderBook();
        book.AddOrder(LimitOrder(1, Side.Buy, 100m, 10));
        book.PeggedOrder(PegOrder(7, Side.Buy, 20));

        var engine = new MatchingEngine(book);
        engine.SubmitOrder(new EngineMessage
        {
            Type = MessageType.ModifyOrder,
            OrderId = 7,
            login = Owner,
            Price = 101m
        });
        await engine.StopAsync();

        // O engine trata o erro e a ordem deve continuar intacta.
        var peg = book.Bids.Single(order => order.Id == 7);
        Assert.Equal(100m, peg.Price);
        Assert.Equal(20, peg.Quantity);
        Assert.Equal(OrderType.PEG, peg.Type);
    }

    [Fact]
    public async Task PegOrder_Cancelled_DoesNotReturnWhenBestPriceChanges()
    {
        var book = new OrderBook();
        book.AddOrder(LimitOrder(1, Side.Buy, 100m, 10));
        book.PeggedOrder(PegOrder(7, Side.Buy, 20));

        var engine = new MatchingEngine(book);
        engine.SubmitOrder(new EngineMessage
        {
            Type = MessageType.CancelOrder,
            OrderId = 7,
            login = Owner
        });
        engine.SubmitOrder(new EngineMessage
        {
            Type = MessageType.NewOrder,
            Order = LimitOrder(8, Side.Buy, 101m, 10)
        });
        await engine.StopAsync();

        // Só as duas LMTs devem permanecer no book.
        var bids = book.Bids.ToList();
        Assert.Equal(2, bids.Count);
        Assert.Equal(8, bids[0].Id);
        Assert.Equal(1, bids[1].Id);
    }
}
