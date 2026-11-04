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
var seriesTeamDirectory = directory + "-series-teams";
var seriesTeams = new OfflineServer(seriesTeamDirectory, seed, "offline://resources/");
seriesTeams.Dispatch("game_init");
seriesTeams.Dispatch("login");
Check(seriesTeams.Dispatch("series_battle_team_update", arguments: new(SeriesTeams: [new(1, [1])])).Status == 400
    && seriesTeams.Dispatch("series_battle_team_update", arguments: new(SeriesTeams: [new(80, [1, 1])])).Status == 400,
    "series update rejects ordinary slots and duplicate members");
Check(seriesTeams.Dispatch("series_battle_team_update", arguments: new(SeriesTeams: [new(80, [1]), new(81, [999])])).Status == 404
    && seriesTeams.Dispatch("login").Body!["user"]!["team"]!["team_list"]!.AsArray().Count == 1,
    "invalid member rolls back entire series batch");
Check(seriesTeams.Dispatch("series_battle_team_update", arguments: new(SeriesTeams: [new(80, [1]), new(81, [])])).Status == 0,
    "series slots created together");
var seriesTeamReload = new OfflineServer(seriesTeamDirectory, seed, "offline://resources/");
seriesTeamReload.Dispatch("game_init");
var savedSeriesTeams = seriesTeamReload.Dispatch("login").Body!["user"]!["team"]!["team_list"]!.AsArray();
Check(savedSeriesTeams.Count == 3 && savedSeriesTeams[1]!["user_unit_id_list"]!.AsArray().Count == 6
    && savedSeriesTeams[1]!["user_unit_id_list"]![0]!.GetValue<long>() == 1
    && JsonNode.DeepEquals(savedSeriesTeams[0], JsonNode.Parse(seed)!["team"]!["team_list"]![0]),
    "series teams persist and preserve ordinary team");
var activeSeriesTeams = new OfflineServer(directory + "-active-series-teams", seriesSeed.ToJsonString(), "offline://resources/");
activeSeriesTeams.Dispatch("game_init");
activeSeriesTeams.Dispatch("login");
Check(activeSeriesTeams.Dispatch("series_battle_team_update", arguments: new(SeriesTeams: [new(80, [1])])).Status == 409,
    "active series roster cannot be replaced");
Console.WriteLine("Passed 5 series team checks.");
var foodSeed = JsonNode.Parse(seed)!.AsObject();
foodSeed["orb"]!["free"] = 3;
foodSeed["orb"]!["purchased"] = 20;
var foodDirectory = directory + "-food";
var food = new OfflineServer(foodDirectory, foodSeed.ToJsonString(), "offline://resources/");
food.Dispatch("game_init");
food.Dispatch("login");
Check(food.Dispatch("lecture_room_food_addition").Status == 503, "food purchase requires master price");
Check(food.Dispatch("lecture_room_food_addition", arguments: new(FoodPurchase: new(10, 2, 60, 1))).Status == 409
    && food.Dispatch("lecture_room_food_addition", arguments: new(FoodPurchase: new(10, 2, 10, 20))).Status == 409,
    "both food balances checked before spending");
var purchasedFood = food.Dispatch("lecture_room_food_addition", arguments: new(FoodPurchase: new(10, 2, 10, 5))).Body!["user_update"]!;
Check(purchasedFood["phantasm_point"]!["points"]!.GetValue<int>() == 80
    && purchasedFood["orb"]!["free"]!.GetValue<long>() == 0 && purchasedFood["orb"]!["purchased"]!.GetValue<long>() == 13
    && purchasedFood["item"]!["item_list"]![0]!["num"]!.GetValue<int>() == 2, "food costs and inventory update together");
food.Dispatch("lecture_room_food_addition", arguments: new(FoodPurchase: new(10, 1, 10, 5)));
var foodReload = new OfflineServer(foodDirectory, foodSeed.ToJsonString(), "offline://resources/");
foodReload.Dispatch("game_init");
var foodUser = foodReload.Dispatch("login").Body!["user"]!;
Check(foodUser["item"]!["item_list"]![0]!["num"]!.GetValue<int>() == 3
    && foodUser["phantasm_point"]!["points"]!.GetValue<int>() == 70
    && JsonNode.DeepEquals(foodUser["lecture_room"], foodSeed["lecture_room"]), "food inventory persists without automatic distribution");
Check(foodReload.Dispatch("lecture_room_food_addition", arguments: new(FoodPurchase: new(10, int.MaxValue, 0, 0))).Status == 409,
    "inventory overflow rejected");
Console.WriteLine("Passed 5 food purchase checks.");
Check(foodReload.Dispatch("lecture_room_food_distribution", arguments: new(FoodDistribution: new(10, 1, 4, 80, 100, 10))).Status == 409,
    "food distribution rejects insufficient inventory");
var beforeDistribution = DateTimeOffset.UtcNow;
var distributed = foodReload.Dispatch("lecture_room_food_distribution", arguments: new(FoodDistribution: new(10, 1, 2, 80, 100, 10))).Body!["user_update"]!;
var foodState = distributed["lecture_room"]!["food"]!;
var firstFinish = DateTimeOffset.Parse(foodState["food_list"]![0]!["add_effect_finish_time"]!.GetValue<string>(),
    System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal);
Check(foodState["food_rest_value"]!.GetValue<int>() == 100 && distributed["item"]!["item_list"]![0]!["num"]!.GetValue<int>() == 1,
    "distribution caps food value and consumes requested items");
Check((firstFinish - beforeDistribution).TotalMinutes is > 19.9 and < 20.1, "food effect duration uses minutes and quantity");
foodReload.Dispatch("lecture_room_food_distribution", arguments: new(FoodDistribution: new(10, 1, 1, 80, 100, 10)));
var foodAfterReload = new OfflineServer(foodDirectory, foodSeed.ToJsonString(), "offline://resources/");
foodAfterReload.Dispatch("game_init");
var fedUser = foodAfterReload.Dispatch("login").Body!["user"]!;
var effectsAfter = fedUser["lecture_room"]!["food"]!["food_list"]!.AsArray();
var secondFinish = DateTimeOffset.Parse(effectsAfter[0]!["add_effect_finish_time"]!.GetValue<string>(),
    System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal);
Check(effectsAfter.Count == 1 && secondFinish - firstFinish == TimeSpan.FromMinutes(10)
    && fedUser["item"]!["item_list"]![0]!["num"]!.GetValue<int>() == 0, "effect extension and consumed inventory persist");
Check(fedUser["phantasm_point"]!["points"]!.GetValue<int>() == 70
    && JsonNode.DeepEquals(fedUser["unit"], foodSeed["unit"]), "distribution does not fabricate growth or charge purchase cost again");
Console.WriteLine("Passed 5 food distribution checks.");
var trainingSeed = JsonNode.Parse(seed)!.AsObject();
trainingSeed["item"]!["item_list"] = new JsonArray(new JsonObject { ["item_id"] = 12, ["num"] = 2 });
var startTrainingDirectory = directory + "-start-training";
var startTraining = new OfflineServer(startTrainingDirectory, trainingSeed.ToJsonString(), "offline://resources/");
startTraining.Dispatch("game_init");
startTraining.Dispatch("login");
Check(startTraining.Dispatch("training_room_start").Status == 503
    && startTraining.Dispatch("training_room_start", arguments: new(Training: new(1, 1, 12, 3, 2, 600))).Status == 400,
    "training requires definition and available slot number");
Check(startTraining.Dispatch("training_room_start", arguments: new(Training: new(999, 1, 12, 1, 2, 600))).Status == 404
    && startTraining.Dispatch("training_room_start", arguments: new(Training: new(1, 1, 99, 1, 2, 600))).Status == 409,
    "training requires owned unit and material");
var startedTraining = startTraining.Dispatch("training_room_start", arguments: new(Training: new(1, 2, 12, 1, 2, 600))).Body!["user_update"]!;
var trainingTask = startedTraining["training_room"]!["unit_list"]![0]!;
Check(startedTraining["item"]!["item_list"]![0]!["num"]!.GetValue<int>() == 1
    && trainingTask["skill_number"]!.GetValue<int>() == 2
    && DateTime.Parse(trainingTask["training_finish_time"]!.GetValue<string>())
        - DateTime.Parse(trainingTask["training_start_time"]!.GetValue<string>()) == TimeSpan.FromSeconds(600),
    "training consumes one material and stores duration in seconds");
Check(startTraining.Dispatch("training_room_start", arguments: new(Training: new(1, 1, 12, 1, 2, 600))).Status == 409
    && startTraining.Dispatch("training_room_start", arguments: new(Training: new(1, 1, 12, 2, 2, 600))).Status == 409,
    "training rejects duplicate slot and unit without another charge");
var startTrainingReload = new OfflineServer(startTrainingDirectory, trainingSeed.ToJsonString(), "offline://resources/");
startTrainingReload.Dispatch("game_init");
var trainingUser = startTrainingReload.Dispatch("login").Body!["user"]!;
Check(trainingUser["item"]!["item_list"]![0]!["num"]!.GetValue<int>() == 1
    && trainingUser["training_room"]!["unit_list"]!.AsArray().Count == 1
    && JsonNode.DeepEquals(trainingUser["unit"], trainingSeed["unit"]), "training persists without fabricated skill growth");
Console.WriteLine("Passed 5 training start checks.");
Check(startTrainingReload.Dispatch("training_room_shortened_finish_time", arguments: new(EntityId: 1,
    TrainingMaterials: [new(12, 1, 60), new(99, 1, 60)])).Status == 409,
    "training reduction validates every inventory entry before saving");
Check(startTrainingReload.Dispatch("training_room_shortened_finish_time", arguments: new(EntityId: 1,
    TrainingMaterials: [new(12, 1, 60), new(12, 1, 60)])).Status == 400,
    "duplicate training materials rejected");
