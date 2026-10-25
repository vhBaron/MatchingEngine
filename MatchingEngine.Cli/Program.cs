using MatchingEngine;

// OrderBook mantém o estado das ordens resting. O MatchingEngine recebe
// o mesmo book e passa a ser o responsável por processar as mensagens.
var book = new OrderBook();

var engine = new MatchingEngine.MatchingEngine(book);


// ====================

// POPULA BIDS

// ====================

book.AddOrder(new Order
{
    Id = 1,
    OwnerID = "Mercado",
    Side = Side.Buy,
    Type = OrderType.LMT,
    Price = 99,
    Quantity = 10
});

book.AddOrder(new Order
{
    Id = 2,
    OwnerID = "Mercado",
    Side = Side.Buy,
    Type = OrderType.LMT,
    Price = 98,
    Quantity = 20
});

book.AddOrder(new Order
{
    Id = 3,
    OwnerID = "Mercado",
    Side = Side.Buy,
    Type = OrderType.LMT,
    Price = 97,
    Quantity = 30
});


// ====================

// POPULA ASKS

// ====================

book.AddOrder(new Order
{
    Id = 4,
    OwnerID = "Mercado",
    Side = Side.Sell,
    Type = OrderType.LMT,
    Price = 101,
    Quantity = 10
});

book.AddOrder(new Order
{
    Id = 5,
    OwnerID = "Mercado",
    Side = Side.Sell,
    Type = OrderType.LMT,
    Price = 102,
    Quantity = 20
});

book.AddOrder(new Order
{
    Id = 6,
    OwnerID = "Mercado",
    Side = Side.Sell,
    Type = OrderType.LMT,
    Price = 103,
    Quantity = 30
});

// ====================
// CONFIGURA EVENTOS E ENTRADA DA CLI
// ====================

// A CLI apenas reage aos eventos emitidos pela worker; ela não precisa acompanhar diretamente quando cada mensagem termina de ser processada.
engine.OrderProcessed += ExecutedOrder;
engine.PrintBook += CanceledModifiedOrder;

long nextID = 7;

Console.Write("Login: ");
string? login = Console.ReadLine();

PrintBook(book);

// Loop principal da interface de console. As operações são convertidas em EngineMessage e enfileiradas; o matching/cancelamento ocorre na worker.
while (true)
{
    Console.WriteLine();

    Console.Write("New Order: '1'\nCancel Order: '2'\nModify Order: '3'\n[TEST] Run MultiThread tests: '4'\nExit: '5'\nOperation: ");

    string? option = Console.ReadLine()?.Trim();

    if (string.IsNullOrWhiteSpace(option))
        continue;

    if (option.Equals("5", StringComparison.OrdinalIgnoreCase))
        break;

    switch (option)
    {
        case "1":

            Console.WriteLine();

            Console.Write("Nova ordem (BUY/SELL LMT QTD @ PRECO  | BUY/SELL MKT QTD): ");

            string? inputCreate = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(inputCreate))
                continue;

            try
            {
                NewOrder(inputCreate);
                break;
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Erro: {ex.Message} Tente novamente.");
            }

            break;


        case "2":

            Console.WriteLine();

            Console.Write("Cancel Order (CANCEL ID): ");

            string? inputCancel = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(inputCancel))
                continue;

            try
            {
                CancelOrder(inputCancel);
                break;
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Erro: {ex.Message} Tente novamente.");
            }

            break;
        
        case "3":
            Console.Write("Modify Order (MODIFY ID QTD @ PRICE): ");

            string? inputModify = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(inputModify))
                continue;

            try
            {
                ModifyOrder(inputModify);
                break;
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Erro: {ex.Message} Tente novamente.");
            }
            break;
        case "4":
            await MultiThreadTests();
            break;
    }
}


// Exibe a visão atual do book. A ordenação já é fornecida pelo OrderBook.
static void PrintBook(OrderBook book)
{
    Console.WriteLine();

    Console.WriteLine("========= ORDER BOOK =========");

    Console.WriteLine();

    Console.WriteLine("ASKS");

    foreach (Order order in book.Asks)
    {
        Console.WriteLine(
            $"ID: {order.Id,2} | " +
            $"QTY: {order.Quantity,3} | " +
            $"PRICE: {order.Price}"
        );
    }

    Console.WriteLine();

    Console.WriteLine("-------- SPREAD --------");

    Console.WriteLine();

    Console.WriteLine("BIDS");

    foreach (Order order in book.Bids)
    {
        Console.WriteLine(
            $"ID: {order.Id,2} | " +
            $"QTY: {order.Quantity,3} | " +
            $"PRICE: {order.Price}"
        );
    }
}

