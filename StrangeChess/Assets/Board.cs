using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Board : MonoBehaviour
{
    [SerializeField] public ulong whitePawn   = 0x00FF000000000000;
    [SerializeField] public ulong whiteRook   = 0x8100000000000000;
    [SerializeField] public ulong whiteKnight = 0x4200000000000000;
    [SerializeField] public ulong whiteBishop = 0x2400000000000000;
    [SerializeField] public ulong whiteQueen  = 0x1000000000000000;
    [SerializeField] public ulong whiteKing   = 0x0800000000000000;
                      
    [SerializeField] public ulong blackPawn   = 0x000000000000FF00;
    [SerializeField] public ulong blackRook   = 0x0000000000000081;
    [SerializeField] public ulong blackKnight = 0x0000000000000042;
    [SerializeField] public ulong blackBishop = 0x0000000000000024;
    [SerializeField] public ulong blackQueen  = 0x0000000000000008;
    [SerializeField] public ulong blackKing   = 0x0000000000000010;
                      
    [SerializeField] public ulong whitePieces;
    [SerializeField] public ulong blackPieces;
    [SerializeField] public ulong allPieces;
    [SerializeField] public ulong[] knightAttacks = new ulong[64];

    [SerializeField] public ulong fileB = 0x00FF000000000000;

    public static Board board;

    void Awake()
    {
        if(Board.board == null)
        {
<<<<<<< Updated upstream
            board = this;
        }        
=======
            Instance = this;
        }

        pieceBitboards[(int)pieceType.whitePawn] = 0x00FF000000000000;
        pieceBitboards[(int)pieceType.whiteKnight] = 0x4200000000000000;
        pieceBitboards[(int)pieceType.whiteBishop] = 0x2400000000000000;
        pieceBitboards[(int)pieceType.whiteRook] = 0x8100000000000000;
        pieceBitboards[(int)pieceType.whiteQueen] = 0x1000000000000000;
        pieceBitboards[(int)pieceType.whiteKing] = 0x0800000000000000;
        pieceBitboards[(int)pieceType.blackPawn] = 0x000000000000FF00;
        pieceBitboards[(int)pieceType.blackKnight] = 0x0000000000000042;
        pieceBitboards[(int)pieceType.blackBishop] = 0x0000000000000024;
        pieceBitboards[(int)pieceType.blackRook] = 0x0000000000000081;
        pieceBitboards[(int)pieceType.blackQueen] = 0x0000000000000008;
        pieceBitboards[(int)pieceType.blackKing] = 0x0000000000000010;
>>>>>>> Stashed changes
    }

    void Start()
    {
<<<<<<< Updated upstream
        Debug.Log(displayBitboard(whitePawn));        
=======
        calculateKnightAttacks();
        Debug.Log(knightAttacks[0]);
        Debug.Log(knightAttacks[63]);
>>>>>>> Stashed changes
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
        whitePieces = whitePawn | whiteKnight | whiteBishop | whiteRook |whiteQueen | whiteKing;
        blackPieces = blackPawn | blackKnight | blackBishop | blackRook |blackQueen | blackKing;
        allPieces = whitePieces | blackPieces;
    }
<<<<<<< Updated upstream
}
=======

    public pieceType bitboardToPiece(ulong from)
    {
        if ((from & allPieces) == 0) return pieceType.none;
        
        for (int i = 1; i <= 12; i++)
        {
            if ((from & pieceBitboards[i]) != 0) 
            {
                return (pieceType)i;
            }
        }
        
        return pieceType.none;
    }
    private void calculateKnightAttacks()
    {
        int[,] offsets = {
            { 2, 1 }, { 2, -1 }, { -2, 1 }, { -2, -1 },
            { 1, 2 }, { 1, -2 }, { -1, 2 }, { -1, -2 }};

        for (int sq = 0; sq < 64; sq++)
        {
            ulong moves = 0;

            int file = sq % 8;
            int rank = sq / 8;

            for (int i = 0; i < 8; i++)
            {
                int newFile = file + offsets[i, 0];
                int newRank = rank + offsets[i, 1];

                if (newFile >= 0 && newFile < 8 &&
                    newRank >= 0 && newRank < 8)
                {
                    int targetIndex = newRank * 8 + newFile;
                    moves |= 1UL << targetIndex;
                }
            }

            knightAttacks[63 - sq] = moves;
        }
    }
}
>>>>>>> Stashed changes
