using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalStatUnlock(int CurrentStep, int NextStep, int Cost, int RequiredValue, bool Enabled);

public sealed partial class OfflineServer
{
    private LocalReply UnlockExtraStat(LocalArguments? request)
    {
        if (request?.EntityId is null or <= 0 || request.Value is null or < 1 or > 6)
            return new(400, null, "Invalid unit or stat.");
        var rule = request.StatUnlock;
        if (rule is null || rule.CurrentStep < 0 || rule.CurrentStep == int.MaxValue || rule.NextStep != rule.CurrentStep + 1
            || rule.Cost < 0 || rule.RequiredValue < 0)
            return new(503, null, "Stat unlock definition unavailable.");
        if (!rule.Enabled) return new(403, null, "Stat unlock is disabled.");
        var updated = user.DeepClone().AsObject();
        var unit = updated["unit"]!["unit_list"]!.AsArray()
            .FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == request.EntityId);
        if (unit is null) return new(404, null, "Owned unit not found.");
        var field = $"type_{request.Value}";
        if (unit["strong_fragrant_wood_limit_info"] is not JsonObject limits
            || unit["strong_fragrant_wood"] is not JsonObject stats)
            return new(503, null, "Extra stat state unavailable.");
        if (limits[field]?.GetValue<int>() != rule.CurrentStep || stats[field]?.GetValue<int>() != rule.RequiredValue)
            return new(409, null, "Stat must reach its current limit before unlocking.");
        var item = updated["item"]!["item_list"]!.AsArray().FirstOrDefault(entry => entry!["item_id"]!.GetValue<int>() == 410);
        if (rule.Cost > 0 && (item is null || item["num"]!.GetValue<int>() < rule.Cost))
            return new(409, null, "Insufficient unlock materials.");
        if (item is not null) item["num"] = item["num"]!.GetValue<int>() - rule.Cost;
        limits[field] = rule.NextStep;
        limits[BasicStatFields[request.Value.Value - 1][4..]] = rule.NextStep;
        limits["limitbreak_params"] = new JsonObject(Enumerable.Range(1, 6)
            .Select(stat => new KeyValuePair<string, JsonNode?>(stat.ToString(), limits[$"type_{stat}"]!.DeepClone())));
        Save(updated);
        return new(0, new()
        {
            ["user_update"] = new JsonObject { ["unit"] = updated["unit"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone() }
        });
    }
}
