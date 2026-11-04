using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalPassTask(int Id, int Experience, int[] Levels, int[] VipLevels);

public sealed partial class OfflineServer
{
    private LocalReply PassOverview() => new(0, new()
    {
        ["user_pass_card"] = user["_local_pass_card"]?.DeepClone() ?? EmptyPassCard()
    });

    private LocalReply CollectPassExperience(LocalArguments? request)
    {
        if (request?.Value is null or <= 0) return new(400, null, "Positive task ID required.");
        var rule = request.PassTask;
        if (rule is null || rule.Id != request.Value || rule.Experience <= 0 || rule.Levels.Length == 0
            || rule.Levels.Concat(rule.VipLevels).Any(level => level is < 1 or > 80))
            return new(503, null, "Pass task definition unavailable.");
        var updated = user.DeepClone().AsObject();
        if (updated["_local_pass_card"] is not JsonObject pass || pass["tasks"] is not JsonArray tasks)
            return new(409, null, "Local pass progress unavailable.");
        var task = tasks.FirstOrDefault(entry => entry?["task_id"]?.GetValue<int>() == rule.Id);
        if (task?["can_cellable"]?.GetValue<int>() is not > 0)
            return new(409, null, "No completed task available to claim.");
        var level = pass["level"]?.GetValue<int>() ?? 0;
        var experience = pass["current_exp"]?.GetValue<int>() ?? -1;
        if (level is < 1 or > 80 || experience is < 0 or >= 150
            || pass["had_got_free_reward_level"] is not JsonArray freeReceived
            || pass["had_got_vip_reward_level"] is not JsonArray vipReceived)
            return new(409, null, "Invalid local pass progress.");
        var total = (long)experience + rule.Experience;
        var next = (int)Math.Min(80L, level + total / 150);
        pass["level"] = next;
        pass["current_exp"] = next == 80 ? 0 : (int)(total % 150);
        task["can_cellable"] = task["can_cellable"]!.GetValue<int>() - 1;
        pass["is_task"] = tasks.Any(entry => entry?["can_cellable"]?.GetValue<int>() > 0) ? 1 : 0;
        var free = rule.Levels.Distinct().Where(value => value <= next && !freeReceived.Any(id => id!.GetValue<int>() == value)).Order().ToArray();
        var vip = pass["is_vip"]?.GetValue<int>() == 1
            ? rule.VipLevels.Distinct().Where(value => value <= next && !vipReceived.Any(id => id!.GetValue<int>() == value)).Order().ToArray()
            : Array.Empty<int>();
        pass["free_rewards"] = new JsonArray(free.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
        pass["vip_rewards"] = new JsonArray(vip.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
        pass["is_reward"] = free.Length > 0 || vip.Length > 0 ? 1 : 0;
        Save(updated);
        return new(0, new() { ["user_pass_card"] = pass.DeepClone() });
    }
}
