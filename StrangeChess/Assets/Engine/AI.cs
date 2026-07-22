using UnityEngine;
using System.Diagnostics;

public struct TTEntry
{
    public ulong key;
    public int score;
    public int depth;
    public int flag; // 0 = Exact, 1 = Alpha (Upper bound / Fail Low), 2 = Beta (Lower bound / Fail High)
    public ushort bestMove;
}

public class AI : MonoBehaviour
{
    public static AI Instance;

    // Standard piece values in centipawns
    [SerializeField] const int pawnValue = 100;
    [SerializeField] const int knightValue = 300;
    [SerializeField] const int bishopValue = 320;
    [SerializeField] const int rookValue = 500;
    [SerializeField] const int queenValue = 900;
    [SerializeField] private int depth = 32;
    public long timeLimitMs = 1000;
    private Stopwatch searchTimer = new Stopwatch();
    private bool isTimeUp = false;
    private int nodesSinceTimerCheck = 0;

    // Transposition Table
    public TTEntry[] transpositionTable = new TTEntry[1048576]; 
    private int ttMask = 1048576 - 1; 

    const int TT_EXACT = 0;
    const int TT_ALPHA = 1;
    const int TT_BETA = 2;

    // Killer Moves Array [Max Depth 64, 2 Moves per ply]
    public ushort[,] killerMoves = new ushort[64, 2];

    // --- PIECE SQUARE TABLES ---
    [SerializeField] private static readonly int[] knightPST = {
        -50, -40, -30, -30, -30, -30, -40, -50,
        -40, -20,   0,   0,   0,   0, -20, -40,
        -30,   0,  10,  15,  15,  10,   0, -30,
        -30,   5,  15,  20,  20,  15,   5, -30,
        -30,   0,  15,  20,  20,  15,   0, -30,
        -30,   5,  10,  15,  15,  10,   5, -30,
        -40, -20,   0,   5,   5,   0, -20, -40,
        -50, -40, -30, -30, -30, -30, -40, -50
    };

    [SerializeField] private static readonly int[] bishopPST = {
        -20,-10,-10,-10,-10,-10,-10,-20,
        -10,  0,  0,  0,  0,  0,  0,-10,
        -10,  0,  5, 10, 10,  5,  0,-10,
        -10,  5,  5, 10, 10,  5,  5,-10,
        -10,  0, 10, 10, 10, 10,  0,-10,
        -10, 10, 10, 10, 10, 10, 10,-10,
        -10,  5,  0,  0,  0,  0,  5,-10,
        -20,-10,-10,-10,-10,-10,-10,-20
    };

    [SerializeField] private static readonly int[] rookPST = {
         0,  0,  0,  0,  0,  0,  0,  0,
         5, 10, 10, 10, 10, 10, 10,  5,
        -5,  0,  0,  0,  0,  0,  0, -5,
        -5,  0,  0,  0,  0,  0,  0, -5,
        -5,  0,  0,  0,  0,  0,  0, -5,
        -5,  0,  0,  0,  0,  0,  0, -5,
        -5,  0,  0,  0,  0,  0,  0, -5,
         0,  0,  0,  5,  5,  0,  0,  0
    };

    [SerializeField] private static readonly int[] queenPST = {
        -20,-10,-10, -5, -5,-10,-10,-20,
        -10,  0,  0,  0,  0,  0,  0,-10,
        -10,  0,  5,  5,  5,  5,  0,-10,
         -5,  0,  5,  5,  5,  5,  0, -5,
          0,  0,  5,  5,  5,  5,  0, -5,
        -10,  5,  5,  5,  5,  5,  0,-10,
        -10,  0,  5,  0,  0,  0,  0,-10,
        -20,-10,-10, -5, -5,-10,-10,-20
    };

    // Middlegame Pawn PST
    [SerializeField] private static readonly int[] pawnPST_MG = {
         0,  0,  0,  0,  0,  0,  0,  0,
        50, 50, 50, 50, 50, 50, 50, 50,
        10, 10, 20, 30, 30, 20, 10, 10,
         5,  5, 10, 25, 25, 10,  5,  5,
         0,  0,  0, 20, 20,  0,  0,  0,
         5, -5,-10,  0,  0,-10, -5,  5,
         5, 10, 10,-20,-20, 10, 10,  5,
         0,  0,  0,  0,  0,  0,  0,  0
    };

    // Endgame Pawn PST (Rewards advancing pawns)
    [SerializeField] private static readonly int[] pawnPST_EG = {
         0,  0,  0,  0,  0,  0,  0,  0,
       160,160,160,160,160,160,160,160,
       100,100,100,100,100,100,100,100,
        60, 60, 60, 60, 60, 60, 60, 60,
        30, 30, 30, 30, 30, 30, 30, 30,
        10, 10, 10, 10, 10, 10, 10, 10,
         0,  0,  0,  0,  0,  0,  0,  0,
         0,  0,  0,  0,  0,  0,  0,  0
    };

    // Middlegame King PST (Encourages castling & hiding)
    [SerializeField] private static readonly int[] kingPST_MG = {
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -20,-30,-30,-40,-40,-30,-30,-20,
        -10,-20,-20,-20,-20,-20,-20,-10,
         20, 20,  0,  0,  0,  0, 20, 20,
         20, 30, 10,  0,  0, 10, 30, 20
    };

    // Endgame King PST (Encourages centralizing the king)
    [SerializeField] private static readonly int[] kingPST_EG = {
        -50,-40,-30,-20,-20,-30,-40,-50,
        -30,-20,-10,  0,  0,-10,-20,-30,
        -30,-10, 20, 30, 30, 20,-10,-30,
        -30,-10, 30, 40, 40, 30,-10,-30,
        -30,-10, 30, 40, 40, 30,-10,-30,
        -30,-10, 20, 30, 30, 20,-10,-30,
        -30,-30,  0,  0,  0,  0,-30,-30,
        -50,-30,-30,-30,-30,-30,-30,-50
    };

