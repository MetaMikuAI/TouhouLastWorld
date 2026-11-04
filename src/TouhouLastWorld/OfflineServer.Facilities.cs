using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private LocalReply RemoveLectureUnit(long? id)
    {
        if (id is null or <= 0) return new(400, null, "Positive unit ID required.");
        var updated = user.DeepClone().AsObject();
        var unit = updated["unit"]!["unit_list"]!.AsArray()
            .FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == id);
        if (unit is null) return new(404, null, "Owned unit not found.");
        var room = updated["lecture_room"]!.AsObject();
        var entries = room["unit"]!["unit_list"]!.AsArray();
        for (var index = entries.Count - 1; index >= 0; index--)
            if (entries[index]!["user_unit_id"]!.GetValue<long>() == id) entries.RemoveAt(index);
        unit["is_lecture"] = 0;
        Save(updated);
        return new(0, new()
        {
            ["user_update"] = new JsonObject
            {
                ["lecture_room"] = room.DeepClone(), ["unit"] = updated["unit"]!.DeepClone()
            }
        });
    }
}
