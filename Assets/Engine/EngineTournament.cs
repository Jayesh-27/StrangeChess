using UnityEngine;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;

public class EngineTournament : MonoBehaviour
{
    public enum TournamentState {
        Idle,
        WaitingForReady,
        WaitingToStart,
        MyEngineThinking,
        StockfishThinking,
        MoveDelay,
        GameOverDelay
    }

    [Header("Tournament Settings")]
    public bool startTournament = false;
    public int startingElo = 1200;
    public int eloStep = 100;
    public int gamesPerMatch = 2; 
    
    [Header("Time Controls")]
    public int myEngineTimeMs = 1000;
    public int stockfishTimeMs = 1000; 
    public float moveDelaySeconds = 0.5f;
    public float gameOverDelaySeconds = 2.0f;

    [Header("Live Stats (Read Only)")]
    public TournamentState currentState = TournamentState.Idle;
    public int currentElo;
    public int matchGameCount = 0;
    public float matchScore = 0; 
    public bool isMyEngineWhite = true;

    private Process stockfishProcess;
    private StreamWriter uciInput;
    private ConcurrentQueue<string> outputQueue = new ConcurrentQueue<string>();

    private bool isEngineRunning = false;
    private bool isWhiteTurn = true;
    private List<string> moveHistory = new List<string>();
    private float delayTimer = 0f;

    void Start()
    {
        if (!startTournament) return;

        if (ClickDetector.Instance != null) ClickDetector.Instance.enabled = false;
        if (AI.Instance != null) AI.Instance.enabled = false;
        if (StockfishTester.Instance != null) StockfishTester.Instance.enabled = false;

        currentElo = startingElo;
        AI.Instance.timeLimitMs = myEngineTimeMs;

        StartStockfish();
        ConfigureStockfish();
        
        // Wait for Stockfish to initialize before starting Game 1
        uciInput.WriteLine("isready");
        currentState = TournamentState.WaitingForReady;
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
    }

    void Update()
    {
        if (!startTournament || !isEngineRunning) return;

        switch (currentState)
        {
            case TournamentState.Idle:
                break;

            case TournamentState.WaitingForReady:
                // FIX: Prevents desync freezes by forcing Unity to wait for Stockfish memory wipes
                while (outputQueue.TryDequeue(out string readyMsg))
                {
                    if (readyMsg == "readyok")
                    {
                        currentState = TournamentState.WaitingToStart;
                    }
                }
                break;

            case TournamentState.WaitingToStart:
                bool isMyTurn = (isWhiteTurn && isMyEngineWhite) || (!isWhiteTurn && !isMyEngineWhite);
                currentState = isMyTurn ? TournamentState.MyEngineThinking : TournamentState.StockfishThinking;
                
                if (currentState == TournamentState.StockfishThinking) {
                    string command = "position startpos moves " + string.Join(" ", moveHistory);
                    uciInput.WriteLine(command);
                    uciInput.WriteLine("go movetime " + stockfishTimeMs);
                }
                break;

            case TournamentState.MyEngineThinking:
                ClickDetector.Instance.isWhiteTurn = isWhiteTurn; 
                ushort bestMove = AI.Instance.GetBestMove(64);

                if (bestMove == 0) 
                {
                    CheckMateOrDraw(); 
                }
                else
                {
                    ApplyPackedMove(bestMove);
                    delayTimer = moveDelaySeconds;
                    currentState = TournamentState.MoveDelay;
                }
                break;

            case TournamentState.StockfishThinking:
                while (outputQueue.TryDequeue(out string message))
                {
                    if (message.StartsWith("bestmove"))
                    {
                        string[] parts = message.Split(' ');
                        string moveString = parts[1];

                        if (moveString == "(none)")
                        {
                            CheckMateOrDraw(); 
                        }
                        else
                        {
                            ApplyUciMove(moveString);
                            delayTimer = moveDelaySeconds;
                            currentState = TournamentState.MoveDelay;
                        }
                    }
                }
                break;

            case TournamentState.MoveDelay:
                delayTimer -= Time.deltaTime;
                if (delayTimer <= 0)
                {
                    currentState = TournamentState.WaitingToStart;
                }
                break;

            case TournamentState.GameOverDelay:
                delayTimer -= Time.deltaTime;
                if (delayTimer <= 0)
                {
                    ResetForNextGame();
                }
                break;
        }
    }

