# Matching Engine

A single-asset, in-memory matching engine implemented in C# as part of the Morgan Stanley Strats Internship selection process.

The project simulates the core behavior of an electronic order matching system, supporting limit, market and pegged orders while preserving price-time priority and serialized access to the order book.

## Features

- Limit Orders (LMT)
- Market Orders (MKT)
- Pegged Orders (PEG)
- Price-time priority
- Partial fills
- Multi-level matching
- Trade generation
- Order cancellation
- Order modification
- Owner validation
- Open order listing by owner
- Best Bid / Best Ask
- Concurrent message submission
- Serialized order processing
- Automated tests

## Architecture

The application is divided into three projects:

```text
MatchingEngine.Core
    Order
    OrderBook
    MatchingEngine
    Trade

MatchingEngine.Cli
    Console interface

MatchingEngine.Tests
    Automated tests
```

The CLI is responsible only for receiving user commands and submitting messages to the engine.

All operations that may modify the order book are processed by the Matching Engine.

```text
CLI / Producers
      |
      v
BlockingCollection<EngineMessage>
      |
      v
ConcurrentQueue
      |
      v
Single Worker
      |
      v
Matching Engine
      |
      v
Order Book
```

This design allows multiple producers to submit messages concurrently while ensuring that the order book is modified sequentially.

## Order Book

Price levels are stored using:

```text
SortedDictionary<decimal, LinkedList<Order>>
```

Bids are ordered from highest to lowest price.

Asks are ordered from lowest to highest price.

Orders at the same price level are stored in a `LinkedList`, preserving FIFO priority.

An auxiliary index:

```text
Dictionary<long, LinkedListNode<Order>>
```

maps each order ID directly to its position in the book.

This allows cancellation and modification without scanning the entire order book.

## Matching Rules

Incoming orders always match against the best available price on the opposite side of the book.

### Limit Orders

A Limit Order continues matching while the opposite price satisfies its limit.

If quantity remains after matching, the order becomes a resting order in the book.

### Market Orders

A Market Order consumes available liquidity starting from the best price.

It continues until:

- The requested quantity is fully executed; or
- The opposite side of the book becomes empty.

Any remaining Market Order quantity is discarded and never rests in the book.

### Execution Price

Trades execute at the price of the resting order.

### Price-Time Priority

Orders are prioritized by:

1. Best price
2. Arrival order within the same price level

FIFO priority is preserved using the `LinkedList` associated with each price level.

## Order Modification

A resting order can have its:

- Quantity modified
- Price modified
- Quantity and price modified

Modification is implemented using cancel-and-replace semantics.

```text
Locate Order
     |
     v
Remove from Book
     |
     v
Apply Changes
     |
     v
Run Matching
     |
     v
Rest Remaining Quantity
```

Because the order is removed and reprocessed, it loses its previous time priority.

Changing the price may also immediately cross the spread and generate trades.

## Pegged Orders

Pegged Orders automatically follow the current best price on their side of the book.

The reference price is based on non-PEG Limit Orders:

```text
BUY PEG  -> Best non-PEG Bid
SELL PEG -> Best non-PEG Ask
```

When the reference price changes, resting PEG orders are automatically repositioned.

PEG orders do not define their own price.

Their quantity may be modified, but their price is controlled by the Matching Engine.

## Concurrency

Commands are submitted through a thread-safe queue:

```text
BlockingCollection<EngineMessage>
        +
ConcurrentQueue<EngineMessage>
```

Multiple threads may submit commands concurrently.

A single worker consumes those commands sequentially.

Therefore:

- Producers do not directly modify the order book
- Matching operations cannot mutate the book simultaneously
- Cancel and Modify use the same serialized processing path
- Message ordering is determined by FIFO queue insertion order

## CLI

Available operations:

```text
1 - New Order
2 - Cancel Order
3 - Modify Order
4 - Pegged Order
5 - List Orders
6 - Print Book
7 - Exit
```

### Limit Order

```text
BUY LMT 20 @99
SELL LMT 10 @101
```

### Market Order

```text
BUY MKT 20
SELL MKT 10
```

### List Orders

The `List Orders` option displays all currently resting orders owned by the logged-in user.

For each open order, the CLI shows:

- Order ID
- Side
- Order type
- Remaining quantity
- Price

Example:

```text
---------------- Minhas ordens abertas ----------------
    ID | SIDE   | TYPE  | LEAVES QTY |        PRICE
---------------------------------------------------
     7 | BUY    | LMT   |         20 |           99
     8 | SELL   | PEG   |         10 |          101
```

The order ID shown in this view is used by the `CANCEL` and `MODIFY` commands.

This makes `List Orders` the main way for the user to identify and manage their currently active orders.

### Cancel Order

First identify the desired order using `List Orders`, then use its ID:

```text
CANCEL 7
```

Only resting orders owned by the logged-in user can be cancelled.

### Modify Order

The order ID can also be obtained from `List Orders`.

Modify quantity and price:

```text
MODIFY 7 20 @101
```

Modify quantity only:

```text
MODIFY 7 20
```

Modify price only:

```text
MODIFY 7 @101
```

### Pegged Order

```text
PEG BUY 20
PEG SELL 20
```

### Book Visualization

The `Print Book` option displays the current aggregated market book rather than only the logged-in user's orders.

The view includes:

- Number of orders at each price level
- Total quantity
- Price
- Best Bid
- Best Ask
- Spread

Executed orders are displayed separately with their execution status and individual trades.

## Project Structure

```text
MatchingEngine/
│
├── MatchingEngine.Core/
│   ├── Orders.cs
│   ├── MatchingEngine.cs
│   └── Trade.cs
│
├── MatchingEngine.Cli/
│   └── Program.cs
│
└── MatchingEngine.Tests/
    └── ...
```

## Running

From the repository root:

```bash
dotnet build
```

Run the CLI:

```bash
dotnet run --project MatchingEngine.Cli
```

## Tests

Run all automated tests with:

```bash
dotnet test
```

The test suite covers the main order book and matching behaviors, including order execution, cancellation, modification and pegged order behavior.

## Design Decisions

### Single Writer Model

Instead of allowing multiple threads to mutate the Order Book directly, commands are serialized through a single worker.

This reduces synchronization complexity and provides deterministic mutation of the book.

### Resting Order Price

Trades execute at the resting order price, following the convention that the incoming order accepts the liquidity already available in the book.

### Modification Priority

A modified Limit Order is removed and processed again.

As a consequence, the order loses its original FIFO priority.

### In-Memory State

Orders and trades are maintained only in memory. No persistent storage is required for the scope of this project.