using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private static readonly Dictionary<string, JsonObject> QueryTemplates = new()
    {
        ["series_battle_ranking_top"] = new()
        {
            ["is_held"] = 0, ["ranking"] = 0, ["hi_score_stage_master_id"] = 0,
            ["hi_score"] = 0, ["score_info"] = new JsonArray()
        },
        ["update_voice_version"] = new(),
        ["rhythm_game_top"] = new() { ["clear_info"] = new JsonArray() },
        ["battle_pass_top"] = new()
        {
            ["battle_pass_mission"] = new JsonObject
            {
                ["unlock_list"] = new JsonArray(), ["clear_list"] = new JsonArray(), ["received_list"] = new JsonArray()
            },
            ["normal_received_reward_level"] = 0, ["premium_received_reward_level_1"] = 0,
            ["premium_received_reward_level_2"] = 0
        },
        ["multiple_spirits_weekly_top"] = new() { ["clear_info"] = new JsonArray() },
        ["get_raid_gauge"] = new() { ["raid_gauge_list"] = new JsonArray() },
        ["get_series_battle_ranking_list"] = new() { ["ranking"] = new JsonArray(), ["myrank"] = new JsonArray() },
        ["personal_info_get"] = new()
        {
            ["user_name"] = "", ["zip_code"] = "", ["address"] = "", ["phone_number"] = ""
        },
        ["anniversary_info_get"] = new() { ["mail_address"] = "" },
        ["friend_player_search"] = new() { ["search_result"] = new JsonArray(), ["result"] = 1 },
        ["friend_top"] = new()
        {
            ["friend_list"] = new JsonArray(), ["friend_request_list"] = new JsonArray(),
            ["friend_approval_pending_list"] = new JsonArray(), ["new_approve_list"] = new JsonArray(),
            ["helper_user_unit_id1"] = 0L
        },
        ["get_clear_history"] = new()
        {
            ["best"] = null, ["ranking"] = new JsonArray(), ["justnow"] = new JsonArray()
        },
        ["pass_card/show_task"] = new() { ["user_pass_card"] = EmptyPassCard() },
        ["pass_card/show_shop"] = new() { ["user_pass_card"] = EmptyPassCard() },
        ["music_list"] = new() { ["collect"] = new JsonArray(), ["unlock"] = new JsonArray() },
        ["get_shrine_office_shop_list"] = new() { ["shop_list"] = new JsonArray() },
        ["avatar/list"] = new() { ["avatar_list"] = new JsonArray() },
        ["picture_book/get_user_picture_book_list"] = new() { ["user_completed_info"] = new JsonArray() },
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

    private static JsonObject EmptyPassCard() => new()
    {
        ["left_day"] = 0, ["current_exp"] = 0, ["is_vip"] = 0, ["is_had_gift"] = 0,
        ["coin"] = 0, ["level"] = 0, ["is_reward"] = 0, ["is_task"] = 0,
        ["had_got_free_reward_level"] = new JsonArray(), ["had_got_vip_reward_level"] = new JsonArray(),
        ["free_rewards"] = new JsonArray(), ["vip_rewards"] = new JsonArray(),
        ["tasks"] = new JsonArray(), ["pass_card_goods"] = new JsonArray()
    };
}
