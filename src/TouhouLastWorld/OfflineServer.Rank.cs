using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalRankMaterial(int UnitId, int Rank, int ObjectType, int ObjectId, int Count);

public sealed partial class OfflineServer
{
    private LocalReply SetRankMaterial(LocalArguments? request)
    {
        if (request?.EntityId is null or <= 0 || request.Value is null or < 1 or > 6)
            return new(400, null, "Invalid unit or material slot.");
        var rule = request.RankMaterial;
        if (rule is null || rule.UnitId <= 0 || rule.Rank <= 0 || rule.Count <= 0)
            return new(503, null, "Rank material definition unavailable.");
        if (rule.ObjectType is not (3 or 12) || request.Ids is { Length: > 0 })
            return new(503, null, "Material type not supported.");
        var updated = user.DeepClone().AsObject();
        var unit = updated["unit"]!["unit_list"]!.AsArray()
            .FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == request.EntityId);
        if (unit is null) return new(404, null, "Owned unit not found.");
        if (unit["unit_id"]!.GetValue<int>() != rule.UnitId || unit["rank"]!.GetValue<int>() != rule.Rank)
            return new(409, null, "Rank material definition is stale.");
        var field = $"is_set_rank_promote_slot{request.Value}";
        if (unit[field]?.GetValue<int>() != 0) return new(409, null, "Material slot unavailable or already filled.");
        var changed = new JsonObject();
        if (rule.ObjectType == 3)
        {
            var gold = updated["gold"]!.GetValue<int>();
            if (gold < rule.Count) return new(409, null, "Insufficient gold.");
            updated["gold"] = gold - rule.Count;
            changed["gold"] = gold - rule.Count;
        }
        else
        {
            if (rule.ObjectId <= 0) return new(503, null, "Material item unavailable.");
            var item = updated["item"]!["item_list"]!.AsArray()
                .FirstOrDefault(entry => entry!["item_id"]!.GetValue<int>() == rule.ObjectId);
            if (item is null || item["num"]!.GetValue<int>() < rule.Count)
                return new(409, null, "Insufficient material inventory.");
            item["num"] = item["num"]!.GetValue<int>() - rule.Count;
            changed["item"] = updated["item"]!.DeepClone();
        }
        unit[field] = 1;
        changed["unit"] = updated["unit"]!.DeepClone();
        Save(updated);
        return new(0, new() { ["user_update"] = changed });
    }
}
