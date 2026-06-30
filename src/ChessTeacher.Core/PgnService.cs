using System.Text;
using System.Text.RegularExpressions;
using ChessTeacher.Core.Models;

namespace ChessTeacher.Core;

public sealed record PgnGame(
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyList<string> SanMoves,
    string Result,
    string RawPgn);

public sealed class PgnService
{
    private static readonly Regex HeaderRegex = new(
        @"^\[(?<key>[A-Za-z0-9_]+)\s+""(?<value>(?:\\.|[^""])*)""\]\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public PgnGame Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new FormatException("PGN is empty.");
        if (text.Length > 5_000_000) throw new FormatException("PGN file is too large.");

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in HeaderRegex.Matches(text))
            headers[match.Groups["key"].Value] = match.Groups["value"].Value.Replace("\\\"", "\"");

        var body = RemoveCommentsAndVariations(HeaderRegex.Replace(text, string.Empty));
        var tokens = body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var moves = new List<string>();
        var result = headers.GetValueOrDefault("Result", "*");

        foreach (var token in tokens)
        {
            if (token.StartsWith('$')) continue;
            if (token is "1-0" or "0-1" or "1/2-1/2" or "*") { result = token; break; }
            var cleaned = Regex.Replace(token, @"^\d+\.(\.\.)?", string.Empty);
            if (string.IsNullOrWhiteSpace(cleaned) || cleaned.Contains('.')) continue;
            moves.Add(cleaned);
        }
        return new PgnGame(headers, moves, result, text);
    }

    public ChessGame LoadMainline(PgnGame pgn)
    {
        var startingFen = pgn.Headers.GetValueOrDefault("FEN", ChessPosition.InitialFen);
        var game = new ChessGame(FenSerializer.Parse(startingFen));
        foreach (var san in pgn.SanMoves)
        {
            if (!game.TryParseSan(san, out var move) || move is null)
                throw new FormatException($"Unable to parse SAN '{san}' at ply {game.Moves.Count + 1}.");
            var result = game.TryMakeMove(move);
            if (!result.Success) throw new FormatException(result.Error);
        }
        return game;
    }

    public string Export(ChessGame game, string white = "White", string black = "Black", string result = "*")
    {
        var builder = new StringBuilder();
        builder.AppendLine("[Event \"ChessTeacher Training\"]");
        builder.AppendLine("[Site \"Local\"]");
        builder.AppendLine($"[Date \"{DateTime.UtcNow:yyyy.MM.dd}\"]");
        builder.AppendLine("[Round \"-\"]");
        builder.AppendLine($"[White \"{Escape(white)}\"]");
        builder.AppendLine($"[Black \"{Escape(black)}\"]");
        builder.AppendLine($"[Result \"{result}\"]");
        builder.AppendLine();

        for (var i = 0; i < game.Moves.Count; i++)
        {
            if (i % 2 == 0) builder.Append($"{i / 2 + 1}. ");
            builder.Append(game.Moves[i].San).Append(' ');
        }
        builder.Append(result);
        return builder.ToString().Trim();
    }

    private static string Escape(string value) => value.Replace("\"", "\\\"");

    private static string RemoveCommentsAndVariations(string input)
    {
        var output = new StringBuilder(input.Length);
        var braces = 0;
        var variations = 0;
        var lineComment = false;
        foreach (var c in input)
        {
            if (lineComment) { if (c == '\n') lineComment = false; continue; }
            if (braces > 0)
            {
                if (c == '{') braces++;
                else if (c == '}') braces--;
                continue;
            }
            if (variations > 0)
            {
                if (c == '(') variations++;
                else if (c == ')') variations--;
                continue;
            }
            if (c == ';') { lineComment = true; continue; }
            if (c == '{') { braces = 1; continue; }
            if (c == '(') { variations = 1; continue; }
            output.Append(c);
        }
        return output.ToString();
    }
}
