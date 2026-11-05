using System.Text.Json.Nodes;
using TouhouLastWorld;

internal sealed partial class RegressionSuite
{
    private void Scenario11()
    {
        var bookSeed = JsonNode.Parse(seed)!;
        bookSeed["_local_picture_book"] = new JsonArray(new JsonObject { ["pb_id"] = 1, ["complete_ls"] = new JsonArray(JsonValue.Create(11)), ["reward"] = new JsonArray() });
        var bookDirectory = directory + "-book-reward";
        var bookServer = new OfflineServer(new JsonStateStore(bookDirectory), bookSeed.ToJsonString(), "offline://resources/");
        bookServer.Dispatch("game_init");
        bookServer.Dispatch("login");
        var bookRule = new LocalBookReward(11, 1, new(12, 99991, 3, 100));
        Check(bookServer.Dispatch("picture_book/get_reward", arguments: new(Value: 12, BookReward: bookRule with { TaskId = 12 })).Status == 409 && bookServer.Dispatch("picture_book/get_reward", arguments: new(Value: 11, BookReward: bookRule with { Reward = new(12, 99991, 3, 2) })).Status == 409, "book reward checks completion and capacity without consuming the task");
        Check(bookServer.Dispatch("picture_book/get_reward", arguments: new(Value: 11, BookReward: bookRule)).Body!["user_update"]!["item"]!["item_list"]!.AsArray().Single(item => item!["item_id"]!.GetValue<int>() == 99991)!["num"]!.GetValue<int>() == 3, "book reward grants the configured item");
        var bookReload = new OfflineServer(new JsonStateStore(bookDirectory), seed, "offline://resources/");
        bookReload.Dispatch("game_init");
        bookReload.Dispatch("login");
        var bookProgress = bookReload.Dispatch("picture_book/get_user_picture_book_list").Body!["user_completed_info"]![0]!;
        Check(bookProgress["complete_ls"]!.AsArray().Count == 0 && bookProgress["reward"]![0]!.GetValue<int>() == 11 && bookReload.Dispatch("picture_book/get_reward", arguments: new(Value: 11, BookReward: bookRule)).Status == 409, "book reward persists the received marker and prevents duplicate rewards");
        Console.WriteLine("Passed 3 book reward checks.");
        var codeSeed = JsonNode.Parse(seed)!;
        var localCodeHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("sample-local-code")));
        codeSeed["_local_exchange_codes"] = new JsonObject
        {
            [localCodeHash] = new JsonObject
            {
                ["received"] = false,
                ["group"] = "sample",
                ["rewards"] = new JsonArray(new JsonObject { ["object_type"] = 3, ["object_id"] = 0, ["object_value"] = 100 })
            }
        };
        var codeDirectory = directory + "-exchange-code";
        var codeServer = new OfflineServer(new JsonStateStore(codeDirectory), codeSeed.ToJsonString(), "offline://resources/");
        codeServer.Dispatch("game_init");
        var codeLogin = codeServer.Dispatch("login").Body!;
        var localCodeRequest = new LocalCodeExchange("sample-local-code", codeLogin["user_id"]!.GetValue<string>(), codeLogin["device_id"]!.GetValue<string>());
        var localCodeArgs = new RequestDraft(CodeExchange: localCodeRequest, MailRules: new(100000, 100000, 1000, Array.Empty<int>()));
        Check(codeServer.Dispatch("get_exchange_code_reward", arguments: localCodeArgs with { CodeExchange = localCodeRequest with { Code = "unknown" } }).Body!["code"]!.GetValue<int>() == 1002 && codeServer.Dispatch("get_exchange_code_reward", arguments: localCodeArgs with { CodeExchange = localCodeRequest with { UserId = "other" } }).Body!["status"]!.GetValue<bool>() == false, "code redemption rejects unknown codes and mismatched identity through the native failure fields");
        Check(codeServer.Dispatch("get_exchange_code_reward", arguments: localCodeArgs with { MailRules = new(1, 1, 1, Array.Empty<int>()) }).Body!["code"]!.GetValue<int>() == 1000, "code redemption failure does not consume the code");
        var redeemedCode = codeServer.Dispatch("get_exchange_code_reward", arguments: localCodeArgs).Body!;
        Check(redeemedCode["status"]!.GetValue<bool>() && redeemedCode["code"]!.GetValue<int>() == 0 && redeemedCode["user_update"]!["gold"]!.GetValue<int>() == 5100, "code redemption grants the configured reward and reports success");
        var codeReload = new OfflineServer(new JsonStateStore(codeDirectory), seed, "offline://resources/");
        codeReload.Dispatch("game_init");
        codeReload.Dispatch("login");
        Check(codeReload.Dispatch("get_exchange_code_reward", arguments: localCodeArgs).Body!["code"]!.GetValue<int>() == 1001 && codeReload.Dispatch("login").Body!["user"]!["gold"]!.GetValue<int>() == 5100, "code redemption persists and cannot grant twice");
        Console.WriteLine("Passed 4 local code redemption checks.");
        var passSeed = JsonNode.Parse(seed)!;
        passSeed["_local_pass_card"] = new JsonObject
        {
            ["left_day"] = 10,
            ["level"] = 1,
            ["current_exp"] = 140,
            ["is_vip"] = 0,
            ["had_got_free_reward_level"] = new JsonArray(JsonValue.Create(1)),
            ["had_got_vip_reward_level"] = new JsonArray(),
            ["free_rewards"] = new JsonArray(),
            ["vip_rewards"] = new JsonArray(),
            ["tasks"] = new JsonArray(new JsonObject { ["task_id"] = 4, ["can_cellable"] = 1, ["completed_times"] = 1 })
        };
        var passDirectory = directory + "-pass-exp";
        var passServer = new OfflineServer(new JsonStateStore(passDirectory), passSeed.ToJsonString(), "offline://resources/");
        passServer.Dispatch("game_init");
        passServer.Dispatch("login");
        var passTaskRule = new LocalPassTask(4, 20, new[] { 1, 2, 3 }, new[] { 1, 2, 3 });
        Check(passServer.Dispatch("pass_card/collect_exp", arguments: new(Value: 5, PassTask: passTaskRule with { Id = 5 })).Status == 409, "pass experience cannot be collected without a completed task");
        var passCollected = passServer.Dispatch("pass_card/collect_exp", arguments: new(Value: 4, PassTask: passTaskRule)).Body!["user_pass_card"]!;
        Check(passCollected["level"]!.GetValue<int>() == 2 && passCollected["current_exp"]!.GetValue<int>() == 10 && passCollected["tasks"]![0]!["can_cellable"]!.GetValue<int>() == 0 && passCollected["is_reward"]!.GetValue<int>() == 1 && passCollected["free_rewards"]!.AsArray().Count == 0 && passCollected["vip_rewards"]!.AsArray().Count == 0, "pass experience upgrades levels, consumes one claim and updates eligible rewards");
        var passReload = new OfflineServer(new JsonStateStore(passDirectory), seed, "offline://resources/");
        passReload.Dispatch("game_init");
        passReload.Dispatch("login");
        Check(passReload.Dispatch("pass_card/collect_exp", arguments: new(Value: 4, PassTask: passTaskRule)).Status == 409 && passReload.Dispatch("pass_card/show_task").Body!["user_pass_card"]!["level"]!.GetValue<int>() == 2, "pass experience persists and cannot be claimed twice");
        Console.WriteLine("Passed 3 pass experience checks.");
        var passRewardRules = new[]
        {
        new LocalPassReward(1, new[] { new LocalInventoryReward(3, 0, 100, 100000) }, Array.Empty<LocalInventoryReward>()),
        new LocalPassReward(2, new[] { new LocalInventoryReward(3, 0, 100, 100000) }, new[] { new LocalInventoryReward(6, 0, 1, 100000) }),
        new LocalPassReward(3, new[] { new LocalInventoryReward(3, 0, 100, 100000) }, Array.Empty<LocalInventoryReward>())
    };
        Check(passReload.Dispatch("pass_card/collect_reward", arguments: new(2, 0, Text: "vip", PassRewards: passRewardRules)).Status == 409 && passReload.Dispatch("pass_card/collect_reward", arguments: new(3, 0, Text: "free", PassRewards: passRewardRules)).Status == 409, "pass rewards enforce entitlement and level");
        var failingPassRewards = passRewardRules.ToArray();
        failingPassRewards[1] = failingPassRewards[1] with
        {
            Free = new[]
            {
            new LocalInventoryReward(3, 0, 100, 1)
        }
        };
        Check(passReload.Dispatch("pass_card/collect_reward", arguments: new(Value: 1, PassRewards: failingPassRewards)).Status == 409, "pass reward batch preserves claims when capacity is insufficient");
        var passRewardsReply = passReload.Dispatch("pass_card/collect_reward", arguments: new(Value: 1, PassRewards: passRewardRules)).Body!;
        Check(passRewardsReply["user_update"]!["gold"]!.GetValue<int>() == 5100 && passRewardsReply["user_pass_card"]!["free_rewards"]![0]!.GetValue<int>() == 2 && passRewardsReply["user_pass_card"]!["vip_rewards"]!.AsArray().Count == 0, "pass reward batch returns only rewards actually granted");
        var passRewardsReload = new OfflineServer(new JsonStateStore(passDirectory), seed, "offline://resources/");
        passRewardsReload.Dispatch("game_init");
        passRewardsReload.Dispatch("login");
        Check(passRewardsReload.Dispatch("pass_card/collect_reward", arguments: new(2, 0, Text: "free", PassRewards: passRewardRules)).Status == 409 && passRewardsReload.Dispatch("pass_card/collect_reward", arguments: new(Value: 1, PassRewards: passRewardRules)).Body!["user_pass_card"]!["free_rewards"]!.AsArray().Count == 0, "pass reward claims persist and repeat batches do not grant again");
        Console.WriteLine("Passed 4 pass reward checks.");
        var passBuySeed = passSeed.DeepClone();
        passBuySeed["orb"]!["free"] = 25L;
        passBuySeed["orb"]!["purchased"] = 20L;
        var passBuyDirectory = directory + "-pass-buy";
        var passBuyServer = new OfflineServer(new JsonStateStore(passBuyDirectory), passBuySeed.ToJsonString(), "offline://resources/");
        passBuyServer.Dispatch("game_init");
        passBuyServer.Dispatch("login");
        Check(passBuyServer.Dispatch("pass_card/user_orb_add_level", arguments: new(Value: 21, PassRewards: passRewardRules)).Status == 400 && passBuyServer.Dispatch("pass_card/user_orb_add_level", arguments: new(Value: 1600, PassRewards: passRewardRules)).Status == 409 && passBuyServer.Dispatch("pass_card/user_orb_add_level", arguments: new(Value: 60, PassRewards: passRewardRules)).Status == 20000, "pass level purchase rejects invalid quantities, excess levels and insufficient balance");
        var passBought = passBuyServer.Dispatch("pass_card/user_orb_add_level", arguments: new(Value: 40, PassRewards: passRewardRules)).Body!;
        Check(passBought["user_pass_card"]!["level"]!.GetValue<int>() == 3 && passBought["user_pass_card"]!["current_exp"]!.GetValue<int>() == 140 && passBought["user_update"]!["orb"]!["free"]!.GetValue<long>() == 0 && passBought["user_update"]!["orb"]!["purchased"]!.GetValue<long>() == 5, "pass level purchase deducts balance and retains experience");
        var passBuyReload = new OfflineServer(new JsonStateStore(passBuyDirectory), seed, "offline://resources/");
        passBuyReload.Dispatch("game_init");
        passBuyReload.Dispatch("login");
        Check(passBuyReload.Dispatch("pass_card/show_task").Body!["user_pass_card"]!["level"]!.GetValue<int>() == 3 && passBuyReload.Dispatch("pass_card/user_orb_add_level", arguments: new(Value: 20, PassRewards: passRewardRules)).Status == 20000, "pass purchased levels and spent balance survive restart");
        Console.WriteLine("Passed 3 pass level purchase checks.");
        var passShopSeed = passSeed.DeepClone();
        passShopSeed["_local_pass_card"]!["coin"] = 15;
        passShopSeed["_local_pass_card"]!["pass_card_goods"] = new JsonArray();
        var passShopDirectory = directory + "-pass-shop";
        var passShopServer = new OfflineServer(new JsonStateStore(passShopDirectory), passShopSeed.ToJsonString(), "offline://resources/");
        passShopServer.Dispatch("game_init");
        passShopServer.Dispatch("login");
        var passGoodsRule = new LocalPassGoods(8, 10, 1, new(12, 99992, 2, 100));
        Check(passShopServer.Dispatch("pass_card/buy_goods", arguments: new(Value: 8, PassGoods: passGoodsRule with { Cost = 20 })).Status == 20010 && passShopServer.Dispatch("pass_card/buy_goods", arguments: new(Value: 8, PassGoods: passGoodsRule with { Reward = new(12, 99992, 2, 1) })).Status == 409, "pass shop validates balance and inventory before consuming coins or purchase count");
        var passGoodsReply = passShopServer.Dispatch("pass_card/buy_goods", arguments: new(Value: 8, PassGoods: passGoodsRule)).Body!;
        Check(passGoodsReply["user_pass_card"]!["coin"]!.GetValue<int>() == 5 && passGoodsReply["user_pass_card"]!["pass_card_goods"]![0]!["buy_times"]!.GetValue<int>() == 1 && passGoodsReply["user_update"]!["item"]!["item_list"]!.AsArray().Single(item => item!["item_id"]!.GetValue<int>() == 99992)!["num"]!.GetValue<int>() == 2, "pass shop buys one configured bundle and increments its purchase count");
        var passShopReload = new OfflineServer(new JsonStateStore(passShopDirectory), seed, "offline://resources/");
        passShopReload.Dispatch("game_init");
        passShopReload.Dispatch("login");
        Check(passShopReload.Dispatch("pass_card/buy_goods", arguments: new(Value: 8, PassGoods: passGoodsRule)).Status == 409 && passShopReload.Dispatch("pass_card/show_shop").Body!["user_pass_card"]!["coin"]!.GetValue<int>() == 5, "pass shop purchase limit and balance survive restart");
        Console.WriteLine("Passed 3 pass shop checks.");
        var passGiftSeed = passSeed.DeepClone();
        passGiftSeed["_local_pass_card"]!["is_vip"] = 1;
        passGiftSeed["_local_pass_card"]!["level"] = 70;
        passGiftSeed["_local_pass_card"]!["is_had_gift"] = 0;
        passGiftSeed["orb"]!["free"] = 100L;
        passGiftSeed["orb"]!["purchased"] = 100L;
        var passGiftDirectory = directory + "-pass-gift";
        var passGiftServer = new OfflineServer(new JsonStateStore(passGiftDirectory), passGiftSeed.ToJsonString(), "offline://resources/");
        passGiftServer.Dispatch("game_init");
        passGiftServer.Dispatch("login");
        Check(passGiftServer.Dispatch("pass_card/get_level_gift").Status == 503, "level gift requires reward definitions before charging");
        var passGiftReply = passGiftServer.Dispatch("pass_card/get_level_gift", arguments: new(Value: 1, PassRewards: passRewardRules)).Body!;
        Check(passGiftReply["user_pass_card"]!["level"]!.GetValue<int>() == 80 && passGiftReply["user_pass_card"]!["is_had_gift"]!.GetValue<int>() == 1 && passGiftReply["user_pass_card"]!["current_exp"]!.GetValue<int>() == 140 && passGiftReply["user_update"]!["orb"]!["free"]!.GetValue<long>() == 0 && passGiftReply["user_update"]!["orb"]!["purchased"]!.GetValue<long>() == 50, "level gift uses fixed pricing and caps gained levels while preserving experience");
        var passGiftReload = new OfflineServer(new JsonStateStore(passGiftDirectory), seed, "offline://resources/");
        passGiftReload.Dispatch("game_init");
        passGiftReload.Dispatch("login");
        Check(passGiftReload.Dispatch("pass_card/get_level_gift", arguments: new(PassRewards: passRewardRules)).Status == 409 && passGiftReload.Dispatch("login").Body!["user"]!["orb"]!["purchased"]!.GetValue<long>() == 50, "level gift purchase persists and cannot charge twice");
        Console.WriteLine("Passed 3 level gift checks.");
        Check(passBuyReload.Dispatch("pass_card/get_level_gift", arguments: new(PassRewards: passRewardRules)).Status == 409, "level gift requires local VIP entitlement");
        Check(passRewardsReload.Dispatch("pass_card/show_reward", arguments: new(PassRewards: passRewardRules)).Status == 503, "reward overview rejects missing next bonus definition");
        var passViewRules = passRewardRules.Append(new LocalPassReward(10, Array.Empty<LocalInventoryReward>(), Array.Empty<LocalInventoryReward>())).ToArray();
        var passView = passRewardsReload.Dispatch("pass_card/show_reward", arguments: new(PassRewards: passViewRules)).Body!["user_pass_card"]!;
        Check(passView["level"]!.GetValue<int>() == 2 && passView["had_got_free_reward_level"]!.AsArray().Any(id => id!.GetValue<int>() == 2) && passView["free_rewards"]!.AsArray().Count == 0, "reward overview returns saved claims without reporting new grants");
        passView["level"] = 80;
        Check(passRewardsReload.Dispatch("pass_card/show_reward", arguments: new(PassRewards: passViewRules)).Body!["user_pass_card"]!["level"]!.GetValue<int>() == 2, "reward overview returns an independent snapshot");
        Console.WriteLine("Passed 3 reward overview checks.");
        var freeItemSeed = JsonNode.Parse(seed)!;
        freeItemSeed["_local_shop_list"] = new JsonArray(new JsonObject { ["type"] = 10, ["goods_list"] = new JsonArray(new JsonObject { ["master_id"] = 9, ["end_at"] = "" }) });
        var freeItemDirectory = directory + "-free-item";
        var freeItemServer = new OfflineServer(new JsonStateStore(freeItemDirectory), freeItemSeed.ToJsonString(), "offline://resources/");
        freeItemServer.Dispatch("game_init");
        freeItemServer.Dispatch("login");
        var freeItemRule = new LocalFreeItem(9, new(3, 0, 100, 100000));
        Check(freeItemServer.Dispatch("gain_free_item", arguments: new(FreeItem: freeItemRule with { Id = 10 })).Status == 503 && freeItemServer.Dispatch("gain_free_item", arguments: new(FreeItem: freeItemRule with { Reward = new(3, 0, 100, 1) })).Status == 409, "free item rejects mismatched definitions and capacity overflow without consuming the daily claim");
        var freeItemReply = freeItemServer.Dispatch("gain_free_item", arguments: new(FreeItem: freeItemRule)).Body!["user_update"]!;
        Check(freeItemReply["gold"]!.GetValue<int>() == 5100 && !string.IsNullOrEmpty(freeItemReply["last_free_item_gain_at"]!.GetValue<string>()) && freeItemServer.Dispatch("get_shop_list").Body!["shop_list"]![0]!["goods_list"]![0]!["master_id"]!.GetValue<int>() == 9, "free item uses the advertised local definition and persists the receipt time");
        var freeItemReload = new OfflineServer(new JsonStateStore(freeItemDirectory), seed, "offline://resources/");
        freeItemReload.Dispatch("game_init");
        freeItemReload.Dispatch("login");
        Check(freeItemReload.Dispatch("gain_free_item", arguments: new(FreeItem: freeItemRule)).Status == 409 && freeItemReload.Dispatch("login").Body!["user"]!["gold"]!.GetValue<int>() == 5100, "free item cannot be received twice after restart");
        Console.WriteLine("Passed 3 free item checks.");
    }
}
