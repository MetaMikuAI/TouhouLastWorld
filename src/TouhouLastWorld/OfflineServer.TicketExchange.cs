using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalTicketExchange(int ExchangeId, int TicketId, int ItemId, int Cost, int Count,
    int ObjectId, int Capacity, bool Active, int Parameter1 = 0, int Parameter2 = 0, int Characteristic = 0,
    int ObjectType = 10, int CostumeId = 0);

public sealed partial class OfflineServer
{
    private LocalReply ExchangeTicket(LocalArguments? request)
    {
        var rule = request?.TicketExchange;
        if (rule is null || rule.ExchangeId <= 0 || rule.TicketId <= 0 || rule.ItemId <= 0 || rule.ObjectId <= 0
            || rule.Cost <= 0 || rule.Capacity <= 0 || rule.ObjectType is not (9 or 10)
            || (rule.ObjectType == 9 && rule.CostumeId <= 0)) return new(503, null, "Exchange definition unavailable.");
        var isUnit = rule.ObjectType == 9;
        if (rule.Count <= 0 || (isUnit && rule.Count != 1)) return new(400, null, "Invalid exchange quantity.");
        if (!rule.Active) return new(409, null, "Exchange is not active.");
        var updated = user.DeepClone().AsObject();
        var kind = isUnit ? "unit" : "picture";
        var listName = kind + "_list";
        var objectIdName = kind + "_id";
        var idName = "user_" + objectIdName;
        var sequenceName = "_local_" + kind + "_sequence";
        var owned = updated[kind]![listName]!.AsArray();
        if ((long)owned.Count + rule.Count > rule.Capacity) return new(409, null, "Inventory capacity exceeded.");
        var item = updated["item"]!["item_list"]!.AsArray().FirstOrDefault(entry => entry?["item_id"]?.GetValue<int>() == rule.ItemId);
        var cost = (long)rule.Cost * rule.Count;
        if (item is null || item["num"]!.GetValue<int>() < cost) return new(409, null, "Insufficient exchange items.");
        var sequence = Math.Max(updated[sequenceName]?.GetValue<long>() ?? 0,
            owned.Select(entry => entry![idName]!.GetValue<long>()).DefaultIfEmpty().Max());
        if (sequence < 0 || sequence > long.MaxValue - rule.Count) return new(409, null, "Inventory identifier limit exceeded.");
        var album = updated["album"]![listName]!.AsArray();
        var isNew = album.All(entry => entry?[objectIdName]?.GetValue<int>() != rule.ObjectId) ? 1 : 0;
        if (isUnit)
        {
            var unit = unitTemplate.DeepClone().AsObject();
            unit[idName] = sequence + 1;
            unit[objectIdName] = rule.ObjectId;
            unit["equip_costume_id"] = rule.CostumeId;
            unit["is_new"] = 1;
            unit["last_motivation_at"] = DateTimeOffset.UtcNow.ToString("O");
            owned.Add(unit);
            if (isNew == 1) album.Add(new JsonObject { [objectIdName] = rule.ObjectId, ["talk_rank"] = 0,
                ["voice_set_id"] = 0, ["character_episode_ids"] = new JsonArray(), ["home_motion_ids"] = new JsonArray() });
        }
        else
        {
            for (var index = 0; index < rule.Count; index++) owned.Add(new JsonObject
            {
                [idName] = sequence + index + 1, [objectIdName] = rule.ObjectId,
                ["spec_actualize"] = 0, ["potential_limitbreak"] = 0, ["equip_user_unit_id"] = 0L,
                ["is_protection"] = 0, ["is_new"] = 1, ["parameter1_value"] = rule.Parameter1,
                ["parameter2_value"] = rule.Parameter2, ["picture_characteristic_group_master_id"] = rule.Characteristic
            });
            if (isNew == 1) album.Add(new JsonObject { [objectIdName] = rule.ObjectId, ["potential_limitbreak"] = 0 });
        }
        item["num"] = (int)(item["num"]!.GetValue<int>() - cost);
        updated[sequenceName] = sequence + rule.Count;
        Save(updated);
        return new(0, new()
        {
            ["reward_object"] = new JsonObject { ["object_type"] = rule.ObjectType, ["object_id"] = rule.ObjectId,
                ["unique_id"] = sequence + 1, ["is_new"] = isNew },
            ["user_update"] = new JsonObject { [kind] = updated[kind]!.DeepClone(),
                ["item"] = updated["item"]!.DeepClone(), ["album"] = updated["album"]!.DeepClone() }
        });
    }
}