// Valida o comando digitado, constrói a Order e envia uma mensagem para o engine.
// Nenhuma alteração no book é feita diretamente por este método.
void NewOrder(string? input = null)
{   input = input.Replace("@", " ");
    string[] parts = input.Split(
        ' ',
        StringSplitOptions.RemoveEmptyEntries
    );

    Side side;

    if (parts[0].Equals("BUY", StringComparison.OrdinalIgnoreCase))
        side = Side.Buy;

    else if (parts[0].Equals("SELL", StringComparison.OrdinalIgnoreCase))
        side = Side.Sell;

    else
    {
        throw new ArgumentException("Side inválido.");
    }

    if (parts.Length < 3)
    {
        throw new ArgumentException("Side inválido.");
    }

    OrderType type;

    if (parts[1].Equals("LMT", StringComparison.OrdinalIgnoreCase))
        type = OrderType.LMT;

    else if (parts[1].Equals("MKT", StringComparison.OrdinalIgnoreCase))
        type = OrderType.MKT;

    else
    {
        throw new ArgumentException("Tipo inválido.");
    }

    decimal? price = null;

    int quantity;


    // ====================
    // LMT exige preço e quantidade.
    // ====================

    if (type == OrderType.LMT)
    {
        if (parts.Length != 4)
        {
            Console.WriteLine(
                "Formato LMT: BUY/SELL LMT PRECO QUANTIDADE"
            );

            throw new ArgumentException("Formato inválido.");
        }

        if (!decimal.TryParse(parts[3], out decimal limitPrice))
        {
            Console.WriteLine("Preço inválido.");

            throw new ArgumentException("Preço inválido.");
        }

        if (!int.TryParse(parts[2], out quantity))
        {
            Console.WriteLine("Quantidade inválida.");

            throw new ArgumentException("Quantidade inválida.");
        }

        price = limitPrice;
            }

    // ====================
    //  MKT não possui preço limite; recebe apenas a quantidade.
    // ====================

    else
    {
        if (parts.Length != 3)
        {
            Console.WriteLine(
                "Formato MKT: BUY/SELL MKT QUANTIDADE"
            );

            throw new ArgumentException("Formato inválido.");
        }

        if (!int.TryParse(parts[2], out quantity))
        {
            Console.WriteLine("Quantidade inválida.");

            throw new ArgumentException("Quantidade inválida.");
        }
    }


    // ====================
    // Depois da validação, cria a ordem que será enviada ao engine.
    // ====================

    var incomingOrder = new Order
    {
        Id = nextID++,
        OwnerID = login,
        Side = side,
        Type = type,
        Price = price,
        Quantity = quantity
    };

    Console.WriteLine();

    if (type == OrderType.LMT)
    {
        Console.WriteLine(
            $"Enviando {side} LMT {quantity} @ {price}"
        );
    }
    else
    {
        Console.WriteLine(
            $"Enviando {side} MKT {quantity}"
        );
    }


    // A CLI encerra sua responsabilidade aqui: a mensagem entra na fila e será processada de forma sequencial pela worker do MatchingEngine.

    var msg = new EngineMessage {Type = MessageType.NewOrder, Order = incomingOrder};

    engine.SubmitOrder(msg);
    
}

// Handler chamado pelo evento OrderProcessed depois que o engine termina o matching de uma nova ordem.
void ExecutedOrder(Order order, List<Trade> trades)
{
    // ====================
    // Exibe todas as execuções geradas pela ordem recebida.
    // ====================

    Console.WriteLine();
    Console.WriteLine("========= TRADES =========");

    if (trades.Count == 0)
    {
        Console.WriteLine("Nenhum trade executado.");
    }

    foreach (Trade trade in trades)
    {
        Console.WriteLine(
            $"BUY: {trade.BuyOrderId} | " +
            $"SELL: {trade.SellOrderId} | " +
            $"QTY: {trade.Quantity} | " +
            $"PRICE: {trade.Price}"
        );
    }

    // ====================
    // Informa o resultado da ordem
    // ====================

    Console.WriteLine();

    if (order.Quantity == 0)
    {
        Console.WriteLine("Ordem totalmente executada.");
    }
    else if (order.Type == OrderType.MKT)
    {
        Console.WriteLine(
            $"MKT parcialmente executada. " +
            $"Quantidade não preenchida: {order.Quantity}"
        );
    }
    else
    {
        Console.WriteLine(
            $"Saldo da LMT no book: {order.Quantity}"
        );
    }
}

