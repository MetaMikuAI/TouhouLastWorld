using System.Text.Json.Nodes;
using TouhouLastWorld;

var directory = Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N"));
var seed = File.ReadAllText(args[0]);
var server = new OfflineServer(directory, seed, "offline://resources/");
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
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
var restarted = new OfflineServer(directory, seed, "offline://resources/");
restarted.Dispatch("game_init");
Check(restarted.Dispatch("user_create").Body!["device_id"]!.GetValue<string>() == deviceId, "identity survives restart");
Check(restarted.Dispatch("login").Body!["user"]!["gold"]!.GetValue<int>() == 123, "persistent local save");
Check(server.Dispatch("game_init").Body!["session_id"]!.GetValue<string>() != init["session_id"]!.GetValue<string>(), "new session");
Check(restarted.Dispatch("login").Body!["user_id"]!.GetValue<string>() == login["user_id"]!.GetValue<string>(), "stable user identity");
var fundedSeed = JsonNode.Parse(seed)!;
fundedSeed["gold"] = 20000;
var fresh = new OfflineServer(directory + "-fresh", fundedSeed.ToJsonString(), "offline://resources/");
Check(fresh.Dispatch("user_create").Status == 401, "create requires initialization");
fresh.Dispatch("game_init");
Check(fresh.Dispatch("check_user").Status == 401, "check requires local account");
Check(fresh.Dispatch("check_conditional_pack").Status == 401, "pack check requires local account");
Check(fresh.Dispatch("user_create").Body!["device_id"]!.GetValue<string>() != deviceId, "separate local accounts");
Check(fresh.Dispatch("check_user").Status == 0, "registered local account passes check");
Console.WriteLine("Passed 19 startup/registration/session/save checks; no HTTP or Python runtime used.");
var fixtures = JsonNode.Parse(File.ReadAllText(args[1]))!;
foreach (var api in new[] { "game_init", "version_get" })
    Check(ResponseProtocol.Encode(api, "{\"value\":1}", OfflineServer.InitialKey) == fixtures[api]!.GetValue<string>(), api + " Python protocol compatibility");
Console.WriteLine("Passed 2 independent Python protocol fixture checks.");
var manifest = JsonNode.Parse(OfflineServer.VersionManifest(92))!["datas"]!.AsArray();
Check(manifest.Count == 1 && manifest[0]!["AppVersion"]!.GetValue<int>() == 92, "version manifest has fallback record for installed version");
Console.WriteLine("Passed version manifest fallback check.");
Check(fresh.Dispatch("gacha_do", 2).Status == 400, "invalid draw count");
Check(fresh.Dispatch("battle_end").Status == 400, "missing battle result");
Check(fresh.Dispatch("battle_start").Status == 0, "battle start");
var win = fresh.Dispatch("battle_end", battleResult: 1).Body!;
Check(win["user_update"]!["gold"]!.GetValue<int>() == 20100, "win rewards");
var loss = fresh.Dispatch("battle_end", battleResult: 0).Body!;
Check(loss["user_update"]!["gold"]!.GetValue<int>() == 20100, "loss has no reward");
var draw = fresh.Dispatch("gacha_do", 10).Body!;
Check(draw["gacha_drop_list"]!.AsArray().Count == 10, "ten draw results");
Check(draw["user_update"]!["crystal"]!.GetValue<int>() == 0, "draw cost");
Check(fresh.Dispatch("gacha_do", 1).Status == 400, "insufficient currency");
var gameRestart = new OfflineServer(directory + "-fresh", seed, "offline://resources/");
gameRestart.Dispatch("game_init");
var saved = gameRestart.Dispatch("login").Body!["user"]!;
Check(saved["gold"]!.GetValue<int>() == 5100 && saved["crystal"]!.GetValue<int>() == 0, "gameplay changes survive restart");
Check(saved["unit"]!["unit_list"]!.AsArray().Select(unit => unit!["user_unit_id"]!.GetValue<long>()).Distinct().Count() == 11, "unique saved unit ids");
Console.WriteLine("Passed 10 battle/gacha persistence checks.");
Check(version["master_path"]!.GetValue<string>() == "offline://resources/master", "master resource path");
Check(version["assetbundle_path"]!.GetValue<string>() == "offline://resources/assetbundle/android", "bundle resource path");
var home = fresh.Dispatch("home").Body!;
Check(home["unread_talk_id_list"]!.AsArray().Count == 0, "home callback receives talk list");
Check(home["put_beginner_mission_id_list"]!.AsArray().Count == 0, "home beginner mission list");
Console.WriteLine("Passed 4 resource/home contract checks.");
var packs = fresh.Dispatch("check_conditional_pack").Body!;
Check(packs["conditional_pack"]!.AsArray().Count == 0 && packs["is_ignore_check"]!.GetValue<int>() == 0,
    "offline pack check has no offers and retains normal check lifecycle");
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
var migrationDir = directory + "-migration";
Directory.CreateDirectory(migrationDir);
File.WriteAllText(Path.Combine(migrationDir, "user.json"), legacy.ToJsonString());
var migrated = new OfflineServer(migrationDir, seed, "offline://resources/");
migrated.Dispatch("game_init");
var migratedUser = migrated.Dispatch("login").Body!["user"]!;
Check(migratedUser["unit"]!["unit_list"]![0]!["unit_id"]!.GetValue<int>() == 1001
    && migratedUser["unit"]!["unit_list"]![0]!["equip_costume_id"]!.GetValue<int>() == 100101, "legacy unit/costume repaired");
Check(migratedUser["gold"]!.GetValue<int>() == 321 && File.Exists(Path.Combine(migrationDir, "user.json.before-seed-fix")), "migration preserves balance and backup");
Check(migratedUser["phantasm_box"]!["level"]!.GetValue<int>() == 1 && migratedUser["gold_box"]!["level"]!.GetValue<int>() == 1, "facility minimum levels");
Check(migratedUser["avatar_filename"]!.GetValue<string>() == "Icon1001", "available avatar asset");
var afk = migrated.Dispatch("afk_top").Body!;
Check(afk["unit_slot_list"]!.AsArray().Count == 0 && afk["picture_slot_list"]!.AsArray().Count == 0
    && afk["search_elapsed_time"]!.GetValue<int>() == 0 && afk["search_max_time"]!.GetValue<int>() > 0, "empty AFK summary has finite progress");
Console.WriteLine("Passed 5 seed migration/AFK checks.");
var gachaServer = new OfflineServer(directory + "-gacha", fundedSeed.ToJsonString(), "offline://resources/");
gachaServer.Dispatch("game_init");
gachaServer.Dispatch("login");
Check(gachaServer.Dispatch("gacha_do", 0, gachaId: 1000, gachaButtonId: 102).Status == 501, "unsupported pool rejected");
Check(gachaServer.Dispatch("gacha_do", 10, gachaId: 10, gachaButtonId: 102).Status == 400, "button/count mismatch rejected");
var single = gachaServer.Dispatch("gacha_do", 0, gachaId: 10, gachaButtonId: 102).Body!;
Check(single["gacha_drop_list"]!.AsArray().Count == 1 && single["gacha_drop_list"]![0]!["object_type"]!.GetValue<int>() == 9, "native default count resolves to unit drop");
Check(single["user_update"]!["gold"]!.GetValue<int>() == 18500 && single["user_update"]!["crystal"]!.GetValue<int>() == 45, "native button costs applied once");
Console.WriteLine("Passed 4 native gacha request checks.");

