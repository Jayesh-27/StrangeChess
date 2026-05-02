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
        // Done - Single Move
        // TODO - Double Move
        // TODO - Captures

        ulong captures = from << 8;
        Debug.Log(Board.board.displayBitboard(from << 8));
        if(((from << 8) & Board.board.allPieces) != 0)   // square is empty
        {
            Debug.Log("Pawn can move 1 Square");
            ClickDetector.clickDetector.availableMoves = ClickDetector.clickDetector.availableMoves | from << 8;
            if((from & Board.board.fileB) != 0 && ((from << 16) & Board.board.allPieces) == 0)
            {
                Debug.Log("Pawn can move 2 Square");
                ClickDetector.clickDetector.availableMoves = ClickDetector.clickDetector.availableMoves | from << 16;
            }
        }
        else
        {
            Debug.Log("pawn can not move");
        }
    }

    public void movePiece(ulong from, ulong to)
    {
        if ((from & Board.board.whitePieces) != 0)
        {
            if ((from & Board.board.whitePawn) != 0)
            {
                Board.board.whitePawn = (Board.board.whitePawn ^ from) | to;
            }
            else if ((from & Board.board.whiteKnight) != 0)
            {
                Board.board.whiteKnight = (Board.board.whiteKnight ^ from) | to;
            }
            else if ((from & Board.board.whiteBishop) != 0)
            {
                Board.board.whiteBishop = (Board.board.whiteBishop ^ from) | to;
            }
            else if ((from & Board.board.whiteRook) != 0)
            {
                Board.board.whiteRook = (Board.board.whiteRook ^ from) | to;
            }
            else if ((from & Board.board.whiteQueen) != 0)
            {
                Board.board.whiteQueen = (Board.board.whiteQueen ^ from) | to;
            }
            else if ((from & Board.board.whiteKing) != 0)
            {
                Board.board.whiteKing = (Board.board.whiteKing ^ from) | to;
            }
        }
    }
}