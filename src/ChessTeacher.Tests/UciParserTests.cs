using ChessTeacher.Engine;

namespace ChessTeacher.Tests;

public sealed class UciParserTests
{
    [Fact]
    public void ParsesCentipawnInfo()
    {
        const string line = "info depth 18 multipv 2 score cp -73 nodes 20000 nps 500000 pv e7e5 g1f3";
        Assert.True(UciInfoParser.TryParseInfo(line, out var result));
        Assert.NotNull(result);
        Assert.Equal(18, result!.Depth);
        Assert.Equal(2, result.MultiPv);
        Assert.Equal(-73, result.Score.Centipawns);
        Assert.Equal("e7e5", result.PrincipalVariation[0]);
    }

    [Fact]
    public void ParsesMateAndBestMove()
    {
        Assert.True(UciInfoParser.TryParseInfo(
            "info depth 22 score mate 3 nodes 10 nps 100 pv h5h7", out var info));
        Assert.Equal(3, info!.Score.Mate);

        Assert.True(UciInfoParser.TryParseBestMove(
            "bestmove e2e4 ponder e7e5", out var best, out var ponder));
        Assert.Equal("e2e4", best);
        Assert.Equal("e7e5", ponder);
    }
}
