using MatchingEngine;
using Xunit;

namespace MatchingEngine.Tests;

public class ModifyOrderTests
{
    private const string Owner = "Victor";

    private static Order LimitOrder(
        long id,
        string owner,
        Side side,
        decimal price,
        int quantity)
    {
        return new Order
        {
            Id = id,
            OwnerID = owner,
            Side = side,
            Type = OrderType.LMT,
            Price = price,
            Quantity = quantity
        };
    }

    private static async Task SubmitModify(
        MatchingEngine engine,
        long orderId,
        string owner,
        decimal? price = null,
        int? quantity = null)
    {
        engine.SubmitOrder(new EngineMessage
        {
            Type = MessageType.ModifyOrder,
            OrderId = orderId,
            login = owner,
            Price = price,
            Quantity = quantity
        });

        // Garante que a worker processou a mensagem antes dos Asserts.
        await engine.StopAsync();
    }

    [Fact]
    public async Task ModifyOrder_ChangesOnlyPrice_AndKeepsQuantity()
    {
        var book = new OrderBook();
        var engine = new MatchingEngine(book);

        book.AddOrder(LimitOrder(
            id: 7,
            owner: Owner,
            side: Side.Buy,
            price: 95m,
            quantity: 20));

        await SubmitModify(
            engine,
            orderId: 7,
            owner: Owner,
            price: 100m);

        var order = Assert.Single(book.Bids);

        Assert.Equal(7, order.Id);
        Assert.Equal(100m, order.Price);
        Assert.Equal(20, order.Quantity);
    }

    [Fact]
    public async Task ModifyOrder_ChangesOnlyQuantity_AndKeepsPrice()
    {
        var book = new OrderBook();
        var engine = new MatchingEngine(book);

        book.AddOrder(LimitOrder(
            id: 7,
            owner: Owner,
            side: Side.Buy,
            price: 95m,
            quantity: 20));

        await SubmitModify(
            engine,
            orderId: 7,
            owner: Owner,
            quantity: 50);

        var order = Assert.Single(book.Bids);

        Assert.Equal(7, order.Id);
        Assert.Equal(95m, order.Price);
        Assert.Equal(50, order.Quantity);
    }

    [Fact]
    public async Task ModifyOrder_ChangesPriceAndQuantity()
    {
        var book = new OrderBook();
        var engine = new MatchingEngine(book);

        book.AddOrder(LimitOrder(
            id: 7,
            owner: Owner,
            side: Side.Buy,
            price: 95m,
            quantity: 20));

        await SubmitModify(
            engine,
            orderId: 7,
            owner: Owner,
            price: 100m,
            quantity: 30);

        var order = Assert.Single(book.Bids);

        Assert.Equal(100m, order.Price);
        Assert.Equal(30, order.Quantity);
    }

    [Fact]
    public async Task ModifyOrder_DoesNotTrade_WhenNewPriceDoesNotCrossSpread()
    {
        var book = new OrderBook();
        var engine = new MatchingEngine(book);

        book.AddOrder(LimitOrder(
            id: 4,
            owner: "Mercado",
            side: Side.Sell,
            price: 101m,
            quantity: 10));

        book.AddOrder(LimitOrder(
            id: 7,
            owner: Owner,
            side: Side.Buy,
            price: 95m,
            quantity: 20));

        await SubmitModify(
            engine,
            orderId: 7,
            owner: Owner,
            price: 100m);

        var bid = Assert.Single(book.Bids);
        var ask = Assert.Single(book.Asks);

        Assert.Equal(7, bid.Id);
        Assert.Equal(100m, bid.Price);
        Assert.Equal(20, bid.Quantity);

        Assert.Equal(4, ask.Id);
        Assert.Equal(101m, ask.Price);
        Assert.Equal(10, ask.Quantity);
    }

    [Fact]
    public async Task ModifyOrder_BuyCrossesAsk_AndLeavesRemainingQuantityInBook()
    {
        var book = new OrderBook();
        var engine = new MatchingEngine(book);

        book.AddOrder(LimitOrder(
            id: 4,
            owner: "Mercado",
            side: Side.Sell,
            price: 101m,
            quantity: 10));

        book.AddOrder(LimitOrder(
            id: 7,
            owner: Owner,
            side: Side.Buy,
            price: 95m,
            quantity: 20));

        await SubmitModify(
            engine,
            orderId: 7,
            owner: Owner,
            price: 102m);

        Assert.Empty(book.Asks);

        var remainingBid = Assert.Single(book.Bids);

        Assert.Equal(7, remainingBid.Id);
        Assert.Equal(102m, remainingBid.Price);
        Assert.Equal(10, remainingBid.Quantity);
    }

