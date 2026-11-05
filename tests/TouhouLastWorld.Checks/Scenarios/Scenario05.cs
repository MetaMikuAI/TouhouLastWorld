using System.Text.Json.Nodes;
using TouhouLastWorld;

internal sealed partial class RegressionSuite
{
    private void Scenario05()
    {
        var allocationSeed = JsonNode.Parse(seed)!.AsObject();
        allocationSeed["unit"]!["unit_list"]![0]!["point_allocation_info"]!["pa_points"] = 20;
        LocalPointRule[] allocationRules = [new(1, 1, 3, 0), new(1, 2, 4, 0), new(7, 1, 5, 7)];
        var allocationDirectory = directory + "-allocation";
        var allocationServer = new OfflineServer(new JsonStateStore(allocationDirectory), allocationSeed.ToJsonString(), "offline://resources/");
        allocationServer.Dispatch("game_init");
        allocationServer.Dispatch("login");
        Check(allocationServer.Dispatch("allocate_points", arguments: new(PointAllocation: new(1, [new(1, 2), new(7, 1)], allocationRules))).Status == 403, "allocation prerequisites use pre-request spent points");
        var allocated = allocationServer.Dispatch("allocate_points", arguments: new(PointAllocation: new(1, [new(1, 2)], allocationRules))).Body!["user_update"]!["unit"]!["unit_list"]![0]!["point_allocation_info"]!;
        Check(allocated["pa_type_1"]!.GetValue<int>() == 2 && allocated["pa_points"]!.GetValue<int>() == 13 && allocated["pa_total_used_points"]!.GetValue<int>() == 7, "allocation sums every intermediate cost without partial failed changes");
        Check(allocationServer.Dispatch("allocate_points", arguments: new(PointAllocation: new(1, [new(1, 1)], allocationRules))).Status == 409 && allocationServer.Dispatch("allocate_points", arguments: new(PointAllocation: new(1, [new(1, 3)], allocationRules))).Status == 503, "allocation rejects decreases and missing rules");
        Check(allocationServer.Dispatch("allocate_points", arguments: new(PointAllocation: new(1, [new(7, 1), new(7, 1)], allocationRules))).Status == 400, "allocation rejects duplicate types");
        allocationServer.Dispatch("allocate_points", arguments: new(PointAllocation: new(1, [new(7, 1)], allocationRules)));
        allocationServer.Dispatch("allocate_points", arguments: new(PointAllocation: new(1, [new(7, 1)], allocationRules)));
        var allocationReload = new OfflineServer(new JsonStateStore(allocationDirectory), allocationSeed.ToJsonString(), "offline://resources/");
        allocationReload.Dispatch("game_init");
        var allocationUser = allocationReload.Dispatch("login").Body!["user"]!;
        var savedAllocation = allocationUser["unit"]!["unit_list"]![0]!["point_allocation_info"]!;
        Check(savedAllocation["pa_points"]!.GetValue<int>() == 8 && savedAllocation["pa_total_used_points"]!.GetValue<int>() == 12 && savedAllocation["pa_type_7"]!.GetValue<int>() == 1, "allocation persists and target replay does not charge twice");
        var resetAllocation = allocationReload.Dispatch("reset_point_allocation", arguments: new(PointReset: new(1, 100, false))).Body!["user_update"]!["unit"]!["unit_list"]![0]!["point_allocation_info"]!;
        Check(resetAllocation["pa_points"]!.GetValue<int>() == 20 && resetAllocation["pa_total_used_points"]!.GetValue<int>() == 0, "allocation and reset preserve total point balance");
        Console.WriteLine("Passed 6 point allocation checks.");
        var deleteContactSeed = JsonNode.Parse(seed)!.AsObject();
        deleteContactSeed["_local_friend_players"] = new JsonArray(new JsonObject { ["player_id"] = "old-a" }, new JsonObject { ["player_id"] = "old-b" });
        deleteContactSeed["_local_friends"] = new JsonArray(new JsonObject { ["public_id"] = "old-a" });
        var deleteContactDirectory = directory + "-delete-contact";
        var deleteContact = new OfflineServer(new JsonStateStore(deleteContactDirectory), deleteContactSeed.ToJsonString(), "offline://resources/");
        deleteContact.Dispatch("game_init");
        deleteContact.Dispatch("login");
        Check(deleteContact.Dispatch("friend_delete").Status == 400 && deleteContact.Dispatch("friend_delete", arguments: new(Text: "missing")).Body!["result"]!.GetValue<int>() == 1, "contact deletion validates ID and reports missing target in result");
        var deletedContact = deleteContact.Dispatch("friend_delete", arguments: new(Text: "old-a")).Body!;
        Check(deletedContact["result"]!.GetValue<int>() == 0 && deletedContact["friend_list"]!.AsArray().Count == 1 && deletedContact["friend_list"]![0]!["player_id"]!.GetValue<string>() == "old-b", "contact deletion returns remaining old-schema entries");
        var deleteContactReload = new OfflineServer(new JsonStateStore(deleteContactDirectory), deleteContactSeed.ToJsonString(), "offline://resources/");
        deleteContactReload.Dispatch("game_init");
        deleteContactReload.Dispatch("login");
        Check(deleteContactReload.Dispatch("friend_top").Body!["friend_list"]!.AsArray().Count == 1 && deleteContactReload.Dispatch("friend_delete", arguments: new(Text: "old-a")).Body!["result"]!.GetValue<int>() == 1, "deleted contact remains absent after reload");
        Check(deleteContactReload.Dispatch("friend/list").Body!["data"]!.AsArray().Count == 1, "old-schema deletion does not guess public ID equivalence");
        Console.WriteLine("Passed 4 contact deletion checks.");
        var cancelContactSeed = JsonNode.Parse(seed)!.AsObject();
        cancelContactSeed["_local_friend_approval_pending"] = new JsonArray(new JsonObject { ["player_id"] = "incoming" });
        cancelContactSeed["_local_friend_requests"] = new JsonArray(new JsonObject { ["player_id"] = "outgoing" });
        cancelContactSeed["_local_friend_players"] = new JsonArray(new JsonObject { ["player_id"] = "friend" });
        var cancelContactDirectory = directory + "-cancel-contact";
        var cancelContact = new OfflineServer(new JsonStateStore(cancelContactDirectory), cancelContactSeed.ToJsonString(), "offline://resources/");
        cancelContact.Dispatch("game_init");
        cancelContact.Dispatch("login");
        Check(cancelContact.Dispatch("friend_request_cancel", arguments: new(Value: 2)).Status == 400 && cancelContact.Dispatch("friend_request_cancel", arguments: new(Value: 0, Text: "missing")).Body!["result"]!.GetValue<int>() == 1, "request cancellation validates mode and reports missing targets");
        cancelContact.Dispatch("friend_request_cancel", arguments: new(Value: 1));
        var afterRejectAll = cancelContact.Dispatch("friend_top").Body!;
        Check(afterRejectAll["friend_approval_pending_list"]!.AsArray().Count == 0 && afterRejectAll["friend_request_list"]!.AsArray().Count == 1 && afterRejectAll["friend_list"]!.AsArray().Count == 1, "reject all preserves outgoing requests and existing contacts");
        Check(cancelContact.Dispatch("friend_request_cancel", arguments: new(Value: 0, Text: "outgoing")).Body!["result"]!.GetValue<int>() == 0, "single outgoing request can be cancelled");
        var cancelContactReload = new OfflineServer(new JsonStateStore(cancelContactDirectory), cancelContactSeed.ToJsonString(), "offline://resources/");
        cancelContactReload.Dispatch("game_init");
        cancelContactReload.Dispatch("login");
        Check(cancelContactReload.Dispatch("friend_top").Body!["friend_request_list"]!.AsArray().Count == 0, "request cancellation persists after reload");
        Console.WriteLine("Passed 4 contact request cancellation checks.");
        var approveContactDirectory = directory + "-approve-contact";
        var approveContact = new OfflineServer(new JsonStateStore(approveContactDirectory), cancelContactSeed.ToJsonString(), "offline://resources/");
        approveContact.Dispatch("game_init");
        approveContact.Dispatch("login");
        Check(approveContact.Dispatch("friend_approve", arguments: new(Value: 1, Text: "incoming")).Body!["result"]!.GetValue<int>() == 1 && approveContact.Dispatch("friend_top").Body!["friend_approval_pending_list"]!.AsArray().Count == 1, "full contact list preserves pending application");
        var approvedContact = approveContact.Dispatch("friend_approve", arguments: new(Value: 2, Text: "incoming")).Body!;
        Check(approvedContact["result"]!.GetValue<int>() == 0 && approvedContact["friend_list"]!.AsArray().Count == 2, "approval adds the incoming player to old-schema contacts");
        Check(approveContact.Dispatch("friend_approve", arguments: new(Value: 3, Text: "incoming")).Body!["result"]!.GetValue<int>() == 1, "approval cannot duplicate an accepted application");
        var approveContactReload = new OfflineServer(new JsonStateStore(approveContactDirectory), cancelContactSeed.ToJsonString(), "offline://resources/");
        approveContactReload.Dispatch("game_init");
        approveContactReload.Dispatch("login");
        var approvedOverview = approveContactReload.Dispatch("friend_top").Body!;
        Check(approvedOverview["friend_approval_pending_list"]!.AsArray().Count == 0 && approvedOverview["friend_list"]!.AsArray().Count == 2 && approvedOverview["friend_request_list"]!.AsArray().Count == 1, "approval persists incoming removal and preserves outgoing requests");
        Console.WriteLine("Passed 4 contact approval checks.");
        var weeklyResetSeed = JsonNode.Parse(seed)!.AsObject();
        weeklyResetSeed["_local_weekly_arena"] = new JsonObject
        {
            ["total_battle_score"] = 30,
            ["weekly_battle_data"] = new JsonObject
            {
                ["battle_number1"] = new JsonObject
                {
                    ["total_score"] = 10,
                    ["difficulty"] = 3,
                    ["attack_count_score"] = 5,
                    ["unit_list"] = new JsonArray(101),
                    ["user_unit_list"] = new JsonArray(1L)
                },
                ["battle_number2"] = new JsonObject
                {
                    ["total_score"] = 20
                }
            }
        };
        var weeklyResetDirectory = directory + "-weekly-reset";
        var weeklyReset = new OfflineServer(new JsonStateStore(weeklyResetDirectory), weeklyResetSeed.ToJsonString(), "offline://resources/");
        weeklyReset.Dispatch("game_init");
        weeklyReset.Dispatch("login");
        Check(weeklyReset.Dispatch("weekly_arena_battle_score_reset", arguments: new(Value: 0)).Status == 400 && weeklyReset.Dispatch("weekly_arena_battle_score_reset", arguments: new(Value: 4)).Status == 404, "weekly reset rejects invalid and absent battle numbers");
        Check(weeklyReset.Dispatch("weekly_arena_battle_score_reset", arguments: new(Value: 1)).Status == 0, "weekly score reset accepts a stored battle");
        var weeklyResetReload = new OfflineServer(new JsonStateStore(weeklyResetDirectory), weeklyResetSeed.ToJsonString(), "offline://resources/");
        weeklyResetReload.Dispatch("game_init");
        var resetWeekly = weeklyResetReload.Dispatch("login").Body!["user"]!["_local_weekly_arena"]!;
        var resetWeeklyBattle = resetWeekly["weekly_battle_data"]!["battle_number1"]!;
        Check(resetWeekly["total_battle_score"]!.GetValue<int>() == 30 && resetWeeklyBattle["total_score"]!.GetValue<int>() == 0 && resetWeeklyBattle["unit_list"]!.AsArray().Count == 0 && resetWeeklyBattle["user_unit_list"]!.AsArray().Count == 0, "weekly reset persists cleared score and roster while preserving historical high score");
        Check(resetWeeklyBattle["difficulty"]!.GetValue<int>() == 3 && resetWeeklyBattle["attack_count_score"]!.GetValue<int>() == 5, "weekly reset preserves difficulty and component scores");
        Console.WriteLine("Passed 4 weekly score reset checks.");
        Check(weeklyResetReload.Dispatch("weekly_arena_top").Status == 503, "weekly overview rejects an incomplete local configuration");
        weeklyOverviewSeed = weeklyResetSeed.DeepClone().AsObject();
        var weeklyOverviewData = weeklyOverviewSeed["_local_weekly_arena"]!.AsObject();
        weeklyOverviewData["custom_battle_id"] = 1;
        for (var number = 1; number <= 4; number++)
        {
            weeklyOverviewData["weekly_battle_data"]![$"battle_number{number}"] = new JsonObject
            {
                ["total_score"] = number * 10,
                ["difficulty"] = 2,
                ["buff_role_type"] = new JsonArray(),
                ["wave_enemy_info"] = new JsonArray(),
                ["unit_list"] = new JsonArray(),
                ["user_unit_list"] = new JsonArray()
            };
        }

        var weeklyOverview = new OfflineServer(new JsonStateStore(directory + "-weekly-overview"), weeklyOverviewSeed.ToJsonString(), "offline://resources/");
        weeklyOverview.Dispatch("game_init");
        weeklyOverview.Dispatch("login");
        var weeklySnapshot = weeklyOverview.Dispatch("weekly_arena_top").Body!;
        Check(weeklySnapshot["weekly_battle_data"]!.AsObject().Count == 4 && weeklySnapshot["custom_battle_id"]!.GetValue<int>() == 1, "weekly overview reads all stored battle configurations");
        weeklySnapshot["weekly_battle_data"]!["battle_number2"]!["total_score"] = 0;
        Check(weeklyOverview.Dispatch("weekly_arena_top").Body!["weekly_battle_data"]!["battle_number2"]!["total_score"]!.GetValue<int>() == 20, "weekly overview returns an isolated snapshot");
        weeklyOverview.Dispatch("weekly_arena_battle_score_reset", arguments: new(Value: 2));
        var weeklyAfterReset = weeklyOverview.Dispatch("weekly_arena_top").Body!;
        Check(weeklyAfterReset["weekly_battle_data"]!["battle_number2"]!["total_score"]!.GetValue<int>() == 0 && weeklyAfterReset["total_battle_score"]!.GetValue<int>() == 30, "weekly overview reflects reset without lowering historical high score");
        Console.WriteLine("Passed 4 weekly overview checks.");
        Check(weeklyOverview.Dispatch("daily_arena_top").Status == 503, "daily overview requires stored configuration");
        dailyOverviewSeed = JsonNode.Parse(seed)!.AsObject();
        dailyOverviewSeed["_local_daily_arena"] = new JsonObject
        {
            ["custom_battle_id"] = 1,
            ["clear_diffculty"] = 2,
            ["reward_day_count"] = 1,
            ["enemy_level"] = 20,
            ["last_clear_difficulty"] = 2,
            ["enemy_info"] = new JsonArray(new JsonObject { ["enemy1"] = new JsonObject { ["enemy_id"] = 101, ["special_action_gauge_limit"] = 3 }, ["enemy2"] = new JsonObject { ["enemy_id"] = 0 }, ["enemy3"] = new JsonObject { ["enemy_id"] = 0 } })
        };
        var dailyOverview = new OfflineServer(new JsonStateStore(directory + "-daily-overview"), dailyOverviewSeed.ToJsonString(), "offline://resources/");
        dailyOverview.Dispatch("game_init");
        dailyOverview.Dispatch("login");
        var dailySnapshot = dailyOverview.Dispatch("daily_arena_top").Body!;
        Check(dailySnapshot["clear_diffculty"]!.GetValue<int>() == 2 && dailySnapshot["reward_day_count"]!.GetValue<int>() == 1 && dailySnapshot["enemy_info"]![0]!["enemy1"]!["enemy_id"]!.GetValue<int>() == 101, "daily overview preserves stored progress and enemy configuration");
        dailySnapshot["enemy_info"]![0]!["enemy1"]!["enemy_id"] = 0;
        Check(dailyOverview.Dispatch("daily_arena_top").Body!["enemy_info"]![0]!["enemy1"]!["enemy_id"]!.GetValue<int>() == 101, "daily overview returns an isolated snapshot");
        dailyOverviewSeed["_local_daily_arena"]!["enemy_info"]![0]!["enemy2"] = null;
        var invalidDailyOverview = new OfflineServer(new JsonStateStore(directory + "-daily-incomplete"), dailyOverviewSeed.ToJsonString(), "offline://resources/");
        invalidDailyOverview.Dispatch("game_init");
        invalidDailyOverview.Dispatch("login");
        Check(invalidDailyOverview.Dispatch("daily_arena_top").Status == 503, "daily overview rejects absent enemy slots");
        Console.WriteLine("Passed 4 daily overview checks.");
        Check(dailyOverview.Dispatch("event_top").Status == 503, "event overview requires an explicit stored snapshot");
        var eventOverviewSeed = JsonNode.Parse(seed)!.AsObject();
        eventOverviewSeed["_local_event_overview"] = new JsonObject
        {
            ["event_point_gacha_list"] = new JsonArray(),
            ["event_special_mission"] = new JsonArray(),
            ["event_total_point_reward"] = new JsonArray(new JsonObject { ["event_id"] = 1, ["next_reward_group_id"] = 2, ["total_point"] = 100, ["step_number"] = 1 })
        };
        var eventOverview = new OfflineServer(new JsonStateStore(directory + "-event-overview"), eventOverviewSeed.ToJsonString(), "offline://resources/");
        eventOverview.Dispatch("game_init");
        eventOverview.Dispatch("login");
        var eventSnapshot = eventOverview.Dispatch("event_top").Body!;
        Check(eventSnapshot["event_total_point_reward"]![0]!["total_point"]!.GetValue<int>() == 100 && eventSnapshot["event_special_mission"]!.AsArray().Count == 0 && eventSnapshot["user_update"] is null, "event overview reads saved progress without issuing user rewards");
        eventSnapshot["event_total_point_reward"]![0]!["total_point"] = 0;
        Check(eventOverview.Dispatch("event_top").Body!["event_total_point_reward"]![0]!["total_point"]!.GetValue<int>() == 100, "event overview returns an isolated snapshot");
        Console.WriteLine("Passed 3 event overview checks.");
        var sendContactSeed = JsonNode.Parse(seed)!.AsObject();
        sendContactSeed["_local_friend_candidates"] = new JsonArray(new JsonObject { ["player_id"] = "candidate-a", ["player_name"] = "Local A" }, new JsonObject { ["player_id"] = "candidate-b", ["player_name"] = "Local B" });
        var sendContactDirectory = directory + "-send-contact";
        var sendContact = new OfflineServer(new JsonStateStore(sendContactDirectory), sendContactSeed.ToJsonString(), "offline://resources/");
        sendContact.Dispatch("game_init");
        sendContact.Dispatch("login");
        Check(sendContact.Dispatch("friend_get_candidate_list").Body!["helper_list"]!.AsArray().Count == 2 && sendContact.Dispatch("friend_request", arguments: new(EntityId: 2, Value: 1, Text: "missing")).Body!["result"]!.GetValue<int>() == 1, "contact requests only target stored candidates");
        var sentContact = sendContact.Dispatch("friend_request", arguments: new(EntityId: 2, Value: 1, Text: "candidate-a")).Body!;
        Check(sentContact["result"]!.GetValue<int>() == 0 && sentContact["friend_request_list"]![0]!["player_name"]!.GetValue<string>() == "Local A", "contact request persists candidate details");
        Check(sendContact.Dispatch("friend_request", arguments: new(EntityId: 2, Value: 2, Text: "candidate-a")).Body!["result"]!.GetValue<int>() == 1 && sendContact.Dispatch("friend_request", arguments: new(EntityId: 2, Value: 1, Text: "candidate-b")).Body!["result"]!.GetValue<int>() == 1, "duplicate and over-capacity requests are rejected");
        var sendContactReload = new OfflineServer(new JsonStateStore(sendContactDirectory), sendContactSeed.ToJsonString(), "offline://resources/");
        sendContactReload.Dispatch("game_init");
        sendContactReload.Dispatch("login");
        Check(sendContactReload.Dispatch("friend_top").Body!["friend_request_list"]!.AsArray().Count == 1, "outgoing request survives reload");
        sendContactReload.Dispatch("friend_request_cancel", arguments: new(Value: 0, Text: "candidate-a"));
        Check(sendContactReload.Dispatch("friend_request", arguments: new(EntityId: 2, Value: 1, Text: "candidate-b")).Body!["result"]!.GetValue<int>() == 0, "cancellation releases local request capacity");
        Console.WriteLine("Passed 5 outgoing contact request checks.");
    }
}
