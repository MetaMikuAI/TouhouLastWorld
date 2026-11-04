using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private LocalReply SetCostumes(LocalCostumeEntry[]? changes)
    {
        if (changes is null || changes.Any(entry => entry.UserUnitId <= 0 || entry.CostumeId <= 0)
            || changes.Select(entry => entry.UserUnitId).Distinct().Count() != changes.Length)
            return new(400, null, "Invalid costume assignments.");
        var updated = user.DeepClone().AsObject();
        var owned = updated["costume"]!["costume_id_list"]!.AsArray().Select(id => id!.GetValue<int>()).ToHashSet();
        var units = updated["unit"]!["unit_list"]!.AsArray();
        foreach (var change in changes)
        {
            if (change.UnitId <= 0) return new(503, null, "Costume definition unavailable.");
            var unit = units.FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == change.UserUnitId);
            if (unit is null) return new(404, null, "Owned unit not found.");
            if (unit["unit_id"]!.GetValue<int>() != change.UnitId) return new(400, null, "Costume does not match unit.");
            if (!change.IsDefault && !owned.Contains(change.CostumeId)
                && unit["equip_costume_id"]!.GetValue<int>() != change.CostumeId)
                return new(403, null, "Costume not owned.");
            unit["equip_costume_id"] = change.CostumeId;
        }
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["unit"] = updated["unit"]!.DeepClone() } });
    }

    private LocalReply SetEquipment(LocalArguments? request)
    {
        if (request?.EntityId is null or <= 0 || request.Equipment is null || request.Equipment.Length > 5
            || request.Equipment.Any(entry => entry.Slot is < 1 or > 5 || entry.PictureId < 0)
            || request.Equipment.Select(entry => entry.Slot).Distinct().Count() != request.Equipment.Length)
            return new(400, null, "Invalid equipment slots.");
        var updated = user.DeepClone().AsObject();
        var units = updated["unit"]!["unit_list"]!.AsArray();
        var unit = units.FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == request.EntityId);
        if (unit is null) return new(404, null, "Owned unit not found.");
        var pictures = updated["picture"]!["picture_list"]!.AsArray();
        foreach (var change in request.Equipment)
        {
            if (change.PictureId > 0)
            {
                var picture = pictures.FirstOrDefault(entry => entry!["user_picture_id"]!.GetValue<long>() == change.PictureId);
                if (picture is null) return new(404, null, "Owned equipment not found.");
                var owner = picture["equip_user_unit_id"]?.GetValue<long>() ?? 0;
                if ((owner != 0 && owner != request.EntityId) || units.Any(other => other != unit
                    && Enumerable.Range(1, 5).Any(slot => other![$"equip_spellcard{slot}_user_picture_id"]?.GetValue<long>() == change.PictureId)))
                    return new(409, null, "Equipment must be released from its current owner.");
            }
            unit[$"equip_spellcard{change.Slot}_user_picture_id"] = change.PictureId;
        }
        var equipped = Enumerable.Range(1, 5).Select(slot => unit[$"equip_spellcard{slot}_user_picture_id"]!.GetValue<long>())
            .Where(id => id > 0).ToArray();
        if (equipped.Distinct().Count() != equipped.Length) return new(409, null, "Equipment cannot occupy multiple slots.");
        foreach (var picture in pictures)
        {
            if (equipped.Contains(picture!["user_picture_id"]!.GetValue<long>())) picture["equip_user_unit_id"] = request.EntityId.Value;
            else if (picture["equip_user_unit_id"]?.GetValue<long>() == request.EntityId) picture["equip_user_unit_id"] = 0L;
        }
        Save(updated);
        return new(0, new()
        {
            ["user_update"] = new JsonObject
            {
                ["unit"] = updated["unit"]!.DeepClone(), ["picture"] = updated["picture"]!.DeepClone()
            }
        });
    }

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