    [Fact]
    public async Task ModifyOrder_BuyCrossesAsk_AndIsFullyExecuted()
    {
        var book = new OrderBook();
        var engine = new MatchingEngine(book);

        book.AddOrder(LimitOrder(
            id: 4,
            owner: "Mercado",
            side: Side.Sell,
            price: 101m,
            quantity: 30));

        book.AddOrder(LimitOrder(
            id: 7,
            owner: Owner,
            side: Side.Buy,
            price: 95m,
            quantity: 20));

        await SubmitModify(
            engine,
            orderId: 7,
            owner: Owner,
            price: 102m);

        Assert.Empty(book.Bids);

        var remainingAsk = Assert.Single(book.Asks);

        Assert.Equal(4, remainingAsk.Id);
        Assert.Equal(101m, remainingAsk.Price);
        Assert.Equal(10, remainingAsk.Quantity);
    }

    [Fact]
    public async Task ModifyOrder_SellCrossesBid_AndLeavesRemainingQuantityInBook()
    {
        var book = new OrderBook();
        var engine = new MatchingEngine(book);

        book.AddOrder(LimitOrder(
            id: 1,
            owner: "Mercado",
            side: Side.Buy,
            price: 99m,
            quantity: 10));

        book.AddOrder(LimitOrder(
            id: 7,
            owner: Owner,
            side: Side.Sell,
            price: 105m,
            quantity: 20));

        await SubmitModify(
            engine,
            orderId: 7,
            owner: Owner,
            price: 98m);

        Assert.Empty(book.Bids);

        var remainingAsk = Assert.Single(book.Asks);

        Assert.Equal(7, remainingAsk.Id);
        Assert.Equal(98m, remainingAsk.Price);
        Assert.Equal(10, remainingAsk.Quantity);
    }

    [Fact]
    public async Task ModifyOrder_DoesNotModify_WhenOwnerDoesNotMatch()
    {
        var book = new OrderBook();
        var engine = new MatchingEngine(book);

        book.AddOrder(LimitOrder(
            id: 7,
            owner: Owner,
            side: Side.Buy,
            price: 95m,
            quantity: 20));

        await SubmitModify(
            engine,
            orderId: 7,
            owner: "OutroUsuario",
            price: 100m);

        var order = Assert.Single(book.Bids);

        Assert.Equal(7, order.Id);
        Assert.Equal(95m, order.Price);
        Assert.Equal(20, order.Quantity);
    }

    [Fact]
    public async Task ModifyOrder_WithNoNewValues_DoesNotChangeOrder()
    {
        var book = new OrderBook();
        var engine = new MatchingEngine(book);

        book.AddOrder(LimitOrder(
            id: 7,
            owner: Owner,
            side: Side.Buy,
            price: 95m,
            quantity: 20));

        await SubmitModify(
            engine,
            orderId: 7,
            owner: Owner);

        var order = Assert.Single(book.Bids);

        Assert.Equal(95m, order.Price);
        Assert.Equal(20, order.Quantity);
    }

    [Fact]
    public async Task ModifyOrder_LosesPriority_WhenReinsertedAtSamePrice()
    {
        var book = new OrderBook();
        var engine = new MatchingEngine(book);

        book.AddOrder(LimitOrder(
            id: 1,
            owner: Owner,
            side: Side.Buy,
            price: 100m,
            quantity: 10));

        book.AddOrder(LimitOrder(
            id: 2,
            owner: "OutroUsuario",
            side: Side.Buy,
            price: 100m,
            quantity: 10));

        await SubmitModify(
            engine,
            orderId: 1,
            owner: Owner,
            quantity: 20);

        var bids = book.Bids.ToList();

        Assert.Equal(2, bids.Count);

        // Modify é tratado como cancel + replace:
        // a ordem modificada volta no fim do mesmo nível de preço.
        Assert.Equal(2, bids[0].Id);
        Assert.Equal(1, bids[1].Id);

        Assert.Equal(20, bids[1].Quantity);
    }
}
