using System;

public enum moveFlag : ushort
{
    QuietMove = 0,                      // Knight, Rook, Bishop, Queen, Pawn
    DoublePawn = 1,                     // Pawn
    KingsideCastle = 2,                 // 
    QueensideCastle = 3,                // 
    Capture = 4,                        // Knight, Rook, Bishop, Queen, Pawn
    EnPassantCapture = 5,               // Pawn
    PromoteToKnight = 8,                // Pawn
    PromoteToBishop = 9,                // Pawn
    PromoteToRook = 10,                 // Pawn
    PromoteToQueen = 11,                // Pawn
    PromoteToKnightAndCapture = 12,     // Pawn
    PromoteToBishopAndCapture = 13,     // Pawn
    PromoteToRookAndCapture = 14,       // Pawn
    PromoteToQueenAndCapture = 15       // Pawn
}

public class Chess
{    
    public static Chess Instance = new Chess();
    
    public int castlingRights = 15;
    ulong wKing  = 1UL << 4;
    ulong bKing  = 1UL << 60;
    ulong waRook = 1UL << 0;
    ulong whRook = 1UL << 7;
    ulong baRook = 1UL << 56;
    ulong bhRook = 1UL << 63;
    ulong castlingAllChecks = 0x9100000000000091;    

    public const int MAX_DEPTH = 64; 
    public ushort[][] moveList;
    public int[][] moveScores;
    public int[] moveCount = new int[MAX_DEPTH];

    public int enPassantTarget = -1;
    public int halfmoveClock = 0;
    public int fullmoveNumber = 1;

    // zobirst hashing
    public ulong currentZobristKey = 0;

    // --- NEW: REPETITION HISTORY ---
    public ulong[] positionHistory = new ulong[4096];
    public int historyPly = 0;

    public Chess()
    {
        moveList = new ushort[MAX_DEPTH][];
        moveScores = new int[MAX_DEPTH][];
        for(int i = 0; i < MAX_DEPTH; i++)
        {
            moveList[i] = new ushort[256];
            moveScores[i] = new int[256];
        }
    }
    
    public ulong pawnAttacks(ulong from)
    {
        const ulong fileA = 0x0101010101010101UL;
        const ulong fileH = 0x8080808080808080UL;
        ulong notFileA = ~fileA;
        ulong notFileH = ~fileH;
        ulong leftCap;
        ulong rightCap;

        if(Board.Instance.isWhiteTurn)
        {
            leftCap = (from & notFileH) >> 7; 
            rightCap = (from & notFileA) >> 9;
        }
        else
        {
            leftCap = (from & notFileA) << 7;
            rightCap = (from & notFileH) << 9;
        }
        return leftCap | rightCap;
    }
    
    public ulong pawnMoves(ulong from)
    {
        ulong all = Board.Instance.allPieces;
        ulong white = Board.Instance.whitePieces;
        ulong black = Board.Instance.blackPieces;

        const ulong fileA = 0x0101010101010101UL;
        const ulong fileH = 0x8080808080808080UL;
        ulong notFileA = ~fileA;
        ulong notFileH = ~fileH;

        ulong moves = 0UL;
        
        ulong epSquare = (enPassantTarget != -1) ? (1UL << enPassantTarget) : 0UL;

        if ((from & white) != 0)
        {
            ulong leftCap = (from & notFileA) << 7;
            ulong rightCap = (from & notFileH) << 9;
            
            moves |= (leftCap & (black | epSquare));
            moves |= (rightCap & (black | epSquare));

            ulong one = from << 8;
            if ((one & all) == 0)
            {
                moves |= one;
                const ulong rank2 = 0x000000000000FF00UL;
                ulong two = from << 16;
                if ((from & rank2) != 0 && (two & all) == 0)
                    moves |= two;
            }
        }
        else if ((from & black) != 0)
        {
            ulong leftCap = (from & notFileH) >> 7; 
            ulong rightCap = (from & notFileA) >> 9;
            
            moves |= (leftCap & (white | epSquare));
            moves |= (rightCap & (white | epSquare));

            ulong one = from >> 8;
            if ((one & all) == 0)
            {
                moves |= one;
                const ulong rank7 = 0x00FF000000000000UL;
                ulong two = from >> 16;
                if ((from & rank7) != 0 && (two & all) == 0)
                    moves |= two;
            }
        }

        return moves;
    }

