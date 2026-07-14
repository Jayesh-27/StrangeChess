using UnityEngine;
using System.Diagnostics;

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

    // Infinity values for Checkmate detection
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
            PlayBestMove(depth); // Searches depth half-moves into the future
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
        
        // --- VISUAL ROUTING FOR AI ---
        if (movingPiece == pieceType.whitePawn && toIndex >= 56) Board.Instance.PromoteVisualPiece(fromIndex, toIndex, true);
        else if (movingPiece == pieceType.blackPawn && toIndex <= 7) Board.Instance.PromoteVisualPiece(fromIndex, toIndex, false);
        else if (movingPiece == pieceType.whiteKing && fromIndex == 4 && toIndex == 6) { Board.Instance.Move3DModel(fromSquare, toSquare); Board.Instance.Move3DModel(1UL << 7, 1UL << 5); } 
        else if (movingPiece == pieceType.whiteKing && fromIndex == 4 && toIndex == 2) { Board.Instance.Move3DModel(fromSquare, toSquare); Board.Instance.Move3DModel(1UL << 0, 1UL << 3); } 
        else if (movingPiece == pieceType.blackKing && fromIndex == 60 && toIndex == 62) { Board.Instance.Move3DModel(fromSquare, toSquare); Board.Instance.Move3DModel(1UL << 63, 1UL << 61); } 
        else if (movingPiece == pieceType.blackKing && fromIndex == 60 && toIndex == 58) { Board.Instance.Move3DModel(fromSquare, toSquare); Board.Instance.Move3DModel(1UL << 56, 1UL << 59); } 
        else Board.Instance.Move3DModel(fromSquare, toSquare);

        // En Passant Visual Destruction 
        if (movingPiece == pieceType.whitePawn && targetPiece == pieceType.none && (toIndex == fromIndex + 7 || toIndex == fromIndex + 9)) Board.Instance.DestroyVisualPiece(toIndex - 8);
        else if (movingPiece == pieceType.blackPawn && targetPiece == pieceType.none && (toIndex == fromIndex - 7 || toIndex == fromIndex - 9)) Board.Instance.DestroyVisualPiece(toIndex + 8);
        
        if (StockfishTester.Instance != null) StockfishTester.Instance.ReportUserMove(fromSquare, toSquare, movingPiece);
        // Physically update engine arrays
        Chess.Instance.movePiece(fromSquare, toSquare);
        
        ClickDetector.Instance.isWhiteTurn = !ClickDetector.Instance.isWhiteTurn;
        ClickDetector.Instance.availableMoves = 0;
        ClickDetector.Instance.isSelected = false;
        
        UnityEngine.Debug.Log($"AI Played Move: {fromIndex} -> {toIndex}");
    }

    public ushort GetBestMove(int maxDepth)
    {
        // 1. Reset the clocks!
        searchTimer.Restart();
        isTimeUp = false;
        nodesSinceTimerCheck = 0;
        
        Chess.Instance.GenerateAllMoves(0);
        int currentMoveCount = Chess.Instance.moveCount[0];            
        OrderMoves(0, currentMoveCount); 
        
        ushort absoluteBestMove = 0;
        bool isWhite = ClickDetector.Instance.isWhiteTurn;
        
        // --- ITERATIVE DEEPENING LOOP ---
        for (int currentDepth = 1; currentDepth <= maxDepth; currentDepth++)
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
                pieceType originalPiece = Board.Instance.boardSquares[fromIndex];
                pieceType capturedPiece = Board.Instance.boardSquares[toIndex]; 

                Chess.Instance.movePiece(fromSquare, toSquare);
                ulong ourKing = isWhite ? Board.Instance.pieceBitboards[(int)pieceType.whiteKing] : Board.Instance.pieceBitboards[(int)pieceType.blackKing];
                
                if (Chess.Instance.isSquareSafe(ourKing))
                {
                    ClickDetector.Instance.isWhiteTurn = !isWhite;
                    int score = -Search(currentDepth - 1, 1, -beta, -alpha);
                    ClickDetector.Instance.isWhiteTurn = isWhite;

                    // THE ABORT: If time ran out mid-search, this score is corrupted. Throw it away!
                    if (isTimeUp) 
                    {
                        Chess.Instance.unmakeMove(fromSquare, toSquare, capturedPiece, originalPiece);
                        Chess.Instance.castlingRights = savedCastling;
                        Chess.Instance.enPassantTarget = savedEP;
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
            }

            // If time is up, break the depth loop so we don't accidentally save an incomplete depth
            if (isTimeUp) break;

            // Otherwise, this depth finished successfully! Save it as our safety net.
            absoluteBestMove = bestMoveThisDepth;
            UnityEngine.Debug.Log($"Depth {currentDepth} completed! Eval: {bestScore / 100f:F2}");

            // --- MOVE ORDERING MAGIC ---
            // Shove the best move we just found to the absolute front of the array (Index 0) for the next depth!
            for (int i = 0; i < currentMoveCount; i++)
            {
                if (Chess.Instance.moveList[0][i] == absoluteBestMove)
                {
                    ushort temp = Chess.Instance.moveList[0][0];
                    Chess.Instance.moveList[0][0] = absoluteBestMove;
                    Chess.Instance.moveList[0][i] = temp;
                    break;
                }
            }
        }

        searchTimer.Stop();
        return absoluteBestMove;
    }
    
    private int Search(int depth, int ply, int alpha, int beta)
    {
        CheckTime();
        if (isTimeUp) return 0;

        if (depth == 0) 
        {
            return QuiescenceSearch(ply, alpha, beta);
        }

        Chess.Instance.GenerateAllMoves(ply);
        int currentMoveCount = Chess.Instance.moveCount[ply];
    
        OrderMoves(ply, currentMoveCount);
        bool isWhite = ClickDetector.Instance.isWhiteTurn;
        
        int legalMovesPlayed = 0;
        int bestScore = negativeInfinity;

        // ONE unified loop for both White and Black!
        for (int i = 0; i < currentMoveCount; i++)
        {
            ushort move = Chess.Instance.moveList[ply][i];
            int fromIndex = move & 0x3F;
            int toIndex = (move >> 6) & 0x3F;
            ulong fromSquare = 1UL << fromIndex;
            ulong toSquare = 1UL << toIndex;

            int savedCastling = Chess.Instance.castlingRights;
            int savedEP = Chess.Instance.enPassantTarget;
            pieceType originalPiece = Board.Instance.boardSquares[fromIndex];
            pieceType capturedPiece = Board.Instance.boardSquares[toIndex]; 

            Chess.Instance.movePiece(fromSquare, toSquare);
            ulong ourKing = isWhite ? Board.Instance.pieceBitboards[(int)pieceType.whiteKing] : Board.Instance.pieceBitboards[(int)pieceType.blackKing];

            if (Chess.Instance.isSquareSafe(ourKing))
            {
                legalMovesPlayed++;
                ClickDetector.Instance.isWhiteTurn = !isWhite;
                
                // The Negamax Recursion
                int score = -Search(depth - 1, ply + 1, -beta, -alpha);
                
                ClickDetector.Instance.isWhiteTurn = isWhite;
                
                if (score > bestScore) bestScore = score;
                if (bestScore > alpha) alpha = bestScore;
                
                // Alpha-Beta Pruning
                if (alpha >= beta)
                {
                    Chess.Instance.unmakeMove(fromSquare, toSquare, capturedPiece, originalPiece);
                    Chess.Instance.castlingRights = savedCastling;
                    Chess.Instance.enPassantTarget = savedEP;
                    break; 
                }
            }

            Chess.Instance.unmakeMove(fromSquare, toSquare, capturedPiece, originalPiece);
            Chess.Instance.castlingRights = savedCastling;
            Chess.Instance.enPassantTarget = savedEP;
        }

        if (legalMovesPlayed == 0)
        {
            ulong ourKing = isWhite ? Board.Instance.pieceBitboards[(int)pieceType.whiteKing] : Board.Instance.pieceBitboards[(int)pieceType.blackKing];
            if (!Chess.Instance.isSquareSafe(ourKing)) return negativeInfinity + ply; 
            else return 0; 
        }
        
        return bestScore;
    }

    private int QuiescenceSearch(int ply, int alpha, int beta)
    {
        CheckTime();
        if (isTimeUp) return 0;

        // 1. The "Stand Pat" Evaluation
        int standPat = EvaluateBoard();
        standPat = ClickDetector.Instance.isWhiteTurn ? standPat : -standPat;

        // If our baseline score is already too good, the opponent will avoid this branch. Prune!
        if (standPat >= beta) return beta;
        
        // If our baseline score is better than alpha, update our minimum expectations.
        if (alpha < standPat) alpha = standPat;

        // 2. Generate Moves
        Chess.Instance.GenerateAllMoves(ply);
        int currentMoveCount = Chess.Instance.moveCount[ply];    
        OrderMoves(ply, currentMoveCount);
        bool isWhite = ClickDetector.Instance.isWhiteTurn;

        for (int i = 0; i < currentMoveCount; i++)
        {
            ushort move = Chess.Instance.moveList[ply][i];
            int flag = move >> 12;

            // --- THE MAGIC FILTER ---
            // We ONLY care about captures in Quiescence Search!
            // In your moveFlag enum: 4 = Capture, 5 = EPCapture, 12-15 = PromotionCaptures
            bool isCapture = (flag == 4 || flag == 5 || flag >= 12);
            if (!isCapture) continue; // Skip quiet moves!

            int fromIndex = move & 0x3F;
            int toIndex = (move >> 6) & 0x3F;
            ulong fromSquare = 1UL << fromIndex;
            ulong toSquare = 1UL << toIndex;

            int savedCastling = Chess.Instance.castlingRights;
            int savedEP = Chess.Instance.enPassantTarget;
            pieceType originalPiece = Board.Instance.boardSquares[fromIndex];
            pieceType capturedPiece = Board.Instance.boardSquares[toIndex]; 

            Chess.Instance.movePiece(fromSquare, toSquare);
            ulong ourKing = isWhite ? Board.Instance.pieceBitboards[(int)pieceType.whiteKing] : Board.Instance.pieceBitboards[(int)pieceType.blackKing];

            if (Chess.Instance.isSquareSafe(ourKing))
            {
                ClickDetector.Instance.isWhiteTurn = !isWhite;
                
                // Recursively call QS, NOT the main Search!
                int score = -QuiescenceSearch(ply + 1, -beta, -alpha);
                
                ClickDetector.Instance.isWhiteTurn = isWhite;

                if (score >= beta)
                {
                    Chess.Instance.unmakeMove(fromSquare, toSquare, capturedPiece, originalPiece);
                    Chess.Instance.castlingRights = savedCastling;
                    Chess.Instance.enPassantTarget = savedEP;
                    return beta; // Prune!
                }
                if (score > alpha) alpha = score;
            }

            Chess.Instance.unmakeMove(fromSquare, toSquare, capturedPiece, originalPiece);
            Chess.Instance.castlingRights = savedCastling;
            Chess.Instance.enPassantTarget = savedEP;
        }

        return alpha;
    }

    // --- MOVE ORDERING ---
    private void OrderMoves(int ply, int currentMoveCount)
    {
        // 1. Assign a score to every move
        for (int i = 0; i < currentMoveCount; i++)
        {
            Chess.Instance.moveScores[ply][i] = ScoreMove(Chess.Instance.moveList[ply][i]);
        }

        // 2. Selection Sort: Push the highest-scoring moves to the front of the array
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

            // Swap Scores
            int tempScore = Chess.Instance.moveScores[ply][i];
            Chess.Instance.moveScores[ply][i] = Chess.Instance.moveScores[ply][maxIndex];
            Chess.Instance.moveScores[ply][maxIndex] = tempScore;

            // Swap Moves
            ushort tempMove = Chess.Instance.moveList[ply][i];
            Chess.Instance.moveList[ply][i] = Chess.Instance.moveList[ply][maxIndex];
            Chess.Instance.moveList[ply][maxIndex] = tempMove;
        }
    }

    private int ScoreMove(ushort move)
    {
        int score = 0;
        int fromIndex = move & 0x3F;
        int toIndex = (move >> 6) & 0x3F;
        int flag = move >> 12;

        bool isCapture = (flag == 4 || flag == 5 || flag >= 12);

        if (isCapture)
        {
            pieceType attacker = Board.Instance.boardSquares[fromIndex];
            pieceType victim = Board.Instance.boardSquares[toIndex];

            // If it is En Passant, the target square is empty, so we manually assign the victim as a Pawn
            if (flag == 5) victim = (attacker == pieceType.whitePawn) ? pieceType.blackPawn : pieceType.whitePawn;

            // MVV-LVA Formula: (Victim Value * 10) - Attacker Value
            // Example: Pawn taking Queen = (900 * 10) - 100 = 8900 score!
            score = 10 * GetPieceValue(victim) - GetPieceValue(attacker);
        }

        // Massive bonus for promoting to a Queen (almost as good as capturing one)
        if (flag == 11 || flag == 15) // PromoteToQueen, PromoteToQueenAndCapture
        {
            score += 90000; 
        }

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

        // White Material 
        tempScore += BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.whitePawn]) * pawnValue;
        tempScore += BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.whiteKnight]) * knightValue;
        tempScore += BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.whiteBishop]) * bishopValue;
        tempScore += BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.whiteRook]) * rookValue;
        tempScore += BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.whiteQueen]) * queenValue;

        // Black Material
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

        // Loop through all 64 squares using the array maintained in Board.cs
        for (int i = 0; i < 64; i++)
        {
            pieceType piece = Board.Instance.boardSquares[i];
            
            // Skip empty squares
            if (piece == pieceType.none) continue;

            bool isWhite = piece >= pieceType.whitePawn && piece <= pieceType.whiteKing;
            
            // MAGIC MATH: If Black, XOR by 56 flips the board index upside down!
            int pstIndex = isWhite ? i : i ^ 56; 
            int pstValue = 0;

            // Look up the score based on the piece type
            switch (piece)
            {
                case pieceType.whitePawn: case pieceType.blackPawn: pstValue = pawnPST[pstIndex]; break;
                case pieceType.whiteKnight: case pieceType.blackKnight: pstValue = knightPST[pstIndex]; break;
                case pieceType.whiteBishop: case pieceType.blackBishop: pstValue = bishopPST[pstIndex]; break;
                case pieceType.whiteRook: case pieceType.blackRook: pstValue = rookPST[pstIndex]; break;
                case pieceType.whiteQueen: case pieceType.blackQueen: pstValue = queenPST[pstIndex]; break;
                case pieceType.whiteKing: case pieceType.blackKing: pstValue = kingPST[pstIndex]; break;
            }

            // White adds to the total score, Black subtracts from it
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