var shortenedTraining = startTrainingReload.Dispatch("training_room_shortened_finish_time", arguments: new(EntityId: 1,
    TrainingMaterials: [new(12, 1, 60)])).Body!;
Check(DateTime.Parse(trainingTask["training_finish_time"]!.GetValue<string>())
        - DateTime.Parse(shortenedTraining["finish_time"]!.GetValue<string>()) == TimeSpan.FromSeconds(60)
    && shortenedTraining["user_update"]!["item"]!["item_list"]![0]!["num"]!.GetValue<int>() == 0,
    "training reduction returns exact updated deadline and consumes material once");
var reducedTrainingReload = new OfflineServer(startTrainingDirectory, trainingSeed.ToJsonString(), "offline://resources/");
reducedTrainingReload.Dispatch("game_init");
var reducedTrainingUser = reducedTrainingReload.Dispatch("login").Body!["user"]!;
Check(reducedTrainingUser["training_room"]!["unit_list"]![0]!["training_finish_time"]!.GetValue<string>()
        == shortenedTraining["finish_time"]!.GetValue<string>()
    && reducedTrainingUser["item"]!["item_list"]![0]!["num"]!.GetValue<int>() == 0,
    "training deadline and material reduction persist together");
var overTraining = new OfflineServer(directory + "-over-training", trainingSeed.ToJsonString(), "offline://resources/");
overTraining.Dispatch("game_init");
overTraining.Dispatch("login");
overTraining.Dispatch("training_room_start", arguments: new(Training: new(1, 1, 12, 1, 2, 600)));
overTraining.Dispatch("training_room_shortened_finish_time", arguments: new(EntityId: 1, TrainingMaterials: [new(12, 1, int.MaxValue)]));
Check(overTraining.Dispatch("training_room_shortened_finish_time", arguments: new(EntityId: 1,
    TrainingMaterials: [new(12, 1, 60)])).Status == 409, "elapsed training cannot consume more materials");
Console.WriteLine("Passed 5 training reduction checks.");
var avatarSeed = JsonNode.Parse(seed)!.AsObject();
avatarSeed["HeadListItemList"] = new JsonArray(
    new JsonObject { ["avatar_id"] = 1, ["is_preview"] = false, ["is_clear"] = true, ["filename"] = "IconA", ["title"] = "A", ["desc"] = "" },
    new JsonObject { ["avatar_id"] = 2, ["is_preview"] = false, ["is_clear"] = true, ["filename"] = "IconB", ["title"] = "B", ["desc"] = "" },
    new JsonObject { ["avatar_id"] = 3, ["is_preview"] = false, ["is_clear"] = false, ["filename"] = "IconC", ["title"] = "C", ["desc"] = "" });
var avatarDirectory = directory + "-avatars";
var avatars = new OfflineServer(avatarDirectory, avatarSeed.ToJsonString(), "offline://resources/");
avatars.Dispatch("game_init");
avatars.Dispatch("login");
Check(avatars.Dispatch("avatar/preview", arguments: new(Value: 99)).Status == 404, "unknown avatar cannot be marked read");
Check(avatars.Dispatch("avatar/preview", arguments: new(Value: 1)).Body!["avatar_tips"]!.GetValue<int>() == 1
    && avatars.Dispatch("avatar/preview", arguments: new(Value: 2)).Body!["avatar_tips"]!.GetValue<int>() == 0,
    "avatar unread indicator excludes locked entries");
var avatarsReload = new OfflineServer(avatarDirectory, avatarSeed.ToJsonString(), "offline://resources/");
avatarsReload.Dispatch("game_init");
var avatarUser = avatarsReload.Dispatch("login").Body!["user"]!;
Check(avatarsReload.Dispatch("avatar/list").Body!["avatar_list"]![0]!["is_preview"]!.GetValue<bool>()
    && !avatarUser["isShowRedPoint"]!.GetValue<bool>() && avatarUser["avatar_id"]!.GetValue<int>() == 0,
    "avatar read state persists without changing selection");
Console.WriteLine("Passed 3 avatar preview checks.");
Check(avatarsReload.Dispatch("avatar/update", arguments: new(Value: 3)).Status == 403
    && avatarsReload.Dispatch("avatar/update", arguments: new(Value: 99)).Status == 404,
    "avatar selection rejects locked and unknown entries");
var selectedAvatar = avatarsReload.Dispatch("avatar/update", arguments: new(Value: 2)).Body!["user_update"]!;
Check(selectedAvatar["avatar_id"]!.GetValue<int>() == 2 && selectedAvatar["avatar_filename"]!.GetValue<string>() == "IconB",
    "avatar selection returns saved ID and resource");
var selectedAvatarReload = new OfflineServer(avatarDirectory, avatarSeed.ToJsonString(), "offline://resources/");
selectedAvatarReload.Dispatch("game_init");
var selectedAvatarUser = selectedAvatarReload.Dispatch("login").Body!["user"]!;
Check(selectedAvatarUser["avatar_id"]!.GetValue<int>() == 2 && selectedAvatarUser["avatar_title"]!.GetValue<string>() == "B"
    && !selectedAvatarUser["HeadListItemList"]![2]!["is_clear"]!.GetValue<bool>(), "avatar selection persists without unlocking other entries");
Console.WriteLine("Passed 3 avatar selection checks.");
var contactSeed = JsonNode.Parse(seed)!.AsObject();
contactSeed["_local_friend_applications"] = new JsonArray(
    new JsonObject { ["public_id"] = "local-a" }, new JsonObject { ["public_id"] = "local-b" });
var contactDirectory = directory + "-contacts";
var contactServer = new OfflineServer(contactDirectory, contactSeed.ToJsonString(), "offline://resources/");
contactServer.Dispatch("game_init");
contactServer.Dispatch("login");
Check(contactServer.Dispatch("friend/refuse_apply", arguments: new(Value: 0, Text: "missing")).Status == 404
    && contactServer.Dispatch("friend/refuse_apply", arguments: new(Value: 0)).Status == 400,
    "contact refusal validates single target");
contactServer.Dispatch("friend/refuse_apply", arguments: new(Value: 0, Text: "local-a"));
Check(contactServer.Dispatch("friend/apply_list").Body!["data"]!.AsArray().Count == 1
    && contactServer.Dispatch("friend/list").Body!["request_tips"]!.GetValue<int>() == 1,
    "single refusal preserves remaining application and indicator");
var contactsReload = new OfflineServer(contactDirectory, contactSeed.ToJsonString(), "offline://resources/");
contactsReload.Dispatch("game_init");
contactsReload.Dispatch("login");
Check(contactsReload.Dispatch("friend/apply_list").Body!["data"]![0]!["public_id"]!.GetValue<string>() == "local-b",
    "contact refusal persists");
contactsReload.Dispatch("friend/refuse_apply", arguments: new(Value: 1));
Check(contactsReload.Dispatch("friend/apply_list").Body!["data"]!.AsArray().Count == 0
    && contactsReload.Dispatch("friend/list").Body!["request_tips"]!.GetValue<int>() == 0,
    "bulk refusal clears applications and indicator");
Console.WriteLine("Passed 4 contact refusal checks.");
var acceptDirectory = directory + "-accept-contact";
var acceptContact = new OfflineServer(acceptDirectory, contactSeed.ToJsonString(), "offline://resources/");
acceptContact.Dispatch("game_init");
acceptContact.Dispatch("login");
Check(acceptContact.Dispatch("friend/accept_apply", arguments: new(Value: 1, Text: "missing")).Status == 404
    && acceptContact.Dispatch("friend/accept_apply", arguments: new(Text: "local-a")).Status == 503,
    "contact acceptance requires existing application and capacity");
Check(acceptContact.Dispatch("friend/accept_apply", arguments: new(Value: 1, Text: "local-a")).Status == 0
    && acceptContact.Dispatch("friend/accept_apply", arguments: new(Value: 1, Text: "local-b")).Status == 20007,
    "contact capacity enforced without consuming blocked application");
var acceptReload = new OfflineServer(acceptDirectory, contactSeed.ToJsonString(), "offline://resources/");
acceptReload.Dispatch("game_init");
acceptReload.Dispatch("login");
var acceptedFriends = acceptReload.Dispatch("friend/list").Body!["data"]!.AsArray();
Check(acceptedFriends.Count == 1 && acceptedFriends[0]!["public_id"]!.GetValue<string>() == "local-a"
    && acceptReload.Dispatch("friend/apply_list").Body!["data"]![0]!["public_id"]!.GetValue<string>() == "local-b",
    "contact acceptance persists both sides of local transition");
Check(acceptReload.Dispatch("friend/accept_apply", arguments: new(Value: 2, Text: "local-a")).Status == 404,
    "consumed application cannot create duplicate relationship");
Console.WriteLine("Passed 4 contact acceptance checks.");
Check(acceptReload.Dispatch("friend/info", arguments: new(Text: "missing")).Status == 404
    && acceptReload.Dispatch("friend/info").Status == 400, "contact details require known local relation");
Check(acceptReload.Dispatch("friend/info", arguments: new(Text: "local-a")).Body!["unit_get_count"]!.GetValue<int>() == 0,
    "contact details use empty counters when unavailable");
var contactInfoSeed = JsonNode.Parse(seed)!.AsObject();
contactInfoSeed["_local_friends"] = new JsonArray(new JsonObject
{
    ["public_id"] = "local-known", ["unit_get_count"] = 7, ["quest_complete_count"] = 8, ["dispatch_complete_count"] = 9
});
var contactInfoServer = new OfflineServer(directory + "-contact-info", contactInfoSeed.ToJsonString(), "offline://resources/");
contactInfoServer.Dispatch("game_init");
contactInfoServer.Dispatch("login");
var knownContact = contactInfoServer.Dispatch("friend/info", arguments: new(Text: "local-known")).Body!;
Check(knownContact["unit_get_count"]!.GetValue<int>() == 7 && knownContact["quest_complete_count"]!.GetValue<int>() == 8
    && knownContact["dispatch_complete_count"]!.GetValue<int>() == 9, "contact details preserve stored counters");
