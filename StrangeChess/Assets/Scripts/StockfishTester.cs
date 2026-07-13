using UnityEngine;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;

public class StockfishTester : MonoBehaviour
{
    public static StockfishTester Instance; // Added Singleton for easy access!

    [SerializeField] private bool canStockfishPlay = false;
    private Process stockfishProcess;
    private StreamWriter uciInput;
    private bool isEngineRunning = false;
    private ConcurrentQueue<string> outputQueue = new ConcurrentQueue<string>();
    private bool isCalculating = false;
    
    // THE CURE FOR AMNESIA:
    public List<string> moveHistory = new List<string>();

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        if(!canStockfishPlay)
            return;
        StartStockfish();
        if (isEngineRunning) ConfigureStockfish();
    }

    void StartStockfish()
    {        
        string engineFileName = "stockfish/stockfish-windows-x86-64-avx2.exe"; 
        string enginePath = Path.Combine(Application.streamingAssetsPath, engineFileName); 

        if (!File.Exists(enginePath)) return;

        stockfishProcess = new Process();
        stockfishProcess.StartInfo.FileName = enginePath;
        stockfishProcess.StartInfo.UseShellExecute = false;
        stockfishProcess.StartInfo.RedirectStandardInput = true;
        stockfishProcess.StartInfo.RedirectStandardOutput = true;
        stockfishProcess.StartInfo.CreateNoWindow = true; 

        stockfishProcess.OutputDataReceived += (sender, args) => 
        {
            if (!string.IsNullOrEmpty(args.Data)) outputQueue.Enqueue(args.Data); 
        };

        stockfishProcess.Start();
        stockfishProcess.BeginOutputReadLine(); 

        uciInput = stockfishProcess.StandardInput;
        isEngineRunning = true;
    }

    void ConfigureStockfish()
    {
        uciInput.WriteLine("uci");
        uciInput.WriteLine("setoption name UCI_LimitStrength value true");
        uciInput.WriteLine("setoption name UCI_Elo value 3190");
        uciInput.WriteLine("isready");
    }

    void Update()
    {
        if(!canStockfishPlay)
            return;
        // AUTOMATE STOCKFISH: If it is White's turn, and Stockfish isn't currently calculating...
        if (ClickDetector.Instance.isWhiteTurn && isEngineRunning && !isCalculating)
        {
            string moveCommand = "position startpos";
            if (moveHistory.Count > 0)
            {
                moveCommand += " moves " + string.Join(" ", moveHistory);
            }

            uciInput.WriteLine(moveCommand);
            uciInput.WriteLine("go movetime 1000"); // 1 second per move
            isCalculating = true; // Lock it so we don't spam the engine
        }

        while (outputQueue.TryDequeue(out string message))
        {
            if (message == "readyok")
            {
                UnityEngine.Debug.Log("<color=#FFD700>--- STOCKFISH IS READY TO PLAY ---</color>");
            }
            else if (message.StartsWith("bestmove"))
            {
                string[] parts = message.Split(' ');
                string moveString = parts[1]; 
                
                // --- THE FIX: CHECKMATE DETECTION ---
                if (moveString == "(none)")
                {
                    UnityEngine.Debug.Log("<color=#FF0000>GAME OVER! Stockfish says: (none). It has been checkmated or stalemated!</color>");
                    isEngineRunning = false; // Stop the engine loop
                    break; 
                }
                
                ExecuteStockfishMove(moveString);
                isCalculating = false; 
            }
        }
    }

    void ExecuteStockfishMove(string moveString)
    {
        // Add Stockfish's move to the history so it doesn't get amnesia!
        moveHistory.Add(moveString);

        string startSquareStr = moveString.Substring(0, 2);
        string targetSquareStr = moveString.Substring(2, 2);

        int startIndex = AlgebraicToIndex(startSquareStr);
        int targetIndex = AlgebraicToIndex(targetSquareStr);

        ulong startSquare = 1UL << startIndex;
        ulong targetSquare = 1UL << targetIndex;

        // Grab the piece identities to check for special moves
        pieceType movingPiece = Board.Instance.boardSquares[startIndex];
        pieceType targetPiece = Board.Instance.boardSquares[targetIndex];

        // --- VISUAL ROUTING FOR STOCKFISH ---
        
        // 1. Pawn Promotion (Stockfish sends "e7e8q")
        if (moveString.Length == 5 || (movingPiece == pieceType.whitePawn && targetIndex >= 56) || (movingPiece == pieceType.blackPawn && targetIndex <= 7)) 
        {
            bool isWhite = movingPiece == pieceType.whitePawn;
            Board.Instance.PromoteVisualPiece(startIndex, targetIndex, isWhite);
        }
        // 2. Castling (White & Black)
        else if (movingPiece == pieceType.whiteKing && startIndex == 4 && targetIndex == 6) { Board.Instance.Move3DModel(startSquare, targetSquare); Board.Instance.Move3DModel(1UL << 7, 1UL << 5); } 
        else if (movingPiece == pieceType.whiteKing && startIndex == 4 && targetIndex == 2) { Board.Instance.Move3DModel(startSquare, targetSquare); Board.Instance.Move3DModel(1UL << 0, 1UL << 3); } 
        else if (movingPiece == pieceType.blackKing && startIndex == 60 && targetIndex == 62) { Board.Instance.Move3DModel(startSquare, targetSquare); Board.Instance.Move3DModel(1UL << 63, 1UL << 61); } 
        else if (movingPiece == pieceType.blackKing && startIndex == 60 && targetIndex == 58) { Board.Instance.Move3DModel(startSquare, targetSquare); Board.Instance.Move3DModel(1UL << 56, 1UL << 59); } 
        // 3. Normal Moves
        else 
        {
            Board.Instance.Move3DModel(startSquare, targetSquare);
        }

        // 4. En Passant Visual Destruction 
        if (movingPiece == pieceType.whitePawn && targetPiece == pieceType.none && (targetIndex == startIndex + 7 || targetIndex == startIndex + 9)) Board.Instance.DestroyVisualPiece(targetIndex - 8);
        else if (movingPiece == pieceType.blackPawn && targetPiece == pieceType.none && (targetIndex == startIndex - 7 || targetIndex == startIndex - 9)) Board.Instance.DestroyVisualPiece(targetIndex + 8);

        // Physically update your internal bitboards
        Chess.Instance.movePiece(startSquare, targetSquare);

        // Swap turns
        ClickDetector.Instance.isWhiteTurn = !ClickDetector.Instance.isWhiteTurn;
        
        UnityEngine.Debug.Log($"Executed Stockfish move: {startSquareStr} to {targetSquareStr}");
    }

    // --- NEW HELPER METHODS ---

    // ClickDetector will call this when YOU make a move
    public void ReportUserMove(ulong fromSquare, ulong toSquare, pieceType movingPiece)
    {
        int fromIndex = Board.Instance.GetBitboardIndex(fromSquare);
        int toIndex = Board.Instance.GetBitboardIndex(toSquare);
        
        string move = IndexToAlgebraic(fromIndex) + IndexToAlgebraic(toIndex);

        // Handle UCI promotion formatting (e.g., e7e8q)
        if (movingPiece == pieceType.whitePawn && toIndex >= 56) move += "q";
        else if (movingPiece == pieceType.blackPawn && toIndex <= 7) move += "q";

        moveHistory.Add(move);
    }

    int AlgebraicToIndex(string square)
    {
        int file = square[0] - 'a'; 
        int rank = square[1] - '1'; 
        return (rank * 8) + file;
    }

    public string IndexToAlgebraic(int index)
    {
        int file = index % 8;
        int rank = index / 8;
        return $"{(char)('a' + file)}{rank + 1}";
    }

    void OnApplicationQuit()
    {
        if (isEngineRunning && stockfishProcess != null && !stockfishProcess.HasExited)
        {
            uciInput.WriteLine("quit");
            stockfishProcess.Close();
        }
    }
}