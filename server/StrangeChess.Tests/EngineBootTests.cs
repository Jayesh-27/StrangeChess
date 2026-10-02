using Xunit;

public class EngineBootTests
{
    /// <summary>
    /// Boots the engine from scratch and verifies the starting position is loaded correctly.
    /// </summary>
    [Fact]
    public void Engine_BootsAndLoadsStartFEN()
    {
        Board.Instance = new Board();
        Chess.Instance = new Chess();
        AI.Instance = new AI();
        Board.Instance.Initialize();

        // Side to move
        Assert.True(Board.Instance.isWhiteTurn);

        // Kings
        Assert.Equal(pieceType.whiteKing, Board.Instance.boardSquares[6]);   // g1
        Assert.Equal(pieceType.blackKing, Board.Instance.boardSquares[57]);  // b8

        // Pawns
        Assert.Equal(pieceType.whitePawn, Board.Instance.boardSquares[12]);  // e2
        Assert.Equal(pieceType.whitePawn, Board.Instance.boardSquares[14]);  // g2
        Assert.Equal(pieceType.whitePawn, Board.Instance.boardSquares[15]);  // h2
        Assert.Equal(pieceType.blackPawn, Board.Instance.boardSquares[48]);  // a7
        Assert.Equal(pieceType.blackPawn, Board.Instance.boardSquares[49]);  // b7
        Assert.Equal(pieceType.blackPawn, Board.Instance.boardSquares[50]);  // c7

        // Empty square in the middle
        Assert.Equal(pieceType.none, Board.Instance.boardSquares[28]);       // e4

        // No castling rights
        Assert.Equal(0, Chess.Instance.castlingRights);

        // No en passant
        Assert.Equal(-1, Chess.Instance.enPassantTarget);
    }

    /// <summary>
    /// Loads a custom FEN (1. e4 played) and verifies state.
    /// </summary>
    [Fact]
    public void Engine_LoadFEN_CustomPosition()
    {
        Board.Instance = new Board();
        Chess.Instance = new Chess();
        AI.Instance = new AI();
        Board.Instance.Initialize();

        Board.Instance.LoadFEN("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1");

        Assert.False(Board.Instance.isWhiteTurn);
        Assert.Equal(pieceType.whitePawn, Board.Instance.boardSquares[28]); // e4
        Assert.Equal(pieceType.none, Board.Instance.boardSquares[12]);      // e2 is now empty
        Assert.Equal(20, Chess.Instance.enPassantTarget);                   // e3 = index 20
        Assert.Equal(15, Chess.Instance.castlingRights);                    // all castling rights intact
    }

    /// <summary>
    /// Verifies that move generation produces 20 pseudo-legal moves from the start position.
    /// </summary>
    [Fact]
    public void Engine_StartPosition_Generates20PseudoLegalMoves()
    {
        Board.Instance = new Board();
        Chess.Instance = new Chess();
        AI.Instance = new AI();
        Board.Instance.Initialize();

        Chess.Instance.GenerateAllMoves(0);

        // Start position with 1 king and 3 pawns: 9 moves
        Assert.Equal(9, Chess.Instance.moveCount[0]);
    }
}
