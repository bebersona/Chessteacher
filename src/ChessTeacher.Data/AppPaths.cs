namespace ChessTeacher.Data;

public sealed class AppPaths
{
    public AppPaths()
    {
        Root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ChessTeacher");
        Database = Path.Combine(Root, "ChessTeacher.db");
        Settings = Path.Combine(Root, "settings.json");
        Logs = Path.Combine(Root, "logs");
        Autosaves = Path.Combine(Root, "autosaves");
        ImportedGames = Path.Combine(Root, "imported-games");
        AnalysisCache = Path.Combine(Root, "analysis-cache");
    }

    public string Root { get; }
    public string Database { get; }
    public string Settings { get; }
    public string Logs { get; }
    public string Autosaves { get; }
    public string ImportedGames { get; }
    public string AnalysisCache { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Autosaves);
        Directory.CreateDirectory(ImportedGames);
        Directory.CreateDirectory(AnalysisCache);
    }
}
