using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private LocalReply AllocatePoints(LocalPointAllocation? request)
    {
        if (request is null || request.UnitId <= 0 || request.Targets is not { Length: > 0 and <= 9 }
            || request.Targets.Any(entry => entry.Type is < 1 or > 9 || entry.Step is < 1 or > int.MaxValue)
            || request.Targets.Select(entry => entry.Type).Distinct().Count() != request.Targets.Length)
            return new(400, null, "Distinct valid allocation targets required.");
        if (request.Rules is null || request.Rules.Length == 0) return new(503, null, "Allocation rules unavailable.");
        var updated = user.DeepClone().AsObject();
        var unit = updated["unit"]!["unit_list"]!.AsArray()
            .FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == request.UnitId);
        if (unit is null) return new(404, null, "Owned unit not found.");
        if (unit["point_allocation_info"] is not JsonObject points)
            return new(409, null, "Point allocation state unavailable.");
        var remaining = points["pa_points"]!.GetValue<int>();
        var used = points["pa_total_used_points"]!.GetValue<int>();
        if (remaining < 0 || used < 0) return new(409, null, "Invalid point balance.");
        decimal cost = 0;
        foreach (var target in request.Targets)
        {
            var field = $"pa_type_{target.Type}";
            var current = points[field]?.GetValue<int>();
            if (current is null or < 0 || target.Step < current)
                return new(409, null, "Allocation cannot decrease without reset.");
            var path = request.Rules.Where(rule => rule.Type == target.Type && rule.Step > current && rule.Step <= target.Step)
                .OrderBy(rule => rule.Step).ToArray();
            if (path.Length != target.Step - current) return new(503, null, "Allocation path unavailable.");
            long nextStep = current.Value + 1L;
            foreach (var rule in path)
            {
                if (rule.Step != nextStep++ || rule.Cost < 0 || rule.RequiredUsed < 0)
                    return new(503, null, "Invalid allocation rule.");
                if (rule.RequiredUsed > used) return new(403, null, "Allocation prerequisite not met.");
                cost += rule.Cost;
            }
            points[field] = (int)target.Step;
        }
        if (cost > remaining || cost + used > int.MaxValue) return new(409, null, "Insufficient or invalid point balance.");
        points["pa_points"] = remaining - (int)cost;
        points["pa_total_used_points"] = used + (int)cost;
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["unit"] = updated["unit"]!.DeepClone() } });
    }

    private LocalReply ResetPoints(LocalPointReset? request)
    {
        if (request is null || request.GoldCost < 0) return new(503, null, "Point reset settings unavailable.");
        if (request.UnitId <= 0) return new(400, null, "Positive unit ID required.");
        var updated = user.DeepClone().AsObject();
        var unit = updated["unit"]!["unit_list"]!.AsArray()
            .FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == request.UnitId);
        if (unit is null) return new(404, null, "Owned unit not found.");
        if (unit["point_allocation_info"] is not JsonObject points)
            return new(409, null, "Point allocation state unavailable.");
        var remaining = points["pa_points"]!.GetValue<int>();
        var used = points["pa_total_used_points"]!.GetValue<int>();
        if (remaining < 0 || used < 0 || (long)remaining + used > int.MaxValue)
            return new(409, null, "Invalid point balance.");
        var types = Enumerable.Range(1, 9).Select(index => $"pa_type_{index}").ToArray();
        if (types.Any(field => points[field] is null || points[field]!.GetValue<int>() < 0))
            return new(409, null, "Invalid point allocation.");
        if (used == 0)
            return types.All(field => points[field]!.GetValue<int>() == 0)
                ? new(0, new()) : new(409, null, "Allocation total does not match its entries.");
        var cost = request.FreeReset || points["is_pa_reset_not_free"]!.GetValue<int>() == 0 ? 0 : request.GoldCost;
        var gold = updated["gold"]!.GetValue<int>();
        if (gold < cost) return new(409, null, "Insufficient local balance.");
        foreach (var field in types) points[field] = 0;
        points["pa_points"] = remaining + used;
        points["pa_total_used_points"] = 0;
        points["is_pa_reset_not_free"] = 1;
        updated["gold"] = gold - cost;
        Save(updated);
        return new(0, new()
        {
            ["user_update"] = new JsonObject { ["unit"] = updated["unit"]!.DeepClone(), ["gold"] = gold - cost }
        });
    }
}
