using System.Globalization;
using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalSeriesStart(int EpisodeId, int Difficulty, int GroupId, int StageId, int AreaCount,
    bool Active, int[]? Replays);

public sealed partial class OfflineServer
{
    private LocalReply StartSeries(LocalSeriesStart? rule)
    {
        if (rule is null || rule.EpisodeId <= 0 || rule.Difficulty is < 0 or > 2 || rule.GroupId <= 0
            || rule.StageId <= 0 || rule.AreaCount is < 1 or > 5)
            return new(503, null, "Series definition unavailable.");
        if (!rule.Active) return new(1008, null, "Series is not open.");
        var state = user["series_battle_status"]!.AsObject();
        var progress = state["progress"]!.GetValue<int>();
        if (progress < 0 || progress > rule.AreaCount) return new(409, null, "Invalid series progress.");
        if (progress > 0 && (state["episode_id"]!.GetValue<int>() != rule.EpisodeId
            || state["difficulty"]!.GetValue<int>() != rule.Difficulty || state["manage_id"]!.GetValue<int>() != 0))
            return new(409, null, "Another series is active.");
        var replays = rule.Replays ?? (progress > 0
            ? state["use_replays"]!.AsArray().Select(value => value!.GetValue<int>()).ToArray()
            : new int[rule.AreaCount]);
        if (replays.Length < rule.AreaCount || replays.Length > 5 || replays.Any(value => value is not (0 or 1))
            || replays.Skip(rule.AreaCount).Any(value => value != 0))
            return new(400, null, "Invalid replay selection.");
        if (progress > 0 && !state["use_replays"]!.AsArray().Select(value => value!.GetValue<int>()).SequenceEqual(replays))
            return new(409, null, "Active replay selection cannot change.");
        var owned = user["unit"]!["unit_list"]!.AsArray().Select(row => row!["user_unit_id"]!.GetValue<long>()).ToHashSet();
        var teams = user["team"]!["team_list"]!.AsArray();
        for (var area = Math.Max(progress, 1); area <= rule.AreaCount; area++)
        {
            if (replays[area - 1] == 1)
            {
                var key = string.Create(CultureInfo.InvariantCulture, $"{rule.StageId}:{area}");
                if (string.IsNullOrEmpty(user["_local_series_replays"]?[key]?.GetValue<string>()))
                    return new(409, null, "Selected replay is unavailable.");
                continue;
            }
            var matching = teams.Where(row => row?["number"]?.GetValue<int>() == 79 + area).ToArray();
            if (matching.Length != 1 || matching[0]?["user_unit_id_list"] is not JsonArray members || members.Count != 6)
                return new(409, null, "Series team is unavailable.");
            var ids = members.Select(value => value!.GetValue<long>()).ToArray();
            var positive = ids.Where(id => id > 0).ToArray();
            if (positive.Length == 0 || ids.Any(id => id < 0) || positive.Distinct().Count() != positive.Length
                || positive.Any(id => !owned.Contains(id))) return new(409, null, "Invalid series team members.");
        }
        var updated = user.DeepClone().AsObject();
        if (progress == 0)
        {
            var next = updated["series_battle_status"]!.AsObject();
            next["episode_id"] = rule.EpisodeId;
            next["difficulty"] = rule.Difficulty;
            next["progress"] = 1;
            next["stage_group_master_id"] = rule.GroupId;
            next["manage_id"] = 0;
            next["value1"] = 0;
            next["use_replays"] = new JsonArray(replays.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
        }
        updated["_local_series_pending"] = new JsonObject { ["episode_id"] = rule.EpisodeId,
            ["difficulty"] = rule.Difficulty, ["progress"] = Math.Max(1, progress) };
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["series_battle_status"] = user["series_battle_status"]!.DeepClone() } });
    }
}
