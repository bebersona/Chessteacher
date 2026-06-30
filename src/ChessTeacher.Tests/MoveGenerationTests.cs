using ChessTeacher.Core;
using ChessTeacher.Core.Models;

namespace ChessTeacher.Tests;

public sealed class MoveGenerationTests
{
    [Fact]
    public void InitialPositionHasTwentyLegalMoves()
    {
        var game = new ChessGame();
        Assert.Equal(20, game.GenerateLegalMoves().Count);
    }

    [Fact]
    public void EnPassantIsGeneratedAndApplied()
    {
        var game = new ChessGame(FenSerializer.Parse(
            "4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 1"));

        var move = game.GenerateLegalMoves()
            .Single(x => x.From.ToString() == "e5" && x.To.ToString() == "d6");

        Assert.True(move.Flags.HasFlag(MoveFlags.EnPassant));
        Assert.True(game.TryMakeMove(move).Success);
        Assert.True(game.Position[new Square(3, 4)].IsEmpty);
    }

    [Fact]
    public void KingSideCastlingMovesTheRook()
    {
        var game = new ChessGame(FenSerializer.Parse(
            "4k3/8/8/8/8/8/8/4K2R w K - 0 1"));

        var castle = game.GenerateLegalMoves()
            .Single(x => x.Flags.HasFlag(MoveFlags.CastleKingSide));

        Assert.True(game.TryMakeMove(castle).Success);
        Assert.Equal(PieceType.King, game.Position[new Square(6, 0)].Type);
        Assert.Equal(PieceType.Rook, game.Position[new Square(5, 0)].Type);
    }

    [Fact]
    public void PromotionOffersFourChoices()
    {
        var game = new ChessGame(FenSerializer.Parse(
            "4k3/P7/8/8/8/8/8/4K3 w - - 0 1"));

        var promotions = game.GenerateLegalMoves()
            .Where(x => x.From.ToString() == "a7" && x.To.ToString() == "a8")
            .ToArray();

        Assert.Equal(4, promotions.Length);
        Assert.All(promotions, x => Assert.True(x.Flags.HasFlag(MoveFlags.Promotion)));
    }

    [Fact]
    public void FoolsMateIsCheckmate()
    {
        var game = new ChessGame();
        Assert.True(game.TryMakeUci("f2f3", out _));
        Assert.True(game.TryMakeUci("e7e5", out _));
        Assert.True(game.TryMakeUci("g2g4", out _));
        Assert.True(game.TryMakeUci("d8h4", out var result));
        Assert.True(result.IsCheckmate);
        Assert.Equal(GameTermination.Checkmate, game.Termination);
    }

    [Fact]
    public void StalemateIsDetected()
    {
        var game = new ChessGame(FenSerializer.Parse(
            "7k/5Q2/6K1/8/8/8/8/8 b - - 0 1"));
        Assert.Equal(GameTermination.Stalemate, game.Termination);
    }
}