    public ulong knightMoves(ulong from)
    {
        int knightIndex = Board.Instance.GetBitboardIndex(from);
        ulong moves = 0;
        if((from & Board.Instance.whitePieces) != 0)      
        {
            moves = Board.Instance.knightAttacks[knightIndex] & ~Board.Instance.whitePieces;    
        }
        else if((from & Board.Instance.blackPieces) != 0) 
        {
            moves = Board.Instance.knightAttacks[knightIndex] & ~Board.Instance.blackPieces;    
        }
        return moves;
    }

    public ulong kingMoves(ulong from)
    {
        int kingIndex = Board.Instance.GetBitboardIndex(from);
        ulong moves = 0;
        if((from & Board.Instance.whitePieces) != 0)      
        {
            moves = Board.Instance.kingAttacks[kingIndex] & ~Board.Instance.whitePieces;    
            
            if((castlingRights & 1) != 0 
            && isSquareSafe(1UL << 4) 
            && isSquareSafe(1UL << 5) 
            && isSquareSafe(1UL << 6) 
            && (Board.Instance.allPieces & (1UL << 5)) == 0 
            && (Board.Instance.allPieces & (1UL << 6)) == 0)
            {
                moves |= 1UL << 6;
            }
            if((castlingRights & 2) != 0 
            && isSquareSafe(1UL << 4) 
            && isSquareSafe(1UL << 3) 
            && isSquareSafe(1UL << 2)
            && (Board.Instance.allPieces & (1UL << 3)) == 0 
            && (Board.Instance.allPieces & (1UL << 2)) == 0
            && (Board.Instance.allPieces & (1UL << 1)) == 0)
            {
                moves |= 1UL << 2;
            }
        }
        else if((from & Board.Instance.blackPieces) != 0) 
        {
            moves = Board.Instance.kingAttacks[kingIndex] & ~Board.Instance.blackPieces;    
            
            if((castlingRights & 4) != 0 
            && isSquareSafe(1UL << 60) 
            && isSquareSafe(1UL << 61) 
            && isSquareSafe(1UL << 62) 
            && (Board.Instance.allPieces & (1UL << 61)) == 0 
            && (Board.Instance.allPieces & (1UL << 62)) == 0)
            {
                moves |= 1UL << 62; 
            }

            if((castlingRights & 8) != 0 
            && isSquareSafe(1UL << 60) 
            && isSquareSafe(1UL << 59) 
            && isSquareSafe(1UL << 58) 
            && (Board.Instance.allPieces & (1UL << 59)) == 0 
            && (Board.Instance.allPieces & (1UL << 58)) == 0
            && (Board.Instance.allPieces & (1UL << 57)) == 0) 
            {
                moves |= 1UL << 58;
            }
        }
        return moves;
    }

    public ulong bishopMoves(ulong from, ulong simAll = 0)
    {
        int bishopIndex = Board.Instance.GetBitboardIndex(from);
        ulong allPieces = simAll != 0 ? simAll : Board.Instance.allPieces;
        ulong blockers = allPieces & Board.Instance.bishopBlockersMasks[bishopIndex];
        int magicIndex = (int)((blockers * Board.bishopMagics[bishopIndex]) >> (64 - Board.bishopBlockerBitCounts[bishopIndex]));
        ulong attacks = Board.Instance.bishopAttackTable[bishopIndex][magicIndex];

        if(Board.Instance.isWhiteTurn)
            attacks &= ~Board.Instance.whitePieces;
        else
            attacks &= ~Board.Instance.blackPieces;
        return attacks;
    }

    public ulong rookMoves(ulong from, ulong simAll = 0)
    {
        int rookIndex = Board.Instance.GetBitboardIndex(from);
        ulong allPieces = simAll != 0 ? simAll : Board.Instance.allPieces;
        ulong blockers = allPieces & Board.Instance.rookBlockersMasks[rookIndex];
        int magicIndex = (int)((blockers * Board.rookMagics[rookIndex]) >> (64 - Board.rookBlockerBitCounts[rookIndex]));
        ulong attacks = Board.Instance.rookAttackTable[rookIndex][magicIndex];

        if(Board.Instance.isWhiteTurn)
            attacks &= ~Board.Instance.whitePieces;
        else
            attacks &= ~Board.Instance.blackPieces;

        return attacks;
    }

