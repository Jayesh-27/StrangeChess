using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;

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

public class Chess : MonoBehaviour
{
    public static Chess Instance;
    public ulong whiteAttacks = 280375465082880;
    public ulong blackAttacks = 16711680;
    public int castlingRights = 15;
    ulong wKing  = 1UL << 4;
    ulong bKing  = 1UL << 60;
    ulong waRook = 1UL << 0;
    ulong whRook = 1UL << 7;
    ulong baRook = 1UL << 56;
    ulong bhRook = 1UL << 63;
    ulong castlingAllChecks = 0x9100000000000091;    

    public const int MAX_DEPTH = 64; // The maximum number of turns the AI can look ahead
    public ushort[][] moveList;
    public int[][] moveScores;
    public int[] moveCount = new int[MAX_DEPTH];

    public int enPassantTarget = -1;
    public int halfmoveClock = 0;
    public int fullmoveNumber = 1;

    // zobirst hashing
    public ulong currentZobristKey = 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        // Pre-allocate the memory for all possible search depths
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

        if(ClickDetector.Instance.isWhiteTurn)
        {
            leftCap = (from & notFileH) >> 7; // from black perspective
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
        
        // 1. Create the En Passant bitboard
        ulong epSquare = (enPassantTarget != -1) ? (1UL << enPassantTarget) : 0UL;

        if ((from & white) != 0)
        {
            ulong leftCap = (from & notFileA) << 7;
            ulong rightCap = (from & notFileH) << 9;
            
            // 2. Allow captures on enemy pieces OR the empty EP square
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
            
            // 2. Allow captures on enemy pieces OR the empty EP square
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
        if((from & Board.Instance.whitePieces) != 0)      // Selected White Knight
        {
            moves = Board.Instance.knightAttacks[knightIndex] & ~Board.Instance.whitePieces;    // Dont go on squares occupied by white pieces
        }
        else if((from & Board.Instance.blackPieces) != 0) // Selected Black Knight
        {
            moves = Board.Instance.knightAttacks[knightIndex] & ~Board.Instance.blackPieces;    // Dont go on squares occupied by black pieces
        }
        return moves;
    }

    public ulong kingMoves(ulong from)
    {
        int kingIndex = Board.Instance.GetBitboardIndex(from);
        ulong moves = 0;
        if((from & Board.Instance.whitePieces) != 0)      // Selected White King
        {
            moves = Board.Instance.kingAttacks[kingIndex] & ~Board.Instance.whitePieces;    // Dont go on squares occupied by white pieces
            
            // KINGSIDE CASTLE
            if((castlingRights & 1) != 0 
            && isSquareSafe(1UL << 4) // e1
            && isSquareSafe(1UL << 5) // f1
            && isSquareSafe(1UL << 6) // g1
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
        else if((from & Board.Instance.blackPieces) != 0) // Selected Black King
        {
            moves = Board.Instance.kingAttacks[kingIndex] & ~Board.Instance.blackPieces;    
            
            // KINGSIDE CASTLE
            if((castlingRights & 4) != 0 
            && isSquareSafe(1UL << 60) // e8
            && isSquareSafe(1UL << 61) // f8
            && isSquareSafe(1UL << 62) // g8
            && (Board.Instance.allPieces & (1UL << 61)) == 0 
            && (Board.Instance.allPieces & (1UL << 62)) == 0)
            {
                moves |= 1UL << 62; // Correctly add to the engine's move mask
            }

            // QUEENSIDE CASTLE
            if((castlingRights & 8) != 0 
            && isSquareSafe(1UL << 60) // e8
            && isSquareSafe(1UL << 59) // d8
            && isSquareSafe(1UL << 58) // c8
            && (Board.Instance.allPieces & (1UL << 59)) == 0 
            && (Board.Instance.allPieces & (1UL << 58)) == 0
            && (Board.Instance.allPieces & (1UL << 57)) == 0) // b8 (must be empty, but doesn't need to be safe)
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

        if(ClickDetector.Instance.isWhiteTurn)
            attacks &= ~Board.Instance.whitePieces;
        else
            attacks &= ~Board.Instance.blackPieces;
        return attacks;
    }

    public ulong rookMoves(ulong from, ulong simAll = 0)
    {
        int rookIndex = Board.Instance.GetBitboardIndex(from);
        
        // 1. Get current board occupancy
        ulong allPieces = simAll != 0 ? simAll : Board.Instance.allPieces;

        // 2. Identify the blockerMask relevant to this specific rook's position/sqaure
        ulong blockers = allPieces & Board.Instance.rookBlockersMasks[rookIndex];

        // 3. Get index using the magic                     
        int magicIndex = (int)((blockers * Board.rookMagics[rookIndex]) >> (64 - Board.rookBlockerBitCounts[rookIndex]));

        // 4. Retrieve the instantly generated O(1) pseudo-legal attack map
        ulong attacks = Board.Instance.rookAttackTable[rookIndex][magicIndex];

        // 5. Remove friendly pieces (we can't capture our own color)
        // using & ~ instead of ^. Assuming White to move!
        if(ClickDetector.Instance.isWhiteTurn)
            attacks &= ~Board.Instance.whitePieces;
        else
            attacks &= ~Board.Instance.blackPieces;

        return attacks;
    }

    public ulong queenMoves(ulong from, ulong simAll = 0)
    {
        int index = Board.Instance.GetBitboardIndex(from);
        
        ulong allPieces = simAll != 0 ? simAll : Board.Instance.allPieces;

        // Calculate Rook-like attacks (Horizontal & Vertical)
        ulong rBlockers = allPieces & Board.Instance.rookBlockersMasks[index];
        int rMagicIndex = (int)((rBlockers * Board.rookMagics[index]) >> (64 - Board.rookBlockerBitCounts[index]));
        ulong rookAttacks = Board.Instance.rookAttackTable[index][rMagicIndex];

        // Calculate Bishop-like attacks (Diagonals)
        ulong bBlockers = allPieces & Board.Instance.bishopBlockersMasks[index];
        int bMagicIndex = (int)((bBlockers * Board.bishopMagics[index]) >> (64 - Board.bishopBlockerBitCounts[index]));
        ulong bishopAttacks = Board.Instance.bishopAttackTable[index][bMagicIndex];

        // The Queen's moves are a combination of both
        ulong attacks = rookAttacks | bishopAttacks;

        // Remove friendly pieces (so we don't capture our own)
        
        if(ClickDetector.Instance.isWhiteTurn)
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

        // --- 1. PROMOTION DETECTION (Auto-Queen) ---
        if (originalPiece == pieceType.whitePawn && targetIndex >= 56) movingPiece = pieceType.whiteQueen;
        else if (originalPiece == pieceType.blackPawn && targetIndex <= 7) movingPiece = pieceType.blackQueen;

        // --- 2. EN PASSANT DETECTION ---
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

        // ==========================================
        // --- ZOBRIST: ERASING THE OLD STATE ---
        // ==========================================
        
        // A. Erase the old Castling Rights and old En Passant target from the hash
        currentZobristKey ^= Zobrist.castlingRightsArray[castlingRights];
        int oldEpIndex = enPassantTarget == -1 ? 64 : enPassantTarget;
        currentZobristKey ^= Zobrist.enPassantArray[oldEpIndex];

        // B. Erase the moving piece from its starting square
        currentZobristKey ^= Zobrist.piecesArray[(int)originalPiece, startIndex];
        
        // C. Erase the captured piece (if any) from the target square
        if (capturedPiece != pieceType.none)
        {
            currentZobristKey ^= Zobrist.piecesArray[(int)capturedPiece, targetIndex];
        }

        // D. Erase the victim pawn if this was an En Passant capture
        if (isEnPassant)
        {
            pieceType epVictim = isWhiteMoving ? pieceType.blackPawn : pieceType.whitePawn;
            currentZobristKey ^= Zobrist.piecesArray[(int)epVictim, epCaptureIndex];
        }

        // --- 3. STANDARD CAPTURE UPDATE ---
        if(capturedPiece != pieceType.none)
        {
            Board.Instance.pieceBitboards[(int)capturedPiece] &= ~targetSquare;      
        }
        
        // --- 4. ARRAY & BITBOARD UPDATES ---
        Board.Instance.boardSquares[targetIndex] = movingPiece; 
        Board.Instance.boardSquares[startIndex] = pieceType.none; 

        Board.Instance.pieceBitboards[(int)originalPiece] &= ~startSquare; 
        Board.Instance.pieceBitboards[(int)movingPiece] |= targetSquare;   

        // ==========================================
        // --- ZOBRIST: WRITING NEW PIECE POSITIONS ---
        // ==========================================
        
        // E. Add the moving piece (or promoted piece) to its new target square
        currentZobristKey ^= Zobrist.piecesArray[(int)movingPiece, targetIndex];

        // --- 5. EN PASSANT EXECUTION ---
        if (isEnPassant)
        {
            pieceType epVictim = isWhiteMoving ? pieceType.blackPawn : pieceType.whitePawn;
            Board.Instance.boardSquares[epCaptureIndex] = pieceType.none;
            Board.Instance.pieceBitboards[(int)epVictim] &= ~(1UL << epCaptureIndex);
        }

        // --- 6. CASTLING EXECUTION (REWRITTEN: NO RECURSION) ---
        if (originalPiece == pieceType.whiteKing && startSquare == (1UL << 4))
        {
            if (targetSquare == (1UL << 6)) // Kingside
            {
                Board.Instance.boardSquares[7] = pieceType.none;
                Board.Instance.boardSquares[5] = pieceType.whiteRook;
                Board.Instance.pieceBitboards[(int)pieceType.whiteRook] &= ~(1UL << 7);
                Board.Instance.pieceBitboards[(int)pieceType.whiteRook] |= (1UL << 5);
                
                currentZobristKey ^= Zobrist.piecesArray[(int)pieceType.whiteRook, 7]; // Remove Rook from h1
                currentZobristKey ^= Zobrist.piecesArray[(int)pieceType.whiteRook, 5]; // Add Rook to f1
            }
            else if (targetSquare == (1UL << 2)) // Queenside
            {
                Board.Instance.boardSquares[0] = pieceType.none;
                Board.Instance.boardSquares[3] = pieceType.whiteRook;
                Board.Instance.pieceBitboards[(int)pieceType.whiteRook] &= ~(1UL << 0);
                Board.Instance.pieceBitboards[(int)pieceType.whiteRook] |= (1UL << 3);

                currentZobristKey ^= Zobrist.piecesArray[(int)pieceType.whiteRook, 0]; // Remove Rook from a1
                currentZobristKey ^= Zobrist.piecesArray[(int)pieceType.whiteRook, 3]; // Add Rook to d1
            }
        }
        else if (originalPiece == pieceType.blackKing && startSquare == (1UL << 60))
        {
            if (targetSquare == (1UL << 62)) // Kingside
            {
                Board.Instance.boardSquares[63] = pieceType.none;
                Board.Instance.boardSquares[61] = pieceType.blackRook;
                Board.Instance.pieceBitboards[(int)pieceType.blackRook] &= ~(1UL << 63);
                Board.Instance.pieceBitboards[(int)pieceType.blackRook] |= (1UL << 61);

                currentZobristKey ^= Zobrist.piecesArray[(int)pieceType.blackRook, 63]; // Remove Rook from h8
                currentZobristKey ^= Zobrist.piecesArray[(int)pieceType.blackRook, 61]; // Add Rook to f8
            }
            else if (targetSquare == (1UL << 58)) // Queenside
            {
                Board.Instance.boardSquares[56] = pieceType.none;
                Board.Instance.boardSquares[59] = pieceType.blackRook;
                Board.Instance.pieceBitboards[(int)pieceType.blackRook] &= ~(1UL << 56);
                Board.Instance.pieceBitboards[(int)pieceType.blackRook] |= (1UL << 59);

                currentZobristKey ^= Zobrist.piecesArray[(int)pieceType.blackRook, 56]; // Remove Rook from a8
                currentZobristKey ^= Zobrist.piecesArray[(int)pieceType.blackRook, 59]; // Add Rook to d8
            }
        }

        // Castling rights checks
        if (((startSquare | targetSquare) & castlingAllChecks) != 0)
        {
            if (startSquare == wKing) castlingRights &= 12;
            else if (startSquare == bKing) castlingRights &= 3;
            if (startSquare == waRook || targetSquare == waRook) castlingRights &= 13;
            if (startSquare == whRook || targetSquare == whRook) castlingRights &= 14;
            if (startSquare == baRook || targetSquare == baRook) castlingRights &= 7;
            if (startSquare == bhRook || targetSquare == bhRook) castlingRights &= 11;
        }

        // --- 7. UPDATE EN PASSANT TARGET STATE ---
        if ((originalPiece == pieceType.whitePawn || originalPiece == pieceType.blackPawn) && Math.Abs(targetIndex - startIndex) == 16)
            enPassantTarget = isWhiteMoving ? startIndex + 8 : startIndex - 8;
        else
            enPassantTarget = -1; 

        // ==========================================
        // --- ZOBRIST: WRITING THE NEW STATE ---
        // ==========================================
        
        // F. Add the NEW Castling Rights and En Passant target back into the hash
        currentZobristKey ^= Zobrist.castlingRightsArray[castlingRights];
        int newEpIndex = enPassantTarget == -1 ? 64 : enPassantTarget;
        currentZobristKey ^= Zobrist.enPassantArray[newEpIndex];

        // G. Swap the turn inside the hash
        currentZobristKey ^= Zobrist.sideToMove;

        // --- 8. INCREMENTAL EXTRA BITBOARDS ---
        ulong moveMask = startSquare | targetSquare;
        if (isWhiteMoving)
        {
            Board.Instance.whitePieces ^= moveMask;
            if (capturedPiece != pieceType.none) Board.Instance.blackPieces ^= targetSquare;
            if (isEnPassant) Board.Instance.blackPieces &= ~(1UL << epCaptureIndex);
            
            // Castling Extra Bitboard Logic
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
            
            // Castling Extra Bitboard Logic
            if (originalPiece == pieceType.blackKing && startSquare == (1UL << 60))
            {
                if (targetSquare == (1UL << 62)) { Board.Instance.blackPieces &= ~(1UL << 63); Board.Instance.blackPieces |= (1UL << 61); }
                else if (targetSquare == (1UL << 58)) { Board.Instance.blackPieces &= ~(1UL << 56); Board.Instance.blackPieces |= (1UL << 59); }
            }
        }        
        
        Board.Instance.CalculateExtraBitboards();
    }

    public ulong checkLegalMoves(ulong startSquare, ulong moves)
    {
        ulong legalMoves = 0;
        
        // Capture the piece BEFORE the loop starts playing moves
        pieceType originalPiece = Board.Instance.boardSquares[Board.Instance.GetBitboardIndex(startSquare)];

        while (moves != 0)
        {
            ulong targetSquare = moves & ~(moves - 1);
            pieceType capturedPiece = Board.Instance.bitboardToPiece(targetSquare);

            int castlingRightsTemp = castlingRights;
            int enPassantTargetTemp = enPassantTarget; 
            ulong savedHash = currentZobristKey; // <-- 1. TAKE SNAPSHOT

            movePiece(startSquare, targetSquare);
            
            ulong SquareToCheck = ClickDetector.Instance.isWhiteTurn ? Board.Instance.pieceBitboards[(int)pieceType.whiteKing] : Board.Instance.pieceBitboards[(int)pieceType.blackKing];
            if(isSquareSafe(SquareToCheck))
                legalMoves |= targetSquare;
                
            unmakeMove(startSquare, targetSquare, capturedPiece, originalPiece); 
            castlingRights = castlingRightsTemp;
            enPassantTarget = enPassantTargetTemp; 
            currentZobristKey = savedHash; // <-- 2. RESTORE SNAPSHOT

            moves &= moves - 1;
        }
        return legalMoves;
    }

    public bool isSquareSafe(ulong king)
    {
        bool isWhiteTurn = ClickDetector.Instance.isWhiteTurn;
        //king = isWhiteTurn ? Board.Instance.pieceBitboards[(int)pieceType.whiteKing] : Board.Instance.pieceBitboards[(int)pieceType.blackKing];
        int kingIndex = Board.Instance.GetBitboardIndex(king);

        // 1. Get enemy bitboards
        ulong enemyKnights = isWhiteTurn ? Board.Instance.pieceBitboards[(int)pieceType.blackKnight] : Board.Instance.pieceBitboards[(int)pieceType.whiteKnight];
        ulong enemyRooks   = isWhiteTurn ? Board.Instance.pieceBitboards[(int)pieceType.blackRook]   : Board.Instance.pieceBitboards[(int)pieceType.whiteRook];
        ulong enemyBishops = isWhiteTurn ? Board.Instance.pieceBitboards[(int)pieceType.blackBishop] : Board.Instance.pieceBitboards[(int)pieceType.whiteBishop];
        ulong enemyQueens  = isWhiteTurn ? Board.Instance.pieceBitboards[(int)pieceType.blackQueen]  : Board.Instance.pieceBitboards[(int)pieceType.whiteQueen];
        ulong enemyPawns   = isWhiteTurn ? Board.Instance.pieceBitboards[(int)pieceType.blackPawn]   : Board.Instance.pieceBitboards[(int)pieceType.whitePawn];
        ulong enemyKing    = isWhiteTurn ? Board.Instance.pieceBitboards[(int)pieceType.blackKing]   : Board.Instance.pieceBitboards[(int)pieceType.whiteKing];

        // 2. Check Knight Attacks
        ulong knightAttacksFromKing = Board.Instance.knightAttacks[kingIndex];
        if ((knightAttacksFromKing & enemyKnights) != 0) return false;

        // 3. Check Rook & Queen Attacks (Horizontal & Vertical)
        ulong rBlockers = Board.Instance.allPieces & Board.Instance.rookBlockersMasks[kingIndex];
        int rMagicIndex = (int)((rBlockers * Board.rookMagics[kingIndex]) >> (64 - Board.rookBlockerBitCounts[kingIndex]));
        ulong rookAttacksFromKing = Board.Instance.rookAttackTable[kingIndex][rMagicIndex];        
        
        if ((rookAttacksFromKing & (enemyRooks | enemyQueens)) != 0) return false;

        // 4. Check Bishop & Queen Attacks (Diagonals)
        ulong bBlockers = Board.Instance.allPieces & Board.Instance.bishopBlockersMasks[kingIndex];
        int bMagicIndex = (int)((bBlockers * Board.bishopMagics[kingIndex]) >> (64 - Board.bishopBlockerBitCounts[kingIndex]));
        ulong bishopAttacksFromKing = Board.Instance.bishopAttackTable[kingIndex][bMagicIndex];        
        
        if ((bishopAttacksFromKing & (enemyBishops | enemyQueens)) != 0) return false;

        // 5. Check Pawn Attacks
        // We pretend the King is a Pawn. If a White King attacks a Black Pawn, it means the Black Pawn is attacking the King.
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

        // 6. Check Enemy King Attacks (prevents Kings from standing next to each other)
        ulong kingAttacksFromKing = Board.Instance.kingAttacks[kingIndex];
        if ((kingAttacksFromKing & enemyKing) != 0) return false;

        return true; 
    }

    public void unmakeMove(ulong startSquare, ulong targetSquare, pieceType capturedPiece, pieceType originalMovedPiece)
    {
        int startIndex = Board.Instance.GetBitboardIndex(startSquare);
        int targetIndex = Board.Instance.GetBitboardIndex(targetSquare);
        
        pieceType movedPiece = Board.Instance.boardSquares[targetIndex];

        // 2. Move the bitboard piece back
        Board.Instance.pieceBitboards[(int)movedPiece] &= ~targetSquare;
        
        // 3. Move the array piece back
        Board.Instance.pieceBitboards[(int)originalMovedPiece] |= startSquare;
        Board.Instance.boardSquares[startIndex] = originalMovedPiece;
        
        // 4. Restore the captured piece exactly where it was
        Board.Instance.boardSquares[targetIndex] = capturedPiece; 
        if (capturedPiece != pieceType.none)
        {
            Board.Instance.pieceBitboards[(int)capturedPiece] |= targetSquare;
        }

        // 5. Undo Castling Rooks (REWRITTEN: NO RECURSION)
        if (originalMovedPiece == pieceType.whiteKing && startSquare == (1UL << 4)) 
        {
            if (targetSquare == (1UL << 6)) // White Kingside
            {
                Board.Instance.boardSquares[7] = pieceType.whiteRook; // h1
                Board.Instance.boardSquares[5] = pieceType.none;      // f1
                Board.Instance.pieceBitboards[(int)pieceType.whiteRook] |= (1UL << 7);
                Board.Instance.pieceBitboards[(int)pieceType.whiteRook] &= ~(1UL << 5);
            }
            else if (targetSquare == (1UL << 2)) // White Queenside
            {
                Board.Instance.boardSquares[0] = pieceType.whiteRook; // a1
                Board.Instance.boardSquares[3] = pieceType.none;      // d1
                Board.Instance.pieceBitboards[(int)pieceType.whiteRook] |= (1UL << 0);
                Board.Instance.pieceBitboards[(int)pieceType.whiteRook] &= ~(1UL << 3);
            }
        }
        else if (originalMovedPiece == pieceType.blackKing && startSquare == (1UL << 60)) 
        {
            if (targetSquare == (1UL << 62)) // Black Kingside
            {
                Board.Instance.boardSquares[63] = pieceType.blackRook; // h8
                Board.Instance.boardSquares[61] = pieceType.none;      // f8
                Board.Instance.pieceBitboards[(int)pieceType.blackRook] |= (1UL << 63);
                Board.Instance.pieceBitboards[(int)pieceType.blackRook] &= ~(1UL << 61);
            }
            else if (targetSquare == (1UL << 58)) // Black Queenside
            {
                Board.Instance.boardSquares[56] = pieceType.blackRook; // a8
                Board.Instance.boardSquares[59] = pieceType.none;      // d8
                Board.Instance.pieceBitboards[(int)pieceType.blackRook] |= (1UL << 56);
                Board.Instance.pieceBitboards[(int)pieceType.blackRook] &= ~(1UL << 59);
            }
        }

        // --- INCREMENTAL BITBOARD REVERSAL ---
        ulong moveMask = startSquare | targetSquare;
        bool isWhiteMoving = originalMovedPiece >= pieceType.whitePawn && originalMovedPiece <= pieceType.whiteKing;
        
        if (isWhiteMoving)
        {
            Board.Instance.whitePieces ^= moveMask;
            if (capturedPiece != pieceType.none) Board.Instance.blackPieces ^= targetSquare; 
            
            // Castling Extra Bitboard Reversal
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
            
            // Castling Extra Bitboard Reversal
            if (originalMovedPiece == pieceType.blackKing && startSquare == (1UL << 60))
            {
                if (targetSquare == (1UL << 62)) { Board.Instance.blackPieces |= (1UL << 63); Board.Instance.blackPieces &= ~(1UL << 61); }
                else if (targetSquare == (1UL << 58)) { Board.Instance.blackPieces |= (1UL << 56); Board.Instance.blackPieces &= ~(1UL << 59); }
            }
        }

        // --- EN PASSANT RESTORATION ---
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
    }

    public void GenerateAllMoves(int ply = 0)
    {
        // Reset the counter for this specific depth layer
        moveCount[ply] = 0;

        ulong knights = Board.Instance.pieceBitboards[ClickDetector.Instance.isWhiteTurn ? (int)pieceType.whiteKnight : (int)pieceType.blackKnight];
        while (knights != 0)        // for all knights on board
        {
            ulong knight = knights & (~knights + 1);
            ulong moves = knightMoves(knight);

            while(moves != 0)
            {
                ulong move = moves & (~moves + 1);
                moveList[ply][moveCount[ply]++] = PackMove(Board.Instance.GetBitboardIndex(knight), Board.Instance.GetBitboardIndex(move), (move & (ClickDetector.Instance.isWhiteTurn ? Board.Instance.blackPieces : Board.Instance.whitePieces)) != 0 ? moveFlag.Capture : moveFlag.QuietMove);
                moves &= moves - 1;
            }
            knights &= knights - 1;
        }

        ulong rooks = Board.Instance.pieceBitboards[ClickDetector.Instance.isWhiteTurn ? (int)pieceType.whiteRook : (int)pieceType.blackRook];
        while (rooks != 0)        // for all rooks on board
        {
            ulong rook = rooks & (~rooks + 1);
            ulong moves = rookMoves(rook);

            while(moves != 0)
            {
                ulong move = moves & (~moves + 1);
                moveList[ply][moveCount[ply]++] = PackMove(Board.Instance.GetBitboardIndex(rook), Board.Instance.GetBitboardIndex(move), (move & (ClickDetector.Instance.isWhiteTurn ? Board.Instance.blackPieces : Board.Instance.whitePieces)) != 0 ? moveFlag.Capture : moveFlag.QuietMove);
                moves &= moves - 1;
            }
            rooks &= rooks - 1;
        }

        ulong bishops = Board.Instance.pieceBitboards[ClickDetector.Instance.isWhiteTurn ? (int)pieceType.whiteBishop : (int)pieceType.blackBishop];
        while (bishops != 0)        // for all bishops on board
        {
            ulong bishop = bishops & (~bishops + 1);
            ulong moves = bishopMoves(bishop);

            while(moves != 0)
            {
                ulong move = moves & (~moves + 1);
                moveList[ply][moveCount[ply]++] = PackMove(Board.Instance.GetBitboardIndex(bishop), Board.Instance.GetBitboardIndex(move), (move & (ClickDetector.Instance.isWhiteTurn ? Board.Instance.blackPieces : Board.Instance.whitePieces)) != 0 ? moveFlag.Capture : moveFlag.QuietMove);
                moves &= moves - 1;
            }
            bishops &= bishops - 1;
        }

        ulong queens = Board.Instance.pieceBitboards[ClickDetector.Instance.isWhiteTurn ? (int)pieceType.whiteQueen : (int)pieceType.blackQueen];
        while (queens != 0)        // for all queens on board
        {
            ulong queen = queens & (~queens + 1);
            ulong moves = queenMoves(queen);

            while(moves != 0)
            {
                ulong move = moves & (~moves + 1);
                moveList[ply][moveCount[ply]++] = PackMove(Board.Instance.GetBitboardIndex(queen), Board.Instance.GetBitboardIndex(move), (move & (ClickDetector.Instance.isWhiteTurn ? Board.Instance.blackPieces : Board.Instance.whitePieces)) != 0 ? moveFlag.Capture : moveFlag.QuietMove);
                moves &= moves - 1;
            }
            queens &= queens - 1;
        }

        ulong pawns = Board.Instance.pieceBitboards[ClickDetector.Instance.isWhiteTurn ? (int)pieceType.whitePawn : (int)pieceType.blackPawn];
        ulong enemyPieces = ClickDetector.Instance.isWhiteTurn ? Board.Instance.blackPieces : Board.Instance.whitePieces;

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
                bool isPromotion = ClickDetector.Instance.isWhiteTurn ? targetIndex >= 56 : targetIndex <= 7;
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
                    else if (delta == 7 || delta == 9) flag = moveFlag.EnPassantCapture; // Diagonal move onto empty bit

                    moveList[ply][moveCount[ply]++] = PackMove(startIndex, targetIndex, flag);
                }
                
                moves &= moves - 1;
            }
            pawns &= pawns - 1;
        }

        ulong kings = Board.Instance.pieceBitboards[ClickDetector.Instance.isWhiteTurn ? (int)pieceType.whiteKing : (int)pieceType.blackKing];
        while (kings != 0)        // for all kings on board
        {
            ulong king = kings & (~kings + 1);
            ulong moves = kingMoves(king);

            while(moves != 0)
            {
                ulong move = moves & (~moves + 1);
                moveList[ply][moveCount[ply]++] = PackMove(Board.Instance.GetBitboardIndex(king), Board.Instance.GetBitboardIndex(move), (move & (ClickDetector.Instance.isWhiteTurn ? Board.Instance.blackPieces : Board.Instance.whitePieces)) != 0 ? moveFlag.Capture : moveFlag.QuietMove);
                moves &= moves - 1;
            }
            kings &= kings - 1;
        }
    }

    public ulong GenerateHashFromScratch()
    {
        ulong hash = 0;

        // 1. Pieces
        for (int i = 0; i < 64; i++)
        {
            pieceType piece = Board.Instance.boardSquares[i];
            if (piece != pieceType.none)
            {
                hash ^= Zobrist.piecesArray[(int)piece, i];
            }
        }

        // 2. Castling Rights (Using your existing castlingRights int)
        hash ^= Zobrist.castlingRightsArray[castlingRights];

        // 3. En Passant
        int epIndex = enPassantTarget == -1 ? 64 : enPassantTarget;
        hash ^= Zobrist.enPassantArray[epIndex];

        // 4. Side to Move
        if (!ClickDetector.Instance.isWhiteTurn)
        {
            hash ^= Zobrist.sideToMove;
        }

        return hash;
    }

    public ushort PackMove(int startSquare, int targetSquare, moveFlag flag)
    {
        return (ushort)(startSquare | (targetSquare << 6) | ((ushort)flag << 12));
    }
}