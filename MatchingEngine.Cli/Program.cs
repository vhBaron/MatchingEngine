using MatchingEngine;

// OrderBook mantém o estado das ordens resting. O MatchingEngine recebe
// o mesmo book e passa a ser o responsável por processar as mensagens.
var book = new OrderBook();

var engine = new MatchingEngine.MatchingEngine(book);


// ====================
// CONFIGURA EVENTOS E ENTRADA DA CLI
// ====================

// A CLI apenas reage aos eventos emitidos pela worker; ela não precisa acompanhar diretamente quando cada mensagem termina de ser processada.
engine.OrderProcessed += ExecutedOrder;
engine.PrintBook += CanceledModifiedOrder;
engine.OrdersListed += PrintMyOrders;

// Garantir a ordenação das mensagens processadas antes de continuar a CLI.
using var commandFinished = new SemaphoreSlim(0, 1);
engine.MessageProcessed += () => commandFinished.Release();

long nextID = 1;

Console.Write("Login: ");
string? login = Console.ReadLine();

PrintBook(book);

// Loop principal da interface de console. As operações são convertidas em EngineMessage e enfileiradas; o matching/cancelamento ocorre na worker.
while (true)
{
    Console.WriteLine();

    Console.Write("New Order: '1'\nCancel Order: '2'\nModify Order: '3'\nPegged Order: '4'\nList Orders: '5'\nPrint Book: '6'\nExit: '7'\nOperation: ");

    string? option = Console.ReadLine()?.Trim();

    if (string.IsNullOrWhiteSpace(option))
        continue;

    if (option.Equals("7", StringComparison.OrdinalIgnoreCase))
        break;

    switch (option)
    {
        case "1":

            Console.WriteLine();

            Console.Write("Nova ordem (BUY/SELL LMT QTD @ PRECO)  | (BUY/SELL MKT QTD): ");

            string? inputCreate = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(inputCreate))
                continue;

            try
            {
                NewOrder(inputCreate);
                await commandFinished.WaitAsync();
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
                await commandFinished.WaitAsync();
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
                await commandFinished.WaitAsync();
                break;
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Erro: {ex.Message} Tente novamente.");
            }
            break;

        case "4":
            Console.Write("Pegged Order (PEG BUY/SELL QTD): ");

            string? inputPegged = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(inputPegged))
                continue;

            try
            {
                PeggedOrder(inputPegged);
                await commandFinished.WaitAsync();
                break;
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Erro: {ex.Message} Tente novamente.");
            }
            break;

        case "5":
            try
            {
                engine.SubmitOrder(new EngineMessage
                {
                    Type = MessageType.ListOrders,
                    login = login
                });

                await commandFinished.WaitAsync();
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Erro: {ex.Message}");
            }
            break;
        case "6":
            PrintBook(book);
            break;
    }
}

try
{
    await engine.StopAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"O engine encerrou com erro: {ex.Message}");
}