    public ulong queenMoves(ulong from, ulong simAll = 0)
    {
        int index = Board.Instance.GetBitboardIndex(from);
        ulong allPieces = simAll != 0 ? simAll : Board.Instance.allPieces;

        ulong rBlockers = allPieces & Board.Instance.rookBlockersMasks[index];
        int rMagicIndex = (int)((rBlockers * Board.rookMagics[index]) >> (64 - Board.rookBlockerBitCounts[index]));
        ulong rookAttacks = Board.Instance.rookAttackTable[index][rMagicIndex];

        ulong bBlockers = allPieces & Board.Instance.bishopBlockersMasks[index];
        int bMagicIndex = (int)((bBlockers * Board.bishopMagics[index]) >> (64 - Board.bishopBlockerBitCounts[index]));
        ulong bishopAttacks = Board.Instance.bishopAttackTable[index][bMagicIndex];

        ulong attacks = rookAttacks | bishopAttacks;
        
        if(Board.Instance.isWhiteTurn)
            attacks &= ~Board.Instance.whitePieces;
        else
            attacks &= ~Board.Instance.blackPieces;

        return attacks;
    }

    public void movePiece(ulong startSquare, ulong targetSquare)
    {
        int startIndex = Board.Instance.GetBitboardIndex(startSquare);
        int targetIndex = Board.Instance.GetBitboardIndex(targetSquare);

        pieceType originalPiece = Board.Instance.boardSquares[startIndex];
        pieceType movingPiece = originalPiece;
        pieceType capturedPiece = Board.Instance.boardSquares[targetIndex];
        bool isWhiteMoving = originalPiece >= pieceType.whitePawn && originalPiece <= pieceType.whiteKing;

        if (originalPiece == pieceType.whitePawn && targetIndex >= 56) movingPiece = pieceType.whiteQueen;
        else if (originalPiece == pieceType.blackPawn && targetIndex <= 7) movingPiece = pieceType.blackQueen;

        bool isEnPassant = false;
        int epCaptureIndex = -1;
        if (originalPiece == pieceType.whitePawn && capturedPiece == pieceType.none && (targetIndex == startIndex + 7 || targetIndex == startIndex + 9))
        {
            isEnPassant = true;
            epCaptureIndex = targetIndex - 8; 
        }
        else if (originalPiece == pieceType.blackPawn && capturedPiece == pieceType.none && (targetIndex == startIndex - 7 || targetIndex == startIndex - 9))
        {
            isEnPassant = true;
            epCaptureIndex = targetIndex + 8; 
        }

        currentZobristKey ^= Zobrist.castlingRightsArray[castlingRights];
        int oldEpIndex = enPassantTarget == -1 ? 64 : enPassantTarget;
        currentZobristKey ^= Zobrist.enPassantArray[oldEpIndex];
        currentZobristKey ^= Zobrist.piecesArray[(int)originalPiece, startIndex];
        
        if (capturedPiece != pieceType.none)
        {
            currentZobristKey ^= Zobrist.piecesArray[(int)capturedPiece, targetIndex];
        }

        if (isEnPassant)
        {
            pieceType epVictim = isWhiteMoving ? pieceType.blackPawn : pieceType.whitePawn;
            currentZobristKey ^= Zobrist.piecesArray[(int)epVictim, epCaptureIndex];
        }

        if(capturedPiece != pieceType.none)
        {
            Board.Instance.pieceBitboards[(int)capturedPiece] &= ~targetSquare;      
        }
        
        Board.Instance.boardSquares[targetIndex] = movingPiece; 
        Board.Instance.boardSquares[startIndex] = pieceType.none; 

        Board.Instance.pieceBitboards[(int)originalPiece] &= ~startSquare; 
        Board.Instance.pieceBitboards[(int)movingPiece] |= targetSquare;   

        currentZobristKey ^= Zobrist.piecesArray[(int)movingPiece, targetIndex];

        if (isEnPassant)
        {
            pieceType epVictim = isWhiteMoving ? pieceType.blackPawn : pieceType.whitePawn;
            Board.Instance.boardSquares[epCaptureIndex] = pieceType.none;
            Board.Instance.pieceBitboards[(int)epVictim] &= ~(1UL << epCaptureIndex);
        }

        if (originalPiece == pieceType.whiteKing && startSquare == (1UL << 4))
        {
            if (targetSquare == (1UL << 6)) 
            {
                Board.Instance.boardSquares[7] = pieceType.none;
                Board.Instance.boardSquares[5] = pieceType.whiteRook;
                Board.Instance.pieceBitboards[(int)pieceType.whiteRook] &= ~(1UL << 7);
                Board.Instance.pieceBitboards[(int)pieceType.whiteRook] |= (1UL << 5);
                currentZobristKey ^= Zobrist.piecesArray[(int)pieceType.whiteRook, 7]; 
                currentZobristKey ^= Zobrist.piecesArray[(int)pieceType.whiteRook, 5]; 
            }
            else if (targetSquare == (1UL << 2)) 
            {
                Board.Instance.boardSquares[0] = pieceType.none;
                Board.Instance.boardSquares[3] = pieceType.whiteRook;
                Board.Instance.pieceBitboards[(int)pieceType.whiteRook] &= ~(1UL << 0);
                Board.Instance.pieceBitboards[(int)pieceType.whiteRook] |= (1UL << 3);
                currentZobristKey ^= Zobrist.piecesArray[(int)pieceType.whiteRook, 0]; 
                currentZobristKey ^= Zobrist.piecesArray[(int)pieceType.whiteRook, 3]; 
            }
        }
        else if (originalPiece == pieceType.blackKing && startSquare == (1UL << 60))
        {
            if (targetSquare == (1UL << 62)) 
            {
                Board.Instance.boardSquares[63] = pieceType.none;
                Board.Instance.boardSquares[61] = pieceType.blackRook;
                Board.Instance.pieceBitboards[(int)pieceType.blackRook] &= ~(1UL << 63);
                Board.Instance.pieceBitboards[(int)pieceType.blackRook] |= (1UL << 61);
                currentZobristKey ^= Zobrist.piecesArray[(int)pieceType.blackRook, 63]; 
                currentZobristKey ^= Zobrist.piecesArray[(int)pieceType.blackRook, 61]; 
            }
            else if (targetSquare == (1UL << 58)) 
            {
                Board.Instance.boardSquares[56] = pieceType.none;
                Board.Instance.boardSquares[59] = pieceType.blackRook;
                Board.Instance.pieceBitboards[(int)pieceType.blackRook] &= ~(1UL << 56);
                Board.Instance.pieceBitboards[(int)pieceType.blackRook] |= (1UL << 59);
                currentZobristKey ^= Zobrist.piecesArray[(int)pieceType.blackRook, 56]; 
                currentZobristKey ^= Zobrist.piecesArray[(int)pieceType.blackRook, 59]; 
            }
        }

        if (((startSquare | targetSquare) & castlingAllChecks) != 0)
        {
            if (startSquare == wKing) castlingRights &= 12;
            else if (startSquare == bKing) castlingRights &= 3;
            if (startSquare == waRook || targetSquare == waRook) castlingRights &= 13;
            if (startSquare == whRook || targetSquare == whRook) castlingRights &= 14;
            if (startSquare == baRook || targetSquare == baRook) castlingRights &= 7;
            if (startSquare == bhRook || targetSquare == bhRook) castlingRights &= 11;
        }

        if ((originalPiece == pieceType.whitePawn || originalPiece == pieceType.blackPawn) && Math.Abs(targetIndex - startIndex) == 16)
            enPassantTarget = isWhiteMoving ? startIndex + 8 : startIndex - 8;
        else
            enPassantTarget = -1; 

        currentZobristKey ^= Zobrist.castlingRightsArray[castlingRights];
        int newEpIndex = enPassantTarget == -1 ? 64 : enPassantTarget;
        currentZobristKey ^= Zobrist.enPassantArray[newEpIndex];
        currentZobristKey ^= Zobrist.sideToMove;

        ulong moveMask = startSquare | targetSquare;
        if (isWhiteMoving)
        {
            Board.Instance.whitePieces ^= moveMask;
            if (capturedPiece != pieceType.none) Board.Instance.blackPieces ^= targetSquare;
            if (isEnPassant) Board.Instance.blackPieces &= ~(1UL << epCaptureIndex);
            
            if (originalPiece == pieceType.whiteKing && startSquare == (1UL << 4))
            {
                if (targetSquare == (1UL << 6)) { Board.Instance.whitePieces &= ~(1UL << 7); Board.Instance.whitePieces |= (1UL << 5); }
                else if (targetSquare == (1UL << 2)) { Board.Instance.whitePieces &= ~(1UL << 0); Board.Instance.whitePieces |= (1UL << 3); }
            }
        }
        else
        {
            Board.Instance.blackPieces ^= moveMask;
            if (capturedPiece != pieceType.none) Board.Instance.whitePieces ^= targetSquare;
            if (isEnPassant) Board.Instance.whitePieces &= ~(1UL << epCaptureIndex); 
            
            if (originalPiece == pieceType.blackKing && startSquare == (1UL << 60))
            {
                if (targetSquare == (1UL << 62)) { Board.Instance.blackPieces &= ~(1UL << 63); Board.Instance.blackPieces |= (1UL << 61); }
                else if (targetSquare == (1UL << 58)) { Board.Instance.blackPieces &= ~(1UL << 56); Board.Instance.blackPieces |= (1UL << 59); }
            }
        }        
        
        Board.Instance.CalculateExtraBitboards();

        // --- NEW: RECORD HISTORY ---
        positionHistory[historyPly] = currentZobristKey;
        historyPly++;
    }

