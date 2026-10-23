using System.Collections.Concurrent;

namespace MatchingEngine
{
    public class MatchingEngine
    {
        private readonly OrderBook _orderBook;
        private readonly BlockingCollection<Order> _incomingOrders =
            new(new ConcurrentQueue<Order>()); // Fila de ordens, define prioridade

        private long _sequence = 0; 
        private readonly Task _worker; 

        public async Task StopAsync() 
        {
            _incomingOrders.CompleteAdding();
            await _worker;
        }

        public MatchingEngine(OrderBook orderBook)
        {
            _orderBook = orderBook;
            _worker = Task.Run(ProcessOrders);
        }

        public void SubmitOrder(Order order) => _incomingOrders.Add(order);
        

        private void ProcessOrders()
        {
            foreach (Order order in _incomingOrders.GetConsumingEnumerable())
            {
                order.Sequence = ++_sequence;
                ProcessOrder(order);
            }
        }

        public List<Trade> ProcessOrder(Order incomingOrder)
        {
            var trades = new List<Trade>();

            if (incomingOrder.Side == Side.Buy)
            {
                MatchBuyOrder(incomingOrder, trades);
            }
            else
            {
                MatchSellOrder(incomingOrder, trades);
            }

            return trades;
        }

        private void MatchBuyOrder(Order incoming, List<Trade> trades)
        {
            while (incoming.Quantity > 0)
            {
                Order? restingOrder = _orderBook.GetBestAsk();

                if (restingOrder == null)
                    break;

                // Só verifica preço se for LIMIT
                if (incoming.Type == OrderType.LMT &&
                    incoming.Price < restingOrder.Price)
                {
                    break;
                }

                int executedQuantity =
                    Math.Min(incoming.Quantity, restingOrder.Quantity);

                var trade = new Trade(
                    buyOrderId: incoming.Id,
                    sellOrderId: restingOrder.Id,
                    price: restingOrder.Price.Value,
                    quantity: executedQuantity);

                trades.Add(trade);

                incoming.Quantity -= executedQuantity;
                restingOrder.Quantity -= executedQuantity;

                if (restingOrder.Quantity == 0)
                    _orderBook.RemoveOrder(restingOrder);
            }

            // Só LIMIT pode ficar no book
            if (incoming.Quantity > 0 &&
                incoming.Type == OrderType.LMT)
            {
                _orderBook.AddOrder(incoming);
            }
        }

        private void MatchSellOrder(Order incoming, List<Trade> trades)
        {
            while (incoming.Quantity > 0)
            {
                Order? restingOrder = _orderBook.GetBestBid();

                if (restingOrder == null)
                    break;

                // Só verifica preço se for LIMIT
                if (incoming.Type == OrderType.LMT &&
                    incoming.Price > restingOrder.Price)
                {
                    break;
                }

                int executedQuantity =
                    Math.Min(incoming.Quantity, restingOrder.Quantity);

                var trade = new Trade(
                    buyOrderId: restingOrder.Id,
                    sellOrderId: incoming.Id,
                    price: restingOrder.Price.Value,
                    quantity: executedQuantity);

                trades.Add(trade);

                incoming.Quantity -= executedQuantity;
                restingOrder.Quantity -= executedQuantity;

                if (restingOrder.Quantity == 0)
                    _orderBook.RemoveOrder(restingOrder);
            }

            if (incoming.Quantity > 0 &&
                incoming.Type == OrderType.LMT)
            {
                _orderBook.AddOrder(incoming);
            }
        }
    }
}