# StrangeChessWeb

A chess website powered by the **StrangeChess** engine (~1900 Elo, bitboards, magic bitboards, Zobrist hashing).

## Features

- **Classic mode:** standard chess vs bot or hot-seat (two humans, one screen)
- **Variant mode:** pieces expire after N turns, point economy, piece drops (game jam variant)

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- [Node.js 22+](https://nodejs.org/) (for the client, Phase 1+)

### Run locally

```powershell
# Server (from repo root)
dotnet run --project server/StrangeChess.Server

# Client (Phase 1+, separate terminal)
cd client
npm install
npm run dev
```

### Run tests

```powershell
dotnet test
```

### Sync engine files from reference repo

```powershell
.\scripts\sync-engine.ps1
```

## Decisions

- **Auto-queen promotion only.** The engine hardcodes queen in `movePiece`; no underpromotion UI in v1.
- **No repetition or 50-move draw** detection in v1.
- **Unicode chess glyphs** for pieces (zero asset setup; swappable for SVG later).
- **Stateless server:** game state lives in the browser; each request sends it, server loads FEN, computes, returns new state.

## Open Balance Questions

- Starting army evaporates fast with default lifetimes (everything but the king gone by ~turn 10). Intended?
- Should drops award points? (default: yes, every turn gives `pointsPerTurn`)
- Should piece costs scale with lifetime?

## Known Limits

- Engine is a process-wide singleton — one game computed at a time (global lock in `EngineFacade`).
- Free-tier Render: ~1 min cold start after 15 min idle, weaker bot due to limited CPU.
- No database, accounts, or multiplayer in v1.