// Valida o comando de cancelamento e o transforma em uma mensagem.
// A validação de existência e propriedade da ordem acontece dentro do OrderBook.
void CancelOrder(string? input = null)
{
    string[] parts = input!.Split(
        ' ',
        StringSplitOptions.RemoveEmptyEntries
    );

    if (!parts[0].Equals("CANCEL", StringComparison.OrdinalIgnoreCase))
    {
        throw new ArgumentException("Formato: CANCEL ID");
    }

    if (!long.TryParse(parts[1], out long orderId))
    {
        throw new ArgumentException("ID inválido.");
    }

    // O cancelamento também passa pela mesma fila das novas ordens. Assim, cancel e matching nunca alteram o book simultaneamente.
    var msg = new EngineMessage {Type = MessageType.CancelOrder, OrderId = orderId, login = login };
    engine.SubmitOrder(msg);

    
}

// Handler acionado quando a tentativa de cancelamento termina; atualiza a visualização com o estado real do book.
void CanceledModifiedOrder()
{
    PrintBook(book);
}

// Valida o comando de modificação e o transforma em uma mensagem.
// A validação de existência e propriedade da ordem acontece dentro do OrderBook.
void ModifyOrder(string? input = null)
{
    decimal? price = null;
    int? quantity = null;

    if (string.IsNullOrWhiteSpace(input))
        throw new ArgumentException("Comando vazio.");

    string[] parts = input.Split(
        ' ',
        StringSplitOptions.RemoveEmptyEntries
    );

    if (parts.Length < 3 || parts.Length > 4)
        throw new ArgumentException(
            "Formato: MODIFY ID [QTD] [@PRICE]"
        );

    if (!parts[0].Equals(
        "MODIFY",
        StringComparison.OrdinalIgnoreCase))
    {
        throw new ArgumentException(
            "Formato: MODIFY ID [QTD] [@PRICE]"
        );
    }

    if (!long.TryParse(parts[1], out long orderId))
        throw new ArgumentException("ID inválido.");


    for (int i = 2; i < parts.Length; i++)
    {
        string value = parts[i];

        if (value.StartsWith("@"))
        {
            if (price.HasValue)
                throw new ArgumentException("Preço informado mais de uma vez.");

            string priceText = value.Substring(1);

            if (!decimal.TryParse(priceText, out decimal parsedPrice))
                throw new ArgumentException("Preço inválido.");

            price = parsedPrice;
        }
        else
        {
            if (quantity.HasValue)
                throw new ArgumentException(
                    "Formato inválido. Use @ antes do preço."
                );

            if (!int.TryParse(value, out int parsedQuantity))
                throw new ArgumentException("Quantidade inválida.");

            quantity = parsedQuantity;
        }
    }

    var msgModify = new EngineMessage {Type = MessageType.ModifyOrder, OrderId = orderId, login = login, Price = price, Quantity = quantity};

    engine.SubmitOrder(msgModify);
}


async Task MultiThreadTests()
{
    var testBook = new OrderBook();
    var testEngine = new MatchingEngine.MatchingEngine(testBook);

    const int numberOfOrders = 3;

    // 2 tasks por ID (Create + Cancel) + thread principal
    using var barrier = new Barrier(numberOfOrders * 2 + 1);

    var tasks = new List<Task>();

    for (int i = 1; i <= numberOfOrders; i++)
    {
        long id = i;

        // CREATE
        tasks.Add(Task.Run(() =>
        {
            var order = new Order
            {
                Id = id,
                OwnerID = login!,
                Side = Side.Buy,
                Type = OrderType.LMT,
                Price = 100m,
                Quantity = 10
            };

            Console.WriteLine(
                $"CREATE {id} pronta | Thread {Environment.CurrentManagedThreadId}"
            );

            barrier.SignalAndWait();

            var msg = new EngineMessage
            {
                Type = MessageType.NewOrder,
                Order = order
            };

            testEngine.SubmitOrder(msg);
        }));


        // CANCEL
        tasks.Add(Task.Run(() =>
        {
            Console.WriteLine(
                $"MODIFY {id} pronto | Thread {Environment.CurrentManagedThreadId}"
            );

            barrier.SignalAndWait();

            var msg = new EngineMessage {Type = MessageType.ModifyOrder, OrderId = id, login = login, Price = 200 , Quantity = 20};

            testEngine.SubmitOrder(msg);
        }));
    }

    Console.WriteLine();
    Console.WriteLine("Esperando todas as threads ficarem prontas...");

    barrier.SignalAndWait();

    Console.WriteLine("GO!");
    Console.WriteLine();

    await Task.WhenAll(tasks);

    // Todas as mensagens já foram enviadas.
    // Agora espera o engine terminar de consumir a fila.
    await testEngine.StopAsync();

    Console.WriteLine();
    Console.WriteLine("========= RESULTADO =========");

    PrintBook(testBook);
}