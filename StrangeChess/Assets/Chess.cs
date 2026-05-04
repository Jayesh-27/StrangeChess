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
        ulong captures = from << 7 | from << 9;
        
        ulong whiteCaptures = ((from << 7) & ~0x0101010101010101UL) | ((from << 9) & ~0x8080808080808080UL);
        if((whiteCaptures & Board.Instance.blackPieces) != 0)
        {
            ClickDetector.Instance.availableMoves |= (whiteCaptures & Board.Instance.blackPieces);
        }
        
        if(((from << 8) & Board.Instance.allPieces) == 0)   // square is empty
        {
            ClickDetector.Instance.availableMoves |= from << 8;
            if((from & Board.Instance.fileB) != 0 && ((from << 16) & Board.Instance.allPieces) == 0)
            {
                ClickDetector.Instance.availableMoves |= from << 16;
            }
        }
    }

    public void knightMoves(ulong from)
    {
        
    }

    public void movePiece(ulong from, ulong to)
    {
        pieceType piece = Board.Instance.bitboardToPiece(from);

        if(piece == pieceType.none)
            return;
            
        if(piece < pieceType.blackPawn)
        {
            pieceType toPiece = Board.Instance.bitboardToPiece(to);
            if(toPiece >= pieceType.blackPawn)
            {
                Board.Instance.pieceBitboards[(int)toPiece] ^= to;
            }
            Board.Instance.pieceBitboards[(int)piece] = (Board.Instance.pieceBitboards[(int)piece] ^ from) | to;
        }
        Board.Instance.CalculateExtraBitboards();
    }
}