Console.WriteLine("Passed 3 contact detail checks.");
var pointsSeed = JsonNode.Parse(seed)!.AsObject();
pointsSeed["gold"] = 100;
var pointsState = pointsSeed["unit"]!["unit_list"]![0]!["point_allocation_info"]!;
pointsState["pa_type_1"] = 1;
pointsState["pa_type_9"] = 2;
pointsState["pa_points"] = 4;
pointsState["pa_total_used_points"] = 12;
var pointsDirectory = directory + "-points";
var pointServer = new OfflineServer(pointsDirectory, pointsSeed.ToJsonString(), "offline://resources/");
pointServer.Dispatch("game_init");
pointServer.Dispatch("login");
var resetPoints = pointServer.Dispatch("reset_point_allocation", arguments: new(PointReset: new(1, 200, false))).Body!["user_update"]!;
Check(resetPoints["gold"]!.GetValue<int>() == 100
    && resetPoints["unit"]!["unit_list"]![0]!["point_allocation_info"]!["pa_points"]!.GetValue<int>() == 16,
    "first reset returns used points without charge");
pointServer.Dispatch("reset_point_allocation", arguments: new(PointReset: new(1, 200, false)));
var pointReload = new OfflineServer(pointsDirectory, pointsSeed.ToJsonString(), "offline://resources/");
pointReload.Dispatch("game_init");
var resetUser = pointReload.Dispatch("login").Body!["user"]!;
var resetState = resetUser["unit"]!["unit_list"]![0]!["point_allocation_info"]!;
Check(resetState["pa_points"]!.GetValue<int>() == 16 && resetState["pa_total_used_points"]!.GetValue<int>() == 0
    && resetState["pa_type_9"]!.GetValue<int>() == 0 && resetState["is_pa_reset_not_free"]!.GetValue<int>() == 1
    && resetUser["gold"]!.GetValue<int>() == 100, "reset persists and repeated empty reset does not refund again");
pointsState["is_pa_reset_not_free"] = 1;
var paidPoints = new OfflineServer(directory + "-paid-points", pointsSeed.ToJsonString(), "offline://resources/");
paidPoints.Dispatch("game_init");
paidPoints.Dispatch("login");
Check(paidPoints.Dispatch("reset_point_allocation", arguments: new(PointReset: new(1, 101, false))).Status == 409,
    "paid reset rejects insufficient balance");
var paidReset = paidPoints.Dispatch("reset_point_allocation", arguments: new(PointReset: new(1, 30, false))).Body!["user_update"]!;
Check(paidReset["gold"]!.GetValue<int>() == 70
    && paidReset["unit"]!["unit_list"]![0]!["point_allocation_info"]!["pa_points"]!.GetValue<int>() == 16,
    "paid reset changes points and gold together");
var freePoints = new OfflineServer(directory + "-free-points", pointsSeed.ToJsonString(), "offline://resources/");
freePoints.Dispatch("game_init");
freePoints.Dispatch("login");
Check(freePoints.Dispatch("reset_point_allocation", arguments: new(PointReset: new(1, 200, true))).Body!["user_update"]!["gold"]!.GetValue<int>() == 100,
    "free reset setting overrides repeat cost");
Console.WriteLine("Passed 5 point reset checks.");
var allocationSeed = JsonNode.Parse(seed)!.AsObject();
allocationSeed["unit"]!["unit_list"]![0]!["point_allocation_info"]!["pa_points"] = 20;
LocalPointRule[] allocationRules = [new(1, 1, 3, 0), new(1, 2, 4, 0), new(7, 1, 5, 7)];
var allocationDirectory = directory + "-allocation";
var allocationServer = new OfflineServer(allocationDirectory, allocationSeed.ToJsonString(), "offline://resources/");
allocationServer.Dispatch("game_init");
allocationServer.Dispatch("login");
Check(allocationServer.Dispatch("allocate_points", arguments: new(PointAllocation: new(1, [new(1, 2), new(7, 1)], allocationRules))).Status == 403,
    "allocation prerequisites use pre-request spent points");
var allocated = allocationServer.Dispatch("allocate_points", arguments: new(PointAllocation: new(1, [new(1, 2)], allocationRules))).Body!["user_update"]!["unit"]!["unit_list"]![0]!["point_allocation_info"]!;
Check(allocated["pa_type_1"]!.GetValue<int>() == 2 && allocated["pa_points"]!.GetValue<int>() == 13
    && allocated["pa_total_used_points"]!.GetValue<int>() == 7, "allocation sums every intermediate cost without partial failed changes");
Check(allocationServer.Dispatch("allocate_points", arguments: new(PointAllocation: new(1, [new(1, 1)], allocationRules))).Status == 409
    && allocationServer.Dispatch("allocate_points", arguments: new(PointAllocation: new(1, [new(1, 3)], allocationRules))).Status == 503,
    "allocation rejects decreases and missing rules");
Check(allocationServer.Dispatch("allocate_points", arguments: new(PointAllocation: new(1, [new(7, 1), new(7, 1)], allocationRules))).Status == 400,
    "allocation rejects duplicate types");
allocationServer.Dispatch("allocate_points", arguments: new(PointAllocation: new(1, [new(7, 1)], allocationRules)));
allocationServer.Dispatch("allocate_points", arguments: new(PointAllocation: new(1, [new(7, 1)], allocationRules)));
var allocationReload = new OfflineServer(allocationDirectory, allocationSeed.ToJsonString(), "offline://resources/");
allocationReload.Dispatch("game_init");
var allocationUser = allocationReload.Dispatch("login").Body!["user"]!;
var savedAllocation = allocationUser["unit"]!["unit_list"]![0]!["point_allocation_info"]!;
Check(savedAllocation["pa_points"]!.GetValue<int>() == 8 && savedAllocation["pa_total_used_points"]!.GetValue<int>() == 12
    && savedAllocation["pa_type_7"]!.GetValue<int>() == 1, "allocation persists and target replay does not charge twice");
var resetAllocation = allocationReload.Dispatch("reset_point_allocation", arguments: new(PointReset: new(1, 100, false))).Body!["user_update"]!["unit"]!["unit_list"]![0]!["point_allocation_info"]!;
Check(resetAllocation["pa_points"]!.GetValue<int>() == 20 && resetAllocation["pa_total_used_points"]!.GetValue<int>() == 0,
    "allocation and reset preserve total point balance");
Console.WriteLine("Passed 6 point allocation checks.");
var deleteContactSeed = JsonNode.Parse(seed)!.AsObject();
deleteContactSeed["_local_friend_players"] = new JsonArray(
    new JsonObject { ["player_id"] = "old-a" }, new JsonObject { ["player_id"] = "old-b" });
deleteContactSeed["_local_friends"] = new JsonArray(new JsonObject { ["public_id"] = "old-a" });
var deleteContactDirectory = directory + "-delete-contact";
var deleteContact = new OfflineServer(deleteContactDirectory, deleteContactSeed.ToJsonString(), "offline://resources/");
deleteContact.Dispatch("game_init");
deleteContact.Dispatch("login");
Check(deleteContact.Dispatch("friend_delete").Status == 400
    && deleteContact.Dispatch("friend_delete", arguments: new(Text: "missing")).Body!["result"]!.GetValue<int>() == 1,
    "contact deletion validates ID and reports missing target in result");
var deletedContact = deleteContact.Dispatch("friend_delete", arguments: new(Text: "old-a")).Body!;
Check(deletedContact["result"]!.GetValue<int>() == 0 && deletedContact["friend_list"]!.AsArray().Count == 1
    && deletedContact["friend_list"]![0]!["player_id"]!.GetValue<string>() == "old-b", "contact deletion returns remaining old-schema entries");
var deleteContactReload = new OfflineServer(deleteContactDirectory, deleteContactSeed.ToJsonString(), "offline://resources/");
deleteContactReload.Dispatch("game_init");
deleteContactReload.Dispatch("login");
Check(deleteContactReload.Dispatch("friend_top").Body!["friend_list"]!.AsArray().Count == 1
    && deleteContactReload.Dispatch("friend_delete", arguments: new(Text: "old-a")).Body!["result"]!.GetValue<int>() == 1,
    "deleted contact remains absent after reload");
Check(deleteContactReload.Dispatch("friend/list").Body!["data"]!.AsArray().Count == 1,
    "old-schema deletion does not guess public ID equivalence");
Console.WriteLine("Passed 4 contact deletion checks.");
var cancelContactSeed = JsonNode.Parse(seed)!.AsObject();
cancelContactSeed["_local_friend_approval_pending"] = new JsonArray(new JsonObject { ["player_id"] = "incoming" });
cancelContactSeed["_local_friend_requests"] = new JsonArray(new JsonObject { ["player_id"] = "outgoing" });
cancelContactSeed["_local_friend_players"] = new JsonArray(new JsonObject { ["player_id"] = "friend" });
var cancelContactDirectory = directory + "-cancel-contact";
var cancelContact = new OfflineServer(cancelContactDirectory, cancelContactSeed.ToJsonString(), "offline://resources/");
cancelContact.Dispatch("game_init");
cancelContact.Dispatch("login");
Check(cancelContact.Dispatch("friend_request_cancel", arguments: new(Value: 2)).Status == 400
    && cancelContact.Dispatch("friend_request_cancel", arguments: new(Value: 0, Text: "missing")).Body!["result"]!.GetValue<int>() == 1,
    "request cancellation validates mode and reports missing targets");
cancelContact.Dispatch("friend_request_cancel", arguments: new(Value: 1));
var afterRejectAll = cancelContact.Dispatch("friend_top").Body!;
Check(afterRejectAll["friend_approval_pending_list"]!.AsArray().Count == 0
    && afterRejectAll["friend_request_list"]!.AsArray().Count == 1
    && afterRejectAll["friend_list"]!.AsArray().Count == 1, "reject all preserves outgoing requests and existing contacts");
