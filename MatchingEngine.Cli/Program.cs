using MatchingEngine;

var book = new OrderBook();
var engine = new MatchingEngine.MatchingEngine(book);

// =====================
// POPULA BIDS
// =====================

book.AddOrder(new Order
{
    Id = 1,
    Side = Side.Buy,
    Type = OrderType.LMT,
    Price = 99,
    Quantity = 10
});

book.AddOrder(new Order
{
    Id = 2,
    Side = Side.Buy,
    Type = OrderType.LMT,
    Price = 98,
    Quantity = 20
});

book.AddOrder(new Order
{
    Id = 3,
    Side = Side.Buy,
    Type = OrderType.LMT,
    Price = 97,
    Quantity = 30
});

// =====================
// POPULA ASKS
// =====================

book.AddOrder(new Order
{
    Id = 4,
    Side = Side.Sell,
    Type = OrderType.LMT,
    Price = 101,
    Quantity = 10
});

book.AddOrder(new Order
{
    Id = 5,
    Side = Side.Sell,
    Type = OrderType.LMT,
    Price = 102,
    Quantity = 20
});

book.AddOrder(new Order
{
    Id = 6,
    Side = Side.Sell,
    Type = OrderType.LMT,
    Price = 103,
    Quantity = 30
});


// =====================
// EXIBE BOOK INICIAL
// =====================

PrintBook(book);


// =====================
// TESTE DE MATCH
// =====================

long nextID=1;

while (true)
{
    Console.WriteLine();
    Console.Write("Nova ordem (BUY/SELL LMT PRECO QTD | BUY/SELL MKT QTD) ou EXIT: ");

    string? input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input))
        continue;

    if (input.Equals("EXIT", StringComparison.OrdinalIgnoreCase))
        break;

    string[] parts = input.Split(
        ' ',
        StringSplitOptions.RemoveEmptyEntries
    );

    // =====================
    // SIDE
    // =====================

    Side side;

    if (parts[0].Equals("BUY", StringComparison.OrdinalIgnoreCase))
        side = Side.Buy;
    else if (parts[0].Equals("SELL", StringComparison.OrdinalIgnoreCase))
        side = Side.Sell;
    else
    {
        Console.WriteLine("Side inválido.");
        continue;
    }


    // =====================
    // TYPE
    // =====================

    if (parts.Length < 3)
    {
        Console.WriteLine("Formato inválido.");
        continue;
    }

    OrderType type;

    if (parts[1].Equals("LMT", StringComparison.OrdinalIgnoreCase))
        type = OrderType.LMT;
    else if (parts[1].Equals("MKT", StringComparison.OrdinalIgnoreCase))
        type = OrderType.MKT;
    else
    {
        Console.WriteLine("Tipo inválido.");
        continue;
    }


    decimal? price = null;
    int quantity;


    // =====================
    // LIMIT ORDER
    // =====================

    if (type == OrderType.LMT)
    {
        if (parts.Length != 4)
        {
            Console.WriteLine(
                "Formato LMT: BUY/SELL LMT PRECO QUANTIDADE"
            );
            continue;
        }

        if (!decimal.TryParse(parts[2], out decimal limitPrice))
        {
            Console.WriteLine("Preço inválido.");
            continue;
        }

        if (!int.TryParse(parts[3], out quantity))
        {
            Console.WriteLine("Quantidade inválida.");
            continue;
        }

        price = limitPrice;
    }

    // =====================
    // MARKET ORDER
    // =====================

    else
    {
        if (parts.Length != 3)
        {
            Console.WriteLine(
                "Formato MKT: BUY/SELL MKT QUANTIDADE"
            );
            continue;
        }

        if (!int.TryParse(parts[2], out quantity))
        {
            Console.WriteLine("Quantidade inválida.");
            continue;
        }
    }


    // =====================
    // CRIA ORDEM
    // =====================

    var incomingOrder = new Order
    {
        Id = nextID++,
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


    // =====================
    // MATCH
    // =====================

    var trades = engine.ProcessOrder(incomingOrder);


    // =====================
    // TRADES
    // =====================

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


    // =====================
    // RESULTADO DA ORDEM
    // =====================

    Console.WriteLine();

    if (incomingOrder.Quantity == 0)
    {
        Console.WriteLine("Ordem totalmente executada.");
    }
    else if (type == OrderType.MKT)
    {
        Console.WriteLine(
            $"MKT parcialmente executada. " +
            $"Quantidade não preenchida: {incomingOrder.Quantity}"
        );
    }
    else
    {
        Console.WriteLine(
            $"Saldo da LMT no book: {incomingOrder.Quantity}"
        );
    }


    // =====================
    // BOOK
    // =====================

    PrintBook(book);
}

     

    
// =====================
// PRINT DO BOOK
// =====================
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
    Console.WriteLine("--------- SPREAD ---------");
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