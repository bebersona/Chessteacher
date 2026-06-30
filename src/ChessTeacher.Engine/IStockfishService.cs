namespace ChessTeacher.Engine;

public interface IStockfishService : IAsyncDisposable
{
    bool IsRunning { get; }
    string EnginePath { get; }
    event EventHandler<EngineLine>? AnalysisUpdated;

    Task EnsureStartedAsync(CancellationToken cancellationToken = default);
    Task ConfigureAsync(EngineSettings settings, CancellationToken cancellationToken = default);
    Task<EngineAnalysis> AnalyzeAsync(
        string fen,
        IReadOnlyList<string>? moves,
        EngineSettings settings,
        CancellationToken cancellationToken = default);
    Task StopAnalysisAsync(CancellationToken cancellationToken = default);
    Task NewGameAsync(CancellationToken cancellationToken = default);
}
