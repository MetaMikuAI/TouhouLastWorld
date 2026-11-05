using System.Text.Json.Nodes;
using TouhouLastWorld;

internal sealed partial class RegressionSuite
{
    private void Scenario07()
    {
        var bootDirectory = directory + "-boot";
        var bootServer = new OfflineServer(new JsonStateStore(bootDirectory), seed, "offline://resources/");
        Check(bootServer.Dispatch("boot").Status == 401, "boot notification requires initialization");
        var bootSession = bootServer.Dispatch("game_init").Body!["session_id"]!.GetValue<string>();
        var bootReply = bootServer.Dispatch("boot");
        Check(bootReply.Status == 0 && bootReply.Body!["session_id"]!.GetValue<string>() == bootSession && bootReply.Body["user_update"] is null && !File.Exists(Path.Combine(bootDirectory, "account.json")), "boot notification preserves the initialized session without creating an account");
        Console.WriteLine("Passed 2 boot notification checks.");
        if (OperatingSystem.IsWindows())
        {
            var lockedDirectory = directory + "-locked-save";
            var lockedServer = new OfflineServer(new JsonStateStore(lockedDirectory), seed, "offline://resources/");
            lockedServer.Dispatch("game_init");
            lockedServer.Dispatch("login");
            var savePath = Path.Combine(lockedDirectory, "user.json");
            var shortLock = File.Open(savePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var release = Task.Run(async () =>
            {
                await Task.Delay(100);
                shortLock.Dispose();
            });
            Check(lockedServer.Dispatch("set_age", arguments: new(Value: 1)).Status == 0, "save retries a temporary Windows delete-sharing conflict");
            release.GetAwaiter().GetResult();
            var rejected = false;
            using (File.Open(savePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                try
                {
                    lockedServer.Dispatch("set_age", arguments: new(Value: 2));
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    rejected = true;
                }
            }

            Check(rejected && lockedServer.Dispatch("login").Body!["user"]!["age_limit_type"]!.GetValue<int>() == 1 && JsonNode.Parse(File.ReadAllText(savePath))!["age_limit_type"]!.GetValue<int>() == 1, "persistent conflict still fails without changing memory or the saved file");
            Check(lockedServer.Dispatch("set_age", arguments: new(Value: 2)).Status == 0, "save succeeds after the conflict is released");
            Console.WriteLine("Passed 3 Windows save conflict checks.");
        }

        var towerOverviewSeed = JsonNode.Parse(seed)!;
        towerOverviewSeed["tower"]!["cleared_floor"] = 7;
        towerOverviewSeed["tower"]!["exist_data"] = 1;
        var towerOverviewServer = new OfflineServer(new JsonStateStore(directory + "-tower-overview"), towerOverviewSeed.ToJsonString(), "offline://resources/");
        towerOverviewServer.Dispatch("game_init");
        towerOverviewServer.Dispatch("login");
        var towerOverviewReply = towerOverviewServer.Dispatch("tower_top").Body!;
        Check(towerOverviewReply["cleared_floor"]!.GetValue<int>() == 7 && towerOverviewReply["exist_data"]!.GetValue<uint>() == 1 && towerOverviewReply["remaining_recovery_count"]!.GetValue<int>() == 0 && towerOverviewReply["enemy_battle_info"]!.GetValue<string>() == "", "floor overview preserves saved progress and supplies an empty continuation stream");
        towerOverviewReply["cleared_floor"] = 99;
        Check(towerOverviewServer.Dispatch("tower_top").Body!["cleared_floor"]!.GetValue<int>() == 7 && towerOverviewServer.Dispatch("login").Body!["user"]!["tower"]!["cleared_floor"]!.GetValue<int>() == 7, "floor overview does not advance progress or expose mutable saved state");
        towerOverviewSeed["tower"]!["cleared_floor"] = -1;
        var invalidTowerOverview = new OfflineServer(new JsonStateStore(directory + "-invalid-tower-overview"), towerOverviewSeed.ToJsonString(), "offline://resources/");
        invalidTowerOverview.Dispatch("game_init");
        invalidTowerOverview.Dispatch("login");
        Check(invalidTowerOverview.Dispatch("tower_top").Status == 503, "invalid floor state is not presented as a successful overview");
        Console.WriteLine("Passed 3 floor overview checks.");
        Check(towerOverviewServer.Dispatch("get_tower_unit_info").Body!["tower_unit_info"]!.GetValue<string>() == "", "absent unit continuation state remains empty");
        towerUnitCsv = "1,123,5,100," + string.Join(",", Enumerable.Repeat("0", 25));
        var towerUnitSeed = JsonNode.Parse(seed)!;
        towerUnitSeed["_local_tower_unit_info"] = towerUnitCsv;
        var towerUnitServer = new OfflineServer(new JsonStateStore(directory + "-tower-unit-info"), towerUnitSeed.ToJsonString(), "offline://resources/");
        towerUnitServer.Dispatch("game_init");
        towerUnitServer.Dispatch("login");
        Check(towerUnitServer.Dispatch("get_tower_unit_info").Body!["tower_unit_info"]!.GetValue<string>() == towerUnitCsv, "saved unit continuation CSV is preserved without recovery or regeneration");
        foreach (var badCsv in new[]
        {
        "1,123",
        towerUnitCsv + "\n" + towerUnitCsv,
        "999" + towerUnitCsv[1..],
        towerUnitCsv + ",bad",
        towerUnitCsv + ",,",
        towerUnitCsv.Replace("1,123", "1,,123")
    }

        )
        {
            towerUnitSeed["_local_tower_unit_info"] = badCsv;
            var badTowerUnits = new OfflineServer(new JsonStateStore(directory + "-tower-unit-invalid-" + Guid.NewGuid().ToString("N")), towerUnitSeed.ToJsonString(), "offline://resources/");
            badTowerUnits.Dispatch("game_init");
            badTowerUnits.Dispatch("login");
            Check(badTowerUnits.Dispatch("get_tower_unit_info").Status == 503, "invalid, duplicate or unowned continuation records are rejected");
        }

        towerUnitSeed["_local_tower_unit_info"] = towerUnitCsv + ",\r\n";
        var nativeTowerCsv = new OfflineServer(new JsonStateStore(directory + "-tower-native-csv"), towerUnitSeed.ToJsonString(), "offline://resources/");
        nativeTowerCsv.Dispatch("game_init");
        nativeTowerCsv.Dispatch("login");
        Check(nativeTowerCsv.Dispatch("get_tower_unit_info").Body!["tower_unit_info"]!.GetValue<string>() == towerUnitCsv + ",\r\n", "native continuation CSV accepts one trailing delimiter and retains its original snapshot");
        Console.WriteLine("Passed 9 unit continuation checks.");
        var deletionDirectory = directory + "-account-deletion";
        var deletionServer = new OfflineServer(new JsonStateStore(deletionDirectory), seed, "offline://resources/");
        Check(deletionServer.Dispatch("delete_account").Status == 401, "account deletion requires initialization");
        deletionServer.Dispatch("game_init");
        Check(deletionServer.Dispatch("delete_account").Status == 401, "account deletion requires an existing account");
        var deletedIdentity = deletionServer.Dispatch("login").Body!["device_id"]!.GetValue<string>();
        deletionServer.Dispatch("set_age", arguments: new(Value: 2));
        File.WriteAllText(Path.Combine(deletionDirectory, "user.json.before-seed-fix"), "old snapshot");
        Check(deletionServer.Dispatch("delete_account").Status == 0 && !File.Exists(Path.Combine(deletionDirectory, "account.json")) && !File.Exists(Path.Combine(deletionDirectory, "user.json.before-seed-fix")) && JsonNode.Parse(File.ReadAllText(Path.Combine(deletionDirectory, "user.json")))!.AsObject().Count == 1, "deletion removes the identity, saved content and module backup while retaining a durable marker");
        Check(deletionServer.Dispatch("login").Status == 401, "deletion invalidates the current session");
        var afterDeletion = new OfflineServer(new JsonStateStore(deletionDirectory), seed, "offline://resources/");
        afterDeletion.Dispatch("game_init");
        Check(afterDeletion.Dispatch("check_user").Status == 401, "deleted account cannot return after restart");
        var replacementAccount = afterDeletion.Dispatch("login").Body!;
        Check(replacementAccount["device_id"]!.GetValue<string>() != deletedIdentity && replacementAccount["user"]!["age_limit_type"]!.GetValue<int>() == JsonNode.Parse(seed)!["age_limit_type"]!.GetValue<int>(), "new registration creates a distinct identity with initial state");
        File.WriteAllText(Path.Combine(deletionDirectory, "user.json"), "{\"_local_account_deleted\":true}");
        File.WriteAllText(Path.Combine(deletionDirectory, "account.json.tmp"), "unfinished identity");
        var interruptedDeletion = new OfflineServer(new JsonStateStore(deletionDirectory), seed, "offline://resources/");
        interruptedDeletion.Dispatch("game_init");
        Check(interruptedDeletion.Dispatch("check_user").Status == 401 && !File.Exists(Path.Combine(deletionDirectory, "account.json")) && !File.Exists(Path.Combine(deletionDirectory, "account.json.tmp")), "restart completes deletion committed before identity-file cleanup");
        Console.WriteLine("Passed 7 local account deletion checks.");
        var giftSeed = JsonNode.Parse(seed)!;
        giftSeed["_local_friends"] = new JsonArray(new JsonObject { ["public_id"] = "A", ["is_give_gold"] = true }, new JsonObject { ["public_id"] = "B", ["is_give_gold"] = true });
        var giftDirectory = directory + "-contact-gifts";
        var giftServer = new OfflineServer(new JsonStateStore(giftDirectory), giftSeed.ToJsonString(), "offline://resources/");
        giftServer.Dispatch("game_init");
        giftServer.Dispatch("login");
        Check(giftServer.Dispatch("friend/give", arguments: new(Value: 0)).Status == 400 && giftServer.Dispatch("friend/give", arguments: new(Value: 0, Text: "unknown")).Status == 404, "gift sending requires a valid local target");
        Check(giftServer.Dispatch("friend/give", arguments: new(Value: 0, Text: "A")).Status == 0 && !giftServer.Dispatch("friend/list").Body!["data"]![0]!["is_give_gold"]!.GetValue<bool>() && giftServer.Dispatch("friend/list").Body!["data"]![1]!["is_give_gold"]!.GetValue<bool>(), "single gift changes only the selected contact");
        giftServer.Dispatch("friend/give", arguments: new(Value: 0, Text: "A"));
        giftServer.Dispatch("friend/give", arguments: new(Value: 1));
        var giftSaved = JsonNode.Parse(File.ReadAllText(Path.Combine(giftDirectory, "user.json")))!;
        Check(giftSaved["_local_friend_gifts_sent"]!.AsArray().Count == 2 && giftSaved["gold"]!.GetValue<int>() == JsonNode.Parse(seed)!["gold"]!.GetValue<int>(), "batch gifts skip already sent targets and do not invent rewards");
        var giftReload = new OfflineServer(new JsonStateStore(giftDirectory), seed, "offline://resources/");
        giftReload.Dispatch("game_init");
        giftReload.Dispatch("login");
        giftReload.Dispatch("friend/give", arguments: new(Value: 1));
        Check(JsonNode.Parse(File.ReadAllText(Path.Combine(giftDirectory, "user.json")))!["_local_friend_gifts_sent"]!.AsArray().Count == 2, "gift flags and outgoing records survive restart without duplicate sends");
        Console.WriteLine("Passed 4 local gift checks.");
        var receiveGiftSeed = JsonNode.Parse(seed)!;
        receiveGiftSeed["_local_friends"] = new JsonArray(new JsonObject { ["public_id"] = "A", ["is_receive_gold"] = true }, new JsonObject { ["public_id"] = "B", ["is_receive_gold"] = true });
        receiveGiftSeed["_local_friend_gifts_inbox"] = new JsonArray(new JsonObject { ["public_id"] = "A", ["gold"] = 25 });
        var receiveGiftDirectory = directory + "-receive-gift";
        var receiveGiftServer = new OfflineServer(new JsonStateStore(receiveGiftDirectory), receiveGiftSeed.ToJsonString(), "offline://resources/");
        receiveGiftServer.Dispatch("game_init");
        receiveGiftServer.Dispatch("login");
        Check(receiveGiftServer.Dispatch("friend/receive", arguments: new(99999, 1)).Status == 503 && receiveGiftServer.Dispatch("login").Body!["user"]!["gold"]!.GetValue<long>() == 5000, "missing gift amount preserves the entire selected batch");
        Check(receiveGiftServer.Dispatch("friend/receive", arguments: new(5000, 0, Text: "A")).Status == 409, "gift capacity rejection preserves the receipt");
        var receivedGiftReply = receiveGiftServer.Dispatch("friend/receive", arguments: new(99999, 0, Text: "A")).Body!;
        Check(receivedGiftReply["user_update"]!["gold"]!.GetValue<int>() == 5025 && receivedGiftReply["user_list"]![0]!.GetValue<string>() == "A", "gift receipt credits the saved amount and reports its sender");
        var receiveGiftReload = new OfflineServer(new JsonStateStore(receiveGiftDirectory), seed, "offline://resources/");
        receiveGiftReload.Dispatch("game_init");
        receiveGiftReload.Dispatch("login");
        Check(receiveGiftReload.Dispatch("friend/receive", arguments: new(99999, 0, Text: "A")).Body!["user_list"]!.AsArray().Count == 0 && receiveGiftReload.Dispatch("login").Body!["user"]!["gold"]!.GetValue<long>() == 5025, "replayed gift receipt cannot credit the same amount after restart");
        receiveGiftSeed["_local_friend_gift_day"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
        receiveGiftSeed["_local_friend_gift_count"] = 29;
        receiveGiftSeed["_local_friend_gifts_inbox"]!.AsArray().Add(new JsonObject { ["public_id"] = "B", ["gold"] = 30 });
        var limitedGifts = new OfflineServer(new JsonStateStore(directory + "-limited-gifts"), receiveGiftSeed.ToJsonString(), "offline://resources/");
        limitedGifts.Dispatch("game_init");
        limitedGifts.Dispatch("login");
        Check(limitedGifts.Dispatch("friend/receive", arguments: new(99999, 1)).Body!["user_list"]!.AsArray().Count == 1 && limitedGifts.Dispatch("friend/receive", arguments: new(99999, 1)).Status == 20006 && limitedGifts.Dispatch("friend/list").Body!["data"]![1]!["is_receive_gold"]!.GetValue<bool>(), "daily limit leaves unclaimed gifts pending");
        Console.WriteLine("Passed 5 gift receipt checks.");
        var gatherSeed = JsonNode.Parse(seed)!;
        gatherSeed["_local_gathering_gold"] = 100;
        var gatherDirectory = directory + "-gather-gold";
        var gatherServer = new OfflineServer(new JsonStateStore(gatherDirectory), gatherSeed.ToJsonString(), "offline://resources/");
        gatherServer.Dispatch("game_init");
        gatherServer.Dispatch("login");
        Check(gatherServer.Dispatch("gather_gold", arguments: new(5050, 2)).Status == 503 && gatherServer.Dispatch("home").Body!["gathering_gold"]!.GetValue<int>() == 100, "gathering rejects stale level capacity without consuming stock");
        var gatherReply = gatherServer.Dispatch("gather_gold", arguments: new(5050, 1)).Body!;
        Check(gatherReply["gathering_gold"]!.GetValue<int>() == 50 && gatherReply["user_update"]!["gold"]!.GetValue<int>() == 5050 && gatherServer.Dispatch("home").Body!["gathering_gold"]!.GetValue<int>() == 50, "gathering fills available capacity and preserves excess stock");
        Check(gatherServer.Dispatch("gather_gold", arguments: new(5050, 1)).Body!["gathering_gold"]!.GetValue<int>() == 50, "full balance does not discard stock");
        var gatherReload = new OfflineServer(new JsonStateStore(gatherDirectory), seed, "offline://resources/");
        gatherReload.Dispatch("game_init");
        gatherReload.Dispatch("login");
        Check(gatherReload.Dispatch("gather_gold", arguments: new(9999, 1)).Body!["user_update"]!["gold"]!.GetValue<int>() == 5100 && gatherReload.Dispatch("gather_gold", arguments: new(9999, 1)).Body!["user_update"]!["gold"]!.GetValue<int>() == 5100, "saved stock survives restart and cannot be collected twice");
        Console.WriteLine("Passed 4 local gathering checks.");
        var gatherPointSeed = JsonNode.Parse(seed)!;
        gatherPointSeed["_local_gathering_phantasm_point"] = 50;
        gatherPointSeed["phantasm_point"]!["purchase_count"] = 7;
        var gatherPointDirectory = directory + "-gather-points";
        var gatherPointServer = new OfflineServer(new JsonStateStore(gatherPointDirectory), gatherPointSeed.ToJsonString(), "offline://resources/");
        gatherPointServer.Dispatch("game_init");
        gatherPointServer.Dispatch("login");
        Check(gatherPointServer.Dispatch("gather_phantasm_point", arguments: new(120, 2)).Status == 503, "point gathering requires current level capacity");
        var gatheredPoints = gatherPointServer.Dispatch("gather_phantasm_point", arguments: new(120, 1)).Body!;
        Check(gatheredPoints["gathering_phantasm_point"]!.GetValue<int>() == 30 && gatheredPoints["user_update"]!["phantasm_point"]!["points"]!.GetValue<int>() == 120 && gatheredPoints["user_update"]!["phantasm_point"]!["purchase_count"]!.GetValue<int>() == 7 && gatherPointServer.Dispatch("home").Body!["gathering_phantasm_point"]!.GetValue<int>() == 30, "point gathering preserves excess stock and unrelated balance fields");
        Check(gatherPointServer.Dispatch("gather_phantasm_point", arguments: new(120, 1)).Body!["gathering_phantasm_point"]!.GetValue<int>() == 30, "full point balance preserves remaining stock");
        var gatherPointReload = new OfflineServer(new JsonStateStore(gatherPointDirectory), seed, "offline://resources/");
        gatherPointReload.Dispatch("game_init");
        gatherPointReload.Dispatch("login");
        gatherPointReload.Dispatch("gather_phantasm_point", arguments: new(9999, 1));
        Check(gatherPointReload.Dispatch("gather_phantasm_point", arguments: new(9999, 1)).Body!["user_update"]!["phantasm_point"]!["points"]!.GetValue<int>() == 150, "point stock persists and repeated gathering does not duplicate credit");
        Console.WriteLine("Passed 4 point gathering checks.");
    }
}
