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
- Order modification
- Owner validation for cancel and modify requests
- Best Bid / Best Ask retrieval
- Concurrent message submission
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

maps each order ID directly to its node, allowing cancellation and modification without scanning the entire order book.

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
- Execution timestamp

## Concurrency

The engine supports messages submitted concurrently from multiple threads.

New orders, cancellations and modifications are placed in a thread-safe queue:

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

This guarantees that matching, cancellation and modification never mutate the order book simultaneously while still allowing multiple producers to submit requests concurrently.

Processing priority is determined by the order in which messages enter the FIFO queue.

## Order Cancellation

Resting orders can be canceled by ID.

The engine validates:

- Whether the order exists
- Whether the cancellation request comes from the order owner

Cancellation is processed through the same worker used for matching, preserving serialized access to the order book.

## Order Modification

Resting orders can be modified by ID.

The user may change:

- Quantity only
- Price only
- Quantity and price together

The engine validates that the order exists and that the modification request comes from the order owner.

A modification is processed as a cancel-and-replace operation:

```text
Locate Order by ID
      |
      v
Remove from Book
      |
      v
Apply New Values
      |
      v
Run Matching Again
      |
      v
Rest Remaining Quantity
```

Because the modified order goes through the matching engine again, a new price may immediately cross the spread and generate one or more trades.

If quantity remains after matching, the Limit Order is inserted back into the appropriate price level.

The current implementation treats every modification as a new resting position, so the previous time priority is not preserved.

## CLI

The current command-line interface supports:

```text
BUY LMT QUANTITY @PRICE
SELL LMT QUANTITY @PRICE

BUY MKT QUANTITY
SELL MKT QUANTITY

CANCEL ORDER_ID

MODIFY ORDER_ID QUANTITY @PRICE
MODIFY ORDER_ID QUANTITY
MODIFY ORDER_ID @PRICE
```

Examples:

```text
BUY LMT 20 @99
SELL MKT 10

CANCEL 7

MODIFY 7 20 @101
MODIFY 7 20
MODIFY 7 @101
```

The CLI displays generated trades and the current state of the order book after processing operations.

A multithreaded test mode is also available to submit concurrent messages and observe how they are serialized by the engine.

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
- Order modification
- Re-matching after modification
- Owner validation for cancellation and modification
- Concurrent message ingestion
- Single-worker serialized processing
- Graceful queue shutdown
- Automated matching tests

## Next Steps

- Order status tracking
- Refine modification priority rules
- Pegged Orders
- Additional modification and concurrency tests
- Improve CLI visualization
- Improve validation and error handling