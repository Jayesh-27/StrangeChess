using UnityEngine;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;

public class EngineTournament : MonoBehaviour
{
    [Header("Tournament Settings")]
    public bool startTournament = false;
    public int startingElo = 1200;
    public int eloStep = 100;
    public int gamesPerMatch = 2; // Must be even so both play White/Black equally
    
    [Header("Time Controls")]
    public int myEngineTimeMs = 1000;
    public int stockfishTimeMs = 500; // Stockfish needs less time

    [Header("Live Stats (Read Only)")]
    public int currentElo;
    public int matchGameCount = 0;
    public float matchScore = 0; // 1 = Win, 0.5 = Draw
    public bool isMyEngineWhite = true;

    private Process stockfishProcess;
    private StreamWriter uciInput;
    private ConcurrentQueue<string> outputQueue = new ConcurrentQueue<string>();

    private bool isEngineRunning = false;
    private bool isStockfishThinking = false;
    private bool isWhiteTurn = true;
    private List<string> moveHistory = new List<string>();

    void Start()
    {
        if (!startTournament) return;

        // 1. Disable UI and Visual scripts so they don't interfere with the math
        if (ClickDetector.Instance != null) ClickDetector.Instance.enabled = false;
        if (AI.Instance != null) AI.Instance.enabled = false;
        if (StockfishTester.Instance != null) StockfishTester.Instance.enabled = false;

        currentElo = startingElo;
        AI.Instance.timeLimitMs = myEngineTimeMs;

        StartStockfish();
        ConfigureStockfish();
    }

