using System.Text.Json.Nodes;
using TouhouLastWorld;

internal sealed partial class RegressionSuite
{
    private void Scenario06()
    {
        var applyContactSeed = JsonNode.Parse(seed)!.AsObject();
        applyContactSeed["_local_friend_recommendations"] = new JsonArray(new JsonObject { ["public_id"] = "candidate", ["player_name"] = "Local" });
        applyContactSeed["_local_friends"] = new JsonArray(new JsonObject { ["public_id"] = "existing" });
        var applyContactDirectory = directory + "-apply-contact";
        var applyContact = new OfflineServer(new JsonStateStore(applyContactDirectory), applyContactSeed.ToJsonString(), "offline://resources/");
        applyContact.Dispatch("game_init");
        applyContact.Dispatch("login");
        Check(applyContact.Dispatch("friend/apply", arguments: new(Value: 1, Text: "candidate")).Status == 20007 && applyContact.Dispatch("friend/apply", arguments: new(Value: 2, Text: "existing")).Status == 409 && applyContact.Dispatch("friend/apply", arguments: new(Value: 2, Text: "missing")).Status == 404, "application checks local capacity, existing contacts and candidate presence");
        Check(applyContact.Dispatch("friend/recommend").Body!["data"]!.AsArray().Count == 1 && applyContact.Dispatch("friend/apply", arguments: new(Value: 2, Text: "candidate")).Status == 0, "application accepts a stored recommendation");
        var applyContactReload = new OfflineServer(new JsonStateStore(applyContactDirectory), applyContactSeed.ToJsonString(), "offline://resources/");
        applyContactReload.Dispatch("game_init");
        var appliedUser = applyContactReload.Dispatch("login").Body!["user"]!;
        Check(appliedUser["_local_friend_outgoing"]!.AsArray().Count == 1 && applyContactReload.Dispatch("friend/apply", arguments: new(Value: 2, Text: "candidate")).Status == 409, "application persists and rejects duplicate delivery after reload");
        Check(applyContactReload.Dispatch("friend/apply_list").Body!["data"]!.AsArray().Count == 0 && applyContactReload.Dispatch("friend/list").Body!["data"]!.AsArray().Count == 1, "outgoing application does not create an incoming request or accepted relation");
        Console.WriteLine("Passed 4 public contact application checks.");
        var episodeSeed = JsonNode.Parse(seed)!.AsObject();
        episodeSeed["quest_playdata"] = new JsonArray(new JsonObject { ["episode_id"] = 1, ["scenario_status"] = 0, ["normal"] = new JsonObject { ["status"] = 3, ["battle_challenge"] = new JsonArray(1, 2) } });
        var episodeDirectory = directory + "-episode";
        var episodeServer = new OfflineServer(new JsonStateStore(episodeDirectory), episodeSeed.ToJsonString(), "offline://resources/");
        episodeServer.Dispatch("game_init");
        episodeServer.Dispatch("login");
        Check(episodeServer.Dispatch("episode_end", arguments: new(EntityId: 0, Value: 1)).Status == 400 && episodeServer.Dispatch("episode_end", arguments: new(EntityId: 1)).Status == 503 && episodeServer.Dispatch("episode_end", arguments: new(EntityId: 1, Value: 2)).Status == 404, "episode completion validates ID and known story type");
        var completedEpisode = episodeServer.Dispatch("episode_end", arguments: new(EntityId: 1, Value: 1)).Body!["user_update"]!["quest_playdata"]![0]!;
        Check(completedEpisode["scenario_status"]!.GetValue<int>() == 1 && completedEpisode["normal"]!["status"]!.GetValue<int>() == 3 && completedEpisode["normal"]!["battle_challenge"]!.AsArray().Count == 2, "episode completion preserves existing battle progress");
        episodeServer.Dispatch("episode_end", arguments: new(EntityId: 2, Value: 1));
        episodeServer.Dispatch("episode_end", arguments: new(EntityId: 2, Value: 1));
        var episodeReload = new OfflineServer(new JsonStateStore(episodeDirectory), episodeSeed.ToJsonString(), "offline://resources/");
        episodeReload.Dispatch("game_init");
        var episodeUser = episodeReload.Dispatch("login").Body!["user"]!;
        Check(episodeUser["quest_playdata"]!.AsArray().Count == 2 && episodeUser["quest_playdata"]![1]!["scenario_status"]!.GetValue<int>() == 1 && episodeUser["quest_playdata"]![1]!["normal"]!["status"]!.GetValue<int>() == -2, "new episode completion persists once without clearing battles");
        Check(episodeUser["gold"]!.GetValue<int>() == episodeSeed["gold"]!.GetValue<int>(), "episode completion does not fabricate currency rewards");
        Console.WriteLine("Passed 4 episode completion checks.");
        var idleSeed = JsonNode.Parse(seed)!.AsObject();
        var idleUnit = idleSeed["unit"]!["unit_list"]![0]!;
        var idleUnitId = idleUnit["user_unit_id"]!.GetValue<long>();
        var idleDirectory = directory + "-idle";
        var idleServer = new OfflineServer(new JsonStateStore(idleDirectory), idleSeed.ToJsonString(), "offline://resources/");
        idleServer.Dispatch("game_init");
        idleServer.Dispatch("login");
        var idleRequest = new LocalIdleSet(idleUnitId, 9, 1, 4, 0, 2, 120, "rare", idleUnit["rare"]!.GetValue<int>(), true);
        Check(idleServer.Dispatch("afk_set", arguments: new(Idle: idleRequest with { Seconds = 0 })).Status == 503 && idleServer.Dispatch("afk_set", arguments: new(Idle: idleRequest with { Eligible = false })).Status == 409 && idleServer.Dispatch("afk_set", arguments: new(Idle: idleRequest with { Slot = 3 })).Status == 409, "idle start requires duration, eligibility and an unlocked slot");
        var idleStart = idleServer.Dispatch("afk_set", arguments: new(Idle: idleRequest));
        Check(idleStart.Status == 0 && (DateTime.Parse(idleStart.Body!["end_at"]!.GetValue<string>()) - DateTime.Parse(idleStart.Body["start_at"]!.GetValue<string>())).TotalSeconds == 120, "idle start returns the persisted duration");
        Check(idleServer.Dispatch("afk_set", arguments: new(Idle: idleRequest)).Status == 409 && idleServer.Dispatch("afk_set", arguments: new(Idle: idleRequest with { Slot = 2 })).Status == 409, "idle slots reject duplicate slot and object assignments");
        var idleReload = new OfflineServer(new JsonStateStore(idleDirectory), idleSeed.ToJsonString(), "offline://resources/");
        idleReload.Dispatch("game_init");
        var idleUser = idleReload.Dispatch("login").Body!["user"]!;
        Check(idleReload.Dispatch("afk_top").Body!["unit_slot_list"]!.AsArray().Count == 1 && idleUser["unit"]!["unit_list"]![0]!["rare"]!.GetValue<int>() == idleRequest.CurrentValue, "idle assignment survives reload without applying growth");
        Check(idleReload.Dispatch("afk_set", arguments: new(Idle: idleRequest with { Cancel = 1, Content = 2 })).Status == 409 && idleReload.Dispatch("afk_top").Body!["unit_slot_list"]!.AsArray().Count == 1, "mismatched idle cancellation preserves the slot");
        Check(idleReload.Dispatch("afk_set", arguments: new(Idle: idleRequest with { Cancel = 1, Seconds = 0, Capacity = 0 })).Status == 0 && idleReload.Dispatch("afk_top").Body!["unit_slot_list"]!.AsArray().Count == 0 && idleReload.Dispatch("afk_set", arguments: new(Idle: idleRequest with { Cancel = 1 })).Status == 404, "idle cancellation removes the stored slot without requiring growth definitions");
        Console.WriteLine("Passed 6 idle slot checks.");
        idleServer = new OfflineServer(new JsonStateStore(idleDirectory), idleSeed.ToJsonString(), "offline://resources/");
        idleServer.Dispatch("game_init");
        idleServer.Dispatch("login");
        idleServer.Dispatch("afk_set", arguments: new(Idle: idleRequest));
        Check(idleServer.Dispatch("afk_complete_upgrade", arguments: new(EntityId: 1, Value: 9, Idle: idleRequest)).Status == 409, "idle completion rejects tasks before their finish time");
        foreach (var (objectType, content, growthField) in new[]
        {
        (9, 4, "rare"),
        (9, 2, "destiny_value"),
        (10, 3, "potential_limitbreak"),
        (10, 7, "spec_actualize")
    }

        )
        {
            var completionSeed = JsonNode.Parse(seed)!.AsObject();
            var objectKey = objectType == 9 ? "unit" : "picture";
            completionSeed[objectKey]![objectKey + "_list"] = new JsonArray(new JsonObject { ["user_" + objectKey + "_id"] = 101L, [objectKey + "_id"] = 1, [growthField] = 1 });
            completionSeed["_local_idle"] = new JsonObject
            {
                [objectKey + "_slot_list"] = new JsonArray(new JsonObject { ["slot_number"] = 1, ["object_type"] = objectType, ["object_id"] = 1, ["user_object_id"] = 101L, ["content"] = content, ["start_at"] = "2000-01-01 00:00:00", ["end_at"] = "2000-01-01 00:01:00", ["_local_growth_field"] = growthField, ["_local_growth_value"] = 1 })
            };
            var completionDirectory = directory + "-idle-completion-" + content;
            var completionServer = new OfflineServer(new JsonStateStore(completionDirectory), completionSeed.ToJsonString(), "offline://resources/");
            completionServer.Dispatch("game_init");
            completionServer.Dispatch("login");
            var completion = new LocalIdleSet(101, objectType, 1, content, 0, GrowthField: growthField, CurrentValue: 1, Eligible: true);
            Check(completionServer.Dispatch("afk_complete_upgrade", arguments: new(EntityId: 1, Value: objectType, Idle: completion with { CurrentValue = 2 })).Status == 409, "idle completion checks the original growth snapshot");
            var completed = completionServer.Dispatch("afk_complete_upgrade", arguments: new(EntityId: 1, Value: objectType, Idle: completion));
            Check(completed.Status == 0 && completed.Body!["user_update"]![objectKey]![objectKey + "_list"]![0]![growthField]!.GetValue<int>() == 2 && completionServer.Dispatch("afk_complete_upgrade", arguments: new(EntityId: 1, Value: objectType, Idle: completion)).Status == 404, "idle completion grows once and removes the task");
            var completedReload = new OfflineServer(new JsonStateStore(completionDirectory), completionSeed.ToJsonString(), "offline://resources/");
            completedReload.Dispatch("game_init");
            var completedUser = completedReload.Dispatch("login").Body!["user"]!;
            Check(completedUser[objectKey]![objectKey + "_list"]![0]![growthField]!.GetValue<int>() == 2 && completedReload.Dispatch("afk_top").Body![objectKey + "_slot_list"]!.AsArray().Count == 0, "growth and task removal persist together");
        }

        Console.WriteLine("Passed 13 idle completion checks.");
        var emptyIdleDrop = idleReload.Dispatch("afk_receive_drop");
        Check(emptyIdleDrop.Status == 0 && emptyIdleDrop.Body!["drop_list"]!.AsArray().Count == 0 && emptyIdleDrop.Body["search_elapsed_time"]!.GetValue<int>() == 0 && emptyIdleDrop.Body["search_max_time"]!.GetValue<int>() > 0 && emptyIdleDrop.Body["user_update"] is null, "empty idle claim returns no inventory mutation");
        var pendingDropSeed = JsonNode.Parse(seed)!.AsObject();
        pendingDropSeed["_local_idle"] = new JsonObject
        {
            ["drop_list"] = new JsonArray(new JsonObject { ["object_type"] = 1, ["object_id"] = 0, ["object_value"] = 10 })
        };
        var pendingDrop = new OfflineServer(new JsonStateStore(directory + "-pending-idle-drop"), pendingDropSeed.ToJsonString(), "offline://resources/");
        pendingDrop.Dispatch("game_init");
        pendingDrop.Dispatch("login");
        Check(pendingDrop.Dispatch("afk_receive_drop").Status == 503 && pendingDrop.Dispatch("login").Body!["user"]!["_local_idle"]!["drop_list"]!.AsArray().Count == 1, "unsupported idle rewards remain pending instead of being silently cleared");
        Console.WriteLine("Passed 2 idle drop boundary checks.");
        var unavailableHelper = idleReload.Dispatch("friend_get_helper_detail").Body!;
        Check(unavailableHelper["helper_unit_info"]!["unit_info"]!["unit_id"]!.GetValue<int>() == 0 && unavailableHelper["user_update"] is null, "missing helper has an explicit unavailable unit rather than null metadata");
        unavailableHelper["helper_unit_info"]!["unit_info"]!["unit_id"] = 1;
        Check(idleReload.Dispatch("friend_get_helper_detail").Body!["helper_unit_info"]!["unit_info"]!["unit_id"]!.GetValue<int>() == 0, "helper response mutation cannot alter subsequent queries");
        Console.WriteLine("Passed 2 unavailable helper response checks.");
        var clearedTeamSeed = JsonNode.Parse(seed)!.AsObject();
        clearedTeamSeed["_local_quest_cleared_teams"] = new JsonArray(new JsonObject { ["quest_type"] = 2, ["target_id"] = 10, ["difficulty"] = 1, ["quest_cleared_team"] = "Saved,1,2,0,0,0,0,", ["use_cost"] = 12, ["season_enhancement_info"] = new JsonArray(0L, 2L, 0L, 0L, 0L, 0L) });
        var clearedTeamServer = new OfflineServer(new JsonStateStore(directory + "-cleared-team"), clearedTeamSeed.ToJsonString(), "offline://resources/");
        clearedTeamServer.Dispatch("game_init");
        clearedTeamServer.Dispatch("login");
        var clearQuery = new RequestDraft(ReplayMeasure: new(2, 10, 1));
        var clearReply = clearedTeamServer.Dispatch("get_quest_cleared_team_info", arguments: clearQuery).Body!;
        Check(clearReply["quest_cleared_team"]!.GetValue<string>() == "Saved,1,2,0,0,0,0," && clearReply["use_cost"]!.GetValue<int>() == 12 && clearReply["season_enhancement_info"]![1]!.GetValue<long>() == 2, "cleared team query preserves stored encoding, cost and enhancement slots");
        Check(clearedTeamServer.Dispatch("get_quest_cleared_team_info", arguments: new(ReplayMeasure: new(2, 10, 0))).Body!["quest_cleared_team"]!.GetValue<string>() == "" && clearedTeamServer.Dispatch("get_quest_cleared_team_info", arguments: new(ReplayMeasure: new(0, 10, 1))).Body!["season_enhancement_info"]!.AsArray().Count == 6, "cleared team queries isolate difficulty and type with a six-slot empty fallback");
        Check(clearedTeamServer.Dispatch("get_quest_cleared_team_info", arguments: new(ReplayMeasure: new(3, 10, 0))).Status == 400, "cleared team query rejects unknown types");
        Console.WriteLine("Passed 3 cleared team query checks.");
        var helperListSeed = JsonNode.Parse(seed)!.AsObject();
        helperListSeed["_local_friend_players"] = new JsonArray(new JsonObject { ["player_id"] = "contact" });
        helperListSeed["_local_friend_requests"] = new JsonArray(new JsonObject { ["player_id"] = "pending" });
        var helperListServer = new OfflineServer(new JsonStateStore(directory + "-helper-list"), helperListSeed.ToJsonString(), "offline://resources/");
        helperListServer.Dispatch("game_init");
        helperListServer.Dispatch("login");
        var helperList = helperListServer.Dispatch("friend_helper_select", arguments: new(Value: 1)).Body!;
        Check(helperList["helper_list"]!.AsArray().Count == 0 && helperList["is_friend_max"]!.GetValue<int>() == 1 && helperList["friend_request_count"]!.GetValue<int>() == 1 && helperList["user_update"] is null, "helper listing reports local capacity and outgoing count without producing candidates or rewards");
        Check(helperListServer.Dispatch("friend_helper_select", arguments: new(Value: 2)).Body!["is_friend_max"]!.GetValue<int>() == 0 && helperListServer.Dispatch("friend_helper_select").Status == 503, "helper listing uses a known capacity definition");
        Console.WriteLine("Passed 2 helper list checks.");
        var receiptSeed = JsonNode.Parse(seed)!.AsObject();
        receiptSeed["_local_mail"] = new JsonArray(MailFixture(1, 3, 30), MailFixture(2, 6, 2), MailFixture(3, 12, 5, 99999), MailFixture(4, 9, 1), MailFixture(5, 3, 1, grace: 1));
        var receiptDirectory = directory + "-mail-receive";
        var mailServer = new OfflineServer(new JsonStateStore(receiptDirectory), receiptSeed.ToJsonString(), "offline://resources/");
        mailServer.Dispatch("game_init");
        mailServer.Dispatch("login");
        var mailRules = new LocalMailRules(1000000, 1000000, 999, [99999]);
        Check(mailServer.Dispatch("mail_receive", arguments: new(Ids: [1, 4], MailRules: mailRules)).Status == 503 && mailServer.Dispatch("login").Body!["user"]!["gold"]!.GetValue<int>() == receiptSeed["gold"]!.GetValue<int>() && mailServer.Dispatch("mail_top").Body!["mail"]![0]!["open_type"]!.GetValue<int>() == 0, "unsupported mixed rewards preserve all mail and currency");
        Check(mailServer.Dispatch("mail_receive", arguments: new(Ids: [1], MailRules: mailRules with { GoldMax = 1 })).Status == 409 && mailServer.Dispatch("mail_receive", arguments: new(Ids: [5], MailRules: mailRules)).Status == 503 && mailServer.Dispatch("mail_receive", arguments: new(Ids: [1, 1], MailRules: mailRules)).Status == 400, "mail claims reject capacity overflow, unsupported expiry and duplicate IDs");
        var receivedMail = mailServer.Dispatch("mail_receive", arguments: new(Ids: [1, 2, 3], MailRules: mailRules)).Body!;
        Check(receivedMail["user_update"]!["gold"]!.GetValue<int>() == receiptSeed["gold"]!.GetValue<int>() + 30 && receivedMail["user_update"]!["crystal"]!.GetValue<int>() == receiptSeed["crystal"]!.GetValue<int>() + 2 && receivedMail["user_update"]!["item"]!["item_list"]!.AsArray().Single(entry => entry!["item_id"]!.GetValue<int>() == 99999)!["num"]!.GetValue<int>() == 5 && receivedMail["unread_mail_num"]!.GetValue<int>() == 2, "mail claims apply supported inventory and receipt states together");
        var receiptReload = new OfflineServer(new JsonStateStore(receiptDirectory), receiptSeed.ToJsonString(), "offline://resources/");
        receiptReload.Dispatch("game_init");
        receiptReload.Dispatch("login");
        var repeatedMail = receiptReload.Dispatch("mail_receive", arguments: new(Ids: [1, 2, 3])).Body!;
        Check(repeatedMail["user_update"]!["gold"]!.GetValue<int>() == receivedMail["user_update"]!["gold"]!.GetValue<int>() && repeatedMail["mail"]![0]!["open_type"]!.GetValue<int>() == 1, "reloaded mail receipts prevent duplicate rewards without requiring definitions again");
        Check(receiptReload.Dispatch("mail_receive", arguments: new(Ids: [99], MailRules: mailRules)).Status == 404 && receiptReload.Dispatch("mail_receive", arguments: new(Ids: [])).Status == 0, "mail claims distinguish missing IDs and an empty selection");
        Console.WriteLine("Passed 5 mail receipt checks.");
    }
}