Check(cancelContact.Dispatch("friend_request_cancel", arguments: new(Value: 0, Text: "outgoing")).Body!["result"]!.GetValue<int>() == 0,
    "single outgoing request can be cancelled");
var cancelContactReload = new OfflineServer(cancelContactDirectory, cancelContactSeed.ToJsonString(), "offline://resources/");
cancelContactReload.Dispatch("game_init");
cancelContactReload.Dispatch("login");
Check(cancelContactReload.Dispatch("friend_top").Body!["friend_request_list"]!.AsArray().Count == 0,
    "request cancellation persists after reload");
Console.WriteLine("Passed 4 contact request cancellation checks.");
var approveContactDirectory = directory + "-approve-contact";
var approveContact = new OfflineServer(approveContactDirectory, cancelContactSeed.ToJsonString(), "offline://resources/");
approveContact.Dispatch("game_init");
approveContact.Dispatch("login");
Check(approveContact.Dispatch("friend_approve", arguments: new(Value: 1, Text: "incoming")).Body!["result"]!.GetValue<int>() == 1
    && approveContact.Dispatch("friend_top").Body!["friend_approval_pending_list"]!.AsArray().Count == 1,
    "full contact list preserves pending application");
var approvedContact = approveContact.Dispatch("friend_approve", arguments: new(Value: 2, Text: "incoming")).Body!;
Check(approvedContact["result"]!.GetValue<int>() == 0 && approvedContact["friend_list"]!.AsArray().Count == 2,
    "approval adds the incoming player to old-schema contacts");
Check(approveContact.Dispatch("friend_approve", arguments: new(Value: 3, Text: "incoming")).Body!["result"]!.GetValue<int>() == 1,
    "approval cannot duplicate an accepted application");
var approveContactReload = new OfflineServer(approveContactDirectory, cancelContactSeed.ToJsonString(), "offline://resources/");
approveContactReload.Dispatch("game_init");
approveContactReload.Dispatch("login");
var approvedOverview = approveContactReload.Dispatch("friend_top").Body!;
Check(approvedOverview["friend_approval_pending_list"]!.AsArray().Count == 0
    && approvedOverview["friend_list"]!.AsArray().Count == 2
    && approvedOverview["friend_request_list"]!.AsArray().Count == 1,
    "approval persists incoming removal and preserves outgoing requests");
Console.WriteLine("Passed 4 contact approval checks.");
var weeklyResetSeed = JsonNode.Parse(seed)!.AsObject();
weeklyResetSeed["_local_weekly_arena"] = new JsonObject
{
    ["total_battle_score"] = 30,
    ["weekly_battle_data"] = new JsonObject
    {
        ["battle_number1"] = new JsonObject
        {
            ["total_score"] = 10, ["difficulty"] = 3, ["attack_count_score"] = 5,
            ["unit_list"] = new JsonArray(101), ["user_unit_list"] = new JsonArray(1L)
        },
        ["battle_number2"] = new JsonObject { ["total_score"] = 20 }
    }
};
var weeklyResetDirectory = directory + "-weekly-reset";
var weeklyReset = new OfflineServer(weeklyResetDirectory, weeklyResetSeed.ToJsonString(), "offline://resources/");
weeklyReset.Dispatch("game_init");
weeklyReset.Dispatch("login");
Check(weeklyReset.Dispatch("weekly_arena_battle_score_reset", arguments: new(Value: 0)).Status == 400
    && weeklyReset.Dispatch("weekly_arena_battle_score_reset", arguments: new(Value: 4)).Status == 404,
    "weekly reset rejects invalid and absent battle numbers");
Check(weeklyReset.Dispatch("weekly_arena_battle_score_reset", arguments: new(Value: 1)).Status == 0,
    "weekly score reset accepts a stored battle");
var weeklyResetReload = new OfflineServer(weeklyResetDirectory, weeklyResetSeed.ToJsonString(), "offline://resources/");
weeklyResetReload.Dispatch("game_init");
var resetWeekly = weeklyResetReload.Dispatch("login").Body!["user"]!["_local_weekly_arena"]!;
var resetWeeklyBattle = resetWeekly["weekly_battle_data"]!["battle_number1"]!;
Check(resetWeekly["total_battle_score"]!.GetValue<int>() == 30 && resetWeeklyBattle["total_score"]!.GetValue<int>() == 0
    && resetWeeklyBattle["unit_list"]!.AsArray().Count == 0 && resetWeeklyBattle["user_unit_list"]!.AsArray().Count == 0,
    "weekly reset persists cleared score and roster while preserving historical high score");
Check(resetWeeklyBattle["difficulty"]!.GetValue<int>() == 3 && resetWeeklyBattle["attack_count_score"]!.GetValue<int>() == 5,
    "weekly reset preserves difficulty and component scores");
Console.WriteLine("Passed 4 weekly score reset checks.");
Check(weeklyResetReload.Dispatch("weekly_arena_top").Status == 503,
    "weekly overview rejects an incomplete local configuration");
var weeklyOverviewSeed = weeklyResetSeed.DeepClone().AsObject();
var weeklyOverviewData = weeklyOverviewSeed["_local_weekly_arena"]!.AsObject();
weeklyOverviewData["custom_battle_id"] = 1;
for (var number = 1; number <= 4; number++)
{
    weeklyOverviewData["weekly_battle_data"]![$"battle_number{number}"] = new JsonObject
    {
        ["total_score"] = number * 10, ["difficulty"] = 2,
        ["buff_role_type"] = new JsonArray(), ["wave_enemy_info"] = new JsonArray(),
        ["unit_list"] = new JsonArray(), ["user_unit_list"] = new JsonArray()
    };
}
var weeklyOverview = new OfflineServer(directory + "-weekly-overview", weeklyOverviewSeed.ToJsonString(), "offline://resources/");
weeklyOverview.Dispatch("game_init");
weeklyOverview.Dispatch("login");
var weeklySnapshot = weeklyOverview.Dispatch("weekly_arena_top").Body!;
Check(weeklySnapshot["weekly_battle_data"]!.AsObject().Count == 4
    && weeklySnapshot["custom_battle_id"]!.GetValue<int>() == 1, "weekly overview reads all stored battle configurations");
weeklySnapshot["weekly_battle_data"]!["battle_number2"]!["total_score"] = 0;
Check(weeklyOverview.Dispatch("weekly_arena_top").Body!["weekly_battle_data"]!["battle_number2"]!["total_score"]!.GetValue<int>() == 20,
    "weekly overview returns an isolated snapshot");
weeklyOverview.Dispatch("weekly_arena_battle_score_reset", arguments: new(Value: 2));
var weeklyAfterReset = weeklyOverview.Dispatch("weekly_arena_top").Body!;
Check(weeklyAfterReset["weekly_battle_data"]!["battle_number2"]!["total_score"]!.GetValue<int>() == 0
    && weeklyAfterReset["total_battle_score"]!.GetValue<int>() == 30, "weekly overview reflects reset without lowering historical high score");
Console.WriteLine("Passed 4 weekly overview checks.");
Check(weeklyOverview.Dispatch("daily_arena_top").Status == 503, "daily overview requires stored configuration");
var dailyOverviewSeed = JsonNode.Parse(seed)!.AsObject();
dailyOverviewSeed["_local_daily_arena"] = new JsonObject
{
    ["custom_battle_id"] = 1, ["clear_diffculty"] = 2, ["reward_day_count"] = 1,
    ["enemy_level"] = 20, ["last_clear_difficulty"] = 2,
    ["enemy_info"] = new JsonArray(new JsonObject
    {
        ["enemy1"] = new JsonObject { ["enemy_id"] = 101, ["special_action_gauge_limit"] = 3 },
        ["enemy2"] = new JsonObject { ["enemy_id"] = 0 },
        ["enemy3"] = new JsonObject { ["enemy_id"] = 0 }
    })
};
var dailyOverview = new OfflineServer(directory + "-daily-overview", dailyOverviewSeed.ToJsonString(), "offline://resources/");
dailyOverview.Dispatch("game_init");
dailyOverview.Dispatch("login");
var dailySnapshot = dailyOverview.Dispatch("daily_arena_top").Body!;
Check(dailySnapshot["clear_diffculty"]!.GetValue<int>() == 2 && dailySnapshot["reward_day_count"]!.GetValue<int>() == 1
    && dailySnapshot["enemy_info"]![0]!["enemy1"]!["enemy_id"]!.GetValue<int>() == 101,
    "daily overview preserves stored progress and enemy configuration");
dailySnapshot["enemy_info"]![0]!["enemy1"]!["enemy_id"] = 0;
Check(dailyOverview.Dispatch("daily_arena_top").Body!["enemy_info"]![0]!["enemy1"]!["enemy_id"]!.GetValue<int>() == 101,
    "daily overview returns an isolated snapshot");
dailyOverviewSeed["_local_daily_arena"]!["enemy_info"]![0]!["enemy2"] = null;
var invalidDailyOverview = new OfflineServer(directory + "-daily-incomplete", dailyOverviewSeed.ToJsonString(), "offline://resources/");
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
    ["event_total_point_reward"] = new JsonArray(new JsonObject
    {
        ["event_id"] = 1, ["next_reward_group_id"] = 2, ["total_point"] = 100, ["step_number"] = 1
    })
};
var eventOverview = new OfflineServer(directory + "-event-overview", eventOverviewSeed.ToJsonString(), "offline://resources/");
eventOverview.Dispatch("game_init");
eventOverview.Dispatch("login");
var eventSnapshot = eventOverview.Dispatch("event_top").Body!;
Check(eventSnapshot["event_total_point_reward"]![0]!["total_point"]!.GetValue<int>() == 100
    && eventSnapshot["event_special_mission"]!.AsArray().Count == 0 && eventSnapshot["user_update"] is null,
    "event overview reads saved progress without issuing user rewards");
