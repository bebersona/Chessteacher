using ChessTeacher.Teaching;

namespace ChessTeacher.Tests;

public sealed class TeachingTests
{
    [Theory]
    [InlineData(10, MoveClassification.Best)]
    [InlineData(30, MoveClassification.Excellent)]
    [InlineData(70, MoveClassification.Good)]
    [InlineData(140, MoveClassification.Inaccuracy)]
    [InlineData(250, MoveClassification.Mistake)]
    [InlineData(350, MoveClassification.Blunder)]
    public void ClassificationUsesConfiguredBaselineThresholds(
        int loss, MoveClassification expected)
    {
        var classifier = new MoveClassifier();
        var result = classifier.Classify(new MoveEvaluationContext(
            500, 500 - loss, null, null, false, false, false, false, 0));
        Assert.Equal(expected, result.Classification);
    }

    [Fact]
    public void AcplCanIgnoreForcedMovesAndCapOutliers()
    {
        var value = AverageCentipawnLoss.Calculate(
            new[] { (10, false), (5000, false), (300, true) },
            ignoreForced: true,
            capCp: 1000);
        Assert.Equal(505.0, value);
    }
}
