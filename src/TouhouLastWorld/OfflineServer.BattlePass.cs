using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalBattlePassPeriod(int Id, int InitialLevel);
public sealed record LocalBattlePassTask(int Id, int Required, int Experience, bool Active);
public sealed record LocalBattlePassClaim(int Id, int All, int PassId, bool Active,
    LocalBattlePassTask[] Tasks, Dictionary<int, int> Levels);
public sealed record LocalBattlePassLevelReward(int Level, LocalInventoryReward[]? Normal,
    LocalInventoryReward[]? Premium, LocalInventoryReward[]? Premium2);
public sealed record LocalBattlePassRewards(int PassId, bool Active, LocalBattlePassLevelReward[] Levels);

public sealed partial class OfflineServer
{
    private JsonObject? OpenBattlePassPeriod(LocalBattlePassPeriod? period)
    {
        if (period is null || period.Id <= 0 || period.InitialLevel < 0) return null;
        var samePeriod = user["battle_pass"]?["battle_pass_id"]?.GetValue<int>() == period.Id;
        if (!samePeriod || user["_local_battle_pass_top"] is not JsonObject)
        {
            var updated = user.DeepClone().AsObject();
            if (!samePeriod)
                updated["battle_pass"] = new JsonObject
                {
                    ["battle_pass_id"] = period.Id, ["is_premium"] = 0, ["is_premium_2"] = 0,
                    ["battle_pass_level"] = period.InitialLevel, ["battle_pass_exp"] = 0
                };
            updated["_local_battle_pass_top"] = QueryTemplates["battle_pass_top"].DeepClone();
            Save(updated);
        }
        return user["battle_pass"]!.DeepClone().AsObject();
    }

