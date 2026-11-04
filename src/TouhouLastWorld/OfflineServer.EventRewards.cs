using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalEventRewardGroup(int Id, int Step, int Points, int IconType, LocalInventoryReward[] Rewards);
public sealed record LocalEventRewards(int EventId, LocalEventRewardGroup[] Groups);

public sealed partial class OfflineServer
{
    private LocalReply TreasureOverview(LocalArguments? request)
    {
        if (request?.Value is null or <= 0) return new(503, null, "Event definition unavailable.");
        if (user["_local_treasure"] is not JsonObject snapshot
            || snapshot["event_id"]?.GetValue<int>() != request.Value
            || snapshot["fd_status"]?.GetValue<int>() is not (-1 or 0 or 1)
            || snapshot["info"] is not JsonObject info)
            return new(503, null, "Local reward snapshot unavailable.");
        var status = snapshot["fd_status"]!.GetValue<int>();
        var gift = info["gift_id"]?.GetValue<int>();
        var code = info["code"]?.GetValue<string>();
        if (gift is null or < 0 || code is null || code.Any(char.IsControl)
            || status == 1 && (gift == 0 || string.IsNullOrWhiteSpace(code)))
            return new(409, null, "Invalid local reward information.");
        return new(0, new() { ["fd_status"] = status,
            ["info"] = new JsonObject { ["gift_id"] = gift.Value, ["code"] = code } });
    }

    private LocalReply ClaimEventPointReward(LocalArguments? request, bool all = false)
    {
        if (request?.Value is null or <= 0) return new(400, null, "Positive event ID required.");
        var definition = request.EventRewards;
        if (definition is null || definition.EventId != request.Value || definition.Groups.Length == 0
            || definition.Groups.Any(row => row.Id <= 0 || row.Step <= 0 || row.Points < 0)
            || definition.Groups.Select(row => row.Step).Distinct().Count() != definition.Groups.Length)
            return new(503, null, "Event reward definitions unavailable.");
        var updated = user.DeepClone().AsObject();
        var progress = (updated["_local_event_overview"]?["event_total_point_reward"] as JsonArray)?
            .FirstOrDefault(entry => entry?["event_id"]?.GetValue<int>() == request.Value);
        if (progress is null) return new(409, null, "Local event progress unavailable.");
        var group = progress["next_reward_group_id"]?.GetValue<int>() ?? 0;
        var step = progress["step_number"]?.GetValue<int>() ?? 0;
        var points = progress["total_point"]?.GetValue<int>() ?? -1;
        if (group == 0) return new(409, null, "No remaining event rewards.");
        var current = definition.Groups.SingleOrDefault(row => row.Id == group && row.Step == step);
        if (current is null || current.IconType != 0 || current.Rewards.Length != 1)
            return new(503, null, "Deterministic event reward unavailable.");
        if (points < current.Points) return new(409, null, "Insufficient accumulated points.");
        var selected = all ? definition.Groups.Where(row => row.Step >= step).OrderBy(row => row.Step)
            .TakeWhile(row => row.Points <= points).ToArray() : new[] { current };
        var granted = new JsonArray();
        foreach (var row in selected)
        {
            if (row.IconType != 0 || row.Rewards.Length != 1)
                return new(503, null, "Deterministic event reward unavailable.");
            var reward = row.Rewards[0];
            var error = ApplyInventoryReward(updated, reward);
            if (error is not null) return error;
            granted.Add(new JsonObject { ["object_type"] = reward.Type, ["object_id"] = reward.Id,
                ["object_value"] = reward.Count, ["is_new"] = 0, ["unique_id"] = 0 });
        }
        step = selected[^1].Step;
        var next = definition.Groups.Where(row => row.Step > step).OrderBy(row => row.Step).FirstOrDefault();
        progress["next_reward_group_id"] = next?.Id ?? 0;
        progress["step_number"] = next?.Step ?? step;
        Save(updated);
        var body = new JsonObject
        {
            ["event_id"] = request.Value.Value, ["total_point"] = points,
            ["step_number"] = progress["step_number"]!.DeepClone(), ["next_reward_group_id"] = next?.Id ?? 0,
            ["user_update"] = new JsonObject { ["gold"] = updated["gold"]!.DeepClone(),
                ["crystal"] = updated["crystal"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone() }
        };
        body[all ? "reward_list" : "reward"] = all ? granted : granted[0]!.DeepClone();
        return new(0, body);
    }
}
