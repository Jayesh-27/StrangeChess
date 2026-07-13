using UnityEngine;

public class AI : MonoBehaviour
{
    public static AI Instance;

    // Standard piece values in centipawns
    [SerializeField] const int pawnValue = 100;
    [SerializeField] const int knightValue = 300;
    [SerializeField] const int bishopValue = 320;
    [SerializeField] const int rookValue = 500;
    [SerializeField] const int queenValue = 900;
    [SerializeField] private int depth = 4;

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
        // PRESS SPACEBAR TO MAKE THE AI PLAY A MOVE!
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
        Debug.Log("AI is thinking...");
        ushort bestMove = GetBestMove(depth);
        
        if (bestMove == 0) 
        {
            Debug.Log("Game Over! No legal moves available.");
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
        
        Debug.Log($"AI Played Move: {fromIndex} -> {toIndex}");
    }

    public ushort GetBestMove(int depth)
    {
        Chess.Instance.GenerateAllMoves(0);
        int currentMoveCount = Chess.Instance.moveCount[0];
        
        ushort bestMove = 0;
        bool isWhite = ClickDetector.Instance.isWhiteTurn;
        
        // Set the worst possible starting scores
        int bestScore = isWhite ? negativeInfinity : positiveInfinity;
        int alpha = negativeInfinity;
        int beta = positiveInfinity;

        for (int i = 0; i < currentMoveCount; i++)
        {
            ushort move = Chess.Instance.moveList[0][i];
            int fromIndex = move & 0x3F;
            int toIndex = (move >> 6) & 0x3F;
            ulong fromSquare = 1UL << fromIndex;
            ulong toSquare = 1UL << toIndex;

            // Backups
            int savedCastling = Chess.Instance.castlingRights;
            int savedEP = Chess.Instance.enPassantTarget;
            pieceType originalPiece = Board.Instance.boardSquares[fromIndex];
            pieceType capturedPiece = Board.Instance.boardSquares[toIndex]; 

            // Execute Move
            Chess.Instance.movePiece(fromSquare, toSquare);

            ulong ourKing = isWhite ? Board.Instance.pieceBitboards[(int)pieceType.whiteKing] : Board.Instance.pieceBitboards[(int)pieceType.blackKing];
            bool isLegal = Chess.Instance.isSquareSafe(ourKing);

            if (isLegal)
            {
                // Swap turns and dive into the tree
                ClickDetector.Instance.isWhiteTurn = !isWhite;
                int score = Search(depth - 1, 1, alpha, beta);
                ClickDetector.Instance.isWhiteTurn = isWhite;

                // White wants the highest score possible
                if (isWhite)
                {
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestMove = move;
                    }
                    alpha = Mathf.Max(alpha, bestScore);
                }
                // Black wants the lowest score possible
                else
                {
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestMove = move;
                    }
                    beta = Mathf.Min(beta, bestScore);
                }
            }

            // Restore physical board state
            Chess.Instance.unmakeMove(fromSquare, toSquare, capturedPiece, originalPiece);
            Chess.Instance.castlingRights = savedCastling;
            Chess.Instance.enPassantTarget = savedEP;
        }

        return bestMove;
    }

    private int Search(int depth, int ply, int alpha, int beta)
    {
        // Base Case: We reached the end of the future timeline. Return the static score!
        if (depth == 0) return EvaluateBoard();

        Chess.Instance.GenerateAllMoves(ply);
        int currentMoveCount = Chess.Instance.moveCount[ply];
        bool isWhite = ClickDetector.Instance.isWhiteTurn;
        
        int legalMovesPlayed = 0;

        if (isWhite)
        {
            int maxScore = negativeInfinity;
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
                bool isLegal = Chess.Instance.isSquareSafe(Board.Instance.pieceBitboards[(int)pieceType.whiteKing]);

                if (isLegal)
                {
                    legalMovesPlayed++;
                    ClickDetector.Instance.isWhiteTurn = false;
                    int eval = Search(depth - 1, ply + 1, alpha, beta);
                    ClickDetector.Instance.isWhiteTurn = true;
                    
                    maxScore = Mathf.Max(maxScore, eval);
                    alpha = Mathf.Max(alpha, eval);
                    
                    // ALPHA-BETA PRUNING: Stop searching this branch if it's already worse than a previous option!
                    if (beta <= alpha)
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
            
            // Checkmate & Stalemate Detection
            if (legalMovesPlayed == 0)
            {
                if (!Chess.Instance.isSquareSafe(Board.Instance.pieceBitboards[(int)pieceType.whiteKing]))
                    return negativeInfinity + ply; // Checkmate (Adding ply makes the AI prefer faster mates)
                else
                    return 0; // Stalemate is a draw (0 points)
            }
            return maxScore;
        }
        else // Black's Turn (The Minimizer)
        {
            int minScore = positiveInfinity;
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
                bool isLegal = Chess.Instance.isSquareSafe(Board.Instance.pieceBitboards[(int)pieceType.blackKing]);

                if (isLegal)
                {
                    legalMovesPlayed++;
                    ClickDetector.Instance.isWhiteTurn = true;
                    int eval = Search(depth - 1, ply + 1, alpha, beta);
                    ClickDetector.Instance.isWhiteTurn = false;
                    
                    minScore = Mathf.Min(minScore, eval);
                    beta = Mathf.Min(beta, eval);
                    
                    // ALPHA-BETA PRUNING
                    if (beta <= alpha)
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

            // Checkmate & Stalemate Detection
            if (legalMovesPlayed == 0)
            {
                if (!Chess.Instance.isSquareSafe(Board.Instance.pieceBitboards[(int)pieceType.blackKing]))
                    return positiveInfinity - ply; // Checkmate
                else
                    return 0; // Stalemate
            }
            return minScore;
        }
    }

    public int EvaluateBoard()
    {
        int score = 0;
        score += MaterialValueEvaluation();
        score += PSTEvaluation();
        return score;
    }

    private int MaterialValueEvaluation()
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