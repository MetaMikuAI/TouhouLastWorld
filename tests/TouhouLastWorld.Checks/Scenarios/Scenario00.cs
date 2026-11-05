using System.Text.Json.Nodes;
using TouhouLastWorld;

internal sealed partial class RegressionSuite
{
    private void Scenario00()
    {
        directory = Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N"));
        seed = File.ReadAllText(args[0]);
        server = new OfflineServer(new JsonStateStore(directory), seed, "offline://resources/");
        Check(server.Dispatch("login").Status == 401, "login requires game_init");
        Check(server.Dispatch("unknown").Status == 501, "unknown API must not fall through");
        var init = server.Dispatch("game_init").Body!;
        Check(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(init["api_server"]!.GetValue<string>())) == "offline://api/", "encoded API endpoint");
        Check(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(init["resource_server"]!.GetValue<string>())) == "offline://resources/", "encoded resource endpoint");
        var version = server.Dispatch("version_get").Body!;
        Check(version["version"]!.GetValue<int>() == 92, "APK version");
        Check(version["session_id"]!.GetValue<string>() == init["session_id"]!.GetValue<string>(), "session continuity");
        var login = server.Dispatch("login").Body!;
        var created = server.Dispatch("user_create").Body!;
        var deviceId = created["device_id"]!.GetValue<string>();
        Check(deviceId.Length == 32, "local device identifier format");
        Check(login["device_id"]!.GetValue<string>() == deviceId, "login and registration identity");
        Check(server.Dispatch("user_create").Body!["device_id"]!.GetValue<string>() == deviceId, "idempotent registration");
        Check(login["user"]!["unit"]!["unit_list"]!.AsArray().Count == 1, "starter unit");
        login["user"]!["gold"] = 0;
        Check(server.Dispatch("login").Body!["user"]!["gold"]!.GetValue<int>() == 5000, "snapshot isolation");
        var stored = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "user.json")))!;
        stored["gold"] = 123;
        File.WriteAllText(Path.Combine(directory, "user.json"), stored.ToJsonString());
        var restarted = new OfflineServer(new JsonStateStore(directory), seed, "offline://resources/");
        restarted.Dispatch("game_init");
        Check(restarted.Dispatch("user_create").Body!["device_id"]!.GetValue<string>() == deviceId, "identity survives restart");
        Check(restarted.Dispatch("login").Body!["user"]!["gold"]!.GetValue<int>() == 123, "persistent local save");
        Check(server.Dispatch("game_init").Body!["session_id"]!.GetValue<string>() != init["session_id"]!.GetValue<string>(), "new session");
        Check(restarted.Dispatch("login").Body!["user_id"]!.GetValue<string>() == login["user_id"]!.GetValue<string>(), "stable user identity");
        fundedSeed = JsonNode.Parse(seed)!;
        fundedSeed["gold"] = 20000;
        var fresh = new OfflineServer(new JsonStateStore(directory + "-fresh"), fundedSeed.ToJsonString(), "offline://resources/");
        Check(fresh.Dispatch("user_create").Status == 401, "create requires initialization");
        fresh.Dispatch("game_init");
        Check(fresh.Dispatch("check_user").Status == 401, "check requires local account");
        Check(fresh.Dispatch("check_conditional_pack").Status == 401, "pack check requires local account");
        Check(fresh.Dispatch("user_create").Body!["device_id"]!.GetValue<string>() != deviceId, "separate local accounts");
        Check(fresh.Dispatch("check_user").Status == 0, "registered local account passes check");
        Console.WriteLine("Passed 19 startup/registration/session/save checks; no HTTP or Python runtime used.");
        var fixtures = JsonNode.Parse(File.ReadAllText(args[1]))!;
        foreach (var api in new[]
        {
        "game_init",
        "version_get"
    }

        )
            Check(ResponseProtocol.Encode(api, "{\"value\":1}", OfflineServer.InitialKey) == fixtures[api]!.GetValue<string>(), api + " Python protocol compatibility");
        Console.WriteLine("Passed 2 independent Python protocol fixture checks.");
        var manifest = JsonNode.Parse(OfflineServer.VersionManifest(92))!["datas"]!.AsArray();
        Check(manifest.Count == 1 && manifest[0]!["AppVersion"]!.GetValue<int>() == 92, "version manifest has fallback record for installed version");
        Console.WriteLine("Passed version manifest fallback check.");
        Check(fresh.Dispatch("gacha_do", 2).Status == 400, "invalid draw count");
        Check(fresh.Dispatch("battle_end").Status == 400, "missing battle result");
        var admission = new LocalQuestBattleStart(101, 201, 0, 1, true, 5, [1L], [1L]);
        var noviceMission = new RequestDraft(ProgressMissions: [new(400200, 101, "序章探索", 450000, [(1117991, 1)], 10, 201)], MissionClaim: new(400200, 0, 10, 0, 0, [new(400200, 10, 0, 1, true, [new(12, 100001, 1, 999)])]));
        Check(fresh.Dispatch("home", arguments: noviceMission).Body!["put_beginner_mission_id_list"]!.AsArray().Count == 0 && fresh.Dispatch("mission_end", arguments: noviceMission).Status == 404, "novice mission requires a battle victory");
        Check(fresh.Dispatch("battle_start", arguments: new(QuestBattleStart: admission with { Open = false })).Status == 409, "locked quest does not start");
        foreach (var _ in Enumerable.Range(0, 2))
        {
            var started = fresh.Dispatch("battle_start", arguments: new(QuestBattleStart: admission)).Body!;
            Check(started["user_update"]!["phantasm_point"]!["points"]!.GetValue<int>() == 100, "battle entry keeps spirit points until victory");
        }

        var rewards = new LocalBattleRewards(100, 100, 99999, 25, 10, [2, 1, 0], [new(1, 0), new(2, 20), new(3, 40)], [new(1L, [new(1, 0), new(2, 10)])]);
        var settlement = new LocalQuestBattleEnd(201, 1, [], rewards, new(3, 250L, [238]));
        Check(fresh.Dispatch("battle_end", arguments: new(QuestBattleEnd: settlement with { BattleId = 202 })).Status == 409, "settlement must match entry");
        var win = fresh.Dispatch("battle_end", arguments: new(QuestBattleEnd: settlement)).Body!;
        var battleUpdate = win["user_update"]!;
        Check(battleUpdate["gold"]!.GetValue<int>() == 20100 && battleUpdate["exp"]!.GetValue<long>() == 25 && battleUpdate["phantasm_point"]!["points"]!.GetValue<int>() == 95 && battleUpdate["level"]!.GetValue<int>() == 2 && battleUpdate["unit"]!["unit_list"]![0]!["level"]!.GetValue<int>() == 2 && battleUpdate["quest_playdata"]![0]!["normal"]!["status"]!.GetValue<int>() == 3 && win["battle_challenge_result"]!.GetValue<int>() == 3, "win uses master rewards and records progression");
        Check(fresh.Dispatch("battle_end", arguments: new(QuestBattleEnd: settlement)).Status == 409, "duplicate battle settlement");
        fresh.Dispatch("battle_start", arguments: new(QuestBattleStart: admission));
        var loss = fresh.Dispatch("battle_end", arguments: new(QuestBattleEnd: new(201, 0, [1L], null))).Body!;
        Check(loss["drop_list"]!.AsArray().Count == 0 && loss["user_update"]!["quest_playdata"]![0]!["normal"]!["status"]!.GetValue<int>() == 3, "loss preserves best result");
        var draw = fresh.Dispatch("gacha_do", 10).Body!;
        Check(draw["gacha_drop_list"]!.AsArray().Count == 10, "ten draw results");
        Check(draw["user_update"]!["crystal"]!.GetValue<int>() == 0, "draw cost");
        Check(fresh.Dispatch("gacha_do", 1).Status == 400, "insufficient currency");
        var gameRestart = new OfflineServer(new JsonStateStore(directory + "-fresh"), seed, "offline://resources/");
        gameRestart.Dispatch("game_init");
        var saved = gameRestart.Dispatch("login").Body!["user"]!;
        var profileHistory = gameRestart.Dispatch("get_history").Body!;
        Check(profileHistory["unit_get_count"]!.GetValue<int>() == 1 && profileHistory["quest_complete_count"]!.GetValue<int>() == 1, "history counts unique partners and settled wins after relogin");
        var clearRecord = gameRestart.Dispatch("get_clear_history", arguments: new(HistoryQuery: new(1, 201, 0, 0, null))).Body!["best"]!;
        var clearDetail = gameRestart.Dispatch("get_clear_history_teaminfo", arguments: new(HistoryQuery: new(1, 201, 0, 0, clearRecord["player_id"]!.GetValue<string>()), HistoryDefinitions: new([1001], new() { [100101] = 1001 }, []))).Body!;
        Check(clearRecord["action"]!.GetValue<int>() == 3 && clearRecord["damage"]!.GetValue<long>() == 250 && clearDetail["units"]![0]!["unit_info"]!["level"]!.GetValue<int>() == 1 && clearRecord["team_units"]![0]!["total_power"]!.GetValue<int>() == 238, "personal clear history preserves the battle team before awarding experience");
        Check(saved["gold"]!.GetValue<int>() == 5100 && saved["crystal"]!.GetValue<int>() == 0 && saved["phantasm_point"]!["points"]!.GetValue<int>() == 95 && saved["exp"]!.GetValue<long>() == 25 && saved["quest_playdata"]![0]!["normal"]!["status"]!.GetValue<int>() == 3, "gameplay changes survive restart");
        Check(saved["unit"]!["unit_list"]!.AsArray().Select(unit => unit!["user_unit_id"]!.GetValue<long>()).Distinct().Count() == 11, "unique saved unit ids");
        Check(gameRestart.Dispatch("home", arguments: noviceMission).Body!["put_beginner_mission_id_list"]![0]!.GetValue<int>() == 400200, "a saved victory opens the novice mission badge");
        var noviceGrant = gameRestart.Dispatch("mission_end", arguments: noviceMission).Body!["user_update"]!["item"]!["item_list"]!.AsArray();
        Check(noviceGrant.Any(item => item!["item_id"]!.GetValue<int>() == 100001 && item["num"]!.GetValue<int>() == 1) && gameRestart.Dispatch("home", arguments: noviceMission).Body!["put_beginner_mission_id_list"]!.AsArray().Count == 0 && gameRestart.Dispatch("mission_end", arguments: noviceMission).Status == 404, "novice mission grants its ticket once");
        var npcAdmission = admission with
        {
            TeamNumber = 0,
            Participants = [long.MaxValue - 807],
            OwnedParticipants = []
        };
        Check(gameRestart.Dispatch("battle_start", arguments: new(QuestBattleStart: npcAdmission)).Status == 0, "fixed story party does not require a saved player team");
        Console.WriteLine("Passed battle/gacha persistence checks.");
        var dailyServer = new OfflineServer(new JsonStateStore(directory + "-daily"), seed, "offline://resources/");
        dailyServer.Dispatch("game_init");
        dailyServer.Dispatch("user_create");
        var dailyDay = new LocalDailyQuestDay(20261105, [20000]);
        var dailyAdmission = admission with
        {
            EpisodeId = 0,
            Daily = new(20000, 200001, 1, 1)
        };
        var dailyStart = new RequestDraft(QuestBattleStart: dailyAdmission, DailyQuestDay: dailyDay);
        var dailyMission = new LocalDailyQuestMission(300030, "通关1次每日探索", 3, [(1, 20), (2, 2), (3, 2), (4, 5)], dailyDay, 4);
        var dailyMissions = new RequestDraft(DailyQuestMission: dailyMission, DailyQuestDay: dailyDay, MissionClaim: new(300030, 0, 3, 0, 0, [new(300030, 3, 0, 1, true, [new(2, 0, 20, 1000), new(12, 403, 2, 999), new(6, 0, 2, 999), new(104, 0, 5, 999)])]));
        Check(dailyServer.Dispatch("mission_end", arguments: dailyMissions).Status == 404, "daily quest mission requires victory");
        Check(dailyServer.Dispatch("battle_start", arguments: dailyStart).Status == 0, "daily battle admits an eligible team");
        var dailyResult = new RequestDraft(QuestBattleEnd: settlement, DailyQuestDay: dailyDay);
        var dailyWin = dailyServer.Dispatch("battle_end", arguments: dailyResult).Body!["user_update"]!["daily_quest_count_list"]![0]!;
        Check(dailyWin["count"]!.GetValue<int>() == 1 && dailyWin["cleared_info"]![0]!["normal"]!["status"]!.GetValue<int>() == 3 && dailyServer.Dispatch("battle_end", arguments: dailyResult).Status == 409, "daily win consumes one attempt");
        var dailyGrant = dailyServer.Dispatch("mission_end", arguments: dailyMissions).Body!["user_update"]!;
        Check(dailyGrant["phantasm_point"]!["points"]!.GetValue<int>() == 115 && dailyServer.Dispatch("mission_end", arguments: dailyMissions).Status == 404, "daily quest mission grants spirit once");
        dailyServer.Dispatch("mission_top", arguments: new(BattlePassPeriod: new(100, 0)));
        var dailyPass = new LocalBattlePassDaily(100, dailyDay, 4, new DateTime(2026, 11, 5, 14, 0, 0), [new(new(1002, 3, 20, true, [new(6, 0, 5, 999)]), 7, "完成3次战斗胜利", 1, [(2, 5), (0, 0), (0, 0), (0, 0)]), new(new(1003, 1, 20, true, [new(6, 0, 2, 999)]), 8, "通关1次每日探索", 2, [(1, 2), (0, 0), (0, 0), (0, 0)])]);
        var dailyPassClaim = new RequestDraft(DailyQuestDay: dailyDay, BattlePassDaily: dailyPass, BattlePassClaim: new(1003, 0, 100, true, [], new() { [0] = 0, [1] = 100 }));
        var passMissions = dailyServer.Dispatch("battle_pass_top", arguments: dailyPassClaim).Body!["battle_pass_mission"]!;
        Check(passMissions["unlock_list"]!.AsArray().Count == 2 && passMissions["clear_list"]!.AsArray().Count == 1, "completed news pass mission remains visible");
        var passGrant = dailyServer.Dispatch("battle_pass_mission_end", arguments: dailyPassClaim).Body!["user_update"]!;
        Check(passGrant["battle_pass"]!["battle_pass_exp"]!.GetValue<int>() == 20 && passGrant["crystal"]!.GetValue<int>() == 54 && dailyServer.Dispatch("battle_pass_mission_end", arguments: dailyPassClaim).Status == 404, "news pass daily rewards once");
        var battlePassWinClaim = dailyPassClaim with
        {
            BattlePassClaim = dailyPassClaim.BattlePassClaim! with
            {
                Id = 1002
            }
        };
        dailyServer.Dispatch("battle_start", arguments: new(QuestBattleStart: admission));
        dailyServer.Dispatch("battle_end", arguments: dailyResult with { QuestBattleEnd = new(201, 0, [1L], null) });
        Check(dailyServer.Dispatch("battle_pass_top", arguments: battlePassWinClaim).Body!["battle_pass_mission"]!["unlock_list"]![0]!["current_value"]!.GetValue<int>() == 1 && dailyServer.Dispatch("battle_pass_mission_end", arguments: battlePassWinClaim).Status == 404, "only settled victories count");
        for (var victory = 0; victory < 2; victory++)
        {
            Check(dailyServer.Dispatch("battle_start", arguments: new(QuestBattleStart: admission)).Status == 0, "main battle admitted");
            Check(dailyServer.Dispatch("battle_end", arguments: dailyResult).Status == 0, "main victory settled");
        }

        var battlePassWinGrant = dailyServer.Dispatch("battle_pass_mission_end", arguments: battlePassWinClaim).Body!["user_update"]!;
        Check(battlePassWinGrant["battle_pass"]!["battle_pass_exp"]!.GetValue<int>() == 40 && battlePassWinGrant["crystal"]!.GetValue<int>() == 59 && dailyServer.Dispatch("battle_pass_mission_end", arguments: battlePassWinClaim).Status == 404, "three victories award the news pass reward once");
        var anotherStage = dailyAdmission with
        {
            Daily = dailyAdmission.Daily! with
            {
                StageId = 200002
            }
        };
        Check(dailyServer.Dispatch("battle_start", arguments: dailyStart with { QuestBattleStart = anotherStage }).Status == 409, "daily stages share the category attempt limit");
        var nextDay = dailyDay with
        {
            Date = 20261106
        };
        var dailyReset = dailyServer.Dispatch("home", arguments: new(DailyQuestDay: nextDay)).Body!["user_update"]!["daily_quest_count_list"]![0]!;
        Check(dailyReset["count"]!.GetValue<int>() == 0 && dailyReset["cleared_info"]!.AsArray().Count == 1 && dailyServer.Dispatch("battle_start", arguments: dailyStart with { DailyQuestDay = nextDay }).Status == 0, "daily reset restores attempts and preserves cleared stages");
        var nextPassMissions = dailyServer.Dispatch("battle_pass_top", arguments: new(DailyQuestDay: nextDay, BattlePassDaily: dailyPass with { Day = nextDay })).Body!["battle_pass_mission"]!;
        Check(nextPassMissions["received_list"]!.AsArray().Count == 0 && nextPassMissions["unlock_list"]!.AsArray().Count == 2 && nextPassMissions["unlock_list"]![0]!["current_value"]!.GetValue<int>() == 0, "news pass daily mission reopens on the next day");
        Check(version["master_path"]!.GetValue<string>() == "offline://resources/master", "master resource path");
        Check(version["assetbundle_path"]!.GetValue<string>() == "offline://resources/assetbundle/android", "bundle resource path");
        var resourcePaths = new LocalResourcePaths("https://cdn.example/resource", "https://cdn.example/master", "https://cdn.example/assetbundle", 1031, 0);
        var cdnServer = new OfflineServer(new JsonStateStore(directory + "-cdn"), seed, "https://cdn.example/root", resourcePaths);
        var cdnInit = cdnServer.Dispatch("game_init").Body!;
        var cdnVersion = cdnServer.Dispatch("version_get").Body!;
        Check(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cdnInit["api_server"]!.GetValue<string>())) == "offline://api/" && cdnVersion["resource_path"]!.GetValue<string>() == resourcePaths.Resource && cdnVersion["master_path"]!.GetValue<string>() == resourcePaths.Master && cdnVersion["assetbundle_path"]!.GetValue<string>() == resourcePaths.AssetBundle && cdnVersion["version"]!.GetValue<int>() == 1031 && cdnVersion["resource_version"]!.GetValue<int>() == 0, "version response can use separate CDN directories while game API remains local");
        Console.WriteLine("Passed CDN resource routing check.");
        var home = fresh.Dispatch("home").Body!;
        Check(home["unread_talk_id_list"]!.AsArray().Count == 0, "home callback receives talk list");
        Check(home["put_beginner_mission_id_list"]!.AsArray().Count == 0, "home beginner mission list");
        Console.WriteLine("Passed 4 resource/home contract checks.");
        var packs = fresh.Dispatch("check_conditional_pack").Body!;
        Check(packs["conditional_pack"]!.AsArray().Count == 0 && packs["is_ignore_check"]!.GetValue<int>() == 0, "offline pack check has no offers and retains normal check lifecycle");
        Console.WriteLine("Passed 2 conditional-pack checks.");
        var legacy = JsonNode.Parse(seed)!;
        legacy["unit"]!["unit_list"]![0]!["unit_id"] = 100101;
        legacy["unit"]!["unit_list"]![0]!["equip_costume_id"] = 1001;
        legacy["home_unit_info"]!["unit_param"]![0]!["unit_id"] = 100101;
        legacy["home_unit_info"]!["unit_param"]![0]!["costume_id"] = 1001;
        legacy["avatar_filename"] = "";
        legacy["phantasm_box"]!["level"] = 0;
        legacy["gold_box"]!["level"] = 0;
        legacy["gold"] = 321;
        legacy["team"]!["team_list"]![0]!["user_unit_id_list"] = new JsonArray(1L);
        legacy["team"]!["team_list"]![0]!["season_enhancement_user_unit_id_list"] = new JsonArray();
        var migrationDir = directory + "-migration";
        Directory.CreateDirectory(migrationDir);
        File.WriteAllText(Path.Combine(migrationDir, "user.json"), legacy.ToJsonString());
        var migrated = new OfflineServer(new JsonStateStore(migrationDir), seed, "offline://resources/");
        migrated.Dispatch("game_init");
        var migratedUser = migrated.Dispatch("login").Body!["user"]!;
        Check(migratedUser["unit"]!["unit_list"]![0]!["unit_id"]!.GetValue<int>() == 1001 && migratedUser["unit"]!["unit_list"]![0]!["equip_costume_id"]!.GetValue<int>() == 100101, "legacy unit/costume repaired");
        Check(DateTimeOffset.TryParse(migratedUser["unit"]!["unit_list"]![0]!["last_motivation_at"]?.GetValue<string>(), out _) && JsonNode.DeepEquals(migratedUser["unit"]!["unit_list"]![0]!["motivation"], legacy["unit"]!["unit_list"]![0]!["motivation"]), "legacy motivation starts recovering from its saved value");
        Check(migratedUser["gold"]!.GetValue<int>() == 321 && File.Exists(Path.Combine(migrationDir, "user.json.before-seed-fix")), "migration preserves balance and backup");
        Check(migratedUser["phantasm_box"]!["level"]!.GetValue<int>() == 1 && migratedUser["gold_box"]!["level"]!.GetValue<int>() == 1, "facility minimum levels");
        Check(migratedUser["avatar_filename"]!.GetValue<string>() == "Icon1001", "available avatar asset");
        var migratedTeam = migratedUser["team"]!["team_list"]![0]!;
        Check(migratedTeam["user_unit_id_list"]!.AsArray().Select(id => id!.GetValue<long>()).SequenceEqual(new long[] { 1, 0, 0, 0, 0, 0 }) && migratedTeam["season_enhancement_user_unit_id_list"]!.AsArray().Select(id => id!.GetValue<long>()).SequenceEqual(new long[6]), "legacy team slots are padded and occupied slots are preserved");
        var afk = migrated.Dispatch("afk_top").Body!;
        Check(afk["unit_slot_list"]!.AsArray().Count == 0 && afk["picture_slot_list"]!.AsArray().Count == 0 && afk["search_elapsed_time"]!.GetValue<int>() == 0 && afk["search_max_time"]!.GetValue<int>() > 0, "empty AFK summary has finite progress");
        Console.WriteLine("Passed 7 seed migration/AFK checks.");
    }
}
