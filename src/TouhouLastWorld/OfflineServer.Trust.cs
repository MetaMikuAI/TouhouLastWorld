using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private LocalReply ReceiveLectureTrust(LocalArguments? request)
    {
        if (request?.EntityId is null or <= 0) return new(400, null, "Invalid unit.");
        if (request.Value is null or <= 0) return new(503, null, "Trust limit unavailable.");
        var updated = user.DeepClone().AsObject();
        var unit = updated["unit"]!["unit_list"]!.AsArray()
            .FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == request.EntityId);
        if (unit is null) return new(404, null, "Owned unit not found.");
        var room = updated["lecture_room"]!;
        var slot = room["unit"]!["unit_list"]!.AsArray()
            .FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == request.EntityId);
        if (slot is null || slot["put_trust_up_icon"]?.GetValue<int>() != 1)
            return new(409, null, "No pending trust reward.");
        var pending = updated["_local_lecture_trust_rewards"] as JsonArray;
        var reward = pending?.FirstOrDefault(entry => entry?["user_unit_id"]?.GetValue<long>() == request.EntityId);
        if (reward?["value"]?.GetValue<int>() is not > 0) return new(503, null, "Pending trust value unavailable.");
        var current = unit["trust"]!.GetValue<int>();
        var total = (long)current + reward["value"]!.GetValue<int>();
        if (current < 0 || total > request.Value) return new(409, null, "Trust capacity exceeded.");
        unit["trust"] = (int)total;
        slot["put_trust_up_icon"] = 0;
        pending!.Remove(reward);
        Save(updated);
        return new(0, new()
        {
            ["user_update"] = new JsonObject { ["unit"] = updated["unit"]!.DeepClone(), ["lecture_room"] = room.DeepClone() }
        });
    }
}
