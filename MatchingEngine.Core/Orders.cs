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
    }

}
