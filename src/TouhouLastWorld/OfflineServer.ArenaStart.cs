using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalArenaStart(int Category, int BattleId, int EnemyLevel, int Difficulty, int BattleNumber, int[] Teams);

public sealed partial class OfflineServer
{
    private LocalReply StartArena(LocalArenaStart? rule)
    {
        if (rule is null) return new(503, null, "Battle definition unavailable.");
        if (rule.Category is not (1 or 2) || rule.BattleId <= 0 || rule.EnemyLevel < 0 || rule.Difficulty < 0
            || rule.Teams.Length != 1 || rule.Teams[0] <= 0
            || rule.Category == 2 && (rule.BattleNumber is < 1 or > 4 || rule.Teams[0] != rule.BattleNumber + 59))
            return new(400, null, "Invalid battle entry.");
        var overviewReply = rule.Category == 1 ? DailyOverview() : WeeklyOverview();
        if (overviewReply.Status != 0) return overviewReply;
        if (overviewReply.Body!["custom_battle_id"]!.GetValue<int>() != rule.BattleId)
            return new(409, null, "Battle configuration changed.");
        var overview = user[rule.Category == 1 ? "_local_daily_arena" : "_local_weekly_arena"]!;
        var definitions = (overview["entry_rules"] as JsonArray)?.Where(row =>
            row?["difficulty"]?.GetValue<int>() == rule.Difficulty
            && row?["enemy_level"]?.GetValue<int>() == rule.EnemyLevel
            && row?["battle_number"]?.GetValue<int>() == rule.BattleNumber).ToArray();
        if (definitions is not { Length: 1 } || definitions[0]?["cost"]?.GetValue<int>() is not >= 0)
            return new(503, null, "Local entry rule unavailable.");
        var cost = definitions[0]!["cost"]!.GetValue<int>();
        var matches = user["team"]!["team_list"]!.AsArray().Where(row => row?["number"]?.GetValue<int>() == rule.Teams[0]).ToArray();
        if (matches.Length != 1 || matches[0]?["user_unit_id_list"] is not JsonArray members || members.Count is < 1 or > 6)
            return new(409, null, "Team unavailable.");
        var ids = members.Select(row => row!.GetValue<long>()).ToArray();
        var positive = ids.Where(id => id > 0).ToArray();
        var units = user["unit"]!["unit_list"]!.AsArray();
        if (positive.Length == 0 || ids.Any(id => id < 0) || positive.Distinct().Count() != positive.Length
            || positive.Any(id => units.Count(row => row?["user_unit_id"]?.GetValue<long>() == id) != 1))
            return new(409, null, "Invalid team members.");
        var unitTypes = positive.Select(id => units.Single(row => row?["user_unit_id"]?.GetValue<long>() == id)!["unit_id"]!.GetValue<int>()).ToArray();
        if (rule.Category == 2)
        {
            var battles = overview["weekly_battle_data"]!;
            for (var number = 1; number <= 4; number++)
            {
                if (number == rule.BattleNumber) continue;
                var reserved = battles[$"battle_number{number}"]!["unit_list"]!.AsArray();
                if (reserved.Any(row => row is not null && unitTypes.Contains(row.GetValue<int>())))
                    return new(409, null, "Unit is assigned to another battle.");
            }
        }
        var pending = new JsonObject { ["category"] = rule.Category, ["custom_battle_id"] = rule.BattleId,
            ["enemy_level"] = rule.EnemyLevel, ["difficulty"] = rule.Difficulty, ["battle_number"] = rule.BattleNumber,
            ["team_number"] = rule.Teams[0], ["unit_ids"] = members.DeepClone(), ["cost"] = cost };
        if (user["_local_arena_pending"] is JsonObject previous)
            return JsonNode.DeepEquals(previous, pending)
                ? new(0, new() { ["user_update"] = new JsonObject { ["phantasm_point"] = user["phantasm_point"]!.DeepClone() } })
                : new(409, null, "Another entry is pending.");
        var points = user["phantasm_point"]?["points"]?.GetValue<int>();
        if (points is null or < 0 || points < cost) return new(409, null, "Insufficient entry points.");
        var updated = user.DeepClone().AsObject();
        updated["phantasm_point"]!["points"] = points.Value - cost;
        updated["_local_arena_pending"] = pending;
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["phantasm_point"] = updated["phantasm_point"]!.DeepClone() } });
    }
}
