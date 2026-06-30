namespace ChessTeacher.Engine;

public static class UciInfoParser
{
    public static bool TryParseInfo(string line, out EngineLine? result)
    {
        result = null;
        if (!line.StartsWith("info ", StringComparison.Ordinal)) return false;

        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var depth = 0;
        var multiPv = 1;
        var nodes = 0L;
        var nps = 0L;
        int? cp = null;
        int? mate = null;
        var pv = Array.Empty<string>();

        for (var i = 1; i < tokens.Length; i++)
        {
            switch (tokens[i])
            {
                case "depth" when i + 1 < tokens.Length:
                    int.TryParse(tokens[++i], out depth);
                    break;
                case "multipv" when i + 1 < tokens.Length:
                    int.TryParse(tokens[++i], out multiPv);
                    break;
                case "nodes" when i + 1 < tokens.Length:
                    long.TryParse(tokens[++i], out nodes);
                    break;
                case "nps" when i + 1 < tokens.Length:
                    long.TryParse(tokens[++i], out nps);
                    break;
                case "score" when i + 2 < tokens.Length:
                    var kind = tokens[++i];
                    if (kind == "cp" && int.TryParse(tokens[++i], out var parsedCp)) cp = parsedCp;
                    else if (kind == "mate" && int.TryParse(tokens[++i], out var parsedMate)) mate = parsedMate;
                    break;
                case "pv":
                    pv = tokens[(i + 1)..];
                    i = tokens.Length;
                    break;
            }
        }

        if (depth <= 0 || (cp is null && mate is null)) return false;
        result = new EngineLine(multiPv, depth, new EngineScore(cp, mate), nodes, nps, pv);
        return true;
    }

    public static bool TryParseBestMove(string line, out string bestMove, out string? ponder)
    {
        bestMove = string.Empty;
        ponder = null;
        if (!line.StartsWith("bestmove ", StringComparison.Ordinal)) return false;
        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2) return false;
        bestMove = tokens[1];
        var ponderIndex = Array.IndexOf(tokens, "ponder");
        if (ponderIndex >= 0 && ponderIndex + 1 < tokens.Length) ponder = tokens[ponderIndex + 1];
        return true;
    }
}
