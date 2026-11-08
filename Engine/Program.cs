using System;

class Program
{
    static void Main(string[] args)
    {
        Board.Instance = new Board();
        Chess.Instance = new Chess();
        AI.Instance = new AI();

        Board.Instance.Initialize();

        while (true)
        {
            string? line = Console.ReadLine();
            if (string.IsNullOrEmpty(line)) continue;

            string[] tokens = line.Trim().Split(' ');
            string command = tokens[0];

            switch (command)
            {
                case "uci":
                    Console.WriteLine("id name StrangeChess");
                    Console.WriteLine("id author Jayesh");
                    Console.WriteLine("uciok");
                    break;

                case "isready":
                    Console.WriteLine("readyok");
                    break;

                case "ucinewgame":
                    Board.Instance.Initialize();
                    Array.Clear(AI.Instance.transpositionTable, 0, AI.Instance.transpositionTable.Length);
                    Array.Clear(AI.Instance.killerMoves, 0, AI.Instance.killerMoves.Length);
                    break;

                case "position":
                    HandlePosition(tokens);
                    break;

                case "go":
                    HandleGo(tokens);
                    break;

                case "quit":
                    return;
            }
        }
    }

    static void HandlePosition(string[] tokens)
    {
        int movesIndex = Array.IndexOf(tokens, "moves");

        if (tokens[1] == "startpos")
        {
            Board.Instance.LoadFEN("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        }
        else if (tokens[1] == "fen")
        {
            int fenEnd = (movesIndex != -1) ? movesIndex : tokens.Length;
            string fen = string.Join(" ", tokens[2..fenEnd]);
            Board.Instance.LoadFEN(fen);
        }

        if (movesIndex != -1)
        {
            for (int i = movesIndex + 1; i < tokens.Length; i++)
            {
                ApplyUciMoveString(tokens[i]);
            }
        }
    }

    static void HandleGo(string[] tokens)
    {
        int searchDepth = 63;
        
        // Default fallback
        AI.Instance.timeLimitMs = 1000; 

        long timeLeft = 0;
        long increment = 0;
        bool isTimeSet = false;

        for (int i = 1; i < tokens.Length; i++)
        {
            if (tokens[i] == "depth" && i + 1 < tokens.Length)
                searchDepth = int.Parse(tokens[i + 1]);
            
            if (tokens[i] == "movetime" && i + 1 < tokens.Length)
            {
                AI.Instance.timeLimitMs = long.Parse(tokens[i + 1]);
                isTimeSet = true;
            }
            else if (Board.Instance.isWhiteTurn)
            {
                if (tokens[i] == "wtime" && i + 1 < tokens.Length) timeLeft = long.Parse(tokens[i + 1]);
                if (tokens[i] == "winc" && i + 1 < tokens.Length) increment = long.Parse(tokens[i + 1]);
            }
            else if (!Board.Instance.isWhiteTurn)
            {
                if (tokens[i] == "btime" && i + 1 < tokens.Length) timeLeft = long.Parse(tokens[i + 1]);
                if (tokens[i] == "binc" && i + 1 < tokens.Length) increment = long.Parse(tokens[i + 1]);
            }
        }

        // Only apply standard calculation if movetime wasn't explicitly forced
        if (!isTimeSet && timeLeft > 0)
        {
            // Allocate a fraction of the remaining time (e.g., 1/40th for a standard game phase) 
            // plus a safe percentage of the increment so we don't bleed the clock dry.
            AI.Instance.timeLimitMs = Math.Max(50, (timeLeft / 40) + (increment * 3 / 4));
        }

        ushort bestMove = AI.Instance.GetBestMove(searchDepth);
        Console.WriteLine($"bestmove {Chess.Instance.FormatUciMove(bestMove)}");
    }

    static void ApplyUciMoveString(string moveStr)
    {
        int fromIndex = (moveStr[1] - '1') * 8 + (moveStr[0] - 'a');
        int toIndex = (moveStr[3] - '1') * 8 + (moveStr[2] - 'a');

        ulong fromSquare = 1UL << fromIndex;
        ulong toSquare = 1UL << toIndex;

        Chess.Instance.movePiece(fromSquare, toSquare);
        Board.Instance.isWhiteTurn = !Board.Instance.isWhiteTurn;
    }
}