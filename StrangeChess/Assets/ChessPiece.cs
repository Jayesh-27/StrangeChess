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
}