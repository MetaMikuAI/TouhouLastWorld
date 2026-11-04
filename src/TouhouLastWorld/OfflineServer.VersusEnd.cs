using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalVersusReward(int Level, LocalInventoryReward? FirstReward, bool HasDrops);
public sealed record LocalVersusEnd(int Id, int BaseLevel, int FinalLevel, int Result, int[] Rules,
    long[] Defeated, bool Ranking, LocalVersusReward[]? Rewards);

public sealed partial class OfflineServer
{
    private LocalReply EndVersus(LocalVersusEnd? rule)
    {
        if (rule is null || rule.Result is not (0 or 1) || rule.Id <= 0 || rule.BaseLevel is < 60 or > 100
            || rule.FinalLevel < rule.BaseLevel || rule.Rules.Any(id => id <= 0)
            || rule.Rules.Distinct().Count() != rule.Rules.Length)
            return new(400, null, "Invalid challenge result.");
        var pending = user["_local_versus_pending"];
        if (pending?["id"]?.GetValue<int>() != rule.Id || pending?["base_level"]?.GetValue<int>() != rule.BaseLevel
            || pending?["final_level"]?.GetValue<int>() != rule.FinalLevel || pending?["unit_ids"] is not JsonArray members
            || pending?["rule_ids"] is not JsonArray rules
            || !rules.Select(id => id!.GetValue<int>()).Order().SequenceEqual(rule.Rules.Order()))
            return new(409, null, "Matching pending challenge required.");
        if (rule.Defeated.Distinct().Count() != rule.Defeated.Length
            || rule.Defeated.Any(id => id <= 0 || !members.Any(value => value!.GetValue<long>() == id)))
            return new(400, null, "Invalid defeated members.");
        if (rule.Ranking) return new(503, null, "Ranking registration unavailable.");
        var overview = VersusOverview([rule.Id]);
        if (overview.Status != 0) return overview;
        var progress = overview.Body!["clear_info"]![0]!.DeepClone();
        var baseLevel = progress["cleared_level"]!.GetValue<int>();
        var extraLevels = progress["add_rules_cleared_level"]!.AsArray().Select(level => level!.GetValue<int>()).ToList();
        var highest = Math.Max(baseLevel, extraLevels.DefaultIfEmpty(0).Max());
        var updated = user.DeepClone().AsObject();
        var drops = new JsonArray();
        if (rule.Result == 1)
        {
            if (rule.Rewards is null || rule.Rewards.Any(row => row.Level < 60)
                || rule.Rewards.Select(row => row.Level).Distinct().Count() != rule.Rewards.Length)
                return new(503, null, "Challenge rewards unavailable.");
            var current = rule.Rewards.SingleOrDefault(row => row.Level == rule.FinalLevel);
            if (current is null || current.HasDrops) return new(503, null, "Challenge drop settlement unavailable.");
            var firstLevel = Math.Max(60L, (long)highest + 1);
            var newlyCleared = rule.Rewards.Where(row => row.Level >= firstLevel && row.Level <= rule.FinalLevel)
                .OrderBy(row => row.Level).ToArray();
            if (newlyCleared.LongLength != Math.Max(0L, (long)rule.FinalLevel - firstLevel + 1))
                return new(503, null, "Intermediate reward definitions unavailable.");
            foreach (var row in newlyCleared)
            {
                var reward = row.FirstReward;
                if (reward is null) continue;
                var error = ApplyInventoryReward(updated, reward);
                if (error is not null) return error;
                drops.Add(new JsonObject { ["object_type"] = reward.Type, ["object_id"] = reward.Id,
                    ["object_value"] = reward.Count, ["rate_up_flg"] = 0, ["point_up_flg"] = 0 });
            }
            progress["cleared_level"] = Math.Max(baseLevel, rule.BaseLevel);
            if (rule.Rules.Length > 0 && rule.FinalLevel >= 100) extraLevels.Add(rule.FinalLevel);
            progress["add_rules_cleared_level"] = new JsonArray(extraLevels.Distinct().Order()
                .Select(level => (JsonNode)JsonValue.Create(level)!).ToArray());
            var saved = updated["_local_versus_clear_info"] as JsonArray ?? new JsonArray();
            if (saved.Parent is null) updated["_local_versus_clear_info"] = saved;
            var previous = saved.FirstOrDefault(row => row?["multiple_spirits_weekly_master_id"]?.GetValue<int>() == rule.Id);
            if (previous != null) saved.Remove(previous);
            saved.Add(progress);
        }
        updated.Remove("_local_versus_pending");
        Save(updated);
        return new(0, new() { ["drop_list"] = drops, ["destiny_drop_list"] = new JsonArray(),
            ["user_update"] = new JsonObject { ["gold"] = updated["gold"]!.DeepClone(),
                ["crystal"] = updated["crystal"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone() } });
    }
}
