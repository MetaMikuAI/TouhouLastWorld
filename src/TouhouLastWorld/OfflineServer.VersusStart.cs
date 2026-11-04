using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalVersusRule(int Id, int AddedLevel);
public sealed record LocalVersusStart(int Id, int BaseLevel, int FinalLevel, int TeamNumber, bool Active,
    int ReleaseEpisode, bool Helper, LocalVersusRule[] Rules);

public sealed partial class OfflineServer
{
    private LocalReply VersusOverview(long[]? ids)
    {
        if (ids is null || ids.Any(id => id <= 0) || ids.Distinct().Count() != ids.Length)
            return new(503, null, "Entry definitions unavailable.");
        var saved = user["_local_versus_clear_info"] as JsonArray ?? new JsonArray();
        var entries = new JsonArray();
        foreach (var id in ids)
        {
            var matches = saved.Where(row => row?["multiple_spirits_weekly_master_id"]?.GetValue<long>() == id).ToArray();
            if (matches.Length > 1) return new(409, null, "Duplicate clear progress.");
            var entry = matches.FirstOrDefault();
            if (entry != null && (entry["cleared_level"]?.GetValue<int>() is not (>= 0 and <= 100)
                || entry["add_rules_cleared_level"] is not JsonArray levels
                || levels.Any(level => level is null || level.GetValue<int>() < 100)))
                return new(409, null, "Invalid clear progress.");
            entries.Add(entry?.DeepClone() ?? new JsonObject { ["multiple_spirits_weekly_master_id"] = id,
                ["cleared_level"] = 0, ["add_rules_cleared_level"] = new JsonArray() });
        }
        return new(0, new() { ["clear_info"] = entries });
    }

    private LocalReply StartVersus(LocalVersusStart? rule)
    {
        if (rule is null || rule.Id <= 0 || rule.ReleaseEpisode <= 0)
            return new(503, null, "Entry definition unavailable.");
        if (!rule.Active) return new(409, null, "Entry is inactive.");
        if (rule.Helper) return new(503, null, "Helper admission unavailable.");
        if (rule.BaseLevel is < 60 or > 100 || rule.TeamNumber <= 0 || rule.Rules.Any(row => row.Id <= 0 || row.AddedLevel < 0)
            || rule.Rules.Select(row => row.Id).Distinct().Count() != rule.Rules.Length
            || (long)rule.BaseLevel + rule.Rules.Sum(row => (long)row.AddedLevel) != rule.FinalLevel)
            return new(400, null, "Invalid challenge levels.");
        var released = (user["quest_playdata"] as JsonArray)?.Any(row => row?["episode_id"]?.GetValue<int>() == rule.ReleaseEpisode
            && row?["normal"]?["status"]?.GetValue<int>() >= 2) == true;
        if (!released) return new(409, null, "Entry is locked.");
        var overview = VersusOverview([rule.Id]);
        if (overview.Status != 0) return overview;
        var cleared = overview.Body!["clear_info"]![0]!["cleared_level"]!.GetValue<int>();
        if (rule.Rules.Length > 0 && cleared < 100) return new(409, null, "Additional rules are locked.");
        var cost = user["_local_versus_entry_costs"]?[rule.Id.ToString()]?.GetValue<int>();
        if (cost is null or < 0) return new(503, null, "Local entry cost unavailable.");
        var teams = user["team"]!["team_list"]!.AsArray().Where(row => row?["number"]?.GetValue<int>() == rule.TeamNumber).ToArray();
        if (teams.Length != 1 || teams[0]?["user_unit_id_list"] is not JsonArray members || members.Count is < 1 or > 6)
            return new(409, null, "Team unavailable.");
        var ids = members.Select(row => row!.GetValue<long>()).ToArray();
        var positive = ids.Where(id => id > 0).ToArray();
        var units = user["unit"]!["unit_list"]!.AsArray();
        if (positive.Length == 0 || ids.Any(id => id < 0) || positive.Distinct().Count() != positive.Length
            || positive.Any(id => units.Count(row => row?["user_unit_id"]?.GetValue<long>() == id) != 1))
            return new(409, null, "Invalid team members.");
        var pending = new JsonObject { ["id"] = rule.Id, ["base_level"] = rule.BaseLevel, ["final_level"] = rule.FinalLevel,
            ["team_number"] = rule.TeamNumber, ["unit_ids"] = members.DeepClone(), ["cost"] = cost.Value,
            ["rule_ids"] = new JsonArray(rule.Rules.OrderBy(row => row.Id).Select(row => (JsonNode)JsonValue.Create(row.Id)!).ToArray()) };
        if (user["_local_versus_pending"] is JsonObject previous)
            return JsonNode.DeepEquals(previous, pending)
                ? new(0, new() { ["user_update"] = new JsonObject { ["phantasm_point"] = user["phantasm_point"]!.DeepClone() } })
                : new(409, null, "Another entry is pending.");
        var points = user["phantasm_point"]?["points"]?.GetValue<int>();
        if (points is null or < 0 || points < cost) return new(409, null, "Insufficient entry points.");
        var updated = user.DeepClone().AsObject();
        updated["phantasm_point"]!["points"] = points.Value - cost.Value;
        updated["_local_versus_pending"] = pending;
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["phantasm_point"] = updated["phantasm_point"]!.DeepClone() } });
    }
}
