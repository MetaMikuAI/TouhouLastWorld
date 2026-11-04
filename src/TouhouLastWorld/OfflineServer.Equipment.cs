using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private LocalReply ResetEquipment(long? id)
    {
        if (id is null or <= 0) return new(400, null, "Positive unit ID required.");
        var updated = user.DeepClone().AsObject();
        var unit = updated["unit"]!["unit_list"]!.AsArray()
            .FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == id);
        if (unit is null) return new(404, null, "Owned unit not found.");
        for (var slot = 1; slot <= 5; slot++) unit[$"equip_spellcard{slot}_user_picture_id"] = 0L;
        foreach (var picture in updated["picture"]!["picture_list"]!.AsArray())
            if (picture!["equip_user_unit_id"]?.GetValue<long>() == id) picture["equip_user_unit_id"] = 0L;
        Save(updated);
        return new(0, new()
        {
            ["user_update"] = new JsonObject
            {
                ["unit"] = updated["unit"]!.DeepClone(), ["picture"] = updated["picture"]!.DeepClone()
            }
        });
    }
}