    const int positiveInfinity = 9999999;
    const int negativeInfinity = -9999999;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Update()
    {        
        if (Input.GetKeyDown(KeyCode.Space))
        {
            PlayBestMove(depth);
        }

        if(!ClickDetector.Instance.isWhiteTurn)
        {
            PlayBestMove(depth);
        }
    }

    public void PlayBestMove(int depth)
    {
        UnityEngine.Debug.Log("AI is thinking...");
        ushort bestMove = GetBestMove(depth);
        
        if (bestMove == 0) 
        {
            UnityEngine.Debug.Log("Game Over! No legal moves available.");
            return;
        }

        int fromIndex = bestMove & 0x3F;
        int toIndex = (bestMove >> 6) & 0x3F;
        ulong fromSquare = 1UL << fromIndex;
        ulong toSquare = 1UL << toIndex;

        pieceType movingPiece = Board.Instance.boardSquares[fromIndex];
        pieceType targetPiece = Board.Instance.boardSquares[toIndex];
        
        if (movingPiece == pieceType.whitePawn && toIndex >= 56) Board.Instance.PromoteVisualPiece(fromIndex, toIndex, true);
        else if (movingPiece == pieceType.blackPawn && toIndex <= 7) Board.Instance.PromoteVisualPiece(fromIndex, toIndex, false);
        else if (movingPiece == pieceType.whiteKing && fromIndex == 4 && toIndex == 6) { Board.Instance.Move3DModel(fromSquare, toSquare); Board.Instance.Move3DModel(1UL << 7, 1UL << 5); } 
        else if (movingPiece == pieceType.whiteKing && fromIndex == 4 && toIndex == 2) { Board.Instance.Move3DModel(fromSquare, toSquare); Board.Instance.Move3DModel(1UL << 0, 1UL << 3); } 
        else if (movingPiece == pieceType.blackKing && fromIndex == 60 && toIndex == 62) { Board.Instance.Move3DModel(fromSquare, toSquare); Board.Instance.Move3DModel(1UL << 63, 1UL << 61); } 
        else if (movingPiece == pieceType.blackKing && fromIndex == 60 && toIndex == 58) { Board.Instance.Move3DModel(fromSquare, toSquare); Board.Instance.Move3DModel(1UL << 56, 1UL << 59); } 
        else Board.Instance.Move3DModel(fromSquare, toSquare);

        if (movingPiece == pieceType.whitePawn && targetPiece == pieceType.none && (toIndex == fromIndex + 7 || toIndex == fromIndex + 9)) Board.Instance.DestroyVisualPiece(toIndex - 8);
        else if (movingPiece == pieceType.blackPawn && targetPiece == pieceType.none && (toIndex == fromIndex - 7 || toIndex == fromIndex - 9)) Board.Instance.DestroyVisualPiece(toIndex + 8);
        
        if (StockfishTester.Instance != null) StockfishTester.Instance.ReportUserMove(fromSquare, toSquare, movingPiece);
        
        Chess.Instance.movePiece(fromSquare, toSquare);
        
        ClickDetector.Instance.isWhiteTurn = !ClickDetector.Instance.isWhiteTurn;
        ClickDetector.Instance.availableMoves = 0;
        ClickDetector.Instance.isSelected = false;
        
        UnityEngine.Debug.Log($"AI Played Move: {fromIndex} -> {toIndex}");
    }

