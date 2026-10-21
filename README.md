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
- Limit order book implemented
- Buy orders sorted by highest price first
- Sell orders sorted by lowest price first
- Simple CLI for inserting and visualizing orders
- Initial automated tests for order book behavior