    private LocalReply ReceiveBattlePassRewards(LocalBattlePassRewards? request)
    {
        var updated = user.DeepClone().AsObject();
        if (request is null || !request.Active || request.PassId <= 0
            || updated["battle_pass"] is not JsonObject status || status["battle_pass_id"]?.GetValue<int>() != request.PassId
            || updated["_local_battle_pass_top"] is not JsonObject top)
            return new(503, null, "Stage reward state unavailable.");
        var level = status["battle_pass_level"]?.GetValue<int>() ?? -1;
        var fields = new[] { "normal_received_reward_level", "premium_received_reward_level_1", "premium_received_reward_level_2" };
        var enabled = new[] { true, (status["is_premium"]?.GetValue<int>() ?? 0) > 0, (status["is_premium_2"]?.GetValue<int>() ?? 0) > 0 };
        var result = new JsonObject { ["not_receive_message"] = "" };
        var rewards = new JsonArray();
        var changed = false;
        for (var track = 0; track < fields.Length; track++)
        {
            var field = fields[track];
            var received = top[field]?.GetValue<int>() ?? 0;
            if (received < 0 || level < received || level < 0) return new(503, null, "Invalid stage claim progress.");
            if (enabled[track])
            {
                var pending = request.Levels.Where(row => row.Level > received && row.Level <= level).OrderBy(row => row.Level).ToArray();
                if (pending.Length != level - received || pending.Where((row, index) => row.Level != received + index + 1).Any())
                    return new(503, null, "Stage reward levels unavailable.");
                foreach (var row in pending)
                {
                    var grants = track switch { 0 => row.Normal, 1 => row.Premium, _ => row.Premium2 };
                    if (grants is null) return new(503, null, "Stage reward group unsupported.");
                    foreach (var grant in grants)
                    {
                        var error = ApplyInventoryReward(updated, grant);
                        if (error is not null) return error;
                        rewards.Add(new JsonObject { ["object_id"] = grant.Id, ["object_type"] = grant.Type, ["object_value"] = grant.Count });
                    }
                }
                if (received != level) changed = true;
                received = level;
                top[field] = received;
            }
            result[field] = received;
        }
        if (changed) Save(updated);
        result["received_reward_list"] = rewards;
        result["user_update"] = new JsonObject { ["gold"] = updated["gold"]!.DeepClone(),
            ["crystal"] = updated["crystal"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone() };
        return new(0, result);
    }

    private LocalReply BattlePassOverview()
    {
        var body = QueryTemplates["battle_pass_top"].DeepClone().AsObject();
        if (user["_local_battle_pass_top"] is JsonObject saved)
            foreach (var field in body.Select(pair => pair.Key).ToArray())
                if (saved[field] is { } value) body[field] = value.DeepClone();
        return new(0, body);
    }

    private LocalReply ReceiveBattlePassMission(LocalBattlePassClaim? request)
    {
        if (request is null || request.All is < 0 or > 1 || (request.All == 0 ? request.Id <= 0 : request.Id != 0))
            return new(400, null, "Invalid stage mission claim.");
        var updated = user.DeepClone().AsObject();
        if (updated["battle_pass"] is not JsonObject status || request.PassId <= 0
            || status["battle_pass_id"]?.GetValue<int>() != request.PassId || !request.Active)
            return new(409, null, "Stage is unavailable.");
        if (updated["_local_battle_pass_top"]?["battle_pass_mission"] is not JsonObject missions
            || missions["clear_list"] is not JsonArray ready || missions["received_list"] is not JsonArray received
            || missions["unlock_list"] is not JsonArray)
            return new(503, null, "Stage mission state unavailable.");
        var exp = status["battle_pass_exp"]?.GetValue<int>() ?? -1;
        var level = status["battle_pass_level"]?.GetValue<int>() ?? -1;
        var levels = request.Levels.OrderBy(row => row.Key).ToArray();
        if (exp < 0 || level < 0 || levels.Length == 0 || !request.Levels.ContainsKey(level)
            || levels.Any(row => row.Key < 0 || row.Value < 0)
            || levels.Zip(levels.Skip(1)).Any(pair => pair.Second.Key != pair.First.Key + 1 || pair.Second.Value <= pair.First.Value)
            || levels.LastOrDefault(row => row.Value <= exp).Key != level || exp < request.Levels[level])
            return new(503, null, "Stage experience definition unavailable.");
        var selected = ready.Where(row => request.All == 1 || row?["id"]?.GetValue<int>() == request.Id).ToArray();
        if (selected.Length == 0 && request.All == 0) return new(404, null, "Stage mission is not claimable.");
        long total = exp;
        var ids = new HashSet<int>();
        foreach (var mission in selected)
        {
            var id = mission?["id"]?.GetValue<int>() ?? 0;
            var matches = request.Tasks.Where(row => row.Id == id).ToArray();
            if (id <= 0 || !ids.Add(id) || received.Any(row => row?["id"]?.GetValue<int>() == id))
                return new(409, null, "Stage mission already received or duplicated.");
            if (matches.Length != 1) return new(503, null, "Stage mission definition unavailable.");
            var rule = matches[0];
            if (!rule.Active || rule.Required <= 0 || rule.Experience <= 0
                || mission!["battle_pass_id"]?.GetValue<int>() != request.PassId
                || mission["complete_value"]?.GetValue<int>() != rule.Required
                || mission["reward_battle_pass_exp"]?.GetValue<int>() != rule.Experience
                || mission["current_value"]?.GetValue<int>() is not int current || current < rule.Required)
                return new(503, null, "Stage progress or reward definition unavailable.");
            total += rule.Experience;
            if (total > int.MaxValue) return new(409, null, "Stage experience exceeds capacity.");
            ready.Remove(mission);
            received.Add(mission);
        }
        status["battle_pass_exp"] = (int)total;
        status["battle_pass_level"] = levels.Last(row => row.Value <= total).Key;
        if (selected.Length > 0) Save(updated);
        return new(0, new() { ["battle_pass_mission"] = missions.DeepClone(), ["reward_list"] = new JsonArray(),
            ["user_update"] = new JsonObject { ["battle_pass"] = status.DeepClone() } });
    }
}