    public ushort GetBestMove(int maxDepth)
    {
        searchTimer.Restart();
        isTimeUp = false;
        nodesSinceTimerCheck = 0;
        
        killerMoves = new ushort[64, 2];
        
        Chess.Instance.GenerateAllMoves(0);
        int currentMoveCount = Chess.Instance.moveCount[0];            

        ulong rootKey = Chess.Instance.currentZobristKey;
        int rootIndex = (int)(rootKey & (ulong)ttMask);
        ushort ttMove = transpositionTable[rootIndex].key == rootKey ? transpositionTable[rootIndex].bestMove : (ushort)0;

        OrderMoves(0, currentMoveCount, ttMove); 
        
        ushort absoluteBestMove = 0;
        bool isWhite = ClickDetector.Instance.isWhiteTurn;
        int previousScore = 0;
        
        int actualMaxDepth = Mathf.Min(maxDepth, 63);
        for (int currentDepth = 1; currentDepth <= actualMaxDepth; currentDepth++)
        {
            // --- ASPIRATION WINDOWS ---
            int alpha = negativeInfinity;
            int beta = positiveInfinity;
            if (currentDepth >= 3)
            {
                alpha = previousScore - 50;
                beta = previousScore + 50;
            }

            int bestScoreThisDepth = negativeInfinity;
            ushort bestMoveThisDepth = 0;
            
            // --- NEW: Track legal moves to prevent infinite loops ---
            int legalMovesPlayedAtRoot = 0; 

            while (true) // Aspiration Loop
            {
                int currentAlpha = alpha;
                int currentBeta = beta;
                int bestScoreThisIteration = negativeInfinity;
                ushort bestMoveThisIteration = 0;
                legalMovesPlayedAtRoot = 0; // Reset for each aspiration attempt

                for (int i = 0; i < currentMoveCount; i++)
                {
                    ushort move = Chess.Instance.moveList[0][i];
                    int fromIndex = move & 0x3F;
                    int toIndex = (move >> 6) & 0x3F;
                    ulong fromSquare = 1UL << fromIndex;
                    ulong toSquare = 1UL << toIndex;

                    int savedCastling = Chess.Instance.castlingRights;
                    int savedEP = Chess.Instance.enPassantTarget;
                    ulong savedHash = Chess.Instance.currentZobristKey;
                    pieceType originalPiece = Board.Instance.boardSquares[fromIndex];
                    pieceType capturedPiece = Board.Instance.boardSquares[toIndex]; 

                    Chess.Instance.movePiece(fromSquare, toSquare);

                    ulong currentKing = isWhite ? Board.Instance.pieceBitboards[(int)pieceType.whiteKing] : Board.Instance.pieceBitboards[(int)pieceType.blackKing];

                    if (Chess.Instance.isSquareSafe(currentKing))
                    {
                        legalMovesPlayedAtRoot++; // --- We found a legal move! ---
                        
                        ClickDetector.Instance.isWhiteTurn = !isWhite;
                        int score = -Search(currentDepth - 1, 1, -currentBeta, -currentAlpha, true);
                        ClickDetector.Instance.isWhiteTurn = isWhite;

                        if (isTimeUp) 
                        {
                            Chess.Instance.unmakeMove(fromSquare, toSquare, capturedPiece, originalPiece);
                            Chess.Instance.castlingRights = savedCastling;
                            Chess.Instance.enPassantTarget = savedEP;
                            Chess.Instance.currentZobristKey = savedHash;
                            break;
                        }

                        if (score > bestScoreThisIteration)
                        {
                            bestScoreThisIteration = score;
                            bestMoveThisIteration = move;
                        }
                        if (bestScoreThisIteration > currentAlpha) currentAlpha = bestScoreThisIteration;
                        if (currentAlpha >= currentBeta) 
                        {
                            Chess.Instance.unmakeMove(fromSquare, toSquare, capturedPiece, originalPiece);
                            Chess.Instance.castlingRights = savedCastling;
                            Chess.Instance.enPassantTarget = savedEP;
                            Chess.Instance.currentZobristKey = savedHash;
                            break; 
                        }
                    }

                    Chess.Instance.unmakeMove(fromSquare, toSquare, capturedPiece, originalPiece);
                    Chess.Instance.castlingRights = savedCastling;
                    Chess.Instance.enPassantTarget = savedEP;
                    Chess.Instance.currentZobristKey = savedHash;
                }

                if (isTimeUp) break;
                
                // --- NEW: If there are absolutely no legal moves, break out immediately to avoid freezing ---
                if (legalMovesPlayedAtRoot == 0) break; 

                // Handle Aspiration Window Failures
                if (bestScoreThisIteration <= alpha) {
                    alpha = negativeInfinity; // Fail Low: widen bound and search again
                    continue;
                }
                if (bestScoreThisIteration >= beta) {
                    beta = positiveInfinity; // Fail High: widen bound and search again
                    continue;
                }

                bestScoreThisDepth = bestScoreThisIteration;
                bestMoveThisDepth = bestMoveThisIteration;
                break; // Score was safely within the window
            }

            // --- NEW: Break out of Iterative Deepening loop if checkmated ---
            if (isTimeUp || legalMovesPlayedAtRoot == 0) break; 

            absoluteBestMove = bestMoveThisDepth;
            previousScore = bestScoreThisDepth;
            OrderMoves(0, currentMoveCount, absoluteBestMove);
        }

        searchTimer.Stop();
        return absoluteBestMove; // Will correctly return 0 if no legal moves exist
    }
    
