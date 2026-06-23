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

    public void movePiece(ulong from, ulong to)
    {        
        if(Board.Instance.bitboardToPiece(to) != pieceType.none)        // if there is a piece on to's place
            Board.Instance.pieceBitboards[(int)Board.Instance.bitboardToPiece(to)] &= ~to;      // removing to from to's pieceType
        Board.Instance.pieceBitboards[(int)Board.Instance.bitboardToPiece(from)] |= to;     // adding from to to's pieceType
        Board.Instance.pieceBitboards[(int)Board.Instance.bitboardToPiece(from)] &= ~from;  // removing from from from's pieceType

        Board.Instance.CalculateExtraBitboards();
    }

    // public ulong checkLegalMoves(ulong from, ulong moves)
    // {
    //     ulong legalMoves = 0;
    //     while (moves != 0)
    //     {
    //         ulong move = moves & ~(moves - 1);
    //         pieceType piece = Board.Instance.bitboardToPiece(move);

    //         movePiece(from, move);
    //         if(isKingSafe())
    //             legalMoves |= move;
    //         unmakeMove(move, from, piece);

    //         moves &= moves - 1;
    //     }
    //     Debug.Log(Board.Instance.BitboardToBoardString(legalMoves));
    //     return legalMoves;

    //     //return moves;
    // }

    // private void unmakeMove(ulong from, ulong to, pieceType piece)
    // {
    //     Board.Instance.pieceBitboards[(int)Board.Instance.bitboardToPiece(from)] |= to;     // adding from to to's pieceType
    //     Board.Instance.pieceBitboards[(int)Board.Instance.bitboardToPiece(from)] &= ~from;  // removing from from from's pieceType
    //     if(piece != pieceType.none)
    //         Board.Instance.pieceBitboards[(int)piece] |= to;                                // adding to to piece

    //     Board.Instance.CalculateExtraBitboards();
    // }

    public ulong checkLegalMoves(ulong startSquare, ulong moves)
    {
        ulong legalMoves = 0;
        while (moves != 0)
        {
            ulong targetSquare = moves & ~(moves - 1);
            pieceType capturedPiece = Board.Instance.bitboardToPiece(targetSquare);

            movePiece(startSquare, targetSquare);
            
            if(isKingSafe())
                legalMoves |= targetSquare;
                
            unmakeMove(startSquare, targetSquare, capturedPiece);

            moves &= moves - 1;
        }
        return legalMoves;
    }

    private void unmakeMove(ulong startSquare, ulong targetSquare, pieceType capturedPiece)
    {
        // 1. Move the piece back from targetSquare to startSquare
        Board.Instance.pieceBitboards[(int)Board.Instance.bitboardToPiece(targetSquare)] |= startSquare;
        Board.Instance.pieceBitboards[(int)Board.Instance.bitboardToPiece(targetSquare)] &= ~targetSquare;
        
        // 2. Restore the captured piece exactly where it was (targetSquare)
        if(capturedPiece != pieceType.none)
            Board.Instance.pieceBitboards[(int)capturedPiece] |= targetSquare;

        Board.Instance.CalculateExtraBitboards();
    }

    private bool isKingSafe()
    {
        bool isWhiteTurn = ClickDetector.Instance.isWhiteTurn;
        ulong king = isWhiteTurn ? Board.Instance.pieceBitboards[6] : Board.Instance.pieceBitboards[12];

        //checking is king safe from knights
        ulong knightCheck = knightMoves(king);
        if(isWhiteTurn)
        {
            if((knightCheck & Board.Instance.pieceBitboards[(int)pieceType.blackKnight]) != 0)
            {
                Debug.Log("King is under attack by black knight");
                return false;
            }
        }
        else
        {
            if((knightCheck & Board.Instance.pieceBitboards[(int)pieceType.whiteKnight]) != 0)
            {
                Debug.Log("King is under attack by black knight");
                return false;
            }
        }
        return true;
    }
}