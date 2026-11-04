using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private LocalReply MiniGameOverview(LocalArguments? request)
    {
        if (request?.Ids is not { Length: > 0 } ids || ids.Any(id => id <= 0 || id > int.MaxValue)
            || ids.Distinct().Count() != ids.Length)
            return new(503, null, "Entry definitions unavailable.");
        var remaining = user["_local_mini_game_remaining_points"]?.GetValue<int>() ?? 0;
        if (remaining < 0) return new(503, null, "Remaining points are invalid.");
        var flags = user["_local_mini_game_claims"] as JsonObject;
        var entries = new JsonArray();
        foreach (var id in ids)
        {
            var flag = flags?[id.ToString(System.Globalization.CultureInfo.InvariantCulture)]?.GetValue<int>() ?? 0;
            if (flag is < 0 or > 1) return new(503, null, "Reward claim state is invalid.");
            entries.Add(new JsonObject { ["mini_game_master_id"] = (int)id, ["get_reward_flg"] = flag });
        }
        return new(0, new() { ["first_clear_reward_info"] = entries, ["get_phantasm_point"] = remaining });
    }

    private LocalReply EndRhythm(LocalRhythmResult? result)
    {
        if (result is null || result.MusicId <= 0 || result.Difficulty is < 2 or > 4
            || result.Score < 0 || !float.IsFinite(result.Rate) || result.Rate < 0 || result.Rank < 0)
            return new(400, null, "Invalid rhythm result.");
        if (user["_local_rhythm_active"]?.GetValue<int>() != result.MusicId)
            return new(409, null, "No matching active music.");
        var updated = user.DeepClone().AsObject();
        var scores = updated["_local_rhythm_scores"] as JsonArray ?? new JsonArray();
        if (scores.Parent is null) updated["_local_rhythm_scores"] = scores;
        var entry = scores.FirstOrDefault(value => value!["rhythm_game_music_master_id"]!.GetValue<int>() == result.MusicId
            && value["difficulty"]!.GetValue<int>() == result.Difficulty);
        if (entry is null)
        {
            entry = new JsonObject
            {
                ["rhythm_game_music_master_id"] = result.MusicId, ["difficulty"] = result.Difficulty,
                ["highest_score"] = 0, ["highest_rate"] = 0f, ["rank"] = 0
            };
            scores.Add(entry);
        }
        entry["highest_score"] = Math.Max(entry["highest_score"]!.GetValue<int>(), result.Score);
        entry["highest_rate"] = Math.Max(entry["highest_rate"]!.GetValue<float>(), result.Rate);
        entry["rank"] = Math.Max(entry["rank"]!.GetValue<int>(), result.Rank);
        updated.Remove("_local_rhythm_active");
        Save(updated);
        return new(0, new() { ["remaining_phantasm_point"] = 0, ["reward_list"] = new JsonArray() });
    }

    private LocalReply StartRhythm(LocalArguments? request)
    {
        if (request?.EntityId is null or <= 0 or > int.MaxValue)
            return new(400, null, "Positive music ID required.");
        if (request.Value != 1) return new(404, null, "Music definition unavailable.");
        var updated = user.DeepClone().AsObject();
        updated["_local_rhythm_active"] = (int)request.EntityId.Value;
        Save(updated);
        return new(0, new());
    }
}