eventSnapshot["event_total_point_reward"]![0]!["total_point"] = 0;
Check(eventOverview.Dispatch("event_top").Body!["event_total_point_reward"]![0]!["total_point"]!.GetValue<int>() == 100,
    "event overview returns an isolated snapshot");
Console.WriteLine("Passed 3 event overview checks.");
var sendContactSeed = JsonNode.Parse(seed)!.AsObject();
sendContactSeed["_local_friend_candidates"] = new JsonArray(new JsonObject { ["player_id"] = "candidate-a", ["player_name"] = "Local A" },
    new JsonObject { ["player_id"] = "candidate-b", ["player_name"] = "Local B" });
var sendContactDirectory = directory + "-send-contact";
var sendContact = new OfflineServer(sendContactDirectory, sendContactSeed.ToJsonString(), "offline://resources/");
sendContact.Dispatch("game_init");
sendContact.Dispatch("login");
Check(sendContact.Dispatch("friend_get_candidate_list").Body!["helper_list"]!.AsArray().Count == 2
    && sendContact.Dispatch("friend_request", arguments: new(EntityId: 2, Value: 1, Text: "missing")).Body!["result"]!.GetValue<int>() == 1,
    "contact requests only target stored candidates");
var sentContact = sendContact.Dispatch("friend_request", arguments: new(EntityId: 2, Value: 1, Text: "candidate-a")).Body!;
Check(sentContact["result"]!.GetValue<int>() == 0 && sentContact["friend_request_list"]![0]!["player_name"]!.GetValue<string>() == "Local A",
    "contact request persists candidate details");
Check(sendContact.Dispatch("friend_request", arguments: new(EntityId: 2, Value: 2, Text: "candidate-a")).Body!["result"]!.GetValue<int>() == 1
    && sendContact.Dispatch("friend_request", arguments: new(EntityId: 2, Value: 1, Text: "candidate-b")).Body!["result"]!.GetValue<int>() == 1,
    "duplicate and over-capacity requests are rejected");
var sendContactReload = new OfflineServer(sendContactDirectory, sendContactSeed.ToJsonString(), "offline://resources/");
sendContactReload.Dispatch("game_init");
sendContactReload.Dispatch("login");
Check(sendContactReload.Dispatch("friend_top").Body!["friend_request_list"]!.AsArray().Count == 1,
    "outgoing request survives reload");
sendContactReload.Dispatch("friend_request_cancel", arguments: new(Value: 0, Text: "candidate-a"));
Check(sendContactReload.Dispatch("friend_request", arguments: new(EntityId: 2, Value: 1, Text: "candidate-b")).Body!["result"]!.GetValue<int>() == 0,
    "cancellation releases local request capacity");
Console.WriteLine("Passed 5 outgoing contact request checks.");
var applyContactSeed = JsonNode.Parse(seed)!.AsObject();
applyContactSeed["_local_friend_recommendations"] = new JsonArray(new JsonObject { ["public_id"] = "candidate", ["player_name"] = "Local" });
applyContactSeed["_local_friends"] = new JsonArray(new JsonObject { ["public_id"] = "existing" });
var applyContactDirectory = directory + "-apply-contact";
var applyContact = new OfflineServer(applyContactDirectory, applyContactSeed.ToJsonString(), "offline://resources/");
applyContact.Dispatch("game_init");
applyContact.Dispatch("login");
Check(applyContact.Dispatch("friend/apply", arguments: new(Value: 1, Text: "candidate")).Status == 20007
    && applyContact.Dispatch("friend/apply", arguments: new(Value: 2, Text: "existing")).Status == 409
    && applyContact.Dispatch("friend/apply", arguments: new(Value: 2, Text: "missing")).Status == 404,
    "application checks local capacity, existing contacts and candidate presence");
Check(applyContact.Dispatch("friend/recommend").Body!["data"]!.AsArray().Count == 1
    && applyContact.Dispatch("friend/apply", arguments: new(Value: 2, Text: "candidate")).Status == 0,
    "application accepts a stored recommendation");
var applyContactReload = new OfflineServer(applyContactDirectory, applyContactSeed.ToJsonString(), "offline://resources/");
applyContactReload.Dispatch("game_init");
var appliedUser = applyContactReload.Dispatch("login").Body!["user"]!;
Check(appliedUser["_local_friend_outgoing"]!.AsArray().Count == 1
    && applyContactReload.Dispatch("friend/apply", arguments: new(Value: 2, Text: "candidate")).Status == 409,
    "application persists and rejects duplicate delivery after reload");
Check(applyContactReload.Dispatch("friend/apply_list").Body!["data"]!.AsArray().Count == 0
    && applyContactReload.Dispatch("friend/list").Body!["data"]!.AsArray().Count == 1,
    "outgoing application does not create an incoming request or accepted relation");
Console.WriteLine("Passed 4 public contact application checks.");
var episodeSeed = JsonNode.Parse(seed)!.AsObject();
episodeSeed["quest_playdata"] = new JsonArray(new JsonObject
{
    ["episode_id"] = 1, ["scenario_status"] = 0,
    ["normal"] = new JsonObject { ["status"] = 3, ["battle_challenge"] = new JsonArray(1, 2) }
});
var episodeDirectory = directory + "-episode";
var episodeServer = new OfflineServer(episodeDirectory, episodeSeed.ToJsonString(), "offline://resources/");
episodeServer.Dispatch("game_init");
episodeServer.Dispatch("login");
Check(episodeServer.Dispatch("episode_end", arguments: new(EntityId: 0, Value: 1)).Status == 400
    && episodeServer.Dispatch("episode_end", arguments: new(EntityId: 1)).Status == 503
    && episodeServer.Dispatch("episode_end", arguments: new(EntityId: 1, Value: 2)).Status == 404,
    "episode completion validates ID and known story type");
var completedEpisode = episodeServer.Dispatch("episode_end", arguments: new(EntityId: 1, Value: 1)).Body!["user_update"]!["quest_playdata"]![0]!;
Check(completedEpisode["scenario_status"]!.GetValue<int>() == 1 && completedEpisode["normal"]!["status"]!.GetValue<int>() == 3
    && completedEpisode["normal"]!["battle_challenge"]!.AsArray().Count == 2, "episode completion preserves existing battle progress");
episodeServer.Dispatch("episode_end", arguments: new(EntityId: 2, Value: 1));
episodeServer.Dispatch("episode_end", arguments: new(EntityId: 2, Value: 1));
var episodeReload = new OfflineServer(episodeDirectory, episodeSeed.ToJsonString(), "offline://resources/");
episodeReload.Dispatch("game_init");
var episodeUser = episodeReload.Dispatch("login").Body!["user"]!;
Check(episodeUser["quest_playdata"]!.AsArray().Count == 2
    && episodeUser["quest_playdata"]![1]!["scenario_status"]!.GetValue<int>() == 1
    && episodeUser["quest_playdata"]![1]!["normal"]!["status"]!.GetValue<int>() == -2,
    "new episode completion persists once without clearing battles");
Check(episodeUser["gold"]!.GetValue<int>() == episodeSeed["gold"]!.GetValue<int>(), "episode completion does not fabricate currency rewards");
Console.WriteLine("Passed 4 episode completion checks.");
var idleSeed = JsonNode.Parse(seed)!.AsObject();
var idleUnit = idleSeed["unit"]!["unit_list"]![0]!;
var idleUnitId = idleUnit["user_unit_id"]!.GetValue<long>();
var idleDirectory = directory + "-idle";
var idleServer = new OfflineServer(idleDirectory, idleSeed.ToJsonString(), "offline://resources/");
idleServer.Dispatch("game_init");
idleServer.Dispatch("login");
var idleRequest = new LocalIdleSet(idleUnitId, 9, 1, 4, 0, 2, 120, "rare", idleUnit["rare"]!.GetValue<int>(), true);
Check(idleServer.Dispatch("afk_set", arguments: new(Idle: idleRequest with { Seconds = 0 })).Status == 503
    && idleServer.Dispatch("afk_set", arguments: new(Idle: idleRequest with { Eligible = false })).Status == 409
    && idleServer.Dispatch("afk_set", arguments: new(Idle: idleRequest with { Slot = 3 })).Status == 409,
    "idle start requires duration, eligibility and an unlocked slot");
var idleStart = idleServer.Dispatch("afk_set", arguments: new(Idle: idleRequest));
Check(idleStart.Status == 0
    && (DateTime.Parse(idleStart.Body!["end_at"]!.GetValue<string>()) - DateTime.Parse(idleStart.Body["start_at"]!.GetValue<string>())).TotalSeconds == 120,
    "idle start returns the persisted duration");
Check(idleServer.Dispatch("afk_set", arguments: new(Idle: idleRequest)).Status == 409
    && idleServer.Dispatch("afk_set", arguments: new(Idle: idleRequest with { Slot = 2 })).Status == 409,
    "idle slots reject duplicate slot and object assignments");
var idleReload = new OfflineServer(idleDirectory, idleSeed.ToJsonString(), "offline://resources/");
idleReload.Dispatch("game_init");
var idleUser = idleReload.Dispatch("login").Body!["user"]!;
Check(idleReload.Dispatch("afk_top").Body!["unit_slot_list"]!.AsArray().Count == 1
    && idleUser["unit"]!["unit_list"]![0]!["rare"]!.GetValue<int>() == idleRequest.CurrentValue,
    "idle assignment survives reload without applying growth");
Check(idleReload.Dispatch("afk_set", arguments: new(Idle: idleRequest with { Cancel = 1, Content = 2 })).Status == 409
    && idleReload.Dispatch("afk_top").Body!["unit_slot_list"]!.AsArray().Count == 1,
    "mismatched idle cancellation preserves the slot");
Check(idleReload.Dispatch("afk_set", arguments: new(Idle: idleRequest with { Cancel = 1, Seconds = 0, Capacity = 0 })).Status == 0
    && idleReload.Dispatch("afk_top").Body!["unit_slot_list"]!.AsArray().Count == 0
    && idleReload.Dispatch("afk_set", arguments: new(Idle: idleRequest with { Cancel = 1 })).Status == 404,
    "idle cancellation removes the stored slot without requiring growth definitions");
