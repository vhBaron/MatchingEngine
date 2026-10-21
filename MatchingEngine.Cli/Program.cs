using MatchingEngine;

var book = new OrderBook();
var engine = new MatchingEngine.MatchingEngine(book);

const int numberOfOrders = 10;

using var barrier = new Barrier(numberOfOrders + 1);

var tasks = new List<Task>();

for (int i = 1; i <= numberOfOrders; i++)
{
    long id = i;

    tasks.Add(Task.Run(() =>
    {
        var order = new Order
        {
            Id = id,
            Side = Side.Buy,
            Type = OrderType.LMT,
            Price = 100m,
            Quantity = 10
        };

        Console.WriteLine(
            $"Order {id} pronta | Thread {Environment.CurrentManagedThreadId}"
        );

        barrier.SignalAndWait(); // Garante que todas as threads enviem as ordens ao mesmo tempo

        engine.SubmitOrder(order);
    }));
}

Console.WriteLine();
Console.WriteLine("Esperando todas as threads ficarem prontas...");

barrier.SignalAndWait();

Console.WriteLine("GO!");
Console.WriteLine();

await Task.WhenAll(tasks);

await engine.StopAsync();

Console.WriteLine();
Console.WriteLine("========= RESULTADO =========");

foreach (Order order in book.Bids)
{
    Console.WriteLine(
        $"ID: {order.Id,2} | " +
        $"SEQ: {order.Sequence,2} | " +
        $"PRICE: {order.Price}"
    );
}