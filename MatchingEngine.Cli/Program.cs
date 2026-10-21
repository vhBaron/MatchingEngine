using MatchingEngine;

var book = new OrderBook();
long nextId = 1;

while (true)
{
    PrintBook(book);

    Console.WriteLine();
    Console.Write("Nova ordem (BUY/SELL PRECO QUANTIDADE) ou EXIT: ");

    string? input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input))
        continue;

    if (input.Equals("EXIT", StringComparison.OrdinalIgnoreCase))
        break;

    string[] parts = input.Split(' ');

    if (parts.Length != 3)
    {
        Console.WriteLine("Formato inválido.");
        Console.ReadKey();
        continue;
    }

    Side side;

    if (parts[0].Equals("BUY", StringComparison.OrdinalIgnoreCase))
        side = Side.Buy;
    else if (parts[0].Equals("SELL", StringComparison.OrdinalIgnoreCase))
        side = Side.Sell;
    else
    {
        Console.WriteLine("Side inválido.");
        Console.ReadKey();
        continue;
    }

    if (!decimal.TryParse(parts[1], out decimal price))
    {
        Console.WriteLine("Preço inválido.");
        Console.ReadKey();
        continue;
    }

    if (!int.TryParse(parts[2], out int quantity))
    {
        Console.WriteLine("Quantidade inválida.");
        Console.ReadKey();
        continue;
    }

    var order = new Order
    {
        Id = nextId++,
        Side = side,
        Type = OrderType.LMT,
        Price = price,
        Quantity = quantity
    };

    book.AddOrder(order);

    Console.Clear();
}

static void PrintBook(OrderBook book)
{
    Console.WriteLine("========== ORDER BOOK ==========");
    Console.WriteLine();

    Console.WriteLine("ASKS");
    Console.WriteLine("-------------------------------");

    foreach (Order order in book.Asks)
    {
        Console.WriteLine(
            $"{order.Id,3} | {order.Price,8} | {order.Quantity,8}"
        );
    }

    Console.WriteLine();
    Console.WriteLine("-------------------------------");
    Console.WriteLine();

    Console.WriteLine("BIDS");
    Console.WriteLine("-------------------------------");

    foreach (Order order in book.Bids)
    {
        Console.WriteLine(
            $"{order.Id,3} | {order.Price,8} | {order.Quantity,8}"
        );
    }

    Console.WriteLine();
    Console.WriteLine("===============================");
}