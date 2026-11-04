using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalSeriesEnd(int EpisodeId, int Difficulty, int Progress, int AreaCount, int Result,
    LocalInventoryReward[]? Rewards, LocalInventoryReward[]? FirstRewards);

public sealed partial class OfflineServer
{
    private LocalReply EndSeries(LocalSeriesEnd? rule)
    {
        if (rule is null || rule.EpisodeId <= 0 || rule.Difficulty is < 0 or > 2 || rule.AreaCount is < 1 or > 5
            || rule.Progress < 1 || rule.Progress > rule.AreaCount || rule.Result is not (0 or 1))
            return new(400, null, "Invalid series result.");
        var current = user["series_battle_status"]!;
        var pending = user["_local_series_pending"];
        if (current["episode_id"]!.GetValue<int>() != rule.EpisodeId || current["difficulty"]!.GetValue<int>() != rule.Difficulty
            || current["progress"]!.GetValue<int>() != rule.Progress || current["manage_id"]!.GetValue<int>() != 0
            || pending?["episode_id"]?.GetValue<int>() != rule.EpisodeId || pending?["difficulty"]?.GetValue<int>() != rule.Difficulty
            || pending?["progress"]?.GetValue<int>() != rule.Progress)
            return new(409, null, "Matching pending series entry required.");
        var updated = user.DeepClone().AsObject();
        var state = updated["series_battle_status"]!.AsObject();
        var drops = new JsonArray();
        if (rule.Result == 1)
        {
            if (rule.Rewards is null) return new(503, null, "Area reward definition unavailable.");
            var grants = rule.Rewards.ToList();
            if (rule.Progress == rule.AreaCount)
            {
                var episodes = updated["quest_playdata"] as JsonArray ?? new JsonArray();
                var entries = episodes.Where(row => row?["episode_id"]?.GetValue<int>() == rule.EpisodeId).ToArray();
                if (entries.Length > 1) return new(503, null, "Duplicate episode state.");
                var episode = entries.FirstOrDefault() as JsonObject;
                if (episode is null)
                {
                    episode = new JsonObject { ["episode_id"] = rule.EpisodeId, ["scenario_status"] = 0 };
                    foreach (var name in new[] { "normal", "hard", "lunatic" })
                        episode[name] = new JsonObject { ["status"] = -2, ["battle_challenge"] = new JsonArray() };
                    episodes.Add(episode);
                }
                var difficulty = new[] { "normal", "hard", "lunatic" }[rule.Difficulty];
                var record = episode[difficulty]!.AsObject();
                var status = record["status"]!.GetValue<int>();
                if (status < 2)
                {
                    if (rule.FirstRewards is null) return new(503, null, "First completion reward definition unavailable.");
                    grants.AddRange(rule.FirstRewards);
                    record["status"] = 2;
                }
                if (episodes.Parent is null) updated["quest_playdata"] = episodes;
                foreach (var name in new[] { "episode_id", "difficulty", "progress", "stage_group_master_id", "manage_id", "value1" })
                    state[name] = 0;
                state["use_replays"] = new JsonArray();
            }
            else state["progress"] = rule.Progress + 1;
            foreach (var grant in grants)
            {
                var error = ApplyInventoryReward(updated, grant);
                if (error is not null) return error;
                drops.Add(new JsonObject { ["object_type"] = grant.Type, ["object_id"] = grant.Id,
                    ["object_value"] = grant.Count, ["rate_up_flg"] = 0, ["point_up_flg"] = 0 });
            }
        }
        updated.Remove("_local_series_pending");
        Save(updated);
        var changes = new JsonObject { ["series_battle_status"] = updated["series_battle_status"]!.DeepClone(),
            ["gold"] = updated["gold"]!.DeepClone(), ["crystal"] = updated["crystal"]!.DeepClone(),
            ["item"] = updated["item"]!.DeepClone() };
        if (updated["quest_playdata"] is JsonArray quests) changes["quest_playdata"] = quests.DeepClone();
        return new(0, new() { ["drop_list"] = drops, ["destiny_drop_list"] = new JsonArray(), ["user_update"] = changes });
    }
}
