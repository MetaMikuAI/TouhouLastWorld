using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
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
