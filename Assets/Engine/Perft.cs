using UnityEngine;
using System.Diagnostics;

public class Perft : MonoBehaviour
{
    [Header("Perft Settings")]
    public int searchDepth = 3; // Start at Depth 3 (Should output 8902 nodes for the starting position)
    public bool runOnStart = true;

    void Start()
    {
        if (runOnStart)
        {
            // Delay by 1 second to ensure Board.cs completes LoadFEN in its Start() method
            Invoke("RunTest", 1f);
        }
    }

    public void RunTest()
    {
        UnityEngine.Debug.Log($"--- Starting Perft Depth {searchDepth} ---");
        
        Stopwatch sw = new Stopwatch();
        sw.Start();
        
        long totalNodes = RunPerftRecursive(searchDepth, 0, true);
        
        sw.Stop();
        float timeSeconds = sw.ElapsedMilliseconds / 1000f;
        
        UnityEngine.Debug.Log($"Depth: {searchDepth} | Nodes: {totalNodes}");
        UnityEngine.Debug.Log($"Time: {timeSeconds:F3} seconds | NPS: {(totalNodes / timeSeconds):F0}");
    }

    private long RunPerftRecursive(int currentDepth, int ply = 0, bool isRoot = false)
    {
        if (currentDepth == 0) return 1;

        // 1. Generate all pseudo-legal moves for this depth into the specific ply's array
        Chess.Instance.GenerateAllMoves(ply);
        
        // 2. Read the exact number of generated moves for this layer
        int currentMoveCount = Chess.Instance.moveCount[ply]; 
        long nodes = 0;

        // 3. Iterate through the pre-allocated move list
        for (int i = 0; i < currentMoveCount; i++)
        {
            ushort move = Chess.Instance.moveList[ply][i];
            
            // Unpack the ushort bitmask
            int fromIndex = move & 0x3F;
            int toIndex = (move >> 6) & 0x3F;
            ulong fromSquare = 1UL << fromIndex;
            ulong toSquare = 1UL << toIndex;

            // Backups (State memory for unmaking)
            int savedCastling = Chess.Instance.castlingRights;
            int savedEP = Chess.Instance.enPassantTarget;
            ulong savedHash = Chess.Instance.currentZobristKey;
            pieceType originalPiece = Board.Instance.boardSquares[fromIndex];
            pieceType capturedPiece = Board.Instance.boardSquares[toIndex]; 

            // Execute Move
            Chess.Instance.movePiece(fromSquare, toSquare);

            // Legality Check (Did we just leave our own King in check?)
            ulong ourKing = ClickDetector.Instance.isWhiteTurn ? 
                Board.Instance.pieceBitboards[(int)pieceType.whiteKing] : 
                Board.Instance.pieceBitboards[(int)pieceType.blackKing];
                
            bool isLegal = Chess.Instance.isSquareSafe(ourKing);

            if (isLegal)
            {
                // Swap turns to allow the opponent to generate moves in the next recursive layer
                ClickDetector.Instance.isWhiteTurn = !ClickDetector.Instance.isWhiteTurn;
                
                // CRITICAL: Pass ply + 1 to the next layer so it writes to the next array row!
                long childNodes = RunPerftRecursive(currentDepth - 1, ply + 1);
                nodes += childNodes;
                
                // Print "Divide" output for the root layer (e.g., e2e4: 20) to track down bugs easily
                if (isRoot)
                {
                    UnityEngine.Debug.Log($"{GetSquareName(fromIndex)}{GetSquareName(toIndex)}: {childNodes}");
                }

                // Restore turns
                ClickDetector.Instance.isWhiteTurn = !ClickDetector.Instance.isWhiteTurn;
            }

            // Restore physical board state
            Chess.Instance.unmakeMove(fromSquare, toSquare, capturedPiece, originalPiece);
            Chess.Instance.castlingRights = savedCastling;
            Chess.Instance.enPassantTarget = savedEP;
            Chess.Instance.currentZobristKey = savedHash;
        }

        return nodes;
    }

    // Helper to format indices into standard chess algebraic notation (e.g., 12 -> e2)
    private string GetSquareName(int index)
    {
        int file = index % 8;
        int rank = index / 8;
        return $"{(char)('a' + file)}{rank + 1}";
    }
}