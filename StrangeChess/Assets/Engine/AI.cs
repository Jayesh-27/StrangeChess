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

    // --- KILLER MOVES ARRAY ---
    // [Max Search Depth, 2 Killer Moves per depth]
    public ushort[,] killerMoves = new ushort[64, 2];

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
    [SerializeField] private static readonly int[] pawnPST = {
         0,  0,  0,  0,  0,  0,  0,  0,
        50, 50, 50, 50, 50, 50, 50, 50,
        10, 10, 20, 30, 30, 20, 10, 10,
         5,  5, 10, 25, 25, 10,  5,  5,
         0,  0,  0, 20, 20,  0,  0,  0,
         5, -5,-10,  0,  0,-10, -5,  5,
         5, 10, 10,-20,-20, 10, 10,  5,
         0,  0,  0,  0,  0,  0,  0,  0
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
    [SerializeField] private static readonly int[] kingPST = {
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -20,-30,-30,-40,-40,-30,-30,-20,
        -10,-20,-20,-20,-20,-20,-20,-10,
         20, 20,  0,  0,  0,  0, 20, 20,
         20, 30, 10,  0,  0, 10, 30, 20
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
        
        int actualMaxDepth = Mathf.Min(maxDepth, 63);
        for (int currentDepth = 1; currentDepth <= actualMaxDepth; currentDepth++)
        {
            int bestScore = negativeInfinity;
            int alpha = negativeInfinity;
            int beta = positiveInfinity;
            ushort bestMoveThisDepth = 0;

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

                // Re-evaluate King after the move
                ulong currentKing = isWhite ? Board.Instance.pieceBitboards[(int)pieceType.whiteKing] : Board.Instance.pieceBitboards[(int)pieceType.blackKing];

                if (Chess.Instance.isSquareSafe(currentKing))
                {
                    ClickDetector.Instance.isWhiteTurn = !isWhite;
                    int score = -Search(currentDepth - 1, 1, -beta, -alpha, true);
                    ClickDetector.Instance.isWhiteTurn = isWhite;

                    if (isTimeUp) 
                    {
                        Chess.Instance.unmakeMove(fromSquare, toSquare, capturedPiece, originalPiece);
                        Chess.Instance.castlingRights = savedCastling;
                        Chess.Instance.enPassantTarget = savedEP;
                        Chess.Instance.currentZobristKey = savedHash;
                        break;
                    }

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestMoveThisDepth = move;
                    }
                    if (bestScore > alpha) alpha = bestScore;
                }

                Chess.Instance.unmakeMove(fromSquare, toSquare, capturedPiece, originalPiece);
                Chess.Instance.castlingRights = savedCastling;
                Chess.Instance.enPassantTarget = savedEP;
                Chess.Instance.currentZobristKey = savedHash;
            }

            if (isTimeUp) break;

            absoluteBestMove = bestMoveThisDepth;
            OrderMoves(0, currentMoveCount, absoluteBestMove);
        }

        searchTimer.Stop();
        return absoluteBestMove;
    }
    
    private int Search(int depth, int ply, int alpha, int beta, bool allowNull)
    {
        CheckTime();
        if (isTimeUp) return 0;

        // --- NEW SAFETY CLAMP ---
        if (ply >= 63) 
        {
            int eval = EvaluateBoard();
            return ClickDetector.Instance.isWhiteTurn ? eval : -eval;
        }

        if (depth == 0) 
        {
            return QuiescenceSearch(ply, alpha, beta);
        }
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

        // --- NULL MOVE PRUNING (NMP) ---
        if (depth >= 3 && allowNull && !inCheck)
        {
            bool hasNonPawnMaterial = false;
            if (isWhite) hasNonPawnMaterial = (Board.Instance.pieceBitboards[(int)pieceType.whiteKnight] | Board.Instance.pieceBitboards[(int)pieceType.whiteBishop] | Board.Instance.pieceBitboards[(int)pieceType.whiteRook] | Board.Instance.pieceBitboards[(int)pieceType.whiteQueen]) != 0;
            else hasNonPawnMaterial = (Board.Instance.pieceBitboards[(int)pieceType.blackKnight] | Board.Instance.pieceBitboards[(int)pieceType.blackBishop] | Board.Instance.pieceBitboards[(int)pieceType.blackRook] | Board.Instance.pieceBitboards[(int)pieceType.blackQueen]) != 0;

            if (hasNonPawnMaterial)
            {
                int savedEP = Chess.Instance.enPassantTarget;
                ulong savedHash = Chess.Instance.currentZobristKey;

                ClickDetector.Instance.isWhiteTurn = !isWhite;
                Chess.Instance.enPassantTarget = -1; 
                Chess.Instance.currentZobristKey ^= Zobrist.sideToMove;
                if (savedEP != -1) Chess.Instance.currentZobristKey ^= Zobrist.enPassantArray[savedEP];
                Chess.Instance.currentZobristKey ^= Zobrist.enPassantArray[64];

                int R = 2; 
                int nullScore = -Search(depth - 1 - R, ply + 1, -beta, -beta + 1, false);

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
            ulong fromSquare = 1UL << fromIndex;
            ulong toSquare = 1UL << toIndex;

            int savedCastling = Chess.Instance.castlingRights;
            int savedEP = Chess.Instance.enPassantTarget;
            ulong savedHash = Chess.Instance.currentZobristKey;
            pieceType originalPiece = Board.Instance.boardSquares[fromIndex];
            pieceType capturedPiece = Board.Instance.boardSquares[toIndex]; 

            Chess.Instance.movePiece(fromSquare, toSquare);

            // Re-evaluate King after the move
            ulong currentKing = isWhite ? Board.Instance.pieceBitboards[(int)pieceType.whiteKing] : Board.Instance.pieceBitboards[(int)pieceType.blackKing];

            if (Chess.Instance.isSquareSafe(currentKing))
            {
                legalMovesPlayed++;
                ClickDetector.Instance.isWhiteTurn = !isWhite;
                
                int score = -Search(depth - 1, ply + 1, -beta, -alpha, true);
                
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
                    int flag = move >> 12;
                    bool isCapture = (flag == 4 || flag == 5 || flag >= 12);
                    
                    if (!isCapture)
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

        if (legalMovesPlayed == 0)
        {
            if (inCheck) return negativeInfinity + ply; // Checkmate
            else return 0; // Stalemate
        }

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

        // --- NEW SAFETY CLAMP ---
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

            // Re-evaluate King after the move
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

    public int EvaluateBoard()
    {
        int score = 0;
        score += MaterialValueEvaluation();
        score += PSTEvaluation();
        return score;
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
    
    private int PSTEvaluation()
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
                case pieceType.whitePawn: case pieceType.blackPawn: pstValue = pawnPST[pstIndex]; break;
                case pieceType.whiteKnight: case pieceType.blackKnight: pstValue = knightPST[pstIndex]; break;
                case pieceType.whiteBishop: case pieceType.blackBishop: pstValue = bishopPST[pstIndex]; break;
                case pieceType.whiteRook: case pieceType.blackRook: pstValue = rookPST[pstIndex]; break;
                case pieceType.whiteQueen: case pieceType.blackQueen: pstValue = queenPST[pstIndex]; break;
                case pieceType.whiteKing: case pieceType.blackKing: pstValue = kingPST[pstIndex]; break;
            }

            tempScore += isWhite ? pstValue : -pstValue;
        }

        return tempScore;
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