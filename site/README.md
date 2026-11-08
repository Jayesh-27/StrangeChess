# StrangeChess website

The showcase site for the StrangeChess engine. The engine in `../Engine` is compiled to
WebAssembly (`../wasm`) and runs in a Web Worker, so visitors play the real C# engine.

## Run locally

```bash
npm install
npm run dev
```

## Rebuild the engine for the web

Only needed after changing `../Engine`. Needs the .NET 10 SDK and `dotnet workload install wasm-tools`.

```bash
bash ../wasm/build.sh
```

The output lands in `public/engine/_framework/` and is committed, because Vercel can't build .NET.

## Match data

`scripts/matches.pgn` is the cutechess-cli output. After a new run, replace it and regenerate
`src/data/match.json` (score + the replayed win):

```bash
npm run replay
```

## Deploy on Vercel

Import the GitHub repo, set **Root Directory** to `site`, and set the production branch to `site`
(Project Settings → Environments → Production → Branch Tracking).