    public ulong checkLegalMoves(ulong startSquare, ulong moves)
    {
        ulong legalMoves = 0;
        pieceType originalPiece = Board.Instance.boardSquares[Board.Instance.GetBitboardIndex(startSquare)];

        while (moves != 0)
        {
            ulong targetSquare = moves & ~(moves - 1);
            pieceType capturedPiece = Board.Instance.bitboardToPiece(targetSquare);

            int castlingRightsTemp = castlingRights;
            int enPassantTargetTemp = enPassantTarget; 
            ulong savedHash = currentZobristKey; 

            movePiece(startSquare, targetSquare);
            
            ulong SquareToCheck = Board.Instance.isWhiteTurn ? Board.Instance.pieceBitboards[(int)pieceType.whiteKing] : Board.Instance.pieceBitboards[(int)pieceType.blackKing];
            if(isSquareSafe(SquareToCheck))
                legalMoves |= targetSquare;
                
            unmakeMove(startSquare, targetSquare, capturedPiece, originalPiece); 
            castlingRights = castlingRightsTemp;
            enPassantTarget = enPassantTargetTemp; 
            currentZobristKey = savedHash; 

            moves &= moves - 1;
        }
        return legalMoves;
    }

    public bool isSquareSafe(ulong king)
    {
        if (king == 0) return false; 
        
        bool isWhiteTurn = Board.Instance.isWhiteTurn;
        int kingIndex = Board.Instance.GetBitboardIndex(king);

        ulong enemyKnights = isWhiteTurn ? Board.Instance.pieceBitboards[(int)pieceType.blackKnight] : Board.Instance.pieceBitboards[(int)pieceType.whiteKnight];
        ulong enemyRooks   = isWhiteTurn ? Board.Instance.pieceBitboards[(int)pieceType.blackRook]   : Board.Instance.pieceBitboards[(int)pieceType.whiteRook];
        ulong enemyBishops = isWhiteTurn ? Board.Instance.pieceBitboards[(int)pieceType.blackBishop] : Board.Instance.pieceBitboards[(int)pieceType.whiteBishop];
        ulong enemyQueens  = isWhiteTurn ? Board.Instance.pieceBitboards[(int)pieceType.blackQueen]  : Board.Instance.pieceBitboards[(int)pieceType.whiteQueen];
        ulong enemyPawns   = isWhiteTurn ? Board.Instance.pieceBitboards[(int)pieceType.blackPawn]   : Board.Instance.pieceBitboards[(int)pieceType.whitePawn];
        ulong enemyKing    = isWhiteTurn ? Board.Instance.pieceBitboards[(int)pieceType.blackKing]   : Board.Instance.pieceBitboards[(int)pieceType.whiteKing];

        ulong knightAttacksFromKing = Board.Instance.knightAttacks[kingIndex];
        if ((knightAttacksFromKing & enemyKnights) != 0) return false;

        ulong rBlockers = Board.Instance.allPieces & Board.Instance.rookBlockersMasks[kingIndex];
        int rMagicIndex = (int)((rBlockers * Board.rookMagics[kingIndex]) >> (64 - Board.rookBlockerBitCounts[kingIndex]));
        ulong rookAttacksFromKing = Board.Instance.rookAttackTable[kingIndex][rMagicIndex];        
        
        if ((rookAttacksFromKing & (enemyRooks | enemyQueens)) != 0) return false;

        ulong bBlockers = Board.Instance.allPieces & Board.Instance.bishopBlockersMasks[kingIndex];
        int bMagicIndex = (int)((bBlockers * Board.bishopMagics[kingIndex]) >> (64 - Board.bishopBlockerBitCounts[kingIndex]));
        ulong bishopAttacksFromKing = Board.Instance.bishopAttackTable[kingIndex][bMagicIndex];        
        
        if ((bishopAttacksFromKing & (enemyBishops | enemyQueens)) != 0) return false;

        const ulong notFileA = ~0x0101010101010101UL;
        const ulong notFileH = ~0x8080808080808080UL;
        
        if (isWhiteTurn)
        {
            ulong kingPawnAttacks = ((king & notFileA) << 7) | ((king & notFileH) << 9);
            if ((kingPawnAttacks & enemyPawns) != 0) return false;
        }
        else
        {
            ulong kingPawnAttacks = ((king & notFileH) >> 7) | ((king & notFileA) >> 9);
            if ((kingPawnAttacks & enemyPawns) != 0) return false;
        }

        ulong kingAttacksFromKing = Board.Instance.kingAttacks[kingIndex];
        if ((kingAttacksFromKing & enemyKing) != 0) return false;

        return true; 
    }