    private int Search(int depth, int ply, int alpha, int beta, bool allowNull)
    {
        CheckTime();
        if (isTimeUp) return 0;

        // --- REPETITION DETECTION ---
        if (ply > 0 && IsRepetition()) return 0; 

        if (ply >= 63) 
        {
            int eval = EvaluateBoard();
            return ClickDetector.Instance.isWhiteTurn ? eval : -eval;
        }

        if (depth == 0) return QuiescenceSearch(ply, alpha, beta);

        int originalAlpha = alpha;
        ulong key = Chess.Instance.currentZobristKey;
        int ttIndex = (int)(key & (ulong)ttMask);
        TTEntry ttEntry = transpositionTable[ttIndex];
        ushort ttMove = 0;

        if (ttEntry.key == key)
        {
            ttMove = ttEntry.bestMove;
            if (ttEntry.depth >= depth)
            {
                int ttScore = ttEntry.score;
                if (ttScore > 9000000) ttScore -= ply;
                else if (ttScore < -9000000) ttScore += ply;

                if (ttEntry.flag == TT_EXACT) return ttScore;
                if (ttEntry.flag == TT_ALPHA && ttScore <= alpha) return ttScore;
                if (ttEntry.flag == TT_BETA && ttScore >= beta) return ttScore;
            }
        }

        bool isWhite = ClickDetector.Instance.isWhiteTurn;
        ulong ourKing = isWhite ? Board.Instance.pieceBitboards[(int)pieceType.whiteKing] : Board.Instance.pieceBitboards[(int)pieceType.blackKing];
        bool inCheck = !Chess.Instance.isSquareSafe(ourKing);

        // --- NULL MOVE PRUNING ---
        if (depth >= 3 && allowNull && !inCheck)
        {
            bool hasNonPawnMaterial = isWhite ? 
                (Board.Instance.pieceBitboards[(int)pieceType.whiteKnight] | Board.Instance.pieceBitboards[(int)pieceType.whiteBishop] | Board.Instance.pieceBitboards[(int)pieceType.whiteRook] | Board.Instance.pieceBitboards[(int)pieceType.whiteQueen]) != 0 : 
                (Board.Instance.pieceBitboards[(int)pieceType.blackKnight] | Board.Instance.pieceBitboards[(int)pieceType.blackBishop] | Board.Instance.pieceBitboards[(int)pieceType.blackRook] | Board.Instance.pieceBitboards[(int)pieceType.blackQueen]) != 0;

            if (hasNonPawnMaterial)
            {
                int savedEP = Chess.Instance.enPassantTarget;
                ulong savedHash = Chess.Instance.currentZobristKey;

                ClickDetector.Instance.isWhiteTurn = !isWhite;
                Chess.Instance.enPassantTarget = -1; 
                Chess.Instance.currentZobristKey ^= Zobrist.sideToMove;
                if (savedEP != -1) Chess.Instance.currentZobristKey ^= Zobrist.enPassantArray[savedEP];
                Chess.Instance.currentZobristKey ^= Zobrist.enPassantArray[64];

                int nullScore = -Search(depth - 1 - 2, ply + 1, -beta, -beta + 1, false);

                ClickDetector.Instance.isWhiteTurn = isWhite;
                Chess.Instance.enPassantTarget = savedEP;
                Chess.Instance.currentZobristKey = savedHash;

                if (isTimeUp) return 0;
                if (nullScore >= beta) return beta;
            }
        }

        Chess.Instance.GenerateAllMoves(ply);
        int currentMoveCount = Chess.Instance.moveCount[ply];
        OrderMoves(ply, currentMoveCount, ttMove);
        
        int legalMovesPlayed = 0;
        int bestScore = negativeInfinity;
        ushort bestMoveInThisPosition = 0;

        for (int i = 0; i < currentMoveCount; i++)
        {
            ushort move = Chess.Instance.moveList[ply][i];
            int fromIndex = move & 0x3F;
            int toIndex = (move >> 6) & 0x3F;
            int flag = move >> 12;

            bool isCapture = (flag == 4 || flag == 5 || flag >= 12);
            bool isPromotion = flag >= 8;

            ulong fromSquare = 1UL << fromIndex;
            ulong toSquare = 1UL << toIndex;

            int savedCastling = Chess.Instance.castlingRights;
            int savedEP = Chess.Instance.enPassantTarget;
            ulong savedHash = Chess.Instance.currentZobristKey;
            pieceType originalPiece = Board.Instance.boardSquares[fromIndex];
            pieceType capturedPiece = Board.Instance.boardSquares[toIndex]; 

            Chess.Instance.movePiece(fromSquare, toSquare);
            ulong currentKing = isWhite ? Board.Instance.pieceBitboards[(int)pieceType.whiteKing] : Board.Instance.pieceBitboards[(int)pieceType.blackKing];

            if (Chess.Instance.isSquareSafe(currentKing))
            {
                legalMovesPlayed++;
                ClickDetector.Instance.isWhiteTurn = !isWhite;
                
                int score = 0; // <-- FIX: Properly initialized to prevent CS0165 error
                
                // --- LATE MOVE REDUCTIONS (LMR) ---
                bool needsFullSearch = true;
                if (legalMovesPlayed >= 4 && depth >= 3 && !isCapture && !isPromotion && !inCheck && move != ttMove)
                {
                    int reduction = (legalMovesPlayed > 6) ? 2 : 1;
                    score = -Search(depth - 1 - reduction, ply + 1, -beta, -alpha, true);
                    needsFullSearch = score > alpha; // If it beats alpha, our reduction was a mistake; research fully
                }

                if (needsFullSearch)
                {
                    score = -Search(depth - 1, ply + 1, -beta, -alpha, true);
                }
                
                ClickDetector.Instance.isWhiteTurn = isWhite;

                if (isTimeUp)
                {
                    Chess.Instance.unmakeMove(fromSquare, toSquare, capturedPiece, originalPiece);
                    Chess.Instance.castlingRights = savedCastling;
                    Chess.Instance.enPassantTarget = savedEP;
                    Chess.Instance.currentZobristKey = savedHash;
                    return 0;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMoveInThisPosition = move;
                }
                if (bestScore > alpha) alpha = bestScore;
                
                if (alpha >= beta)
                {
                    if (!isCapture && !isPromotion)
                    {
                        if (move != killerMoves[ply, 0])
                        {
                            killerMoves[ply, 1] = killerMoves[ply, 0];
                            killerMoves[ply, 0] = move;
                        }
                    }

                    Chess.Instance.unmakeMove(fromSquare, toSquare, capturedPiece, originalPiece);
                    Chess.Instance.castlingRights = savedCastling;
                    Chess.Instance.enPassantTarget = savedEP;
                    Chess.Instance.currentZobristKey = savedHash;
                    break; 
                }
            }

            Chess.Instance.unmakeMove(fromSquare, toSquare, capturedPiece, originalPiece);
            Chess.Instance.castlingRights = savedCastling;
            Chess.Instance.enPassantTarget = savedEP;
            Chess.Instance.currentZobristKey = savedHash;
        }

        if (legalMovesPlayed == 0) return inCheck ? negativeInfinity + ply : 0;

        if (!isTimeUp)
        {
            int ttFlag = TT_EXACT;
            if (bestScore <= originalAlpha) ttFlag = TT_ALPHA;
            else if (bestScore >= beta) ttFlag = TT_BETA;

            int storedScore = bestScore;
            if (storedScore > 9000000) storedScore += ply;
            else if (storedScore < -9000000) storedScore -= ply;

            if (ttEntry.key == 0 || ttEntry.key == key || depth >= ttEntry.depth)
            {
                transpositionTable[ttIndex].key = key;
                transpositionTable[ttIndex].score = storedScore;
                transpositionTable[ttIndex].depth = depth;
                transpositionTable[ttIndex].flag = ttFlag;
                transpositionTable[ttIndex].bestMove = bestMoveInThisPosition;
            }
        }
        
        return bestScore;
    }

