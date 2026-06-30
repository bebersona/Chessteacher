using ChessTeacher.Core;

namespace ChessTeacher.Tests;

public sealed class PgnTests
{
    [Fact]
    public void ParsesLoadsAndExportsBasicPgn()
    {
        const string pgn = """
            [Event "Test"]
            [White "White"]
            [Black "Black"]
            [Result "*"]

            1. e4 e5 2. Nf3 Nc6 *
            """;

        var service = new PgnService();
        var parsed = service.Parse(pgn);
        var game = service.LoadMainline(parsed);

        Assert.Equal(4, game.Moves.Count);

        var exported = service.Export(
            game,
            parsed.Headers.GetValueOrDefault("White", "White"),
            parsed.Headers.GetValueOrDefault("Black", "Black"),
            parsed.Result);

        Assert.Contains("1. e4 e5", exported);
        Assert.Contains("[White \"White\"]", exported);
    }
}
