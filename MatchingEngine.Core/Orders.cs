namespace MatchingEngine
{
    /*Define as estruturas utilizadas pelo sistema de ordens.
    Contém os tipos de ordem e mensagem, a representação de uma Order
    e o OrderBook responsável por armazenar e organizar as resting orders.
    Lado da ordem no mercado.*/
    public enum Side { Buy, Sell }

    // MKT executa contra a liquidez disponível; LMT também pode permanecer no book.
    public enum OrderType { MKT, LMT, PEG }

    // Tipos de comando aceitos pela fila do MatchingEngine.
    public enum MessageType {NewOrder, CancelOrder, ModifyOrder, PeggedOrder, ListOrders}

    // Representa uma ordem enviada ao engine ou armazenada no OrderBook.
    public class Order
    {
        public long Id { get; init; }

        public string OwnerID { get; init; }

        public Side Side { get; init; }

        public OrderType Type { get; init; }

        // É nullable porque ordens MKT não possuem preço limite.
        public decimal? Price { get; set; }

        public int Quantity { get; set; }
    }

    // Mensagem colocado na fila do engine. Cada MessageType utiliza apenas os campos necessários para aquela operação.
    public class EngineMessage
    {
        public MessageType Type { get; init; }

        public string? login { get; init; }

        public Order? Order { get; init; }

        public long? OrderId { get; init; }

        public decimal? Price { get; init; }

        public int? Quantity { get; set; }
    }

    // Representa e gerencia o livro de ordens do sistema.
    // Mantém BIDs e ASKs organizados por prioridade de preço e tempo, além de controlar inserções, cancelamentos, modificações e ordens PEG.
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

            if (order.Type != OrderType.LMT && order.Type != OrderType.PEG)
                throw new ArgumentException(
                    "Apenas ordens LMT e PEG podem permanecer no book.");

            if (order.Quantity <= 0)
                throw new ArgumentException(
                    "A quantidade deve ser maior que zero.");

            if (!order.Price.HasValue || order.Price.Value <= 0)
                throw new ArgumentException(
                    "O preço deve ser maior que zero.");

            if (_ordersById.ContainsKey(order.Id))
                throw new ArgumentException(
                    $"Order {order.Id} already exists in the book."
                );


            var bookSide = GetSide(order.Side);

            decimal price = order.Price.Value;

            // Melhor preço antes da inserção
            decimal? previousBestPrice =
                bookSide.Count > 0
                    ? bookSide.First().Key
                    : null;


            // Cria o nível de preço somente quando ele ainda não existe.
            if (!bookSide.TryGetValue(price, out LinkedList<Order>? level))
            {
                level = new LinkedList<Order>();

                bookSide.Add(price, level);
            }

            // A ordem mais nova entra no fim do nível, mantendo price-time priority.
            var node = level.AddLast(order); 
            _ordersById.Add(order.Id, node);

            decimal newBestPrice = bookSide.First().Key;

            bool changedBestPrice =
                previousBestPrice == null ||
                newBestPrice != previousBestPrice;

            if (changedBestPrice && newBestPrice == price)
            {
                UpdatePeggedOrders(order);
            }

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

        // Retorna a primeira ordem que não seja PEG, para servir de referência de preço para ordens PEG.
        private Order? GetBestReferenceOrder(Side side)
        {
            if (side == Side.Buy)
                return Bids.FirstOrDefault(order => order.Type != OrderType.PEG);

            return Asks.FirstOrDefault(order => order.Type != OrderType.PEG);
        }

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

            UpdatePeggedOrders(order);
        }

        // Cancelamento solicitado pelo usuário. O índice por ID permite localizar qualquer resting order diretamente, independentemente da posição no nível.
        public string CancelOrder(long orderId, string ownerId)
        {
            if (!_ordersById.TryGetValue(orderId, out var node))
                throw new ArgumentException("ID não encontrado");


            if (node.Value.OwnerID != ownerId)
                throw new ArgumentException("Owner Diferente");


            
            RemoveNode(node);

            UpdatePeggedOrders(node.Value);

            return "Ordem cancelada com sucesso";
        }
        
        // Modificação solicitado pelo usuário. O índice por ID permite localizar qualquer resting order diretamente, independentemente da posição no nível.
        public Order ModifyOrder(long orderId, string ownerId, decimal? newPrice = null, int? newQuantity = null)
        {
            if (!_ordersById.TryGetValue(orderId, out var node))
                throw new ArgumentException("ID não encontrado.");

            if (node.Value.OwnerID != ownerId)
                throw new ArgumentException("Owner diferente.");

            if (!newPrice.HasValue && !newQuantity.HasValue)
                throw new ArgumentException(
                    "É necessário fornecer preço ou quantidade.");

            // Valida antes de alterar ou remover a ordem original.
            if (newQuantity.HasValue && newQuantity.Value <= 0)
                throw new ArgumentException(
                    "A quantidade deve ser maior que zero.");

            if (newPrice.HasValue && newPrice.Value <= 0)
                throw new ArgumentException(
                    "O preço deve ser maior que zero.");

            Order order = node.Value;

            if (order.Type == OrderType.PEG)
            {
                if (newPrice.HasValue)
                    throw new ArgumentException(
                        "Pegged Order não permite modificar o preço.");

                if (newQuantity.HasValue)
                    order.Quantity = newQuantity.Value;

                return order;
            }

            RemoveNode(node);

            if (newPrice.HasValue)
                order.Price = newPrice.Value;

            if (newQuantity.HasValue)
                order.Quantity = newQuantity.Value;

            return order;
        }

        // Adiciona uma ordem PEG ao book
        public void PeggedOrder(Order order)
        {

            if (order.Type != OrderType.PEG)
                throw new ArgumentException("A ordem deve ser do tipo PEG.");

            if (order.Quantity <= 0)
                throw new ArgumentException(
                    "A quantidade deve ser maior que zero.");

            Order? restingOrder = GetBestReferenceOrder(order.Side);

            if (restingOrder == null)
                throw new ArgumentException(
                    "Não há ordem LMT do mesmo lado para servir de referência."); 

            Order peggedOrder = new Order
            {
                Id = order.Id,
                OwnerID = order.OwnerID,
                Side = order.Side,
                Type = OrderType.PEG,
                Price = restingOrder.Price,
                Quantity = order.Quantity
            };
            AddOrder(peggedOrder);
            

        }

        // Atualiza o preço de todas as ordens PEG do mesmo lado da ordem que acabou de ser inserida ou removida.
        public void UpdatePeggedOrders(Order order)
        {

            Order? reference = GetBestReferenceOrder(order.Side);

            var orders = order.Side == Side.Buy ? Bids : Asks;

            var peggedIds = orders
                .Where(x => x.Type == OrderType.PEG)
                .Select(x => x.Id)
                .ToList();
            if (reference == null)
            {
                foreach (long id in peggedIds)
                {
                    RemoveNode(_ordersById[id]);
                }

                return;
            }

            foreach (long id in peggedIds)
            {
                LinkedListNode<Order> oldNode = _ordersById[id];

                Order peggedOrder = oldNode.Value;

                decimal oldPrice = peggedOrder.Price!.Value;
                decimal newPrice = reference.Price!.Value;

                if (oldPrice == newPrice)
                    continue;

                var bookSide = GetSide(peggedOrder.Side);

                // Remove fisicamente do nível antigo
                LinkedList<Order> oldLevel = oldNode.List!;

                oldLevel.Remove(oldNode);

                // Remove o nível antigo se ele ficar vazio
                if (oldLevel.Count == 0)
                    bookSide.Remove(oldPrice);

                // Atualiza preço
                peggedOrder.Price = newPrice;

                // Cria/encontra novo nível
                if (!bookSide.TryGetValue(newPrice, out LinkedList<Order>? newLevel))
                {
                    newLevel = new LinkedList<Order>();
                    bookSide.Add(newPrice, newLevel);
                }

                // PEG entra atrás das ordens que já estavam nesse preço
                LinkedListNode<Order> newNode = newLevel.AddLast(peggedOrder);

                // Atualiza os índices
                _ordersById[id] = newNode;
            }
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