Console.WriteLine("Passed 6 idle slot checks.");
idleServer = new OfflineServer(idleDirectory, idleSeed.ToJsonString(), "offline://resources/");
idleServer.Dispatch("game_init");
idleServer.Dispatch("login");
idleServer.Dispatch("afk_set", arguments: new(Idle: idleRequest));
Check(idleServer.Dispatch("afk_complete_upgrade", arguments: new(EntityId: 1, Value: 9, Idle: idleRequest)).Status == 409,
    "idle completion rejects tasks before their finish time");
foreach (var (objectType, content, growthField) in new[] { (9, 4, "rare"), (9, 2, "destiny_value"), (10, 3, "potential_limitbreak"), (10, 7, "spec_actualize") })
{
    var completionSeed = JsonNode.Parse(seed)!.AsObject();
    var objectKey = objectType == 9 ? "unit" : "picture";
    completionSeed[objectKey]![objectKey + "_list"] = new JsonArray(new JsonObject
    {
        ["user_" + objectKey + "_id"] = 101L, [objectKey + "_id"] = 1, [growthField] = 1
    });
    completionSeed["_local_idle"] = new JsonObject
    {
        [objectKey + "_slot_list"] = new JsonArray(new JsonObject
        {
            ["slot_number"] = 1, ["object_type"] = objectType, ["object_id"] = 1, ["user_object_id"] = 101L,
            ["content"] = content, ["start_at"] = "2000-01-01 00:00:00", ["end_at"] = "2000-01-01 00:01:00",
            ["_local_growth_field"] = growthField, ["_local_growth_value"] = 1
        })
    };
    var completionDirectory = directory + "-idle-completion-" + content;
    var completionServer = new OfflineServer(completionDirectory, completionSeed.ToJsonString(), "offline://resources/");
    completionServer.Dispatch("game_init");
    completionServer.Dispatch("login");
    var completion = new LocalIdleSet(101, objectType, 1, content, 0, GrowthField: growthField, CurrentValue: 1, Eligible: true);
    Check(completionServer.Dispatch("afk_complete_upgrade", arguments: new(EntityId: 1, Value: objectType, Idle: completion with { CurrentValue = 2 })).Status == 409,
        "idle completion checks the original growth snapshot");
    var completed = completionServer.Dispatch("afk_complete_upgrade", arguments: new(EntityId: 1, Value: objectType, Idle: completion));
    Check(completed.Status == 0 && completed.Body!["user_update"]![objectKey]![objectKey + "_list"]![0]![growthField]!.GetValue<int>() == 2
        && completionServer.Dispatch("afk_complete_upgrade", arguments: new(EntityId: 1, Value: objectType, Idle: completion)).Status == 404,
        "idle completion grows once and removes the task");
    var completedReload = new OfflineServer(completionDirectory, completionSeed.ToJsonString(), "offline://resources/");
    completedReload.Dispatch("game_init");
    var completedUser = completedReload.Dispatch("login").Body!["user"]!;
    Check(completedUser[objectKey]![objectKey + "_list"]![0]![growthField]!.GetValue<int>() == 2
        && completedReload.Dispatch("afk_top").Body![objectKey + "_slot_list"]!.AsArray().Count == 0,
        "growth and task removal persist together");
}
Console.WriteLine("Passed 13 idle completion checks.");
var emptyIdleDrop = idleReload.Dispatch("afk_receive_drop");
Check(emptyIdleDrop.Status == 0 && emptyIdleDrop.Body!["drop_list"]!.AsArray().Count == 0
    && emptyIdleDrop.Body["search_elapsed_time"]!.GetValue<int>() == 0
    && emptyIdleDrop.Body["search_max_time"]!.GetValue<int>() > 0 && emptyIdleDrop.Body["user_update"] is null,
    "empty idle claim returns no inventory mutation");
var pendingDropSeed = JsonNode.Parse(seed)!.AsObject();
pendingDropSeed["_local_idle"] = new JsonObject { ["drop_list"] = new JsonArray(new JsonObject
{
    ["object_type"] = 1, ["object_id"] = 0, ["object_value"] = 10
}) };
var pendingDrop = new OfflineServer(directory + "-pending-idle-drop", pendingDropSeed.ToJsonString(), "offline://resources/");
pendingDrop.Dispatch("game_init");
pendingDrop.Dispatch("login");
Check(pendingDrop.Dispatch("afk_receive_drop").Status == 503
    && pendingDrop.Dispatch("login").Body!["user"]!["_local_idle"]!["drop_list"]!.AsArray().Count == 1,
    "unsupported idle rewards remain pending instead of being silently cleared");
Console.WriteLine("Passed 2 idle drop boundary checks.");
var unavailableHelper = idleReload.Dispatch("friend_get_helper_detail").Body!;
Check(unavailableHelper["helper_unit_info"]!["unit_info"]!["unit_id"]!.GetValue<int>() == 0
    && unavailableHelper["user_update"] is null, "missing helper has an explicit unavailable unit rather than null metadata");
unavailableHelper["helper_unit_info"]!["unit_info"]!["unit_id"] = 1;
Check(idleReload.Dispatch("friend_get_helper_detail").Body!["helper_unit_info"]!["unit_info"]!["unit_id"]!.GetValue<int>() == 0,
    "helper response mutation cannot alter subsequent queries");
Console.WriteLine("Passed 2 unavailable helper response checks.");
var clearedTeamSeed = JsonNode.Parse(seed)!.AsObject();
clearedTeamSeed["_local_quest_cleared_teams"] = new JsonArray(new JsonObject
{
    ["quest_type"] = 2, ["target_id"] = 10, ["difficulty"] = 1,
    ["quest_cleared_team"] = "Saved,1,2,0,0,0,0,", ["use_cost"] = 12,
    ["season_enhancement_info"] = new JsonArray(0L, 2L, 0L, 0L, 0L, 0L)
});
var clearedTeamServer = new OfflineServer(directory + "-cleared-team", clearedTeamSeed.ToJsonString(), "offline://resources/");
clearedTeamServer.Dispatch("game_init");
clearedTeamServer.Dispatch("login");
var clearQuery = new LocalArguments(ReplayMeasure: new(2, 10, 1));
var clearReply = clearedTeamServer.Dispatch("get_quest_cleared_team_info", arguments: clearQuery).Body!;
Check(clearReply["quest_cleared_team"]!.GetValue<string>() == "Saved,1,2,0,0,0,0,"
    && clearReply["use_cost"]!.GetValue<int>() == 12 && clearReply["season_enhancement_info"]![1]!.GetValue<long>() == 2,
    "cleared team query preserves stored encoding, cost and enhancement slots");
Check(clearedTeamServer.Dispatch("get_quest_cleared_team_info", arguments: new(ReplayMeasure: new(2, 10, 0))).Body!["quest_cleared_team"]!.GetValue<string>() == ""
    && clearedTeamServer.Dispatch("get_quest_cleared_team_info", arguments: new(ReplayMeasure: new(0, 10, 1))).Body!["season_enhancement_info"]!.AsArray().Count == 6,
    "cleared team queries isolate difficulty and type with a six-slot empty fallback");
Check(clearedTeamServer.Dispatch("get_quest_cleared_team_info", arguments: new(ReplayMeasure: new(3, 10, 0))).Status == 400,
    "cleared team query rejects unknown types");
Console.WriteLine("Passed 3 cleared team query checks.");
var helperListSeed = JsonNode.Parse(seed)!.AsObject();
helperListSeed["_local_friend_players"] = new JsonArray(new JsonObject { ["player_id"] = "contact" });
helperListSeed["_local_friend_requests"] = new JsonArray(new JsonObject { ["player_id"] = "pending" });
var helperListServer = new OfflineServer(directory + "-helper-list", helperListSeed.ToJsonString(), "offline://resources/");
helperListServer.Dispatch("game_init");
helperListServer.Dispatch("login");
var helperList = helperListServer.Dispatch("friend_helper_select", arguments: new(Value: 1)).Body!;
Check(helperList["helper_list"]!.AsArray().Count == 0 && helperList["is_friend_max"]!.GetValue<int>() == 1
    && helperList["friend_request_count"]!.GetValue<int>() == 1 && helperList["user_update"] is null,
    "helper listing reports local capacity and outgoing count without producing candidates or rewards");
Check(helperListServer.Dispatch("friend_helper_select", arguments: new(Value: 2)).Body!["is_friend_max"]!.GetValue<int>() == 0
    && helperListServer.Dispatch("friend_helper_select").Status == 503,
    "helper listing uses a known capacity definition");
Console.WriteLine("Passed 2 helper list checks.");
JsonObject MailFixture(long id, int type, int amount, int itemId = 0, int grace = 0) => new()
{
    ["id"] = id, ["open_type"] = 0, ["receive_grace"] = grace,
    ["present_list"] = new JsonArray(new JsonObject { ["object_type"] = type, ["object_id"] = itemId, ["object_value"] = amount })
};
var receiptSeed = JsonNode.Parse(seed)!.AsObject();
receiptSeed["_local_mail"] = new JsonArray(MailFixture(1, 3, 30), MailFixture(2, 6, 2), MailFixture(3, 12, 5, 99999),
    MailFixture(4, 9, 1), MailFixture(5, 3, 1, grace: 1));
var receiptDirectory = directory + "-mail-receive";
var mailServer = new OfflineServer(receiptDirectory, receiptSeed.ToJsonString(), "offline://resources/");
mailServer.Dispatch("game_init");
mailServer.Dispatch("login");
var mailRules = new LocalMailRules(1000000, 1000000, 999, [99999]);
Check(mailServer.Dispatch("mail_receive", arguments: new(Ids: [1, 4], MailRules: mailRules)).Status == 503
    && mailServer.Dispatch("login").Body!["user"]!["gold"]!.GetValue<int>() == receiptSeed["gold"]!.GetValue<int>()
    && mailServer.Dispatch("mail_top").Body!["mail"]![0]!["open_type"]!.GetValue<int>() == 0,
    "unsupported mixed rewards preserve all mail and currency");