    void ApplyPackedMove(ushort move)
    {
        int fromIndex = move & 0x3F;
        int toIndex = (move >> 6) & 0x3F;
        ulong fromSquare = 1UL << fromIndex;
        ulong toSquare = 1UL << toIndex;
        pieceType movingPiece = Board.Instance.boardSquares[fromIndex];
        pieceType targetPiece = Board.Instance.boardSquares[toIndex];

        ApplyVisualMove(fromIndex, toIndex, movingPiece, targetPiece, move >> 12);

        string uciMove = GetAlgebraicMove(fromIndex, toIndex, movingPiece); 
        moveHistory.Add(uciMove);

        Chess.Instance.movePiece(fromSquare, toSquare);
        SyncTurns();
    }

    void ApplyUciMove(string moveString)
    {
        string startSquareStr = moveString.Substring(0, 2);
        string targetSquareStr = moveString.Substring(2, 2);

        int fromIndex = AlgebraicToIndex(startSquareStr);
        int toIndex = AlgebraicToIndex(targetSquareStr);
        ulong fromSquare = 1UL << fromIndex;
        ulong toSquare = 1UL << toIndex;
        pieceType movingPiece = Board.Instance.boardSquares[fromIndex];
        pieceType targetPiece = Board.Instance.boardSquares[toIndex];

        moveHistory.Add(moveString);
        ApplyVisualMove(fromIndex, toIndex, movingPiece, targetPiece, 0);

        Chess.Instance.movePiece(fromSquare, toSquare);
        SyncTurns();
    }

    void ApplyVisualMove(int fromIndex, int toIndex, pieceType movingPiece, pieceType targetPiece, int moveFlagValue)
    {
        ulong fromSquare = 1UL << fromIndex;
        ulong toSquare = 1UL << toIndex;

        bool isPromotion = moveFlagValue >= (int)moveFlag.PromoteToKnight ||
            (movingPiece == pieceType.whitePawn && toIndex >= 56) ||
            (movingPiece == pieceType.blackPawn && toIndex <= 7);

        bool isWhiteKingCastle = movingPiece == pieceType.whiteKing && fromIndex == 4 && (toIndex == 6 || toIndex == 2);
        bool isBlackKingCastle = movingPiece == pieceType.blackKing && fromIndex == 60 && (toIndex == 62 || toIndex == 58);

        if (isPromotion)
        {
            Board.Instance.PromoteVisualPiece(fromIndex, toIndex, movingPiece == pieceType.whitePawn);
        }
        else if (isWhiteKingCastle)
        {
            Board.Instance.Move3DModel(fromSquare, toSquare);
            if (toIndex == 6) Board.Instance.Move3DModel(1UL << 7, 1UL << 5);
            else Board.Instance.Move3DModel(1UL << 0, 1UL << 3);
        }
        else if (isBlackKingCastle)
        {
            Board.Instance.Move3DModel(fromSquare, toSquare);
            if (toIndex == 62) Board.Instance.Move3DModel(1UL << 63, 1UL << 61);
            else Board.Instance.Move3DModel(1UL << 56, 1UL << 59);
        }
        else
        {
            Board.Instance.Move3DModel(fromSquare, toSquare);
        }

        if (movingPiece == pieceType.whitePawn && targetPiece == pieceType.none && (toIndex == fromIndex + 7 || toIndex == fromIndex + 9))
            Board.Instance.DestroyVisualPiece(toIndex - 8);
        else if (movingPiece == pieceType.blackPawn && targetPiece == pieceType.none && (toIndex == fromIndex - 7 || toIndex == fromIndex - 9))
            Board.Instance.DestroyVisualPiece(toIndex + 8);
    }

    void SyncTurns()
    {
        isWhiteTurn = !isWhiteTurn;
        ClickDetector.Instance.isWhiteTurn = isWhiteTurn;
        ClickDetector.Instance.availableMoves = 0;
        ClickDetector.Instance.isSelected = false;
    }

