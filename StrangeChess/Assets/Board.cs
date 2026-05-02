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

    [SerializeField] public ulong fileB = 0x00FF000000000000;

    public static Board board;

    void Awake()
    {
        if(Board.board == null)
        {
            board = this;
        }        
    }

    void Start()
    {
        Debug.Log(displayBitboard(whitePawn));        
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
}
