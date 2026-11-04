using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalAutomaticRelease(int Id, int UnitId, int Level, int Stage, bool RequiresFullStats, bool FullStats);

public sealed partial class OfflineServer
{
    private LocalReply ReleaseAutomaticNode(LocalArguments? request)
    {
        if (request?.EntityId is null or <= 0 || request.Value is null or <= 0)
            return new(400, null, "Unit and release IDs required.");
        var rule = request.AutomaticRelease;
        if (rule is null || rule.Id != request.Value || rule.UnitId <= 0 || rule.Level < 0 || rule.Stage < 0)
            return new(503, null, "Automatic release definition unavailable.");
        var updated = user.DeepClone().AsObject();
        var unit = updated["unit"]!["unit_list"]!.AsArray().FirstOrDefault(entry => entry?["user_unit_id"]?.GetValue<long>() == request.EntityId);
        if (unit is null || unit["unit_id"]!.GetValue<int>() != rule.UnitId) return new(404, null, "Unit not found.");
        if (unit["release_functions"] is not JsonArray { Count: > 0 } released)
            return new(409, null, "Release progression has not started.");
        if (released.Any(id => id!.GetValue<int>() == rule.Id))
            return new(0, new() { ["user_update"] = new JsonObject { ["unit"] = updated["unit"]!.DeepClone() } });
        if (unit["level"]!.GetValue<int>() < rule.Level
            || (unit["reincarnation_stage"]?.GetValue<int>() ?? 0) < rule.Stage
            || rule.RequiresFullStats && !rule.FullStats)
            return new(409, null, "Release requirements not met.");
        released.Add(rule.Id);
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["unit"] = updated["unit"]!.DeepClone() } });
    }
}
