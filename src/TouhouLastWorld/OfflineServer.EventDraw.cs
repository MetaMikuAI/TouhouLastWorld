using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalEventDrop(int Id, int Count, bool Target, LocalInventoryReward Reward);
public sealed record LocalEventPool(int Id, int Step, int Cost, int Total, LocalEventDrop[] Drops);
public sealed record LocalEventDraw(int EventId, int GachaId, int Count, bool Active, LocalEventPool[] Pools);

public sealed partial class OfflineServer
{
    private LocalReply DrawEventPoints(LocalArguments? request)
    {
        var rule = request?.EventDraw;
        if (rule is null) return new(503, null, "Pool definitions unavailable.");
        if (rule.GachaId <= 0 || rule.Count <= 0) return new(400, null, "Positive pool and count required.");
        if (!rule.Active) return new(409, null, "Event is inactive.");
        var pools = rule.Pools.OrderBy(row => row.Step).ToArray();
        if (rule.EventId <= 0 || pools.Length == 0 || pools.Select(row => row.Id).Distinct().Count() != pools.Length
            || pools.Where((row, index) => row.Step != index + 1).Any()
            || pools.Any(row => row.Id <= 0 || row.Cost <= 0 || row.Total <= 0 || row.Drops.Length == 0
                || row.Drops.Any(drop => drop.Id <= 0 || drop.Count <= 0)
                || row.Drops.Select(drop => drop.Id).Distinct().Count() != row.Drops.Length
                || row.Drops.Sum(drop => (long)drop.Count) != row.Total))
            return new(503, null, "Invalid pool definitions.");
        var pool = pools.SingleOrDefault(row => row.Id == rule.GachaId);
        if (pool is null) return new(503, null, "Pool definition unavailable.");
        var updated = user.DeepClone().AsObject();
        if (updated["event_info"] is not JsonArray events
            || events.Count(row => row?["event_id"]?.GetValue<int>() == rule.EventId) != 1
            || updated["_local_event_overview"]?["event_point_gacha_list"] is not JsonArray progress)
            return new(503, null, "Event state unavailable.");
        var balance = events.Single(row => row?["event_id"]?.GetValue<int>() == rule.EventId)!;
        var points = balance["event_point"]?.GetValue<int>() ?? -1;
        var cost = (long)pool.Cost * rule.Count;
        if (points < 0 || cost > points) return new(409, null, "Insufficient event points.");
        var states = new Dictionary<int, JsonObject>();
        foreach (var definition in pools)
        {
            var matches = progress.Where(row => row?["event_id"]?.GetValue<int>() == rule.EventId
                && row?["step_number"]?.GetValue<int>() == definition.Step).ToArray();
            if (matches.Length > 1) return new(409, null, "Duplicate pool progress.");
            var state = matches.FirstOrDefault() as JsonObject;
            if (state is null)
            {
                state = new JsonObject { ["event_id"] = rule.EventId, ["gacha_id"] = definition.Id,
                    ["step_number"] = definition.Step, ["drop_count_list"] = new JsonArray() };
                progress.Add(state);
            }
            if (state["gacha_id"]?.GetValue<int>() != definition.Id || state["drop_count_list"] is not JsonArray counts
                || counts.Any(row => row is not JsonObject || row["drop_master_id"] is null || row["drop_count"] is null)
                || counts.Select(row => row!["drop_master_id"]!.GetValue<int>()).Distinct().Count() != counts.Count)
                return new(409, null, "Invalid pool progress.");
            foreach (var count in counts)
            {
                var drop = definition.Drops.SingleOrDefault(row => row.Id == count!["drop_master_id"]!.GetValue<int>());
                var used = count!["drop_count"]!.GetValue<int>();
                if (drop is null || used < 0 || used > drop.Count) return new(409, null, "Invalid reward count.");
            }
            states.Add(definition.Id, state);
            if (definition.Step < pool.Step && definition.Drops.Where(row => row.Target).Any(drop =>
                (counts.FirstOrDefault(row => row!["drop_master_id"]!.GetValue<int>() == drop.Id)?["drop_count"]?.GetValue<int>() ?? 0) != drop.Count))
                return new(409, null, "Pool is locked.");
        }
        var selectedCounts = states[pool.Id]["drop_count_list"]!.AsArray();
        var usedTotal = selectedCounts.Sum(row => (long)row!["drop_count"]!.GetValue<int>());
        if (rule.Count > pool.Total - usedTotal) return new(409, null, "Pool has insufficient rewards.");
        if (updated["_local_event_draw_sequences"]?[pool.Id.ToString()] is not JsonArray sequence || sequence.Count < rule.Count)
            return new(503, null, "Prepared draw sequence unavailable.");
        var regular = new JsonArray();
        var targets = new JsonArray();
        for (var index = 0; index < rule.Count; index++)
        {
            var drop = pool.Drops.SingleOrDefault(row => row.Id == sequence[index]?.GetValue<int>());
            if (drop is null) return new(503, null, "Prepared reward definition unavailable.");
            var count = selectedCounts.FirstOrDefault(row => row!["drop_master_id"]!.GetValue<int>() == drop.Id);
            var used = count?["drop_count"]?.GetValue<int>() ?? 0;
            if (used >= drop.Count) return new(409, null, "Prepared reward exceeds pool inventory.");
            var error = ApplyInventoryReward(updated, drop.Reward);
            if (error is not null) return error;
            if (count is null) selectedCounts.Add(new JsonObject { ["drop_master_id"] = drop.Id, ["drop_count"] = 1 });
            else count["drop_count"] = used + 1;
            (drop.Target ? targets : regular).Add(new JsonObject { ["object_type"] = drop.Reward.Type,
                ["object_id"] = drop.Reward.Id, ["object_value"] = drop.Reward.Count, ["unique_id"] = 0, ["is_new"] = 0 });
        }
        for (var index = 0; index < rule.Count; index++) sequence.RemoveAt(0);
        balance["event_point"] = points - (int)cost;
        Save(updated);
        return new(0, new() { ["event_point_gacha_list"] = new JsonArray(pools.Select(row => (JsonNode)states[row.Id].DeepClone()).ToArray()),
            ["drop_list"] = regular, ["target_drop_list"] = targets,
            ["user_update"] = new JsonObject { ["event_info"] = events.DeepClone(), ["gold"] = updated["gold"]!.DeepClone(),
                ["crystal"] = updated["crystal"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone() } });
    }
}
