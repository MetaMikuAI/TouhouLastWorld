using System.Text.Json.Nodes;
using TouhouLastWorld;

internal sealed partial class RegressionSuite
{
    private void Scenario15()
    {
        var seriesStartSeed = JsonNode.Parse(seed)!;
        var seriesStartId = seriesStartSeed["unit"]!["unit_list"]![0]!["user_unit_id"]!.GetValue<long>();
        seriesStartSeed["team"]!["team_list"]!.AsArray().Add(new JsonObject { ["number"] = 80, ["user_unit_id_list"] = new JsonArray(seriesStartId, 0L, 0L, 0L, 0L, 0L) });
        var seriesStartServer = new OfflineServer(new JsonStateStore(directory + "-series-start"), seriesStartSeed.ToJsonString(), "offline://resources/");
        seriesStartServer.Dispatch("game_init");
        seriesStartServer.Dispatch("login");
        var seriesStartRule = new LocalSeriesStart(901, 0, 70, 71, 1, true, new[] { 0 });
        Check(seriesStartServer.Dispatch("series_battle_start", arguments: new(SeriesStart: seriesStartRule with { Active = false })).Status == 1008 && seriesStartServer.Dispatch("series_battle_start", arguments: new(SeriesStart: seriesStartRule with { Replays = new[] { 1 } })).Status == 409 && seriesStartServer.Dispatch("login").Body!["user"]!["series_battle_status"]!["progress"]!.GetValue<int>() == 0, "series start rejects closed entries and missing replay without starting progress");
        var seriesStartResponse = seriesStartServer.Dispatch("series_battle_start", arguments: new(SeriesStart: seriesStartRule)).Body!;
        Check(seriesStartResponse["user_update"]!["series_battle_status"]!["progress"]!.GetValue<int>() == 1 && seriesStartServer.Dispatch("series_battle_start", arguments: new(SeriesStart: seriesStartRule with { EpisodeId = 902 })).Status == 409 && seriesStartServer.Dispatch("series_battle_start", arguments: new(SeriesStart: seriesStartRule with { Replays = new[] { 1 } })).Status == 409, "series start persists initial progress and protects active selection");
        var seriesStartReload = new OfflineServer(new JsonStateStore(directory + "-series-start"), seed, "offline://resources/");
        seriesStartReload.Dispatch("game_init");
        seriesStartReload.Dispatch("login");
        Check(seriesStartReload.Dispatch("series_battle_start", arguments: new(SeriesStart: seriesStartRule with { Replays = null })).Body!["user_update"]!["series_battle_status"]!["progress"]!.GetValue<int>() == 1 && seriesStartReload.Dispatch("series_battle_reset").Status == 0 && seriesStartReload.Dispatch("series_battle_start", arguments: new(SeriesStart: seriesStartRule)).Status == 0, "series retry survives restart without advancing and reset permits a new start");
        Console.WriteLine("Passed 3 series start checks.");
        seriesStartSeed["team"]!["team_list"]!.AsArray().Add(new JsonObject { ["number"] = 81, ["user_unit_id_list"] = new JsonArray(seriesStartId, 0L, 0L, 0L, 0L, 0L) });
        var seriesEndDirectory = directory + "-series-end";
        var seriesEndServer = new OfflineServer(new JsonStateStore(seriesEndDirectory), seriesStartSeed.ToJsonString(), "offline://resources/");
        seriesEndServer.Dispatch("game_init");
        seriesEndServer.Dispatch("login");
        var seriesEndStart = seriesStartRule with
        {
            AreaCount = 2,
            Replays = new[]
            {
            0,
            0
        }
        };
        seriesEndServer.Dispatch("series_battle_start", arguments: new(SeriesStart: seriesEndStart));
        var seriesEndRule = new LocalSeriesEnd(901, 0, 1, 2, 1, new[] { new LocalInventoryReward(3, 0, 10, int.MaxValue) }, new[] { new LocalInventoryReward(6, 0, 5, int.MaxValue) });
        var seriesEndBefore = seriesEndServer.Dispatch("login").Body!["user"]!.ToJsonString();
        Check(seriesEndServer.Dispatch("series_battle_end", arguments: new(SeriesEnd: seriesEndRule with { Rewards = null })).Status == 503 && seriesEndServer.Dispatch("series_battle_end", arguments: new(SeriesEnd: seriesEndRule with { Rewards = new[] { new LocalInventoryReward(3, 0, 10, 1) } })).Status == 409 && seriesEndServer.Dispatch("login").Body!["user"]!.ToJsonString() == seriesEndBefore, "series settlement preserves pending state and progress when reward definitions or capacity fail");
        Check(seriesEndServer.Dispatch("series_battle_end", arguments: new(SeriesEnd: seriesEndRule)).Body!["user_update"]!["series_battle_status"]!["progress"]!.GetValue<int>() == 2 && seriesEndServer.Dispatch("series_battle_end", arguments: new(SeriesEnd: seriesEndRule)).Status == 409, "series settlement advances once and consumes the pending entry");
        seriesEndServer.Dispatch("series_battle_start", arguments: new(SeriesStart: seriesEndStart with { Replays = null }));
        Check(seriesEndServer.Dispatch("series_battle_end", arguments: new(SeriesEnd: seriesEndRule with { Progress = 2, Result = 0, Rewards = null })).Body!["user_update"]!["series_battle_status"]!["progress"]!.GetValue<int>() == 2, "series loss retains progress and grants no reward");
        seriesEndServer.Dispatch("series_battle_start", arguments: new(SeriesStart: seriesEndStart with { Replays = null }));
        var seriesEndFinal = seriesEndServer.Dispatch("series_battle_end", arguments: new(SeriesEnd: seriesEndRule with { Progress = 2 })).Body!;
        var seriesEndReload = new OfflineServer(new JsonStateStore(seriesEndDirectory), seed, "offline://resources/");
        seriesEndReload.Dispatch("game_init");
        seriesEndReload.Dispatch("login");
        seriesEndReload.Dispatch("series_battle_start", arguments: new(SeriesStart: seriesEndStart));
        seriesEndReload.Dispatch("series_battle_end", arguments: new(SeriesEnd: seriesEndRule));
        seriesEndReload.Dispatch("series_battle_start", arguments: new(SeriesStart: seriesEndStart with { Replays = null }));
        var seriesEndRepeat = seriesEndReload.Dispatch("series_battle_end", arguments: new(SeriesEnd: seriesEndRule with { Progress = 2 })).Body!;
        Check(seriesEndFinal["user_update"]!["series_battle_status"]!["progress"]!.GetValue<int>() == 0 && seriesEndFinal["drop_list"]!.AsArray().Count == 2 && seriesEndRepeat["drop_list"]!.AsArray().Count == 1 && seriesEndRepeat["user_update"]!["quest_playdata"]!.AsArray().Single(row => row!["episode_id"]!.GetValue<int>() == 901)!["normal"]!["status"]!.GetValue<int>() == 2 && seriesEndRepeat["user_update"]!["crystal"]!.GetValue<int>() == seriesStartSeed["crystal"]!.GetValue<int>() + 5, "series completion survives restart and first completion rewards are not repeated");
        Console.WriteLine("Passed 4 series settlement checks.");
        var towerStartSeed = JsonNode.Parse(seed)!;
        towerStartSeed["tower"]!["cleared_floor"] = 0;
        var towerStartTeam = towerStartSeed["team"]!["team_list"]!.AsArray().First(row => row!["user_unit_id_list"]!.AsArray().Any(id => id!.GetValue<long>() > 0))!["number"]!.GetValue<int>();
        var towerStartDirectory = directory + "-tower-start";
        var towerStartServer = new OfflineServer(new JsonStateStore(towerStartDirectory), towerStartSeed.ToJsonString(), "offline://resources/");
        towerStartServer.Dispatch("game_init");
        var beforeTowerStart = towerStartServer.Dispatch("login").Body!["user"]!;
        var towerStartRule = new LocalTowerStart(1, 101, 1, towerStartTeam);
        Check(towerStartServer.Dispatch("tower_battle_start", arguments: new(TowerStart: towerStartRule with { Floor = 2 })).Status == 409 && towerStartServer.Dispatch("tower_battle_start", arguments: new(TowerStart: towerStartRule with { TeamNumber = 99999 })).Status == 409 && towerStartServer.Dispatch("login").Body!["user"]!["_local_tower_pending"] is null, "floor entry validates progress and team before persisting");
        Check(towerStartServer.Dispatch("tower_battle_start", arguments: new(TowerStart: towerStartRule)).Status == 0 && towerStartServer.Dispatch("tower_battle_start", arguments: new(TowerStart: towerStartRule)).Status == 0 && towerStartServer.Dispatch("tower_battle_start", arguments: new(TowerStart: towerStartRule with { BattleId = 102 })).Status == 409, "floor entry retries are idempotent and preserve the pending target");
        var towerStartReload = new OfflineServer(new JsonStateStore(towerStartDirectory), seed, "offline://resources/");
        towerStartReload.Dispatch("game_init");
        var towerStartSaved = towerStartReload.Dispatch("login").Body!["user"]!;
        Check(towerStartSaved["_local_tower_pending"]!["battle_id"]!.GetValue<int>() == 101 && towerStartSaved["tower"]!["cleared_floor"]!.GetValue<int>() == 0 && JsonNode.DeepEquals(towerStartSaved["unit"], beforeTowerStart["unit"]), "floor entry persists without changing completion or restoring units");
        Console.WriteLine("Passed 3 floor entry checks.");
        var towerEndEnemies = "100," + string.Join(",", Enumerable.Repeat("0", 25)) + "\n";
        var towerEndRule = new LocalTowerEnd(1, 101, 1, 1, 0, towerUnitCsv + ",\n", towerEndEnemies, new[] { 1L }, new[] { 100 }, new[] { new LocalInventoryReward(3, 0, 10, int.MaxValue) }, new[] { new LocalInventoryReward(3, 0, 2, int.MaxValue) });
        Check(towerStartReload.Dispatch("tower_battle_end", arguments: new(TowerEnd: towerEndRule with { Enemies = "100,0" })).Status == 400 && towerStartReload.Dispatch("tower_battle_end", arguments: new(TowerEnd: towerEndRule with { Units = "" })).Status == 400 && towerStartReload.PendingTowerId == 1, "floor settlement rejects incomplete snapshots and retains its pending entry");
        var towerEndLoss = towerStartReload.Dispatch("tower_battle_end", arguments: new(TowerEnd: towerEndRule)).Body!;
        var towerEndReload = new OfflineServer(new JsonStateStore(towerStartDirectory), seed, "offline://resources/");
        towerEndReload.Dispatch("game_init");
        towerEndReload.Dispatch("login");
        Check(towerEndLoss["drop_list"]!.AsArray().Count == 0 && towerEndReload.PendingTowerId == 0 && towerEndReload.Dispatch("tower_top").Body!["enemy_battle_info"]!.GetValue<string>() == towerEndEnemies && towerEndReload.Dispatch("get_tower_unit_info").Body!["tower_unit_info"]!.GetValue<string>() == towerEndRule.Units, "floor loss preserves both snapshots across restart without granting rewards");
        towerEndReload.Dispatch("tower_battle_start", arguments: new(TowerStart: towerStartRule with { MaxFloor = 1 }));
        var towerEndWin = towerEndRule with
        {
            Result = 1,
            Enemies = "",
            Defeated = []
        };
        Check(towerEndReload.Dispatch("tower_battle_end", arguments: new(TowerEnd: towerEndWin with { FirstRewards = new[] { new LocalInventoryReward(3, 0, 10, 1) } })).Status == 409 && towerEndReload.PendingTowerId == 1 && towerEndReload.Dispatch("tower_top").Body!["cleared_floor"]!.GetValue<int>() == 0, "floor reward capacity failure leaves snapshots, progress and pending entry untouched");
        var towerEndSuccess = towerEndReload.Dispatch("tower_battle_end", arguments: new(TowerEnd: towerEndWin)).Body!;
        Check(towerEndSuccess["user_update"]!["tower"]!["cleared_floor"]!.GetValue<int>() == 1 && towerEndSuccess["drop_list"]![0]!["object_value"]!.GetValue<int>() == 10 && towerEndReload.Dispatch("tower_battle_end", arguments: new(TowerEnd: towerEndWin)).Status == 409 && towerEndReload.Dispatch("tower_top").Body!["enemy_battle_info"]!.GetValue<string>() == "", "floor victory grants once and clears enemy damage while preserving unit damage");
        Check(towerEndReload.Dispatch("tower_battle_start", arguments: new(TowerStart: towerStartRule with { MaxFloor = 2 })).Status == 409 && towerEndReload.Dispatch("tower_battle_start", arguments: new(TowerStart: towerStartRule with { MaxFloor = 1 })).Status == 0 && towerEndReload.Dispatch("tower_battle_end", arguments: new(TowerEnd: towerEndWin)).Body!["drop_list"]![0]!["object_value"]!.GetValue<int>() == 2, "only the completed maximum floor receives repetition rewards");
        Console.WriteLine("Passed 5 floor settlement checks.");
        var treasureDrawSeed = JsonNode.Parse(seed)!;
        treasureDrawSeed["_local_treasure"] = new JsonObject
        {
            ["event_id"] = 99,
            ["fd_status"] = 0,
            ["info"] = new JsonObject
            {
                ["gift_id"] = 3,
                ["code"] = "prepared-local-result"
            }
        };
        var treasureDrawDirectory = directory + "-treasure-draw";
        var treasureDrawServer = new OfflineServer(new JsonStateStore(treasureDrawDirectory), treasureDrawSeed.ToJsonString(), "offline://resources/");
        treasureDrawServer.Dispatch("game_init");
        treasureDrawServer.Dispatch("login");
        Check(treasureDrawServer.Dispatch("fudai/fudai_draw", arguments: new(TreasureDraw: new(99, false, [3]))).Status == 409 && treasureDrawServer.Dispatch("fudai/fudai_draw", arguments: new(TreasureDraw: new(99, true, [4]))).Status == 503 && treasureDrawServer.Dispatch("fudai/fudai_info", arguments: new(Value: 99)).Body!["fd_status"]!.GetValue<int>() == 0, "inactive or unknown prepared rewards preserve eligibility");
        var treasureDrawResult = treasureDrawServer.Dispatch("fudai/fudai_draw", arguments: new(TreasureDraw: new(99, true, [3])));
        Check(treasureDrawResult.Status == 0 && treasureDrawResult.Body!["code"]!.GetValue<string>() == "prepared-local-result" && treasureDrawServer.Dispatch("fudai/fudai_info", arguments: new(Value: 99)).Body!["fd_status"]!.GetValue<int>() == 1, "prepared reward claim persists the claimed state");
        var treasureDrawReload = new OfflineServer(new JsonStateStore(treasureDrawDirectory), seed, "offline://resources/");
        treasureDrawReload.Dispatch("game_init");
        treasureDrawReload.Dispatch("login");
        var treasureDrawRetry = treasureDrawReload.Dispatch("fudai/fudai_draw", arguments: new(TreasureDraw: new(99, true, [3])));
        Check(treasureDrawRetry.Status == 0 && treasureDrawRetry.Body!["gift_id"]!.GetValue<int>() == 3 && treasureDrawRetry.Body["code"]!.GetValue<string>() == "prepared-local-result", "reward claim retries return the original receipt after restart");
        Console.WriteLine("Passed 3 prepared reward claim checks.");
        var eventDrawSeed = JsonNode.Parse(seed)!;
        eventDrawSeed["event_info"] = new JsonArray(new JsonObject { ["event_id"] = 7, ["event_point"] = 100, ["event_total_point"] = 100 });
        eventDrawSeed["_local_event_overview"] = new JsonObject
        {
            ["event_point_gacha_list"] = new JsonArray(),
            ["event_special_mission"] = new JsonArray(),
            ["event_total_point_reward"] = new JsonArray()
        };
        eventDrawSeed["_local_event_draw_sequences"] = new JsonObject
        {
            ["11"] = new JsonArray(101, 102),
            ["12"] = new JsonArray(103)
        };
        var eventDrawDirectory = directory + "-event-draw";
        var eventDrawServer = new OfflineServer(new JsonStateStore(eventDrawDirectory), eventDrawSeed.ToJsonString(), "offline://resources/");
        eventDrawServer.Dispatch("game_init");
        eventDrawServer.Dispatch("login");
        var eventDrawPools = new[]
        {
        new LocalEventPool(11, 1, 10, 2, [new(101, 1, false, new(3, 0, 5, 100000)), new(102, 1, true, new(6, 0, 2, 100000))]),
        new LocalEventPool(12, 2, 10, 1, [new(103, 1, true, new(3, 0, 10, 100000))])
    };
        var eventDrawRule = new LocalEventDraw(7, 11, 2, true, eventDrawPools);
        Check(eventDrawServer.Dispatch("event_point_gacha_do", arguments: new(EventDraw: eventDrawRule with { GachaId = 12, Count = 1 })).Status == 409 && eventDrawServer.Dispatch("event_point_gacha_do", arguments: new(EventDraw: eventDrawRule with { Count = 3 })).Status == 409, "locked stages and depleted pool requests reject before spending");
        var eventDrawCappedPools = eventDrawPools.ToArray();
        eventDrawCappedPools[0] = eventDrawPools[0] with
        {
            Drops = [eventDrawPools[0].Drops[0], new(102, 1, true, new(6, 0, 2, 1))]
        };
        Check(eventDrawServer.Dispatch("event_point_gacha_do", arguments: new(EventDraw: eventDrawRule with { Pools = eventDrawCappedPools })).Status == 409, "later reward capacity failure rejects the whole draw batch");
        var eventDrawReply = eventDrawServer.Dispatch("event_point_gacha_do", arguments: new(EventDraw: eventDrawRule));
        Check(eventDrawReply.Status == 0 && eventDrawReply.Body!["drop_list"]!.AsArray().Count == 1 && eventDrawReply.Body["target_drop_list"]!.AsArray().Count == 1 && eventDrawReply.Body["user_update"]!["event_info"]![0]!["event_point"]!.GetValue<int>() == 80 && eventDrawReply.Body["user_update"]!["event_info"]![0]!["event_total_point"]!.GetValue<int>() == 100 && eventDrawReply.Body["user_update"]!["gold"]!.GetValue<int>() == eventDrawSeed["gold"]!.GetValue<int>() + 5, "draw batch consumes once after rollback and returns both reward categories");
        var eventDrawReload = new OfflineServer(new JsonStateStore(eventDrawDirectory), seed, "offline://resources/");
        eventDrawReload.Dispatch("game_init");
        eventDrawReload.Dispatch("login");
        Check(eventDrawReload.Dispatch("event_point_gacha_do", arguments: new(EventDraw: eventDrawRule with { Count = 1 })).Status == 409, "exhausted pool remains exhausted after restart");
        var eventDrawNext = eventDrawReload.Dispatch("event_point_gacha_do", arguments: new(EventDraw: eventDrawRule with { GachaId = 12, Count = 1 }));
        Check(eventDrawNext.Status == 0 && eventDrawNext.Body!["user_update"]!["event_info"]![0]!["event_point"]!.GetValue<int>() == 70 && eventDrawNext.Body["event_point_gacha_list"]![1]!["drop_count_list"]![0]!["drop_count"]!.GetValue<int>() == 1, "completed target rewards unlock the next stage with persistent counts");
        Console.WriteLine("Passed 5 event draw settlement checks.");
        arenaEntrySeed = dailyOverviewSeed.DeepClone();
        arenaEntrySeed["_local_daily_arena"]!["enemy_info"]![0]!["enemy2"] = new JsonObject
        {
            ["enemy_id"] = 0
        };
        arenaEntrySeed["_local_daily_arena"]!["entry_rules"] = new JsonArray(new JsonObject { ["difficulty"] = 2, ["enemy_level"] = 20, ["battle_number"] = 0, ["cost"] = 10 });
        var arenaEntryDirectory = directory + "-arena-entry";
        var arenaEntryServer = new OfflineServer(new JsonStateStore(arenaEntryDirectory), arenaEntrySeed.ToJsonString(), "offline://resources/");
        arenaEntryServer.Dispatch("game_init");
        arenaEntryServer.Dispatch("login");
        arenaEntryRule = new LocalArenaStart(1, 1, 20, 2, 0, [1]);
        Check(arenaEntryServer.Dispatch("arena_battle_start", arguments: new(ArenaStart: arenaEntryRule with { BattleId = 2 })).Status == 409 && arenaEntryServer.Dispatch("arena_battle_start", arguments: new(ArenaStart: arenaEntryRule with { Difficulty = 3 })).Status == 503, "battle mismatch and missing admission rules reject entry");
        var arenaEntryReply = arenaEntryServer.Dispatch("arena_battle_start", arguments: new(ArenaStart: arenaEntryRule));
        var arenaEntryRetry = arenaEntryServer.Dispatch("arena_battle_start", arguments: new(ArenaStart: arenaEntryRule));
        Check(arenaEntryReply.Status == 0 && arenaEntryRetry.Status == 0 && arenaEntryReply.Body!["user_update"]!["phantasm_point"]!["points"]!.GetValue<int>() == 90 && arenaEntryRetry.Body!["user_update"]!["phantasm_point"]!["points"]!.GetValue<int>() == 90, "admission deducts once and retries return the current balance");
        arenaEntryReload = new OfflineServer(new JsonStateStore(arenaEntryDirectory), seed, "offline://resources/");
        arenaEntryReload.Dispatch("game_init");
        var arenaEntrySaved = arenaEntryReload.Dispatch("login").Body!["user"]!;
        Check(arenaEntrySaved["_local_arena_pending"]!["custom_battle_id"]!.GetValue<int>() == 1 && arenaEntrySaved["phantasm_point"]!["points"]!.GetValue<int>() == 90, "pending battle and admission payment survive restart");
        arenaWeeklySeed = weeklyOverviewSeed.DeepClone();
        arenaWeeklySeed["_local_weekly_arena"]!["entry_rules"] = new JsonArray(new JsonObject { ["difficulty"] = 2, ["enemy_level"] = 0, ["battle_number"] = 1, ["cost"] = 0 });
        var arenaWeeklyTeam = arenaWeeklySeed["team"]!["team_list"]![0]!.DeepClone();
        arenaWeeklyTeam["number"] = 60;
        arenaWeeklySeed["team"]!["team_list"]!.AsArray().Add(arenaWeeklyTeam);
        arenaWeeklySeed["_local_weekly_arena"]!["weekly_battle_data"]!["battle_number2"]!["unit_list"] = new JsonArray(arenaWeeklySeed["unit"]!["unit_list"]![0]!["unit_id"]!.GetValue<int>());
        var arenaWeeklyServer = new OfflineServer(new JsonStateStore(directory + "-arena-weekly-entry"), arenaWeeklySeed.ToJsonString(), "offline://resources/");
        arenaWeeklyServer.Dispatch("game_init");
        arenaWeeklyServer.Dispatch("login");
        arenaWeeklyRule = new LocalArenaStart(2, 1, 0, 2, 1, [60]);
        Check(arenaWeeklyServer.Dispatch("arena_battle_start", arguments: new(ArenaStart: arenaWeeklyRule)).Status == 409, "weekly entry prevents reusing a character type from another battle");
        arenaWeeklyServer.Dispatch("weekly_arena_battle_score_reset", arguments: new(Value: 2));
        Check(arenaWeeklyServer.Dispatch("arena_battle_start", arguments: new(ArenaStart: arenaWeeklyRule)).Status == 0, "weekly reset releases the occupied character for admission");
        Console.WriteLine("Passed 5 arena admission checks.");
    }
}