// Exibe a visão atual do book. A ordenação já é fornecida pelo OrderBook.
static void PrintBook(OrderBook book)
{
    // Agrupa as ordens por preço.
    // A ordenação já vem do OrderBook.
    var bids = book.Bids
        .GroupBy(order => order.Price!.Value)
        .Select(level => new
        {
            Price = level.Key,
            Quantity = level.Sum(order => (long)order.Quantity),
            Orders = level.Count()
        })
        .ToList();

    var asks = book.Asks
        .GroupBy(order => order.Price!.Value)
        .Select(level => new
        {
            Price = level.Key,
            Quantity = level.Sum(order => (long)order.Quantity),
            Orders = level.Count()
        })
        .ToList();

    string FormatPrice(decimal price) =>
        price.ToString(
            "0.####",
            System.Globalization.CultureInfo.InvariantCulture);

    Console.WriteLine();
    Console.WriteLine(new string('=', 73));
    Console.WriteLine($"{"BID — COMPRA",-35} | {"ASK — VENDA",35}");
    Console.WriteLine(new string('-', 73));

    Console.WriteLine(
        $"{"ORDERS",7} | {"QTY",10} | {"PRICE",12} || " +
        $"{"PRICE",12} | {"QTY",10} | {"ORDERS",7}");

    Console.WriteLine(new string('-', 73));

    int rows = Math.Max(bids.Count, asks.Count);

    if (rows == 0)
    {
        Console.WriteLine("Livro vazio.");
    }

    for (int i = 0; i < rows; i++)
    {
        string bidOrders = "";
        string bidQuantity = "";
        string bidPrice = "";

        string askOrders = "";
        string askQuantity = "";
        string askPrice = "";

        if (i < bids.Count)
        {
            bidOrders = bids[i].Orders.ToString();
            bidQuantity = bids[i].Quantity.ToString();
            bidPrice = FormatPrice(bids[i].Price);
        }

        if (i < asks.Count)
        {
            askOrders = asks[i].Orders.ToString();
            askQuantity = asks[i].Quantity.ToString();
            askPrice = FormatPrice(asks[i].Price);
        }

        Console.WriteLine(
            $"{bidOrders,7} | {bidQuantity,10} | {bidPrice,12} || " +
            $"{askPrice,12} | {askQuantity,10} | {askOrders,7}");
    }

    Console.WriteLine(new string('-', 73));

    if (bids.Count > 0 && asks.Count > 0)
    {
        decimal spread = asks[0].Price - bids[0].Price;

        Console.WriteLine(
            $"Best bid: {FormatPrice(bids[0].Price)}  |  " +
            $"Best ask: {FormatPrice(asks[0].Price)}  |  " +
            $"Spread: {FormatPrice(spread)}");
    }
    else
    {
        Console.WriteLine("Spread: —");
    }

    Console.WriteLine(new string('=', 73));
}

// Valida o comando digitado, constrói a Order e envia uma mensagem para o engine.
// Nenhuma alteração no book é feita diretamente por este método.
void NewOrder(string? input = null)
{   if (string.IsNullOrWhiteSpace(input))
        throw new ArgumentException("Comando vazio.");

    input = input.Replace("@", " ");

    string[] parts = input.Split(
        ' ',
        StringSplitOptions.RemoveEmptyEntries);

    if (parts.Length < 3)
        throw new ArgumentException(
            "Formato: BUY/SELL LMT QTD @PRECO ou BUY/SELL MKT QTD");

    Side side;


    if (parts[0].Equals("BUY", StringComparison.OrdinalIgnoreCase))
        side = Side.Buy;

    else if (parts[0].Equals("SELL", StringComparison.OrdinalIgnoreCase))
        side = Side.Sell;

    else
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
    int executedQuantity = trades.Sum(trade => trade.Quantity);
    int originalQuantity = executedQuantity + order.Quantity;

    decimal? averagePrice = executedQuantity > 0
    ? trades.Sum(trade => trade.Price * trade.Quantity)
        / executedQuantity
    : null;

    string status;

    if (order.Quantity == 0)
        status = "FILLED";
    else if (executedQuantity > 0)
        status = "PARTFILL";
    else
        status = "UNFILLED";

    string side = order.Side.ToString().ToUpperInvariant();

    string FormatPrice(decimal? price) =>
        price.HasValue
            ? "@" + price.Value.ToString(
                "0.####",
                System.Globalization.CultureInfo.InvariantCulture)
            : "—";

    // Order não possui timestamp próprio.
    // Quando há trades, usamos o horário da última execução.
    string time = trades.Count > 0
        ? trades[^1].Timestamp.ToLocalTime().ToString("HH:mm:ss")
        : "—";

    Console.WriteLine();
    Console.WriteLine("-------------------------- Trades --------------------------");

    Console.WriteLine(
        $"{"SIDE",-6} | " +
        $"{"STATUS",-9} | " +
        $"{"TYPE",-5} | " +
        $"{"AMOUNT",8} | " +
        $"{"PRICE",12} | " +
        $"{"AVG PX",12} | " +
        $"{"TIME",-8}");

    Console.WriteLine(new string('-', 63));

    // Pai: ordem enviada e seu resultado final.
    Console.WriteLine(
        $"{side,-6} | " +
        $"{status,-9} | " +
        $"{order.Type,-5} | " +
        $"{originalQuantity,8} | " +
        $"{FormatPrice(order.Price),12} | " +
        $"{FormatPrice(averagePrice),12} | " +
        $"{time,-8}");

    // Filhos: cada execução gerada pelo matching.
    for (int i = 0; i < trades.Count; i++)
    {
        Trade trade = trades[i];

        string branch = i == trades.Count - 1 ? "└─" : "├─";
        string label = $"  {branch} EXECUTION";

        string tradeTime = trade.Timestamp
            .ToLocalTime()
            .ToString("HH:mm:ss");

        Console.WriteLine(
            $"{label,-26} | " +
            $"{trade.Quantity,8} | " +
            $"{FormatPrice(trade.Price),12} | " +
            $"{FormatPrice(trade.Price),12} | " +
            $"{tradeTime,-8}");
    }

    Console.WriteLine(new string('-', 63));

    if (order.Quantity > 0)
    {
        if (order.Type == OrderType.MKT)
        {
            Console.WriteLine(
                $"Saldo MKT não executado e descartado: {order.Quantity}");
        }
        else
        {
            Console.WriteLine(
                $"Saldo LMT no book: {order.Quantity}");
        }
    }
}

