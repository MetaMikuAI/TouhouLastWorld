using System.Text.Json.Nodes;
using TouhouLastWorld;

internal sealed partial class RegressionSuite
{
    private void Scenario14()
    {
        var missionSeed = JsonNode.Parse(seed)!;
        missionSeed["_local_missions"] = new JsonObject
        {
            ["unlock_list"] = new JsonArray(),
            ["clear_list"] = new JsonArray(new JsonObject { ["id"] = 10, ["type"] = 1, ["event_id"] = 0, ["chapter_id"] = 0, ["complete_value"] = 2, ["current_value"] = 2 }, new JsonObject { ["id"] = 11, ["type"] = 1, ["event_id"] = 0, ["chapter_id"] = 0, ["complete_value"] = 2, ["current_value"] = 2 })
        };
        var missionDirectory = directory + "-mission-claims";
        var missionServer = new OfflineServer(new JsonStateStore(missionDirectory), missionSeed.ToJsonString(), "offline://resources/");
        missionServer.Dispatch("game_init");
        missionServer.Dispatch("login");
        var missionRules = new[]
        {
        new LocalMissionRule(10, 1, 0, 2, true, new[] { new LocalInventoryReward(3, 0, 100, 10000) }),
        new LocalMissionRule(11, 1, 0, 2, true, new[] { new LocalInventoryReward(12, 99990, 3, 2) })
    };
        var missionClaim = new LocalMissionClaim(0, 1, 0, 0, 0, missionRules);
        Check(missionServer.Dispatch("mission_end", arguments: new(MissionClaim: missionClaim)).Status == 409 && missionServer.Dispatch("login").Body!["user"]!["gold"]!.GetValue<int>() == 5000 && missionServer.Dispatch("mission_top").Body!["mission"]!["clear_list"]!.AsArray().Count == 2, "batch mission claims roll back rewards and claim records on capacity failure");
        var missionSingle = missionServer.Dispatch("mission_end", arguments: new(MissionClaim: missionClaim with { Id = 10, All = 0 })).Body!;
        Check(missionSingle["reward_list"]!.AsArray().Count == 1 && missionSingle["receive_mission_list"]![0]!["mission_id"]!.GetValue<int>() == 10 && missionSingle["mission"]!["clear_list"]!.AsArray().Count == 1 && missionSingle["user_update"]!["gold"]!.GetValue<int>() == 5100, "single mission claims grant rewards and remove only the claimed entry");
        var missionReload = new OfflineServer(new JsonStateStore(missionDirectory), seed, "offline://resources/");
        missionReload.Dispatch("game_init");
        missionReload.Dispatch("login");
        missionRules[1] = missionRules[1] with
        {
            Rewards = new[]
            {
            new LocalInventoryReward(12, 99990, 3, 100)
        }
        };
        Check(missionReload.Dispatch("mission_end", arguments: new(MissionClaim: missionClaim with { Id = 10, All = 0 })).Status == 404 && missionReload.Dispatch("mission_end", arguments: new(MissionClaim: missionClaim)).Body!["receive_mission_list"]!.AsArray().Count == 1 && missionReload.Dispatch("mission_end", arguments: new(MissionClaim: missionClaim)).Body!["reward_list"]!.AsArray().Count == 0, "mission receipts survive restart and repeated batch claims grant no duplicate reward");
        Console.WriteLine("Passed 3 mission claim checks.");
        var noviceSeed = JsonNode.Parse(seed)!;
        noviceSeed["quest_playdata"]!.AsArray().Add(new JsonObject { ["episode_id"] = 100100501, ["scenario_status"] = 1 });
        var noviceDirectory = directory + "-novice-finale";
        var noviceServer = new OfflineServer(new JsonStateStore(noviceDirectory), noviceSeed.ToJsonString(), "offline://resources/");
        noviceServer.Dispatch("game_init");
        noviceServer.Dispatch("login");
        var noviceFinale = new LocalProgressMission(400210, 100100501, "通关所有序章关卡", 2, [(1117993, 1), (0, 0), (0, 0), (0, 0)], Type: 10);
        var finaleRule = new LocalMissionRule(400210, 10, 0, 1, true, [new(9, 1015, 1, 1, 101501)]);
        var finaleClaim = new RequestDraft(ProgressMissions: [noviceFinale], MissionClaim: new(400210, 0, 10, 0, 0, [finaleRule]));
        Check(noviceServer.Dispatch("mission_end", arguments: finaleClaim).Status == 409, "novice partner claim checks roster capacity");
        finaleRule.Rewards[0] = finaleRule.Rewards[0] with
        {
            Capacity = 100
        };
        var finaleReward = noviceServer.Dispatch("mission_end", arguments: finaleClaim).Body!["user_update"]!;
        Check(finaleReward["unit"]!["unit_list"]!.AsArray().Any(row => row?["unit_id"]?.GetValue<int>() == 1015) && finaleReward["album"]!["unit_list"]!.AsArray().Any(row => row?["unit_id"]?.GetValue<int>() == 1015), "novice finale grants its partner and album entry");
        var noviceReload = new OfflineServer(new JsonStateStore(noviceDirectory), seed, "offline://resources/");
        noviceReload.Dispatch("game_init");
        noviceReload.Dispatch("login");
        Check(noviceReload.Dispatch("mission_end", arguments: finaleClaim).Status == 404, "novice finale remains claimed after restart");
        var dailyLoginDirectory = directory + "-daily-login";
        var dailyLoginServer = new OfflineServer(new JsonStateStore(dailyLoginDirectory), seed, "offline://resources/");
        dailyLoginServer.Dispatch("game_init");
        dailyLoginServer.Dispatch("user_create");
        var loginMission = new LocalDailyLoginMission(300010, "登录", 1, [(3000101, 10), (3000102, 5), (0, 0), (0, 0)], 0, DateTime.Now);
        var loginRule = new LocalMissionRule(300010, 3, 0, 1, true, [new(12, 401, 10, 9999), new(104, 0, 5, int.MaxValue)]);
        var dailyLoginArguments = new RequestDraft(DailyLoginMission: loginMission, MissionClaim: new(300010, 0, 3, 0, 0, [loginRule]));
        Check(dailyLoginServer.Dispatch("mission_top", arguments: dailyLoginArguments).Body!["mission"]!["unlock_list"]!.AsArray().Count == 1 && dailyLoginServer.Dispatch("mission_end", arguments: dailyLoginArguments).Status == 404, "daily login mission requires a successful login");
        dailyLoginServer.Dispatch("login");
        dailyLoginArguments = dailyLoginArguments with
        {
            DailyLoginMission = loginMission with
            {
                Now = DateTime.Now
            }
        };
        var loginMissionRow = dailyLoginServer.Dispatch("mission_top", arguments: dailyLoginArguments).Body!["mission"]!["clear_list"]![0]!;
        Check(loginMissionRow["id"]!.GetValue<int>() == 300010 && loginMissionRow["reward_group2_value"]!.GetValue<int>() == 5, "login completes the daily master mission");
        var dailyLoginClaim = dailyLoginServer.Dispatch("mission_end", arguments: dailyLoginArguments).Body!;
        Check(dailyLoginClaim["reward_list"]!.AsArray().Count == 2 && dailyLoginClaim["user_update"]!["mop_up_day_coupon"]!.GetValue<int>() == 5 && dailyLoginClaim["user_update"]!["item"]!["item_list"]!.AsArray().Single(item => item!["item_id"]!.GetValue<int>() == 401)!["num"]!.GetValue<int>() == 10 && dailyLoginClaim["mission"]!["clear_list"]!.AsArray().Count == 0, "daily login grants the configured item and sweep tickets");
        var dailyLoginReload = new OfflineServer(new JsonStateStore(dailyLoginDirectory), seed, "offline://resources/");
        dailyLoginReload.Dispatch("game_init");
        Check(dailyLoginReload.Dispatch("login").Body!["user"]!["mop_up_day_coupon"]!.GetValue<int>() == 5 && dailyLoginReload.Dispatch("mission_end", arguments: dailyLoginArguments).Status == 404 && dailyLoginReload.Dispatch("mission_top", arguments: dailyLoginArguments).Body!["mission"]!["unlock_list"]!.AsArray().Count == 0, "daily login grant and receipt persist across restart");
        Check(dailyLoginReload.Dispatch("mission_top", arguments: dailyLoginArguments with { DailyLoginMission = loginMission with { Now = loginMission.Now.AddDays(1) } }).Body!["mission"]!["unlock_list"]!.AsArray().Count == 1, "new game day requires another login");
        Console.WriteLine("Passed 5 daily login mission checks.");
        var stageSeed = JsonNode.Parse(seed)!;
        stageSeed["battle_pass"] = new JsonObject
        {
            ["battle_pass_id"] = 7,
            ["battle_pass_level"] = 1,
            ["battle_pass_exp"] = 0,
            ["is_premium"] = 0,
            ["is_premium_2"] = 0
        };
        stageSeed["_local_battle_pass_top"] = new JsonObject
        {
            ["battle_pass_mission"] = new JsonObject
            {
                ["unlock_list"] = new JsonArray(),
                ["received_list"] = new JsonArray(),
                ["clear_list"] = new JsonArray(new JsonObject { ["id"] = 20, ["battle_pass_id"] = 7, ["complete_value"] = 1, ["current_value"] = 1, ["reward_battle_pass_exp"] = 100 }, new JsonObject { ["id"] = 21, ["battle_pass_id"] = 7, ["complete_value"] = 1, ["current_value"] = 1, ["reward_battle_pass_exp"] = 150 })
            }
        };
        var stageDirectory = directory + "-stage-missions";
        var stageServer = new OfflineServer(new JsonStateStore(stageDirectory), stageSeed.ToJsonString(), "offline://resources/");
        stageServer.Dispatch("game_init");
        stageServer.Dispatch("login");
        var stageClaim = new LocalBattlePassClaim(0, 1, 7, true, new[] { new LocalBattlePassTask(20, 1, 100, true), new LocalBattlePassTask(21, 1, 150, false) }, new() { [1] = 0, [2] = 100, [3] = 250 });
        Check(stageServer.Dispatch("battle_pass_mission_end", arguments: new(BattlePassClaim: stageClaim)).Status == 503 && stageServer.Dispatch("login").Body!["user"]!["battle_pass"]!["battle_pass_exp"]!.GetValue<int>() == 0 && stageServer.Dispatch("battle_pass_top").Body!["battle_pass_mission"]!["clear_list"]!.AsArray().Count == 2, "stage mission batch validation leaves experience and claim lists unchanged");
        stageClaim.Tasks[1] = stageClaim.Tasks[1] with
        {
            Active = true
        };
        var stageResult = stageServer.Dispatch("battle_pass_mission_end", arguments: new(BattlePassClaim: stageClaim)).Body!;
        Check(stageResult["user_update"]!["battle_pass"]!["battle_pass_exp"]!.GetValue<int>() == 250 && stageResult["user_update"]!["battle_pass"]!["battle_pass_level"]!.GetValue<int>() == 3 && stageResult["battle_pass_mission"]!["received_list"]!.AsArray().Count == 2, "stage mission rewards use cumulative thresholds and persist received entries");
        var stageReload = new OfflineServer(new JsonStateStore(stageDirectory), seed, "offline://resources/");
        stageReload.Dispatch("game_init");
        stageReload.Dispatch("login");
        Check(stageReload.Dispatch("battle_pass_mission_end", arguments: new(BattlePassClaim: stageClaim with { Id = 20, All = 0 })).Status == 404 && stageReload.Dispatch("battle_pass_mission_end", arguments: new(BattlePassClaim: stageClaim)).Body!["user_update"]!["battle_pass"]!["battle_pass_exp"]!.GetValue<int>() == 250, "stage claims remain consumed after restart and cannot duplicate experience");
        Console.WriteLine("Passed 3 stage mission checks.");
        var stageRewardSeed = JsonNode.Parse(seed)!;
        stageRewardSeed["battle_pass"] = new JsonObject
        {
            ["battle_pass_id"] = 7,
            ["battle_pass_level"] = 1,
            ["battle_pass_exp"] = 0,
            ["is_premium"] = 1,
            ["is_premium_2"] = 0
        };
        stageRewardSeed["_local_battle_pass_top"] = new JsonObject
        {
            ["normal_received_reward_level"] = 0,
            ["premium_received_reward_level_1"] = 0,
            ["premium_received_reward_level_2"] = 0
        };
        var stageRewardDirectory = directory + "-stage-rewards";
        var stageRewardServer = new OfflineServer(new JsonStateStore(stageRewardDirectory), stageRewardSeed.ToJsonString(), "offline://resources/");
        stageRewardServer.Dispatch("game_init");
        stageRewardServer.Dispatch("login");
        var stageRewardRule = new LocalBattlePassRewards(7, true, new[] { new LocalBattlePassLevelReward(1, new[] { new LocalInventoryReward(3, 0, 100, 10000) }, new[] { new LocalInventoryReward(12, 99990, 3, 2) }, null) });
        Check(stageRewardServer.Dispatch("battle_pass_receive_reward", arguments: new(BattlePassRewards: stageRewardRule)).Status == 409 && stageRewardServer.Dispatch("login").Body!["user"]!["gold"]!.GetValue<int>() == 5000 && stageRewardServer.Dispatch("battle_pass_top").Body!["normal_received_reward_level"]!.GetValue<int>() == 0, "reward track failure rolls back earlier grants and claim progress");
        stageRewardRule.Levels[0] = stageRewardRule.Levels[0] with
        {
            Premium = new[]
            {
            new LocalInventoryReward(12, 99990, 3, 100)
        }
        };
        var stageRewards = stageRewardServer.Dispatch("battle_pass_receive_reward", arguments: new(BattlePassRewards: stageRewardRule)).Body!;
        Check(stageRewards["received_reward_list"]!.AsArray().Count == 2 && stageRewards["normal_received_reward_level"]!.GetValue<int>() == 1 && stageRewards["premium_received_reward_level_1"]!.GetValue<int>() == 1 && stageRewards["premium_received_reward_level_2"]!.GetValue<int>() == 0, "stage rewards advance only entitled tracks and grant their inventory");
        var stageRewardReload = new OfflineServer(new JsonStateStore(stageRewardDirectory), seed, "offline://resources/");
        stageRewardReload.Dispatch("game_init");
        stageRewardReload.Dispatch("login");
        Check(stageRewardReload.Dispatch("battle_pass_receive_reward", arguments: new(BattlePassRewards: stageRewardRule)).Body!["received_reward_list"]!.AsArray().Count == 0 && stageRewardReload.Dispatch("login").Body!["user"]!["gold"]!.GetValue<int>() == 5100, "stage reward progress persists and prevents duplicate grants");
        Console.WriteLine("Passed 3 stage reward checks.");
        var rankingSeed = JsonNode.Parse(seed)!;
        rankingSeed["_local_ranking_teams"] = new JsonArray(new JsonObject { ["manage_id"] = 30, ["public_id"] = "local-entry", ["player_name"] = "local", ["difficulty"] = 1, ["stage_group_master_id"] = 40, ["area1_team_info"] = new JsonArray(new JsonObject { ["unit_id"] = 1001, ["costume_id"] = 100101 }), ["area2_team_info"] = new JsonArray(), ["area3_team_info"] = new JsonArray(), ["area4_team_info"] = new JsonArray(), ["area5_team_info"] = new JsonArray() });
        var rankingServer = new OfflineServer(new JsonStateStore(directory + "-ranking-history"), rankingSeed.ToJsonString(), "offline://resources/");
        rankingServer.Dispatch("game_init");
        rankingServer.Dispatch("login");
        var rankingQuery = new RequestDraft(Value: 30, Text: "local-entry", HistoryDefinitions: historyDefinitions with { StageGroups = new[] { 40 } });
        Check(rankingServer.Dispatch("get_series_battle_ranking_teaminfo", arguments: rankingQuery with { Value = 31 }).Status == 404 && rankingServer.Dispatch("get_series_battle_ranking_teaminfo", arguments: rankingQuery with { HistoryDefinitions = historyDefinitions }).Status == 503, "ranking query requires matching identifiers and valid stage definitions");
        var rankingReply = rankingServer.Dispatch("get_series_battle_ranking_teaminfo", arguments: rankingQuery).Body!;
        Check(rankingReply["area1_team_info"]![0]!["costume_id"]!.GetValue<int>() == 100101 && rankingReply["area5_team_info"]!.AsArray().Count == 0 && !rankingReply.ContainsKey("public_id"), "ranking history returns validated area members and omits internal query keys");
        rankingReply["area1_team_info"]![0]!["unit_id"] = 0;
        Check(rankingServer.Dispatch("get_series_battle_ranking_teaminfo", arguments: rankingQuery).Body!["area1_team_info"]![0]!["unit_id"]!.GetValue<int>() == 1001, "ranking response mutation does not alter stored history");
        Console.WriteLine("Passed 3 ranking history checks.");
        var miniOverviewSeed = JsonNode.Parse(seed)!;
        miniOverviewSeed["_local_mini_game_claims"] = new JsonObject
        {
            ["2"] = 1
        };
        miniOverviewSeed["_local_mini_game_remaining_points"] = 12;
        var miniOverviewServer = new OfflineServer(new JsonStateStore(directory + "-mini-overview"), miniOverviewSeed.ToJsonString(), "offline://resources/");
        miniOverviewServer.Dispatch("game_init");
        miniOverviewServer.Dispatch("login");
        var miniOverview = miniOverviewServer.Dispatch("mini_game_top", arguments: new(Ids: new[] { 1L, 2L })).Body!;
        Check(miniOverview["first_clear_reward_info"]!.AsArray().Count == 2 && miniOverview["first_clear_reward_info"]![0]!["get_reward_flg"]!.GetValue<int>() == 0 && miniOverview["first_clear_reward_info"]![1]!["get_reward_flg"]!.GetValue<int>() == 1 && miniOverview["get_phantasm_point"]!.GetValue<int>() == 12, "entry overview supplies every master entry and retains stored flags and remaining points");
        Check(miniOverviewServer.Dispatch("mini_game_top", arguments: new(Ids: new[] { 1L, 1L })).Status == 503 && miniOverviewServer.Dispatch("login").Body!["user"]!["phantasm_point"]!["points"]!.GetValue<int>() == 100, "entry overview rejects duplicate definitions and grants no points");
        Console.WriteLine("Passed 2 entry overview checks.");
        var pointExchangeSeed = JsonNode.Parse(seed)!;
        pointExchangeSeed["_local_gacha_points"] = new JsonArray(new JsonObject { ["gacha_point_id"] = 2, ["value"] = 300 });
        var pointExchangeServer = new OfflineServer(new JsonStateStore(directory + "-point-exchange"), pointExchangeSeed.ToJsonString(), "offline://resources/");
        pointExchangeServer.Dispatch("game_init");
        pointExchangeServer.Dispatch("login");
        var pointExchangeRule = new LocalPointExchange(8, 2, 100, 1, 10, 501, 1000, true, new(501, 10, 20, 30));
        var pointExchangeBefore = pointExchangeServer.Dispatch("login").Body!["user"]!.ToJsonString();
        Check(pointExchangeServer.Dispatch("gacha_point_exchange", arguments: new(PointExchange: pointExchangeRule with { Active = false })).Status == 409 && pointExchangeServer.Dispatch("gacha_point_exchange", arguments: new(PointExchange: pointExchangeRule with { Cost = 301 })).Status == 409 && pointExchangeServer.Dispatch("gacha_point_exchange", arguments: new(PointExchange: pointExchangeRule with { Capacity = 0 })).Status == 503 && pointExchangeServer.Dispatch("login").Body!["user"]!.ToJsonString() == pointExchangeBefore, "point exchange rejects unavailable offers and insufficient funds without state changes");
        var pointExchangeResult = pointExchangeServer.Dispatch("gacha_point_exchange", arguments: new(PointExchange: pointExchangeRule)).Body!;
        var pointExchangeItem = pointExchangeResult["user_update"]!["picture"]!["picture_list"]!.AsArray().Single(row => row!["user_picture_id"]!.GetValue<long>() == pointExchangeResult["gain_object"]!["unique_id"]!.GetValue<long>())!;
        Check(pointExchangeResult["gacha_point"]!.GetValue<int>() == 200 && pointExchangeResult["remaining_count"]!.GetValue<int>() == 0 && pointExchangeItem["picture_id"]!.GetValue<int>() == 501 && pointExchangeItem["parameter2_value"]!.GetValue<int>() == 20, "point exchange returns remaining balance and count with the persisted equipment identifier");
        var pointExchangeReload = new OfflineServer(new JsonStateStore(directory + "-point-exchange"), seed, "offline://resources/");
        pointExchangeReload.Dispatch("game_init");
        pointExchangeReload.Dispatch("login");
        var pointExchangeOverview = pointExchangeReload.Dispatch("gacha_top").Body!;
        Check(pointExchangeReload.Dispatch("gacha_point_exchange", arguments: new(PointExchange: pointExchangeRule)).Status == 409 && pointExchangeOverview["gacha_point_list"]![0]!["value"]!.GetValue<int>() == 200 && pointExchangeOverview["gacha_point_exchange_count_list"]![0]!["count"]!.GetValue<int>() == 1, "point exchange limits and overview balances survive restart");
        Console.WriteLine("Passed 3 point exchange checks.");
        Check(relationReload.Dispatch("user_twitter_info_update", arguments: new(Text: " ")).Status == 400 && relationReload.Dispatch("user_twitter_info_update", arguments: new(Text: "invalid\nidentity")).Status == 400 && relationReload.Dispatch("user_twitter_info_update", arguments: new(Text: "local-updated")).Body!["user_update"]!["twitter_info"]!["is_related"]!.GetValue<int>() == 1, "local relation update validates and saves its identity");
        Check(relationReload.Dispatch("user_twitter_info_update", arguments: new(Text: "local-updated")).Status == 0 && relationReload.Dispatch("user_twitter_info_update", arguments: new(Text: "replacement")).Status == 409 && relationReload.Dispatch("user_twitter_info_delete", arguments: new(Text: "replacement")).Status == 409, "local relation retries preserve the saved identity and reject replacement");
        var relationUpdateReload = new OfflineServer(new JsonStateStore(relationDirectory), seed, "offline://resources/");
        relationUpdateReload.Dispatch("game_init");
        relationUpdateReload.Dispatch("login");
        Check(relationUpdateReload.Dispatch("login").Body!["user"]!["_local_twitter_user_id"]!.GetValue<string>() == "local-updated" && relationUpdateReload.Dispatch("user_twitter_info_delete", arguments: new(Text: "local-updated")).Status == 0, "updated local relation survives restart and can be cleared");
        Console.WriteLine("Passed 3 local relation update checks.");
    }
}
