using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalDestinyMaterial(int ItemId, int Count, int Value);
public sealed record LocalDestinyGrowth(int UnitId, int Current, int Maximum, LocalDestinyMaterial[] Materials);

public sealed partial class OfflineServer
{
    private LocalReply IncreaseDestiny(LocalArguments? request)
    {
        if (request?.EntityId is null or <= 0) return new(400, null, "Invalid unit.");
        if (request.Ids is { Length: > 0 }) return new(503, null, "Unit materials not supported.");
        var rule = request.DestinyGrowth;
        if (rule is null || rule.UnitId <= 0 || rule.Current < 0 || rule.Maximum <= 0
            || rule.Materials is not { Length: 3 }) return new(503, null, "Growth definition unavailable.");
        if (rule.Materials.Any(material => material.Count < 0) || rule.Materials.All(material => material.Count == 0))
            return new(400, null, "Invalid material count.");
        if (rule.Materials.Any(material => material.Count > 0 && (material.ItemId <= 0 || material.Value <= 0)))
            return new(503, null, "Material definition unavailable.");
        var updated = user.DeepClone().AsObject();
        var unit = updated["unit"]!["unit_list"]!.AsArray()
            .FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == request.EntityId);
        if (unit is null) return new(404, null, "Owned unit not found.");
        if (unit["unit_id"]!.GetValue<int>() != rule.UnitId || unit["destiny_value"]!.GetValue<int>() != rule.Current)
            return new(409, null, "Growth definition is stale.");
        if (rule.Current >= rule.Maximum) return new(409, null, "Growth limit reached.");
        if (updated["_local_idle"]?["unit_slot_list"] is JsonArray slots && slots.Any(slot =>
                slot?["user_object_id"]?.GetValue<long>() == request.EntityId && slot["content"]?.GetValue<int>() == 2))
            return new(409, null, "Unit growth is already in progress.");
        long value = rule.Current;
        foreach (var material in rule.Materials.Where(material => material.Count > 0))
        {
            var item = updated["item"]!["item_list"]!.AsArray()
                .FirstOrDefault(entry => entry!["item_id"]!.GetValue<int>() == material.ItemId);
            if (item is null || item["num"]!.GetValue<int>() < material.Count)
                return new(409, null, "Insufficient materials.");
            item["num"] = item["num"]!.GetValue<int>() - material.Count;
            value = Math.Min(rule.Maximum, value + (long)material.Count * material.Value);
        }
        unit["destiny_value"] = (int)Math.Max(1, value);
        Save(updated);
        return new(0, new()
        {
            ["user_update"] = new JsonObject { ["unit"] = updated["unit"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone() }
        });
    }
}
