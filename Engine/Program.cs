using System;

class Program
{
    static void Main(string[] args)
    {
        Board.Instance = new Board();
        Chess.Instance = new Chess();
        AI.Instance = new AI();

        Board.Instance.Initialize();
        Console.WriteLine("Engine initialized. You are White.");

        while (true)
        {
            if (Board.Instance.isWhiteTurn)
            {
                Console.Write("\nEnter move (e.g. e2e4): ");
                string input = Console.ReadLine();

                // 1. Parse the text into bitboard indices
                int fromFile = input[0] - 'a';
                int fromRank = input[1] - '1';
                int toFile = input[2] - 'a';
                int toRank = input[3] - '1';

                int fromIndex = fromRank * 8 + fromFile;
                int toIndex = toRank * 8 + toFile;

                ulong fromSquare = 1UL << fromIndex;
                ulong toSquare = 1UL << toIndex;

                // 2. Apply the human move
                Chess.Instance.movePiece(fromSquare, toSquare);
                Board.Instance.isWhiteTurn = false;
            }
            else
            {
                Console.WriteLine("\nAI is thinking...");
                
                // 3. Trigger the AI
                AI.Instance.timeLimitMs = 3000;
                AI.Instance.PlayBestMove(6); 
                
                // 4. Show the board so you can see what happened
                Console.WriteLine("AI Moved! Current Occupied Squares:");
                Console.WriteLine(Board.Instance.BitboardToBoardString(Board.Instance.allPieces));
            }
        }
    }
}