var flagSeed = JsonNode.Parse(seed)!;
flagSeed["picture"]!["picture_list"]!.AsArray().Add(new JsonObject
{
    ["user_picture_id"] = 7L, ["picture_id"] = 1001, ["is_protection"] = 0, ["is_new"] = 1
});
var flagDirectory = directory + "-flags";
var flags = new OfflineServer(flagDirectory, flagSeed.ToJsonString(), "offline://resources/");
flags.Dispatch("game_init");
Check(flags.Dispatch("unit_protection", arguments: new(1, 1)).Status == 401, "flag update requires identity");
flags.Dispatch("login");
var unitKey = flagSeed["unit"]!["unit_list"]![0]!["user_unit_id"]!.GetValue<long>();
foreach (var (section, id) in new[] { ("unit", unitKey), ("picture", 7L) })
{
    Check(flags.Dispatch(section + "_protection", arguments: new(id, 2)).Status == 400, "invalid flag rejected");
    Check(flags.Dispatch(section + "_protection", arguments: new(long.MaxValue, 1)).Status == 404, "missing entity rejected");
    Check(flags.Dispatch(section + "_protection", arguments: new(id, 1)).Status == 0, "protection update");
    Check(flags.Dispatch(section + "_new_update", arguments: new(Ids: [id, long.MaxValue])).Status == 404, "mixed batch rejected");
    var unchanged = flags.Dispatch("login").Body!["user"]![section]![section + "_list"]![0]!;
    Check(unchanged["is_new"]!.GetValue<int>() == 1, "mixed batch has no partial write");
    Check(flags.Dispatch(section + "_new_update", arguments: new(Ids: [id, id])).Status == 0, "duplicate IDs are idempotent");
    var reload = new OfflineServer(flagDirectory, flagSeed.ToJsonString(), "offline://resources/");
    reload.Dispatch("game_init");
    var entry = reload.Dispatch("login").Body!["user"]![section]![section + "_list"]![0]!;
    Check(entry["is_protection"]!.GetValue<int>() == 1 && entry["is_new"]!.GetValue<int>() == 0, "flags persist across restart");
}
Check(flags.Dispatch("mail_top").Body!["mail"]!.AsArray().Count == 0, "empty mail list contract");
var friends = flags.Dispatch("friend/list").Body!;
Check(friends["data"]!.AsArray().Count == 0 && friends["request_tips"]!.GetValue<int>() == 0, "empty friend list contract");
Console.WriteLine("Passed 17 entity flag and minimal response checks.");
Check(flags.Dispatch("unit_voice_autoset_update", arguments: new(Value: -1)).Status == 400, "invalid preference rejected");
Check(flags.Dispatch("unit_voice_autoset_update", arguments: new(Value: 1)).Status == 0, "preference enabled");
var preferenceReload = new OfflineServer(flagDirectory, flagSeed.ToJsonString(), "offline://resources/");
preferenceReload.Dispatch("game_init");
Check(preferenceReload.Dispatch("login").Body!["user"]!["unit_voice_autoset"]!.GetValue<int>() == 1, "preference persists");
Check(preferenceReload.Dispatch("unit_voice_autoset_update", arguments: new(Value: 0)).Body!["user_update"]!["unit_voice_autoset"]!.GetValue<int>() == 0, "preference disabled");
foreach (var route in new[] { "friend/apply_list", "friend/recommend" })
    Check(flags.Dispatch(route).Body!["data"]!.AsArray().Count == 0, "empty list contract: " + route);
var catalog = flags.Dispatch("get_shop_list").Body!;
Check(catalog["shop_list"]!.AsArray().Count == 0 && catalog["feature_product_shop_list"]!.AsArray().Count == 0, "empty catalog contract");
Console.WriteLine("Passed 7 preference and list response checks.");

foreach (var (route, field) in new[]
{
    ("get_history", "bronze_trophy_count"), ("achievement_top", "achievement_list"),
    ("get_dispatch_list", "dispatch_list"), ("mission_top", "mission"),
    ("friend_get_candidate_list", "helper_list"),
    ("picture_book/get_user_picture_book_list", "user_completed_info"),
    ("avatar/list", "avatar_list"), ("get_shrine_office_shop_list", "shop_list")
})
{
    var query = flags.Dispatch(route);
    Check(query.Status == 0 && query.Body![field] is not null, "query contract: " + route);
    query.Body![field] = null;
    Check(flags.Dispatch(route).Body![field] is not null, "query response isolation: " + route);
}
Check(flags.Dispatch("mission_top").Body!["mission"]!["clear_list"]!.AsArray().Count == 0, "nested mission lists");
Check(flags.Dispatch("tutorial_update", arguments: new(Value: -1)).Status == 400, "negative progress rejected");
Check(flags.Dispatch("tutorial_update", arguments: new(Value: 10000)).Body!["user_update"]!["progress_value"]!.GetValue<int>() == 10000, "progress advances");
Check(flags.Dispatch("tutorial_update", arguments: new(Value: 1)).Body!["user_update"]!["progress_value"]!.GetValue<int>() == 10000, "progress does not regress");
var progressReload = new OfflineServer(flagDirectory, flagSeed.ToJsonString(), "offline://resources/");
progressReload.Dispatch("game_init");
Check(progressReload.Dispatch("login").Body!["user"]!["progress_value"]!.GetValue<int>() == 10000, "progress persists");
Console.WriteLine("Passed 21 query and progress checks.");
var music = flags.Dispatch("music_list").Body!;
Check(music["collect"]!.AsArray().Count == 0 && music["unlock"]!.AsArray().Count == 0, "empty music lists");
foreach (var route in new[] { "pass_card/show_task", "pass_card/show_shop" })
{
    var pass = flags.Dispatch(route).Body!["user_pass_card"]!;
    Check(pass["is_vip"]!.GetValue<int>() == 0 && pass["tasks"]!.AsArray().Count == 0
        && pass["pass_card_goods"]!.AsArray().Count == 0, "inactive pass contract: " + route);
}
var history = flags.Dispatch("get_clear_history").Body!;
Check(history.ContainsKey("best") && history["best"] is null && history["ranking"]!.AsArray().Count == 0
    && history["justnow"]!.AsArray().Count == 0, "absent history contract");
var contacts = flags.Dispatch("friend_top").Body!;
Check(new[] { "friend_list", "friend_request_list", "friend_approval_pending_list", "new_approve_list" }
    .All(field => contacts[field]!.AsArray().Count == 0), "empty contact overview");
var beforeLogout = flags.Dispatch("login").Body!;
var localId = beforeLogout["user_id"]!.GetValue<string>();
Check(flags.Dispatch("logout", arguments: new(Text: "unknown")).Status == 403
    && flags.Dispatch("check_user").Status == 0, "identity mismatch preserves session");
var logout = flags.Dispatch("logout", arguments: new(Text: localId));
Check(logout.Status == 0 && logout.Body!["session_id"]!.GetValue<string>() == beforeLogout["session_id"]!.GetValue<string>(), "logout replies in current session");
Check(flags.Dispatch("check_user").Status == 401 && flags.Dispatch("login").Status == 401, "logout invalidates session");
flags.Dispatch("game_init");
var afterLogout = flags.Dispatch("login").Body!;
Check(afterLogout["user_id"]!.GetValue<string>() == localId
    && JsonNode.DeepEquals(afterLogout["user"], beforeLogout["user"]), "logout preserves identity and save");
Console.WriteLine("Passed 9 query and session checks.");
Check(flags.Dispatch("set_age", arguments: new(Value: 0)).Status == 400
    && flags.Dispatch("set_age", arguments: new(Value: 4)).Status == 400, "age category bounds");
Check(flags.Dispatch("set_age", arguments: new(Value: 2)).Body!["user_update"]!["age_limit_type"]!.GetValue<int>() == 2,
    "age category update");
Check(flags.Dispatch("friend/search").Status == 2000, "contact search uses missing result status");
var playerSearch = flags.Dispatch("friend_player_search").Body!;
Check(playerSearch["result"]!.GetValue<int>() != 0 && playerSearch["search_result"]!.AsArray().Count == 0,
    "player search does not expose empty success result");
Check(flags.Dispatch("user_info_update", arguments: new(Profile: new(PlayerName: "Local", HeroName: "Second"))).Status == 0,
    "combined name update");
Check(flags.Dispatch("user_info_update", arguments: new(Profile: new(Comment: "Profile"))).Status == 0,
    "partial profile update");
var profileBeforeInvalid = flags.Dispatch("login").Body!["user"]!.DeepClone();
Check(flags.Dispatch("user_info_update", arguments: new(Profile: new(PlayerName: "Changed", HeroName: " "))).Status == 1000,
    "invalid name rejects whole update");
Check(flags.Dispatch("user_info_update", arguments: new(Profile: new(Comment: "bad\ntext"))).Status == 1000,
    "control characters rejected");
Check(JsonNode.DeepEquals(flags.Dispatch("login").Body!["user"], profileBeforeInvalid), "invalid profile has no partial writes");
Check(flags.Dispatch("user_info_update", arguments: new(Profile: new(Comment: ""))).Status == 0, "comment can be cleared");
var profileReload = new OfflineServer(flagDirectory, flagSeed.ToJsonString(), "offline://resources/");
profileReload.Dispatch("game_init");
var persistedProfile = profileReload.Dispatch("login").Body!["user"]!;
Check(persistedProfile["player_name"]!.GetValue<string>() == "Local"
    && persistedProfile["hero_name"]!.GetValue<string>() == "Second"
    && persistedProfile["comment"]!.GetValue<string>() == ""
    && persistedProfile["age_limit_type"]!.GetValue<int>() == 2, "profile and category survive restart");
Console.WriteLine("Passed 11 profile and search checks.");
Check(flags.Dispatch("get_profile_setting").Body!["profile_setting"]!.GetValue<string>() == "", "new layout uses client defaults");
var layoutText = "{\"characterId\":1001,\"comment\":\"Layout\",\"playerInfoPos\":true,\"efudaScale\":{\"x\":1,\"y\":1,\"z\":1}}";
Check(flags.Dispatch("update_profile_setting", arguments: new(Text: layoutText)).Status == 0, "valid layout saved");
foreach (var invalidLayout in new[] { "{", "[]", "{\"characterId\":-1}", "{\"comment\":1}", "{\"playerInfoPos\":1}", "{\"efudaScale\":{\"x\":1}}" })
    Check(flags.Dispatch("update_profile_setting", arguments: new(Text: invalidLayout)).Status == 400, "invalid layout rejected");
