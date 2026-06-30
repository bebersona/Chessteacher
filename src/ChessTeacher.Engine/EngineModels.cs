namespace ChessTeacher.Engine;

public sealed record EngineSettings
{
    public int SkillLevel { get; init; } = 10;
    public int Threads { get; init; } = 2;
    public int HashMb { get; init; } = 128;
    public int MultiPv { get; init; } = 3;
    public int Depth { get; init; } = 16;
    public int MoveTimeMs { get; init; } = 1000;
    public bool Ponder { get; init; }
}

public sealed record EngineScore(int? Centipawns, int? Mate)
{
    public double PawnValue => (Centipawns ?? 0) / 100.0;
    public override string ToString() =>
        Mate is not null
            ? (Mate >= 0 ? $"M{Mate}" : $"-M{Math.Abs(Mate.Value)}")
            : $"{PawnValue:+0.00;-0.00;0.00}";
}

public sealed record EngineLine(
    int MultiPv,
    int Depth,
    EngineScore Score,
    long Nodes,
    long Nps,
    IReadOnlyList<string> PrincipalVariation);

public sealed record EngineAnalysis(
    string BestMove,
    string? Ponder,
    IReadOnlyList<EngineLine> Lines,
    int CompletedDepth);
