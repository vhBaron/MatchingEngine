using MatchingEngine;
using Xunit;

namespace MatchingEngine.Tests
{
    public class CancelOrderTests
    {
        [Fact]
        public void CancelOrder_RemovesOrder_WhenOwnerMatches()
        {
            var book = new OrderBook();

            var order = new Order
            {
                Id = 1,
                OwnerID = "Victor",
                Side = Side.Buy,
                Type = OrderType.LMT,
                Price = 100m,
                Quantity = 10
            };

            book.AddOrder(order);

            var result = book.CancelOrder(order.Id, "Victor");

            Assert.Equal("Ordem cancelada com sucesso", result);
            Assert.Empty(book.Bids);
        }

        [Fact]
        public void CancelOrder_Throws_WhenOrderDoesNotExist()
        {
            var book = new OrderBook();

            var exception = Assert.Throws<ArgumentException>(() =>
                book.CancelOrder(999, "Victor")
            );

            Assert.Equal("ID não encontrado", exception.Message);
        }

        [Fact]
        public void CancelOrder_Throws_WhenOwnerDoesNotMatch()
        {
            var book = new OrderBook();

            var order = new Order
            {
                Id = 1,
                OwnerID = "Victor",
                Side = Side.Buy,
                Type = OrderType.LMT,
                Price = 100m,
                Quantity = 10
            };

            book.AddOrder(order);

            var exception = Assert.Throws<ArgumentException>(() =>
                book.CancelOrder(order.Id, "OutroUsuario")
            );

            Assert.Equal("Owner Diferente", exception.Message);
            Assert.Single(book.Bids);
            Assert.Equal(order.Id, book.Bids.First().Id);
        }

        [Fact]
        public void CancelOrder_RemovesOnlyTargetOrder_FromSamePriceLevel()
        {
            var book = new OrderBook();

            var firstOrder = new Order
            {
                Id = 1,
                OwnerID = "Victor",
                Side = Side.Buy,
                Type = OrderType.LMT,
                Price = 100m,
                Quantity = 10
            };

            var secondOrder = new Order
            {
                Id = 2,
                OwnerID = "Victor",
                Side = Side.Buy,
                Type = OrderType.LMT,
                Price = 100m,
                Quantity = 20
            };

            var thirdOrder = new Order
            {
                Id = 3,
                OwnerID = "Victor",
                Side = Side.Buy,
                Type = OrderType.LMT,
                Price = 100m,
                Quantity = 30
            };

            book.AddOrder(firstOrder);
            book.AddOrder(secondOrder);
            book.AddOrder(thirdOrder);

            book.CancelOrder(secondOrder.Id, "Victor");

            var remainingOrders = book.Bids.ToList();

            Assert.Equal(2, remainingOrders.Count);
            Assert.Equal(firstOrder.Id, remainingOrders[0].Id);
            Assert.Equal(thirdOrder.Id, remainingOrders[1].Id);
            Assert.DoesNotContain(remainingOrders, order => order.Id == secondOrder.Id);
        }

        [Fact]
        public void CancelOrder_RemovesPriceLevel_WhenLastOrderAtPriceIsCanceled()
        {
            var book = new OrderBook();

            var bestBid = new Order
            {
                Id = 1,
                OwnerID = "Victor",
                Side = Side.Buy,
                Type = OrderType.LMT,
                Price = 100m,
                Quantity = 10
            };

            var lowerBid = new Order
            {
                Id = 2,
                OwnerID = "Victor",
                Side = Side.Buy,
                Type = OrderType.LMT,
                Price = 99m,
                Quantity = 10
            };

            book.AddOrder(bestBid);
            book.AddOrder(lowerBid);

            book.CancelOrder(bestBid.Id, "Victor");

            var newBestBid = book.GetBestBid();

            Assert.NotNull(newBestBid);
            Assert.Equal(lowerBid.Id, newBestBid!.Id);
            Assert.Equal(99m, newBestBid.Price);
        }

        [Fact]
        public void CancelOrder_CanRemoveOrderFromMiddleOfBook()
        {
            var book = new OrderBook();

            book.AddOrder(new Order
            {
                Id = 1,
                OwnerID = "Victor",
                Side = Side.Buy,
                Type = OrderType.LMT,
                Price = 101m,
                Quantity = 10
            });

            book.AddOrder(new Order
            {
                Id = 2,
                OwnerID = "Victor",
                Side = Side.Buy,
                Type = OrderType.LMT,
                Price = 100m,
                Quantity = 10
            });

            book.AddOrder(new Order
            {
                Id = 3,
                OwnerID = "Victor",
                Side = Side.Buy,
                Type = OrderType.LMT,
                Price = 99m,
                Quantity = 10
            });

            book.CancelOrder(2, "Victor");

            var remainingIds = book.Bids
                .Select(order => order.Id)
                .ToList();

            Assert.Equal(new long[] { 1, 3 }, remainingIds);
        }
    }
}
