using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private static readonly Dictionary<string, JsonObject> QueryTemplates = new()
    {
        ["get_history"] = new()
        {
            ["bronze_trophy_count"] = 0, ["silver_trophy_count"] = 0, ["gold_trophy_count"] = 0,
            ["unit_get_count"] = 0, ["quest_complete_count"] = 0, ["dispatch_complete_count"] = 0
        }
    };
}
