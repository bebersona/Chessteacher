using System.Text.Json;
using ChessTeacher.Engine;

namespace ChessTeacher.Data;

public sealed record AppSettings
{
    public string Theme { get; init; } = "Dark";
    public string BoardTheme { get; init; } = "Forest";
    public string PieceStyle { get; init; } = "Unicode";
    public double SoundVolume { get; init; } = 0.7;
    public int AnimationMs { get; init; } = 120;
    public bool AutoSave { get; init; } = true;
    public bool ShowCoordinates { get; init; } = true;
    public bool HighlightLegalMoves { get; init; } = true;
    public bool ShowBestMoveArrows { get; init; } = true;
    public EngineSettings Engine { get; init; } = new();
}

public interface ISettingsService
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

public sealed class SettingsService : ISettingsService
{
    private readonly AppPaths _paths;
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public SettingsService(AppPaths paths) => _paths = paths;

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        if (!File.Exists(_paths.Settings)) return new AppSettings();
        try
        {
            await using var stream = File.OpenRead(_paths.Settings);
            return await JsonSerializer.DeserializeAsync<AppSettings>(stream, Options, cancellationToken)
                   ?? new AppSettings();
        }
        catch (JsonException)
        {
            var backup = _paths.Settings + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            File.Move(_paths.Settings, backup, overwrite: true);
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        var temp = _paths.Settings + ".tmp";
        await using (var stream = File.Create(temp))
            await JsonSerializer.SerializeAsync(stream, settings, Options, cancellationToken);
        File.Move(temp, _paths.Settings, overwrite: true);
    }
}
