using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private int RemainingTowerRecovery => user["_local_tower_recovery_count"]?.GetValue<int>() ?? 0;

    private LocalReply RecoverTowerUnits(LocalArguments? arguments)
    {
        if (!string.IsNullOrEmpty(arguments?.Text))
            return new(400, null, "Unexpected recovery payload.");
        var remaining = RemainingTowerRecovery;
        if (remaining is < 0 or > 4) return new(503, null, "Recovery allowance unavailable.");
        var cost = remaining switch { 1 => 15, 2 => 10, 3 => 5, _ => 0 };
        var balance = user["crystal"]?.GetValue<int>() ?? 0;
        var snapshot = TowerUnits();
        if (snapshot.Status != 0) return snapshot;
        var hasDamage = !string.IsNullOrEmpty(snapshot.Body!["tower_unit_info"]!.GetValue<string>())
            || user["unit"]?["tower_unit_list"] is JsonArray { Count: > 0 };
        if (remaining == 0 || balance < cost || !hasDamage)
            return new(0, new() { ["result"] = 0, ["remaining_recovery_count"] = remaining });
        if (user["unit"] is not JsonObject) return new(503, null, "Unit state unavailable.");
        var updated = user.DeepClone().AsObject();
        updated["crystal"] = balance - cost;
        updated["_local_tower_recovery_count"] = remaining - 1;
        updated["_local_tower_unit_info"] = "";
        updated["unit"]!["tower_unit_list"] = new JsonArray();
        Save(updated);
        return new(0, new() { ["result"] = 1, ["remaining_recovery_count"] = remaining - 1,
            ["user_update"] = new JsonObject { ["crystal"] = updated["crystal"]!.DeepClone(), ["unit"] = updated["unit"]!.DeepClone() } });
    }

    private LocalReply TowerUnits(JsonNode? supplied = null)
    {
        var snapshot = supplied ?? user["_local_tower_unit_info"];
        if (snapshot == null)
            return new(0, new() { ["tower_unit_info"] = "" });
        if (snapshot is not JsonValue value || !value.TryGetValue<string>(out var csv))
            return new(503, null, "Local unit state unavailable.");

        var ids = new HashSet<long>();
        foreach (var line in csv.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var record = line.TrimEnd('\r');
            if (record.EndsWith(',')) record = record[..^1];
            var fields = record.Split(',');
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
            ["remaining_recovery_count"] = RemainingTowerRecovery, ["enemy_battle_info"] = user["_local_tower_enemy_info"]?.DeepClone() ?? JsonValue.Create("")
        });
    }
}
