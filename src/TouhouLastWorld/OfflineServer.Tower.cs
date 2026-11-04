using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
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