    private int QuiescenceSearch(int ply, int alpha, int beta)
    {
        CheckTime();
        if (isTimeUp) return 0;

        if (ply >= 63) 
        {
            int eval = EvaluateBoard();
            return ClickDetector.Instance.isWhiteTurn ? eval : -eval;
        }

        int standPat = EvaluateBoard();
        standPat = ClickDetector.Instance.isWhiteTurn ? standPat : -standPat;

        if (standPat >= beta) return beta;
        if (alpha < standPat) alpha = standPat;

        Chess.Instance.GenerateAllMoves(ply);
        int currentMoveCount = Chess.Instance.moveCount[ply];    
        OrderMoves(ply, currentMoveCount, 0);
        bool isWhite = ClickDetector.Instance.isWhiteTurn;

        for (int i = 0; i < currentMoveCount; i++)
        {
            ushort move = Chess.Instance.moveList[ply][i];
            int flag = move >> 12;

            bool isCaptureOrPromotion = (flag == 4 || flag == 5 || flag >= 11);
            if (!isCaptureOrPromotion) continue; 

            int fromIndex = move & 0x3F;
            int toIndex = (move >> 6) & 0x3F;
            ulong fromSquare = 1UL << fromIndex;
            ulong toSquare = 1UL << toIndex;

            int savedCastling = Chess.Instance.castlingRights;
            int savedEP = Chess.Instance.enPassantTarget;
            ulong savedHash = Chess.Instance.currentZobristKey;
            pieceType originalPiece = Board.Instance.boardSquares[fromIndex];
            pieceType capturedPiece = Board.Instance.boardSquares[toIndex]; 

            Chess.Instance.movePiece(fromSquare, toSquare);
            ulong currentKing = isWhite ? Board.Instance.pieceBitboards[(int)pieceType.whiteKing] : Board.Instance.pieceBitboards[(int)pieceType.blackKing];

            if (Chess.Instance.isSquareSafe(currentKing))
            {
                ClickDetector.Instance.isWhiteTurn = !isWhite;
                int score = -QuiescenceSearch(ply + 1, -beta, -alpha);
                ClickDetector.Instance.isWhiteTurn = isWhite;

                if (score >= beta)
                {
                    Chess.Instance.unmakeMove(fromSquare, toSquare, capturedPiece, originalPiece);
                    Chess.Instance.castlingRights = savedCastling;
                    Chess.Instance.enPassantTarget = savedEP;
                    Chess.Instance.currentZobristKey = savedHash;
                    return beta; 
                }
                if (score > alpha) alpha = score;
            }

            Chess.Instance.unmakeMove(fromSquare, toSquare, capturedPiece, originalPiece);
            Chess.Instance.castlingRights = savedCastling;
            Chess.Instance.enPassantTarget = savedEP;
            Chess.Instance.currentZobristKey = savedHash;
        }

        return alpha;
    }

    private void OrderMoves(int ply, int currentMoveCount, ushort ttMove = 0)
    {
        for (int i = 0; i < currentMoveCount; i++)
        {
            Chess.Instance.moveScores[ply][i] = ScoreMove(Chess.Instance.moveList[ply][i], ttMove, ply);
        }

        for (int i = 0; i < currentMoveCount - 1; i++)
        {
            int maxIndex = i;
            for (int j = i + 1; j < currentMoveCount; j++)
            {
                if (Chess.Instance.moveScores[ply][j] > Chess.Instance.moveScores[ply][maxIndex])
                {
                    maxIndex = j;
                }
            }

            int tempScore = Chess.Instance.moveScores[ply][i];
            Chess.Instance.moveScores[ply][i] = Chess.Instance.moveScores[ply][maxIndex];
            Chess.Instance.moveScores[ply][maxIndex] = tempScore;

            ushort tempMove = Chess.Instance.moveList[ply][i];
            Chess.Instance.moveList[ply][i] = Chess.Instance.moveList[ply][maxIndex];
            Chess.Instance.moveList[ply][maxIndex] = tempMove;
        }
    }

    private int ScoreMove(ushort move, ushort ttMove, int ply)
    {
        if (ttMove != 0 && move == ttMove) return 2000000; 

        int score = 0;
        int fromIndex = move & 0x3F;
        int toIndex = (move >> 6) & 0x3F;
        int flag = move >> 12;

        bool isCapture = (flag == 4 || flag == 5 || flag >= 12);

        if (isCapture)
        {
            pieceType attacker = Board.Instance.boardSquares[fromIndex];
            pieceType victim = Board.Instance.boardSquares[toIndex];
            if (flag == 5) victim = (attacker == pieceType.whitePawn) ? pieceType.blackPawn : pieceType.whitePawn;

            score = 1000000 + (10 * GetPieceValue(victim) - GetPieceValue(attacker));
        }
        else 
        {
            if (move == killerMoves[ply, 0]) score += 900000; 
            else if (move == killerMoves[ply, 1]) score += 800000; 
        }

        if (flag == 11 || flag == 15) score += 90000; 

        return score;
    }

    private int GetPieceValue(pieceType piece)
    {
        switch (piece)
        {
            case pieceType.whitePawn: case pieceType.blackPawn: return pawnValue;
            case pieceType.whiteKnight: case pieceType.blackKnight: return knightValue;
            case pieceType.whiteBishop: case pieceType.blackBishop: return bishopValue;
            case pieceType.whiteRook: case pieceType.blackRook: return rookValue;
            case pieceType.whiteQueen: case pieceType.blackQueen: return queenValue;
            default: return 0;
        }
    }

