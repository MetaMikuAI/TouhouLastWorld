using System.Text.Json.Nodes;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Security.Cryptography;

namespace TouhouLastWorld;

internal sealed class EventsService
{
    internal EventsService(PlayerState playerState)
    {
        PlayerState = playerState;
    }

    private readonly PlayerState PlayerState;
    internal LocalReply EventOverview()
    {
        if (PlayerState.user["_local_event_overview"] is not JsonObject overview)
            return new(503, null, "Local event snapshot unavailable.");
        var body = new JsonObject();
        foreach (var field in new[]
        {
            "event_point_gacha_list",
            "event_special_mission",
            "event_total_point_reward"
        }

        )
        {
            if (overview[field] is not JsonArray entries)
                return new(503, null, "Local event snapshot incomplete.");
            body[field] = entries.DeepClone();
        }

        return new(0, body);
    }

    internal LocalReply DrawEventPoints(DrawEventPointsCommand? request)
    {
        var rule = request?.EventDraw;
        if (rule is null)
            return new(503, null, "Pool definitions unavailable.");
        if (rule.GachaId <= 0 || rule.Count <= 0)
            return new(400, null, "Positive pool and count required.");
        if (!rule.Active)
            return new(409, null, "Event is inactive.");
        var pools = rule.Pools.OrderBy(row => row.Step).ToArray();
        if (rule.EventId <= 0 || pools.Length == 0 || pools.Select(row => row.Id).Distinct().Count() != pools.Length || pools.Where((row, index) => row.Step != index + 1).Any() || pools.Any(row => row.Id <= 0 || row.Cost <= 0 || row.Total <= 0 || row.Drops.Length == 0 || row.Drops.Any(drop => drop.Id <= 0 || drop.Count <= 0) || row.Drops.Select(drop => drop.Id).Distinct().Count() != row.Drops.Length || row.Drops.Sum(drop => (long)drop.Count) != row.Total))
            return new(503, null, "Invalid pool definitions.");
        var pool = pools.SingleOrDefault(row => row.Id == rule.GachaId);
        if (pool is null)
            return new(503, null, "Pool definition unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        if (updated["event_info"] is not JsonArray events || events.Count(row => row?["event_id"]?.GetValue<int>() == rule.EventId) != 1 || updated["_local_event_overview"]?["event_point_gacha_list"] is not JsonArray progress)
            return new(503, null, "Event state unavailable.");
        var balance = events.Single(row => row?["event_id"]?.GetValue<int>() == rule.EventId)!;
        var points = balance["event_point"]?.GetValue<int>() ?? -1;
        var cost = (long)pool.Cost * rule.Count;
        if (points < 0 || cost > points)
            return new(409, null, "Insufficient event points.");
        var states = new Dictionary<int, JsonObject>();
        foreach (var definition in pools)
        {
            var matches = progress.Where(row => row?["event_id"]?.GetValue<int>() == rule.EventId && row?["step_number"]?.GetValue<int>() == definition.Step).ToArray();
            if (matches.Length > 1)
                return new(409, null, "Duplicate pool progress.");
            var state = matches.FirstOrDefault() as JsonObject;
            if (state is null)
            {
                state = new JsonObject
                {
                    ["event_id"] = rule.EventId,
                    ["gacha_id"] = definition.Id,
                    ["step_number"] = definition.Step,
                    ["drop_count_list"] = new JsonArray()
                };
                progress.Add(state);
            }

            if (state["gacha_id"]?.GetValue<int>() != definition.Id || state["drop_count_list"] is not JsonArray counts || counts.Any(row => row is not JsonObject || row["drop_master_id"] is null || row["drop_count"] is null) || counts.Select(row => row!["drop_master_id"]!.GetValue<int>()).Distinct().Count() != counts.Count)
                return new(409, null, "Invalid pool progress.");
            foreach (var count in counts)
            {
                var drop = definition.Drops.SingleOrDefault(row => row.Id == count!["drop_master_id"]!.GetValue<int>());
                var used = count!["drop_count"]!.GetValue<int>();
                if (drop is null || used < 0 || used > drop.Count)
                    return new(409, null, "Invalid reward count.");
            }

            states.Add(definition.Id, state);
            if (definition.Step < pool.Step && definition.Drops.Where(row => row.Target).Any(drop => (counts.FirstOrDefault(row => row!["drop_master_id"]!.GetValue<int>() == drop.Id)?["drop_count"]?.GetValue<int>() ?? 0) != drop.Count))
                return new(409, null, "Pool is locked.");
        }

        var selectedCounts = states[pool.Id]["drop_count_list"]!.AsArray();
        var usedTotal = selectedCounts.Sum(row => (long)row!["drop_count"]!.GetValue<int>());
        if (rule.Count > pool.Total - usedTotal)
            return new(409, null, "Pool has insufficient rewards.");
        if (updated["_local_event_draw_sequences"]?[pool.Id.ToString()] is not JsonArray sequence || sequence.Count < rule.Count)
            return new(503, null, "Prepared draw sequence unavailable.");
        var regular = new JsonArray();
        var targets = new JsonArray();
        for (var index = 0; index < rule.Count; index++)
        {
            var drop = pool.Drops.SingleOrDefault(row => row.Id == sequence[index]?.GetValue<int>());
            if (drop is null)
                return new(503, null, "Prepared reward definition unavailable.");
            var count = selectedCounts.FirstOrDefault(row => row!["drop_master_id"]!.GetValue<int>() == drop.Id);
            var used = count?["drop_count"]?.GetValue<int>() ?? 0;
            if (used >= drop.Count)
                return new(409, null, "Prepared reward exceeds pool inventory.");
            var error = EconomyService.ApplyInventoryReward(updated, drop.Reward);
            if (error is not null)
                return error;
            if (count is null)
                selectedCounts.Add(new JsonObject { ["drop_master_id"] = drop.Id, ["drop_count"] = 1 });
            else
                count["drop_count"] = used + 1;
            (drop.Target ? targets : regular).Add(new JsonObject { ["object_type"] = drop.Reward.Type, ["object_id"] = drop.Reward.Id, ["object_value"] = drop.Reward.Count, ["unique_id"] = 0, ["is_new"] = 0 });
        }

        for (var index = 0; index < rule.Count; index++)
            sequence.RemoveAt(0);
        balance["event_point"] = points - (int)cost;
        PlayerState.Save(updated);
        return new(0, new() { ["event_point_gacha_list"] = new JsonArray(pools.Select(row => (JsonNode)states[row.Id].DeepClone()).ToArray()), ["drop_list"] = regular, ["target_drop_list"] = targets, ["user_update"] = new JsonObject { ["event_info"] = events.DeepClone(), ["gold"] = updated["gold"]!.DeepClone(), ["crystal"] = updated["crystal"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone() } });
    }

    internal LocalReply TreasureOverview(TreasureOverviewCommand? request)
    {
        if (request?.Value is null or <= 0)
            return new(503, null, "Event definition unavailable.");
        if (PlayerState.user["_local_treasure"] is not JsonObject snapshot || snapshot["event_id"]?.GetValue<int>() != request.Value || snapshot["fd_status"]?.GetValue<int>() is not (-1 or 0 or 1) || snapshot["info"] is not JsonObject info)
            return new(503, null, "Local reward snapshot unavailable.");
        var status = snapshot["fd_status"]!.GetValue<int>();
        var gift = info["gift_id"]?.GetValue<int>();
        var code = info["code"]?.GetValue<string>();
        if (gift is null or < 0 || code is null || code.Any(char.IsControl) || status == 1 && (gift == 0 || string.IsNullOrWhiteSpace(code)))
            return new(409, null, "Invalid local reward information.");
        return new(0, new() { ["fd_status"] = status, ["info"] = new JsonObject { ["gift_id"] = gift.Value, ["code"] = code } });
    }

    internal LocalReply DrawTreasure(DrawTreasureCommand? request)
    {
        var rule = request?.TreasureDraw;
        if (rule is null || rule.EventId <= 0 || rule.GiftIds.Length == 0)
            return new(503, null, "Reward definitions unavailable.");
        if (!rule.Active)
            return new(409, null, "Event is inactive.");
        var overview = TreasureOverview(new(Value: rule.EventId));
        if (overview.Status != 0)
            return overview;
        var status = overview.Body!["fd_status"]!.GetValue<int>();
        var info = overview.Body["info"]!.AsObject();
        var gift = info["gift_id"]!.GetValue<int>();
        if (status == -1)
            return new(409, null, "Reward is not available.");
        if (!rule.GiftIds.Contains(gift) || string.IsNullOrWhiteSpace(info["code"]!.GetValue<string>()))
            return new(503, null, "Prepared reward unavailable.");
        if (status == 0)
        {
            var updated = PlayerState.user.DeepClone().AsObject();
            updated["_local_treasure"]!["fd_status"] = 1;
            PlayerState.Save(updated);
        }

        return new(0, info.DeepClone().AsObject());
    }

    internal LocalReply ClaimEventPointReward(ClaimEventPointRewardCommand? request, bool all = false)
    {
        if (request?.Value is null or <= 0)
            return new(400, null, "Positive event ID required.");
        var definition = request.EventRewards;
        if (definition is null || definition.EventId != request.Value || definition.Groups.Length == 0 || definition.Groups.Any(row => row.Id <= 0 || row.Step <= 0 || row.Points < 0) || definition.Groups.Select(row => row.Step).Distinct().Count() != definition.Groups.Length)
            return new(503, null, "Event reward definitions unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var progress = (updated["_local_event_overview"]?["event_total_point_reward"] as JsonArray)?.FirstOrDefault(entry => entry?["event_id"]?.GetValue<int>() == request.Value);
        if (progress is null)
            return new(409, null, "Local event progress unavailable.");
        var group = progress["next_reward_group_id"]?.GetValue<int>() ?? 0;
        var step = progress["step_number"]?.GetValue<int>() ?? 0;
        var points = progress["total_point"]?.GetValue<int>() ?? -1;
        if (group == 0)
            return new(409, null, "No remaining event rewards.");
        var current = definition.Groups.SingleOrDefault(row => row.Id == group && row.Step == step);
        if (current is null || current.IconType != 0 || current.Rewards.Length != 1)
            return new(503, null, "Deterministic event reward unavailable.");
        if (points < current.Points)
            return new(409, null, "Insufficient accumulated points.");
        var selected = all ? definition.Groups.Where(row => row.Step >= step).OrderBy(row => row.Step).TakeWhile(row => row.Points <= points).ToArray() : new[]
        {
            current
        };
        var granted = new JsonArray();
        foreach (var row in selected)
        {
            if (row.IconType != 0 || row.Rewards.Length != 1)
                return new(503, null, "Deterministic event reward unavailable.");
            var reward = row.Rewards[0];
            var error = EconomyService.ApplyInventoryReward(updated, reward);
            if (error is not null)
                return error;
            granted.Add(new JsonObject { ["object_type"] = reward.Type, ["object_id"] = reward.Id, ["object_value"] = reward.Count, ["is_new"] = 0, ["unique_id"] = 0 });
        }

        step = selected[^1].Step;
        var next = definition.Groups.Where(row => row.Step > step).OrderBy(row => row.Step).FirstOrDefault();
        progress["next_reward_group_id"] = next?.Id ?? 0;
        progress["step_number"] = next?.Step ?? step;
        PlayerState.Save(updated);
        var body = new JsonObject
        {
            ["event_id"] = request.Value.Value,
            ["total_point"] = points,
            ["step_number"] = progress["step_number"]!.DeepClone(),
            ["next_reward_group_id"] = next?.Id ?? 0,
            ["user_update"] = new JsonObject
            {
                ["gold"] = updated["gold"]!.DeepClone(),
                ["crystal"] = updated["crystal"]!.DeepClone(),
                ["item"] = updated["item"]!.DeepClone()
            }
        };
        body[all ? "reward_list" : "reward"] = all ? granted : granted[0]!.DeepClone();
        return new(0, body);
    }
}
