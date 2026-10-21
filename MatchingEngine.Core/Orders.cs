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
        private readonly List<Order> _bids = new();
        private readonly List<Order> _asks = new();

        private long _lastSequence = 0;

        public IReadOnlyList<Order> Bids => _bids;
        public IReadOnlyList<Order> Asks => _asks;

        public void AddOrder(Order order)
        {
            if (order.Type != OrderType.LMT) // MOMENTANEO
                throw new ArgumentException("Only LMT orders can stay in the book.");

            if (order.Price == null)
                throw new ArgumentException("LMT order must have a price.");

            order.Sequence = ++_lastSequence;

            if (order.Side == Side.Buy)
                InsertBid(order);
            else
                InsertAsk(order);
        }

        private void InsertBid(Order order)
        {
            int index = _bids.FindIndex(
                existing => existing.Price < order.Price // Garante seq maior preço -> menor preço para o bid
            );

            if (index == -1)
                _bids.Add(order);
            else
                _bids.Insert(index, order);
        }

        private void InsertAsk(Order order)
        {
            int index = _asks.FindIndex(
                existing => existing.Price > order.Price // Garante seq menor preço -> maior preço para o ask
            );

            if (index == -1)
                _asks.Add(order);
            else
                _asks.Insert(index, order);
        }
    }

}
