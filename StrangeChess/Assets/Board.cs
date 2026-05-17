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
    public System.Collections.Generic.Dictionary<ulong, ulong>[] rookAttackMap = new System.Collections.Generic.Dictionary<ulong, ulong>[64];
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
        for (int i = 0; i < 64; i++)
        {
            rookAttackMap[i] = new System.Collections.Generic.Dictionary<ulong, ulong>();
        }

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

        // create rook mask
        for(int i = 0; i < 64; i++)
        {
            int row = i / 8;
            int col = i % 8;

            // calculating rook mask
            ulong mask = 0;
            foreach (int r in allRows[row])
            {
                if (r != i) mask |= indexToBitboard(r);
            }
            foreach (int c in allCols[col])
            {
                if (c != i) mask |= indexToBitboard(c);
            }
            rookMasks[i] = mask;

            rookBlockersMasks[i] = rookMasks[i];
            rookBlockersMasks[i] &= ~indexToBitboard(allRows[row][0]);
            rookBlockersMasks[i] &= ~indexToBitboard(allRows[row][7]);
            rookBlockersMasks[i] &= ~indexToBitboard(allCols[col][0]);
            rookBlockersMasks[i] &= ~indexToBitboard(allCols[col][7]);

            ulong blockers = 0;
            do {
                ulong attacks = 0;
                // Right
                for(int r = col + 1; r < 8; r++) {
                    ulong sq = indexToBitboard(row * 8 + r);
                    attacks |= sq;
                    if ((blockers & sq) != 0) break;
                }
                // Left
                for(int r = col - 1; r >= 0; r--) {
                    ulong sq = indexToBitboard(row * 8 + r);
                    attacks |= sq;
                    if ((blockers & sq) != 0) break;
                }
                // Up
                for(int r = row + 1; r < 8; r++) {
                    ulong sq = indexToBitboard(r * 8 + col);
                    attacks |= sq;
                    if ((blockers & sq) != 0) break;
                }
                // Down
                for(int r = row - 1; r >= 0; r--) {
                    ulong sq = indexToBitboard(r * 8 + col);
                    attacks |= sq;
                    if ((blockers & sq) != 0) break;
                }
                rookAttackMap[i][blockers] = attacks;

                blockers = (blockers - rookBlockersMasks[i]) & rookBlockersMasks[i];
            } while (blockers != 0);
        }
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