var layoutReload = new OfflineServer(flagDirectory, flagSeed.ToJsonString(), "offline://resources/");
layoutReload.Dispatch("game_init");
layoutReload.Dispatch("login");
Check(layoutReload.Dispatch("get_profile_setting").Body!["profile_setting"]!.GetValue<string>() == layoutText,
    "layout survives restart and rejected writes");
Check(flags.Dispatch("update_profile_setting2", arguments: new(Text: "")).Body!["text_value"]!.GetValue<int>() == 0, "layout comment may be empty");
Check(flags.Dispatch("update_profile_setting2", arguments: new(Text: "bad\ntext")).Body!["text_value"]!.GetValue<int>() != 0, "layout comment rejects controls");
Check(flags.Dispatch("update_profile_setting2").Body!["text_value"]!.GetValue<int>() != 0, "layout comment requires input");
Check(flags.Dispatch("get_profile_setting").Body!["profile_setting"]!.GetValue<string>() == layoutText, "text check does not save layout");
Console.WriteLine("Passed 13 layout checks.");
Check(flags.Dispatch("get_auto_replay_info").Status == 400, "measurement query requires target");
Check(flags.Dispatch("update_auto_replay_info", arguments: new(ReplayMeasure: new(0, 1, 0, -1, 1))).Status == 400,
    "negative measurement rejected");
Check(flags.Dispatch("update_auto_replay_info", arguments: new(ReplayMeasure: new(0, 1, 0, 120, 5))).Status == 0,
    "win measurement preserves counts above 100");
Check(flags.Dispatch("get_auto_replay_info", arguments: new(ReplayMeasure: new(0, 1, 1))).Body!["win_rate"]!.GetValue<int>() == 0,
    "measurement difficulty isolation");
var measure = flags.Dispatch("get_auto_replay_info", arguments: new(ReplayMeasure: new(0, 1, 0))).Body!;
Check(measure["win_rate"]!.GetValue<int>() == 120 && measure["action_num"]!.GetValue<int>() == 5, "measurement round trip");
measure["win_rate"] = 0;
Check(flags.Dispatch("get_auto_replay_info", arguments: new(ReplayMeasure: new(0, 1, 0))).Body!["win_rate"]!.GetValue<int>() == 120,
    "measurement response isolation");
const string readReplay = "get_multiple_spirits_weekly_replay_info";
const string writeReplay = "update_multiple_spirits_weekly_replay_info";
Check(flags.Dispatch(readReplay, arguments: new(EntityId: 1)).Body!["replay_info"]!.GetValue<string>() == "", "absent replay is empty");
Check(flags.Dispatch(writeReplay, arguments: new(ReplayContent: new(1, 10, "", null))).Status == 400, "missing replay content rejected");
Check(flags.Dispatch(writeReplay, arguments: new(ReplayContent: new(1, 10, "rules", "opaque-content"))).Status == 0,
    "replay content saved without reinterpretation");
Check(flags.Dispatch(readReplay, arguments: new(EntityId: 2)).Body!["replay_info"]!.GetValue<string>() == "", "replay target isolation");
var replayReload = new OfflineServer(flagDirectory, flagSeed.ToJsonString(), "offline://resources/");
replayReload.Dispatch("game_init");
replayReload.Dispatch("login");
var replayBody = replayReload.Dispatch(readReplay, arguments: new(EntityId: 1)).Body!;
Check(replayBody["replay_info"]!.GetValue<string>() == "opaque-content" && replayBody["level"]!.GetValue<int>() == 10
    && replayBody["add_rules"]!.GetValue<string>() == "rules", "replay persists across restart");
Check(replayReload.Dispatch("get_auto_replay_info", arguments: new(ReplayMeasure: new(0, 1, 0))).Body!["win_rate"]!.GetValue<int>() == 120,
    "measurement persists across restart");
Check(replayReload.Dispatch(writeReplay, arguments: new(ReplayContent: new(1, 10, "", ""))).Status == 0
    && replayReload.Dispatch(readReplay, arguments: new(EntityId: 1)).Body!["replay_info"]!.GetValue<string>() == "", "empty replay clears record");
var clearedReplay = new OfflineServer(flagDirectory, flagSeed.ToJsonString(), "offline://resources/");
clearedReplay.Dispatch("game_init");
clearedReplay.Dispatch("login");
Check(clearedReplay.Dispatch(readReplay, arguments: new(EntityId: 1)).Body!["replay_info"]!.GetValue<string>() == "", "replay deletion persists");
Console.WriteLine("Passed 14 replay checks.");
const string readSeries = "get_series_battle_replay_info";
const string writeSeries = "update_series_battle_replay_info";
Check(flags.Dispatch(writeSeries, arguments: new(10, 0, Text: "invalid")).Status == 400, "series rejects zero area");
Check(flags.Dispatch(readSeries, arguments: new(EntityId: 10, Ids: [6])).Status == 400, "series rejects out of range area");
Check(flags.Dispatch(writeSeries, arguments: new(10, 1, Text: "area-one")).Status == 0
    && flags.Dispatch(writeSeries, arguments: new(10, 5, Text: "area-five")).Status == 0, "series boundary areas saved");
var seriesSubset = flags.Dispatch(readSeries, arguments: new(EntityId: 10, Ids: [5, 5])).Body!;
Check(seriesSubset["replay_info1"]!.GetValue<string>() == "" && seriesSubset["replay_info5"]!.GetValue<string>() == "area-five",
    "series query respects selected areas and duplicate indices");
Check(flags.Dispatch(readSeries, arguments: new(EntityId: 11, Ids: [1])).Body!["replay_info1"]!.GetValue<string>() == "", "series stages are isolated");
var ordinaryTarget = new LocalReplayMeasure(0, 10, 1);
Check(flags.Dispatch("update_replay_info", arguments: new(ReplayMeasure: ordinaryTarget)).Status == 400, "ordinary replay requires content");
Check(flags.Dispatch("update_replay_info", arguments: new(Text: "ordinary", ReplayMeasure: ordinaryTarget)).Status == 0,
    "ordinary replay saved");
Check(flags.Dispatch("get_replay_info", arguments: new(ReplayMeasure: new(1, 10, 1))).Body!["replay_info"]!.GetValue<string>() == "",
    "ordinary replay task types are isolated");
var allReplayReload = new OfflineServer(flagDirectory, flagSeed.ToJsonString(), "offline://resources/");
allReplayReload.Dispatch("game_init");
allReplayReload.Dispatch("login");
Check(allReplayReload.Dispatch(readSeries, arguments: new(EntityId: 10, Ids: [1, 5])).Body!["replay_info1"]!.GetValue<string>() == "area-one",
    "series replay persists");
Check(allReplayReload.Dispatch("get_replay_info", arguments: new(ReplayMeasure: ordinaryTarget)).Body!["replay_info"]!.GetValue<string>() == "ordinary",
    "ordinary replay persists");
allReplayReload.Dispatch(writeSeries, arguments: new(10, 1, Text: ""));
var afterAreaClear = allReplayReload.Dispatch(readSeries, arguments: new(EntityId: 10, Ids: [1, 5])).Body!;
Check(afterAreaClear["replay_info1"]!.GetValue<string>() == "" && afterAreaClear["replay_info5"]!.GetValue<string>() == "area-five",
    "series deletion only clears selected area");
allReplayReload.Dispatch("update_replay_info", arguments: new(Text: "", ReplayMeasure: ordinaryTarget));
Check(allReplayReload.Dispatch("get_replay_info", arguments: new(ReplayMeasure: ordinaryTarget)).Body!["replay_info"]!.GetValue<string>() == "",
    "ordinary replay cleared");
Console.WriteLine("Passed 12 partitioned replay checks.");
var teamSeed = JsonNode.Parse(seed)!;
teamSeed["achievement"]!["recently_received_achievement_id_list"] = new JsonArray(11, 12);
var teamDirectory = directory + "-teams";
var teams = new OfflineServer(teamDirectory, teamSeed.ToJsonString(), "offline://resources/");
teams.Dispatch("game_init");
teams.Dispatch("login");
Check(teams.Dispatch("achievement_new_update", arguments: new(Ids: [11, -1])).Status == 400, "invalid marker batch rejected");
var markers = teams.Dispatch("achievement_new_update", arguments: new(Ids: [11, 11])).Body!["user_update"]!["achievement"]!["recently_received_achievement_id_list"]!.AsArray();
Check(markers.Count == 1 && markers[0]!.GetValue<int>() == 12, "only requested markers cleared");
Check(teams.Dispatch("achievement_new_update", arguments: new(Ids: [11])).Status == 0, "marker clear is idempotent");
Check(teams.Dispatch("team_update", arguments: new(Team: new(1, "Renamed", null, 0, null, 0))).Status == 0, "team name update");
Check(teams.Dispatch("login").Body!["user"]!["team"]!["team_list"]![0]!["user_unit_id_list"]![0]!.GetValue<long>() == 1,
    "name update preserves members");
