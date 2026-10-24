# Matching Engine

A single-asset in-memory matching engine developed in C# as part of the Morgan Stanley Strats Internship selection process.

The project focuses on order matching, price-time priority, efficient order book operations and serialized processing of concurrent requests.

## Features

- Limit Orders (LMT)
- Market Orders (MKT)
- Price-time priority
- Partial fills
- Multi-level matching
- Trade generation
- Order cancellation
- Best Bid / Best Ask retrieval
- Concurrent order submission
- FIFO message processing
- Automated tests

## Project Structure

- `MatchingEngine.Core` — order models, order book and matching logic
- `MatchingEngine.Cli` — command-line interface for interacting with the engine
- `MatchingEngine.Tests` — automated tests for matching and order book behavior

## Order Book

The order book is organized by price levels using:

```text
SortedDictionary<decimal, LinkedList<Order>>
```

Bids are ordered from highest to lowest price, while asks are ordered from lowest to highest.

Orders at the same price level are stored in a `LinkedList`, preserving FIFO priority.

An auxiliary index:

```text
Dictionary<long, LinkedListNode<Order>>
```

maps each order ID directly to its node, allowing cancellation without scanning the entire order book.

## Matching

Incoming orders are matched against the best available price on the opposite side of the book.

```text
Incoming Order
      |
      v
 Best Opposite Price
      |
      v
   Execute Trade
      |
      v
 Remaining Quantity?
```

Limit Orders continue matching while the available price satisfies their limit. Any remaining quantity is added to the order book.

Market Orders consume available liquidity until fully executed or until the opposite side of the book becomes empty. Unfilled Market Order quantity is not added to the book.

Trades are executed at the resting order price.

Each execution generates a `Trade` containing:

- Buy Order ID
- Sell Order ID
- Executed price
- Executed quantity

## Concurrency

The engine supports messages submitted concurrently from multiple threads.

New orders and cancellation requests are placed in a thread-safe queue:

```text
Multiple Producers
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

A single worker consumes and processes the messages sequentially.

This guarantees that matching and cancellation never modify the order book simultaneously while still allowing multiple producers to submit requests concurrently.

Processing priority is determined by the order in which messages enter the FIFO queue.

## Order Cancellation

Resting orders can be canceled by ID.

The engine validates:

- Whether the order exists
- Whether the cancellation request comes from the order owner

Cancellation and matching are processed through the same worker, preserving serialized access to the order book.

## CLI

The current command-line interface supports:

```text
BUY LMT PRICE QUANTITY
SELL LMT PRICE QUANTITY

BUY MKT QUANTITY
SELL MKT QUANTITY

CANCEL ORDER_ID
```

The CLI also displays generated trades and the current state of the order book.

A multithreaded test mode is available to submit concurrent create and cancel requests and observe how they are serialized by the engine.

## Current Status

Implemented:

- Order book with price-time priority
- Limit Order matching
- Market Order matching
- Partial fills
- Multi-level execution
- Trade generation
- Efficient order lookup by ID
- Order cancellation
- Owner validation for cancellations
- Concurrent message ingestion
- Single-worker serialized processing
- Graceful queue shutdown
- Automated matching tests

## Next Steps

- Order modification
- Order status tracking
- Pegged Orders
- Additional cancellation and concurrency tests
- Improve CLI visualization
- Improve validation and error handling