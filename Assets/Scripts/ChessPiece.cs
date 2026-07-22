using UnityEngine;

public enum PieceType
{
    Pawn,
    Knight,
    Bishop,
    Rook,
    Queen,
    King
}

public class ChessPiece : MonoBehaviour
{
    public bool isWhitePiece = true;
    public PieceType piece;
    
    // We add memory so the piece knows where it is, and where it came from
    public int currentSquare = -1;
    public int previousSquare = -1;
}