using System.Text.Json.Nodes;
namespace TouhouLastWorld;

internal static class PlayerProgress
{
    internal static int HighestUnitLevel(JsonObject state) => Math.Max(state["_local_highest_unit_level"]?.GetValue<int>() ?? 0, (state["unit"]?["unit_list"] as JsonArray)?.Select(row => row?["level"]?.GetValue<int>() ?? 0).DefaultIfEmpty().Max() ?? 0);
}