Check(teams.Dispatch("team_update", arguments: new(Team: new(1, null, [1, 1], 0, null, 0))).Status == 400, "duplicate team members rejected");
Check(teams.Dispatch("team_update", arguments: new(Team: new(1, "Rejected", [999], 0, null, 0))).Status == 404,
    "unowned team member rejected");
Check(teams.Dispatch("login").Body!["user"]!["team"]!["team_list"]![0]!["name"]!.GetValue<string>() == "Renamed",
    "rejected team update preserves name");
Check(teams.Dispatch("team_update", arguments: new(Team: new(1, null, [1, 0], 5, [0, 1], 5))).Status == 400,
    "enhanced member must match slot");
var savedTeam = teams.Dispatch("team_update", arguments: new(Team: new(1, null, [0, 1], 5, [0, 1], 6)))
    .Body!["user_update"]!["team"]!["team_list"]![0]!;
Check(savedTeam["user_unit_id_list"]!.AsArray().Count == 6 && savedTeam["use_cost"]!.GetValue<int>() == 5,
    "team slots normalized and cost stored");
Check(teams.Dispatch("friend_setting_helper", arguments: new(999, 100)).Status == 404, "helper ownership required");
Check(teams.Dispatch("friend_setting_helper", arguments: new(1, 100)).Body!["helper_user_unit_id1"]!.GetValue<long>() == 1,
    "helper selected");
var teamReload = new OfflineServer(teamDirectory, teamSeed.ToJsonString(), "offline://resources/");
teamReload.Dispatch("game_init");
var savedUser = teamReload.Dispatch("login").Body!["user"]!;
Check(savedUser["team"]!["team_list"]![0]!["user_unit_id_list"]![1]!.GetValue<long>() == 1
    && savedUser["achievement"]!["recently_received_achievement_id_list"]!.AsArray().Count == 1,
    "team and markers persist");
Check(teamReload.Dispatch("friend_top").Body!["helper_user_unit_id1"]!.GetValue<long>() == 1, "helper persists and overview reads selection");
Console.WriteLine("Passed 14 team, marker and helper checks.");
Check(teams.Dispatch("home_select_unit_update", arguments: new(Value: 1)).Status == 400
    && teams.Dispatch("home_select_unit_update", arguments: new(Value: -1)).Status == 400, "home selection bounds");
Check(teams.Dispatch("home_select_unit_update", arguments: new(Value: 0)).Body!["user_update"]!["home_unit_info"]!["select_unit_index"]!.GetValue<int>() == 0,
    "home selection saved");
Check(teams.Dispatch("home_bgm_update", arguments: new(Value: -1)).Status == 400, "negative home music rejected");
Check(teams.Dispatch("home_bgm_update", arguments: new(Value: 123)).Status == 0, "home music saved");
Check(teams.Dispatch("unit_bgm_update", arguments: new(999, 123)).Status == 404, "unit music requires owned unit");
Check(teams.Dispatch("unit_bgm_update", arguments: new(1, -1)).Status == 400, "negative unit music rejected");
Check(teams.Dispatch("unit_bgm_update", arguments: new(1, 456)).Status == 0, "unit music saved");
var homeReload = new OfflineServer(teamDirectory, teamSeed.ToJsonString(), "offline://resources/");
homeReload.Dispatch("game_init");
var homeUser = homeReload.Dispatch("login").Body!["user"]!;
Check(homeUser["select_bgm"]!.GetValue<int>() == 123 && homeUser["unit"]!["unit_list"]![0]!["select_bgm"]!.GetValue<int>() == 456
    && homeUser["bgm_item"]!["received_bgm_item_id_list"]!.AsArray().Count == 0, "music preferences persist without granting items");
Check(homeReload.Dispatch("home_bgm_update", arguments: new(Value: 0)).Body!["user_update"]!["select_bgm"]!.GetValue<int>() == 0,
    "home music default restored");
Console.WriteLine("Passed 9 home preference checks.");
var homeEntry = new LocalHomeEntry(1001, 1, 100101, 0, 123);
Check(teams.Dispatch("favorite_unit_update", arguments: new(Home: new([], 0, 0))).Status == 400, "home requires an entry");
Check(teams.Dispatch("favorite_unit_update", arguments: new(Home: new([homeEntry], 1, 0))).Status == 400, "home configuration selection bounds");
Check(teams.Dispatch("favorite_unit_update", arguments: new(Home: new([homeEntry], 0, 2))).Status == 400, "home random mode bounds");
Check(teams.Dispatch("favorite_unit_update", arguments: new(Home: new([homeEntry with { UserUnitId = 999 }], 0, 0))).Status == 404,
    "home configuration ownership");
Check(teams.Dispatch("favorite_unit_update", arguments: new(Home: new([homeEntry with { UnitId = 999 }], 0, 0))).Status == 400,
    "home identifiers must match");
var configuredHome = teams.Dispatch("favorite_unit_update", arguments: new(Home: new([homeEntry], 0, 1)))
    .Body!["user_update"]!["home_unit_info"]!;
Check(configuredHome["random_select_mode"]!.GetValue<int>() == 1 && configuredHome["unit_param"]![0]!["bgm"]!.GetValue<int>() == 123,
    "complete home configuration saved");
var beforeInvalidHome = teams.Dispatch("login").Body!["user"]!["home_unit_info"]!.DeepClone();
Check(teams.Dispatch("favorite_unit_update", arguments: new(Home: new([homeEntry, homeEntry with { UserUnitId = 999 }], 0, 0))).Status == 404
    && JsonNode.DeepEquals(beforeInvalidHome, teams.Dispatch("login").Body!["user"]!["home_unit_info"]), "home batch update is atomic");
Check(teams.Dispatch("update_home_motion_mode", arguments: new(999, 1)).Status == 404
    && teams.Dispatch("update_home_motion_mode", arguments: new(1, -1)).Status == 400, "motion preference validates target and value");
Check(teams.Dispatch("update_home_motion_mode", arguments: new(1, 1)).Status == 0, "motion preference saved");
var configuredHomeReload = new OfflineServer(teamDirectory, teamSeed.ToJsonString(), "offline://resources/");
configuredHomeReload.Dispatch("game_init");
var configuredUser = configuredHomeReload.Dispatch("login").Body!["user"]!;
Check(configuredUser["home_unit_info"]!["random_select_mode"]!.GetValue<int>() == 1
    && configuredUser["unit"]!["unit_list"]![0]!["home_motion"]!.GetValue<int>() == 1, "home configuration and motion persist");
Console.WriteLine("Passed 10 home configuration checks.");
Check(flags.Dispatch("anniversary_info_get").Body!["mail_address"]!.GetValue<string>() == "", "empty anniversary contact");
var personal = flags.Dispatch("personal_info_get").Body!;
Check(new[] { "user_name", "zip_code", "address", "phone_number" }.All(field => personal[field]!.GetValue<string>() == ""),
    "empty personal contact fields");
var ranking = flags.Dispatch("get_series_battle_ranking_list").Body!;
Check(ranking["ranking"]!.AsArray().Count == 0 && ranking["myrank"]!.AsArray().Count == 0, "no fabricated ranking");
var gauge = flags.Dispatch("get_raid_gauge").Body!;
Check(gauge["raid_gauge_list"]!.AsArray().Count == 0, "empty shared progress list");
gauge["raid_gauge_list"]!.AsArray().Add(new JsonObject());
Check(flags.Dispatch("get_raid_gauge").Body!["raid_gauge_list"]!.AsArray().Count == 0, "query templates remain isolated");
Console.WriteLine("Passed 5 contact and progress query checks.");
var mailSeed = JsonNode.Parse(seed)!.AsObject();
mailSeed["_local_mail"] = new JsonArray(
    new JsonObject { ["id"] = 1, ["open_type"] = 0 },
    new JsonObject { ["id"] = 2, ["open_type"] = 1 });
var mailDirectory = directory + "-mail";
var mailbox = new OfflineServer(mailDirectory, mailSeed.ToJsonString(), "offline://resources/");
mailbox.Dispatch("game_init");
mailbox.Dispatch("login");
Check(mailbox.Dispatch("mail_top").Body!["mail"]!.AsArray().Count == 2, "mail overview reads local records");
var remainingMail = mailbox.Dispatch("mail_delete").Body!["mail"]!.AsArray();
Check(remainingMail.Count == 1 && remainingMail[0]!["id"]!.GetValue<int>() == 1, "deletion preserves unread mail");
Check(mailbox.Dispatch("mail_delete").Body!["mail"]!.AsArray().Count == 1, "mail deletion is idempotent");
Check(mailbox.Dispatch("read_unit_talk", arguments: new(Value: -1)).Status == 400, "invalid talk ID rejected");
Check(mailbox.Dispatch("read_unit_talk", arguments: new(Value: 12)).Status == 0
    && mailbox.Dispatch("read_unit_talk", arguments: new(Value: 12)).Status == 0, "talk read is idempotent");