Check(mailServer.Dispatch("mail_receive", arguments: new(Ids: [1], MailRules: mailRules with { GoldMax = 1 })).Status == 409
    && mailServer.Dispatch("mail_receive", arguments: new(Ids: [5], MailRules: mailRules)).Status == 503
    && mailServer.Dispatch("mail_receive", arguments: new(Ids: [1, 1], MailRules: mailRules)).Status == 400,
    "mail claims reject capacity overflow, unsupported expiry and duplicate IDs");
var receivedMail = mailServer.Dispatch("mail_receive", arguments: new(Ids: [1, 2, 3], MailRules: mailRules)).Body!;
Check(receivedMail["user_update"]!["gold"]!.GetValue<int>() == receiptSeed["gold"]!.GetValue<int>() + 30
    && receivedMail["user_update"]!["crystal"]!.GetValue<int>() == receiptSeed["crystal"]!.GetValue<int>() + 2
    && receivedMail["user_update"]!["item"]!["item_list"]!.AsArray().Single(entry => entry!["item_id"]!.GetValue<int>() == 99999)!["num"]!.GetValue<int>() == 5
    && receivedMail["unread_mail_num"]!.GetValue<int>() == 2, "mail claims apply supported inventory and receipt states together");
var receiptReload = new OfflineServer(receiptDirectory, receiptSeed.ToJsonString(), "offline://resources/");
receiptReload.Dispatch("game_init");
receiptReload.Dispatch("login");
var repeatedMail = receiptReload.Dispatch("mail_receive", arguments: new(Ids: [1, 2, 3])).Body!;
Check(repeatedMail["user_update"]!["gold"]!.GetValue<int>() == receivedMail["user_update"]!["gold"]!.GetValue<int>()
    && repeatedMail["mail"]![0]!["open_type"]!.GetValue<int>() == 1,
    "reloaded mail receipts prevent duplicate rewards without requiring definitions again");
Check(receiptReload.Dispatch("mail_receive", arguments: new(Ids: [99], MailRules: mailRules)).Status == 404
    && receiptReload.Dispatch("mail_receive", arguments: new(Ids: [])).Status == 0, "mail claims distinguish missing IDs and an empty selection");
Console.WriteLine("Passed 5 mail receipt checks.");
var bootDirectory = directory + "-boot";
var bootServer = new OfflineServer(bootDirectory, seed, "offline://resources/");
Check(bootServer.Dispatch("boot").Status == 401, "boot notification requires initialization");
var bootSession = bootServer.Dispatch("game_init").Body!["session_id"]!.GetValue<string>();
var bootReply = bootServer.Dispatch("boot");
Check(bootReply.Status == 0 && bootReply.Body!["session_id"]!.GetValue<string>() == bootSession
    && bootReply.Body["user_update"] is null && !File.Exists(Path.Combine(bootDirectory, "account.json")),
    "boot notification preserves the initialized session without creating an account");
