using System.Collections.Concurrent;

namespace MatchingEngine
{
    /*Responsável pelo processamento das ordens e execução do matching.
    Centraliza ordens LMT, MKT e PEG, além de cancelamentos e modificações,
    processando as mensagens sequencialmente para manter a consistência do OrderBook.*/
    public class MatchingEngine
    {
        private readonly OrderBook _orderBook;

        // Fila thread-safe de comandos enviados ao engine.
        // A ConcurrentQueue preserva a ordem de chegada e a BlockingCollection permite que a worker espere sem ficar consultando a fila continuamente.
        private readonly BlockingCollection<EngineMessage> _incomingOrders =
            new(new ConcurrentQueue<EngineMessage>()); // Fila de ordens, define prioridade

        private readonly Task _worker;

        // Eventos usados pela camada externa (CLI/UI) para reagir ao resultado
        public event Action<Order, List<Trade>>? OrderProcessed;
        public event Action? PrintBook;
        public event Action? MessageProcessed;
        public event Action<List<Order>>? OrdersListed;

        // Impede novas mensagens e aguarda a worker terminar tudo que já está na fila.
        public async Task StopAsync()
        {
            _incomingOrders.CompleteAdding();

            await _worker;
        }

        // Recebe o book compartilhado e inicia uma única worker responsável por consumir as mensagens sequencialmente.
        public MatchingEngine(OrderBook orderBook)
        {
            _orderBook = orderBook;

            _worker = Task.Run(ProcessMessages);
        }

        // Entrada pública do engine: produtores apenas enfileiram mensagens.
        // A alteração real do book acontece depois, dentro de ProcessMessages.
        public void SubmitOrder(EngineMessage msg)
        {
            try
            {
                _incomingOrders.Add(msg);
            }
            catch (InvalidOperationException)
            {
                throw new ArgumentException(
                    "O engine está encerrado ou indisponível.");
            }
        }


        // Event loop do engine. Todas as ações que podem alterar o book passam por este ponto, criando uma ordem total de processamento das mensagens.
        private void ProcessMessages()
        {
            try
            {
                foreach (EngineMessage msg in
                    _incomingOrders.GetConsumingEnumerable())
                {
                    switch (msg.Type)
                    {
                        case MessageType.NewOrder:
                            HandleNewOrder(msg.Order!);
                            break;

                        case MessageType.CancelOrder:
                            HandleCancelOrder(
                                msg.OrderId!.Value,
                                msg.login!);
                            break;

                        case MessageType.ModifyOrder:
                            HandleModifyOrder(
                                msg.OrderId!.Value,
                                msg.login!,
                                msg.Price,
                                msg.Quantity);
                            break;

                        case MessageType.PeggedOrder:
                            HandlePeggedOrder(msg.Order!);
                            break;
                        case MessageType.ListOrders:
                         var orders = _orderBook.Bids
                            .Concat(_orderBook.Asks)
                            .Where(order => order.OwnerID == msg.login)
                            .ToList();

                        OrdersListed?.Invoke(orders);
                        break;
                    }
                    MessageProcessed?.Invoke();
                }
            }
            catch (Exception ex)
            {
               
                // Não aceita novos comandos com o estado possivelmente inconsistente.
                _incomingOrders.CompleteAdding();

                Console.WriteLine(
                    $"Falha inesperada no engine. " +
                    $"O processamento foi interrompido: {ex.Message}");

                MessageProcessed?.Invoke();

                throw;
            }
        }

        // Processa uma nova ordem dentro da worker e notifica os assinantes quando o matching daquela mensagem termina.
        private void HandleNewOrder(Order order)
        {
            try
            {
                var trades = MatchOrder(order);
                OrderProcessed?.Invoke(order,trades);
                PrintBook?.Invoke();
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Erro ao enviar ordem: {ex.Message}");
            }
        }

        // Executa o cancelamento na mesma worker usada pelo matching.
        private void HandleCancelOrder(long orderId, string login)
        {
            try
            {
                var result = _orderBook.CancelOrder(orderId, login);
                Console.WriteLine(result);
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Erro ao cancelar ordem: {ex.Message}");
            }
            finally // Mesmo se o cancelamento falha imprime o book
            {
                PrintBook?.Invoke();
            }
        }

        // Executa a modificação na mesma worker usada pelo matching.
        private void HandleModifyOrder(long orderId, string login, decimal? newPrice, int? newQuantity)
        {
            try
            {
                Order modifiedOrder = _orderBook.ModifyOrder(orderId, login, newPrice, newQuantity);

                if (modifiedOrder.Type == OrderType.PEG)
                {
                    Console.WriteLine(modifiedOrder);
                    PrintBook?.Invoke();
                    return;
                }

                List<Trade> trades = MatchOrder(modifiedOrder);

                _orderBook.UpdatePeggedOrders(modifiedOrder);

                Console.WriteLine(modifiedOrder);
                OrderProcessed?.Invoke(modifiedOrder, trades);
                PrintBook?.Invoke();
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Erro ao modificar ordem: {ex.Message}");
            }
        }

        // Executa a ordem PEG na mesma worker usada pelo matching.
        private void HandlePeggedOrder(Order order)
        {
            try
            {
                _orderBook.PeggedOrder(order);
                PrintBook?.Invoke();
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Erro ao enviar ordem: {ex.Message}");
            }
        }

        // Direciona a ordem para o algoritmo correspondente ao lado da operação e retorna todos os trades gerados durante o matching.
        public List<Trade> MatchOrder(Order incomingOrder)
        {
            if (incomingOrder.Type != OrderType.LMT &&
                    incomingOrder.Type != OrderType.MKT)
            {
                throw new ArgumentException(
                    "Este método aceita apenas ordens LMT e MKT.");
            }

            if (incomingOrder.Quantity <= 0)
                throw new ArgumentException(
                    "A quantidade deve ser maior que zero.");

            if (incomingOrder.Type == OrderType.LMT &&
                (!incomingOrder.Price.HasValue ||
                incomingOrder.Price.Value <= 0))
            {
                throw new ArgumentException(
                    "Uma ordem LMT deve ter preço maior que zero.");
            }
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


        // BUY consome o melhor ASK disponível enquanto houver quantidade e, para LMT, enquanto o preço limite ainda aceitar o preço da ordem resting.
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


            // Uma MKT nunca descansa no book. Se sobrar quantidade de uma LMT, ela passa a ser uma resting order no seu nível de preço.
            if (incoming.Quantity > 0 &&
                incoming.Type == OrderType.LMT)
            {
                _orderBook.AddOrder(incoming);
            }


        }


        // SELL consome o melhor BID disponível enquanto houver quantidade e, para LMT,  enquanto o preço limite ainda aceitar o preço da ordem resting.
        private void MatchSellOrder(Order incoming, List<Trade> trades)
        {
            while (incoming.Quantity > 0)
            {
                Order? restingOrder = _orderBook.GetBestBid();


                if (restingOrder == null)
                    break;

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