var mailReload = new OfflineServer(mailDirectory, mailSeed.ToJsonString(), "offline://resources/");
mailReload.Dispatch("game_init");
var mailUser = mailReload.Dispatch("login").Body!["user"]!;
Check(mailReload.Dispatch("mail_top").Body!["mail"]!.AsArray().Count == 1, "mail deletion persists");
Check(mailUser["_local_read_talk_ids"]!.AsArray().Count == 1
    && mailUser["_local_read_talk_ids"]![0]!.GetValue<int>() == 12, "read marker persists without duplicates");
Console.WriteLine("Passed 7 mail and talk checks.");
Check(mailbox.Dispatch("unit_voice_update", arguments: new(1001, -1)).Status == 400, "negative voice selection rejected");
Check(mailbox.Dispatch("unit_voice_update", arguments: new(999999, 1)).Status == 404, "unknown collection unit rejected");
var voiceAlbum = mailbox.Dispatch("unit_voice_update", arguments: new(1001, 23)).Body!["user_update"]!["album"]!;
Check(voiceAlbum["unit_list"]![0]!["voice_set_id"]!.GetValue<int>() == 23, "voice selection stored in album");
Check(mailbox.Dispatch("unit_voice_update", arguments: new(1001, 0)).Status == 0, "default voice selection accepted");
var voiceReload = new OfflineServer(mailDirectory, mailSeed.ToJsonString(), "offline://resources/");
voiceReload.Dispatch("game_init");
var voiceUser = voiceReload.Dispatch("login").Body!["user"]!;
Check(voiceUser["album"]!["unit_list"]!.AsArray().Count == 1
    && voiceUser["album"]!["unit_list"]![0]!["voice_set_id"]!.GetValue<int>() == 0, "voice preference persists without duplicate entries");
var beforeTelemetry = voiceUser.DeepClone();
Check(voiceReload.Dispatch("transcribe_kpi", arguments: new(Value: 1)).Status == 0
    && voiceReload.Dispatch("transcribe_kpi", arguments: new(Value: 2)).Status == 0, "known telemetry events accepted");
Check(voiceReload.Dispatch("transcribe_kpi", arguments: new(Value: 3)).Status == 400, "unknown telemetry event rejected");
Check(JsonNode.DeepEquals(beforeTelemetry, voiceReload.Dispatch("login").Body!["user"]), "telemetry does not mutate user state");
Console.WriteLine("Passed 8 voice and telemetry checks.");
var facilitySeed = JsonNode.Parse(seed)!.AsObject();
facilitySeed["unit"]!["unit_list"]![0]!["is_lecture"] = 1;
facilitySeed["lecture_room"]!["unit"]!["unit_list"]!.AsArray().Add(
    new JsonObject { ["user_unit_id"] = 1L, ["put_trust_up_icon"] = 0 });
var facilityDirectory = directory + "-facilities";
var facilities = new OfflineServer(facilityDirectory, facilitySeed.ToJsonString(), "offline://resources/");
facilities.Dispatch("game_init");
facilities.Dispatch("login");
Check(facilities.Dispatch("lecture_room_unit_remove", arguments: new(EntityId: 0)).Status == 400, "invalid facility member rejected");
Check(facilities.Dispatch("lecture_room_unit_remove", arguments: new(EntityId: 999)).Status == 404, "facility removal requires owned unit");
var snapshot = facilities.Dispatch("lecture_room_update").Body!;
Check(snapshot["user_update"]!["lecture_room"]!["unit"]!["unit_list"]!.AsArray().Count == 1
    && snapshot["spend_info"]!["exp"]!.GetValue<int>() == 0, "facility snapshot preserves members without fabricated earnings");
var removed = facilities.Dispatch("lecture_room_unit_remove", arguments: new(EntityId: 1)).Body!["user_update"]!;
Check(removed["lecture_room"]!["unit"]!["unit_list"]!.AsArray().Count == 0
    && removed["unit"]!["unit_list"]![0]!["is_lecture"]!.GetValue<int>() == 0, "facility removal clears membership and flag");
Check(facilities.Dispatch("lecture_room_unit_remove", arguments: new(EntityId: 1)).Status == 0, "facility removal is idempotent");
var facilityReload = new OfflineServer(facilityDirectory, facilitySeed.ToJsonString(), "offline://resources/");
facilityReload.Dispatch("game_init");
var facilityUser = facilityReload.Dispatch("login").Body!["user"]!;
Check(facilityUser["lecture_room"]!["unit"]!["unit_list"]!.AsArray().Count == 0
    && facilityUser["unit"]!["unit_list"]![0]!["is_lecture"]!.GetValue<int>() == 0, "facility removal persists");
Check(JsonNode.DeepEquals(facilitySeed["lecture_room"]!["food"], facilityUser["lecture_room"]!["food"]), "removal preserves food state");
Console.WriteLine("Passed 7 facility checks.");
Check(facilityReload.Dispatch("lecture_room_unit_set", arguments: new(1)).Status == 503, "joining requires resolved capacity");
Check(facilityReload.Dispatch("lecture_room_unit_set", arguments: new(999, 1)).Status == 404, "joining requires ownership");
var joined = facilityReload.Dispatch("lecture_room_unit_set", arguments: new(1, 1)).Body!["user_update"]!;
Check(joined["lecture_room"]!["unit"]!["unit_list"]!.AsArray().Count == 1
    && joined["unit"]!["unit_list"]![0]!["is_lecture"]!.GetValue<int>() == 1, "joining updates membership and flag");
Check(facilityReload.Dispatch("lecture_room_unit_set", arguments: new(1, 1)).Body!["user_update"]!["lecture_room"]!["unit"]!["unit_list"]!.AsArray().Count == 1,
    "repeated joining does not duplicate members");
var joinedReload = new OfflineServer(facilityDirectory, facilitySeed.ToJsonString(), "offline://resources/");
joinedReload.Dispatch("game_init");
Check(joinedReload.Dispatch("login").Body!["user"]!["unit"]!["unit_list"]![0]!["is_lecture"]!.GetValue<int>() == 1,
    "joining persists");
var fullSeed = facilitySeed.DeepClone().AsObject();
var secondUnit = fullSeed["unit"]!["unit_list"]![0]!.DeepClone();
secondUnit["user_unit_id"] = 2L;
secondUnit["is_lecture"] = 0;
fullSeed["unit"]!["unit_list"]!.AsArray().Add(secondUnit);
var fullFacility = new OfflineServer(directory + "-full-facility", fullSeed.ToJsonString(), "offline://resources/");
fullFacility.Dispatch("game_init");
fullFacility.Dispatch("login");
Check(fullFacility.Dispatch("lecture_room_unit_set", arguments: new(2, 1)).Status == 409
    && fullFacility.Dispatch("login").Body!["user"]!["unit"]!["unit_list"]![1]!["is_lecture"]!.GetValue<int>() == 0,
    "full facility rejects without mutation");
Console.WriteLine("Passed 6 facility joining checks.");
var expansionSeed = JsonNode.Parse(seed)!.AsObject();
expansionSeed["orb"]!["free"] = 3;
expansionSeed["orb"]!["purchased"] = 7;
var expansionDirectory = directory + "-expansion";
var expansion = new OfflineServer(expansionDirectory, expansionSeed.ToJsonString(), "offline://resources/");
expansion.Dispatch("game_init");
expansion.Dispatch("login");
Check(expansion.Dispatch("lecture_room_unit_expansion").Status == 503, "missing expansion data rejected");
Check(expansion.Dispatch("lecture_room_unit_expansion", arguments: new(2, 1)).Status == 409, "expansion cannot skip levels");
Check(expansion.Dispatch("lecture_room_unit_expansion", arguments: new(1, 11)).Status == 409
    && expansion.LectureExpansionRank == 0, "insufficient expansion funds preserve level");
var expanded = expansion.Dispatch("lecture_room_unit_expansion", arguments: new(1, 5)).Body!["user_update"]!;
Check(expanded["orb"]!["free"]!.GetValue<long>() == 0 && expanded["orb"]!["purchased"]!.GetValue<long>() == 5
    && expansion.LectureExpansionRank == 1, "expansion debits free balance first and updates level");
Check(expansion.Dispatch("lecture_room_unit_expansion", arguments: new(1, 5)).Status == 409,
    "stale expansion quote rejected");
var expansionReload = new OfflineServer(expansionDirectory, expansionSeed.ToJsonString(), "offline://resources/");
expansionReload.Dispatch("game_init");
Check(expansionReload.Dispatch("login").Body!["user"]!["orb"]!["purchased"]!.GetValue<long>() == 5
    && expansionReload.LectureExpansionRank == 1, "expansion debit and level persist together");
Console.WriteLine("Passed 6 facility expansion checks.");
Check(expansionReload.Dispatch("lecture_room_food_expansion", arguments: new(1, 6)).Status == 409
    && expansionReload.LectureFoodExpansionRank == 0, "food expansion requires funds");
