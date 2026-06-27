using UnityEngine;

public class AI : MonoBehaviour
{
    public static AI Instance;

    // Standard piece values in centipawns (100 = 1 pawn)
    [SerializeField] const int pawnValue = 100;
    [SerializeField] const int bishopValue = 320;
    [SerializeField] const int knightValue = 300;
    [SerializeField] const int rookValue = 500;
    [SerializeField] const int queenValue = 900;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    public int EvaluateBoard()
    {
        int score = 0;

        score = MaterialValueEvaluation();

        return score;
    }

    private int MaterialValueEvaluation()
    {
        int tempScore = 0;

        // --- 1. White Material (Positive tempScore) ---
        tempScore += BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.whitePawn]) * pawnValue;
        tempScore += BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.whiteKnight]) * knightValue;
        tempScore += BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.whiteBishop]) * bishopValue;
        tempScore += BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.whiteRook]) * rookValue;
        tempScore += BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.whiteQueen]) * queenValue;

        // --- 2. Black Material (Negative tempScore) ---
        tempScore -= BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.blackPawn]) * pawnValue;
        tempScore -= BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.blackKnight]) * knightValue;
        tempScore -= BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.blackBishop]) * bishopValue;
        tempScore -= BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.blackRook]) * rookValue;
        tempScore -= BitboardUtility.PopCount(Board.Instance.pieceBitboards[(int)pieceType.blackQueen]) * queenValue;

        // Positive means White is winning. Negative means Black is winning.
        return tempScore;
    }
}

public static class BitboardUtility
{
    // SWAR PopCount: Counts the number of '1' bits in a ulong in O(1) time
    public static int PopCount(ulong x)
    {
        x -= (x >> 1) & 0x5555555555555555UL;
        x = (x & 0x3333333333333333UL) + ((x >> 2) & 0x3333333333333333UL);
        x = (x + (x >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
        return (int)((x * 0x0101010101010101UL) >> 56);
    }
}