namespace MatchingEngine
{
    // Lado da ordem no mercado.
    public enum Side { Buy, Sell }

    // MKT executa contra a liquidez disponível; LMT também pode permanecer no book.
    public enum OrderType { MKT, LMT }

    // Tipos de comando aceitos pela fila do MatchingEngine.
    public enum MessageType {NewOrder, CancelOrder, ModifyOrder}

    // Representa uma ordem enviada ao engine ou armazenada no OrderBook.
    public class Order
    {
        public long Id { get; init; }

        public string OwnerID { get; init; }

        public Side Side { get; init; }

        public OrderType Type { get; init; }

        // É nullable porque ordens MKT não possuem preço limite.
        public decimal? Price { get; init; }

        public int Quantity { get; set; }
    }

    // Mensagem colocado na fila do engine. Cada MessageType utiliza apenas os campos necessários para aquela operação.
    public class EngineMessage
    {
        public MessageType Type { get; init; }

        public string? login { get; init; }

        public Order? Order { get; init; }

        public long? OrderId { get; init; }
    }

    // Mantém as resting orders organizadas por preço e, dentro de cada preço, por ordem de chegada (LinkedList).
    public class OrderBook
    {
        // Bids em ordem decrescente: o maior preço é sempre o melhor BID.
        private readonly SortedDictionary<decimal, LinkedList<Order>> _bids =
            new(Comparer<decimal>.Create((a, b) => b.CompareTo(a))); 

        // Asks usam a ordenação crescente padrão: o menor preço é o melhor ASK.
        private readonly SortedDictionary<decimal, LinkedList<Order>> _asks = new();

        // Índice auxiliar: ID -> nó da LinkedList.
        // Evita percorrer o book inteiro para localizar uma ordem no cancelamento.
        private readonly Dictionary<long, LinkedListNode<Order>> _ordersById = new();

        // Expõe uma visão linear dos níveis de preço preservando a ordem do book.
        public IEnumerable<Order> Bids =>
            _bids.SelectMany(level => level.Value);

        public IEnumerable<Order> Asks =>
            _asks.SelectMany(level => level.Value);


        // Retorna a estrutura correspondente ao lado da ordem.
        private SortedDictionary<decimal, LinkedList<Order>> GetSide(Side side) =>
            side == Side.Buy ? _bids : _asks;

        // Adiciona apenas ordens que podem permanecer no book.
        // Dentro do mesmo preço, AddLast preserva a prioridade FIFO.
        public void AddOrder(Order order)
        {
            if (order.Type != OrderType.LMT)
                throw new ArgumentException("Only LMT orders can stay in the book.");

            if (order.Price == null)
                throw new ArgumentException("LMT order must have a price.");

            if (_ordersById.ContainsKey(order.Id))
                throw new ArgumentException(
                    $"Order {order.Id} already exists in the book."
                );


            var bookSide = GetSide(order.Side);

            decimal price = order.Price.Value;

            // Cria o nível de preço somente quando ele ainda não existe.
            if (!bookSide.TryGetValue(price, out LinkedList<Order>? level))
            {
                level = new LinkedList<Order>();

                bookSide.Add(price, level);
            }

            // A ordem mais nova entra no fim do nível, mantendo price-time priority.
            var node = level.AddLast(order); 

            _ordersById.Add(order.Id, node);
        }

        // Como os dicionários já estão ordenados, First() fornece o melhor preço e First da LinkedList fornece a ordem mais antiga daquele nível.
        public Order? GetBestBid() =>
            _bids.Count == 0
                ? null
                : _bids.First().Value.First!.Value;


        public Order? GetBestAsk() =>
            _asks.Count == 0
                ? null
                : _asks.First().Value.First!.Value;



        // Remoção usada pelo matching. Uma execução só pode consumir a ordem
        // que está na frente do nível de preço, preservando prioridade temporal.
        public void RemoveOrder(Order order)
        {
            if (!_ordersById.TryGetValue(order.Id, out var node))
                return;


            if (node.Previous != null)
                throw new InvalidOperationException(
                    "Only the first order of a price level can be removed during matching."
                );


            RemoveNode(node);
        }


        
        // Cancelamento solicitado pelo usuário. O índice por ID permite localizar qualquer resting order diretamente, independentemente da posição no nível.
        public string CancelOrder(long orderId, string ownerId)
        {
            if (!_ordersById.TryGetValue(orderId, out var node))
                throw new ArgumentException("ID não encontrado");


            if (node.Value.OwnerID != ownerId)
                throw new ArgumentException("Owner Diferente");


            RemoveNode(node);

            return "Ordem cancelada com sucesso";
        }

        // Centraliza a remoção para manter LinkedList, índice por ID e níveis de preço sempre consistentes entre si.
        private void RemoveNode(LinkedListNode<Order> node)
        {
            Order order = node.Value;

            LinkedList<Order> level = node.List!;


            level.Remove(node);          // O(1)

            _ordersById.Remove(order.Id); // O(1)


            if (level.Count == 0)
            {
                GetSide(order.Side).Remove(order.Price!.Value);
            }
        }
    }
}