var foodExpanded = expansionReload.Dispatch("lecture_room_food_expansion", arguments: new(1, 2)).Body!["user_update"]!;
Check(expansionReload.LectureFoodExpansionRank == 1 && expansionReload.LectureExpansionRank == 1
    && foodExpanded["orb"]!["purchased"]!.GetValue<long>() == 3, "food expansion has independent level and shared balance");
Check(foodExpanded["lecture_room"]!["food"]!["food_rest_value"]!.GetValue<int>() == 0,
    "food expansion does not grant food");
var foodExpansionReload = new OfflineServer(expansionDirectory, expansionSeed.ToJsonString(), "offline://resources/");
foodExpansionReload.Dispatch("game_init");
Check(foodExpansionReload.Dispatch("login").Body!["user"]!["orb"]!["purchased"]!.GetValue<long>() == 3
    && foodExpansionReload.LectureFoodExpansionRank == 1, "food expansion persists with debit");
Console.WriteLine("Passed 4 food expansion checks.");
Check(foodExpansionReload.Dispatch("training_room_expansion", arguments: new(1, 4)).Status == 409,
    "training expansion requires sufficient funds");
var trainingExpanded = foodExpansionReload.Dispatch("training_room_expansion", arguments: new(1, 2)).Body!["user_update"]!;
Check(trainingExpanded["orb"]!["purchased"]!.GetValue<long>() == 1
    && foodExpansionReload.TrainingExpansionRank == 1 && foodExpansionReload.LectureFoodExpansionRank == 1,
    "training expansion updates its own level and balance");
var trainingSnapshot = foodExpansionReload.Dispatch("training_room_update").Body!["user_update"]!["training_room"]!;
Check(trainingSnapshot["expansion_rank"]!.GetValue<int>() == 1 && trainingSnapshot["unit_list"]!.AsArray().Count == 0,
    "training snapshot reads saved state");
trainingSnapshot["expansion_rank"] = 99;
Check(foodExpansionReload.TrainingExpansionRank == 1, "training snapshot is isolated");
var trainingReload = new OfflineServer(expansionDirectory, expansionSeed.ToJsonString(), "offline://resources/");
trainingReload.Dispatch("game_init");
Check(trainingReload.Dispatch("login").Body!["user"]!["orb"]!["purchased"]!.GetValue<long>() == 1
    && trainingReload.TrainingExpansionRank == 1, "training expansion and payment persist");
Console.WriteLine("Passed 5 training facility checks.");
var cancelSeed = JsonNode.Parse(seed)!.AsObject();
cancelSeed["training_room"]!["unit_list"] = new JsonArray(
    new JsonObject { ["slot_number"] = 1, ["user_unit_id"] = 1L, ["skill_number"] = 1 },
    new JsonObject { ["slot_number"] = 2, ["user_unit_id"] = 2L, ["skill_number"] = 2 });
var cancelDirectory = directory + "-cancel";
var cancel = new OfflineServer(cancelDirectory, cancelSeed.ToJsonString(), "offline://resources/");
cancel.Dispatch("game_init");
cancel.Dispatch("login");
Check(cancel.Dispatch("training_room_cancel", arguments: new(Value: 0)).Status == 400, "zero training slot rejected");
Check(cancel.Dispatch("training_room_cancel", arguments: new(Value: 3)).Status == 404, "missing training task rejected");
var remainingTraining = cancel.Dispatch("training_room_cancel", arguments: new(Value: 1)).Body!["user_update"]!["training_room"]!["unit_list"]!.AsArray();
Check(remainingTraining.Count == 1 && remainingTraining[0]!["slot_number"]!.GetValue<int>() == 2,
    "cancellation removes only requested task");
Check(cancel.Dispatch("training_room_cancel", arguments: new(Value: 1)).Status == 404, "cancelled slot is empty");
var cancelReload = new OfflineServer(cancelDirectory, cancelSeed.ToJsonString(), "offline://resources/");
cancelReload.Dispatch("game_init");
var cancelledUser = cancelReload.Dispatch("login").Body!["user"]!;
Check(cancelledUser["training_room"]!["unit_list"]!.AsArray().Count == 1, "cancellation persists");
Check(JsonNode.DeepEquals(cancelSeed["unit"], cancelledUser["unit"])
    && JsonNode.DeepEquals(cancelSeed["orb"], cancelledUser["orb"]), "cancellation does not grant growth or currency");
Console.WriteLine("Passed 6 training cancellation checks.");
var equipmentSeed = JsonNode.Parse(seed)!.AsObject();
equipmentSeed["unit"]!["unit_list"]![0]!["equip_spellcard1_user_picture_id"] = 11L;
equipmentSeed["picture"]!["picture_list"] = new JsonArray(
    new JsonObject { ["user_picture_id"] = 11L, ["equip_user_unit_id"] = 1L, ["is_protection"] = 1 },
    new JsonObject { ["user_picture_id"] = 12L, ["equip_user_unit_id"] = 2L, ["is_protection"] = 0 });
var equipmentDirectory = directory + "-equipment";
var equipment = new OfflineServer(equipmentDirectory, equipmentSeed.ToJsonString(), "offline://resources/");
equipment.Dispatch("game_init");
equipment.Dispatch("login");
Check(equipment.Dispatch("unit_equip_picture_reset", arguments: new(EntityId: 0)).Status == 400, "equipment reset requires positive ID");
Check(equipment.Dispatch("unit_equip_picture_reset", arguments: new(EntityId: 999)).Status == 404, "equipment reset requires ownership");
var resetEquipment = equipment.Dispatch("unit_equip_picture_reset", arguments: new(EntityId: 1)).Body!["user_update"]!;
Check(Enumerable.Range(1, 5).All(slot => resetEquipment["unit"]!["unit_list"]![0]![$"equip_spellcard{slot}_user_picture_id"]!.GetValue<long>() == 0),
    "all equipment slots cleared");
Check(resetEquipment["picture"]!["picture_list"]![0]!["equip_user_unit_id"]!.GetValue<long>() == 0
    && resetEquipment["picture"]!["picture_list"]![0]!["is_protection"]!.GetValue<int>() == 1
    && resetEquipment["picture"]!["picture_list"]![1]!["equip_user_unit_id"]!.GetValue<long>() == 2,
    "reverse link cleared without changing protection or other owners");
Check(equipment.Dispatch("unit_equip_picture_reset", arguments: new(EntityId: 1)).Status == 0, "equipment reset is idempotent");
var equipmentReload = new OfflineServer(equipmentDirectory, equipmentSeed.ToJsonString(), "offline://resources/");
equipmentReload.Dispatch("game_init");
var equipmentUser = equipmentReload.Dispatch("login").Body!["user"]!;
Check(equipmentUser["unit"]!["unit_list"]![0]!["equip_spellcard1_user_picture_id"]!.GetValue<long>() == 0
    && equipmentUser["picture"]!["picture_list"]![0]!["equip_user_unit_id"]!.GetValue<long>() == 0,
    "both equipment references persist together");
Console.WriteLine("Passed 6 equipment reset checks.");
Check(equipmentReload.Dispatch("unit_equip_picture", arguments: new(1, Equipment: [new(0, 11)])).Status == 400,
    "equipment slot bounds enforced");
Check(equipmentReload.Dispatch("unit_equip_picture", arguments: new(1, Equipment: [new(1, 11), new(1, 0)])).Status == 400,
    "duplicate equipment slot rejected");
var equippedUpdate = equipmentReload.Dispatch("unit_equip_picture", arguments: new(1, Equipment: [new(1, 11)])).Body!["user_update"]!;
Check(equippedUpdate["unit"]!["unit_list"]![0]!["equip_spellcard1_user_picture_id"]!.GetValue<long>() == 11
    && equippedUpdate["picture"]!["picture_list"]![0]!["equip_user_unit_id"]!.GetValue<long>() == 1,
    "equipment assignment updates both references");
Check(equipmentReload.Dispatch("unit_equip_picture", arguments: new(1, Equipment: [new(2, 11)])).Status == 409,
    "same equipment cannot fill two slots");
Check(equipmentReload.Dispatch("unit_equip_picture", arguments: new(1, Equipment: [new(1, 0), new(2, 999)])).Status == 404
    && equipmentReload.Dispatch("login").Body!["user"]!["unit"]!["unit_list"]![0]!["equip_spellcard1_user_picture_id"]!.GetValue<long>() == 11,
    "invalid batch leaves earlier assignments unchanged");
Check(equipmentReload.Dispatch("unit_equip_picture", arguments: new(1, Equipment: [new(2, 12)])).Status == 409,
    "equipment owned by another unit must be released");
Check(equipmentReload.Dispatch("unit_equip_picture", arguments: new(1, Equipment: [new(1, 0), new(2, 11)])).Status == 0,
    "equipment can move between slots in one batch");
