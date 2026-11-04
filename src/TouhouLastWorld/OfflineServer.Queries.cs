using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private static readonly Dictionary<string, JsonObject> QueryTemplates = new()
    {
        ["get_auto_replay_info"] = new() { ["win_rate"] = 0, ["action_num"] = 0 },
        ["friend_get_candidate_list"] = new() { ["helper_list"] = new JsonArray() },
        ["mission_top"] = new()
        {
            ["event_id_list"] = new JsonArray(), ["chapter_id_list"] = new JsonArray(),
            ["multiple_spirits_mission_id_list"] = new JsonArray(),
            ["mission"] = new JsonObject { ["unlock_list"] = new JsonArray(), ["clear_list"] = new JsonArray() }
        },
        ["get_dispatch_list"] = new() { ["dispatch_list"] = new JsonArray() },
        ["achievement_top"] = new()
        {
            ["bronze_trophy_count"] = 0, ["silver_trophy_count"] = 0, ["gold_trophy_count"] = 0,
            ["achievement_list"] = new JsonArray()
        },
        ["get_history"] = new()
        {
            ["bronze_trophy_count"] = 0, ["silver_trophy_count"] = 0, ["gold_trophy_count"] = 0,
            ["unit_get_count"] = 0, ["quest_complete_count"] = 0, ["dispatch_complete_count"] = 0
        }
    };
}
