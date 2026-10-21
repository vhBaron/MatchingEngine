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
- Limit order book structure implemented
- Order book organized by price levels using SortedDictionary<decimal, Queue<Order>>
- Buy orders sorted by highest price first
- Sell orders sorted by lowest price first
- Price-time priority preserved through serialized order processing
- Thread-safe order ingress implemented
- Concurrent orders are serialized before entering the matching engine
- FIFO incoming order queue with a single consumer
- Automated tests for price priority and concurrent order sequencing
- Concurrent order submission demo through the CLI


### Order Processing Flow

Orders can be submitted concurrently from multiple threads.
Each order receives a sequence number when it is officially accepted by the engine. Orders are then placed in a FIFO queue and processed by a single worker, ensuring deterministic processing order before interacting with the order book.

```text
Multiple Producers
        |
        v
   Order Ingress
        |
     Sequence
        |
        v
   Incoming Queue
        |
        v
 Single Consumer
        |
        v
    Order Book
```

Matching logic is the next major implementation step.