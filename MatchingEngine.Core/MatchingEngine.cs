using System.Collections.Concurrent;

namespace MatchingEngine
{
    public class MatchingEngine
    {
        private readonly OrderBook _orderBook;

        private readonly BlockingCollection<Order> _incomingOrders =
            new(new ConcurrentQueue<Order>());

        private readonly object _ingressLock = new();

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

        public void SubmitOrder(Order order)
        {
            // Defines the official arrival order of orders into the engine.
            lock (_ingressLock)
            {
                order.Sequence = ++_sequence;

                _incomingOrders.Add(order);
            }
        }

        private void ProcessOrders()
        {
            foreach (Order order in _incomingOrders.GetConsumingEnumerable())
            {
                ProcessOrder(order);
            }
        }

        private void ProcessOrder(Order order)
        {
            _orderBook.AddOrder(order);
        }
    }
}