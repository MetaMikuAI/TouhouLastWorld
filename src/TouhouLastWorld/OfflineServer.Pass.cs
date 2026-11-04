using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalPassTask(int Id, int Experience, int[] Levels, int[] VipLevels);
public sealed record LocalPassReward(int Level, LocalInventoryReward[] Free, LocalInventoryReward[] Vip);
public sealed record LocalPassGoods(int Id, int Cost, int Limit, LocalInventoryReward Reward);

public sealed partial class OfflineServer
{
    private LocalReply BuyPassGoods(LocalArguments? request)
    {
        if (request?.Value is null or <= 0) return new(400, null, "Positive goods ID required.");
        var rule = request.PassGoods;
        if (rule is null || rule.Id != request.Value || rule.Cost < 0 || rule.Limit <= 0)
            return new(503, null, "Goods definition unavailable.");
        var updated = user.DeepClone().AsObject();
        if (updated["_local_pass_card"] is not JsonObject pass || pass["pass_card_goods"] is not JsonArray goods)
            return new(409, null, "Local shop state unavailable.");
        var purchased = goods.FirstOrDefault(entry => entry?["goods_id"]?.GetValue<int>() == rule.Id);
        var count = purchased?["buy_times"]?.GetValue<int>() ?? 0;
        if (count < 0 || count >= rule.Limit) return new(409, null, "Goods purchase limit reached.");
        var coins = pass["coin"]?.GetValue<int>() ?? 0;
        if (coins < rule.Cost) return new(20010, null, "Insufficient local coins.");
        var error = ApplyInventoryReward(updated, rule.Reward);
        if (error is not null) return error;
        pass["coin"] = coins - rule.Cost;
        if (purchased is null) goods.Add(new JsonObject { ["goods_id"] = rule.Id, ["buy_times"] = 1 });
        else purchased["buy_times"] = count + 1;
        Save(updated);
        return new(0, new()
        {
            ["user_pass_card"] = pass.DeepClone(),
            ["user_update"] = new JsonObject { ["gold"] = updated["gold"]!.DeepClone(),
                ["crystal"] = updated["crystal"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone() }
        });
    }

    private LocalReply BuyPassLevels(LocalArguments? request)
    {
        if (request?.Value is null or <= 0 || request.Value % 20 != 0)
            return new(400, null, "Level purchase requires multiples of twenty.");
        var rules = request.PassRewards;
        if (rules is null || rules.Length == 0 || rules.Any(row => row.Level is < 1 or > 80))
            return new(503, null, "Pass level definitions unavailable.");
        var updated = user.DeepClone().AsObject();
        if (updated["_local_pass_card"] is not JsonObject pass
            || pass["had_got_free_reward_level"] is not JsonArray freeReceived
            || pass["had_got_vip_reward_level"] is not JsonArray vipReceived)
            return new(409, null, "Local pass progress unavailable.");
        var current = pass["level"]?.GetValue<int>() ?? 0;
        var levels = request.Value.Value / 20;
        if (current is < 1 or >= 80 || levels > 80 - current)
            return new(409, null, "Pass level limit exceeded.");
        var orb = updated["orb"]!;
        var free = orb["free"]!.GetValue<long>();
        var purchased = orb["purchased"]!.GetValue<long>();
        if (free < 0 || purchased < 0 || (decimal)free + purchased < request.Value.Value)
            return new(20000, null, "Insufficient local balance.");
        var freeSpent = Math.Min(free, request.Value.Value);
        orb["free"] = free - freeSpent;
        orb["purchased"] = purchased - (request.Value.Value - freeSpent);
        pass["level"] = current + levels;
        var vip = pass["is_vip"]?.GetValue<int>() == 1;
        pass["is_reward"] = rules.Any(row => row.Level <= current + levels &&
            (row.Free.Length > 0 && !freeReceived.Any(id => id!.GetValue<int>() == row.Level)
             || vip && row.Vip.Length > 0 && !vipReceived.Any(id => id!.GetValue<int>() == row.Level))) ? 1 : 0;
        pass["free_rewards"] = new JsonArray();
        pass["vip_rewards"] = new JsonArray();
        Save(updated);
        return new(0, new() { ["user_pass_card"] = pass.DeepClone(),
            ["user_update"] = new JsonObject { ["orb"] = orb.DeepClone() } });
    }

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
        pass["free_rewards"] = new JsonArray();
        pass["vip_rewards"] = new JsonArray();
        pass["is_reward"] = free.Length > 0 || vip.Length > 0 ? 1 : 0;
        Save(updated);
        return new(0, new() { ["user_pass_card"] = pass.DeepClone() });
    }

    private LocalReply CollectPassRewards(LocalArguments? request)
    {
        if (request?.Value is not (0 or 1) || request.Value == 0 && (request.EntityId is null or < 1 or > 80 || request.Text is not ("free" or "vip")))
            return new(400, null, "Invalid reward selection.");
        var rules = request.PassRewards;
        if (rules is null || rules.Length == 0 || rules.Any(row => row.Level is < 1 or > 80)
            || rules.Select(row => row.Level).Distinct().Count() != rules.Length)
            return new(503, null, "Pass rewards unavailable.");
        var updated = user.DeepClone().AsObject();
        if (updated["_local_pass_card"] is not JsonObject pass
            || pass["had_got_free_reward_level"] is not JsonArray freeReceived
            || pass["had_got_vip_reward_level"] is not JsonArray vipReceived)
            return new(409, null, "Local pass progress unavailable.");
        var level = pass["level"]?.GetValue<int>() ?? 0;
        var vip = pass["is_vip"]?.GetValue<int>() == 1;
        var freeClaims = new JsonArray();
        var vipClaims = new JsonArray();
        foreach (var row in rules.OrderBy(row => row.Level))
        {
            if (row.Level > level || request.Value == 0 && row.Level != request.EntityId) continue;
            foreach (var kind in new[] { "free", "vip" })
            {
                if (request.Value == 0 && request.Text != kind || kind == "vip" && !vip) continue;
                var received = kind == "free" ? freeReceived : vipReceived;
                var rewards = kind == "free" ? row.Free : row.Vip;
                if (rewards.Length == 0 || received.Any(id => id!.GetValue<int>() == row.Level)) continue;
                foreach (var reward in rewards)
                {
                    var error = ApplyInventoryReward(updated, reward);
                    if (error is not null) return error;
                }
                received.Add(row.Level);
                (kind == "free" ? freeClaims : vipClaims).Add(row.Level);
            }
        }
        if (request.Value == 0 && freeClaims.Count + vipClaims.Count == 0)
            return new(409, null, "Reward unavailable or already received.");
        pass["is_reward"] = rules.Any(row => row.Level <= level &&
            (row.Free.Length > 0 && !freeReceived.Any(id => id!.GetValue<int>() == row.Level)
             || vip && row.Vip.Length > 0 && !vipReceived.Any(id => id!.GetValue<int>() == row.Level))) ? 1 : 0;
        pass["free_rewards"] = new JsonArray();
        pass["vip_rewards"] = new JsonArray();
        if (freeClaims.Count + vipClaims.Count > 0) Save(updated);
        var response = pass.DeepClone().AsObject();
        response["free_rewards"] = freeClaims;
        response["vip_rewards"] = vipClaims;
        return new(0, new()
        {
            ["user_pass_card"] = response,
            ["user_update"] = new JsonObject { ["gold"] = updated["gold"]!.DeepClone(),
                ["crystal"] = updated["crystal"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone() }
        });
    }
}
