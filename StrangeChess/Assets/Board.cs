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
    
    [SerializeField] public ulong[] bishopMasks = new ulong[64];
    [SerializeField] public ulong[] bishopBlockersMasks = new ulong[64];
    [SerializeField] public ulong[][] bishopAttackTable = new ulong[64][];

    public static readonly ulong[] bishopMagics = new ulong[64] {
        0x10040080B20200UL, 0x42301409005800UL, 0x3008808410808000UL, 0x1008204050008802UL, 
        0x414152001800410UL, 0x214300808000000UL, 0x21144200408A0UL, 0x41008044200501UL, 
        0xBA81120208011410UL, 0x300208104488084UL, 0x8080160802018A20UL, 0x2200022082100000UL, 
        0x904E020211448002UL, 0x4000884110110412UL, 0x10000090C8084000UL, 0x442202100550UL, 
        0x810420A101000A2UL, 0x501000044400C412UL, 0x8010000204184008UL, 0x8080824808210080UL, 
        0x2000422010020UL, 0x800214202012000UL, 0x8808B00100882000UL, 0x428104022180UL, 
        0x900210A64085080FUL, 0x900808000441C801UL, 0xC220890041040UL, 0x808008020002UL, 
        0x31080401004000UL, 0x680810020806000UL, 0x400988802123000UL, 0x520200820141UL, 
        0x410901102040420UL, 0x2109000020240UL, 0x2203000880880UL, 0x1000020080080081UL, 
        0x800440400404100UL, 0x4040020141000UL, 0x40818800200E0UL, 0x10C410428024400UL, 
        0x2504108809000420UL, 0x5140401C8110420UL, 0x2020202100100UL, 0x2100002028009420UL, 
        0x5401082104005040UL, 0x1022408100100UL, 0x8812404100890UL, 0x802045040800208UL, 
        0x4000443084100002UL, 0x2002088241100300UL, 0x94004864100102UL, 0x5091084484044846UL, 
        0xC0080B10240024UL, 0x8444040810644000UL, 0x2020020441042C00UL, 0x1711100101182018UL, 
        0x8840484804100200UL, 0xA04404484828086BUL, 0x40010024320801UL, 0x5108420229UL, 
        0x4400100144105405UL, 0x200024810703228UL, 0x8420090950040840UL, 0x8010012800840848UL
    };

    public static readonly int[] bishopBlockerBitCounts = new int[64] {
        6, 5, 5, 5, 5, 5, 5, 6,
        5, 5, 5, 5, 5, 5, 5, 5,
        5, 5, 7, 7, 7, 7, 5, 5,
        5, 5, 7, 9, 9, 7, 5, 5,
        5, 5, 7, 9, 9, 7, 5, 5,
        5, 5, 7, 7, 7, 7, 5, 5,
        5, 5, 5, 5, 5, 5, 5, 5,
        6, 5, 5, 5, 5, 5, 5, 6
    };

    // Magic Bitboard data
    public static readonly ulong[] rookMagics = new ulong[64] {
        0x80002040008890UL, 0x40400010002000UL, 0x8801000828a2000UL, 0x500100009002044UL,
        0x200100409200a00UL, 0x4100040002082100UL, 0xc00024401081190UL, 0x8900008900002046UL,
        0x1d0800024814009UL, 0x2009400420025000UL, 0x1000801000832004UL, 0x4421001001012038UL,
        0x1000800400800800UL, 0x200200080c110200UL, 0x5324002821102402UL, 0x140a000064108102UL,
        0x20c0008000244484UL, 0x9010020400180UL, 0x81010040200012UL, 0x41818018001000UL,
        0x2820320012006228UL, 0x18808002000400UL, 0x400840001461008UL, 0x410200008d4b24UL,
        0x80010100208446UL, 0x500040002000UL, 0x812200420010UL, 0x20c100180080280UL,
        0xb800110100080004UL, 0x320080140080UL, 0x28300400180146UL, 0x2006200018104UL,
        0x8008a000400048UL, 0x6200540401000UL, 0x8810002004801280UL, 0x200080082801000UL,
        0x1a480081800400UL, 0x4020130a2000408UL, 0x1102000906004408UL, 0x408200010cUL,
        0x8040208840008000UL, 0x100d00260004000UL, 0x300820040120025UL, 0x101002010010008UL,
        0x6009040008008080UL, 0x92001008020064UL, 0x802020004010100UL, 0x5009008110c20004UL,
        0x422180190100UL, 0x200040008080UL, 0x806001c103201100UL, 0xa0682010050100UL,
        0x14808800440280UL, 0x240a0010a4388e00UL, 0x8051208100400UL, 0x5004a241040200UL,
        0x2012004080310022UL, 0x100080415102002aUL, 0x10c1000820004011UL, 0x800100a050000c59UL,
        0x4802000864102002UL, 0x60220010184c2902UL, 0x101208151018821cUL, 0x1020285021008402UL
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
        calculateBishopAttacks();
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
            //Debug.Log("Destoryed a Piece");
        }
        visualPieces[fromIndex].transform.localPosition = ChessManager.Instance.sockets[toIndex].transform.localPosition + visualPiecesPositionOffset;
        visualPieces[toIndex] = visualPieces[fromIndex];
        visualPieces[fromIndex] = null;
        //Debug.Log("Moved a Piece");
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
        #region creating and filling all rows and cols arrays
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

        #endregion

        allRows[0][0] = 0;      // may be this line is doing nothing idk..
        // create rook mask
        

        #region creating rook mask, blockers mask, and attack table for every square
        //running this loop for every square
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
            
            // Safely clear the edges instead of using ^=, because if the piece is ON the edge,
            // XORing it would flip it from 0 (since it was removed above) to 1, erroneously creating an extra blocker bit!
            rookBlockersMasks[i] &= ~indexToBitboard(allRows[row][0]);
            rookBlockersMasks[i] &= ~indexToBitboard(allCols[col][0]);
            rookBlockersMasks[i] &= ~indexToBitboard(allRows[row][7]);
            rookBlockersMasks[i] &= ~indexToBitboard(allCols[col][7]);

            // Initialize the inner jagged array for this square's attack table (1 << bitCount permutations)
            // This grants O(1) constant time lookups!
            //  Creating a dynamic sized array to save memory
            int entryCount = 1 << rookBlockerBitCounts[i];
            rookAttackTable[i] = new ulong[entryCount];

            // Iterate through every subset of the blockers mask using the Carry-Rippler trick
            ulong blockers = 0;
            do
            {
                // Multiply blockers by the magic number to scramble it, then shift right to create a dense index
                // finding the index using magic
                int magicIndex = (int)((blockers * rookMagics[i]) >> (64 - rookBlockerBitCounts[i]));

                // Compute exact physical rays for this blocker configuration and store them in the hash table
                rookAttackTable[i][magicIndex] = GetSlowRookAttacks(i, blockers);

                // Advance to the next subset permutation of the blocker mask  -  carry-rippler trick
                blockers = (blockers - rookBlockersMasks[i]) & rookBlockersMasks[i];
            } 
            while (blockers != 0);
        }
        #endregion
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

    private void calculateBishopAttacks()
    {
        for(int i = 0; i < 64; i++)
        {
            int rank = i / 8;
            int file = i % 8;

            // calculating bishop mask
            ulong mask = 0;
            for (int r = rank + 1, f = file + 1; r <= 6 && f <= 6; r++, f++) mask |= indexToBitboard(r * 8 + f);
            for (int r = rank + 1, f = file - 1; r <= 6 && f >= 1; r++, f--) mask |= indexToBitboard(r * 8 + f);
            for (int r = rank - 1, f = file + 1; r >= 1 && f <= 6; r--, f++) mask |= indexToBitboard(r * 8 + f);
            for (int r = rank - 1, f = file - 1; r >= 1 && f >= 1; r--, f--) mask |= indexToBitboard(r * 8 + f);

            bishopMasks[i] = mask;
            bishopBlockersMasks[i] = mask;

            int entryCount = 1 << bishopBlockerBitCounts[i];
            bishopAttackTable[i] = new ulong[entryCount];

            ulong blockers = 0;
            do
            {
                int magicIndex = (int)((blockers * bishopMagics[i]) >> (64 - bishopBlockerBitCounts[i]));

                bishopAttackTable[i][magicIndex] = GetSlowBishopAttacks(i, blockers);

                blockers = (blockers - bishopBlockersMasks[i]) & bishopBlockersMasks[i];
            } 
            while (blockers != 0);
        }
    }

    public ulong GetSlowBishopAttacks(int square, ulong blockers)
    {
        ulong attacks = 0UL;
        int rank = square / 8;
        int file = square % 8;

        // North-East
        for (int r = rank + 1, f = file + 1; r < 8 && f < 8; r++, f++)
        {
            ulong sqMask = 1UL << (r * 8 + f);
            attacks |= sqMask;
            if ((blockers & sqMask) != 0) break;
        }
        
        // North-West
        for (int r = rank + 1, f = file - 1; r < 8 && f >= 0; r++, f--)
        {
            ulong sqMask = 1UL << (r * 8 + f);
            attacks |= sqMask;
            if ((blockers & sqMask) != 0) break;
        }
        
        // South-East
        for (int r = rank - 1, f = file + 1; r >= 0 && f < 8; r--, f++)
        {
            ulong sqMask = 1UL << (r * 8 + f);
            attacks |= sqMask;
            if ((blockers & sqMask) != 0) break;
        }
        
        // South-West
        for (int r = rank - 1, f = file - 1; r >= 0 && f >= 0; r--, f--)
        {
            ulong sqMask = 1UL << (r * 8 + f);
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