    public void unmakeMove(ulong startSquare, ulong targetSquare, pieceType capturedPiece, pieceType originalMovedPiece)
    {
        int startIndex = Board.Instance.GetBitboardIndex(startSquare);
        int targetIndex = Board.Instance.GetBitboardIndex(targetSquare);
        
        pieceType movedPiece = Board.Instance.boardSquares[targetIndex];

        Board.Instance.pieceBitboards[(int)movedPiece] &= ~targetSquare;
        
        Board.Instance.pieceBitboards[(int)originalMovedPiece] |= startSquare;
        Board.Instance.boardSquares[startIndex] = originalMovedPiece;
        
        Board.Instance.boardSquares[targetIndex] = capturedPiece; 
        if (capturedPiece != pieceType.none)
        {
            Board.Instance.pieceBitboards[(int)capturedPiece] |= targetSquare;
        }

        if (originalMovedPiece == pieceType.whiteKing && startSquare == (1UL << 4)) 
        {
            if (targetSquare == (1UL << 6)) 
            {
                Board.Instance.boardSquares[7] = pieceType.whiteRook; 
                Board.Instance.boardSquares[5] = pieceType.none;      
                Board.Instance.pieceBitboards[(int)pieceType.whiteRook] |= (1UL << 7);
                Board.Instance.pieceBitboards[(int)pieceType.whiteRook] &= ~(1UL << 5);
            }
            else if (targetSquare == (1UL << 2)) 
            {
                Board.Instance.boardSquares[0] = pieceType.whiteRook; 
                Board.Instance.boardSquares[3] = pieceType.none;      
                Board.Instance.pieceBitboards[(int)pieceType.whiteRook] |= (1UL << 0);
                Board.Instance.pieceBitboards[(int)pieceType.whiteRook] &= ~(1UL << 3);
            }
        }
        else if (originalMovedPiece == pieceType.blackKing && startSquare == (1UL << 60)) 
        {
            if (targetSquare == (1UL << 62)) 
            {
                Board.Instance.boardSquares[63] = pieceType.blackRook; 
                Board.Instance.boardSquares[61] = pieceType.none;      
                Board.Instance.pieceBitboards[(int)pieceType.blackRook] |= (1UL << 63);
                Board.Instance.pieceBitboards[(int)pieceType.blackRook] &= ~(1UL << 61);
            }
            else if (targetSquare == (1UL << 58)) 
            {
                Board.Instance.boardSquares[56] = pieceType.blackRook; 
                Board.Instance.boardSquares[59] = pieceType.none;      
                Board.Instance.pieceBitboards[(int)pieceType.blackRook] |= (1UL << 56);
                Board.Instance.pieceBitboards[(int)pieceType.blackRook] &= ~(1UL << 59);
            }
        }

        ulong moveMask = startSquare | targetSquare;
        bool isWhiteMoving = originalMovedPiece >= pieceType.whitePawn && originalMovedPiece <= pieceType.whiteKing;
        
        if (isWhiteMoving)
        {
            Board.Instance.whitePieces ^= moveMask;
            if (capturedPiece != pieceType.none) Board.Instance.blackPieces ^= targetSquare; 
            
            if (originalMovedPiece == pieceType.whiteKing && startSquare == (1UL << 4))
            {
                if (targetSquare == (1UL << 6)) { Board.Instance.whitePieces |= (1UL << 7); Board.Instance.whitePieces &= ~(1UL << 5); }
                else if (targetSquare == (1UL << 2)) { Board.Instance.whitePieces |= (1UL << 0); Board.Instance.whitePieces &= ~(1UL << 3); }
            }
        }
        else
        {
            Board.Instance.blackPieces ^= moveMask;
            if (capturedPiece != pieceType.none) Board.Instance.whitePieces ^= targetSquare; 
            
            if (originalMovedPiece == pieceType.blackKing && startSquare == (1UL << 60))
            {
                if (targetSquare == (1UL << 62)) { Board.Instance.blackPieces |= (1UL << 63); Board.Instance.blackPieces &= ~(1UL << 61); }
                else if (targetSquare == (1UL << 58)) { Board.Instance.blackPieces |= (1UL << 56); Board.Instance.blackPieces &= ~(1UL << 59); }
            }
        }

        bool isEnPassant = false;
        int epCaptureIndex = -1;

        if (originalMovedPiece == pieceType.whitePawn && capturedPiece == pieceType.none && (targetIndex == startIndex + 7 || targetIndex == startIndex + 9))
        {
            isEnPassant = true;
            epCaptureIndex = targetIndex - 8;
        }
        else if (originalMovedPiece == pieceType.blackPawn && capturedPiece == pieceType.none && (targetIndex == startIndex - 7 || targetIndex == startIndex - 9))
        {
            isEnPassant = true;
            epCaptureIndex = targetIndex + 8;
        }

        if (isEnPassant)
        {
            pieceType epVictim = isWhiteMoving ? pieceType.blackPawn : pieceType.whitePawn;
            Board.Instance.boardSquares[epCaptureIndex] = epVictim;
            Board.Instance.pieceBitboards[(int)epVictim] |= (1UL << epCaptureIndex);
            
            if (isWhiteMoving) Board.Instance.blackPieces |= (1UL << epCaptureIndex);
            else Board.Instance.whitePieces |= (1UL << epCaptureIndex);
        }

        Board.Instance.CalculateExtraBitboards();

        // --- NEW: REWIND HISTORY ---
        historyPly--;
    }

