using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MatchingEngine;
using Xunit;

namespace MatchingEngine.Tests
{
    public class OrderProcessingTests
    {
        [Fact]
        public async Task SubmitOrder_LimitWithoutMatch_RestsInBook()
        {
            var book = new OrderBook();
            var engine = new MatchingEngine(book);

            var trades = new List<Trade>();

            engine.OrderProcessed += (_, orderTrades) =>
                trades.AddRange(orderTrades);

            var order = CreateLimitOrder(
                id: 1,
                side: Side.Buy,
                price: 100m,
                quantity: 10
            );

            engine.SubmitOrder(NewOrderMessage(order));

            await engine.StopAsync();

            var restingOrder = Assert.Single(book.Bids);

            Assert.Equal(order.Id, restingOrder.Id);
            Assert.Equal(100m, restingOrder.Price);
            Assert.Equal(10, restingOrder.Quantity);

            Assert.Empty(book.Asks);
            Assert.Empty(trades);
        }


        [Fact]
        public async Task SubmitOrder_CrossingLimitOrders_ExecuteCompletely()
        {
            var book = new OrderBook();
            var engine = new global::MatchingEngine.MatchingEngine(book);

            var trades = new List<Trade>();

            engine.OrderProcessed += (_, orderTrades) =>
                trades.AddRange(orderTrades);

            var sell = CreateLimitOrder(
                id: 1,
                side: Side.Sell,
                price: 101m,
                quantity: 10
            );

            var buy = CreateLimitOrder(
                id: 2,
                side: Side.Buy,
                price: 101m,
                quantity: 10
            );

            engine.SubmitOrder(NewOrderMessage(sell));
            engine.SubmitOrder(NewOrderMessage(buy));

            await engine.StopAsync();

            var trade = Assert.Single(trades);

            Assert.Equal(2, trade.BuyOrderId);
            Assert.Equal(1, trade.SellOrderId);
            Assert.Equal(101m, trade.Price);
            Assert.Equal(10, trade.Quantity);

            Assert.Empty(book.Bids);
            Assert.Empty(book.Asks);
        }


        [Fact]
        public async Task SubmitOrder_NonCrossingLimitOrders_RemainInBook()
        {
            var book = new OrderBook();
            var engine = new MatchingEngine(book);

            var trades = new List<Trade>();

            engine.OrderProcessed += (_, orderTrades) =>
                trades.AddRange(orderTrades);

            var sell = CreateLimitOrder(
                id: 1,
                side: Side.Sell,
                price: 101m,
                quantity: 10
            );

            var buy = CreateLimitOrder(
                id: 2,
                side: Side.Buy,
                price: 100m,
                quantity: 10
            );

            engine.SubmitOrder(NewOrderMessage(sell));
            engine.SubmitOrder(NewOrderMessage(buy));

            await engine.StopAsync();

            var restingBid = Assert.Single(book.Bids);
            var restingAsk = Assert.Single(book.Asks);

            Assert.Equal(2, restingBid.Id);
            Assert.Equal(100m, restingBid.Price);

            Assert.Equal(1, restingAsk.Id);
            Assert.Equal(101m, restingAsk.Price);

            Assert.Empty(trades);
        }


        [Fact]
        public async Task SubmitOrder_PartiallyFilledLimitOrder_RestsRemainingQuantity()
        {
            var book = new OrderBook();
            var engine = new MatchingEngine(book);

            var trades = new List<Trade>();

            engine.OrderProcessed += (_, orderTrades) =>
                trades.AddRange(orderTrades);

            var sell = CreateLimitOrder(
                id: 1,
                side: Side.Sell,
                price: 101m,
                quantity: 5
            );

            var buy = CreateLimitOrder(
                id: 2,
                side: Side.Buy,
                price: 101m,
                quantity: 10
            );

            engine.SubmitOrder(NewOrderMessage(sell));
            engine.SubmitOrder(NewOrderMessage(buy));

            await engine.StopAsync();

            var trade = Assert.Single(trades);
            var remainingBuy = Assert.Single(book.Bids);

            Assert.Equal(5, trade.Quantity);
            Assert.Equal(101m, trade.Price);

            Assert.Equal(2, remainingBuy.Id);
            Assert.Equal(5, remainingBuy.Quantity);

            Assert.Empty(book.Asks);
        }


        [Fact]
        public async Task SubmitOrder_PartiallyFilledMarketOrder_DoesNotRestInBook()
        {
            var book = new OrderBook();
            var engine = new MatchingEngine(book);

            var trades = new List<Trade>();

            engine.OrderProcessed += (_, orderTrades) =>
                trades.AddRange(orderTrades);

            var sell = CreateLimitOrder(
                id: 1,
                side: Side.Sell,
                price: 101m,
                quantity: 5
            );

            var marketBuy = CreateMarketOrder(
                id: 2,
                side: Side.Buy,
                quantity: 10
            );

            engine.SubmitOrder(NewOrderMessage(sell));
            engine.SubmitOrder(NewOrderMessage(marketBuy));

            await engine.StopAsync();

            var trade = Assert.Single(trades);

            Assert.Equal(5, trade.Quantity);
            Assert.Equal(101m, trade.Price);

            // O restante da MKT não pode ficar resting no book.
            Assert.Equal(5, marketBuy.Quantity);
            Assert.Empty(book.Bids);
            Assert.Empty(book.Asks);
        }


