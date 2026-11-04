using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalStatMaterial(int ItemId, int Count, int Stat, int CostPerPoint, int Current, int Maximum);

public sealed partial class OfflineServer
{
    private static readonly string[] BasicStatFields =
        ["add_life_point", "add_yang_attack", "add_yang_defense", "add_yin_attack", "add_yin_defense", "add_speed"];

    private LocalReply StrengthenUnit(LocalArguments? request, bool extra = false)
    {
        if (request?.EntityId is null or <= 0 || request.StatMaterials is not { Length: > 0 and <= 6 } materials
            || materials.Any(entry => entry.ItemId <= 0 || entry.Count <= 0)
            || materials.Select(entry => entry.ItemId).Distinct().Count() != materials.Length)
            return new(400, null, "Invalid strengthening materials.");
        if (materials.Any(entry => entry.Stat is < 1 or > 6 || entry.CostPerPoint <= 0 || entry.Current < 0 || entry.Maximum < entry.Current)
            || materials.Select(entry => entry.Stat).Distinct().Count() != materials.Length)
            return new(503, null, "Strengthening definition unavailable.");
        var updated = user.DeepClone().AsObject();
        var unit = updated["unit"]!["unit_list"]!.AsArray()
            .FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == request.EntityId);
        if (unit is null) return new(404, null, "Owned unit not found.");
        var stats = extra ? unit["strong_fragrant_wood"] as JsonObject : unit as JsonObject;
        if (stats is null) return new(503, null, "Strengthening state unavailable.");
        var items = updated["item"]!["item_list"]!.AsArray();
        foreach (var material in materials)
        {
            var field = extra ? $"type_{material.Stat}" : BasicStatFields[material.Stat - 1];
            if (extra && material.CostPerPoint != 1) return new(503, null, "Invalid extra strengthening cost.");
            if (stats[field]?.GetValue<int>() != material.Current)
                return new(409, null, "Strengthening state is stale.");
            if (material.Count % material.CostPerPoint != 0)
                return new(400, null, "Material count does not match point cost.");
            var gain = material.Count / material.CostPerPoint;
            if (gain > material.Maximum - material.Current)
                return new(409, null, "Strengthening exceeds current limit.");
            var item = items.FirstOrDefault(entry => entry!["item_id"]!.GetValue<int>() == material.ItemId);
            if (item is null || item["num"]!.GetValue<int>() < material.Count)
                return new(409, null, "Insufficient strengthening materials.");
            item["num"] = item["num"]!.GetValue<int>() - material.Count;
            stats[field] = material.Current + gain;
            if (extra) stats[BasicStatFields[material.Stat - 1]] = material.Current + gain;
        }
        Save(updated);
        return new(0, new()
        {
            ["user_update"] = new JsonObject { ["unit"] = updated["unit"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone() }
        });
    }
}
