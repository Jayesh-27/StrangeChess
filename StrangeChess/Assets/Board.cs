using System.Collections;
using System.Collections.Generic;
using System.Numerics;
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
    [SerializeField] private GameObject[] visualPieces = new GameObject[64];
    [SerializeField] public ulong[] pieceBitboards = new ulong[13];
    [SerializeField] public ulong whitePieces;
    [SerializeField] public ulong blackPieces;
    [SerializeField] public ulong allPieces;
    [SerializeField] public ulong[] knightAttacks = new ulong[64];
    [SerializeField] public ulong[] kingAttacks = new ulong[64];
    [SerializeField] private UnityEngine.Vector3 visualPiecesPositionOffset = new UnityEngine.Vector3(0.000800319016f,-0.0148002654f,0.0781002268f);

    [SerializeField] public ulong fileB = 0x000000000000FF00;

    public static Board Instance;

    [SerializeField] public ulong[] rookMasks = new ulong[64];
    [SerializeField] public ulong[] rookBlockersMasks = new ulong[64];
    [SerializeField] public ulong[][] rookAttackTable = new ulong[64][];
    
    // Magic Bitboard data
    public static readonly ulong[] rookMagics = new ulong[64] {
        0xa8002c000108020UL, 0x6c00049b00020081UL, 0x100200010090040UL, 0x2480041000800801UL,
        0x280028004000800UL, 0x900410008040022UL, 0x280020001001080UL, 0x288000204100080UL,
        0xa000800080400034UL, 0x4808020004000UL, 0x2290802004801000UL, 0x411000d00100020UL,
        0x402800800040080UL, 0xb000401004208UL, 0x2409000100040200UL, 0x1002100004082UL,
        0x22878001e24000UL, 0x1090810021004010UL, 0x801030040200012UL, 0x500808008001000UL,
        0xa08018014000880UL, 0x8000808004000200UL, 0x201008080010200UL, 0x801020000441091UL,
        0x800080204005UL, 0x1040200040100048UL, 0x120200402082UL, 0xd14880480100080UL,
        0x12040280080080UL, 0x100040080020080UL, 0x9020010080800200UL, 0x813241200148449UL,
        0x491604001800080UL, 0x100401000402001UL, 0x4820010021001040UL, 0x400402202000812UL,
        0x209009005000802UL, 0x810800601800400UL, 0x4301083214000150UL, 0x204026458e001401UL,
        0x40204000808000UL, 0x8001008040010020UL, 0x8410820820420010UL, 0x1003001000090020UL,
        0x804040008008080UL, 0x12000810020004UL, 0x1000100200040208UL, 0x430000a044020001UL,
        0x2800090234003UL, 0xe0000400022011UL, 0x200000f100020411UL, 0x9800041002020004UL,
        0x2c00004002020088UL, 0x800000400080200UL, 0x3020008100100040UL, 0x640000810040080UL,
        0x8010000008002100UL, 0x8002000000404400UL, 0x8040000008008400UL, 0x4010000000408100UL,
        0x4000000000204800UL, 0x1000000000406200UL, 0x2000000000004100UL, 0x208000000040008UL
    };

    public static readonly int[] rookBlockerBitCounts = new int[64] {
        12, 11, 11, 11, 11, 11, 11, 12,
        11, 10, 10, 10, 10, 10, 10, 11,
        11, 10, 10, 10, 10, 10, 10, 11,
        11, 10, 10, 10, 10, 10, 10, 11,
        11, 10, 10, 10, 10, 10, 10, 11,
        11, 10, 10, 10, 10, 10, 10, 11,
        11, 10, 10, 10, 10, 10, 10, 11,
        12, 11, 11, 11, 11, 11, 11, 12
    };

    [SerializeField] private bool displayRookAttackTable = false;
    [SerializeField] private int a = 0;
    [SerializeField] private int b = 0;
    void Awake()
    {
        if(Board.Instance == null)
        {
            Instance = this;
        }

        pieceBitboards[(int)pieceType.whitePawn]   = 0x000000000000FF00;
        pieceBitboards[(int)pieceType.whiteKnight] = 0x0000000000000042;
        pieceBitboards[(int)pieceType.whiteBishop] = 0x0000000000000024;
        pieceBitboards[(int)pieceType.whiteRook]   = 0x0000000000000081;
        pieceBitboards[(int)pieceType.whiteQueen]  = 0x0000000000000008;
        pieceBitboards[(int)pieceType.whiteKing]   = 0x0000000000000010;
        
        pieceBitboards[(int)pieceType.blackPawn]   = 0x00FF000000000000;
        pieceBitboards[(int)pieceType.blackKnight] = 0x4200000000000000;
        pieceBitboards[(int)pieceType.blackBishop] = 0x2400000000000000;
        pieceBitboards[(int)pieceType.blackRook]   = 0x8100000000000000;
        pieceBitboards[(int)pieceType.blackQueen]  = 0x0800000000000000;
        pieceBitboards[(int)pieceType.blackKing]   = 0x1000000000000000;

        CalculateExtraBitboards();
    }

    void Start()
    {
        calculateKnightAttacks();
        calculateKingAttacks();
        calculateRookAttacks();
    }
    
    void Update()
    {
        if(displayRookAttackTable)
        {
            displayRookAttackTable = false;
            Debug.Log(BitboardToBoardString(rookAttackTable[a][b]));
        }
        CalculateExtraBitboards();
    }

    public void Move3DModel(ulong from, ulong to)
    {
        int fromIndex = GetBitboardIndex(from);
        int toIndex = GetBitboardIndex(to);
        Debug.Log("Moving Piece from " + fromIndex + " to " + toIndex);
        if(visualPieces[toIndex] != null)
        {
            Destroy(visualPieces[toIndex]);
            Debug.Log("Destoryed a Piece");
        }
        visualPieces[fromIndex].transform.localPosition = ChessManager.Instance.sockets[toIndex].transform.localPosition + visualPiecesPositionOffset;
        visualPieces[toIndex] = visualPieces[fromIndex];
        visualPieces[fromIndex] = null;
        Debug.Log("Moved a Piece");
    }

    public int GetBitboardIndex(ulong bitboard)
    {
        if (bitboard == 0) return -1;

        int index = 0;        
        while ((bitboard & 1UL) == 0)
        {
            bitboard >>= 1;
            index++;
        }
        return index;
    }

    public string displayBitboard(ulong bitboard)
    {
        return System.Convert.ToString((long)bitboard, 2).PadLeft(64, '0');
    }

    public void CalculateExtraBitboards()
    {
        whitePieces =   pieceBitboards[(int)pieceType.whitePawn]| 
                        pieceBitboards[(int)pieceType.whiteKnight]|
                        pieceBitboards[(int)pieceType.whiteBishop]|
                        pieceBitboards[(int)pieceType.whiteRook]|
                        pieceBitboards[(int)pieceType.whiteQueen]|
                        pieceBitboards[(int)pieceType.whiteKing];

        blackPieces =   pieceBitboards[(int)pieceType.blackPawn]| 
                        pieceBitboards[(int)pieceType.blackKnight]|
                        pieceBitboards[(int)pieceType.blackBishop]|
                        pieceBitboards[(int)pieceType.blackRook]|
                        pieceBitboards[(int)pieceType.blackQueen]|
                        pieceBitboards[(int)pieceType.blackKing];

        allPieces = whitePieces | blackPieces;
    }

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

            knightAttacks[sq] = moves;
        }
    }

    private void calculateKingAttacks()
    {
        int[,] offsets = {{0, 1 }, {1, 1 }, {1, 0 }, {1, -1 }, {-1, -1 }, {-1, 0 }, {0, -1 }, {-1, 1}};

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

            kingAttacks[sq] = moves;
        }
    }
    private void calculateRookAttacks()
    {
        int[][] allRows = new int[8][];
        int[][] allCols = new int[8][];

        for (int i = 0; i < 8; i++)
        {
            allRows[i] = new int[8];
            allCols[i] = new int[8];
        }

        // calculate all rows and cols
        for(int i = 0; i < 8; i++)
        {            
            for(int j = 0; j < 8; j++)
            {
                allRows[i][j] = (i * 8) + j;
                allCols[j][i] = (i * 8) + j;
            }
        }

        allRows[0][0] = 0;
        // create rook mask
        for(int i = 0; i < 64; i++)
        {
            int row = i / 8;
            int col = i % 8;

            // calculating rook mask
            ulong mask = 0;
            foreach (int r in allRows[row])
            {
                mask |= indexToBitboard(r);
            }
            foreach (int c in allCols[col])
            {
                mask |= indexToBitboard(c);
            }
            mask = mask ^ indexToBitboard(i);
            rookMasks[i] = mask;

            rookBlockersMasks[i] = rookMasks[i];
            rookBlockersMasks[i] ^= indexToBitboard(allRows[row][0]);
            rookBlockersMasks[i] ^= indexToBitboard(allCols[col][0]);
            rookBlockersMasks[i] ^= indexToBitboard(allRows[row][7]);
            rookBlockersMasks[i] ^= indexToBitboard(allCols[col][7]);

            // Initialize the inner jagged array for this square's attack table (1 << bitCount permutations)
            // This grants O(1) constant time lookups!
            int entryCount = 1 << rookBlockerBitCounts[i];
            rookAttackTable[i] = new ulong[entryCount];

            // Iterate through every subset of the blockers mask using the Carry-Rippler trick
            ulong blockers = 0;
            do
            {
                // Multiply blockers by the magic number to scramble it, then shift right to create a dense index
                int magicIndex = (int)((blockers * rookMagics[i]) >> (64 - rookBlockerBitCounts[i]));

                // Compute exact physical rays for this blocker configuration and store them in the hash table
                rookAttackTable[i][magicIndex] = GetSlowRookAttacks(i, blockers);

                // Advance to the next subset permutation of the blocker mask
                blockers = (blockers - rookBlockersMasks[i]) & rookBlockersMasks[i];
            } 
            while (blockers != 0);
        }
    }

    /// <summary>
    /// Calculates exact physical rook attacks on the fly using standard raycasting.
    /// Considers LERF bitboard mapping (0 = A1, 63 = H8).
    /// </summary>
    public ulong GetSlowRookAttacks(int square, ulong blockers)
    {
        ulong attacks = 0UL;
        int rank = square / 8;
        int file = square % 8;

        // North (Rank increases)
        for (int r = rank + 1; r < 8; r++)
        {
            ulong sqMask = 1UL << (r * 8 + file);
            attacks |= sqMask;
            // Stop parsing further in this direction if we hit a blocking piece
            if ((blockers & sqMask) != 0) break;
        }
        
        // South (Rank decreases)
        for (int r = rank - 1; r >= 0; r--)
        {
            ulong sqMask = 1UL << (r * 8 + file);
            attacks |= sqMask;
            if ((blockers & sqMask) != 0) break;
        }
        
        // East (File increases)
        for (int f = file + 1; f < 8; f++)
        {
            ulong sqMask = 1UL << (rank * 8 + f);
            attacks |= sqMask;
            if ((blockers & sqMask) != 0) break;
        }
        
        // West (File decreases)
        for (int f = file - 1; f >= 0; f--)
        {
            ulong sqMask = 1UL << (rank * 8 + f);
            attacks |= sqMask;
            if ((blockers & sqMask) != 0) break;
        }

        return attacks;
    }

    public ulong indexToBitboard(int index)
    {
        return 1UL << index;
    }
    public string BitboardToBoardString(ulong bb)
    {
        string board = "";

        for (int row = 7; row >= 0; row--)
        {
            for (int col = 0; col < 8; col++)
            {
                int index = row * 8 + col;
                board += (((bb >> index) & 1UL) != 0 ? "1" : "0") + "\t";
            }
            board += "\n";
        }
        return board;
    }
}