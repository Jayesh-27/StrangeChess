using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chess : MonoBehaviour
{
    public static Chess chess;

    private void Awake()
    {
        if (chess == null)
        {
            chess = this;
        }
    }
    public void pawnMoves(ulong from)
    {
        // TODO - Promotion
        // TODO - En passant
        // DONE - Single Move
        // DONE - Double Move
        // TODO - Captures

        ulong captures = from >> 7 | from >> 9;
        if((captures & Board.board.blackPieces) != 0)
        {
            ClickDetector.clickDetector.availableMoves = ClickDetector.clickDetector.availableMoves | (captures & Board.board.blackPieces);
        }
        Debug.Log("From >> 8" + Board.board.displayBitboard(from >> 8));
        if(((from >> 8) & Board.board.allPieces) == 0)   // square is empty
        {
            Debug.Log("Pawn can move 1 Square");
            ClickDetector.clickDetector.availableMoves = ClickDetector.clickDetector.availableMoves | from >> 8;
            Debug.Log("From >> 8" + Board.board.displayBitboard(16));
            if((from & Board.board.fileB) != 0 && ((from >> 16) & Board.board.allPieces) == 0)
            {
                Debug.Log("Pawn can move 2 Square");
                ClickDetector.clickDetector.availableMoves = ClickDetector.clickDetector.availableMoves | from >> 16;
            }
        }
        else
        {
            Debug.Log("pawn can not move");
        }
    }

    public void movePiece(ulong from, ulong to)
    {
        pieces piece = Board.board.bitboardToPiece(from);
        if(piece < pieces.blackPawn)
        {
            pieces toPiece = Board.board.bitboardToPiece(from);
            if(toPiece >= pieces.blackPawn)
            {
                Board.board.pieceBitboards[(int)toPiece] = Board.board.pieceBitboards[(int)toPiece] ^ to;
            }
            Board.board.pieceBitboards[(int)piece] = Board.board.pieceBitboards[(int)piece] ^ from;
            Board.board.pieceBitboards[(int)piece] = Board.board.pieceBitboards[(int)piece] | to;
        }
    }
}