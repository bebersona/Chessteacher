using ChessTeacher.Core.Models;
using Microsoft.Data.Sqlite;

namespace ChessTeacher.Data;

public interface IGameRepository
{
    Task<long> SaveAsync(SavedGame game, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SavedGame>> GetRecentAsync(
        int limit = 20,
        int offset = 0,
        CancellationToken cancellationToken = default);
}

public sealed class GameRepository : IGameRepository
{
    private readonly AppPaths _paths;
    public GameRepository(AppPaths paths) => _paths = paths;

    public async Task<long> SaveAsync(SavedGame game, CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        await using var connection = new SqliteConnection($"Data Source={_paths.Database}");
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            INSERT INTO Games (
                PlayedAt, WhitePlayer, BlackPlayer, Result, TimeControl, StartingFen, Pgn,
                OpeningName, EcoCode, WhiteAcpl, BlackAcpl, BestMoves, Inaccuracies,
                Mistakes, Blunders, AnalysisComplete)
            VALUES (
                $playedAt, $white, $black, $result, $timeControl, $startingFen, $pgn,
                $opening, $eco, $whiteAcpl, $blackAcpl, $best, $inaccuracies,
                $mistakes, $blunders, $analysisComplete);
            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue("$playedAt", game.PlayedAt.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$white", game.WhitePlayer);
        command.Parameters.AddWithValue("$black", game.BlackPlayer);
        command.Parameters.AddWithValue("$result", game.Result);
        command.Parameters.AddWithValue("$timeControl", game.TimeControl);
        command.Parameters.AddWithValue("$startingFen", game.StartingFen);
        command.Parameters.AddWithValue("$pgn", game.Pgn);
        command.Parameters.AddWithValue("$opening", game.OpeningName);
        command.Parameters.AddWithValue("$eco", game.EcoCode);
        command.Parameters.AddWithValue("$whiteAcpl", (object?)game.WhiteAcpl ?? DBNull.Value);
        command.Parameters.AddWithValue("$blackAcpl", (object?)game.BlackAcpl ?? DBNull.Value);
        command.Parameters.AddWithValue("$best", game.BestMoves);
        command.Parameters.AddWithValue("$inaccuracies", game.Inaccuracies);
        command.Parameters.AddWithValue("$mistakes", game.Mistakes);
        command.Parameters.AddWithValue("$blunders", game.Blunders);
        command.Parameters.AddWithValue("$analysisComplete", game.AnalysisComplete ? 1 : 0);

        var id = (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
        await transaction.CommitAsync(cancellationToken);
        return id;
    }

    public async Task<IReadOnlyList<SavedGame>> GetRecentAsync(
        int limit = 20,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var result = new List<SavedGame>();
        await using var connection = new SqliteConnection($"Data Source={_paths.Database}");
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Games ORDER BY PlayedAt DESC LIMIT $limit OFFSET $offset;";
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 200));
        command.Parameters.AddWithValue("$offset", Math.Max(0, offset));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(Read(reader));
        return result;
    }

    private static SavedGame Read(SqliteDataReader reader) => new(
        reader.GetInt64(reader.GetOrdinal("Id")),
        DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("PlayedAt"))),
        reader.GetString(reader.GetOrdinal("WhitePlayer")),
        reader.GetString(reader.GetOrdinal("BlackPlayer")),
        reader.GetString(reader.GetOrdinal("Result")),
        reader.GetString(reader.GetOrdinal("TimeControl")),
        reader.GetString(reader.GetOrdinal("StartingFen")),
        reader.GetString(reader.GetOrdinal("Pgn")),
        reader.GetString(reader.GetOrdinal("OpeningName")),
        reader.GetString(reader.GetOrdinal("EcoCode")),
        reader.IsDBNull(reader.GetOrdinal("WhiteAcpl")) ? null : reader.GetDouble(reader.GetOrdinal("WhiteAcpl")),
        reader.IsDBNull(reader.GetOrdinal("BlackAcpl")) ? null : reader.GetDouble(reader.GetOrdinal("BlackAcpl")),
        reader.GetInt32(reader.GetOrdinal("BestMoves")),
        reader.GetInt32(reader.GetOrdinal("Inaccuracies")),
        reader.GetInt32(reader.GetOrdinal("Mistakes")),
        reader.GetInt32(reader.GetOrdinal("Blunders")),
        reader.GetInt32(reader.GetOrdinal("AnalysisComplete")) != 0);
}
