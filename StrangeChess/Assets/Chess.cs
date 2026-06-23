using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

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

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
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
    
    public void pawnMoves(ulong from)
    {
        // Only: single move, double move, captures for both colors (no promotion/en-passant)
        ulong all = Board.Instance.allPieces;
        ulong white = Board.Instance.whitePieces;
        ulong black = Board.Instance.blackPieces;

        // file masks to prevent wraparound on diagonal captures
        const ulong fileA = 0x0101010101010101UL;
        const ulong fileH = 0x8080808080808080UL;
        ulong notFileA = ~fileA;
        ulong notFileH = ~fileH;

        ulong moves = 0UL;

        // White pawns move up (left capture: <<7, right capture: <<9)
        if ((from & white) != 0)
        {
            ulong leftCap = (from & notFileA) << 7;
            ulong rightCap = (from & notFileH) << 9;
            moves |= (leftCap & black);
            moves |= (rightCap & black);

            ulong one = from << 8;
            if ((one & all) == 0)
            {
                moves |= one;
                // double from rank 2
                const ulong rank2 = 0x000000000000FF00UL;
                ulong two = from << 16;
                if ((from & rank2) != 0 && (two & all) == 0)
                    moves |= two;
            }
        }
        // Black pawns move down (captures: >>7 and >>9)
        else if ((from & black) != 0)
        {
            ulong leftCap = (from & notFileH) >> 7; // from black perspective
            ulong rightCap = (from & notFileA) >> 9;
            moves |= (leftCap & white);
            moves |= (rightCap & white);

            ulong one = from >> 8;
            if ((one & all) == 0)
            {
                moves |= one;
                // double from rank 7
                const ulong rank7 = 0x00FF000000000000UL;
                ulong two = from >> 16;
                if ((from & rank7) != 0 && (two & all) == 0)
                    moves |= two;
            }
        }

        ClickDetector.Instance.availableMoves |= moves;
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

    public void kingMoves(ulong from)
    {
        int kingIndex = Board.Instance.GetBitboardIndex(from);
        if((from & Board.Instance.whitePieces) != 0)      // Selected White King
        {
            ClickDetector.Instance.availableMoves = Board.Instance.kingAttacks[kingIndex] & ~Board.Instance.whitePieces;    // Dont go on squares occupied by white pieces
            
            // KINGSIDE CASTLE
            if((castlingRights & 1) != 0 
            && isSquareSafe(1UL << 4) // e1
            && isSquareSafe(1UL << 5) // f1
            && isSquareSafe(1UL << 6) // g1
            && (Board.Instance.allPieces & (1UL << 5)) == 0 
            && (Board.Instance.allPieces & (1UL << 6)) == 0)
            {
                ClickDetector.Instance.availableMoves |= 1UL << 6;
            }
            if((castlingRights & 2) != 0 
            && isSquareSafe(1UL << 4) 
            && isSquareSafe(1UL << 3) 
            && isSquareSafe(1UL << 2)
            && (Board.Instance.allPieces & (1UL << 3)) == 0 
            && (Board.Instance.allPieces & (1UL << 2)) == 0
            && (Board.Instance.allPieces & (1UL << 1)) == 0)
            {
                ClickDetector.Instance.availableMoves |= 1UL << 2;
            }
        }
        else if((from & Board.Instance.blackPieces) != 0) // Selected Black King
        {
            ClickDetector.Instance.availableMoves = Board.Instance.kingAttacks[kingIndex] & ~Board.Instance.blackPieces;    
            
            // KINGSIDE CASTLE
            if((castlingRights & 4) != 0 
            && isSquareSafe(1UL << 60) // e8
            && isSquareSafe(1UL << 61) // f8
            && isSquareSafe(1UL << 62) // g8
            && (Board.Instance.allPieces & (1UL << 61)) == 0 
            && (Board.Instance.allPieces & (1UL << 62)) == 0)
            {
                ClickDetector.Instance.availableMoves |= 1UL << 62;
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
                ClickDetector.Instance.availableMoves |= 1UL << 58;
            }
        }
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
        pieceType movingPiece = Board.Instance.bitboardToPiece(startSquare);
        pieceType capturedPiece = Board.Instance.bitboardToPiece(targetSquare);

        if(capturedPiece != pieceType.none)        
            Board.Instance.pieceBitboards[(int)capturedPiece] &= ~targetSquare;      
            
        Board.Instance.pieceBitboards[(int)movingPiece] |= targetSquare;     
        Board.Instance.pieceBitboards[(int)movingPiece] &= ~startSquare;  

        if(movingPiece == pieceType.whiteKing && targetSquare == 1Ul << 6)
        {
            movePiece(1Ul << 7, 1Ul << 5);
        }
        else if(movingPiece == pieceType.whiteKing && targetSquare == 1Ul << 2)
        {
            movePiece(1Ul << 0, 1Ul << 3);
        }
        else if(movingPiece == pieceType.blackKing && targetSquare == 1UL << 62) // Black Kingside
        {
            movePiece(1UL << 63, 1UL << 61); // Move h8 rook to f8
        }
        else if(movingPiece == pieceType.blackKing && targetSquare == 1UL << 58) // Black Queenside
        {
            movePiece(1UL << 56, 1UL << 59); // Move a8 rook to d8
        }
        // just castling rights cheecks
        if (((startSquare | targetSquare) & castlingAllChecks) != 0)
        {
            if (startSquare == wKing)
            {
                castlingRights &= 12;
            }
            else if (startSquare == bKing)
            {
                castlingRights &= 3;
            }
            if (startSquare == waRook || targetSquare == waRook)
            {
                castlingRights &= 13;
            }
            if (startSquare == whRook || targetSquare == whRook)
            {
                castlingRights &= 14;
            }
            if (startSquare == baRook || targetSquare == baRook)
            {
                castlingRights &= 7;
            }
            if (startSquare == bhRook || targetSquare == bhRook)
            {
                castlingRights &= 11;
            }
        }
        Board.Instance.CalculateExtraBitboards();
    }

    public ulong checkLegalMoves(ulong startSquare, ulong moves)
    {
        ulong legalMoves = 0;
        while (moves != 0)
        {
            ulong targetSquare = moves & ~(moves - 1);
            pieceType capturedPiece = Board.Instance.bitboardToPiece(targetSquare);

            int castlingRightsTemp = castlingRights;
            movePiece(startSquare, targetSquare);
            
            ulong SquareToCheck = ClickDetector.Instance.isWhiteTurn ? Board.Instance.pieceBitboards[(int)pieceType.whiteKing] : Board.Instance.pieceBitboards[(int)pieceType.blackKing];
            if(isSquareSafe(SquareToCheck))
                legalMoves |= targetSquare;
                
            unmakeMove(startSquare, targetSquare, capturedPiece);
            castlingRights = castlingRightsTemp;

            moves &= moves - 1;
        }
        return legalMoves;
    }

    private bool isSquareSafe(ulong king)
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

    private void unmakeMove(ulong startSquare, ulong targetSquare, pieceType capturedPiece)
    {
        pieceType movedPiece = Board.Instance.bitboardToPiece(targetSquare);

        // 1. Move the piece back from targetSquare to startSquare
        Board.Instance.pieceBitboards[(int)movedPiece] |= startSquare;
        Board.Instance.pieceBitboards[(int)movedPiece] &= ~targetSquare;
        
        // 2. Undo Castling Rooks!
        if (movedPiece == pieceType.whiteKing && targetSquare == (1UL << 6)) // Kingside
        {
            // Move Rook from f1 back to h1
            Board.Instance.pieceBitboards[(int)pieceType.whiteRook] |= (1UL << 7);
            Board.Instance.pieceBitboards[(int)pieceType.whiteRook] &= ~(1UL << 5);
        }
        else if (movedPiece == pieceType.whiteKing && targetSquare == (1UL << 2)) // Queenside
        {
            // Move Rook from d1 back to a1
            Board.Instance.pieceBitboards[(int)pieceType.whiteRook] |= (1UL << 0);
            Board.Instance.pieceBitboards[(int)pieceType.whiteRook] &= ~(1UL << 3);
        }
        else if (movedPiece == pieceType.blackKing && targetSquare == (1UL << 62)) // Black Kingside
        {
            // Move Rook from f8 back to h8
            Board.Instance.pieceBitboards[(int)pieceType.blackRook] |= (1UL << 63);
            Board.Instance.pieceBitboards[(int)pieceType.blackRook] &= ~(1UL << 61);
        }
        else if (movedPiece == pieceType.blackKing && targetSquare == (1UL << 58)) // Black Queenside
        {
            // Move Rook from d8 back to a8
            Board.Instance.pieceBitboards[(int)pieceType.blackRook] |= (1UL << 56);
            Board.Instance.pieceBitboards[(int)pieceType.blackRook] &= ~(1UL << 59);
        }
        if(capturedPiece != pieceType.none)
            Board.Instance.pieceBitboards[(int)capturedPiece] |= targetSquare;

        Board.Instance.CalculateExtraBitboards();
    }
}