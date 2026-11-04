using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalHistoryQuery(int Kind, int FirstId, int SecondId, int Type, string? PlayerId);
public sealed record LocalHistoryDefinitions(int[] Units, Dictionary<int, int> Costumes, int[] Pictures);

public sealed partial class OfflineServer
{
    private LocalReply HistoryTeam(LocalArguments? request)
    {
        var query = request?.HistoryQuery;
        if (query is null || query.Kind < 0 || query.FirstId < 0 || query.SecondId < 0 || query.Type < 0
            || string.IsNullOrWhiteSpace(query.PlayerId) || query.PlayerId.Any(char.IsControl))
            return new(400, null, "Complete history query required.");
        var matches = (user["_local_history_teams"] as JsonArray)?.Where(row =>
            row?["kind"]?.GetValue<int>() == query.Kind && row["uniq_id1"]?.GetValue<int>() == query.FirstId
            && row["uniq_id2"]?.GetValue<int>() == query.SecondId && row["type"]?.GetValue<int>() == query.Type
            && row["player_id"]?.GetValue<string>() == query.PlayerId).ToArray();
        if (matches is null || matches.Length == 0) return new(404, null, "Local history not found.");
        var rules = request!.HistoryDefinitions;
        if (matches.Length != 1 || matches[0]?["units"] is not JsonArray { Count: > 0 and <= 6 } units || rules is null)
            return new(503, null, "History snapshot unavailable.");
        foreach (var entry in units)
        {
            var unit = entry?["unit_info"];
            if (unit is not JsonObject || unit["unit_id"]?.GetValue<int>() is not > 0
                || !rules.Units.Contains(unit["unit_id"]!.GetValue<int>())
                || unit["equip_costume_id"]?.GetValue<int>() is not > 0
                || !rules.Costumes.TryGetValue(unit["equip_costume_id"]!.GetValue<int>(), out var costumeUnit)
                || costumeUnit != unit["unit_id"]!.GetValue<int>()
                || unit["level"]?.GetValue<int>() is not > 0 || unit["rare"]?.GetValue<int>() is not > 0
                || unit["strong_fragrant_wood"] is not JsonObject || unit["strong_fragrant_wood_limit_info"] is not JsonObject)
                return new(503, null, "History unit definition unavailable.");
            for (var slot = 1; slot <= 5; slot++)
            {
                var picture = entry![$"spellcard_picture_info_{slot}"];
                if (picture is not null && (picture is not JsonObject || picture["picture_id"]?.GetValue<int>() is not > 0
                    || !rules.Pictures.Contains(picture["picture_id"]!.GetValue<int>())))
                    return new(503, null, "History equipment definition unavailable.");
            }
        }
        return new(0, new() { ["units"] = units.DeepClone() });
    }
}
