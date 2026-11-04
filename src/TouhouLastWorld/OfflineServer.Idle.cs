using System.Globalization;
using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalIdleSet(long ObjectId, int ObjectType, int Slot, int Content, int Cancel,
    int Capacity = 0, long Seconds = 0, string? GrowthField = null, int CurrentValue = 0, bool Eligible = false);

public sealed partial class OfflineServer
{
    public LocalIdleSet? IdleSelection(int type, int number)
    {
        if (type is not (9 or 10) || number is < 1 or > 5) return null;
        var slots = user["_local_idle"]?[type == 9 ? "unit_slot_list" : "picture_slot_list"] as JsonArray;
        var slot = slots?.FirstOrDefault(entry => entry?["slot_number"]?.GetValue<int>() == number);
        return slot is null ? null : new(slot["user_object_id"]!.GetValue<long>(), type, number, slot["content"]!.GetValue<int>(), 0);
    }

    private LocalReply CompleteIdleSlot(LocalArguments? arguments)
    {
        if (arguments?.Value is not (9 or 10) || arguments.EntityId is null or < 1 or > 5)
            return new(400, null, "Invalid idle completion selection.");
        var selection = IdleSelection(arguments.Value.Value, (int)arguments.EntityId.Value);
        if (selection is null) return new(404, null, "Idle slot not found.");
        var request = arguments.Idle;
        if (request is null || request.GrowthField is null) return new(503, null, "Idle growth definition unavailable.");
        if (request.ObjectId != selection.ObjectId || request.Content != selection.Content || request.ObjectType != selection.ObjectType
            || request.Slot != selection.Slot || !request.Eligible)
            return new(409, null, "Idle completion state changed.");
        var updated = user.DeepClone().AsObject();
        var slots = updated["_local_idle"]![selection.ObjectType == 9 ? "unit_slot_list" : "picture_slot_list"]!.AsArray();
        var slot = slots.First(entry => entry!["slot_number"]!.GetValue<int>() == selection.Slot)!;
        if (!DateTimeOffset.TryParse(slot["end_at"]?.GetValue<string>(), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var end) || end > DateTimeOffset.UtcNow)
            return new(409, null, "Idle growth has not finished.");
        var key = selection.ObjectType == 9 ? "unit" : "picture";
        var entry = updated[key]![key + "_list"]!.AsArray()
            .FirstOrDefault(value => value?["user_" + key + "_id"]?.GetValue<long>() == selection.ObjectId);
        if (entry is null) return new(404, null, "Owned idle object not found.");
        if (request.CurrentValue is < 0 or int.MaxValue || entry[request.GrowthField]?.GetValue<int>() != request.CurrentValue
            || slot["_local_growth_field"]?.GetValue<string>() != request.GrowthField
            || slot["_local_growth_value"]?.GetValue<int>() != request.CurrentValue)
            return new(409, null, "Idle growth snapshot changed or unavailable.");
        entry[request.GrowthField] = request.CurrentValue + 1;
        slots.Remove(slot);
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { [key] = updated[key]!.DeepClone() } });
    }

    public int IdleStage(int content)
    {
        var stages = user["_local_idle"]?["content_stage_list"] as JsonArray;
        return content > 0 && stages is not null && content <= stages.Count ? stages[content - 1]!.GetValue<int>() : 0;
    }

    private JsonObject IdleOverview() => new()
    {
        ["content_stage_list"] = user["_local_idle"]?["content_stage_list"]?.DeepClone() ?? new JsonArray(),
        ["before_content_stage_list"] = user["_local_idle"]?["content_stage_list"]?.DeepClone() ?? new JsonArray(),
        ["unit_slot_list"] = user["_local_idle"]?["unit_slot_list"]?.DeepClone() ?? new JsonArray(),
        ["picture_slot_list"] = user["_local_idle"]?["picture_slot_list"]?.DeepClone() ?? new JsonArray(),
        ["drop_list"] = new JsonArray(), ["search_elapsed_time"] = 0, ["search_max_time"] = 43200
    };

    private LocalReply SetIdleSlot(LocalIdleSet? request)
    {
        if (request is null || request.ObjectId <= 0 || request.ObjectType is not (9 or 10)
            || request.Slot is < 1 or > 5 || request.Cancel is not (0 or 1)
            || (request.ObjectType == 9 ? request.Content is not (2 or 4) : request.Content is not (3 or 7)))
            return new(400, null, "Invalid idle slot selection.");
        var updated = user.DeepClone().AsObject();
        var idle = updated["_local_idle"] as JsonObject ?? new JsonObject();
        if (idle.Parent is null) updated["_local_idle"] = idle;
        var key = request.ObjectType == 9 ? "unit_slot_list" : "picture_slot_list";
        var slots = idle[key] as JsonArray ?? new JsonArray();
        if (slots.Parent is null) idle[key] = slots;
        var slot = slots.FirstOrDefault(entry => entry?["slot_number"]?.GetValue<int>() == request.Slot);
        if (request.Cancel == 1)
        {
            if (slot is null) return new(404, null, "Idle slot not found.");
            if (slot["user_object_id"]!.GetValue<long>() != request.ObjectId || slot["content"]!.GetValue<int>() != request.Content)
                return new(409, null, "Idle slot selection changed.");
            slots.Remove(slot);
            Save(updated);
            return new(0, new() { ["start_at"] = "", ["end_at"] = "" });
        }
        if (request.Capacity is < 1 or > 5 || request.Seconds <= 0 || request.GrowthField is null)
            return new(503, null, "Idle growth definition unavailable.");
        if (request.Slot > request.Capacity || !request.Eligible)
            return new(409, null, "Idle slot or growth unavailable.");
        var owned = request.ObjectType == 9 ? updated["unit"]?["unit_list"] : updated["picture"]?["picture_list"];
        var idKey = request.ObjectType == 9 ? "user_unit_id" : "user_picture_id";
        var entry = (owned as JsonArray)?.FirstOrDefault(value => value?[idKey]?.GetValue<long>() == request.ObjectId);
        if (entry is null) return new(404, null, "Owned idle object not found.");
        if (entry[request.GrowthField]?.GetValue<int>() != request.CurrentValue)
            return new(409, null, "Idle growth state changed.");
        if (slot is not null || slots.Any(value => value?["user_object_id"]?.GetValue<long>() == request.ObjectId))
            return new(409, null, "Idle slot or object already in use.");
        var now = DateTime.UtcNow;
        if (request.Seconds > (DateTime.MaxValue - now).TotalSeconds)
            return new(400, null, "Idle duration exceeds limit.");
        var start = now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var end = now.AddSeconds(request.Seconds).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        slots.Add(new JsonObject
        {
            ["slot_number"] = request.Slot, ["object_type"] = request.ObjectType,
            ["object_id"] = entry[request.ObjectType == 9 ? "unit_id" : "picture_id"]!.DeepClone(),
            ["user_object_id"] = request.ObjectId, ["content"] = request.Content, ["start_at"] = start, ["end_at"] = end,
            ["_local_growth_field"] = request.GrowthField, ["_local_growth_value"] = request.CurrentValue
        });
        Save(updated);
        return new(0, new() { ["start_at"] = start, ["end_at"] = end });
    }
}
