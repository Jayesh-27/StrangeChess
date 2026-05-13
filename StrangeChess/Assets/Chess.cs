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
        // TODO - Promotion
        // TODO - En passant
        // DONE - Single Move
        // DONE - Double Move
        // DONE - Captures

        ulong captures = from << 7 | from << 9;
        if((captures & Board.Instance.blackPieces) != 0)
        {
            ClickDetector.Instance.availableMoves = ClickDetector.Instance.availableMoves | (captures & Board.Instance.blackPieces);
        }
        if(((from << 8) & Board.Instance.allPieces) == 0)   // square is empty
        {
            Debug.Log("Pawn can move 1 Square");
            ClickDetector.Instance.availableMoves = ClickDetector.Instance.availableMoves | from << 8;
            if((from & Board.Instance.fileB) != 0 && ((from << 16) & Board.Instance.allPieces) == 0)
            {
                Debug.Log("Pawn can move 2 Square");
                ClickDetector.Instance.availableMoves = ClickDetector.Instance.availableMoves | from << 16;
            }
        }
        else
        {
            Debug.Log("pawn can not move");
        }
    }

    public void knightMoves(ulong from)
    {
        int knightIndex = Board.Instance.GetBitboardIndex(from);
        ClickDetector.Instance.availableMoves = Board.Instance.knightAttacks[knightIndex] ^ Board.Instance.whitePieces;
    }

    public void kingMoves(ulong from)
    {
        int kingIndex = Board.Instance.GetBitboardIndex(from);
        ClickDetector.Instance.availableMoves = Board.Instance.kingAttacks[kingIndex] ^ Board.Instance.whitePieces;
    }

    public void rookMoves(ulong from)
    {
        int rookIndex = Board.Instance.GetBitboardIndex(from);
        // ulong attacks = Board.Instance.GetRookAttacks(rookIndex, Board.Instance.allPieces);

        // // remove your own pieces
        // attacks &= ~Board.Instance.whitePieces;

        // ClickDetector.Instance.availableMoves |= attacks;
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
                Board.Instance.pieceBitboards[(int)toPiece] = Board.Instance.pieceBitboards[(int)toPiece] ^ to;
            }
            Board.Instance.pieceBitboards[(int)piece] = (Board.Instance.pieceBitboards[(int)piece] ^ from) | to;
        }
        Board.Instance.Move3DModel(from, to);
    }
}