using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public class Chess : MonoBehaviour
{
    public static Chess Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
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

    public void knightMoves(ulong from)
    {
        int knightIndex = Board.Instance.GetBitboardIndex(from);
        if((from & Board.Instance.whitePieces) != 0)      // Selected White Knight
        {
            ClickDetector.Instance.availableMoves = Board.Instance.knightAttacks[knightIndex] & ~Board.Instance.whitePieces;    // Dont go on squares occupied by white pieces
        }
        else if((from & Board.Instance.blackPieces) != 0) // Selected Black Knight
        {
            ClickDetector.Instance.availableMoves = Board.Instance.knightAttacks[knightIndex] & ~Board.Instance.blackPieces;    // Dont go on squares occupied by black pieces
        }
    }

    public void kingMoves(ulong from)
    {
        // TODO Castle
        int kingIndex = Board.Instance.GetBitboardIndex(from);
        if((from & Board.Instance.whitePieces) != 0)      // Selected White King
        {
            ClickDetector.Instance.availableMoves = Board.Instance.kingAttacks[kingIndex] & ~Board.Instance.whitePieces;    // Dont go on squares occupied by white pieces
        }
        else if((from & Board.Instance.blackPieces) != 0) // Selected Black King
        {
            ClickDetector.Instance.availableMoves = Board.Instance.kingAttacks[kingIndex] & ~Board.Instance.blackPieces;    // Dont go on squares occupied by black pieces
        }
    }

    public void bishopMoves(ulong from)
    {
        int bishopIndex = Board.Instance.GetBitboardIndex(from);
        
        ulong allPieces = Board.Instance.allPieces;

        ulong blockers = allPieces & Board.Instance.bishopBlockersMasks[bishopIndex];

        int magicIndex = (int)((blockers * Board.bishopMagics[bishopIndex]) >> (64 - Board.bishopBlockerBitCounts[bishopIndex]));

        ulong attacks = Board.Instance.bishopAttackTable[bishopIndex][magicIndex];

        attacks &= ~Board.Instance.whitePieces;

        ClickDetector.Instance.availableMoves = attacks;
    }

    public void rookMoves(ulong from)
    {
        int rookIndex = Board.Instance.GetBitboardIndex(from);
        
        // 1. Get current board occupancy
        ulong allPieces = Board.Instance.allPieces;

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

        ClickDetector.Instance.availableMoves = attacks;
    }

    public void queenMoves(ulong from)
    {
        int index = Board.Instance.GetBitboardIndex(from);
        
        ulong allPieces = Board.Instance.allPieces;

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

        ClickDetector.Instance.availableMoves = attacks;
    }

    public void movePiece(ulong from, ulong to)
    {
        pieceType piece = Board.Instance.bitboardToPiece(from);

        if(piece == 0)
            return;
        if(piece < pieceType.blackPawn)
        {
            pieceType toPiece = Board.Instance.bitboardToPiece(to);
            if(toPiece >= pieceType.blackPawn)
            {
                Board.Instance.pieceBitboards[(int)toPiece] = Board.Instance.pieceBitboards[(int)toPiece] ^ to;     // Removing Captured Piece
            }
        }
        else if(piece > pieceType.whiteKing && piece != pieceType.none)
        {
            pieceType toPiece = Board.Instance.bitboardToPiece(to);
            if(toPiece <= pieceType.whiteKing)
            {
                Board.Instance.pieceBitboards[(int)toPiece] = Board.Instance.pieceBitboards[(int)toPiece] ^ to;     // Removing Captured Piece
            }
        }
        Board.Instance.pieceBitboards[(int)piece] = (Board.Instance.pieceBitboards[(int)piece] ^ from) | to;    // Moving Piece
        Board.Instance.Move3DModel(from, to);

        ClickDetector.Instance.isWhiteTurn = !ClickDetector.Instance.isWhiteTurn;
    }
}