using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
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