        [Fact]
        public async Task SubmitOrder_MarketOrder_MatchesAcrossMultiplePriceLevels()
        {
            var book = new OrderBook();
            var engine = new MatchingEngine(book);

            var trades = new List<Trade>();

            engine.OrderProcessed += (_, orderTrades) =>
                trades.AddRange(orderTrades);

            var firstAsk = CreateLimitOrder(
                id: 1,
                side: Side.Sell,
                price: 101m,
                quantity: 5
            );

            var secondAsk = CreateLimitOrder(
                id: 2,
                side: Side.Sell,
                price: 102m,
                quantity: 7
            );

            var marketBuy = CreateMarketOrder(
                id: 3,
                side: Side.Buy,
                quantity: 10
            );

            engine.SubmitOrder(NewOrderMessage(firstAsk));
            engine.SubmitOrder(NewOrderMessage(secondAsk));
            engine.SubmitOrder(NewOrderMessage(marketBuy));

            await engine.StopAsync();

            Assert.Equal(2, trades.Count);

            Assert.Equal(1, trades[0].SellOrderId);
            Assert.Equal(101m, trades[0].Price);
            Assert.Equal(5, trades[0].Quantity);

            Assert.Equal(2, trades[1].SellOrderId);
            Assert.Equal(102m, trades[1].Price);
            Assert.Equal(5, trades[1].Quantity);

            var remainingAsk = Assert.Single(book.Asks);

            Assert.Equal(2, remainingAsk.Id);
            Assert.Equal(2, remainingAsk.Quantity);

            Assert.Empty(book.Bids);
        }


        [Fact]
        public async Task SubmitOrder_SamePrice_UsesTimePriority()
        {
            var book = new OrderBook();
            var engine = new MatchingEngine(book);

            var trades = new List<Trade>();

            engine.OrderProcessed += (_, orderTrades) =>
                trades.AddRange(orderTrades);

            var firstAsk = CreateLimitOrder(
                id: 1,
                side: Side.Sell,
                price: 101m,
                quantity: 5
            );

            var secondAsk = CreateLimitOrder(
                id: 2,
                side: Side.Sell,
                price: 101m,
                quantity: 5
            );

            var marketBuy = CreateMarketOrder(
                id: 3,
                side: Side.Buy,
                quantity: 5
            );

            // Como as mensagens são enviadas sequencialmente neste teste,
            // a ordem 1 deve chegar ao nível de preço antes da ordem 2.
            engine.SubmitOrder(NewOrderMessage(firstAsk));
            engine.SubmitOrder(NewOrderMessage(secondAsk));
            engine.SubmitOrder(NewOrderMessage(marketBuy));

            await engine.StopAsync();

            var trade = Assert.Single(trades);

            Assert.Equal(1, trade.SellOrderId);
            Assert.Equal(101m, trade.Price);
            Assert.Equal(5, trade.Quantity);

            var remainingAsk = Assert.Single(book.Asks);

            Assert.Equal(2, remainingAsk.Id);
            Assert.Equal(5, remainingAsk.Quantity);
        }


        [Fact]
        public async Task SubmitOrder_SellOrder_MatchesBestBid()
        {
            var book = new OrderBook();
            var engine = new MatchingEngine(book);

            var trades = new List<Trade>();

            engine.OrderProcessed += (_, orderTrades) =>
                trades.AddRange(orderTrades);

            var buy = CreateLimitOrder(
                id: 1,
                side: Side.Buy,
                price: 100m,
                quantity: 10
            );

            var sell = CreateLimitOrder(
                id: 2,
                side: Side.Sell,
                price: 100m,
                quantity: 10
            );

            engine.SubmitOrder(NewOrderMessage(buy));
            engine.SubmitOrder(NewOrderMessage(sell));

            await engine.StopAsync();

            var trade = Assert.Single(trades);

            Assert.Equal(1, trade.BuyOrderId);
            Assert.Equal(2, trade.SellOrderId);
            Assert.Equal(100m, trade.Price);
            Assert.Equal(10, trade.Quantity);

            Assert.Empty(book.Bids);
            Assert.Empty(book.Asks);
        }


        private static EngineMessage NewOrderMessage(Order order) =>
            new()
            {
                Type = MessageType.NewOrder,
                Order = order
            };


        private static Order CreateLimitOrder(
            long id,
            Side side,
            decimal price,
            int quantity,
            string ownerId = "TestUser") =>
            new()
            {
                Id = id,
                OwnerID = ownerId,
                Side = side,
                Type = OrderType.LMT,
                Price = price,
                Quantity = quantity
            };


        private static Order CreateMarketOrder(
            long id,
            Side side,
            int quantity,
            string ownerId = "TestUser") =>
            new()
            {
                Id = id,
                OwnerID = ownerId,
                Side = side,
                Type = OrderType.MKT,
                Price = null,
                Quantity = quantity
            };
    }
}
