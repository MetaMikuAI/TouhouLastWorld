using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalBatchQuest(int Id, int UnitId, int Limit, bool Eligible);
public sealed record LocalBatchSearch(LocalBatchQuest[] Quests, LocalInventoryReward[] Rewards);

public sealed partial class OfflineServer
{
    private LocalReply BatchSearch(LocalBatchSearch? rule)
    {
        if (rule is null) return new(503, null, "Batch definitions unavailable.");
        if (rule.Quests.Length == 0 || rule.Quests.Any(row => row.Id <= 0 || row.UnitId <= 0 || row.Limit <= 0)
            || rule.Quests.Select(row => row.Id).Distinct().Count() != rule.Quests.Length)
            return new(400, null, "Invalid batch selection.");
        var updated = user.DeepClone().AsObject();
        var saved = updated["character_quest_list"] as JsonArray;
        var plans = updated["_local_batch_search_rewards"] as JsonObject;
        var drops = new JsonArray();
        long cost = 0;
        foreach (var quest in rule.Quests)
        {
            if (!quest.Eligible || !updated["unit"]!["unit_list"]!.AsArray().Any(row => row?["unit_id"]?.GetValue<int>() == quest.UnitId))
                return new(409, null, "Batch entry is locked.");
            var matches = saved?.Where(row => row?["character_quest_id"]?.GetValue<int>() == quest.Id).ToArray();
            if (matches is not { Length: 1 } || matches[0] is not JsonObject progress
                || progress["lunatic"]?["status"]?.GetValue<int>() != 3)
                return new(409, null, "Completed challenge required.");
            var used = progress["count"]?.GetValue<int>();
            if (used is null or < 0 || used >= quest.Limit) return new(409, null, "No remaining attempts.");
            var remaining = quest.Limit - used.Value;
            var plan = plans?[quest.Id.ToString()];
            if (plan?["count"]?.GetValue<int>() != used || plan?["runs"] is not JsonArray runs || runs.Count != remaining)
                return new(503, null, "Matching local batch rewards required.");
            foreach (var run in runs)
            {
                if (run is not JsonArray rewards) return new(503, null, "Invalid batch reward outcome.");
                foreach (var reward in rewards)
                {
                    var type = reward?["object_type"]?.GetValue<int>() ?? 0;
                    var id = reward?["object_id"]?.GetValue<int>() ?? 0;
                    var count = reward?["object_value"]?.GetValue<int>() ?? 0;
                    var allowed = rule.Rewards.Where(row => row.Type == type && row.Id == id).ToArray();
                    if (allowed.Length != 1) return new(503, null, "Batch reward unsupported.");
                    var error = ApplyInventoryReward(updated, allowed[0] with { Count = count });
                    if (error is not null) return error;
                    drops.Add(new JsonObject { ["object_type"] = type, ["object_id"] = id,
                        ["object_value"] = count, ["is_eff"] = false, ["custom_parameter"] = "" });
                }
            }
            cost += (long)remaining * 12;
            progress["count"] = quest.Limit;
            plans!.Remove(quest.Id.ToString());
        }
        var points = updated["phantasm_point"]?["points"]?.GetValue<int>();
        if (points is null or < 0 || cost > points) return new(409, null, "Insufficient batch points.");
        updated["phantasm_point"]!["points"] = points.Value - (int)cost;
        Save(updated);
        return new(0, new() { ["drop_list"] = drops, ["user_update"] = new JsonObject {
            ["character_quest_list"] = updated["character_quest_list"]!.DeepClone(),
            ["phantasm_point"] = updated["phantasm_point"]!.DeepClone(), ["gold"] = updated["gold"]!.DeepClone(),
            ["crystal"] = updated["crystal"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone() } });
    }
}
