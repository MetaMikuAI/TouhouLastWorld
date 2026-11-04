using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalEventRewardGroup(int Id, int Step, int Points, int IconType, LocalInventoryReward[] Rewards);
public sealed record LocalEventRewards(int EventId, LocalEventRewardGroup[] Groups);

public sealed partial class OfflineServer
{
    private LocalReply ClaimEventPointReward(LocalArguments? request)
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
        var reward = current.Rewards[0];
        var error = ApplyInventoryReward(updated, reward);
        if (error is not null) return error;
        var next = definition.Groups.Where(row => row.Step > step).OrderBy(row => row.Step).FirstOrDefault();
        progress["next_reward_group_id"] = next?.Id ?? 0;
        progress["step_number"] = next?.Step ?? step;
        Save(updated);
        return new(0, new()
        {
            ["event_id"] = request.Value.Value, ["total_point"] = points,
            ["step_number"] = progress["step_number"]!.DeepClone(), ["next_reward_group_id"] = next?.Id ?? 0,
            ["reward"] = new JsonObject { ["object_type"] = reward.Type, ["object_id"] = reward.Id,
                ["object_value"] = reward.Count, ["is_new"] = 0, ["unique_id"] = 0 },
            ["user_update"] = new JsonObject { ["gold"] = updated["gold"]!.DeepClone(),
                ["crystal"] = updated["crystal"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone() }
        });
    }
}
