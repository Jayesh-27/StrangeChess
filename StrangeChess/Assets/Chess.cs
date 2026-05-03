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
<<<<<<< Updated upstream
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
=======
        //Debug.Log("From >> 8" + Board.Instance.displayBitboard(from >> 8));
        if(((from >> 8) & Board.Instance.allPieces) == 0)   // square is empty
        {
            //Debug.Log("Pawn can move 1 Square");
            ClickDetector.Instance.availableMoves = ClickDetector.Instance.availableMoves | from >> 8;
            //Debug.Log("From >> 8" + Board.Instance.displayBitboard(16));
            if((from & Board.Instance.fileB) != 0 && ((from >> 16) & Board.Instance.allPieces) == 0)
            {
                //Debug.Log("Pawn can move 2 Square");
                ClickDetector.Instance.availableMoves = ClickDetector.Instance.availableMoves | from >> 16;
>>>>>>> Stashed changes
            }
        }
        else
        {
            Debug.Log("pawn can not move");
        }
    }

    public void knightMoves(ulong from)
    {
        
    }

    public void movePiece(ulong from, ulong to)
    {
        if ((from & Board.board.whitePieces) != 0)
        {
            if((to & Board.board.blackPieces) != 0)
            {
                if ((to & Board.board.blackPawn) != 0)
            {
                Board.board.blackPawn = Board.board.blackPawn ^ to;
            }
            else if ((to & Board.board.blackKnight) != 0)
            {
                Board.board.blackKnight = Board.board.blackKnight ^ to;
            }
            else if ((to & Board.board.blackBishop) != 0)
            {
                Board.board.blackBishop = Board.board.blackBishop ^ to;
            }
            else if ((to & Board.board.blackRook) != 0)
            {
                Board.board.blackRook = Board.board.blackRook ^ to;
            }
            else if ((to & Board.board.blackQueen) != 0)
            {
                Board.board.blackQueen = Board.board.blackQueen ^ to;
            }
            else if ((to & Board.board.blackKing) != 0)
            {
                Board.board.blackKing = Board.board.blackKing ^ to;
            }
            }
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