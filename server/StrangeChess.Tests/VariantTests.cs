using Xunit;
using System.Collections.Generic;
using System.Linq;
using System;

public class VariantTests
{
    private readonly EngineFacade _facade = new();

    private GameState SetupVariantGame()
    {
        var req = new NewGameRequest("variant", null);
        var state = new GameState
        {
            Mode = "variant",
            Config = new VariantConfig(),
            Board = FenHelper.StartingBoard(),
            Turn = "white",
            Castling = "KQkq",
            Fullmove = 1
        };
        VariantRules.InitializeGame(state);
        VariantRules.ComputeDerivedFields(state, _facade);
        return state;
    }

    [Fact]
    public void InitialPieces_HaveLifetimes()
    {
        var state = SetupVariantGame();
        Assert.Equal(15, state.Board[12].Left); // White e2 pawn
        Assert.Null(state.Board[6].Left); // White g1 king
    }

    [Fact]
    public void Move_DecrementsLifetimes_AndTravels()
    {
        var state = SetupVariantGame();
        
        // White plays e2e4
        var next = VariantRules.SimulateMove(state, "e2e4", _facade);
        
        // e4 pawn should have travelled and decremented to 14
        int e4Idx = 28; // a1=0, e1=4, e2=12, e3=20, e4=28
        Assert.Equal("P", next.Board[e4Idx].P);
        Assert.Equal(14, next.Board[e4Idx].Left);

        // other white pawns decremented to 14
        Assert.Equal(14, next.Board[14].Left); // g2 pawn
        
        // black pieces NOT decremented because it was white's turn
        Assert.Equal(15, next.Board[48].Left); // black a7 pawn
    }

    [Fact]
    public void DroppedPieceExpires()
    {
        var state = SetupVariantGame();
        // Give white 80 points and drop a queen (lifetime 5)
        state.Points["white"] = 80;
        state = VariantRules.SimulateDrop(state, "Q", "d1");
        
        Assert.Equal(5, state.Board[3].Left); // Queen at d1
        
        // Turn 1 Black
        state = VariantRules.SimulateMove(state, "a7a6", _facade);
        Assert.Equal(5, state.Board[3].Left); // Queen still 5

        // Fast forward 4 more white turns
        state = VariantRules.SimulateMove(state, "g2g3", _facade);
        state = VariantRules.SimulateMove(state, "a6a5", _facade);
        state = VariantRules.SimulateMove(state, "g3g4", _facade);
        state = VariantRules.SimulateMove(state, "a5a4", _facade);
        state = VariantRules.SimulateMove(state, "g4g5", _facade);
        state = VariantRules.SimulateMove(state, "b7b6", _facade);
        state = VariantRules.SimulateMove(state, "h2h3", _facade);
        state = VariantRules.SimulateMove(state, "b6b5", _facade);
        state = VariantRules.SimulateMove(state, "h3h4", _facade);

        // Turn 6 White just ended. Queen should be expired.
        Assert.Null(state.Board[3]);
        
        // Verify event
        Assert.Contains(state.Events, e => e.Type == "expired" && e.Piece == "Q" && e.Square == "d1");
    }

    [Fact]
    public void Drop_NotDecrementedOnDropTurn()
    {
        var state = SetupVariantGame();
        // Give white points to afford a knight (30 points)
        state.Points["white"] = 30;
        
        var next = VariantRules.SimulateDrop(state, "N", "a3");
        
        // Knight dropped at a3. Initial lifetime = 10.
        int a3Idx = 16;
        Assert.NotNull(next.Board[a3Idx]);
        Assert.Equal("N", next.Board[a3Idx].P);
        Assert.Equal(10, next.Board[a3Idx].Left); 
    }

    [Fact]
    public void Drop_BlocksCheck_AndClearsEnPassant()
    {
        var state = SetupVariantGame();
        state.Board = new PieceInfo[64];
        state.Board[4] = new PieceInfo("K", null); // White king e1
        state.Board[60] = new PieceInfo("k", null); // Black king e8
        state.Board[20] = new PieceInfo("r", 5); // Black rook e3 (attacking white king!)
        state.Turn = "white";
        state.Points["white"] = 30; // Knight cost
        state.EnPassant = "e3"; // Just a dummy EP

        VariantRules.ComputeDerivedFields(state, _facade);
        
        // Drop N on e2 blocks the check!
        Assert.True(state.LegalDrops.ContainsKey("N"));
        Assert.Contains("e2", state.LegalDrops["N"]);

        var next = VariantRules.SimulateDrop(state, "N", "e2");
        Assert.Null(next.EnPassant); // EP cleared
        
        string newFen = FenHelper.ToFen(next);
        Assert.False(_facade.IsKingAttacked(newFen, true)); // White king is safe now
    }

    [Fact]
    public void CastlingRightCleared_WhenHomeRookExpires()
    {
        var state = SetupVariantGame();
        // Set white h1 rook lifetime to 1
        state.Board[7] = new PieceInfo("R", 1);
        
        var next = VariantRules.SimulateMove(state, "e2e4", _facade);
        
        // Rook expired
        Assert.Null(next.Board[7]);
        
        // K castling right removed, Qkq remain
        Assert.Equal("Qkq", next.Castling);
    }

    [Fact]
    public void Bot_SelfPlay_NeverReturnsIllegalMove()
    {
        for (int i = 0; i < 200; i++)
        {
            var state = SetupVariantGame();
            int turns = 0;
            while (state.Status == "active" && turns < 20)
            {
                // Action!
                state = GameEndpoints.ComputeBotAction(state, _facade, 1, 50);
                
                // If the game didn't crash and status is updated, we successfully made a move
                turns++;
            }
        }
    }
}
