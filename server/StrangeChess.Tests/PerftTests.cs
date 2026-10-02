using Xunit;

/// <summary>
/// Perft tests via EngineFacade — these guard the engine integration.
/// If any number differs from the standard, investigate and report; do NOT edit engine algorithm files.
/// </summary>
public class PerftTests
{
    private readonly EngineFacade _facade = new();

    [Theory]
    [InlineData(1, 20)]
    [InlineData(2, 400)]
    [InlineData(3, 8902)]
    [InlineData(4, 197281)]
    public void StartPosition_Perft(int depth, long expectedNodes)
    {
        const string startFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
        long nodes = _facade.Perft(startFen, depth);
        Assert.Equal(expectedNodes, nodes);
    }

    [Theory]
    [InlineData(1, 48)]
    [InlineData(2, 2039)]
    [InlineData(3, 97862)]
    public void Kiwipete_Perft(int depth, long expectedNodes)
    {
        const string kiwipete = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";
        long nodes = _facade.Perft(kiwipete, depth);
        Assert.Equal(expectedNodes, nodes);
    }
}
