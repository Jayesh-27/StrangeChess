using System;
using System.Collections;
using System.Collections.Generic;
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

        ulong captures = from >> 7 | from >> 9;
        if((captures & Board.Instance.blackPieces) != 0)
        {
            ClickDetector.Instance.availableMoves = ClickDetector.Instance.availableMoves | (captures & Board.Instance.blackPieces);
        }
        Debug.Log("From >> 8" + Board.Instance.displayBitboard(from >> 8));
        if(((from >> 8) & Board.Instance.allPieces) == 0)   // square is empty
        {
            Debug.Log("Pawn can move 1 Square");
            ClickDetector.Instance.availableMoves = ClickDetector.Instance.availableMoves | from >> 8;
            Debug.Log("From >> 8" + Board.Instance.displayBitboard(16));
            if((from & Board.Instance.fileB) != 0 && ((from >> 16) & Board.Instance.allPieces) == 0)
            {
                Debug.Log("Pawn can move 2 Square");
                ClickDetector.Instance.availableMoves = ClickDetector.Instance.availableMoves | from >> 16;
            }
        }
        else
        {
            Debug.Log("pawn can not move");
        }
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
    }
}