    // =========================================================================
    // --- ADVANCED EVALUATION SYSTEM ---
    // =========================================================================

    public int EvaluateBoard()
    {
        int material = MaterialValueEvaluation();
        
        int mgScore = material + PSTEvaluationMG();
        int egScore = material + PSTEvaluationEG();

        int positionalTerms = EvaluatePositionalTerms();
        
        // --- NEW: Calculate Development and Castling ---
        int developmentScore = EvaluateDevelopment();
        int castlingScore = EvaluateCastling();
        
        // Apply development and castling ONLY to the Middlegame phase!
        mgScore += positionalTerms + developmentScore + castlingScore;
        egScore += positionalTerms; 

        int phase = CalculatePhase();
        int finalEval = ((mgScore * phase) + (egScore * (24 - phase))) / 24;

        if (Mathf.Abs(material) >= 300 && phase <= 6)
        {
            finalEval += EvaluateMopUp(material > 0);
        }

        return finalEval;
    }

    public int MaterialValueEvaluation()
    {
        int tempScore = 0;

        tempScore += BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.whitePawn]) * pawnValue;
        tempScore += BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.whiteKnight]) * knightValue;
        tempScore += BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.whiteBishop]) * bishopValue;
        tempScore += BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.whiteRook]) * rookValue;
        tempScore += BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.whiteQueen]) * queenValue;

        tempScore -= BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.blackPawn]) * pawnValue;
        tempScore -= BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.blackKnight]) * knightValue;
        tempScore -= BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.blackBishop]) * bishopValue;
        tempScore -= BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.blackRook]) * rookValue;
        tempScore -= BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.blackQueen]) * queenValue;

        return tempScore;
    }

    private int CalculatePhase()
    {
        int phase = 0;
        phase += BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.whiteKnight]) * 1;
        phase += BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.blackKnight]) * 1;
        phase += BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.whiteBishop]) * 1;
        phase += BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.blackBishop]) * 1;
        phase += BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.whiteRook])   * 2;
        phase += BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.blackRook])   * 2;
        phase += BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.whiteQueen])  * 4;
        phase += BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.blackQueen])  * 4;

        return Mathf.Min(phase, 24);
    }

    private int PSTEvaluationMG()
    {
        int tempScore = 0;
        for (int i = 0; i < 64; i++)
        {
            pieceType piece = Board.Instance.boardSquares[i];
            if (piece == pieceType.none) continue;

            bool isWhite = piece >= pieceType.whitePawn && piece <= pieceType.whiteKing;
            int pstIndex = isWhite ? i : i ^ 56; 
            int pstValue = 0;

            switch (piece)
            {
                case pieceType.whitePawn: case pieceType.blackPawn: pstValue = pawnPST_MG[pstIndex]; break;
                case pieceType.whiteKnight: case pieceType.blackKnight: pstValue = knightPST[pstIndex]; break;
                case pieceType.whiteBishop: case pieceType.blackBishop: pstValue = bishopPST[pstIndex]; break;
                case pieceType.whiteRook: case pieceType.blackRook: pstValue = rookPST[pstIndex]; break;
                case pieceType.whiteQueen: case pieceType.blackQueen: pstValue = queenPST[pstIndex]; break;
                case pieceType.whiteKing: case pieceType.blackKing: pstValue = kingPST_MG[pstIndex]; break;
            }

            tempScore += isWhite ? pstValue : -pstValue;
        }
        return tempScore;
    }

    private int PSTEvaluationEG()
    {
        int tempScore = 0;
        for (int i = 0; i < 64; i++)
        {
            pieceType piece = Board.Instance.boardSquares[i];
            if (piece == pieceType.none) continue;

            bool isWhite = piece >= pieceType.whitePawn && piece <= pieceType.whiteKing;
            int pstIndex = isWhite ? i : i ^ 56; 
            int pstValue = 0;

            switch (piece)
            {
                case pieceType.whitePawn: case pieceType.blackPawn: pstValue = pawnPST_EG[pstIndex]; break;
                case pieceType.whiteKnight: case pieceType.blackKnight: pstValue = knightPST[pstIndex]; break;
                case pieceType.whiteBishop: case pieceType.blackBishop: pstValue = bishopPST[pstIndex]; break;
                case pieceType.whiteRook: case pieceType.blackRook: pstValue = rookPST[pstIndex]; break;
                case pieceType.whiteQueen: case pieceType.blackQueen: pstValue = queenPST[pstIndex]; break;
                case pieceType.whiteKing: case pieceType.blackKing: pstValue = kingPST_EG[pstIndex]; break;
            }

            tempScore += isWhite ? pstValue : -pstValue;
        }
        return tempScore;
    }

    private int EvaluatePositionalTerms()
    {
        int score = 0;
        ulong wPawns = Board.Instance.pieceBitboards[(int)pieceType.whitePawn];
        ulong bPawns = Board.Instance.pieceBitboards[(int)pieceType.blackPawn];
        ulong allPawns = wPawns | bPawns;

        // 1. Bishop Pair
        if (BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.whiteBishop]) >= 2) score += 35;
        if (BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.blackBishop]) >= 2) score -= 35;

        // 2. Rooks on Open Files
        ulong tempWRooks = Board.Instance.pieceBitboards[(int)pieceType.whiteRook];
        while (tempWRooks != 0)
        {
            ulong rookBit = tempWRooks & (~tempWRooks + 1);
            int idx = Board.Instance.GetBitboardIndex(rookBit);
            int file = idx % 8;
            ulong fileMask = 0x0101010101010101UL << file;

            if ((allPawns & fileMask) == 0) score += 25; 
            else if ((wPawns & fileMask) == 0) score += 12; 

            if (idx / 8 == 6) score += 30; // 7th rank

            tempWRooks &= tempWRooks - 1;
        }

        ulong tempBRooks = Board.Instance.pieceBitboards[(int)pieceType.blackRook];
        while (tempBRooks != 0)
        {
            ulong rookBit = tempBRooks & (~tempBRooks + 1);
            int idx = Board.Instance.GetBitboardIndex(rookBit);
            int file = idx % 8;
            ulong fileMask = 0x0101010101010101UL << file;

            if ((allPawns & fileMask) == 0) score -= 25; 
            else if ((bPawns & fileMask) == 0) score -= 12; 

            if (idx / 8 == 1) score -= 30; // 2nd rank

            tempBRooks &= tempBRooks - 1;
        }

        // 3. Pawn Structure
        score += EvaluatePawnStructure(wPawns, bPawns);
        
        // 4. King Safety (Pawn Shield)
        score += EvaluateKingSafety(wPawns, bPawns);

        return score;
    }

    private int EvaluateKingSafety(ulong wPawns, ulong bPawns)
    {
        int score = 0;
        ulong wKing = Board.Instance.pieceBitboards[(int)pieceType.whiteKing];
        ulong bKing = Board.Instance.pieceBitboards[(int)pieceType.blackKing];

        // White Kingside Shield (f2, g2, h2 = indices 13, 14, 15)
        if ((wKing & (1UL << 6)) != 0) 
        {
            if ((wPawns & (1UL << 13)) == 0) score -= 15;
            if ((wPawns & (1UL << 14)) == 0) score -= 20; // g2 is critical
            if ((wPawns & (1UL << 15)) == 0) score -= 10;
        } 
        // White Queenside Shield (a2, b2, c2 = indices 8, 9, 10)
        else if ((wKing & (1UL << 2)) != 0) 
        {
            if ((wPawns & (1UL << 8)) == 0) score -= 10;
            if ((wPawns & (1UL << 9)) == 0) score -= 15;
            if ((wPawns & (1UL << 10)) == 0) score -= 15;
        }

        // Black Kingside Shield (f7, g7, h7 = indices 53, 54, 55)
        if ((bKing & (1UL << 62)) != 0) 
        {
            if ((bPawns & (1UL << 53)) == 0) score += 15;
            if ((bPawns & (1UL << 54)) == 0) score += 20;
            if ((bPawns & (1UL << 55)) == 0) score += 10;
        } 
        // Black Queenside Shield (a7, b7, c7 = indices 48, 49, 50)
        else if ((bKing & (1UL << 58)) != 0) 
        {
            if ((bPawns & (1UL << 48)) == 0) score += 10;
            if ((bPawns & (1UL << 49)) == 0) score += 15;
            if ((bPawns & (1UL << 50)) == 0) score += 15;
        }

        return score;
    }

    private int EvaluatePawnStructure(ulong wPawns, ulong bPawns)
    {
        int score = 0;
        int[] passedPawnBonus = { 0, 0, 10, 20, 40, 70, 130, 0 };

        ulong tempWPawns = wPawns;
        while (tempWPawns != 0)
        {
            ulong pawnBit = tempWPawns & (~tempWPawns + 1);
            int idx = Board.Instance.GetBitboardIndex(pawnBit);
            int file = idx % 8;
            int rank = idx / 8;

            ulong fileMask = 0x0101010101010101UL << file;
            if (BitboardUtility.PopCount(wPawns & fileMask) > 1) score -= 15; 

            ulong adjFilesMask = 0UL;
            if (file > 0) adjFilesMask |= (0x0101010101010101UL << (file - 1));
            if (file < 7) adjFilesMask |= (0x0101010101010101UL << (file + 1));
            if ((wPawns & adjFilesMask) == 0) score -= 15; 

            ulong passedFrontMask = (fileMask | adjFilesMask);
            ulong higherRanksMask = ~((1UL << ((rank + 1) * 8)) - 1);
            if ((bPawns & passedFrontMask & higherRanksMask) == 0)
            {
                score += passedPawnBonus[rank];
            }
            tempWPawns &= tempWPawns - 1;
        }

        ulong tempBPawns = bPawns;
        while (tempBPawns != 0)
        {
            ulong pawnBit = tempBPawns & (~tempBPawns + 1);
            int idx = Board.Instance.GetBitboardIndex(pawnBit);
            int file = idx % 8;
            int rank = idx / 8;

            ulong fileMask = 0x0101010101010101UL << file;
            if (BitboardUtility.PopCount(bPawns & fileMask) > 1) score += 15; 

            ulong adjFilesMask = 0UL;
            if (file > 0) adjFilesMask |= (0x0101010101010101UL << (file - 1));
            if (file < 7) adjFilesMask |= (0x0101010101010101UL << (file + 1));
            if ((bPawns & adjFilesMask) == 0) score += 15; 

            ulong passedFrontMask = (fileMask | adjFilesMask);
            ulong lowerRanksMask = (1UL << (rank * 8)) - 1;
            if ((wPawns & passedFrontMask & lowerRanksMask) == 0)
            {
                score -= passedPawnBonus[7 - rank];
            }
            tempBPawns &= tempBPawns - 1;
        }

        return score;
    }

    private int EvaluateMopUp(bool isWhiteWinning)
    {
        int mopUpScore = 0;

        ulong whiteKingBb = Board.Instance.pieceBitboards[(int)pieceType.whiteKing];
        ulong blackKingBb = Board.Instance.pieceBitboards[(int)pieceType.blackKing];
        if (whiteKingBb == 0 || blackKingBb == 0) return 0;

        int winningKingIdx = Board.Instance.GetBitboardIndex(isWhiteWinning ? whiteKingBb : blackKingBb);
        int losingKingIdx  = Board.Instance.GetBitboardIndex(isWhiteWinning ? blackKingBb : whiteKingBb);

        if (winningKingIdx < 0 || losingKingIdx < 0) return 0;

        int losingRank = losingKingIdx / 8;
        int losingFile = losingKingIdx % 8;

        int centerDist = Mathf.Max(3 - losingFile, losingFile - 4) + Mathf.Max(3 - losingRank, losingRank - 4);
        mopUpScore += centerDist * 10;

        int winningRank = winningKingIdx / 8;
        int winningFile = winningKingIdx % 8;
        int kingDist = Mathf.Abs(winningRank - losingRank) + Mathf.Abs(winningFile - losingFile);
        mopUpScore += (14 - kingDist) * 4;

        return isWhiteWinning ? mopUpScore : -mopUpScore;
    }

    private bool IsRepetition()
    {
        int currentPly = Chess.Instance.historyPly - 1;
        ulong currentKey = Chess.Instance.currentZobristKey;
        
        int startPly = Mathf.Max(0, currentPly - 100);
        for (int i = currentPly - 2; i >= startPly; i -= 2)
        {
            if (Chess.Instance.positionHistory[i] == currentKey) return true;
        }
        return false;
    }

    private void CheckTime()
    {
        nodesSinceTimerCheck++;
        if (nodesSinceTimerCheck > 2048) 
        {
            nodesSinceTimerCheck = 0;
            if (searchTimer.ElapsedMilliseconds >= timeLimitMs)
            {
                isTimeUp = true;
            }
        }
    }
    private int EvaluateDevelopment()
    {
        int score = 0;
        
        // 1. Penalize undeveloped minor pieces
        ulong wKnights = Board.Instance.pieceBitboards[(int)pieceType.whiteKnight];
        ulong wBishops = Board.Instance.pieceBitboards[(int)pieceType.whiteBishop];
        if ((wKnights & (1UL << 1)) != 0) score -= 20; // Knight still on b1
        if ((wKnights & (1UL << 6)) != 0) score -= 20; // Knight still on g1
        if ((wBishops & (1UL << 2)) != 0) score -= 20; // Bishop still on c1
        if ((wBishops & (1UL << 5)) != 0) score -= 20; // Bishop still on f1

        ulong bKnights = Board.Instance.pieceBitboards[(int)pieceType.blackKnight];
        ulong bBishops = Board.Instance.pieceBitboards[(int)pieceType.blackBishop];
        if ((bKnights & (1UL << 57)) != 0) score += 20; // Knight still on b8
        if ((bKnights & (1UL << 62)) != 0) score += 20; // Knight still on g8
        if ((bBishops & (1UL << 58)) != 0) score += 20; // Bishop still on c8
        if ((bBishops & (1UL << 61)) != 0) score += 20; // Bishop still on f8

        // 2. Early Queen Penalty
        ulong wQueen = Board.Instance.pieceBitboards[(int)pieceType.whiteQueen];
        if ((wQueen & (1UL << 3)) == 0) // White Queen is NOT on its starting square (d1)
        {
            int undevelopedCount = 0;
            if ((wKnights & (1UL << 1)) != 0) undevelopedCount++;
            if ((wKnights & (1UL << 6)) != 0) undevelopedCount++;
            if ((wBishops & (1UL << 2)) != 0) undevelopedCount++;
            if ((wBishops & (1UL << 5)) != 0) undevelopedCount++;
            
            // If Queen moved but 2 or more minor pieces are still at home, heavy penalty!
            if (undevelopedCount >= 2) score -= 30; 
        }

        ulong bQueen = Board.Instance.pieceBitboards[(int)pieceType.blackQueen];
        if ((bQueen & (1UL << 59)) == 0) // Black Queen is NOT on d8
        {
            int undevelopedCount = 0;
            if ((bKnights & (1UL << 57)) != 0) undevelopedCount++;
            if ((bKnights & (1UL << 62)) != 0) undevelopedCount++;
            if ((bBishops & (1UL << 58)) != 0) undevelopedCount++;
            if ((bBishops & (1UL << 61)) != 0) undevelopedCount++;
            
            if (undevelopedCount >= 2) score += 30; 
        }

        return score;
    }
    private int EvaluateCastling()
    {
        int score = 0;
        int rights = Chess.Instance.castlingRights;
        
        ulong wKing = Board.Instance.pieceBitboards[(int)pieceType.whiteKing];
        bool wCastled = (wKing & ((1UL << 2) | (1UL << 6))) != 0; // King is on c1 or g1
        
        if (wCastled) score += 40; // Reward for castling
        else if ((rights & 3) == 0) score -= 40; // Lost both Kingside and Queenside rights but didn't castle
        
        ulong bKing = Board.Instance.pieceBitboards[(int)pieceType.blackKing];
        bool bCastled = (bKing & ((1UL << 58) | (1UL << 62))) != 0; // King is on c8 or g8
        
        if (bCastled) score -= 40; 
        else if ((rights & 12) == 0) score += 40; 

        return score;
    }
}

public static class BitboardUtility
{
    public static int PopCount(ulong x)
    {
        x -= (x >> 1) & 0x5555555555555555UL;
        x = (x & 0x3333333333333333UL) + ((x >> 2) & 0x3333333333333333UL);
        x = (x + (x >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
        return (int)((x * 0x0101010101010101UL) >> 56);
    }
}