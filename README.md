# Matching Engine

A simple in-memory order matching engine developed in C# as part of the Morgan Stanley Strats Internship selection process.

## About

The goal of this project is to implement a basic matching engine for a single asset.

The engine will maintain an order book and process different types of orders according to price and time priority.

The challenge includes support for:

- Limit orders
- Market orders
- Order book visualization
- Order cancellation
- Order modification
- Pegged orders

The implementation will be developed incrementally, with the matching logic kept separate from the command-line interface.

## Project Structure

- `MatchingEngine.Core` — order models, order book and matching logic
- `MatchingEngine.Cli` — command-line interface used to interact with the engine
- `MatchingEngine.Tests` — automated tests for the matching behavior

## Current Status

- Basic order structure implemented
- Limit order book organized by price levels using `SortedDictionary<decimal, Queue<Order>>`
- Buy orders prioritized by highest price
- Sell orders prioritized by lowest price
- FIFO priority preserved within each price level
- Thread-safe order ingress with serialized processing
- Concurrent orders assigned an execution sequence before processing
- Limit order matching
- Market order matching
- Partial fills
- Multi-level order execution
- Best Bid / Best Ask retrieval
- Trade generation
- Filled orders automatically removed from the order book
- Unfilled Limit Orders remain in the book
- Unfilled Market Orders are discarded
- Automated tests covering order book priority, Limit Orders and Market Orders

### Order Processing Flow

Orders can be submitted concurrently from multiple threads.

Each order receives a sequence number when it is accepted by the engine and is processed by a single consumer. The matching engine then attempts to execute the incoming order against the best available prices in the opposite side of the book.

```text
Multiple Producers
        |
        v
   Order Ingress
        |
        v
     Sequence
        |
        v
   Incoming Queue
        |
        v
 Single Consumer
        |
        v
  Matching Engine
      /      \
     v        v
 Best Bid   Best Ask
      \      /
       v    v
       Trades
         |
         v
    Order Book
```

Limit Orders may remain in the order book when they cannot be fully executed at their specified price or better.

Market Orders consume the best available prices until fully executed or until the opposite side of the book has no remaining liquidity.

## Next Steps

- Order cancellation
- Order modification
- Order status tracking
- Pegged orders
- Improve CLI visualization
- Additional matching and concurrency tests