using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    public int LectureExpansionRank => user["lecture_room"]!["unit"]!["expansion_rank"]!.GetValue<int>();
    public int LectureFoodExpansionRank => user["lecture_room"]!["food"]!["expansion_rank"]!.GetValue<int>();
    public int TrainingExpansionRank => user["training_room"]!["expansion_rank"]!.GetValue<int>();

    private LocalReply ExpandFacility(LocalArguments? request, string field, string? section = null)
    {
        if (request?.EntityId is null or <= 0 or > int.MaxValue || request.Value is null or < 0)
            return new(503, null, "Next facility level unavailable.");
        var current = section is null ? user[field]! : user[field]![section]!;
        var rank = current["expansion_rank"]!.GetValue<int>();
        if (request.EntityId != (long)rank + 1)
            return new(409, null, "Facility level changed.");
        var updated = user.DeepClone().AsObject();
        var orb = updated["orb"]!.AsObject();
        var free = orb["free"]!.GetValue<long>();
        var purchased = orb["purchased"]!.GetValue<long>();
        var cost = request.Value.Value;
        if (free < 0 || purchased < 0 || (decimal)free + purchased < cost)
            return new(409, null, "Insufficient local balance.");
        var freeSpent = Math.Min(free, cost);
        orb["free"] = free - freeSpent;
        orb["purchased"] = purchased - (cost - freeSpent);
        var facility = section is null ? updated[field]! : updated[field]![section]!;
        facility["expansion_rank"] = (int)request.EntityId.Value;
        Save(updated);
        return new(0, new()
        {
            ["user_update"] = new JsonObject
            {
                [field] = updated[field]!.DeepClone(), ["orb"] = orb.DeepClone()
            }
        });
    }

    private LocalReply AddLectureUnit(LocalArguments? request)
    {
        if (request?.EntityId is null or <= 0) return new(400, null, "Positive unit ID required.");
        if (request.Value is null or <= 0) return new(503, null, "Facility capacity unavailable.");
        var updated = user.DeepClone().AsObject();
        var unit = updated["unit"]!["unit_list"]!.AsArray()
            .FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == request.EntityId);
        if (unit is null) return new(404, null, "Owned unit not found.");
        var room = updated["lecture_room"]!.AsObject();
        var entries = room["unit"]!["unit_list"]!.AsArray();
        if (!entries.Any(entry => entry!["user_unit_id"]!.GetValue<long>() == request.EntityId))
        {
            if (entries.Count >= request.Value) return new(409, null, "Facility is full.");
            entries.Add(new JsonObject { ["user_unit_id"] = request.EntityId.Value, ["put_trust_up_icon"] = 0 });
        }
        unit["is_lecture"] = 1;
        Save(updated);
        return new(0, new()
        {
            ["user_update"] = new JsonObject
            {
                ["lecture_room"] = room.DeepClone(), ["unit"] = updated["unit"]!.DeepClone()
            }
        });
    }

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
