using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace ChessTeacher.Data;

public sealed class DatabaseInitializer
{
    private readonly AppPaths _paths;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(AppPaths paths, ILogger<DatabaseInitializer> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        await using var connection = new SqliteConnection($"Data Source={_paths.Database};Mode=ReadWriteCreate");
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = Schema;
        await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("Database initialized at {Path}", _paths.Database);
    }

    private const string Schema = """
        PRAGMA journal_mode=WAL;
        PRAGMA foreign_keys=ON;

        CREATE TABLE IF NOT EXISTS Users (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            DisplayName TEXT NOT NULL,
            CreatedAt TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Games (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            PlayedAt TEXT NOT NULL,
            WhitePlayer TEXT NOT NULL,
            BlackPlayer TEXT NOT NULL,
            Result TEXT NOT NULL,
            TimeControl TEXT NOT NULL,
            StartingFen TEXT NOT NULL,
            Pgn TEXT NOT NULL,
            OpeningName TEXT NOT NULL DEFAULT '',
            EcoCode TEXT NOT NULL DEFAULT '',
            WhiteAcpl REAL NULL,
            BlackAcpl REAL NULL,
            BestMoves INTEGER NOT NULL DEFAULT 0,
            Inaccuracies INTEGER NOT NULL DEFAULT 0,
            Mistakes INTEGER NOT NULL DEFAULT 0,
            Blunders INTEGER NOT NULL DEFAULT 0,
            AnalysisComplete INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS Moves (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            GameId INTEGER NOT NULL,
            Ply INTEGER NOT NULL,
            San TEXT NOT NULL,
            Uci TEXT NOT NULL,
            FenAfter TEXT NOT NULL,
            FOREIGN KEY(GameId) REFERENCES Games(Id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS Evaluations (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            MoveId INTEGER NOT NULL,
            EvaluationBefore INTEGER NULL,
            EvaluationAfter INTEGER NULL,
            MateBefore INTEGER NULL,
            MateAfter INTEGER NULL,
            BestMove TEXT NOT NULL,
            PrincipalVariation TEXT NOT NULL,
            CentipawnLoss INTEGER NOT NULL,
            Classification TEXT NOT NULL,
            Explanation TEXT NOT NULL,
            ThemesJson TEXT NOT NULL DEFAULT '[]',
            FOREIGN KEY(MoveId) REFERENCES Moves(Id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS Puzzles (
            Id TEXT PRIMARY KEY,
            Fen TEXT NOT NULL,
            SolutionJson TEXT NOT NULL,
            SideToMove TEXT NOT NULL,
            Theme TEXT NOT NULL,
            Rating INTEGER NOT NULL,
            Explanation TEXT NOT NULL,
            Source TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS PuzzleAttempts (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            PuzzleId TEXT NOT NULL,
            AttemptedAt TEXT NOT NULL,
            Success INTEGER NOT NULL,
            HintsUsed INTEGER NOT NULL,
            TimeMs INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Lessons (
            Id TEXT PRIMARY KEY,
            Category TEXT NOT NULL,
            Title TEXT NOT NULL,
            Json TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS LessonProgress (
            LessonId TEXT PRIMARY KEY,
            Completed INTEGER NOT NULL,
            Score REAL NOT NULL,
            UpdatedAt TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Openings (
            EcoCode TEXT NOT NULL,
            Name TEXT NOT NULL,
            Moves TEXT NOT NULL,
            PRIMARY KEY(EcoCode, Moves)
        );

        CREATE TABLE IF NOT EXISTS OpeningRepertoire (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Color TEXT NOT NULL,
            EcoCode TEXT NOT NULL,
            Name TEXT NOT NULL,
            Moves TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Statistics (
            Key TEXT PRIMARY KEY,
            ValueJson TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Settings (
            Key TEXT PRIMARY KEY,
            ValueJson TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS ActivityHistory (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            ActivityType TEXT NOT NULL,
            OccurredAt TEXT NOT NULL,
            DetailsJson TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS IX_Games_PlayedAt ON Games(PlayedAt DESC);
        CREATE INDEX IF NOT EXISTS IX_Moves_GameId_Ply ON Moves(GameId, Ply);
        CREATE INDEX IF NOT EXISTS IX_PuzzleAttempts_PuzzleId ON PuzzleAttempts(PuzzleId);
        """;
}
