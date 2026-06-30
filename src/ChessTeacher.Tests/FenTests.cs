using ChessTeacher.Core;
using ChessTeacher.Core.Models;

namespace ChessTeacher.Tests;

public sealed class FenTests
{
    [Fact]
    public void InitialPositionRoundTrips()
    {
        var position = ChessPosition.CreateInitial();
        var fen = FenSerializer.ToFen(position);
        var parsed = FenSerializer.Parse(fen);
        Assert.Equal(fen, FenSerializer.ToFen(parsed));
    }

    [Theory]
    [InlineData("")]
    [InlineData("8/8/8/8/8/8/8/8 w - - 0 1")]
    [InlineData("invalid")]
    public void InvalidFenIsRejected(string fen)
    {
        Assert.False(FenSerializer.TryParse(fen, out _, out _));
    }
}
