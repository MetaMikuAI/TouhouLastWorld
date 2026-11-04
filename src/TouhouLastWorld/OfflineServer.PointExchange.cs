using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalPointExchange(int Id, int PointId, int Cost, int Limit, int ObjectType, int ObjectId,
    int Capacity, bool Active, LocalIntroPicture? Picture = null);

public sealed partial class OfflineServer
{
    private LocalReply ExchangeDrawPoints(LocalPointExchange? rule)
    {
        if (rule is null || rule.Id <= 0 || rule.PointId <= 0 || rule.Cost <= 0 || rule.Limit <= 0
            || rule.ObjectId <= 0 || rule.Capacity <= 0 || rule.ObjectType is not (9 or 10)
            || (rule.ObjectType == 9 && rule.ObjectId != unitTemplate["unit_id"]!.GetValue<int>())
            || (rule.ObjectType == 10 && rule.Picture?.Id != rule.ObjectId))
            return new(503, null, "Exchange definition unavailable.");
        if (!rule.Active) return new(409, null, "Exchange is not active.");
        var updated = user.DeepClone().AsObject();
        var points = updated["_local_gacha_points"] as JsonArray ?? new JsonArray();
        var counts = updated["_local_gacha_exchange_counts"] as JsonArray ?? new JsonArray();
        var balances = points.Where(row => row?["gacha_point_id"]?.GetValue<int>() == rule.PointId).ToArray();
        var receipts = counts.Where(row => row?["gacha_point_exchange_master_id"]?.GetValue<int>() == rule.Id).ToArray();
        if (balances.Length > 1 || receipts.Length > 1) return new(503, null, "Duplicate exchange state.");
        var balance = balances.FirstOrDefault()?["value"]?.GetValue<int>() ?? 0;
        var used = receipts.FirstOrDefault()?["count"]?.GetValue<int>() ?? 0;
        if (balance < 0 || used < 0) return new(503, null, "Invalid exchange state.");
        if (balance < rule.Cost || used >= rule.Limit) return new(409, null, "Insufficient points or exchange limit reached.");
        var isUnit = rule.ObjectType == 9;
        var kind = isUnit ? "unit" : "picture";
        var listName = kind + "_list";
        var idName = "user_" + kind + "_id";
        var sequenceName = "_local_" + kind + "_sequence";
        var owned = updated[kind]![listName]!.AsArray();
        var sequence = Math.Max(updated[sequenceName]?.GetValue<long>() ?? 0,
            owned.Select(row => row![idName]!.GetValue<long>()).DefaultIfEmpty().Max());
        if (owned.Count >= rule.Capacity || sequence < 0 || sequence == long.MaxValue)
            return new(409, null, "Inventory capacity exceeded.");
        var uniqueId = sequence + 1;
        var album = updated["album"]![listName]!.AsArray();
        var isNew = album.All(row => row?[kind + "_id"]?.GetValue<int>() != rule.ObjectId) ? 1 : 0;
        JsonObject item;
        if (isUnit)
        {
            item = unitTemplate.DeepClone().AsObject();
            item[idName] = uniqueId;
            item["is_new"] = 1;
            if (isNew == 1) album.Add(new JsonObject { ["unit_id"] = rule.ObjectId, ["talk_rank"] = 0,
                ["voice_set_id"] = 0, ["character_episode_ids"] = new JsonArray(), ["home_motion_ids"] = new JsonArray() });
        }
        else
        {
            item = new JsonObject { [idName] = uniqueId, ["picture_id"] = rule.ObjectId, ["spec_actualize"] = 0,
                ["potential_limitbreak"] = 0, ["equip_user_unit_id"] = 0L, ["is_protection"] = 0, ["is_new"] = 1,
                ["parameter1_value"] = rule.Picture!.Parameter1, ["parameter2_value"] = rule.Picture.Parameter2,
                ["picture_characteristic_group_master_id"] = rule.Picture.Characteristic };
            if (isNew == 1) album.Add(new JsonObject { ["picture_id"] = rule.ObjectId, ["potential_limitbreak"] = 0 });
        }
        owned.Add(item);
        updated[sequenceName] = uniqueId;
        balances[0]!["value"] = balance - rule.Cost;
        if (receipts.Length == 0) counts.Add(new JsonObject { ["gacha_point_exchange_master_id"] = rule.Id, ["count"] = used + 1 });
        else receipts[0]!["count"] = used + 1;
        if (counts.Parent is null) updated["_local_gacha_exchange_counts"] = counts;
        Save(updated);
        return new(0, new() { ["gacha_point"] = balance - rule.Cost, ["remaining_count"] = rule.Limit - used - 1,
            ["gain_object"] = new JsonObject { ["object_type"] = rule.ObjectType, ["object_id"] = rule.ObjectId,
                ["unique_id"] = uniqueId, ["is_new"] = isNew },
            ["user_update"] = new JsonObject { [kind] = updated[kind]!.DeepClone(), ["album"] = updated["album"]!.DeepClone() } });
    }
}
