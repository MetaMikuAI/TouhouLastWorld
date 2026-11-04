using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private LocalReply TowerUnits()
    {
        var snapshot = user["_local_tower_unit_info"];
        if (snapshot == null)
            return new(0, new() { ["tower_unit_info"] = "" });
        if (snapshot is not JsonValue value || !value.TryGetValue<string>(out var csv))
            return new(503, null, "Local unit state unavailable.");

        var ids = new HashSet<long>();
        foreach (var line in csv.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split(',');
            if (fields.Length is not (27 or 29) ||
                !long.TryParse(fields[0], out var id) || id <= 0 || !ids.Add(id) ||
                user["unit"]?["unit_list"] is not JsonArray units ||
                !units.Any(unit => unit?["user_unit_id"]?.GetValue<long>() == id) ||
                fields.Skip(1).Any(field => !int.TryParse(field, out _)))
                return new(503, null, "Invalid local unit state.");
        }
        return new(0, new() { ["tower_unit_info"] = csv });
    }

    private LocalReply TowerOverview()
    {
        if (user["tower"] is not JsonObject tower ||
            tower["cleared_floor"] is not JsonValue floorValue || !floorValue.TryGetValue<int>(out var floor) || floor < 0 ||
            tower["exist_data"] is not JsonValue existsValue || !existsValue.TryGetValue<uint>(out var exists))
            return new(503, null, "Local floor state unavailable.");

        return new(0, new()
        {
            ["cleared_floor"] = floor, ["exist_data"] = exists,
            ["remaining_recovery_count"] = 0, ["enemy_battle_info"] = ""
        });
    }
}
