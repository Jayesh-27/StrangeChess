using System;

public static class Zobrist
{
    // [13 piece types (including none), 64 squares]
    public static readonly ulong[,] piecesArray = new ulong[13, 64];
    public static readonly ulong[] castlingRightsArray = new ulong[16];
    public static readonly ulong[] enPassantArray = new ulong[65]; // 0-63 for squares, 64 for "none"
    public static readonly ulong sideToMove;

    static Zobrist()
    {
        System.Random rnd = new System.Random(12345); // Fixed seed for debugging consistency

        // Helper to generate a random 64-bit number (ulong)
        ulong Random64()
        {
            byte[] buf = new byte[8];
            rnd.NextBytes(buf);
            return BitConverter.ToUInt64(buf, 0);
        }

        // 1. Initialize Piece-Square keys
        for (int piece = 0; piece < 13; piece++)
        {
            for (int square = 0; square < 64; square++)
            {
                piecesArray[piece, square] = Random64();
            }
        }

        // 2. Initialize Castling keys (16 possible states)
        for (int i = 0; i < 16; i++) castlingRightsArray[i] = Random64();

        // 3. Initialize En Passant keys
        for (int i = 0; i < 65; i++) enPassantArray[i] = Random64();

        // 4. Initialize Side to Move key
        sideToMove = Random64();
    }
}