// Valida o comando de cancelamento e o transforma em uma mensagem.
// A validação de existência e propriedade da ordem acontece dentro do OrderBook.
void CancelOrder(string? input = null)
{
    if (string.IsNullOrWhiteSpace(input))
        throw new ArgumentException("Formato: CANCEL ID");

    string[] parts = input.Split(
        ' ',
        StringSplitOptions.RemoveEmptyEntries);

    if (parts.Length != 2 ||
        !parts[0].Equals("CANCEL", StringComparison.OrdinalIgnoreCase))
    {
        throw new ArgumentException("Formato: CANCEL ID");
    }

    if (!long.TryParse(parts[1], out long orderId) || orderId <= 0)
        throw new ArgumentException("ID inválido.");

    var msg = new EngineMessage
    {
        Type = MessageType.CancelOrder,
        OrderId = orderId,
        login = login
    };

    engine.SubmitOrder(msg);
}

// Handler acionado quando a tentativa de cancelamento termina; atualiza a visualização com o estado real do book.
void CanceledModifiedOrder()
{
    PrintBook(book);
}

// Valida o comando de criação de uma ordem PEG e envia a mensagem para o engine.
void PeggedOrder(string? input = null)
{
    if (string.IsNullOrWhiteSpace(input))
        throw new ArgumentException("Formato: PEG BUY/SELL QTD");

    string[] parts = input.Split(
        ' ',
        StringSplitOptions.RemoveEmptyEntries);

    if (parts.Length != 3 ||
        !parts[0].Equals("PEG", StringComparison.OrdinalIgnoreCase))
    {
        throw new ArgumentException("Formato: PEG BUY/SELL QTD");
    }

    Side side;

    if (parts[1].Equals("BUY", StringComparison.OrdinalIgnoreCase))
        side = Side.Buy;
    else if (parts[1].Equals("SELL", StringComparison.OrdinalIgnoreCase))
        side = Side.Sell;
    else
        throw new ArgumentException("Side inválido.");

    if (!int.TryParse(parts[2], out int quantity) || quantity <= 0)
        throw new ArgumentException(
            "A quantidade deve ser um inteiro maior que zero.");

    var order = new Order
    {
        Id = nextID++,
        OwnerID = login,
        Side = side,
        Type = OrderType.PEG,
        Price = null,
        Quantity = quantity
    };

    var msg = new EngineMessage
    {
        Type = MessageType.PeggedOrder,
        Order = order
    };

    engine.SubmitOrder(msg);
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

static void PrintMyOrders(List<Order> orders)
{
    Console.WriteLine();
    Console.WriteLine("---------------- Minhas ordens abertas ----------------");

    Console.WriteLine(
        $"{"ID",6} | " +
        $"{"SIDE",-6} | " +
        $"{"TYPE",-5} | " +
        $"{"LEAVES QTY",10} | " +
        $"{"PRICE",12}");

    Console.WriteLine(new string('-', 51));

    if (orders.Count == 0)
    {
        Console.WriteLine("Você não possui ordens abertas.");
    }

    foreach (Order order in orders)
    {
        string price = order.Price.HasValue
            ? order.Price.Value.ToString(
                "0.####",
                System.Globalization.CultureInfo.InvariantCulture)
            : "—";

        Console.WriteLine(
            $"{order.Id,6} | " +
            $"{order.Side.ToString().ToUpperInvariant(),-6} | " +
            $"{order.Type,-5} | " +
            $"{order.Quantity,10} | " +
            $"{price,12}");
    }

    Console.WriteLine(new string('-', 51));
}