    public void GenerateAllMoves(int ply = 0)
    {
        moveCount[ply] = 0;

        ulong knights = Board.Instance.pieceBitboards[Board.Instance.isWhiteTurn ? (int)pieceType.whiteKnight : (int)pieceType.blackKnight];
        while (knights != 0)        
        {
            ulong knight = knights & (~knights + 1);
            ulong moves = knightMoves(knight);

            while(moves != 0)
            {
                ulong move = moves & (~moves + 1);
                moveList[ply][moveCount[ply]++] = PackMove(Board.Instance.GetBitboardIndex(knight), Board.Instance.GetBitboardIndex(move), (move & (Board.Instance.isWhiteTurn ? Board.Instance.blackPieces : Board.Instance.whitePieces)) != 0 ? moveFlag.Capture : moveFlag.QuietMove);
                moves &= moves - 1;
            }
            knights &= knights - 1;
        }

        ulong rooks = Board.Instance.pieceBitboards[Board.Instance.isWhiteTurn ? (int)pieceType.whiteRook : (int)pieceType.blackRook];
        while (rooks != 0)        
        {
            ulong rook = rooks & (~rooks + 1);
            ulong moves = rookMoves(rook);

            while(moves != 0)
            {
                ulong move = moves & (~moves + 1);
                moveList[ply][moveCount[ply]++] = PackMove(Board.Instance.GetBitboardIndex(rook), Board.Instance.GetBitboardIndex(move), (move & (Board.Instance.isWhiteTurn ? Board.Instance.blackPieces : Board.Instance.whitePieces)) != 0 ? moveFlag.Capture : moveFlag.QuietMove);
                moves &= moves - 1;
            }
            rooks &= rooks - 1;
        }

        ulong bishops = Board.Instance.pieceBitboards[Board.Instance.isWhiteTurn ? (int)pieceType.whiteBishop : (int)pieceType.blackBishop];
        while (bishops != 0)        
        {
            ulong bishop = bishops & (~bishops + 1);
            ulong moves = bishopMoves(bishop);

            while(moves != 0)
            {
                ulong move = moves & (~moves + 1);
                moveList[ply][moveCount[ply]++] = PackMove(Board.Instance.GetBitboardIndex(bishop), Board.Instance.GetBitboardIndex(move), (move & (Board.Instance.isWhiteTurn ? Board.Instance.blackPieces : Board.Instance.whitePieces)) != 0 ? moveFlag.Capture : moveFlag.QuietMove);
                moves &= moves - 1;
            }
            bishops &= bishops - 1;
        }

        ulong queens = Board.Instance.pieceBitboards[Board.Instance.isWhiteTurn ? (int)pieceType.whiteQueen : (int)pieceType.blackQueen];
        while (queens != 0)        
        {
            ulong queen = queens & (~queens + 1);
            ulong moves = queenMoves(queen);

            while(moves != 0)
            {
                ulong move = moves & (~moves + 1);
                moveList[ply][moveCount[ply]++] = PackMove(Board.Instance.GetBitboardIndex(queen), Board.Instance.GetBitboardIndex(move), (move & (Board.Instance.isWhiteTurn ? Board.Instance.blackPieces : Board.Instance.whitePieces)) != 0 ? moveFlag.Capture : moveFlag.QuietMove);
                moves &= moves - 1;
            }
            queens &= queens - 1;
        }

        ulong pawns = Board.Instance.pieceBitboards[Board.Instance.isWhiteTurn ? (int)pieceType.whitePawn : (int)pieceType.blackPawn];
        ulong enemyPieces = Board.Instance.isWhiteTurn ? Board.Instance.blackPieces : Board.Instance.whitePieces;

        while (pawns != 0)
        {
            ulong pawn = pawns & (~pawns + 1);
            ulong moves = pawnMoves(pawn);
            int startIndex = Board.Instance.GetBitboardIndex(pawn);

            while(moves != 0)
            {
                ulong moveBit = moves & (~moves + 1);
                int targetIndex = Board.Instance.GetBitboardIndex(moveBit);
                
                bool isCapture = (moveBit & enemyPieces) != 0;
                bool isPromotion = Board.Instance.isWhiteTurn ? targetIndex >= 56 : targetIndex <= 7;
                int delta = Math.Abs(targetIndex - startIndex);
                
                if (isPromotion)
                {
                    moveList[ply][moveCount[ply]++] = PackMove(startIndex, targetIndex, isCapture ? moveFlag.PromoteToQueenAndCapture : moveFlag.PromoteToQueen);
                    moveList[ply][moveCount[ply]++] = PackMove(startIndex, targetIndex, isCapture ? moveFlag.PromoteToRookAndCapture : moveFlag.PromoteToRook);
                    moveList[ply][moveCount[ply]++] = PackMove(startIndex, targetIndex, isCapture ? moveFlag.PromoteToBishopAndCapture : moveFlag.PromoteToBishop);
                    moveList[ply][moveCount[ply]++] = PackMove(startIndex, targetIndex, isCapture ? moveFlag.PromoteToKnightAndCapture : moveFlag.PromoteToKnight);
                }
                else
                {
                    moveFlag flag = moveFlag.QuietMove;
                    if (isCapture) flag = moveFlag.Capture;
                    else if (delta == 16) flag = moveFlag.DoublePawn;
                    else if (delta == 7 || delta == 9) flag = moveFlag.EnPassantCapture; 

                    moveList[ply][moveCount[ply]++] = PackMove(startIndex, targetIndex, flag);
                }
                
                moves &= moves - 1;
            }
            pawns &= pawns - 1;
        }

        ulong kings = Board.Instance.pieceBitboards[Board.Instance.isWhiteTurn ? (int)pieceType.whiteKing : (int)pieceType.blackKing];
        while (kings != 0)        
        {
            ulong king = kings & (~kings + 1);
            ulong moves = kingMoves(king);

            while(moves != 0)
            {
                ulong move = moves & (~moves + 1);
                moveList[ply][moveCount[ply]++] = PackMove(Board.Instance.GetBitboardIndex(king), Board.Instance.GetBitboardIndex(move), (move & (Board.Instance.isWhiteTurn ? Board.Instance.blackPieces : Board.Instance.whitePieces)) != 0 ? moveFlag.Capture : moveFlag.QuietMove);
                moves &= moves - 1;
            }
            kings &= kings - 1;
        }
    }

