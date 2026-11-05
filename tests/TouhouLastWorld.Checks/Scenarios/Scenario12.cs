using System.Text.Json.Nodes;
using TouhouLastWorld;

internal sealed partial class RegressionSuite
{
    private void Scenario12()
    {
        var pointRewardSeed = JsonNode.Parse(seed)!;
        pointRewardSeed["_local_event_overview"] = new JsonObject
        {
            ["event_point_gacha_list"] = new JsonArray(),
            ["event_special_mission"] = new JsonArray(),
            ["event_total_point_reward"] = new JsonArray(new JsonObject { ["event_id"] = 7, ["total_point"] = 100, ["step_number"] = 1, ["next_reward_group_id"] = 11 })
        };
        var pointRewardDirectory = directory + "-point-reward";
        var pointRewardServer = new OfflineServer(new JsonStateStore(pointRewardDirectory), pointRewardSeed.ToJsonString(), "offline://resources/");
        pointRewardServer.Dispatch("game_init");
        pointRewardServer.Dispatch("login");
        var pointRewardRule = new LocalEventRewards(7, new[] { new LocalEventRewardGroup(11, 1, 100, 0, new[] { new LocalInventoryReward(3, 0, 100, 100000) }), new LocalEventRewardGroup(12, 2, 200, 0, new[] { new LocalInventoryReward(6, 0, 1, 100000) }) });
        Check(pointRewardServer.Dispatch("get_event_total_point_reward", arguments: new(Value: 7, EventRewards: pointRewardRule with { Groups = new[] { pointRewardRule.Groups[0] with { Rewards = new[] { new LocalInventoryReward(3, 0, 100, 1) } } } })).Status == 409, "point reward preserves progress when inventory capacity is insufficient");
        var pointRewardReply = pointRewardServer.Dispatch("get_event_total_point_reward", arguments: new(Value: 7, EventRewards: pointRewardRule)).Body!;
        Check(pointRewardReply["user_update"]!["gold"]!.GetValue<int>() == 5100 && pointRewardReply["next_reward_group_id"]!.GetValue<int>() == 12 && pointRewardReply["step_number"]!.GetValue<int>() == 2 && pointRewardReply["total_point"]!.GetValue<int>() == 100 && pointRewardReply["reward"]!["object_value"]!.GetValue<int>() == 100, "point reward grants the current fixed reward and advances without spending accumulated points");
        var pointRewardReload = new OfflineServer(new JsonStateStore(pointRewardDirectory), seed, "offline://resources/");
        pointRewardReload.Dispatch("game_init");
        pointRewardReload.Dispatch("login");
        Check(pointRewardReload.Dispatch("get_event_total_point_reward", arguments: new(Value: 7, EventRewards: pointRewardRule)).Status == 409 && pointRewardReload.Dispatch("login").Body!["user"]!["gold"]!.GetValue<int>() == 5100, "point reward progress survives restart and enforces the next threshold");
        Console.WriteLine("Passed 3 accumulated point reward checks.");
        pointRewardSeed["_local_event_overview"]!["event_total_point_reward"]![0]!["total_point"] = 250;
        var pointBatchDirectory = directory + "-point-batch";
        var pointBatchServer = new OfflineServer(new JsonStateStore(pointBatchDirectory), pointRewardSeed.ToJsonString(), "offline://resources/");
        pointBatchServer.Dispatch("game_init");
        pointBatchServer.Dispatch("login");
        var pointBatchFailure = pointRewardRule with
        {
            Groups = new[]
            {
            pointRewardRule.Groups[0],
            pointRewardRule.Groups[1] with
            {
                IconType = 1
            }
        }
        };
        Check(pointBatchServer.Dispatch("get_event_total_point_reward_all", arguments: new(Value: 7, EventRewards: pointBatchFailure)).Status == 503 && pointBatchServer.Dispatch("login").Body!["user"]!["gold"]!.GetValue<int>() == 5000, "point reward batch rolls back earlier rewards when a later group is unsupported");
        var pointBatchReply = pointBatchServer.Dispatch("get_event_total_point_reward_all", arguments: new(Value: 7, EventRewards: pointRewardRule)).Body!;
        Check(pointBatchReply["reward_list"]!.AsArray().Count == 2 && pointBatchReply["next_reward_group_id"]!.GetValue<int>() == 0 && pointBatchReply["step_number"]!.GetValue<int>() == 2 && pointBatchReply["user_update"]!["gold"]!.GetValue<int>() == 5100, "point reward batch grants eligible groups and marks the last group complete");
        var pointBatchReload = new OfflineServer(new JsonStateStore(pointBatchDirectory), seed, "offline://resources/");
        pointBatchReload.Dispatch("game_init");
        pointBatchReload.Dispatch("login");
        Check(pointBatchReload.Dispatch("get_event_total_point_reward_all", arguments: new(Value: 7, EventRewards: pointRewardRule)).Status == 409, "completed point reward batches cannot grant again after restart");
        Console.WriteLine("Passed 3 accumulated reward batch checks.");
        var treasureSeed = JsonNode.Parse(seed)!;
        treasureSeed["_local_treasure"] = new JsonObject
        {
            ["event_id"] = 99,
            ["fd_status"] = 1,
            ["info"] = new JsonObject
            {
                ["gift_id"] = 3,
                ["code"] = "local-test"
            }
        };
        var treasureServer = new OfflineServer(new JsonStateStore(directory + "-treasure"), treasureSeed.ToJsonString(), "offline://resources/");
        treasureServer.Dispatch("game_init");
        treasureServer.Dispatch("login");
        Check(treasureServer.Dispatch("fudai/fudai_info").Status == 503 && treasureServer.Dispatch("fudai/fudai_info", arguments: new(Value: 98)).Status == 503, "treasure overview requires a matching event definition");
        var treasureReply = treasureServer.Dispatch("fudai/fudai_info", arguments: new(Value: 99)).Body!;
        Check(treasureReply["fd_status"]!.GetValue<int>() == 1 && treasureReply["info"]!["gift_id"]!.GetValue<int>() == 3 && treasureReply["info"]!["code"]!.GetValue<string>() == "local-test", "treasure overview returns the saved reward without generating another");
        treasureReply["info"]!["code"] = "changed";
        Check(treasureServer.Dispatch("fudai/fudai_info", arguments: new(Value: 99)).Body!["info"]!["code"]!.GetValue<string>() == "local-test", "treasure overview returns an independent snapshot");
        Console.WriteLine("Passed 3 treasure overview checks.");
        var ticketSeed = JsonNode.Parse(seed)!;
        ticketSeed["item"]!["item_list"]!.AsArray().Add(new JsonObject { ["item_id"] = 99991, ["num"] = 5 });
        var ticketDirectory = directory + "-ticket-exchange";
        var ticketServer = new OfflineServer(new JsonStateStore(ticketDirectory), ticketSeed.ToJsonString(), "offline://resources/");
        ticketServer.Dispatch("game_init");
        ticketServer.Dispatch("login");
        var ticketRule = new LocalTicketExchange(2, 1, 99991, 2, 2, 100, 10, true, 0, 0, 0);
        Check(ticketServer.Dispatch("votive_picture_exchange", arguments: new(TicketExchange: ticketRule with { Active = false })).Status == 409 && ticketServer.Dispatch("votive_picture_exchange", arguments: new(TicketExchange: ticketRule with { Capacity = 1 })).Status == 409 && ticketServer.Dispatch("votive_picture_exchange", arguments: new(TicketExchange: ticketRule with { Count = 3 })).Status == 409, "ticket exchange enforces activity, capacity and sufficient materials");
        var ticketReply = ticketServer.Dispatch("votive_picture_exchange", arguments: new(TicketExchange: ticketRule)).Body!;
        var ticketPictures = ticketReply["user_update"]!["picture"]!["picture_list"]!.AsArray();
        Check(ticketPictures.Count == 2 && ticketPictures.Select(row => row!["user_picture_id"]!.GetValue<long>()).Distinct().Count() == 2 && ticketReply["user_update"]!["item"]!["item_list"]![0]!["num"]!.GetValue<int>() == 1 && ticketReply["user_update"]!["album"]!["picture_list"]!.AsArray().Count == 1 && ticketReply["reward_object"]!["unique_id"]!.GetValue<long>() == ticketPictures[0]!["user_picture_id"]!.GetValue<long>(), "ticket exchange creates distinct equipment, consumes materials and records the album entry");
        var ticketReload = new OfflineServer(new JsonStateStore(ticketDirectory), seed, "offline://resources/");
        ticketReload.Dispatch("game_init");
        ticketReload.Dispatch("login");
        Check(ticketReload.Dispatch("votive_picture_exchange", arguments: new(TicketExchange: ticketRule with { Count = 1 })).Status == 409 && ticketReload.Dispatch("login").Body!["user"]!["picture"]!["picture_list"]!.AsArray().Count == 2, "ticket exchange inventory and remaining materials survive restart");
        var partnerTicketSeed = JsonNode.Parse(seed)!;
        partnerTicketSeed["item"]!["item_list"]!.AsArray().Add(new JsonObject { ["item_id"] = 100001, ["num"] = 2 });
        var partnerTicketDirectory = directory + "-partner-ticket-exchange";
        var partnerTicketServer = new OfflineServer(new JsonStateStore(partnerTicketDirectory), partnerTicketSeed.ToJsonString(), "offline://resources/");
        partnerTicketServer.Dispatch("game_init");
        partnerTicketServer.Dispatch("login");
        var partnerTicket = new LocalTicketExchange(1001002, 1001, 100001, 1, 1, 1002, 10, true, ObjectType: 9, CostumeId: 100201);
        Check(partnerTicketServer.Dispatch("votive_picture_exchange", arguments: new(TicketExchange: partnerTicket with { Count = 2 })).Status == 400, "partner ticket exchanges accept only one selection");
        var partnerReply = partnerTicketServer.Dispatch("votive_picture_exchange", arguments: new(TicketExchange: partnerTicket)).Body!;
        var partnerId = partnerReply["reward_object"]!["unique_id"]!.GetValue<long>();
        var partner = partnerReply["user_update"]!["unit"]!["unit_list"]!.AsArray().Single(row => row!["user_unit_id"]!.GetValue<long>() == partnerId)!;
        Check(partner["unit_id"]!.GetValue<int>() == 1002 && partner["equip_costume_id"]!.GetValue<int>() == 100201 && partner["level"]!.GetValue<int>() == 1 && partnerReply["reward_object"]!["is_new"]!.GetValue<int>() == 1, "partner ticket grants the selected partner with its default costume");
        var partnerTicketReload = new OfflineServer(new JsonStateStore(partnerTicketDirectory), seed, "offline://resources/");
        partnerTicketReload.Dispatch("game_init");
        partnerTicketReload.Dispatch("login");
        var duplicatePartner = partnerTicketReload.Dispatch("votive_picture_exchange", arguments: new(TicketExchange: partnerTicket)).Body!;
        Check(duplicatePartner["reward_object"]!["is_new"]!.GetValue<int>() == 0 && duplicatePartner["reward_object"]!["unique_id"]!.GetValue<long>() > partnerId && duplicatePartner["user_update"]!["album"]!["unit_list"]!.AsArray().Count(row => row!["unit_id"]!.GetValue<int>() == 1002) == 1 && partnerTicketReload.Dispatch("votive_picture_exchange", arguments: new(TicketExchange: partnerTicket)).Status == 409, "partner tickets are spent once per grant and duplicate partners share an album entry across restart");
        Console.WriteLine("Passed 6 ticket exchange checks.");
        var releaseSeed = JsonNode.Parse(seed)!;
        var releaseUnit = releaseSeed["unit"]!["unit_list"]![0]!;
        var releaseUnitId = releaseUnit["user_unit_id"]!.GetValue<long>();
        releaseUnit["release_functions"] = new JsonArray(JsonValue.Create(1));
        releaseUnit["level"] = 50;
        releaseUnit["reincarnation_stage"] = 1;
        var releaseDirectory = directory + "-automatic-release";
        var releaseServer = new OfflineServer(new JsonStateStore(releaseDirectory), releaseSeed.ToJsonString(), "offline://resources/");
        releaseServer.Dispatch("game_init");
        releaseServer.Dispatch("login");
        var releaseRule = new LocalAutomaticRelease(2, releaseUnit["unit_id"]!.GetValue<int>(), 50, 1, true, true);
        Check(releaseServer.Dispatch("unit_reincarnation_release", arguments: new(releaseUnitId, 2, AutomaticRelease: releaseRule with { Level = 51 })).Status == 409 && releaseServer.Dispatch("unit_reincarnation_release", arguments: new(releaseUnitId, 2, AutomaticRelease: releaseRule with { FullStats = false })).Status == 409, "automatic release checks level and full-stat requirements");
        var releasedReply = releaseServer.Dispatch("unit_reincarnation_release", arguments: new(releaseUnitId, 2, AutomaticRelease: releaseRule));
        Check(releasedReply.Status == 0 && releasedReply.Body!["user_update"]!["unit"]!["unit_list"]![0]!["release_functions"]!.AsArray().Count == 2, "automatic release adds the verified node");
        var releaseReload = new OfflineServer(new JsonStateStore(releaseDirectory), seed, "offline://resources/");
        releaseReload.Dispatch("game_init");
        releaseReload.Dispatch("login");
        Check(releaseReload.Dispatch("unit_reincarnation_release", arguments: new(releaseUnitId, 2, AutomaticRelease: releaseRule)).Body!["user_update"]!["unit"]!["unit_list"]![0]!["release_functions"]!.AsArray().Count == 2, "automatic release persists and duplicate requests are idempotent");
        Console.WriteLine("Passed 3 automatic release checks.");
        var refreshSeed = JsonNode.Parse(seed)!;
        refreshSeed["orb"]!["free"] = 5L;
        refreshSeed["orb"]!["purchased"] = 10L;
        refreshSeed["_local_shop_list"] = new JsonArray(new JsonObject { ["type"] = 3, ["manual_update_count"] = 0, ["goods_list"] = new JsonArray() });
        refreshSeed["_local_gold_shop_batches"] = new JsonArray(new JsonArray(new JsonObject { ["master_id"] = 10, ["shop_item_id"] = 100, ["stock"] = 2, ["end_at"] = "" }));
        var refreshDirectory = directory + "-shop-refresh";
        var refreshServer = new OfflineServer(new JsonStateStore(refreshDirectory), refreshSeed.ToJsonString(), "offline://resources/");
        refreshServer.Dispatch("game_init");
        refreshServer.Dispatch("login");
        var refreshRule = new LocalShopRefresh(new[] { 10 }, new[] { 10 });
        Check(refreshServer.Dispatch("update_gold_exchange_shop_list", arguments: new(ShopRefresh: refreshRule with { ActiveGoods = Array.Empty<int>() })).Status == 503 && refreshServer.Dispatch("update_gold_exchange_shop_list", arguments: new(ShopRefresh: refreshRule with { Costs = new[] { 20 } })).Status == 409, "shop refresh preserves its batch when goods are unavailable or balance is insufficient");
        var refreshReply = refreshServer.Dispatch("update_gold_exchange_shop_list", arguments: new(ShopRefresh: refreshRule)).Body!;
        Check(refreshReply["exchange_shop"]!["manual_update_count"]!.GetValue<int>() == 1 && refreshReply["exchange_shop"]!["goods_list"]![0]!["master_id"]!.GetValue<int>() == 10 && refreshReply["user_update"]!["orb"]!["free"]!.GetValue<long>() == 0 && refreshReply["user_update"]!["orb"]!["purchased"]!.GetValue<long>() == 5, "shop refresh replaces goods, charges the configured price and increments its count");
        var refreshReload = new OfflineServer(new JsonStateStore(refreshDirectory), seed, "offline://resources/");
        refreshReload.Dispatch("game_init");
        refreshReload.Dispatch("login");
        Check(refreshReload.Dispatch("update_gold_exchange_shop_list", arguments: new(ShopRefresh: refreshRule)).Status == 409 && refreshReload.Dispatch("get_shop_list").Body!["shop_list"]![0]!["goods_list"]![0]!["shop_item_id"]!.GetValue<int>() == 100, "refreshed goods and the refresh limit survive restart");
        Console.WriteLine("Passed 3 shop refresh checks.");
        var goldExchangeRule = new LocalGoldExchange(100, 10, 2, 100, true, new(12, 99990, 3, 100));
        Check(refreshReload.Dispatch("exchange", arguments: new(GoldExchange: goldExchangeRule with { Active = false })).Status == 1005 && refreshReload.Dispatch("exchange", arguments: new(GoldExchange: goldExchangeRule with { Cost = 10000 })).Status == 409 && refreshReload.Dispatch("exchange", arguments: new(GoldExchange: goldExchangeRule with { Reward = new(12, 99990, 3, 5) })).Status == 409, "gold exchange preserves gold and stock when closed, unaffordable or over capacity");
        var goldExchangeReply = refreshReload.Dispatch("exchange", arguments: new(GoldExchange: goldExchangeRule)).Body!;
        Check(goldExchangeReply["update_shop"]!["goods_list"]![0]!["stock"]!.GetValue<int>() == 0 && goldExchangeReply["user_update"]!["gold"]!.GetValue<int>() == 4800 && goldExchangeReply["user_update"]!["item"]!["item_list"]!.AsArray().Single(item => item!["item_id"]!.GetValue<int>() == 99990)!["num"]!.GetValue<int>() == 6, "gold exchange charges per bundle and grants its configured quantity");
        var goldExchangeReload = new OfflineServer(new JsonStateStore(refreshDirectory), seed, "offline://resources/");
        goldExchangeReload.Dispatch("game_init");
        goldExchangeReload.Dispatch("login");
        Check(goldExchangeReload.Dispatch("exchange", arguments: new(GoldExchange: goldExchangeRule with { Count = 1 })).Status == 409 && goldExchangeReload.Dispatch("login").Body!["user"]!["gold"]!.GetValue<int>() == 4800, "gold exchange stock and balance survive restart");
        Console.WriteLine("Passed 3 gold exchange checks.");
        introSeed = JsonNode.Parse(seed)!;
        var introUnitId = introSeed["unit"]!["unit_list"]![0]!["unit_id"]!.GetValue<int>();
        introSeed["_local_first_draw_candidate"] = new JsonObject
        {
            ["unit_ids"] = new JsonArray(new JsonObject { ["unit_id"] = introUnitId, ["voice_set_id"] = 1, ["voice_set_file_id"] = 1 }),
            ["picture_ids"] = new JsonArray()
        };
        introDirectory = directory + "-intro";
        var introServer = new OfflineServer(new JsonStateStore(introDirectory), introSeed.ToJsonString(), "offline://resources/");
        introServer.Dispatch("game_init");
        var introLogin = introServer.Dispatch("login").Body!;
        introPublicId = introLogin["public_id"]!.GetValue<string>();
        introRules = new LocalIntroDefinitions(new[] { introUnitId }, Array.Empty<int>());
        Check(introServer.Dispatch("first_draw_data", arguments: new(Text: "invalid", IntroDefinitions: introRules)).Status == 403 && introServer.Dispatch("first_draw_data", arguments: new(Text: introPublicId, IntroDefinitions: introRules with { UnitIds = Array.Empty<int>() })).Status == 503, "initial draw requires the local identity and known definitions");
        var introReply = introServer.Dispatch("first_draw_data", arguments: new(Text: introPublicId, IntroDefinitions: introRules)).Body!;
        Check(introReply["unit_ids"]!.AsArray().Count == 1 && introReply["picture_ids"]!.AsArray().Count == 0 && introServer.Dispatch("login").Body!["user"]!["unit"]!["unit_list"]!.AsArray().Count == introLogin["user"]!["unit"]!["unit_list"]!.AsArray().Count, "initial draw stages a result without granting inventory");
        introReload = new OfflineServer(new JsonStateStore(introDirectory), seed, "offline://resources/");
        introReload.Dispatch("game_init");
        introReload.Dispatch("login");
        Check(introReload.Dispatch("first_draw_data", arguments: new(Text: introPublicId, IntroDefinitions: introRules)).Body!["unit_ids"]![0]!["unit_id"]!.GetValue<int>() == introUnitId, "initial draw pending result survives restart");
        Console.WriteLine("Passed 3 initial draw checks.");
    }
}
