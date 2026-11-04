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
    ("friend_get_candidate_list", "helper_list"), ("get_auto_replay_info", "win_rate"),
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
Console.WriteLine("Passed 23 query and progress checks.");