    public ulong GenerateHashFromScratch()
    {
        ulong hash = 0;

        for (int i = 0; i < 64; i++)
        {
            pieceType piece = Board.Instance.boardSquares[i];
            if (piece != pieceType.none)
            {
                hash ^= Zobrist.piecesArray[(int)piece, i];
            }
        }

        hash ^= Zobrist.castlingRightsArray[castlingRights];

        int epIndex = enPassantTarget == -1 ? 64 : enPassantTarget;
        hash ^= Zobrist.enPassantArray[epIndex];

        if (!Board.Instance.isWhiteTurn)
        {
            hash ^= Zobrist.sideToMove;
        }

        return hash;
    }

    public ushort PackMove(int startSquare, int targetSquare, moveFlag flag)
    {
        return (ushort)(startSquare | (targetSquare << 6) | ((ushort)flag << 12));
    }

    public string FormatUciMove(ushort move)
    {
        int fromIndex = move & 0x3F;
        int toIndex = (move >> 6) & 0x3F;
        int flag = move >> 12;

        int fromFile = fromIndex % 8;
        int fromRank = fromIndex / 8;
        int toFile = toIndex % 8;
        int toRank = toIndex / 8;

        string uci = $"{(char)('a' + fromFile)}{fromRank + 1}{(char)('a' + toFile)}{toRank + 1}";

        // Add promotion piece character if applicable
        if (flag == 8 || flag == 12) uci += "n";
        else if (flag == 9 || flag == 13) uci += "b";
        else if (flag == 10 || flag == 14) uci += "r";
        else if (flag == 11 || flag == 15) uci += "q";

        return uci;
    }
}