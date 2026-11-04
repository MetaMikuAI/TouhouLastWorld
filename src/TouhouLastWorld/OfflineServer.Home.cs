using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private LocalReply UpdateHome(LocalHomeUpdate? request)
    {
        if (request?.Units is null || request.Units.Length is < 1 or > 6 || request.RandomMode is not (0 or 1)
            || request.SelectedIndex < 0 || request.SelectedIndex >= request.Units.Length)
            return new(400, null, "Home entries, valid selection and random flag required.");
        var entries = new JsonArray();
        foreach (var entry in request.Units)
        {
            if (entry is null || entry.UserUnitId <= 0 || entry.UnitId <= 0 || entry.CostumeId < 0 || entry.Motion < 0 || entry.Music < 0)
                return new(400, null, "Invalid home entry.");
            var owned = user["unit"]!["unit_list"]!.AsArray()
                .FirstOrDefault(unit => unit!["user_unit_id"]!.GetValue<long>() == entry.UserUnitId);
            if (owned is null) return new(404, null, "Home unit not found.");
            if (owned["unit_id"]!.GetValue<int>() != entry.UnitId)
                return new(400, null, "Home unit identifiers do not match.");
            entries.Add(new JsonObject
            {
                ["unit_id"] = entry.UnitId, ["user_unit_id"] = entry.UserUnitId, ["costume_id"] = entry.CostumeId,
                ["home_motion"] = entry.Motion, ["bgm"] = entry.Music
            });
        }
        var info = new JsonObject
        {
            ["unit_param"] = entries, ["select_unit_index"] = request.SelectedIndex, ["random_select_mode"] = request.RandomMode
        };
        var updated = user.DeepClone().AsObject();
        updated["home_unit_info"] = info;
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["home_unit_info"] = info.DeepClone() } });
    }

    private LocalReply SetUnitMusic(LocalArguments? request)
    {
        if (request?.EntityId is null or <= 0 || request.Value is null or < 0)
            return new(400, null, "Positive unit ID and nonnegative music selection required.");
        var updated = user.DeepClone().AsObject();
        var unit = updated["unit"]!["unit_list"]!.AsArray()
            .FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == request.EntityId);
        if (unit is null) return new(404, null, "Unit not found.");
        unit["select_bgm"] = request.Value.Value;
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["unit"] = user["unit"]!.DeepClone() } });
    }

    private LocalReply SelectHomeUnit(int? index)
    {
        var info = user["home_unit_info"]!.AsObject();
        if (index is null or < 0 || index >= info["unit_param"]!.AsArray().Count)
            return new(400, null, "Home selection index out of range.");
        var updated = user.DeepClone().AsObject();
        updated["home_unit_info"]!["select_unit_index"] = index.Value;
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["home_unit_info"] = user["home_unit_info"]!.DeepClone() } });
    }
}
