using System.Collections.Concurrent;
using System.Diagnostics;

namespace MatchingEngine
{
    // Centraliza o processamento das mensagens do mercado.
    // Apenas a worker interna altera o OrderBook, evitando concorrência direta entre envio, cancelamento e futuras modificações de ordens.
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
        public event Action? OrderCanceled;

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

            _worker = Task.Run(ProcessOrders);
        }

        // Entrada pública do engine: produtores apenas enfileiram mensagens.
        // A alteração real do book acontece depois, dentro de ProcessOrders.
        public void SubmitOrder(EngineMessage msg) =>
            _incomingOrders.Add(msg);


        // Event loop do engine. Todas as ações que podem alterar o book passam por este ponto, criando uma ordem total de processamento das mensagens.
        private void ProcessOrders()
        {
            foreach (EngineMessage msg in _incomingOrders.GetConsumingEnumerable())
            {
                switch (msg.Type)
                {
                    case MessageType.NewOrder:
                        msgNewOrder(msg.Order!);
                        break;

                    case MessageType.CancelOrder:
                        msgCancelOrder(msg.OrderId!.Value, msg.login!);
                        break;
                }
               
            }
        }

        // Processa uma nova ordem dentro da worker e notifica os assinantes quando o matching daquela mensagem termina.
        private void msgNewOrder(Order order)
        {
            try
            {
                var trades = ProcessOrder(order);
                OrderProcessed?.Invoke(order,trades);
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Erro ao enviar ordem: {ex.Message}");
            }
        }

        // Executa o cancelamento na mesma worker usada pelo matching.
        private void msgCancelOrder(long orderId, string login)
        {
            try
            {
                var result = CancelOrder(orderId, login);
                Console.WriteLine(result);
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Erro ao cancelar ordem: {ex.Message}");
            }
            finally // Mesmo se o cancelamento falha imprime o book
            {
                OrderCanceled?.Invoke();
            }
        }

        // Direciona a ordem para o algoritmo correspondente ao lado da operação e retorna todos os trades gerados durante o matching.
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

        // Delega ao OrderBook a validação de existência/propriedade e a remoção.
        public string CancelOrder(long id, string login)
        {
            var cancel = _orderBook.CancelOrder(id, login);
            return cancel;
        }
    }
}