    void StartStockfish()
    {
        string enginePath = Path.Combine(Application.streamingAssetsPath, "stockfish/stockfish-windows-x86-64-avx2.exe");
        if (!File.Exists(enginePath)) return;

        stockfishProcess = new Process();
        stockfishProcess.StartInfo.FileName = enginePath;
        stockfishProcess.StartInfo.UseShellExecute = false;
        stockfishProcess.StartInfo.RedirectStandardInput = true;
        stockfishProcess.StartInfo.RedirectStandardOutput = true;
        stockfishProcess.StartInfo.CreateNoWindow = true;

        stockfishProcess.OutputDataReceived += (sender, args) => {
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
        uciInput.WriteLine("setoption name UCI_Elo value " + currentElo);
        uciInput.WriteLine("isready");
    }

    void Update()
    {
        if (!startTournament || !isEngineRunning) return;

        bool isMyTurn = (isWhiteTurn && isMyEngineWhite) || (!isWhiteTurn && !isMyEngineWhite);

        if (isMyTurn)
        {
            // === MY ENGINE'S TURN ===
            
            // Sync the global turn state so AI.cs calculates correctly
            ClickDetector.Instance.isWhiteTurn = isWhiteTurn; 
            
            // This will freeze the Unity Editor for 'myEngineTimeMs'. This is entirely normal!
            ushort bestMove = AI.Instance.GetBestMove(64);

            if (bestMove == 0) 
            {
                CheckMateOrDraw(isWhiteTurn); 
            }
            else
            {
                int fromIndex = bestMove & 0x3F;
                int toIndex = (bestMove >> 6) & 0x3F;
                pieceType movingPiece = Board.Instance.boardSquares[fromIndex];

                string uciMove = GetAlgebraicMove(fromIndex, toIndex, movingPiece);
                moveHistory.Add(uciMove);
                LogicalMove(bestMove);
            }
        }
        else
        {
            // === STOCKFISH'S TURN ===
            if (!isStockfishThinking)
            {
                string command = "position startpos moves " + string.Join(" ", moveHistory);
                uciInput.WriteLine(command);
                uciInput.WriteLine("go movetime " + stockfishTimeMs);
                isStockfishThinking = true;
            }

            while (outputQueue.TryDequeue(out string message))
            {
                if (message.StartsWith("bestmove"))
                {
                    string[] parts = message.Split(' ');
                    string moveString = parts[1];

                    isStockfishThinking = false;

                    if (moveString == "(none)")
                    {
                        CheckMateOrDraw(!isWhiteTurn); 
                    }
                    else
                    {
                        moveHistory.Add(moveString);
                        ushort sfMove = ParseUCIToUshort(moveString);
                        LogicalMove(sfMove);
                    }
                }
            }
        }
    }

    void LogicalMove(ushort move)
    {
        int fromIndex = move & 0x3F;
        int toIndex = (move >> 6) & 0x3F;
        ulong fromSquare = 1UL << fromIndex;
        ulong toSquare = 1UL << toIndex;

        // "Headless" Move: Modifies the math, completely ignores the 3D meshes
        Chess.Instance.movePiece(fromSquare, toSquare);
        isWhiteTurn = !isWhiteTurn;
    }

    void CheckMateOrDraw(bool whiteHasNoMoves)
    {
        ulong ourKing = whiteHasNoMoves ?
            Board.Instance.pieceBitboards[(int)pieceType.whiteKing] :
            Board.Instance.pieceBitboards[(int)pieceType.blackKing];

        // Temporarily set the turn flag so the engine checks the correct side
        ClickDetector.Instance.isWhiteTurn = whiteHasNoMoves;
        bool isCheck = !Chess.Instance.isSquareSafe(ourKing);
        ClickDetector.Instance.isWhiteTurn = isWhiteTurn; // Restore

        bool aiWon = false;
        bool isDraw = false;

        if (isCheck)
        {
            aiWon = (whiteHasNoMoves && !isMyEngineWhite) || (!whiteHasNoMoves && isMyEngineWhite);
            UnityEngine.Debug.Log(aiWon ? "<color=green>GAME WON!</color>" : "<color=red>GAME LOST!</color>");
        }
        else
        {
            isDraw = true;
            UnityEngine.Debug.Log("<color=yellow>GAME DRAWN!</color>");
        }

        if (isDraw) matchScore += 0.5f;
        else if (aiWon) matchScore += 1f;

        matchGameCount++;

        if (matchGameCount >= gamesPerMatch)
        {
            if (matchScore > gamesPerMatch / 2f)
            {
                currentElo += eloStep;
                UnityEngine.Debug.Log($"<color=cyan>MATCH WON! Increasing Stockfish Elo to {currentElo}</color>");
            }
            else if (matchScore < gamesPerMatch / 2f)
            {
                currentElo -= eloStep;
                UnityEngine.Debug.Log($"<color=orange>MATCH LOST! Decreasing Stockfish Elo to {currentElo}</color>");
            }
            else
            {
                UnityEngine.Debug.Log($"<color=magenta><b>--- TOURNAMENT COMPLETE ---</b>\nFINAL ESTIMATED RATING: {currentElo}</color>");
                startTournament = false;
                return;
            }

            matchGameCount = 0;
            matchScore = 0;
            ConfigureStockfish();
        }

        ResetForNextGame();
    }

    void ResetForNextGame()
    {
        moveHistory.Clear();
        isMyEngineWhite = !isMyEngineWhite; // Swap colors so AI plays the opposite side
        isWhiteTurn = true;
        ClickDetector.Instance.isWhiteTurn = true;
        Board.Instance.LoadFEN("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        Chess.Instance.currentZobristKey = Chess.Instance.GenerateHashFromScratch();
        isStockfishThinking = false;
    }

    // --- Helpers ---
    string GetAlgebraicMove(int fromIndex, int toIndex, pieceType movingPiece)
    {
        string move = IndexToAlgebraic(fromIndex) + IndexToAlgebraic(toIndex);
        if (movingPiece == pieceType.whitePawn && toIndex >= 56) move += "q";
        else if (movingPiece == pieceType.blackPawn && toIndex <= 7) move += "q";
        return move;
    }

    string IndexToAlgebraic(int index)
    {
        int file = index % 8;
        int rank = index / 8;
        return $"{(char)('a' + file)}{rank + 1}";
    }

    ushort ParseUCIToUshort(string moveString)
    {
        int fromFile = moveString[0] - 'a';
        int fromRank = moveString[1] - '1';
        int toFile = moveString[2] - 'a';
        int toRank = moveString[3] - '1';

        int fromIndex = fromRank * 8 + fromFile;
        int toIndex = toRank * 8 + toFile;

        // Simplify packing: movePiece calculates the flag data automatically
        return Chess.Instance.PackMove(fromIndex, toIndex, moveFlag.QuietMove);
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