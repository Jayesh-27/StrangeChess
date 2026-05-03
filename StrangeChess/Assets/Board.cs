using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public enum pieces
{
    whitePawn,
    whiteKnight,
    whiteBishop,
    whiteRook,
    whiteQueen,
    whiteKing,
    blackPawn,
    blackKnight,
    blackBishop,
    blackRook,
    blackQueen,
    blackKing
}
public class Board : MonoBehaviour
{
    [SerializeField] public ulong[] pieceBitboards = new ulong[12];
                      
    [SerializeField] public ulong whitePieces;
    [SerializeField] public ulong blackPieces;
    [SerializeField] public ulong allPieces;

    [SerializeField] public ulong fileB = 0x00FF000000000000;

    public static Board board;

    void Awake()
    {
        if(Board.board == null)
        {
            board = this;
        }

        pieceBitboards[(int)pieces.whitePawn]   = 0x00FF000000000000;
        pieceBitboards[(int)pieces.whiteKnight]   = 0x8100000000000000;
        pieceBitboards[(int)pieces.whiteBishop]   = 0x4200000000000000;
        pieceBitboards[(int)pieces.whiteRook]   = 0x2400000000000000;
        pieceBitboards[(int)pieces.whiteQueen]   = 0x1000000000000000;
        pieceBitboards[(int)pieces.whiteKing]   = 0x0800000000000000;
        pieceBitboards[(int)pieces.blackPawn]   = 0x000000000000FF00;
        pieceBitboards[(int)pieces.blackKnight]   = 0x0000000000000042;
        pieceBitboards[(int)pieces.blackBishop]   = 0x0000000000000024;
        pieceBitboards[(int)pieces.blackRook]   = 0x0000000000000081;
        pieceBitboards[(int)pieces.blackQueen]   = 0x0000000000000008;
        pieceBitboards[(int)pieces.blackKing]   = 0x0000000000000010;
    }

    void Start()
    {
        Debug.Log(displayBitboard(pieceBitboards[(int)pieces.whitePawn]));
    }
    void Update()
    {
        CalculateExtraBitboards();
    }

    public string displayBitboard(ulong bitboard)
    {
        return System.Convert.ToString((long)bitboard, 2).PadLeft(64, '0');
    }

    void CalculateExtraBitboards()
    {
        whitePieces = pieceBitboards[0] | pieceBitboards[1] | pieceBitboards[2] | pieceBitboards[3] |pieceBitboards[4] | pieceBitboards[5];
        blackPieces = pieceBitboards[6] | pieceBitboards[7] | pieceBitboards[8] | pieceBitboards[9] |pieceBitboards[10] | pieceBitboards[11];
        allPieces = whitePieces | blackPieces;
    }

    public pieces bitboardToPiece(ulong from)
    {
        if((from & Board.board.allPieces) != 0)
        {
            if ((from & Board.board.whitePieces) != 0)
            {
                if ((from & Board.board.pieceBitboards[(int)pieces.whitePawn]) != 0)
                {
                    return pieces.whitePawn;
                }
                else if ((from & Board.board.pieceBitboards[(int)pieces.whiteKnight]) != 0)
                {
                    return pieces.whiteKnight;
                }
                else if ((from & Board.board.pieceBitboards[(int)pieces.whiteBishop]) != 0)
                {
                    return pieces.whiteBishop;
                }
                else if ((from & Board.board.pieceBitboards[(int)pieces.whiteRook]) != 0)
                {
                    return pieces.whiteRook;
                }
                else if ((from & Board.board.pieceBitboards[(int)pieces.whiteQueen]) != 0)
                {
                    return pieces.whiteQueen;
                }
                else if ((from & Board.board.pieceBitboards[(int)pieces.whiteKing]) != 0)
                {
                    return pieces.whiteKing;
                }
            }
            else
            {
                if ((from & Board.board.pieceBitboards[(int)pieces.blackPawn]) != 0)
                {
                    return pieces.blackPawn;
                }
                else if ((from & Board.board.pieceBitboards[(int)pieces.blackKnight]) != 0)
                {
                    return pieces.blackKnight;
                }
                else if ((from & Board.board.pieceBitboards[(int)pieces.blackBishop]) != 0)
                {
                    return pieces.blackBishop;
                }
                else if ((from & Board.board.pieceBitboards[(int)pieces.blackRook]) != 0)
                {
                    return pieces.blackRook;
                }
                else if ((from & Board.board.pieceBitboards[(int)pieces.blackQueen]) != 0)
                {
                    return pieces.blackQueen;
                }
                else if ((from & Board.board.pieceBitboards[(int)pieces.blackKing]) != 0)
                {
                    return pieces.blackKing;
                }
            }
        }
        return 0;
    }
}
