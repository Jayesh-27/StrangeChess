using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum pieceType
{
    none,
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
    [SerializeField] public ulong[] pieceBitboards = new ulong[13];

    [SerializeField] public ulong whitePieces;
    [SerializeField] public ulong blackPieces;
    [SerializeField] public ulong allPieces;
    [SerializeField] public ulong[] knightAttacks = new ulong[64];

    // Rank 2 mask for LERF indexing (A1 = bit 0).
    [SerializeField] public ulong fileB = 0x000000000000FF00;

    public static Board Instance;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        InitializeBitboards();
        CalculateExtraBitboards();
        CalculateKnightAttacks();
    }

    public string displayBitboard(ulong bitboard)
    {
        return System.Convert.ToString((long)bitboard, 2).PadLeft(64, '0');
    }

    public void CalculateExtraBitboards()
    {
        whitePieces = pieceBitboards[(int)pieceType.whitePawn] |
                      pieceBitboards[(int)pieceType.whiteKnight] |
                      pieceBitboards[(int)pieceType.whiteBishop] |
                      pieceBitboards[(int)pieceType.whiteRook] |
                      pieceBitboards[(int)pieceType.whiteQueen] |
                      pieceBitboards[(int)pieceType.whiteKing];

        blackPieces = pieceBitboards[(int)pieceType.blackPawn] |
                      pieceBitboards[(int)pieceType.blackKnight] |
                      pieceBitboards[(int)pieceType.blackBishop] |
                      pieceBitboards[(int)pieceType.blackRook] |
                      pieceBitboards[(int)pieceType.blackQueen] |
                      pieceBitboards[(int)pieceType.blackKing];

        allPieces = whitePieces | blackPieces;
    }

    public pieceType bitboardToPiece(ulong from)
    {
        if ((from & allPieces) == 0)
        {
            return pieceType.none;
        }

        for (int i = 1; i <= 12; i++)
        {
            if ((from & pieceBitboards[i]) != 0)
            {
                return (pieceType)i;
            }
        }

        return pieceType.none;
    }

    void InitializeBitboards()
    {
        pieceBitboards[(int)pieceType.whitePawn] = 0x000000000000FF00;
        pieceBitboards[(int)pieceType.whiteKnight] = 0x0000000000000042;
        pieceBitboards[(int)pieceType.whiteBishop] = 0x0000000000000024;
        pieceBitboards[(int)pieceType.whiteRook] = 0x0000000000000081;
        pieceBitboards[(int)pieceType.whiteQueen] = 0x0000000000000008;
        pieceBitboards[(int)pieceType.whiteKing] = 0x0000000000000010;
        pieceBitboards[(int)pieceType.blackPawn] = 0x00FF000000000000;
        pieceBitboards[(int)pieceType.blackKnight] = 0x4200000000000000;
        pieceBitboards[(int)pieceType.blackBishop] = 0x2400000000000000;
        pieceBitboards[(int)pieceType.blackRook] = 0x8100000000000000;
        pieceBitboards[(int)pieceType.blackQueen] = 0x1000000000000000;
        pieceBitboards[(int)pieceType.blackKing] = 0x0800000000000000;
    }

    void CalculateKnightAttacks()
    {
        int[,] offsets = {
            { 2, 1 }, { 2, -1 }, { -2, 1 }, { -2, -1 },
            { 1, 2 }, { 1, -2 }, { -1, 2 }, { -1, -2 }
        };

        for (int sq = 0; sq < 64; sq++)
        {
            ulong moves = 0;

            int file = sq % 8;
            int rank = sq / 8;

            for (int i = 0; i < 8; i++)
            {
                int newFile = file + offsets[i, 0];
                int newRank = rank + offsets[i, 1];

                if (newFile >= 0 && newFile < 8 && newRank >= 0 && newRank < 8)
                {
                    int targetIndex = newRank * 8 + newFile;
                    moves |= 1UL << targetIndex;
                }
            }

            knightAttacks[sq] = moves;
        }
    }
}
