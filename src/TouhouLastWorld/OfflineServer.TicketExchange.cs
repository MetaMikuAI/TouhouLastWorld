using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalTicketExchange(int ExchangeId, int TicketId, int ItemId, int Cost, int Count,
    int PictureId, int Capacity, bool Active, int Parameter1, int Parameter2, int Characteristic);

public sealed partial class OfflineServer
{
    private LocalReply ExchangeTicket(LocalArguments? request)
    {
        var rule = request?.TicketExchange;
        if (rule is null || rule.ExchangeId <= 0 || rule.TicketId <= 0 || rule.ItemId <= 0 || rule.PictureId <= 0
            || rule.Cost <= 0 || rule.Capacity <= 0) return new(503, null, "Exchange definition unavailable.");
        if (rule.Count <= 0) return new(400, null, "Positive exchange quantity required.");
        if (!rule.Active) return new(409, null, "Exchange is not active.");
        var updated = user.DeepClone().AsObject();
        var pictures = updated["picture"]!["picture_list"]!.AsArray();
        if ((long)pictures.Count + rule.Count > rule.Capacity) return new(409, null, "Equipment capacity exceeded.");
        var item = updated["item"]!["item_list"]!.AsArray().FirstOrDefault(entry => entry?["item_id"]?.GetValue<int>() == rule.ItemId);
        var cost = (long)rule.Cost * rule.Count;
        if (item is null || item["num"]!.GetValue<int>() < cost) return new(409, null, "Insufficient exchange items.");
        var nextId = Math.Max(updated["_local_picture_sequence"]?.GetValue<long>() ?? 0,
            pictures.Select(entry => entry!["user_picture_id"]!.GetValue<long>()).DefaultIfEmpty().Max());
        if (nextId < 0 || nextId > long.MaxValue - rule.Count) return new(409, null, "Equipment identifier limit exceeded.");
        var album = updated["album"]!["picture_list"]!.AsArray();
        var isNew = album.All(entry => entry?["picture_id"]?.GetValue<int>() != rule.PictureId) ? 1 : 0;
        for (var index = 0; index < rule.Count; index++)
            pictures.Add(new JsonObject
            {
                ["user_picture_id"] = nextId + index + 1, ["picture_id"] = rule.PictureId,
                ["spec_actualize"] = 0, ["potential_limitbreak"] = 0, ["equip_user_unit_id"] = 0L,
                ["is_protection"] = 0, ["is_new"] = 1, ["parameter1_value"] = rule.Parameter1,
                ["parameter2_value"] = rule.Parameter2, ["picture_characteristic_group_master_id"] = rule.Characteristic
            });
        if (isNew == 1) album.Add(new JsonObject { ["picture_id"] = rule.PictureId, ["potential_limitbreak"] = 0 });
        item["num"] = (int)(item["num"]!.GetValue<int>() - cost);
        updated["_local_picture_sequence"] = nextId + rule.Count;
        Save(updated);
        return new(0, new()
        {
            ["reward_object"] = new JsonObject { ["object_type"] = 10, ["object_id"] = rule.PictureId,
                ["unique_id"] = nextId + 1, ["is_new"] = isNew },
            ["user_update"] = new JsonObject { ["picture"] = updated["picture"]!.DeepClone(),
                ["item"] = updated["item"]!.DeepClone(), ["album"] = updated["album"]!.DeepClone() }
        });
    }
}
