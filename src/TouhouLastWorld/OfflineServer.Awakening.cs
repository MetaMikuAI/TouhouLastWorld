using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalAwakening(int CurrentRare, int NextRare, int PieceCost);

public sealed partial class OfflineServer
{
    private LocalReply AwakenUnit(LocalArguments? request)
    {
        if (request?.EntityId is null or <= 0) return new(400, null, "Invalid unit.");
        var rule = request.Awakening;
        if (rule is null || rule.CurrentRare <= 0 || rule.CurrentRare == int.MaxValue
            || rule.NextRare != rule.CurrentRare + 1 || rule.PieceCost < 0)
            return new(503, null, "Awakening definition unavailable.");
        var updated = user.DeepClone().AsObject();
        var unit = updated["unit"]!["unit_list"]!.AsArray()
            .FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == request.EntityId);
        if (unit is null) return new(404, null, "Owned unit not found.");
        if (unit["rare"]!.GetValue<int>() != rule.CurrentRare)
            return new(409, null, "Awakening definition is stale.");
        if (updated["_local_idle"]?["unit_slot_list"] is JsonArray slots && slots.Any(slot =>
                slot?["user_object_id"]?.GetValue<long>() == request.EntityId && slot["content"]?.GetValue<int>() == 4))
            return new(409, null, "Unit awakening is already in progress.");
        var piece = updated["piece"]!.GetValue<int>();
        if (piece < rule.PieceCost) return new(409, null, "Insufficient pieces.");
        updated["piece"] = piece - rule.PieceCost;
        unit["rare"] = rule.NextRare;
        if (rule.PieceCost > 0) updated[UnitAwakeningMilestone] = true;
        Save(updated);
        return new(0, new()
        {
            ["user_update"] = new JsonObject { ["piece"] = piece - rule.PieceCost, ["unit"] = updated["unit"]!.DeepClone() }
        });
    }
}