    void CheckMateOrDraw()
    {
        // 1. Identify whose turn it is when the game ends
        bool isWhiteTurnNow = isWhiteTurn;
        ulong kingInDanger = isWhiteTurnNow ?
            Board.Instance.pieceBitboards[(int)pieceType.whiteKing] :
            Board.Instance.pieceBitboards[(int)pieceType.blackKing];

        // 2. Check if that King is under attack
        ClickDetector.Instance.isWhiteTurn = isWhiteTurnNow;
        bool isCheckmate = !Chess.Instance.isSquareSafe(kingInDanger);

        bool myEngineWon = false;
        bool isDraw = false;
        string resultStr = "*";

        if (isCheckmate)
        {
            // If it is your engine's turn and your engine is in checkmate, your engine lost.
            bool myEngineLost = (isWhiteTurnNow == isMyEngineWhite);
            myEngineWon = !myEngineLost;

            if (myEngineWon)
            {
                resultStr = isMyEngineWhite ? "1-0" : "0-1";
                UnityEngine.Debug.Log("<color=green>CHECKMATE! My Engine WON!</color>");
            }
            else
            {
                resultStr = isMyEngineWhite ? "0-1" : "1-0";
                UnityEngine.Debug.Log("<color=red>CHECKMATE! My Engine LOST!</color>");
            }
        }
        else
        {
            isDraw = true;
            resultStr = "1/2-1/2";
            UnityEngine.Debug.Log("<color=yellow>STALEMATE / DRAW!</color>");
        }

        // Print to Console
        PrintPGN(resultStr);

        // Update Match Score
        if (myEngineWon) matchScore += 1f;
        else if (isDraw) matchScore += 0.5f;

        matchGameCount++;

        // End of Match Logic
        if (matchGameCount >= gamesPerMatch)
        {
            float requiredToWin = gamesPerMatch / 2f;
            
            if (matchScore > requiredToWin)
            {
                currentElo += eloStep;
                UnityEngine.Debug.Log($"<color=cyan>MATCH WON! My Engine is stronger than {currentElo - eloStep} Elo. Upgrading Stockfish to {currentElo} for a harder test.</color>");
            }
            else if (matchScore < requiredToWin)
            {
                currentElo -= eloStep;
                UnityEngine.Debug.Log($"<color=orange>MATCH LOST! My Engine is weaker than {currentElo + eloStep} Elo. Downgrading Stockfish to {currentElo} for an easier test.</color>");
            }
            else
            {
                UnityEngine.Debug.Log($"<color=magenta><b>--- TOURNAMENT COMPLETE ---</b>\nFINAL ESTIMATED RATING: {currentElo}</color>");
                startTournament = false;
                currentState = TournamentState.Idle;
                return;
            }

            matchGameCount = 0;
            matchScore = 0;

            // Push the new Elo to Stockfish
            uciInput.WriteLine("setoption name UCI_Elo value " + currentElo);
        }

        delayTimer = gameOverDelaySeconds;
        currentState = TournamentState.GameOverDelay;
    }

    void PrintPGN(string resultStr)
    {
        string whiteName = isMyEngineWhite ? "MyEngine" : $"Stockfish ({currentElo})";
        string blackName = isMyEngineWhite ? $"Stockfish ({currentElo})" : "MyEngine";

        StringBuilder pgn = new StringBuilder();
        pgn.AppendLine("[Event \"Unity Engine Tournament\"]");
        pgn.AppendLine("[Site \"Unity Engine\"]");
        pgn.AppendLine($"[Date \"{System.DateTime.Now:yyyy.MM.dd}\"]");
        pgn.AppendLine($"[White \"{whiteName}\"]");
        pgn.AppendLine($"[Black \"{blackName}\"]");
        pgn.AppendLine($"[Result \"{resultStr}\"]");
        pgn.AppendLine();

        for (int i = 0; i < moveHistory.Count; i++)
        {
            if (i % 2 == 0)
            {
                pgn.Append($"{(i / 2) + 1}. ");
            }
            pgn.Append($"{moveHistory[i]} ");
        }
        pgn.Append(resultStr);

        UnityEngine.Debug.Log($"<color=#00FFFF><b>--- GAME PGN (Copy to Chess.com / Lichess) ---</b>\n{pgn.ToString()}</color>");
    }

    void ResetForNextGame()
    {
        moveHistory.Clear();
        isMyEngineWhite = !isMyEngineWhite; 
        isWhiteTurn = true;
        ClickDetector.Instance.isWhiteTurn = true;
        
        Board.Instance.ResetVisualBoard();

        if (AI.Instance != null) {
            System.Array.Clear(AI.Instance.transpositionTable, 0, AI.Instance.transpositionTable.Length);
            System.Array.Clear(AI.Instance.killerMoves, 0, AI.Instance.killerMoves.Length);
        }

        Board.Instance.LoadFEN("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");

        // FIX: Force a memory reset and wait for a response before starting the game
        uciInput.WriteLine("ucinewgame");
        uciInput.WriteLine("isready");

        currentState = TournamentState.WaitingForReady;
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

    int AlgebraicToIndex(string square)
    {
        int file = square[0] - 'a';
        int rank = square[1] - '1';
        return rank * 8 + file;
    }

    ushort ParseUCIToUshort(string moveString)
    {
        int fromFile = moveString[0] - 'a';
        int fromRank = moveString[1] - '1';
        int toFile = moveString[2] - 'a';
        int toRank = moveString[3] - '1';

        return Chess.Instance.PackMove(fromRank * 8 + fromFile, toRank * 8 + toFile, moveFlag.QuietMove);
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