Console.WriteLine("Passed 2 boot notification checks.");
if (OperatingSystem.IsWindows())
{
    var lockedDirectory = directory + "-locked-save";
    var lockedServer = new OfflineServer(lockedDirectory, seed, "offline://resources/");
    lockedServer.Dispatch("game_init");
    lockedServer.Dispatch("login");
    var savePath = Path.Combine(lockedDirectory, "user.json");
    var shortLock = File.Open(savePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    var release = Task.Run(async () => { await Task.Delay(100); shortLock.Dispose(); });
    Check(lockedServer.Dispatch("set_age", arguments: new(Value: 1)).Status == 0,
        "save retries a temporary Windows delete-sharing conflict");
    release.GetAwaiter().GetResult();
    var rejected = false;
    using (File.Open(savePath, FileMode.Open, FileAccess.Read, FileShare.Read))
    {
        try { lockedServer.Dispatch("set_age", arguments: new(Value: 2)); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { rejected = true; }
    }
    Check(rejected && lockedServer.Dispatch("login").Body!["user"]!["age_limit_type"]!.GetValue<int>() == 1
        && JsonNode.Parse(File.ReadAllText(savePath))!["age_limit_type"]!.GetValue<int>() == 1,
        "persistent conflict still fails without changing memory or the saved file");
    Check(lockedServer.Dispatch("set_age", arguments: new(Value: 2)).Status == 0,
        "save succeeds after the conflict is released");
    Console.WriteLine("Passed 3 Windows save conflict checks.");
}

var towerOverviewSeed = JsonNode.Parse(seed)!;
towerOverviewSeed["tower"]!["cleared_floor"] = 7;
towerOverviewSeed["tower"]!["exist_data"] = 1;
var towerOverviewServer = new OfflineServer(directory + "-tower-overview", towerOverviewSeed.ToJsonString(), "offline://resources/");
towerOverviewServer.Dispatch("game_init");
towerOverviewServer.Dispatch("login");
var towerOverviewReply = towerOverviewServer.Dispatch("tower_top").Body!;
Check(towerOverviewReply["cleared_floor"]!.GetValue<int>() == 7 && towerOverviewReply["exist_data"]!.GetValue<uint>() == 1
    && towerOverviewReply["remaining_recovery_count"]!.GetValue<int>() == 0 && towerOverviewReply["enemy_battle_info"]!.GetValue<string>() == "",
    "floor overview preserves saved progress and supplies an empty continuation stream");
towerOverviewReply["cleared_floor"] = 99;
Check(towerOverviewServer.Dispatch("tower_top").Body!["cleared_floor"]!.GetValue<int>() == 7
    && towerOverviewServer.Dispatch("login").Body!["user"]!["tower"]!["cleared_floor"]!.GetValue<int>() == 7,
    "floor overview does not advance progress or expose mutable saved state");
towerOverviewSeed["tower"]!["cleared_floor"] = -1;
var invalidTowerOverview = new OfflineServer(directory + "-invalid-tower-overview", towerOverviewSeed.ToJsonString(), "offline://resources/");
invalidTowerOverview.Dispatch("game_init");
invalidTowerOverview.Dispatch("login");
Check(invalidTowerOverview.Dispatch("tower_top").Status == 503, "invalid floor state is not presented as a successful overview");
Console.WriteLine("Passed 3 floor overview checks.");

Check(towerOverviewServer.Dispatch("get_tower_unit_info").Body!["tower_unit_info"]!.GetValue<string>() == "",
    "absent unit continuation state remains empty");
var towerUnitCsv = "1,123,5,100," + string.Join(",", Enumerable.Repeat("0", 25));
var towerUnitSeed = JsonNode.Parse(seed)!;
towerUnitSeed["_local_tower_unit_info"] = towerUnitCsv;
var towerUnitServer = new OfflineServer(directory + "-tower-unit-info", towerUnitSeed.ToJsonString(), "offline://resources/");
towerUnitServer.Dispatch("game_init");
towerUnitServer.Dispatch("login");
Check(towerUnitServer.Dispatch("get_tower_unit_info").Body!["tower_unit_info"]!.GetValue<string>() == towerUnitCsv,
    "saved unit continuation CSV is preserved without recovery or regeneration");
foreach (var badCsv in new[] { "1,123", towerUnitCsv + "\n" + towerUnitCsv, "999" + towerUnitCsv[1..], towerUnitCsv + ",bad" })
{
    towerUnitSeed["_local_tower_unit_info"] = badCsv;
    var badTowerUnits = new OfflineServer(directory + "-tower-unit-invalid-" + Guid.NewGuid().ToString("N"), towerUnitSeed.ToJsonString(), "offline://resources/");
    badTowerUnits.Dispatch("game_init");
    badTowerUnits.Dispatch("login");
    Check(badTowerUnits.Dispatch("get_tower_unit_info").Status == 503, "invalid, duplicate or unowned continuation records are rejected");
}
Console.WriteLine("Passed 6 unit continuation checks.");

var deletionDirectory = directory + "-account-deletion";
var deletionServer = new OfflineServer(deletionDirectory, seed, "offline://resources/");
Check(deletionServer.Dispatch("delete_account").Status == 401, "account deletion requires initialization");
deletionServer.Dispatch("game_init");
Check(deletionServer.Dispatch("delete_account").Status == 401, "account deletion requires an existing account");
var deletedIdentity = deletionServer.Dispatch("login").Body!["device_id"]!.GetValue<string>();
deletionServer.Dispatch("set_age", arguments: new(Value: 2));
File.WriteAllText(Path.Combine(deletionDirectory, "user.json.before-seed-fix"), "old snapshot");
Check(deletionServer.Dispatch("delete_account").Status == 0
    && !File.Exists(Path.Combine(deletionDirectory, "account.json"))
    && !File.Exists(Path.Combine(deletionDirectory, "user.json.before-seed-fix"))
    && JsonNode.Parse(File.ReadAllText(Path.Combine(deletionDirectory, "user.json")))!.AsObject().Count == 1,
    "deletion removes the identity, saved content and module backup while retaining a durable marker");
Check(deletionServer.Dispatch("login").Status == 401, "deletion invalidates the current session");
var afterDeletion = new OfflineServer(deletionDirectory, seed, "offline://resources/");
afterDeletion.Dispatch("game_init");
Check(afterDeletion.Dispatch("check_user").Status == 401, "deleted account cannot return after restart");
var replacementAccount = afterDeletion.Dispatch("login").Body!;
Check(replacementAccount["device_id"]!.GetValue<string>() != deletedIdentity
    && replacementAccount["user"]!["age_limit_type"]!.GetValue<int>() == JsonNode.Parse(seed)!["age_limit_type"]!.GetValue<int>(),
    "new registration creates a distinct identity with initial state");
File.WriteAllText(Path.Combine(deletionDirectory, "user.json"), "{\"_local_account_deleted\":true}");
File.WriteAllText(Path.Combine(deletionDirectory, "account.json.tmp"), "unfinished identity");
var interruptedDeletion = new OfflineServer(deletionDirectory, seed, "offline://resources/");
interruptedDeletion.Dispatch("game_init");
Check(interruptedDeletion.Dispatch("check_user").Status == 401
    && !File.Exists(Path.Combine(deletionDirectory, "account.json"))
    && !File.Exists(Path.Combine(deletionDirectory, "account.json.tmp")),
    "restart completes deletion committed before identity-file cleanup");
Console.WriteLine("Passed 7 local account deletion checks.");

var giftSeed = JsonNode.Parse(seed)!;
giftSeed["_local_friends"] = new JsonArray(
    new JsonObject { ["public_id"] = "A", ["is_give_gold"] = true },
    new JsonObject { ["public_id"] = "B", ["is_give_gold"] = true });
var giftDirectory = directory + "-contact-gifts";
var giftServer = new OfflineServer(giftDirectory, giftSeed.ToJsonString(), "offline://resources/");
giftServer.Dispatch("game_init");
giftServer.Dispatch("login");
Check(giftServer.Dispatch("friend/give", arguments: new(Value: 0)).Status == 400
    && giftServer.Dispatch("friend/give", arguments: new(Value: 0, Text: "unknown")).Status == 404,
    "gift sending requires a valid local target");
Check(giftServer.Dispatch("friend/give", arguments: new(Value: 0, Text: "A")).Status == 0
    && !giftServer.Dispatch("friend/list").Body!["data"]![0]!["is_give_gold"]!.GetValue<bool>()
    && giftServer.Dispatch("friend/list").Body!["data"]![1]!["is_give_gold"]!.GetValue<bool>(),
    "single gift changes only the selected contact");
giftServer.Dispatch("friend/give", arguments: new(Value: 0, Text: "A"));
giftServer.Dispatch("friend/give", arguments: new(Value: 1));
var giftSaved = JsonNode.Parse(File.ReadAllText(Path.Combine(giftDirectory, "user.json")))!;
Check(giftSaved["_local_friend_gifts_sent"]!.AsArray().Count == 2
    && giftSaved["gold"]!.GetValue<int>() == JsonNode.Parse(seed)!["gold"]!.GetValue<int>(),
    "batch gifts skip already sent targets and do not invent rewards");
var giftReload = new OfflineServer(giftDirectory, seed, "offline://resources/");
giftReload.Dispatch("game_init");
giftReload.Dispatch("login");
giftReload.Dispatch("friend/give", arguments: new(Value: 1));
Check(JsonNode.Parse(File.ReadAllText(Path.Combine(giftDirectory, "user.json")))!["_local_friend_gifts_sent"]!.AsArray().Count == 2,
    "gift flags and outgoing records survive restart without duplicate sends");
Console.WriteLine("Passed 4 local gift checks.");

var receiveGiftSeed = JsonNode.Parse(seed)!;
receiveGiftSeed["_local_friends"] = new JsonArray(
    new JsonObject { ["public_id"] = "A", ["is_receive_gold"] = true },
    new JsonObject { ["public_id"] = "B", ["is_receive_gold"] = true });
receiveGiftSeed["_local_friend_gifts_inbox"] = new JsonArray(new JsonObject { ["public_id"] = "A", ["gold"] = 25 });
var receiveGiftDirectory = directory + "-receive-gift";
var receiveGiftServer = new OfflineServer(receiveGiftDirectory, receiveGiftSeed.ToJsonString(), "offline://resources/");
receiveGiftServer.Dispatch("game_init");
receiveGiftServer.Dispatch("login");
Check(receiveGiftServer.Dispatch("friend/receive", arguments: new(99999, 1)).Status == 503
    && receiveGiftServer.Dispatch("login").Body!["user"]!["gold"]!.GetValue<long>() == 5000,
    "missing gift amount preserves the entire selected batch");
Check(receiveGiftServer.Dispatch("friend/receive", arguments: new(5000, 0, Text: "A")).Status == 409,
    "gift capacity rejection preserves the receipt");
var receivedGiftReply = receiveGiftServer.Dispatch("friend/receive", arguments: new(99999, 0, Text: "A")).Body!;
Check(receivedGiftReply["user_update"]!["gold"]!.GetValue<int>() == 5025
    && receivedGiftReply["user_list"]![0]!.GetValue<string>() == "A",
    "gift receipt credits the saved amount and reports its sender");
var receiveGiftReload = new OfflineServer(receiveGiftDirectory, seed, "offline://resources/");
receiveGiftReload.Dispatch("game_init");
receiveGiftReload.Dispatch("login");
Check(receiveGiftReload.Dispatch("friend/receive", arguments: new(99999, 0, Text: "A")).Body!["user_list"]!.AsArray().Count == 0
    && receiveGiftReload.Dispatch("login").Body!["user"]!["gold"]!.GetValue<long>() == 5025,
    "replayed gift receipt cannot credit the same amount after restart");
receiveGiftSeed["_local_friend_gift_day"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
receiveGiftSeed["_local_friend_gift_count"] = 29;
receiveGiftSeed["_local_friend_gifts_inbox"]!.AsArray().Add(new JsonObject { ["public_id"] = "B", ["gold"] = 30 });
var limitedGifts = new OfflineServer(directory + "-limited-gifts", receiveGiftSeed.ToJsonString(), "offline://resources/");
limitedGifts.Dispatch("game_init");
limitedGifts.Dispatch("login");
Check(limitedGifts.Dispatch("friend/receive", arguments: new(99999, 1)).Body!["user_list"]!.AsArray().Count == 1
    && limitedGifts.Dispatch("friend/receive", arguments: new(99999, 1)).Status == 20006
    && limitedGifts.Dispatch("friend/list").Body!["data"]![1]!["is_receive_gold"]!.GetValue<bool>(),
    "daily limit leaves unclaimed gifts pending");
Console.WriteLine("Passed 5 gift receipt checks.");

var gatherSeed = JsonNode.Parse(seed)!;
gatherSeed["_local_gathering_gold"] = 100;
var gatherDirectory = directory + "-gather-gold";
var gatherServer = new OfflineServer(gatherDirectory, gatherSeed.ToJsonString(), "offline://resources/");
gatherServer.Dispatch("game_init");
gatherServer.Dispatch("login");
Check(gatherServer.Dispatch("gather_gold", arguments: new(5050, 2)).Status == 503
    && gatherServer.Dispatch("home").Body!["gathering_gold"]!.GetValue<int>() == 100,
    "gathering rejects stale level capacity without consuming stock");
var gatherReply = gatherServer.Dispatch("gather_gold", arguments: new(5050, 1)).Body!;
Check(gatherReply["gathering_gold"]!.GetValue<int>() == 50
    && gatherReply["user_update"]!["gold"]!.GetValue<int>() == 5050
    && gatherServer.Dispatch("home").Body!["gathering_gold"]!.GetValue<int>() == 50,
    "gathering fills available capacity and preserves excess stock");
Check(gatherServer.Dispatch("gather_gold", arguments: new(5050, 1)).Body!["gathering_gold"]!.GetValue<int>() == 50,
    "full balance does not discard stock");
var gatherReload = new OfflineServer(gatherDirectory, seed, "offline://resources/");
gatherReload.Dispatch("game_init");
gatherReload.Dispatch("login");
Check(gatherReload.Dispatch("gather_gold", arguments: new(9999, 1)).Body!["user_update"]!["gold"]!.GetValue<int>() == 5100
    && gatherReload.Dispatch("gather_gold", arguments: new(9999, 1)).Body!["user_update"]!["gold"]!.GetValue<int>() == 5100,
    "saved stock survives restart and cannot be collected twice");
Console.WriteLine("Passed 4 local gathering checks.");

var gatherPointSeed = JsonNode.Parse(seed)!;
gatherPointSeed["_local_gathering_phantasm_point"] = 50;
gatherPointSeed["phantasm_point"]!["purchase_count"] = 7;
var gatherPointDirectory = directory + "-gather-points";
var gatherPointServer = new OfflineServer(gatherPointDirectory, gatherPointSeed.ToJsonString(), "offline://resources/");
gatherPointServer.Dispatch("game_init");
gatherPointServer.Dispatch("login");
Check(gatherPointServer.Dispatch("gather_phantasm_point", arguments: new(120, 2)).Status == 503,
    "point gathering requires current level capacity");
var gatheredPoints = gatherPointServer.Dispatch("gather_phantasm_point", arguments: new(120, 1)).Body!;
Check(gatheredPoints["gathering_phantasm_point"]!.GetValue<int>() == 30
    && gatheredPoints["user_update"]!["phantasm_point"]!["points"]!.GetValue<int>() == 120
    && gatheredPoints["user_update"]!["phantasm_point"]!["purchase_count"]!.GetValue<int>() == 7
    && gatherPointServer.Dispatch("home").Body!["gathering_phantasm_point"]!.GetValue<int>() == 30,
    "point gathering preserves excess stock and unrelated balance fields");
Check(gatherPointServer.Dispatch("gather_phantasm_point", arguments: new(120, 1)).Body!["gathering_phantasm_point"]!.GetValue<int>() == 30,
    "full point balance preserves remaining stock");
var gatherPointReload = new OfflineServer(gatherPointDirectory, seed, "offline://resources/");
gatherPointReload.Dispatch("game_init");
gatherPointReload.Dispatch("login");
gatherPointReload.Dispatch("gather_phantasm_point", arguments: new(9999, 1));
Check(gatherPointReload.Dispatch("gather_phantasm_point", arguments: new(9999, 1)).Body!["user_update"]!["phantasm_point"]!["points"]!.GetValue<int>() == 150,
    "point stock persists and repeated gathering does not duplicate credit");
Console.WriteLine("Passed 4 point gathering checks.");
