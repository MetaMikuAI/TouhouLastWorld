using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalBattlePassTask(int Id, int Required, int Experience, bool Active);
public sealed record LocalBattlePassClaim(int Id, int All, int PassId, bool Active,
    LocalBattlePassTask[] Tasks, Dictionary<int, int> Levels);

public sealed partial class OfflineServer
{
    private LocalReply BattlePassOverview()
    {
        var body = QueryTemplates["battle_pass_top"].DeepClone().AsObject();
        if (user["_local_battle_pass_top"] is JsonObject saved) body = saved.DeepClone().AsObject();
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
