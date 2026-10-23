namespace MatchingEngine
{

    public enum Side{Buy, Sell}
    public enum OrderType{MKT,LMT}

    public class Order
    {
        public long Id { get; init; }

        public long Sequence { get; internal set; }

        public Side Side { get; init; }

        public OrderType Type { get; init; }

        public decimal? Price { get; init; }

        public int Quantity { get; set; }
    }

    public class OrderBook
    {
        private readonly SortedDictionary<decimal, Queue<Order>> _bids =
            new(Comparer<decimal>.Create((a, b) => b.CompareTo(a))); // Highest price first
        private readonly SortedDictionary<decimal, Queue<Order>> _asks = new();

        public IEnumerable<Order> Bids =>
            _bids.SelectMany(level => level.Value);

        public IEnumerable<Order> Asks =>
            _asks.SelectMany(level => level.Value);

        public void AddOrder(Order order)
        {
            if (order.Type != OrderType.LMT) 
                throw new ArgumentException(
                    "Only LMT orders can stay in the book.");

            if (order.Price == null)
                throw new ArgumentException(
                    "LMT order must have a price.");

            var bookSide =
                order.Side == Side.Buy
                ? _bids
                : _asks;

            decimal price = order.Price.Value;

            if (!bookSide.TryGetValue(price, out Queue<Order>? level))
            {
                level = new Queue<Order>();

                bookSide.Add(price, level);
            }

            level.Enqueue(order);
        }

        public Order? GetBestBid()
        {
            if (_bids.Count == 0)
                return null;

            return _bids.First().Value.Peek();
        }

        public Order? GetBestAsk()
        {
            if (_asks.Count == 0)
                return null;

            return _asks.First().Value.Peek();
        }

        public void RemoveOrder(Order order)
        {
            if (order.Price == null)
                throw new ArgumentException("Order must have a price.");

            var bookSide =
                order.Side == Side.Buy
                ? _bids
                : _asks;

            decimal price = order.Price.Value;

            if (!bookSide.TryGetValue(price, out Queue<Order>? level))
                return;

            if (level.Count == 0)
                return;

            if (level.Peek().Id != order.Id)
                throw new InvalidOperationException(
                    "Only the first order of a price level can be removed during matching.");

            level.Dequeue();

            if (level.Count == 0)
                bookSide.Remove(price);
        }
    }

}
