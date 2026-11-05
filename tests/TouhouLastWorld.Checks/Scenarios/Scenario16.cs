using System.Text.Json.Nodes;
using TouhouLastWorld;

internal sealed partial class RegressionSuite
{
    private void Scenario16()
    {
        var arenaEndSeed = arenaWeeklySeed.DeepClone();
        arenaEndSeed["_local_weekly_arena"]!["total_battle_score"] = 0;
        for (var number = 1; number <= 4; number++)
        {
            var entry = arenaEndSeed["_local_weekly_arena"]!["weekly_battle_data"]![$"battle_number{number}"]!;
            entry["total_score"] = 0;
            entry["is_clear"] = 0;
            entry["unit_list"] = new JsonArray();
            entry["user_unit_list"] = new JsonArray();
        }

        var arenaEndDirectory = directory + "-arena-end";
        var arenaEndServer = new OfflineServer(new JsonStateStore(arenaEndDirectory), arenaEndSeed.ToJsonString(), "offline://resources/");
        arenaEndServer.Dispatch("game_init");
        arenaEndServer.Dispatch("login");
        Check(arenaEndServer.Dispatch("arena_battle_start", arguments: new(ArenaStart: arenaWeeklyRule)).Status == 0, "weekly settlement starts with a persisted entry");
        var arenaEndRule = new LocalArenaEnd(1, 1, [], false, new(1, 0, 64, 24, 20, 20), [new(1, 20, 5), new(2, 60, 7)], 100);
        Check(arenaEndServer.Dispatch("arena_battle_end", arguments: new(ArenaEnd: arenaEndRule with { Score = new(1, 0, 65, 24, 20, 20) })).Status == 409 && arenaEndServer.Dispatch("arena_battle_end", arguments: new(ArenaEnd: arenaEndRule with { Capacity = 10 })).Status == 409, "score mismatch and reward overflow leave the pending settlement intact");
        var arenaEndReply = arenaEndServer.Dispatch("arena_battle_end", arguments: new(ArenaEnd: arenaEndRule));
        Check(arenaEndReply.Status == 0 && arenaEndReply.Body!["first_cleared_reward_list"]![0]!["object_type"]!.GetValue<int>() == 21 && arenaEndReply.Body["user_update"]!["arena_point"]!.GetValue<int>() == 12 && arenaEndServer.Dispatch("arena_battle_end", arguments: new(ArenaEnd: arenaEndRule)).Status == 409, "weekly settlement grants crossed thresholds once and consumes the pending entry");
        var arenaEndReload = new OfflineServer(new JsonStateStore(arenaEndDirectory), seed, "offline://resources/");
        arenaEndReload.Dispatch("game_init");
        arenaEndReload.Dispatch("login");
        arenaEndReload.Dispatch("arena_battle_start", arguments: new(ArenaStart: arenaWeeklyRule));
        var arenaLowerEnd = arenaEndReload.Dispatch("arena_battle_end", arguments: new(ArenaEnd: arenaEndRule with { Score = new(17, 10, 20, 0, 0, 20) }));
        Check(arenaLowerEnd.Status == 0 && arenaLowerEnd.Body!["first_cleared_reward_list"]!.AsArray().Count == 0 && arenaEndReload.Dispatch("weekly_arena_top").Body!["weekly_battle_data"]!["battle_number1"]!["total_score"]!.GetValue<int>() == 64, "lower repeat scores preserve the saved best score and do not grant thresholds again");
        Check(arenaEntryReload.Dispatch("arena_battle_end", arguments: new(ArenaEnd: new(1, 0, [], false, null, null, 0))).Status == 503 && arenaEntryReload.Dispatch("arena_battle_end", arguments: new(ArenaEnd: new(0, 0, [], false, null, null, 0))).Status == 0 && arenaEntryReload.Dispatch("arena_battle_start", arguments: new(ArenaStart: arenaEntryRule)).Status == 0, "missing daily reward definitions retain the entry while defeat releases it for another attempt");
        Console.WriteLine("Passed 5 arena settlement checks.");
        var dailyEndSeed = arenaEntrySeed.DeepClone();
        dailyEndSeed["_local_daily_arena"]!["clear_diffculty"] = 0;
        var dailyEndDirectory = directory + "-daily-end";
        var dailyEndServer = new OfflineServer(new JsonStateStore(dailyEndDirectory), dailyEndSeed.ToJsonString(), "offline://resources/");
        dailyEndServer.Dispatch("game_init");
        dailyEndServer.Dispatch("login");
        dailyEndServer.Dispatch("arena_battle_start", arguments: new(ArenaStart: arenaEntryRule));
        var dailyEndDefinition = new LocalArenaDaily(1, [new(1, [new(3, 0, 5, 100000), new(21, 0, 3, 100)]), new(2, [new(6, 0, 2, 100000)])]);
        var dailyEndRule = new LocalArenaEnd(1, 0, [], false, null, null, 0, dailyEndDefinition);
        Check(dailyEndServer.Dispatch("arena_battle_end", arguments: new(ArenaEnd: dailyEndRule with { Daily = dailyEndDefinition with { Day = 2 } })).Status == 503 && dailyEndServer.Dispatch("arena_battle_end", arguments: new(ArenaEnd: dailyEndRule with { Daily = new(1, [dailyEndDefinition.Stages[0], new(2, [new(6, 0, 2, 1)])]) })).Status == 409, "wrong rotation day and later reward overflow preserve the complete daily settlement");
        var dailyEndReply = dailyEndServer.Dispatch("arena_battle_end", arguments: new(ArenaEnd: dailyEndRule));
        Check(dailyEndReply.Status == 0 && dailyEndReply.Body!["first_cleared_reward_list"]!.AsArray().Count == 3 && dailyEndReply.Body["user_update"]!["arena_point"]!.GetValue<int>() == 3 && dailyEndReply.Body["user_update"]!["gold"]!.GetValue<int>() == dailyEndSeed["gold"]!.GetValue<int>() + 5 && dailyEndReply.Body["user_arena"]!["daily_cleared_difficulty"]!.GetValue<int>() == 2, "daily victory grants each newly cleared stage once after atomic rollback");
        var dailyEndReload = new OfflineServer(new JsonStateStore(dailyEndDirectory), seed, "offline://resources/");
        dailyEndReload.Dispatch("game_init");
        dailyEndReload.Dispatch("login");
        var dailyEndOverview = dailyEndReload.Dispatch("daily_arena_top").Body!;
        Check(dailyEndOverview["clear_diffculty"]!.GetValue<int>() == 2 && dailyEndOverview["last_clear_difficulty"]!.GetValue<int>() == 2 && dailyEndOverview["reward_day_count"]!.GetValue<int>() == 1, "daily progress survives restart without changing the rotation day");
        dailyEndReload.Dispatch("arena_battle_start", arguments: new(ArenaStart: arenaEntryRule));
        var dailyEndRepeat = dailyEndReload.Dispatch("arena_battle_end", arguments: new(ArenaEnd: dailyEndRule));
        Check(dailyEndRepeat.Status == 0 && dailyEndRepeat.Body!["first_cleared_reward_list"]!.AsArray().Count == 0 && dailyEndRepeat.Body["user_update"]!["arena_point"]!.GetValue<int>() == 3 && dailyEndReload.Dispatch("arena_battle_end", arguments: new(ArenaEnd: dailyEndRule)).Status == 409, "repeated daily victories do not grant cleared stages again");
        Console.WriteLine("Passed 4 daily settlement checks.");
        var versusEntrySeed = JsonNode.Parse(seed)!;
        versusEntrySeed["quest_playdata"] = new JsonArray(new JsonObject { ["episode_id"] = 901, ["normal"] = new JsonObject { ["status"] = 2 } });
        versusEntrySeed["_local_versus_entry_costs"] = new JsonObject
        {
            ["7"] = 10
        };
        var versusEntryDirectory = directory + "-versus-entry";
        var versusEntryServer = new OfflineServer(new JsonStateStore(versusEntryDirectory), versusEntrySeed.ToJsonString(), "offline://resources/");
        versusEntryServer.Dispatch("game_init");
        versusEntryServer.Dispatch("login");
        var versusEntryRule = new LocalVersusStart(7, 60, 60, 1, true, 901, false, []);
        Check(versusEntryServer.Dispatch("multiple_spirits_weekly_top").Status == 503 && versusEntryServer.Dispatch("multiple_spirits_weekly_top", arguments: new(Ids: [7])).Body!["clear_info"]![0]!["cleared_level"]!.GetValue<int>() == 0, "entry overview requires definitions and supplies initial progress for active entries");
        Check(versusEntryServer.Dispatch("multiple_spirits_weekly_battle_start", arguments: new(VersusStart: versusEntryRule with { ReleaseEpisode = 902 })).Status == 409 && versusEntryServer.Dispatch("multiple_spirits_weekly_battle_start", arguments: new(VersusStart: versusEntryRule with { Helper = true })).Status == 503 && versusEntryServer.Dispatch("multiple_spirits_weekly_battle_start", arguments: new(VersusStart: versusEntryRule with { FinalLevel = 65, Rules = [new(1, 5)] })).Status == 409, "locked episode, helper use and locked extra rules do not admit a battle");
        var versusEntryReply = versusEntryServer.Dispatch("multiple_spirits_weekly_battle_start", arguments: new(VersusStart: versusEntryRule));
        var versusEntryRetry = versusEntryServer.Dispatch("multiple_spirits_weekly_battle_start", arguments: new(VersusStart: versusEntryRule));
        Check(versusEntryReply.Status == 0 && versusEntryRetry.Status == 0 && versusEntryRetry.Body!["user_update"]!["phantasm_point"]!["points"]!.GetValue<int>() == 90 && versusEntryServer.Dispatch("multiple_spirits_weekly_battle_start", arguments: new(VersusStart: versusEntryRule with { BaseLevel = 61, FinalLevel = 61 })).Status == 409, "entry payment is idempotent and a different challenge cannot replace the pending battle");
        var versusEntryReload = new OfflineServer(new JsonStateStore(versusEntryDirectory), seed, "offline://resources/");
        versusEntryReload.Dispatch("game_init");
        Check(versusEntryReload.Dispatch("login").Body!["user"]!["_local_versus_pending"]!["base_level"]!.GetValue<int>() == 60, "pending challenge survives restart without granting progress");
        versusEntrySeed["_local_versus_clear_info"] = new JsonArray(new JsonObject { ["multiple_spirits_weekly_master_id"] = 7, ["cleared_level"] = 100, ["add_rules_cleared_level"] = new JsonArray() });
        var versusRulesServer = new OfflineServer(new JsonStateStore(directory + "-versus-rules"), versusEntrySeed.ToJsonString(), "offline://resources/");
        versusRulesServer.Dispatch("game_init");
        versusRulesServer.Dispatch("login");
        var versusRulesEntry = versusEntryRule with
        {
            BaseLevel = 100,
            FinalLevel = 110,
            Rules = [new(1, 5), new(2, 5)]
        };
        Check(versusRulesServer.Dispatch("multiple_spirits_weekly_battle_start", arguments: new(VersusStart: versusRulesEntry with { FinalLevel = 109 })).Status == 400 && versusRulesServer.Dispatch("multiple_spirits_weekly_battle_start", arguments: new(VersusStart: versusRulesEntry)).Status == 0, "unlocked additional rules require the exact summed challenge level");
        Console.WriteLine("Passed 5 versus admission checks.");
        var versusEndRule = new LocalVersusEnd(7, 60, 60, 1, [], [], false, [new(60, new(3, 0, 5, 100000), false)]);
        Check(versusEntryReload.Dispatch("multiple_spirits_weekly_battle_end", arguments: new(VersusEnd: versusEndRule with { Rules = [1] })).Status == 409 && versusEntryReload.Dispatch("multiple_spirits_weekly_battle_end", arguments: new(VersusEnd: versusEndRule with { Ranking = true })).Status == 503 && versusEntryReload.Dispatch("multiple_spirits_weekly_battle_end", arguments: new(VersusEnd: versusEndRule with { Rewards = [new(60, null, true)] })).Status == 503, "changed rules, ranking and random drops retain the pending challenge");
        var versusEndReply = versusEntryReload.Dispatch("multiple_spirits_weekly_battle_end", arguments: new(VersusEnd: versusEndRule));
        Check(versusEndReply.Status == 0 && versusEndReply.Body!["user_update"]!["gold"]!.GetValue<int>() == versusEntrySeed["gold"]!.GetValue<int>() + 5 && versusEntryReload.Dispatch("multiple_spirits_weekly_battle_end", arguments: new(VersusEnd: versusEndRule)).Status == 409, "initial fixed reward is granted once and consumes the pending challenge");
        var versusEndReload = new OfflineServer(new JsonStateStore(versusEntryDirectory), seed, "offline://resources/");
        versusEndReload.Dispatch("game_init");
        versusEndReload.Dispatch("login");
        Check(versusEndReload.Dispatch("multiple_spirits_weekly_top", arguments: new(Ids: [7])).Body!["clear_info"]![0]!["cleared_level"]!.GetValue<int>() == 60 && versusEndReload.Dispatch("multiple_spirits_weekly_battle_start", arguments: new(VersusStart: versusEntryRule)).Status == 0 && versusEndReload.Dispatch("multiple_spirits_weekly_battle_end", arguments: new(VersusEnd: versusEndRule)).Body!["drop_list"]!.AsArray().Count == 0, "clear progress survives restart and repeating the level grants no first reward");
        var versusExtraRewards = Enumerable.Range(101, 10).Select(level => new LocalVersusReward(level, new(3, 0, 1, 100000), false)).ToArray();
        var versusExtraEnd = new LocalVersusEnd(7, 100, 110, 1, [2, 1], [], false, versusExtraRewards);
        Check(versusRulesServer.Dispatch("multiple_spirits_weekly_battle_end", arguments: new(VersusEnd: versusExtraEnd with { Rewards = versusExtraRewards.Skip(1).ToArray() })).Status == 503 && versusRulesServer.Dispatch("multiple_spirits_weekly_battle_end", arguments: new(VersusEnd: versusExtraEnd with { Rewards = versusExtraRewards.Take(9).Append(new(110, new(6, 0, 1, 1), false)).ToArray() })).Status == 409, "missing intermediate rewards and later capacity failure leave all progress unchanged");
        var versusExtraReply = versusRulesServer.Dispatch("multiple_spirits_weekly_battle_end", arguments: new(VersusEnd: versusExtraEnd));
        var versusExtraInfo = versusRulesServer.Dispatch("multiple_spirits_weekly_top", arguments: new(Ids: [7])).Body!["clear_info"]![0]!;
        Check(versusExtraReply.Status == 0 && versusExtraReply.Body!["user_update"]!["gold"]!.GetValue<int>() == versusEntrySeed["gold"]!.GetValue<int>() + 10 && versusExtraInfo["cleared_level"]!.GetValue<int>() == 100 && versusExtraInfo["add_rules_cleared_level"]![0]!.GetValue<int>() == 110, "additional rules grant newly crossed levels atomically and preserve the base level");
        versusRulesServer.Dispatch("multiple_spirits_weekly_battle_start", arguments: new(VersusStart: versusRulesEntry));
        Check(versusRulesServer.Dispatch("multiple_spirits_weekly_battle_end", arguments: new(VersusEnd: versusExtraEnd with { Result = 0, Rewards = null })).Status == 0 && versusRulesServer.Dispatch("multiple_spirits_weekly_battle_end", arguments: new(VersusEnd: versusExtraEnd)).Status == 409, "defeat consumes the challenge without requiring rewards or granting progress");
        Console.WriteLine("Passed 6 versus settlement checks.");
        var batchSeed = JsonNode.Parse(seed)!;
        var batchUnit = batchSeed["unit"]!["unit_list"]![0]!["unit_id"]!.GetValue<int>();
        batchSeed["character_quest_list"] = new JsonArray(new JsonObject { ["character_quest_id"] = 101, ["count"] = 1, ["lunatic"] = new JsonObject { ["status"] = 3 } }, new JsonObject { ["character_quest_id"] = 102, ["count"] = 0, ["lunatic"] = new JsonObject { ["status"] = 3 } });
        batchSeed["_local_batch_search_rewards"] = new JsonObject
        {
            ["101"] = new JsonObject
            {
                ["count"] = 1,
                ["runs"] = new JsonArray(new JsonArray(new JsonObject { ["object_type"] = 3, ["object_id"] = 0, ["object_value"] = 5 }), new JsonArray(new JsonObject { ["object_type"] = 6, ["object_id"] = 0, ["object_value"] = 2 }))
            },
            ["102"] = new JsonObject
            {
                ["count"] = 0,
                ["runs"] = new JsonArray(new JsonArray(new JsonObject { ["object_type"] = 3, ["object_id"] = 0, ["object_value"] = 7 }))
            }
        };
        var batchDirectory = directory + "-batch";
        var batchServer = new OfflineServer(new JsonStateStore(batchDirectory), batchSeed.ToJsonString(), "offline://resources/");
        batchServer.Dispatch("game_init");
        batchServer.Dispatch("login");
        var batchRule = new LocalBatchSearch([new(101, batchUnit, 3, true), new(102, batchUnit, 1, true)], [new(3, 0, 0, 100000), new(6, 0, 0, 100000)]);
        Check(batchServer.Dispatch("batch_search", arguments: new(Batch: batchRule with { Quests = [batchRule.Quests[0], batchRule.Quests[0]] })).Status == 400 && batchServer.Dispatch("batch_search", arguments: new(Batch: batchRule with { Quests = [batchRule.Quests[0] with { Eligible = false }] })).Status == 409, "batch duplicate selection and ineligible members reject without consuming attempts");
        Check(batchServer.Dispatch("batch_search", arguments: new(Batch: batchRule with { Quests = [batchRule.Quests[0], batchRule.Quests[1] with { Limit = 2 }] })).Status == 503 && batchServer.Dispatch("batch_search", arguments: new(Batch: batchRule with { Rewards = [batchRule.Rewards[0], new(6, 0, 0, 1)] })).Status == 409, "later missing outcomes and reward capacity failures roll back the complete batch");
        var batchReply = batchServer.Dispatch("batch_search", arguments: new(Batch: batchRule));
        Check(batchReply.Status == 0 && batchReply.Body!["drop_list"]!.AsArray().Count == 3 && batchReply.Body["user_update"]!["gold"]!.GetValue<int>() == batchSeed["gold"]!.GetValue<int>() + 12 && batchReply.Body["user_update"]!["phantasm_point"]!["points"]!.GetValue<int>() == 64 && batchReply.Body["user_update"]!["character_quest_list"]![0]!["count"]!.GetValue<int>() == 3, "batch grants prepared outcomes and charges twelve points per remaining attempt exactly once");
        var batchReload = new OfflineServer(new JsonStateStore(batchDirectory), seed, "offline://resources/");
        batchReload.Dispatch("game_init");
        var batchSaved = batchReload.Dispatch("login").Body!["user"]!;
        Check(batchSaved["_local_batch_search_rewards"]!.AsObject().Count == 0 && batchReload.Dispatch("batch_search", arguments: new(Batch: batchRule)).Status == 409, "consumed outcomes and attempt counts survive restart and reject repeated batches");
        var batchPoorSeed = batchSeed.DeepClone();
        batchPoorSeed["phantasm_point"]!["points"] = 35;
        var batchPoorServer = new OfflineServer(new JsonStateStore(directory + "-batch-poor"), batchPoorSeed.ToJsonString(), "offline://resources/");
        batchPoorServer.Dispatch("game_init");
        batchPoorServer.Dispatch("login");
        Check(batchPoorServer.Dispatch("batch_search", arguments: new(Batch: batchRule)).Status == 409 && batchPoorServer.Dispatch("batch_search", arguments: new(Batch: batchRule with { Quests = [batchRule.Quests[0]] })).Body!["drop_list"]!.AsArray().Count == 2, "insufficient balance preserves prepared rewards for a smaller affordable batch");
        Console.WriteLine("Passed 5 batch settlement checks.");
        var rerollSeed = JsonNode.Parse(seed)!;
        rerollSeed["_local_rogue_draw"] = new JsonObject
        {
            ["drawn_seals"] = new JsonArray(11, 12, 13),
            ["remaining_draws"] = 2,
            ["remaining_rerolls"] = 2
        };
        rerollSeed["_local_rogue_reroll_sequence"] = new JsonArray(new JsonArray(14, 15, 16), new JsonArray(11, 12, 13));
        var rerollDirectory = directory + "-reroll";
        var rerollServer = new OfflineServer(new JsonStateStore(rerollDirectory), rerollSeed.ToJsonString(), "offline://resources/");
        rerollServer.Dispatch("game_init");
        rerollServer.Dispatch("login");
        var rerollRule = new LocalReroll(0, [11, 12, 13], 2, 2, [11, 12, 13, 14, 15, 16]);
        Check(rerollServer.Dispatch("roguelite_reroll", arguments: new(Reroll: rerollRule with { Selected = 11 })).Status == 503 && rerollServer.Dispatch("roguelite_reroll", arguments: new(Reroll: rerollRule with { Offered = [14, 15, 16] })).Status == 409 && rerollServer.Dispatch("roguelite_reroll", arguments: new(Reroll: rerollRule with { Definitions = [11, 12, 13] })).Status == 503, "effect redraw, stale selection and unknown prepared candidates preserve the draw");
        var rerollReply = rerollServer.Dispatch("roguelite_reroll", arguments: new(Reroll: rerollRule));
        Check(rerollReply.Status == 0 && rerollReply.Body!["drawn_seals"]![0]!.GetValue<int>() == 14 && rerollReply.Body["remaining_draws"]!.GetValue<int>() == 2 && rerollReply.Body["remaining_rerolls"]!.GetValue<int>() == 1 && rerollServer.Dispatch("roguelite_reroll", arguments: new(Reroll: rerollRule)).Status == 409, "ordinary redraw consumes one reroll while rejecting stale retries");
        var rerollReload = new OfflineServer(new JsonStateStore(rerollDirectory), seed, "offline://resources/");
        rerollReload.Dispatch("game_init");
        var rerollSaved = rerollReload.Dispatch("login").Body!["user"]!;
        Check(rerollSaved["_local_rogue_draw"]!["remaining_rerolls"]!.GetValue<int>() == 1 && rerollSaved["_local_rogue_reroll_sequence"]!.AsArray().Count == 1 && rerollReload.Dispatch("roguelite_reroll", arguments: new(Reroll: rerollRule with { Offered = [14, 15, 16], Rerolls = 1 })).Status == 0 && rerollReload.Dispatch("roguelite_reroll", arguments: new(Reroll: rerollRule with { Rerolls = 0 })).Status == 409, "saved draw outcomes persist and exhausted rerolls cannot advance again");
        Console.WriteLine("Passed 3 redraw settlement checks.");
    }
}