var equippedReload = new OfflineServer(equipmentDirectory, equipmentSeed.ToJsonString(), "offline://resources/");
equippedReload.Dispatch("game_init");
var equippedUser = equippedReload.Dispatch("login").Body!["user"]!;
Check(equippedUser["unit"]!["unit_list"]![0]!["equip_spellcard1_user_picture_id"]!.GetValue<long>() == 0
    && equippedUser["unit"]!["unit_list"]![0]!["equip_spellcard2_user_picture_id"]!.GetValue<long>() == 11,
    "equipment slot move persists");
Console.WriteLine("Passed 8 equipment assignment checks.");
var costumeSeed = JsonNode.Parse(seed)!.AsObject();
costumeSeed["costume"]!["costume_id_list"]!.AsArray().Add(100102);
var costumeDirectory = directory + "-costume";
var costumes = new OfflineServer(costumeDirectory, costumeSeed.ToJsonString(), "offline://resources/");
costumes.Dispatch("game_init");
costumes.Dispatch("login");
Check(costumes.Dispatch("unit_equip_costume", arguments: new(Costumes: [new(1, 100102, 999, false)])).Status == 400,
    "costume definition must match unit");
Check(costumes.Dispatch("unit_equip_costume", arguments: new(Costumes: [new(1, 100103, 1001, false)])).Status == 403,
    "unowned costume rejected");
Check(costumes.Dispatch("unit_equip_costume", arguments: new(Costumes: [new(1, 100102, 1001, false), new(999, 100102, 1001, false)])).Status == 404
    && costumes.Dispatch("login").Body!["user"]!["unit"]!["unit_list"]![0]!["equip_costume_id"]!.GetValue<int>() == 100101,
    "costume batch failure preserves earlier assignments");
Check(costumes.Dispatch("unit_equip_costume", arguments: new(Costumes: [new(1, 100102, 1001, false)])).Status == 0,
    "owned costume accepted");
var costumeReload = new OfflineServer(costumeDirectory, costumeSeed.ToJsonString(), "offline://resources/");
costumeReload.Dispatch("game_init");
var costumeUser = costumeReload.Dispatch("login").Body!["user"]!;
Check(costumeUser["unit"]!["unit_list"]![0]!["equip_costume_id"]!.GetValue<int>() == 100102
    && costumeUser["costume"]!["costume_id_list"]!.AsArray().Count == 1, "costume persists without unlocking new entries");
Check(costumeReload.Dispatch("unit_equip_costume", arguments: new(Costumes: [new(1, 100101, 1001, true)])).Status == 0,
    "default costume can be restored");
Console.WriteLine("Passed 6 costume checks.");
var dispatchSeed = JsonNode.Parse(seed)!.AsObject();
dispatchSeed["dispatch"]!["dispatch_list"] = new JsonArray(
    new JsonObject { ["user_dispatch_id"] = 10L, ["dispatch_id"] = 1, ["user_unit_id_list"] = new JsonArray(1L) },
    new JsonObject { ["user_dispatch_id"] = 11L, ["dispatch_id"] = 2, ["user_unit_id_list"] = new JsonArray(2L) });
var dispatchDirectory = directory + "-dispatch";
var dispatches = new OfflineServer(dispatchDirectory, dispatchSeed.ToJsonString(), "offline://resources/");
dispatches.Dispatch("game_init");
dispatches.Dispatch("login");
Check(dispatches.Dispatch("dispatch_cancel", arguments: new(EntityId: 0)).Status == 400, "invalid dispatch ID rejected");
Check(dispatches.Dispatch("dispatch_cancel", arguments: new(EntityId: 99)).Status == 404, "unknown dispatch rejected");
var cancelledDispatch = dispatches.Dispatch("dispatch_cancel", arguments: new(EntityId: 10)).Body!;
Check(cancelledDispatch["remove_user_dispatch_id"]!.GetValue<long>() == 10
    && cancelledDispatch["user_update"]!["dispatch"]!["dispatch_list"]!.AsArray().Count == 1,
    "dispatch cancellation includes native deletion marker");
var dispatchReload = new OfflineServer(dispatchDirectory, dispatchSeed.ToJsonString(), "offline://resources/");
dispatchReload.Dispatch("game_init");
var dispatchUser = dispatchReload.Dispatch("login").Body!["user"]!;
Check(dispatchUser["dispatch"]!["dispatch_list"]![0]!["user_dispatch_id"]!.GetValue<long>() == 11,
    "dispatch cancellation persists and preserves unrelated task");
Check(JsonNode.DeepEquals(dispatchSeed["unit"], dispatchUser["unit"])
    && JsonNode.DeepEquals(dispatchSeed["orb"], dispatchUser["orb"]), "dispatch cancellation does not fabricate rewards");
Console.WriteLine("Passed 5 dispatch cancellation checks.");
var weeklyInfo = dispatchReload.Dispatch("multiple_spirits_weekly_top").Body!;
Check(weeklyInfo["clear_info"]!.AsArray().Count == 0, "weekly progress starts empty");
weeklyInfo["clear_info"]!.AsArray().Add(new JsonObject());
Check(dispatchReload.Dispatch("multiple_spirits_weekly_top").Body!["clear_info"]!.AsArray().Count == 0,
    "weekly response template remains isolated");
Console.WriteLine("Passed 2 weekly query checks.");
var passOverview = dispatchReload.Dispatch("battle_pass_top").Body!;
Check(new[] { "unlock_list", "clear_list", "received_list" }.All(field => passOverview["battle_pass_mission"]![field]!.AsArray().Count == 0),
    "pass mission groups are nonnull empty lists");
Check(new[] { "normal_received_reward_level", "premium_received_reward_level_1", "premium_received_reward_level_2" }
    .All(field => passOverview[field]!.GetValue<int>() == 0), "no pass reward levels fabricated");
Check(dispatchReload.Dispatch("rhythm_game_top").Body!["clear_info"]!.AsArray().Count == 0, "score overview starts empty");
Console.WriteLine("Passed 3 overview query checks.");
var rhythmDirectory = directory + "-rhythm";
var rhythm = new OfflineServer(rhythmDirectory, seed, "offline://resources/");
rhythm.Dispatch("game_init");
rhythm.Dispatch("login");
Check(rhythm.Dispatch("rhythm_game_start", arguments: new(1, 0)).Status == 404, "missing music definition rejected");
Check(rhythm.Dispatch("rhythm_game_end", arguments: new(Rhythm: new(1, 2, 100, 90, 2))).Status == 409,
    "result without start rejected");
Check(rhythm.Dispatch("rhythm_game_start", arguments: new(1, 1)).Status == 0, "known music starts");
Check(rhythm.Dispatch("rhythm_game_end", arguments: new(Rhythm: new(1, 0, 100, 90, 2))).Status == 400
    && rhythm.Dispatch("rhythm_game_end", arguments: new(Rhythm: new(1, 2, 100, float.NaN, 2))).Status == 400,
    "invalid wire difficulty and nonfinite rate rejected");
var rhythmEnded = rhythm.Dispatch("rhythm_game_end", arguments: new(Rhythm: new(1, 2, 100, 90, 2)));
Check(rhythmEnded.Status == 0 && rhythmEnded.Body!["reward_list"]!.AsArray().Count == 0
    && rhythmEnded.Body["remaining_phantasm_point"]!.GetValue<int>() == 0, "result persists without reward allocation");
Check(rhythm.Dispatch("rhythm_game_end", arguments: new(Rhythm: new(1, 2, 200, 95, 3))).Status == 409,
    "duplicate completion rejected");
rhythm.Dispatch("rhythm_game_start", arguments: new(1, 1));
rhythm.Dispatch("rhythm_game_end", arguments: new(Rhythm: new(1, 2, 50, 95, 1)));
rhythm.Dispatch("rhythm_game_start", arguments: new(1, 1));
rhythm.Dispatch("rhythm_game_end", arguments: new(Rhythm: new(1, 3, 20, 50, 1)));
var rhythmReload = new OfflineServer(rhythmDirectory, seed, "offline://resources/");
rhythmReload.Dispatch("game_init");
var rhythmUser = rhythmReload.Dispatch("login").Body!["user"]!;
var savedScores = rhythmReload.Dispatch("rhythm_game_top").Body!["clear_info"]!.AsArray();
Check(savedScores.Count == 2 && savedScores[0]!["highest_score"]!.GetValue<int>() == 100
    && savedScores[0]!["highest_rate"]!.GetValue<float>() == 95 && savedScores[0]!["rank"]!.GetValue<int>() == 2,
    "independent best values persist by difficulty");
Check(JsonNode.DeepEquals(rhythmUser["phantasm_point"], JsonNode.Parse(seed)!["phantasm_point"]),
    "rhythm completion preserves held points");
Console.WriteLine("Passed 8 rhythm checks.");
var achievementSeed = JsonNode.Parse(seed)!.AsObject();
achievementSeed["_local_achievements"] = new JsonArray(
    new JsonObject { ["achievement_id"] = 10, ["current_value"] = 1, ["is_new"] = 0, ["is_acquired"] = 1 },
    new JsonObject { ["achievement_id"] = 20, ["current_value"] = 0, ["is_new"] = 0, ["is_acquired"] = 0 });
