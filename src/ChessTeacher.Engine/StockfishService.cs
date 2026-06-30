using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace ChessTeacher.Engine;

public sealed class StockfishService : IStockfishService
{
    private readonly ILogger<StockfishService> _logger;
    private readonly SemaphoreSlim _processGate = new(1, 1);
    private readonly SemaphoreSlim _analysisGate = new(1, 1);
    private Process? _process;
    private StreamWriter? _input;
    private Channel<string>? _output;
    private EngineSettings? _lastSettings;
    private bool _disposed;

    public StockfishService(ILogger<StockfishService> logger)
    {
        _logger = logger;
        EnginePath = Path.Combine(AppContext.BaseDirectory, "engines", "stockfish", "stockfish.exe");
    }

    public bool IsRunning => _process is { HasExited: false };
    public string EnginePath { get; }
    public event EventHandler<EngineLine>? AnalysisUpdated;

    public async Task EnsureStartedAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _processGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsRunning) return;
            await StopProcessCoreAsync().ConfigureAwait(false);

            if (!File.Exists(EnginePath))
                throw new FileNotFoundException(
                    "Stockfish is missing. Reinstall ChessTeacher or place stockfish.exe in engines\\stockfish.",
                    EnginePath);

            var info = new ProcessStartInfo
            {
                FileName = EnginePath,
                WorkingDirectory = Path.GetDirectoryName(EnginePath)!,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            _process = new Process { StartInfo = info, EnableRaisingEvents = true };
            _process.Exited += (_, _) => _logger.LogWarning("Stockfish exited.");
            if (!_process.Start()) throw new InvalidOperationException("Stockfish failed to start.");

            _input = _process.StandardInput;
            _input.AutoFlush = true;
            _output = Channel.CreateUnbounded<string>();
            _ = ReadOutputAsync(_process, _output.Writer);
            _ = ReadErrorAsync(_process);

            await SendAsync("uci", cancellationToken).ConfigureAwait(false);
            await WaitForAsync("uciok", TimeSpan.FromSeconds(8), cancellationToken).ConfigureAwait(false);
            await SendAsync("isready", cancellationToken).ConfigureAwait(false);
            await WaitForAsync("readyok", TimeSpan.FromSeconds(8), cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Stockfish started from {Path}", EnginePath);
        }
        catch
        {
            await StopProcessCoreAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            _processGate.Release();
        }
    }

    public async Task ConfigureAsync(EngineSettings settings, CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
        if (_lastSettings == settings) return;

        await SendAsync($"setoption name Threads value {Math.Clamp(settings.Threads, 1, 16)}", cancellationToken);
        await SendAsync($"setoption name Hash value {Math.Clamp(settings.HashMb, 16, 2048)}", cancellationToken);
        await SendAsync($"setoption name MultiPV value {Math.Clamp(settings.MultiPv, 1, 10)}", cancellationToken);
        await SendAsync($"setoption name Skill Level value {Math.Clamp(settings.SkillLevel, 0, 20)}", cancellationToken);
        await SendAsync($"setoption name Ponder value {(settings.Ponder ? "true" : "false")}", cancellationToken);
        await SendAsync("isready", cancellationToken);
        await WaitForAsync("readyok", TimeSpan.FromSeconds(8), cancellationToken);
        _lastSettings = settings;
    }

    public async Task<EngineAnalysis> AnalyzeAsync(
        string fen,
        IReadOnlyList<string>? moves,
        EngineSettings settings,
        CancellationToken cancellationToken = default)
    {
        await _analysisGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ConfigureAsync(settings, cancellationToken).ConfigureAwait(false);
            DrainOutput();

            var position = new StringBuilder("position fen ").Append(fen);
            if (moves is { Count: > 0 }) position.Append(" moves ").AppendJoin(' ', moves);
            await SendAsync(position.ToString(), cancellationToken).ConfigureAwait(false);

            var go = settings.MoveTimeMs > 0
                ? $"go movetime {Math.Clamp(settings.MoveTimeMs, 50, 120000)}"
                : $"go depth {Math.Clamp(settings.Depth, 1, 40)}";
            await SendAsync(go, cancellationToken).ConfigureAwait(false);

            using var registration = cancellationToken.Register(() =>
            {
                try { _input?.WriteLine("stop"); } catch { }
            });

            var lines = new Dictionary<int, EngineLine>();
            string bestMove;
            string? ponder;
            while (true)
            {
                var line = await ReadLineAsync(TimeSpan.FromMinutes(3), cancellationToken).ConfigureAwait(false);
                if (UciInfoParser.TryParseInfo(line, out var parsed) && parsed is not null)
                {
                    lines[parsed.MultiPv] = parsed;
                    AnalysisUpdated?.Invoke(this, parsed);
                }
                else if (UciInfoParser.TryParseBestMove(line, out bestMove, out ponder))
                {
                    break;
                }
            }

            return new EngineAnalysis(
                bestMove,
                ponder,
                lines.Values.OrderBy(x => x.MultiPv).ToArray(),
                lines.Values.Select(x => x.Depth).DefaultIfEmpty(0).Max());
        }
        catch (OperationCanceledException)
        {
            await StopAnalysisAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            _logger.LogError(ex, "Stockfish analysis failed.");
            await ResetEngineAsync().ConfigureAwait(false);
            throw new InvalidOperationException(
                "The chess engine stopped responding. It was reset; try again.", ex);
        }
        finally
        {
            _analysisGate.Release();
        }
    }

    public async Task StopAnalysisAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRunning) return;
        try { await SendAsync("stop", cancellationToken).ConfigureAwait(false); }
        catch (Exception ex) { _logger.LogDebug(ex, "Unable to send stop."); }
    }

    public async Task NewGameAsync(CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
        await SendAsync("ucinewgame", cancellationToken);
        await SendAsync("isready", cancellationToken);
        await WaitForAsync("readyok", TimeSpan.FromSeconds(8), cancellationToken);
    }

    private async Task SendAsync(string command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_input is null) throw new InvalidOperationException("Stockfish is not running.");
        await _input.WriteLineAsync(command.AsMemory(), cancellationToken).ConfigureAwait(false);
    }

    private async Task WaitForAsync(string expected, TimeSpan timeout, CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await ReadLineAsync(timeout, cancellationToken).ConfigureAwait(false);
            if (line.Equals(expected, StringComparison.Ordinal)) return;
        }
    }

    private async Task<string> ReadLineAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (_output is null) throw new InvalidOperationException("Engine output is unavailable.");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(timeout);
        return await _output.Reader.ReadAsync(linked.Token).ConfigureAwait(false);
    }

    private static async Task ReadOutputAsync(Process process, ChannelWriter<string> writer)
    {
        try
        {
            while (await process.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { } line)
                await writer.WriteAsync(line).ConfigureAwait(false);
            writer.TryComplete();
        }
        catch (Exception ex) { writer.TryComplete(ex); }
    }

    private async Task ReadErrorAsync(Process process)
    {
        try
        {
            while (await process.StandardError.ReadLineAsync().ConfigureAwait(false) is { } line)
                _logger.LogDebug("Stockfish stderr: {Line}", line);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Stockfish stderr reader stopped.");
        }
    }

    private void DrainOutput()
    {
        if (_output is null) return;
        while (_output.Reader.TryRead(out _)) { }
    }

    private async Task ResetEngineAsync()
    {
        await _processGate.WaitAsync().ConfigureAwait(false);
        try { await StopProcessCoreAsync().ConfigureAwait(false); }
        finally { _processGate.Release(); }
    }

    private async Task StopProcessCoreAsync()
    {
        var process = _process;
        _process = null;
        try
        {
            if (process is { HasExited: false })
            {
                try
                {
                    if (_input is not null)
                    {
                        await _input.WriteLineAsync("quit").ConfigureAwait(false);
                        await _input.FlushAsync().ConfigureAwait(false);
                    }
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                }
                catch
                {
                    try { process.Kill(entireProcessTree: true); } catch { }
                }
            }
        }
        finally
        {
            _input?.Dispose();
            _input = null;
            process?.Dispose();
            _output = null;
            _lastSettings = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _processGate.WaitAsync().ConfigureAwait(false);
        try { await StopProcessCoreAsync().ConfigureAwait(false); }
        finally
        {
            _processGate.Release();
            _processGate.Dispose();
            _analysisGate.Dispose();
        }
    }
}