var achievementDirectory = directory + "-achievement";
var achievements = new OfflineServer(achievementDirectory, achievementSeed.ToJsonString(), "offline://resources/");
achievements.Dispatch("game_init");
achievements.Dispatch("login");
Check(achievements.Dispatch("achievement_select", arguments: new(Value: 20)).Status == 403
    && achievements.Dispatch("achievement_select", arguments: new(Value: 99)).Status == 403, "unacquired selections rejected");
Check(achievements.Dispatch("achievement_select", arguments: new(Value: 10)).Body!["user_update"]!["achievement"]!["selected_achievement_id"]!.GetValue<int>() == 10,
    "selection returns native user update");
var achievementReload = new OfflineServer(achievementDirectory, achievementSeed.ToJsonString(), "offline://resources/");
achievementReload.Dispatch("game_init");
Check(achievementReload.Dispatch("login").Body!["user"]!["achievement"]!["selected_achievement_id"]!.GetValue<int>() == 10,
    "selection survives reload");
Check(achievementReload.Dispatch("achievement_select", arguments: new(Value: 0)).Status == 0
    && achievementReload.Dispatch("achievement_top").Body!["achievement_list"]!.AsArray().Count == 2, "clearing selection preserves collection");
Check(achievementReload.Dispatch("update_voice_version").Status == 0, "voice notification receives minimal response");
Console.WriteLine("Passed 5 selection and notification checks.");
var deckDirectory = directory + "-decks";
var decks = new OfflineServer(deckDirectory, seed, "offline://resources/");
decks.Dispatch("game_init");
decks.Dispatch("login");
Check(decks.Dispatch("roguelite_deck_save", arguments: new(Value: -1, Ids: [1], Text: "A")).Status == 400
    && decks.Dispatch("roguelite_deck_save", arguments: new(Value: 1, Ids: [0], Text: "A")).Status == 400,
    "invalid preset number and entry rejected");
Check(decks.Dispatch("roguelite_deck_save", arguments: new(Value: 1, Ids: [3, 2, 3], Text: "A")).Status == 0,
    "preset preserves ordered entries and repeated copies");
decks.Dispatch("roguelite_deck_save", arguments: new(Value: 2, Ids: [], Text: "B"));
Check(decks.Dispatch("roguelite_deck_call", arguments: new(Value: 99)).Status == 404, "missing preset rejected");
Check(decks.Dispatch("roguelite_deck_call", arguments: new(Value: 1)).Status == 0, "saved preset activates");
var deckReload = new OfflineServer(deckDirectory, seed, "offline://resources/");
deckReload.Dispatch("game_init");
var savedDecks = deckReload.Dispatch("login").Body!["user"]!["_local_saved_decks"]!;
Check(savedDecks["0"]!["deck_no"]!.GetValue<int>() == 0
    && savedDecks["0"]!["deck_name"]!.GetValue<string>() == "A"
    && JsonNode.DeepEquals(savedDecks["0"]!["deck_list"], new JsonArray(3, 2, 3)), "active preset survives reload");
Check(savedDecks["1"]!["deck_no"]!.GetValue<int>() == 1 && savedDecks["2"]!["deck_list"]!.AsArray().Count == 0,
    "activation preserves source and other presets");
Console.WriteLine("Passed 6 deck checks.");
Check(deckReload.Dispatch("roguelite_top").Status == 503, "overview requires master capacity");
var deckOverview = deckReload.Dispatch("roguelite_top", arguments: new(Value: 3)).Body!;
Check(deckOverview["saved_decks"]!.AsArray().Select(entry => entry!["deck_no"]!.GetValue<int>()).SequenceEqual(new[] { 0, 1, 2, 3 })
    && JsonNode.DeepEquals(deckOverview["saved_decks"]![1]!["deck_list"], new JsonArray(3, 2, 3)),
    "overview fills numbered slots and retains saved preset");
Check(deckOverview["progress"]!.GetValue<int>() == 0 && deckOverview["map_id"]!.GetValue<int>() == 0
    && deckOverview["play_details"]!.GetValue<string>() == "", "overview does not fabricate active progress");
Check(deckReload.Dispatch("roguelite_deck_call", arguments: new(Value: 3)).Status == 0,
    "initialized empty preset can be activated");
Console.WriteLine("Passed 4 deck overview checks.");
var pageSeed = JsonNode.Parse(seed)!.AsObject();
pageSeed["comic_point"] = 100;
var pageDirectory = directory + "-pages";
var pages = new OfflineServer(pageDirectory, pageSeed.ToJsonString(), "offline://resources/");
pages.Dispatch("game_init");
pages.Dispatch("login");
Check(pages.Dispatch("comic_get_pages").Status == 503, "page acquisition needs master definition");
Check(pages.Dispatch("comic_get_pages", arguments: new(Pages: new(1, 11, 10, 5))).Status == 409
    && pages.Dispatch("comic_get_pages", arguments: new(Pages: new(1, 10, 10, 20))).Status == 409,
    "page limit and insufficient balance rejected");
Check(pages.Dispatch("comic_read_end", arguments: new(1, 1)).Status == 404, "unacquired collection cannot be marked read");
var acquiredPages = pages.Dispatch("comic_get_pages", arguments: new(Pages: new(1, 3, 10, 5))).Body!["user_update"]!;
Check(acquiredPages["comic_point"]!.GetValue<int>() == 85
    && acquiredPages["comic_data"]![0]!["received_page_count"]!.GetValue<int>() == 3, "page acquisition deducts matching point cost");
Check(pages.Dispatch("comic_read_end", arguments: new(1, 4)).Status == 403, "unacquired page cannot be marked read");
pages.Dispatch("comic_read_end", arguments: new(1, 3));
pages.Dispatch("comic_read_end", arguments: new(1, 1));
pages.Dispatch("comic_get_pages", arguments: new(Pages: new(1, 2, 10, 5)));
var pageReload = new OfflineServer(pageDirectory, pageSeed.ToJsonString(), "offline://resources/");
pageReload.Dispatch("game_init");
var pageUser = pageReload.Dispatch("login").Body!["user"]!;
Check(pageUser["comic_point"]!.GetValue<int>() == 75 && pageUser["comic_data"]![0]!["received_page_count"]!.GetValue<int>() == 5
    && pageUser["comic_data"]![0]!["rad_page_count"]!.GetValue<int>() == 3, "page balance and monotonic reading progress survive reload");
Check(pageReload.Dispatch("comic_get_pages", arguments: new(Pages: new(1, int.MaxValue, int.MaxValue, int.MaxValue))).Status == 409
    && pageReload.Dispatch("login").Body!["user"]!["comic_point"]!.GetValue<int>() == 75, "overflow-sized request preserves balance");
Console.WriteLine("Passed 7 page checks.");
var seriesSeed = JsonNode.Parse(seed)!.AsObject();
seriesSeed["series_battle_status"]!["episode_id"] = 10;
seriesSeed["series_battle_status"]!["progress"] = 3;
seriesSeed["series_battle_status"]!["use_replays"] = new JsonArray(1, 2);
seriesSeed["_local_series_replays"] = new JsonObject { ["1:1"] = "saved" };
var seriesDirectory = directory + "-series-reset";
var series = new OfflineServer(seriesDirectory, seriesSeed.ToJsonString(), "offline://resources/");
series.Dispatch("game_init");
series.Dispatch("login");
var resetSeries = series.Dispatch("series_battle_reset").Body!["user_update"]!["series_battle_status"]!;
Check(resetSeries["episode_id"]!.GetValue<int>() == 0 && resetSeries["progress"]!.GetValue<int>() == 0
    && resetSeries["use_replays"]!.AsArray().Count == 0, "series reset clears active state");
Check(series.Dispatch("series_battle_reset").Status == 0, "series reset is repeatable");
var seriesReload = new OfflineServer(seriesDirectory, seriesSeed.ToJsonString(), "offline://resources/");
seriesReload.Dispatch("game_init");
var seriesUser = seriesReload.Dispatch("login").Body!["user"]!;
Check(seriesUser["series_battle_status"]!["progress"]!.GetValue<int>() == 0
    && JsonNode.DeepEquals(seriesUser["_local_series_replays"], seriesSeed["_local_series_replays"])
    && JsonNode.DeepEquals(seriesUser["unit"], seriesSeed["unit"])
    && JsonNode.DeepEquals(seriesUser["gold"], seriesSeed["gold"]), "series reset persists without changing collection or rewards");
Console.WriteLine("Passed 3 series reset checks.");
var seriesOverview = seriesReload.Dispatch("series_battle_ranking_top").Body!;
Check(seriesOverview["is_held"]!.GetValue<int>() == 0 && seriesOverview["ranking"]!.GetValue<int>() == 0
    && seriesOverview["score_info"]!.AsArray().Count == 0, "ranking overview remains inactive without scores");
Console.WriteLine("Passed 1 ranking overview check.");
